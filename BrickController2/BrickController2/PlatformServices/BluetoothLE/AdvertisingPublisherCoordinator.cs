// AdvertisingPublisherCoordinator: platform independent state machine that owns the advertising
// publisher(s) of one advertiser device (used by the WinUI BleAdvertiserDevice).
//
// Root cause it addresses (task 003-win-advertising): the old WinUI code stopped the running
// publisher and created + started a new one on EVERY data change (up to 10 Hz) without waiting for
// the asynchronous Stop()/Start() to complete or looking at the publisher status. The Windows
// Bluetooth stack queues those operations, so the data really on air lags seconds behind.
//
// This class therefore:
//  * coalesces updates ("latest wins"): intermediate payloads are dropped,
//  * applies updates strictly sequentially (one swap in flight at most),
//  * awaits the real publisher status (Started / Stopped) with a timeout,
//  * rate limits swaps (minimum interval),
//  * recovers from Aborted publishers by re-creating them (limited retries).

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BrickController2.PlatformServices.BluetoothLE
{
    /// <summary>
    /// Owns the advertising publisher(s) and serialises/coalesces payload updates.
    /// </summary>
    public sealed class AdvertisingPublisherCoordinator : IDisposable
    {
        /// <summary>Maximum automatic restarts after an Aborted publisher before waiting for the next update.</summary>
        private const int MaxAbortRetries = 3;

        /// <summary>Back-off before an automatic restart after an abort.</summary>
        private static readonly TimeSpan AbortRetryDelay = TimeSpan.FromMilliseconds(200);

        /// <summary>Payload waiting to be applied (value type, so "no pending" is a null reference).</summary>
        private sealed record Payload(ushort ManufacturerId, byte[] RawData, long Sequence, bool IsRetry);

        private readonly IAdvertisingPublisherFactory _factory;
        private readonly TimeSpan _statusTimeout;
        private readonly TimeSpan _minSwapInterval;
        private readonly ILogger? _logger;

        /// <summary>Protects all mutable state below. Never held across an await.</summary>
        private readonly object _gate = new();

        /// <summary>Cancels delays/waits when stopping or disposing.</summary>
        private CancellationTokenSource _cts = new();

        /// <summary>Strategy currently in use; may degrade from MakeBeforeBreak to StopStartAwaited.</summary>
        private AdvertisingUpdateStrategy _strategy;

        private AdvertisingInterval _interval;
        private TxPowerLevel _power;

        /// <summary>True between StartAsync and StopAsync/Dispose.</summary>
        private bool _active;

        /// <summary>Publisher that is (supposed to be) on air.</summary>
        private IAdvertisingPublisher? _current;

        /// <summary>True when <see cref="_current"/> reported Aborted and must be re-created.</summary>
        private bool _currentDead;

        /// <summary>Newest payload, kept to re-create an aborted publisher.</summary>
        private Payload? _desired;

        /// <summary>Single "latest wins" slot for the next payload to apply.</summary>
        private Payload? _pending;

        /// <summary>Running worker loop, null if idle.</summary>
        private Task? _worker;

        /// <summary>Completed when the round that takes the current pending payload is applied.</summary>
        private TaskCompletionSource _roundCompletion = NewCompletion();

        private long _sequence;
        private int _abortRetries;
        private readonly Stopwatch _sinceLastSwap = Stopwatch.StartNew();
        private bool _hasSwapped;

        /// <summary>
        /// Creates a coordinator.
        /// </summary>
        /// <param name="factory">Creates platform publishers.</param>
        /// <param name="strategy">Initial update strategy.</param>
        /// <param name="statusTimeout">Maximum time to wait for a status transition (Started/Stopped).</param>
        /// <param name="minSwapInterval">Minimum time between two publisher swaps.</param>
        /// <param name="logger">Optional logger for diagnostics.</param>
        public AdvertisingPublisherCoordinator(
            IAdvertisingPublisherFactory factory,
            AdvertisingUpdateStrategy strategy,
            TimeSpan statusTimeout,
            TimeSpan minSwapInterval,
            ILogger? logger = null)
        {
            _factory = factory;
            _strategy = strategy;
            _statusTimeout = statusTimeout;
            _minSwapInterval = minSwapInterval;
            _logger = logger;
        }

        /// <summary>
        /// Creates and starts the first publisher and waits until it is on air (or the timeout elapsed).
        /// </summary>
        public async Task StartAsync(AdvertisingInterval interval, TxPowerLevel power, ushort manufacturerId, byte[] rawData)
        {
            await StopAsync().ConfigureAwait(false); // restart semantics: never leak an older publisher

            CancellationToken token;
            lock (_gate)
            {
                _cts.Dispose();
                _cts = new CancellationTokenSource();
                token = _cts.Token;
                _interval = interval;
                _power = power;
                _active = true;
                _currentDead = false;
                _abortRetries = 0;
                _desired = new Payload(manufacturerId, rawData, ++_sequence, false);
            }

            var publisher = await CreateAndStartAsync(manufacturerId, rawData, token).ConfigureAwait(false);
            PublishCurrent(publisher);
        }

        /// <summary>
        /// Queues a new payload. Older not yet applied payloads are dropped. The returned task completes
        /// when this or a newer payload has been applied (or the attempt failed/timed out), which gives the
        /// caller natural back-pressure. Does nothing when not started.
        /// </summary>
        public Task UpdateAsync(ushort manufacturerId, byte[] rawData)
        {
            lock (_gate)
            {
                if (!_active)
                {
                    return Task.CompletedTask;
                }

                var payload = new Payload(manufacturerId, rawData, ++_sequence, false);
                if (_pending != null)
                {
                    _logger?.LogDebug("AdvUpdate #{Seq} replaces not yet applied #{Old} (coalesced)", payload.Sequence, _pending.Sequence);
                }

                _desired = payload;
                _pending = payload;
                _abortRetries = 0; // fresh data from the app: allow automatic restarts again
                EnsureWorkerLocked();
                return _roundCompletion.Task;
            }
        }

        /// <summary>
        /// Stops all publishers and waits (bounded) until they are stopped. Idempotent.
        /// </summary>
        public async Task StopAsync()
        {
            Task? worker;
            lock (_gate)
            {
                _active = false;
                _pending = null;
                _desired = null;
                worker = _worker;
                _cts.Cancel(); // wakes delays/waits of the worker so it ends quickly
            }

            if (worker != null)
            {
                await worker.ConfigureAwait(false); // worker never throws
            }

            IAdvertisingPublisher? current;
            lock (_gate)
            {
                current = _current;
                _current = null;
            }

            if (current != null)
            {
                // use a fresh token: _cts is already cancelled but we still want to await Stopped
                using var stopCts = new CancellationTokenSource();
                await StopAndDisposeAsync(current, stopCts.Token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Synchronous shutdown: cancels everything and stops publishers without waiting. Never throws.
        /// </summary>
        public void Dispose()
        {
            IAdvertisingPublisher? current;
            TaskCompletionSource waiters;
            lock (_gate)
            {
                _active = false;
                _pending = null;
                _desired = null;
                current = _current;
                _current = null;
                waiters = _roundCompletion;
                _roundCompletion = NewCompletion();
                _cts.Cancel();
            }

            waiters.TrySetResult();
            StopAndDisposeQuietly(current);
        }

        // ---------------------------------------------------------------- worker

        /// <summary>Starts the worker loop if it is not running. Caller holds <see cref="_gate"/>.</summary>
        private void EnsureWorkerLocked()
        {
            if (_worker == null)
            {
                _worker = Task.Run(WorkerLoopAsync);
            }
        }

        /// <summary>
        /// Applies pending payloads one after another until the slot is empty. Never throws.
        /// </summary>
        private async Task WorkerLoopAsync()
        {
            while (true)
            {
                Payload payload;
                TaskCompletionSource round;
                CancellationToken token;
                lock (_gate)
                {
                    if (_pending == null || !_active)
                    {
                        _worker = null;
                        // nobody must hang on a payload that will never be applied
                        _roundCompletion.TrySetResult();
                        _roundCompletion = NewCompletion();
                        return;
                    }

                    payload = _pending;
                    _pending = null;
                    token = _cts.Token;

                    // callers that queued before this moment are served by this round; later ones by the next
                    round = _roundCompletion;
                    _roundCompletion = NewCompletion();
                }

                try
                {
                    await ApplyAsync(payload, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // stop/dispose requested - the loop ends on the next iteration
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "AdvUpdate #{Seq} failed", payload.Sequence);
                }
                finally
                {
                    round.TrySetResult();
                }
            }
        }

        /// <summary>Applies one payload with the configured strategy and logs the timing.</summary>
        private async Task ApplyAsync(Payload payload, CancellationToken token)
        {
            var total = Stopwatch.StartNew();

            // rate limit: drop-free because newer payloads simply overwrite the pending slot meanwhile
            await WaitMinSwapIntervalAsync(payload.IsRetry, token).ConfigureAwait(false);

            // a newer payload may have arrived while waiting; use it instead (latest wins)
            lock (_gate)
            {
                if (_pending != null)
                {
                    _logger?.LogDebug("AdvUpdate #{Seq} superseded by #{New} before swap", payload.Sequence, _pending.Sequence);
                    payload = _pending;
                    _pending = null;
                }
            }

            _logger?.LogDebug("AdvUpdate #{Seq} swap start (strategy {Strategy}, {Len} bytes)", payload.Sequence, _strategy, payload.RawData.Length);

            IAdvertisingPublisher? old;
            bool dead;
            lock (_gate)
            {
                old = _current;
                dead = _currentDead;
            }

            if (old == null || dead)
            {
                await ReplaceBrokenAsync(old, payload, token).ConfigureAwait(false);
            }
            else if (_strategy == AdvertisingUpdateStrategy.InPlace && TryInPlace(old, payload))
            {
                // payload swapped without touching the stack
            }
            else if (_strategy != AdvertisingUpdateStrategy.StopStartAwaited)
            {
                await MakeBeforeBreakAsync(old, payload, token).ConfigureAwait(false);
            }
            else
            {
                await StopStartAsync(old, payload, token).ConfigureAwait(false);
            }

            _hasSwapped = true;
            _sinceLastSwap.Restart();
            _logger?.LogDebug("AdvUpdate #{Seq} applied (total {Ms} ms)", payload.Sequence, total.ElapsedMilliseconds);
        }

        /// <summary>Waits until at least the minimum swap interval has passed since the last swap.</summary>
        private async Task WaitMinSwapIntervalAsync(bool isRetry, CancellationToken token)
        {
            var wait = TimeSpan.Zero;
            if (_hasSwapped)
            {
                wait = _minSwapInterval - _sinceLastSwap.Elapsed;
            }

            if (isRetry && wait < AbortRetryDelay)
            {
                wait = AbortRetryDelay;
            }

            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, token).ConfigureAwait(false);
            }
        }

        // ---------------------------------------------------------------- strategies

        /// <summary>In-place payload replacement; returns false (caller falls back) when rejected.</summary>
        private bool TryInPlace(IAdvertisingPublisher publisher, Payload payload)
        {
            bool ok;
            try
            {
                ok = _factory.TryUpdateInPlace(publisher, payload.ManufacturerId, payload.RawData);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "In-place update threw, falling back to publisher swap");
                ok = false;
            }

            if (!ok)
            {
                _logger?.LogDebug("In-place update rejected, falling back to MakeBeforeBreak");
            }

            return ok;
        }

        /// <summary>New publisher first, old one only after the new one is on air.</summary>
        private async Task MakeBeforeBreakAsync(IAdvertisingPublisher old, Payload payload, CancellationToken token)
        {
            IAdvertisingPublisher? fresh = null;
            try
            {
                fresh = await CreateAndStartAsync(payload.ManufacturerId, payload.RawData, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Starting second publisher failed");
            }

            if (fresh != null && fresh.Status == AdvertisingPublisherStatus.Started)
            {
                if (PublishCurrent(fresh))
                {
                    await StopAndDisposeAsync(old, token).ConfigureAwait(false);
                }

                return;
            }

            // The adapter does not allow two publishers (or the new one timed out): degrade permanently
            _logger?.LogWarning("MakeBeforeBreak failed (status {Status}); falling back to StopStartAwaited", fresh?.Status);
            _strategy = AdvertisingUpdateStrategy.StopStartAwaited;
            StopAndDisposeQuietly(fresh);
            await StopStartAsync(old, payload, token).ConfigureAwait(false);
        }

        /// <summary>Old publisher fully stopped first, then the new one started; both awaited.</summary>
        private async Task StopStartAsync(IAdvertisingPublisher old, Payload payload, CancellationToken token)
        {
            lock (_gate)
            {
                if (ReferenceEquals(_current, old))
                {
                    _current = null; // nothing on air from now on
                }
            }

            await StopAndDisposeAsync(old, token).ConfigureAwait(false);
            await StartFreshAsync(payload, token).ConfigureAwait(false);
        }

        /// <summary>Replaces a missing or aborted publisher (nothing healthy is on air).</summary>
        private async Task ReplaceBrokenAsync(IAdvertisingPublisher? broken, Payload payload, CancellationToken token)
        {
            lock (_gate)
            {
                _current = null;
            }

            if (broken != null)
            {
                await StopAndDisposeAsync(broken, token).ConfigureAwait(false);
            }

            await StartFreshAsync(payload, token).ConfigureAwait(false);
        }

        private async Task StartFreshAsync(Payload payload, CancellationToken token)
        {
            var fresh = await CreateAndStartAsync(payload.ManufacturerId, payload.RawData, token).ConfigureAwait(false);
            PublishCurrent(fresh);
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Creates a publisher, subscribes to its status, starts it and waits for Started/Aborted/timeout.
        /// </summary>
        private async Task<IAdvertisingPublisher> CreateAndStartAsync(ushort manufacturerId, byte[] rawData, CancellationToken token)
        {
            var publisher = _factory.Create(manufacturerId, rawData, _interval, _power);
            publisher.StatusChanged += status => OnPublisherStatusChanged(publisher, status);

            var watch = Stopwatch.StartNew();
            publisher.Start();
            AdvertisingPublisherStatus? reached;
            try
            {
                reached = await WaitForStatusAsync(publisher, AdvertisingPublisherStatus.Started, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                StopAndDisposeQuietly(publisher); // do not leave an orphaned publisher on air
                throw;
            }

            _logger?.LogDebug("Publisher start finished with {Status} after {Ms} ms", reached?.ToString() ?? "timeout", watch.ElapsedMilliseconds);

            if (reached == AdvertisingPublisherStatus.Started)
            {
                _abortRetries = 0;
            }
            else if (reached == null)
            {
                _logger?.LogWarning("Publisher did not report Started within {Timeout} ms", _statusTimeout.TotalMilliseconds);
            }

            return publisher;
        }

        /// <summary>
        /// Makes <paramref name="publisher"/> the current one. If the coordinator was stopped meanwhile the
        /// publisher is shut down instead and false is returned.
        /// </summary>
        private bool PublishCurrent(IAdvertisingPublisher publisher)
        {
            lock (_gate)
            {
                if (_active)
                {
                    _current = publisher;
                    _currentDead = publisher.Status == AdvertisingPublisherStatus.Aborted;
                    return true;
                }
            }

            StopAndDisposeQuietly(publisher);
            return false;
        }

        /// <summary>Stops a publisher, waits (bounded) for Stopped and disposes it.</summary>
        private async Task StopAndDisposeAsync(IAdvertisingPublisher publisher, CancellationToken token)
        {
            var watch = Stopwatch.StartNew();
            try
            {
                publisher.Stop();
                var reached = await WaitForStatusAsync(publisher, AdvertisingPublisherStatus.Stopped, token).ConfigureAwait(false);
                _logger?.LogDebug("Publisher stop finished with {Status} after {Ms} ms", reached?.ToString() ?? "timeout", watch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Stopping publisher failed");
            }
            finally
            {
                StopAndDisposeQuietly(publisher, stop: false);
            }
        }

        /// <summary>Best effort stop + dispose that never throws and never waits.</summary>
        private void StopAndDisposeQuietly(IAdvertisingPublisher? publisher, bool stop = true)
        {
            if (publisher == null)
            {
                return;
            }

            try
            {
                if (stop)
                {
                    publisher.Stop();
                }

                publisher.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Ignoring error while disposing publisher");
            }
        }

        /// <summary>
        /// Waits until the publisher reports <paramref name="wanted"/> or Aborted, at most the status timeout.
        /// Returns the reached status or null on timeout. Throws OperationCanceledException when cancelled.
        /// </summary>
        private async Task<AdvertisingPublisherStatus?> WaitForStatusAsync(IAdvertisingPublisher publisher, AdvertisingPublisherStatus wanted, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<AdvertisingPublisherStatus>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Check(AdvertisingPublisherStatus status)
            {
                // a never started publisher counts as "stopped"
                bool isWanted = status == wanted ||
                    (wanted == AdvertisingPublisherStatus.Stopped && status == AdvertisingPublisherStatus.Created);
                if (isWanted || status == AdvertisingPublisherStatus.Aborted)
                {
                    tcs.TrySetResult(status);
                }
            }

            publisher.StatusChanged += Check;
            try
            {
                Check(publisher.Status); // status may already have changed before we subscribed

                using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                var finished = await Task.WhenAny(tcs.Task, Task.Delay(_statusTimeout, delayCts.Token)).ConfigureAwait(false);
                delayCts.Cancel(); // release the timer

                token.ThrowIfCancellationRequested();
                return finished == tcs.Task ? tcs.Task.Result : null;
            }
            finally
            {
                publisher.StatusChanged -= Check;
            }
        }

        /// <summary>
        /// Logs every status change and re-creates the current publisher if it aborted (bounded retries).
        /// </summary>
        private void OnPublisherStatusChanged(IAdvertisingPublisher publisher, AdvertisingPublisherStatus status)
        {
            _logger?.LogDebug("Publisher status changed to {Status}", status);

            if (status != AdvertisingPublisherStatus.Aborted)
            {
                return;
            }

            lock (_gate)
            {
                if (!_active || !ReferenceEquals(publisher, _current))
                {
                    return; // an old/failed-to-start publisher; handled by the strategy code
                }

                _currentDead = true;
                _logger?.LogError("Current advertising publisher aborted (retry {Retry}/{Max})", _abortRetries + 1, MaxAbortRetries);

                if (_pending == null && _desired != null && _abortRetries < MaxAbortRetries)
                {
                    _abortRetries++;
                    _pending = _desired with { Sequence = ++_sequence, IsRetry = true };
                    EnsureWorkerLocked();
                }
            }
        }

        private static TaskCompletionSource NewCompletion()
            => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
