// One GATT link of the stick (one conn_id): CONNECT, DISCOVER, READ, WRITE, SUBSCRIBE and the
// ordered delivery of notifications. Pure protocol logic, no BC2 device semantics (see StickGattDevice).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using BrickController2.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>Result of a GATT operation: the stick status plus the ATT error code when the peer refused.</summary>
internal readonly record struct GattResult(StickStatus Status, byte AttError = 0)
{
    /// <summary>The operation succeeded.</summary>
    public bool IsOk => Status == StickStatus.Ok;

    /// <summary>Successful result.</summary>
    public static GattResult Ok => new(StickStatus.Ok);
}

/// <summary>
/// A connection slot (conn_id) of the stick and everything that happens on it. The slot is reserved by
/// <see cref="UsbStickConnection.TryAcquireLink"/> and given back by <see cref="Dispose"/>.
/// <para>
/// Rules implemented here (protocol 1.1, tasks/009-usb-stick-gatt-fw/plan.md section 3):
/// requests are never retransmitted (a duplicate write would be harmful); only WRITE_CMD is retried when the
/// stick answers BUSY (it was not queued then); every request has an operation specific timeout; events of
/// the stick arrive on the transport read thread, so notifications go through a per-link channel with one
/// reader task: the read loop never blocks and the order is kept.
/// </para>
/// </summary>
internal sealed class StickGattLink : IDisposable
{
    /// <summary>Time the stick gets to acknowledge a request that is answered immediately.</summary>
    internal static readonly TimeSpan ImmediateAckTimeout = TimeSpan.FromMilliseconds(250);

    /// <summary>Time for READ / WRITE / SUBSCRIBE (the stick itself gives up after 5 s).</summary>
    internal static readonly TimeSpan ProcedureTimeout = TimeSpan.FromSeconds(6);

    /// <summary>Time for a complete discovery.</summary>
    internal static readonly TimeSpan DiscoverTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Extra time on top of the stick's own connect timeout before the host gives up waiting.</summary>
    private static readonly TimeSpan ConnectGuard = TimeSpan.FromSeconds(3);

    /// <summary>Time to wait for EVT_DISCONNECTED after a DISCONNECT before the conn_id is reused.</summary>
    private static readonly TimeSpan DisconnectConfirmTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Retries of a WRITE_CMD that the stick rejected with BUSY, and the pause between them (protocol: >= 5 ms).</summary>
    private const int BusyRetries = 5;
    private static readonly TimeSpan BusyRetryDelay = TimeSpan.FromMilliseconds(6);

    private enum LinkState { Idle, Connecting, Connected, Closed }

    private readonly UsbStickConnection _connection;
    private readonly TimeProvider _clock;
    private readonly ILogger _logger;
    private readonly object _lock = new();

    // Ordered notification delivery: written by the read thread, read by exactly one task.
    private readonly Channel<StickNotify> _notifications = Channel.CreateUnbounded<StickNotify>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true, AllowSynchronousContinuations = false });

    private LinkState _state = LinkState.Idle;
    private TaskCompletionSource<StickDisconnected?>? _connectTcs;   // result of CONNECT: null = connected
    private TaskCompletionSource<bool>? _closedTcs;                   // completes when the link ended
    private DiscoveryCollector? _discovery;
    private byte _disconnectReason;
    private int _mtu = StickProtocol.DefaultMtu;
    private int _released;

    internal StickGattLink(UsbStickConnection connection, byte connId, TimeProvider clock, ILogger logger)
    {
        _connection = connection;
        ConnId = connId;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>The conn_id of this link on the stick (0 .. MaxConns-1).</summary>
    public byte ConnId { get; }

    /// <summary>Diagnostic: writes a trace line prefixed with this link's conn_id and state.</summary>
    private void Trace(string message) => UsbStickTrace.Write($"conn {ConnId} [{_state}]: {message}");

    /// <summary>Handler called, in order, for every notification (value handle, data). Set before connecting.</summary>
    public Action<ushort, byte[]>? NotificationHandler { get; set; }

    /// <summary>Raised once when a connected link ended without a local DISCONNECT (peer, stick, unplug). Argument: HCI/stick reason.</summary>
    public event Action<byte>? LinkLost;

    /// <summary>Negotiated MTU of the connected link.</summary>
    public int Mtu => Volatile.Read(ref _mtu);

    /// <summary>True while the link is connected.</summary>
    public bool IsConnected
    {
        get
        {
            lock (_lock)
            {
                return _state == LinkState.Connected;
            }
        }
    }

    /// <summary>Largest write payload: min(max_value_len of the stick, mtu - 3).</summary>
    public int MaxPayload => Math.Min(_connection.Info?.MaxValueLen ?? 0, Mtu - 3);

    // ------------------------------------------------------------------ connect / disconnect

    /// <summary>
    /// Connects to the peripheral and waits for EVT_CONNECTED. Cancelling while connecting sends DISCONNECT.
    /// </summary>
    /// <param name="address">Peer address, 6 bytes in display order.</param>
    /// <param name="addressType">0 public, 1 random, 0xFF = let the stick decide from its scan cache.</param>
    /// <param name="background">Background connect (no timeout, runs until connected or cancelled).</param>
    /// <param name="cancellationToken">Cancels the attempt.</param>
    /// <returns>Ok, or the reason the link could not be established (Cancelled shows as <see cref="StickStatus.Disconnected"/>).</returns>
    public async Task<GattResult> ConnectAsync(byte[] address, byte addressType, bool background, CancellationToken cancellationToken)
    {
        TaskCompletionSource<StickDisconnected?> connectTcs;
        lock (_lock)
        {
            if (_state != LinkState.Idle)
            {
                return new GattResult(StickStatus.AlreadyRunning);
            }

            // Armed BEFORE the request goes out: the stick's events may be processed before our await resumes.
            _state = LinkState.Connecting;
            _closedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            connectTcs = _connectTcs = new TaskCompletionSource<StickDisconnected?>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        byte flags = background ? StickProtocol.ConnectFlagBackground : (byte)0;
        Trace($"ConnectAsync start: peer={StickAddress.Format(address)} addressType={addressType} background={background}");
        byte[] payload = StickFrameEncoder.ConnectPayload(ConnId, address, addressType, flags, 0, 0, 0);

        try
        {
            var (ack, _) = await SendAsync(StickProtocol.TypeConnect, payload, ImmediateAckTimeout, cancellationToken).ConfigureAwait(false);
            if (!ack.IsOk)
            {
                Trace($"CONNECT rejected by the stick: status={ack.Status} attError=0x{ack.AttError:X2} -> connect failed");
                MarkClosed();
                return ack;
            }


            // The stick enforces its own connect timeout (default 10 s) and reports it as EVT_DISCONNECTED 0xF0;
            // the guard only protects against a stick that never answers.
            TimeSpan wait = background ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(10) + ConnectGuard;
            StickDisconnected? failure = await connectTcs.Task.WaitAsync(wait, _clock, cancellationToken).ConfigureAwait(false);
            if (failure != null)
            {
                Trace($"connect attempt ended before EVT_CONNECTED: reason={UsbStickTrace.ReasonName(failure.Reason)} wasConnected={failure.WasConnected}");
                MarkClosed();
                _logger.LogInformation("USB stick: connect to {Address} failed (reason 0x{Reason:X2})", StickAddress.Format(address), failure.Reason);
                return new GattResult(StickStatus.Disconnected);
            }

            return GattResult.Ok;
        }
        catch (OperationCanceledException)
        {
            // Cancel while connecting: tell the stick to abort the attempt (best effort) and report the failure.
            await AbortConnectAsync().ConfigureAwait(false);
            throw;
        }
        catch (TimeoutException)
        {
            Trace("ConnectAsync: host guard timeout, the stick sent neither EVT_CONNECTED nor EVT_DISCONNECTED");
            await AbortConnectAsync().ConfigureAwait(false);
            return new GattResult(StickStatus.Timeout);
        }
    }

    /// <summary>
    /// Sends DISCONNECT for a connect attempt that is no longer wanted, marks the link closed and waits (bounded)
    /// for the stick's EVT_DISCONNECTED. The caller returns the conn_id only afterwards: otherwise a quick reconnect
    /// on the same id could be answered with ALREADY_RUNNING or be failed by the late event of this attempt.
    /// </summary>
    private async Task AbortConnectAsync()
    {
        bool wasConnected;
        Task<bool>? closed;
        lock (_lock)
        {
            wasConnected = _state == LinkState.Connected;
            closed = _closedTcs?.Task;
            _state = LinkState.Closed;
            _discovery?.Fail(StickStatus.Disconnected);

            // _closedTcs is NOT completed here: it stands for "the stick confirmed the end".
        }

        _notifications.Writer.TryComplete();
        if (await SendBestEffortAsync(StickProtocol.TypeDisconnect, StickFrameEncoder.ConnIdPayload(ConnId)).ConfigureAwait(false) && closed != null)
        {
            await WaitForStickConfirmationAsync(closed).ConfigureAwait(false);
        }

        _logger.LogDebug("USB stick: connect on conn {ConnId} aborted (connected: {Connected})", ConnId, wasConnected);
    }

    /// <summary>Waits up to <see cref="DisconnectConfirmTimeout"/> for the stick's EVT_DISCONNECTED (or a stick loss).</summary>
    private async Task WaitForStickConfirmationAsync(Task<bool> closed)
    {
        try
        {
            await closed.WaitAsync(DisconnectConfirmTimeout, _clock).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _logger.LogDebug("USB stick: no EVT_DISCONNECTED for conn {ConnId}", ConnId);
        }
    }

    /// <summary>
    /// Ends the link (idempotent). Pending requests complete with an error. Waits briefly for the stick's
    /// EVT_DISCONNECTED so the conn_id can be reused right away.
    /// </summary>
    public async Task DisconnectAsync() => await CloseAsync().ConfigureAwait(false);

    /// <summary>
    /// Ends the link because the policy changed (the setting no longer allows GATT through the stick): the
    /// peer is really disconnected on the stick (so it advertises again and Windows can find it), and the owner
    /// gets the normal <see cref="LinkLost"/> event, i.e. BC2 sees an ordinary disconnect and reconnects.
    /// </summary>
    /// <returns>True when an established link was closed (the owner was notified).</returns>
    internal async Task<bool> CloseByPolicyAsync()
    {
        if (!await CloseAsync().ConfigureAwait(false))
        {
            return false;
        }

        LinkLost?.Invoke(StickProtocol.ReasonCancelled);
        return true;
    }

    /// <summary>Shared part of <see cref="DisconnectAsync"/> and <see cref="CloseByPolicyAsync"/>.</summary>
    /// <returns>True when the link was established (Connected) before it was closed.</returns>
    private async Task<bool> CloseAsync()
    {
        Task<bool>? closed;
        bool wasConnected;
        lock (_lock)
        {
            if (_state == LinkState.Idle || _state == LinkState.Closed)
            {
                _state = LinkState.Closed;
                return false;
            }

            wasConnected = _state == LinkState.Connected;
            closed = _closedTcs?.Task;
            Trace("link down: local disconnect requested");
            _state = LinkState.Closed;
            _discovery?.Fail(StickStatus.Disconnected);
            _connectTcs?.TrySetResult(new StickDisconnected(ConnId, false, StickProtocol.ReasonCancelled));
        }

        _notifications.Writer.TryComplete();

        if (await SendBestEffortAsync(StickProtocol.TypeDisconnect, StickFrameEncoder.ConnIdPayload(ConnId)).ConfigureAwait(false) && closed != null)
        {
            await WaitForStickConfirmationAsync(closed).ConfigureAwait(false);
        }

        return wasConnected;
    }

    // ------------------------------------------------------------------ discovery

    /// <summary>
    /// Discovers all services and characteristics. Events are collected into the tree; characteristics are
    /// assigned to services by handle range.
    /// </summary>
    public async Task<(GattResult Result, IReadOnlyList<StickGattService> Services)> DiscoverAsync(CancellationToken cancellationToken)
    {
        DiscoveryCollector collector;
        lock (_lock)
        {
            if (_state != LinkState.Connected)
            {
                return (new GattResult(StickStatus.NotConnected), []);
            }

            collector = _discovery = new DiscoveryCollector();
        }

        long started = _clock.GetTimestamp();
        try
        {
            var (ack, _) = await SendAsync(StickProtocol.TypeDiscover, StickFrameEncoder.ConnIdPayload(ConnId), ImmediateAckTimeout, cancellationToken).ConfigureAwait(false);
            if (!ack.IsOk)
            {
                Trace($"DISCOVER rejected: status={ack.Status} -> discovery failed");
                return (ack, []);
            }

            StickStatus status = await collector.Done.WaitAsync(DiscoverTimeout, _clock, cancellationToken).ConfigureAwait(false);
            if (status != StickStatus.Ok)
            {
                Trace($"DiscoverAsync failed after {_clock.GetElapsedTime(started).TotalMilliseconds:F0} ms: EVT_DISCOVER_DONE/abort status={status}");
                return (new GattResult(status), []);
            }

            IReadOnlyList<StickGattService> services = collector.BuildServices();
            Trace($"discovery completed in {_clock.GetElapsedTime(started).TotalMilliseconds:F0} ms: {services.Count} service(s), {services.Sum(x => x.Characteristics.Count)} characteristic(s)");
            return (GattResult.Ok, services);
        }
        catch (TimeoutException)
        {
            Trace($"DiscoverAsync TIMEOUT after {DiscoverTimeout.TotalSeconds:F0} s (no EVT_DISCOVER_DONE)");
            return (new GattResult(StickStatus.Timeout), []);
        }
        finally
        {
            lock (_lock)
            {
                if (ReferenceEquals(_discovery, collector))
                {
                    _discovery = null;
                }
            }
        }
    }

    // ------------------------------------------------------------------ read / write / subscribe

    /// <summary>ATT read of a characteristic value.</summary>
    public async Task<(GattResult Result, byte[]? Data)> ReadAsync(ushort handle, CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            return (new GattResult(StickStatus.NotConnected), null);
        }

        var (result, frame) = await SendAsync(StickProtocol.TypeRead, StickFrameEncoder.ReadPayload(ConnId, handle), ProcedureTimeout, cancellationToken).ConfigureAwait(false);
        if (result.IsOk && frame != null && StickResponseDecoder.TryDecodeGatt(frame, out var message) && message is StickReadResponse read)
        {
            return (GattResult.Ok, read.Data);
        }

        Trace($"ReadAsync FAILED handle=0x{handle:X4} status={result.Status} attError=0x{result.AttError:X2} responseFrame={(frame == null ? "none" : UsbStickTrace.FrameName(frame.Type))}");

        return (result.IsOk ? new GattResult(StickStatus.BleError) : result, null);
    }

    /// <summary>ATT write with response; completes when the peer confirmed.</summary>
    public Task<GattResult> WriteAsync(ushort handle, byte[] data, CancellationToken cancellationToken)
        => WriteCoreAsync(StickProtocol.TypeWrite, handle, data, ProcedureTimeout, 1, cancellationToken);

    /// <summary>ATT write without response; completes when the stick queued it (BUSY is retried a few times).</summary>
    public Task<GattResult> WriteCommandAsync(ushort handle, byte[] data, CancellationToken cancellationToken)
        => WriteCoreAsync(StickProtocol.TypeWriteCmd, handle, data, ImmediateAckTimeout, 1 + BusyRetries, cancellationToken);

    private async Task<GattResult> WriteCoreAsync(byte type, ushort handle, byte[] data, TimeSpan timeout, int attempts, CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            return new GattResult(StickStatus.NotConnected);
        }


        // Reject oversized data without touching the wire (the stick would answer PAYLOAD_TOO_LARGE anyway).
        if (data.Length == 0 || data.Length > MaxPayload)
        {
            Trace($"write of {data.Length} bytes rejected locally (limit {MaxPayload})");
            _logger.LogWarning("USB stick: write of {Length} bytes rejected (limit {Limit})", data.Length, MaxPayload);
            return new GattResult(StickStatus.PayloadTooLarge);
        }

        byte[] payload = StickFrameEncoder.WritePayload(ConnId, handle, data);
        GattResult result = default;
        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            (result, _) = await SendAsync(type, payload, timeout, cancellationToken).ConfigureAwait(false);

            // BUSY means "not queued": only then is it safe to send the same write again.
            if (result.Status != StickStatus.Busy || attempt == attempts)
            {
                break;
            }

            await Task.Delay(BusyRetryDelay, _clock, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>Writes the CCCD: mode 0 = off, 1 = notify, 2 = indicate.</summary>
    public async Task<GattResult> SubscribeAsync(ushort cccdHandle, byte mode, CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            return new GattResult(StickStatus.NotConnected);
        }

        var (result, _) = await SendAsync(StickProtocol.TypeSubscribe, StickFrameEncoder.SubscribePayload(ConnId, cccdHandle, mode), ProcedureTimeout, cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Registers a "safe state" write the stick executes when it terminates the link on its own (keep-alive
    /// timeout, button, ...). Empty data clears the slot. Not used by BC2 yet (product owner decision pending).
    /// </summary>
    public async Task<GattResult> SetFailsafeAsync(byte slot, ushort handle, byte[] data, CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            return new GattResult(StickStatus.NotConnected);
        }

        var (result, _) = await SendAsync(StickProtocol.TypeSetFailsafe, StickFrameEncoder.SetFailsafePayload(ConnId, slot, handle, data), ImmediateAckTimeout, cancellationToken).ConfigureAwait(false);
        return result;
    }

    // ------------------------------------------------------------------ request plumbing

    /// <summary>
    /// Sends one request (single attempt) and maps the answer: ACK status to a <see cref="GattResult"/>, other
    /// response frames are handed back to the caller.
    /// </summary>
    private async Task<(GattResult Result, StickFrame? Frame)> SendAsync(byte type, byte[] payload, TimeSpan timeout, CancellationToken cancellationToken)
    {
        long started = _clock.GetTimestamp();
        try
        {
            StickFrame frame = await _connection.RequestOnceAsync(type, payload, timeout, cancellationToken).ConfigureAwait(false);
            if (frame.Type != StickProtocol.TypeAck)
            {
                return (GattResult.Ok, frame);
            }

            StickResponseDecoder.TryDecodeAck(frame, out var ack);

            // The ACK names the request it answers: anything else is an answer to a different request (a sequence
            // number mix-up) and must not be taken as the result of this one.
            if (ack!.OriginalType != type)
            {
                _logger.LogWarning("USB stick: ACK for 0x{Got:X2} received for request 0x{Expected:X2} on conn {ConnId}", ack.OriginalType, type, ConnId);
                return (new GattResult(StickStatus.BleError), null);
            }

            byte attError = ack.Status == StickStatus.AttError && ack.Extra.Length > 0 ? ack.Extra[0] : (byte)0;
            if (ack.Status != StickStatus.Ok)
            {
                Trace($"{UsbStickTrace.FrameName(type)} procedure error: status={ack.Status} attError=0x{attError:X2} after {_clock.GetElapsedTime(started).TotalMilliseconds:F0} ms");
            }
            return (new GattResult(ack.Status, attError), null);
        }
        catch (StickTimeoutException)
        {
            Trace($"{UsbStickTrace.FrameName(type)} PROCEDURE TIMEOUT after {_clock.GetElapsedTime(started).TotalMilliseconds:F0} ms (limit {timeout.TotalMilliseconds:F0} ms)");
            _logger.LogWarning("USB stick: request 0x{Type:X2} on conn {ConnId} timed out", type, ConnId);
            return (new GattResult(StickStatus.Timeout), null);
        }
        catch (StickException ex)
        {
            // Session closed / stick unplugged: the link is gone as well.
            Trace($"{UsbStickTrace.FrameName(type)} failed, stick session error: {ex.GetType().Name}: {ex.Message}");
            _logger.LogDebug("USB stick: request 0x{Type:X2} failed: {Message}", type, ex.Message);
            return (new GattResult(StickStatus.Disconnected), null);
        }
    }

    /// <summary>Sends a request whose result does not matter; returns true if the stick received it.</summary>
    private async Task<bool> SendBestEffortAsync(byte type, byte[] payload)
    {
        try
        {
            await _connection.RequestOnceAsync(type, payload, ImmediateAckTimeout, CancellationToken.None).ConfigureAwait(false);
            return true;
        }
        catch (StickException)
        {
            return false;
        }
    }

    // ------------------------------------------------------------------ events from the stick (read thread)

    /// <summary>Handles an event of this connection. Runs on the transport read thread and never blocks.</summary>
    internal void OnMessage(StickGattMessage message)
    {
        switch (message)
        {
            case StickConnected connected:
                OnConnected(connected);
                break;

            case StickDisconnected disconnected:
                OnDisconnected(disconnected);
                break;

            case StickNotify notify:
                // Written in arrival order; the reader task delivers it. Failure only after the link ended.
                _notifications.Writer.TryWrite(notify);
                break;

            case StickGattServiceEvent or StickGattCharEvent or StickDiscoverDone:
                lock (_lock)
                {
                    _discovery?.Add(message);
                }

                break;
        }
    }

    private void OnConnected(StickConnected connected)
    {
        lock (_lock)
        {
            if (_state != LinkState.Connecting)
            {
                return;
            }

            Trace($"link up: peer={connected.Address} mtu={connected.Mtu} interval={connected.Interval * 1.25:F2} ms");
            Volatile.Write(ref _mtu, connected.Mtu);
            _state = LinkState.Connected;
            _connectTcs?.TrySetResult(null);
        }

        // The notification pump runs from now on, so nothing that arrives next to the SUBSCRIBE ACK is lost.
        _ = Task.Run(PumpNotificationsAsync);
    }

    private void OnDisconnected(StickDisconnected disconnected)
    {
        bool lost;
        lock (_lock)
        {
            // Stale events of an earlier use of this conn_id (aborted attempt, link ended before a HELLO) must not
            // end the current link: this link never asked for a DISCONNECT (that closes it locally first), so
            // "cancelled" is not ours, and a "was not connected" event cannot belong to an established link.
            bool stale = (_state is LinkState.Connecting or LinkState.Connected && disconnected.Reason == StickProtocol.ReasonCancelled)
                || (_state == LinkState.Connected && !disconnected.WasConnected);
            Trace($"link down: reason={UsbStickTrace.ReasonName(disconnected.Reason)} wasConnected={disconnected.WasConnected} stale={stale}");
            if (stale)
            {
                _logger.LogDebug("USB stick: stale EVT_DISCONNECTED (reason 0x{Reason:X2}) ignored on conn {ConnId}", disconnected.Reason, ConnId);
                return;
            }

            _disconnectReason = disconnected.Reason;
            lost = _state == LinkState.Connected;

            if (_state == LinkState.Connecting)
            {
                _connectTcs?.TrySetResult(disconnected);
            }

            _state = LinkState.Closed;
            _discovery?.Fail(StickStatus.Disconnected);
            _closedTcs?.TrySetResult(true);
        }

        _notifications.Writer.TryComplete();
        if (lost)
        {
            LinkLost?.Invoke(disconnected.Reason);
        }
    }

    /// <summary>The stick (session) is gone: the link is dead without any further event.</summary>
    internal void OnStickLost()
    {
        bool lost;
        lock (_lock)
        {
            // A link that was never connected has nothing to lose: staying Idle lets its first ConnectAsync run
            // (and fail on its own if the stick is really gone) instead of failing with AlreadyRunning.
            if (_state == LinkState.Idle)
            {
                return;
            }

            lost = _state == LinkState.Connected;
            _disconnectReason = StickProtocol.ReasonSafetyStop;
            if (_state == LinkState.Connecting)
            {
                _connectTcs?.TrySetResult(new StickDisconnected(ConnId, false, StickProtocol.ReasonSafetyStop));
            }

            _state = LinkState.Closed;
            _discovery?.Fail(StickStatus.Disconnected);
            _closedTcs?.TrySetResult(true);
        }

        _notifications.Writer.TryComplete();
        if (lost)
        {
            LinkLost?.Invoke(StickProtocol.ReasonSafetyStop);
        }
    }

    /// <summary>Marks the link as ended without raising <see cref="LinkLost"/> (local decision).</summary>
    private void MarkClosed()
    {
        lock (_lock)
        {
            _state = LinkState.Closed;
            _discovery?.Fail(StickStatus.Disconnected);
            _closedTcs?.TrySetResult(true);
        }

        _notifications.Writer.TryComplete();
    }

    /// <summary>Single reader of the notification channel: delivers to the handler in arrival order.</summary>
    private async Task PumpNotificationsAsync()
    {
        await foreach (StickNotify notify in _notifications.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                NotificationHandler?.Invoke(notify.Handle, notify.Data);
            }
            catch (Exception ex)
            {
                // A faulty device handler must not stop the delivery for the link.
                _logger.LogWarning(ex, "USB stick: notification handler failed on conn {ConnId}", ConnId);
            }
        }
    }

    /// <summary>
    /// Gives the conn_id back. A link that is still up is disconnected first, so the slot is never leaked
    /// on the stick.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _released, 1) != 0)
        {
            return;
        }

        _ = ReleaseAsync();
    }

    private async Task ReleaseAsync()
    {
        try
        {
            await DisconnectAsync().ConfigureAwait(false);
        }
        finally
        {
            await _connection.ReleaseLinkAsync(this).ConfigureAwait(false);
        }
    }

    // ------------------------------------------------------------------ discovery bookkeeping

    /// <summary>Collects EVT_GATT_SERVICE / EVT_GATT_CHAR until EVT_DISCOVER_DONE (always used under the link lock).</summary>
    private sealed class DiscoveryCollector
    {
        private readonly List<StickGattServiceEvent> _services = new();
        private readonly List<StickGattCharEvent> _chars = new();
        private readonly TaskCompletionSource<StickStatus> _done = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Completes with the status of EVT_DISCOVER_DONE (or Disconnected when the link ended).</summary>
        public Task<StickStatus> Done => _done.Task;

        public void Add(StickGattMessage message)
        {
            switch (message)
            {
                case StickGattServiceEvent service:
                    _services.Add(service);
                    break;
                case StickGattCharEvent chr:
                    _chars.Add(chr);
                    break;
                case StickDiscoverDone done:
                    _done.TrySetResult(done.Status);
                    break;
            }
        }

        public void Fail(StickStatus status) => _done.TrySetResult(status);

        /// <summary>Builds the tree: a characteristic belongs to the service whose handle range contains its value handle.</summary>
        public IReadOnlyList<StickGattService> BuildServices()
            => _services
                .Select(s => new StickGattService(
                    s.Uuid,
                    s.StartHandle,
                    s.EndHandle,
                    _chars.Where(c => c.ValueHandle >= s.StartHandle && c.ValueHandle <= s.EndHandle)
                          .Select(c => new StickGattCharacteristic(c.Uuid, c.ValueHandle, c.CccdHandle, c.Properties))
                          .ToList()))
                .ToList();
    }
}
