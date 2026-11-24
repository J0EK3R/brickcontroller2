using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using BrickController2.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// One open connection to a stick transport: frames requests, matches responses by sequence
/// number, retries on timeout and forwards asynchronous events. It knows nothing about advertising
/// sets; <see cref="UsbStickConnection"/> builds on top of it.
/// Thread-safe: requests may be issued from any thread, responses arrive on the transport thread.
/// </summary>
internal sealed class StickSession : IDisposable
{
    private readonly TimeProvider _clock;
    private readonly ILogger _logger;
    private readonly StickFrameParser _parser;

    // Serialises frame writes so that two frames can never interleave on the wire.
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    // Requests waiting for their response, keyed by sequence number.
    private readonly ConcurrentDictionary<byte, TaskCompletionSource<StickFrame>> _pending = new();

    private readonly object _seqLock = new();
    private byte _lastSeq;
    private int _closed;
    private long _lastSendTimestamp;
    private long _lastReceiveTimestamp;
    private int _transportClosed;
    private int _undispatched;   // events queued or being handled

    // Events (frames without a waiting request) are handed from the transport's read thread to one dedicated
    // consumer so that notification handling / user callbacks can never delay reading the next bytes (and thereby
    // the PONGs and ACKs). One consumer keeps the order of all events, hence the order per link.
    private readonly Channel<StickFrame> _events = Channel.CreateUnbounded<StickFrame>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true, AllowSynchronousContinuations = false });

    // Only a silence longer than this between two received frames is traced (the stick sends several per second).
    private static readonly TimeSpan TraceGap = TimeSpan.FromMilliseconds(1000);

    public StickSession(IStickTransport transport, TimeProvider clock, ILogger logger)
    {
        Transport = transport;
        _clock = clock;
        _logger = logger;
        _parser = new StickFrameParser(clock);
        _lastSendTimestamp = clock.GetTimestamp();
        _lastReceiveTimestamp = _lastSendTimestamp;

        // Long-running consumer on its own thread (not the thread pool, which may be starved).
        _ = Task.Factory.StartNew(DispatchEventsAsync, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    /// <summary>The transport this session talks to.</summary>
    public IStickTransport Transport { get; }

    /// <summary>True once the transport failed, was unplugged or the session was disposed.</summary>
    public bool IsClosed => Volatile.Read(ref _closed) != 0;

    /// <summary>Raised for each asynchronous event frame (SEQ 0) or frame without a waiting request.</summary>
    public event Action<StickFrame>? EventReceived;

    /// <summary>Raised once, with a reason, when the session ended because of a transport problem.</summary>
    public event Action<string>? Closed;

    /// <summary>Time since the last frame was written (used to skip redundant PINGs).</summary>
    public TimeSpan TimeSinceLastSend => _clock.GetElapsedTime(Volatile.Read(ref _lastSendTimestamp));

    /// <summary>Time since the last valid frame was received (any frame proves the stick is alive).</summary>
    public TimeSpan TimeSinceLastReceive => _clock.GetElapsedTime(Volatile.Read(ref _lastReceiveTimestamp));

    /// <summary>
    /// True once <see cref="Dispose"/> finished closing the transport. Unlike <see cref="IsClosed"/> (set at the
    /// START of the dispose) this stays false while the port is still being closed, which can take over a second.
    /// The PING pump keeps sending until then so the stick does not see a keep-alive gap.
    /// </summary>
    public bool IsTransportClosed => Volatile.Read(ref _transportClosed) != 0;

    /// <summary>Opens the transport and starts receiving.</summary>
    public void Open()
    {
        Transport.DataReceived += OnDataReceived;
        Transport.Disconnected += OnTransportDisconnected;
        Transport.Open();
    }

    /// <summary>
    /// Sends a request and waits for its response. The same sequence number is reused for retries,
    /// so a late answer to an earlier attempt still completes the request.
    /// </summary>
    /// <param name="type">Request type.</param>
    /// <param name="payload">Request payload.</param>
    /// <param name="timeout">Time to wait for the response per attempt.</param>
    /// <param name="attempts">Number of attempts (1 = no retry).</param>
    /// <param name="cancellationToken">Cancels waiting.</param>
    /// <returns>The response frame.</returns>
    /// <exception cref="StickTimeoutException">No answer after all attempts.</exception>
    /// <exception cref="StickTransportException">The transport is closed or the write failed.</exception>
    public async Task<StickFrame> RequestAsync(byte type, byte[] payload, TimeSpan timeout, int attempts, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<StickFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        // Reserve a sequence number that no other waiting request uses (never overwrites a pending entry).
        byte seq = ReserveSeq(tcs);
        byte[] frame = StickFrameEncoder.Encode(type, seq, payload);

        try
        {
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                await WriteAsync(frame, cancellationToken).ConfigureAwait(false);

                try
                {
                    return await tcs.Task.WaitAsync(timeout, _clock, cancellationToken).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    UsbStickTrace.Write($"request timeout: no response to {UsbStickTrace.FrameName(type)} seq={seq} after {timeout.TotalMilliseconds:F0}ms (attempt {attempt}/{attempts})");
                    _logger.LogDebug("USB stick: no response to type 0x{Type:X2} (attempt {Attempt}/{Attempts})", type, attempt, attempts);
                }
            }

            throw new StickTimeoutException($"No response to request 0x{type:X2} after {attempts} attempt(s).");
        }
        finally
        {
            // Remove only our own entry: the response handler may already have removed it and a later
            // request may have reserved the same number in the meantime.
            _pending.TryRemove(new KeyValuePair<byte, TaskCompletionSource<StickFrame>>(seq, tcs));
        }
    }

    /// <summary>
    /// Sends one PING frame without waiting for the PONG and without ever blocking: used by the keep-alive pump,
    /// which must not depend on a round trip (the stick only needs frames) or on a busy writer. If another frame
    /// is being written right now, that frame keeps the stick alive and the PING is skipped. Works on a session
    /// that is being disposed (the port may still be open for a while) and swallows write errors then.
    /// </summary>
    /// <returns>True when a PING was written.</returns>
    public bool TrySendPing()
    {
        if (IsTransportClosed || !_writeLock.Wait(0))
        {
            return false;
        }

        try
        {
            // The sequence number is reserved with a throw-away entry: the PONG is not awaited, it only has to
            // be recognised (and ignored) by the receive path.
            byte seq = ReserveSeq(new TaskCompletionSource<StickFrame>());
            _pending.TryRemove(seq, out _);
            Transport.WriteAsync(StickFrameEncoder.Encode(StickProtocol.TypePing, seq, []), CancellationToken.None).GetAwaiter().GetResult();
            Volatile.Write(ref _lastSendTimestamp, _clock.GetTimestamp());
            return true;
        }
        catch (Exception ex)
        {
            // Closing port or unplugged stick: the transport reports that through Disconnected / Closed.
            _logger.LogDebug(ex, "USB stick: PING write failed");
            return false;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>Closes the transport without raising <see cref="Closed"/>.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _closed, 1) == 0)
        {
            FailPending("Session disposed.");
        }

        Transport.DataReceived -= OnDataReceived;
        Transport.Disconnected -= OnTransportDisconnected;
        _events.Writer.TryComplete();
        try
        {
            Transport.Dispose();      // may block for a while (SerialPort.Close); PING continues meanwhile
        }
        finally
        {
            Volatile.Write(ref _transportClosed, 1);
        }
    }

    /// <summary>
    /// Picks the next sequence number in 1..255 (0 is reserved for events) that is not used by a
    /// request still waiting for its answer and registers <paramref name="tcs"/> under it. Requests can
    /// stay pending for seconds while others are sent at a high rate, so wrapping around must skip
    /// numbers in use instead of overwriting them.
    /// </summary>
    /// <exception cref="StickTransportException">All 255 sequence numbers are in use.</exception>
    private byte ReserveSeq(TaskCompletionSource<StickFrame> tcs)
    {
        lock (_seqLock)
        {
            for (int i = 0; i < 255; i++)
            {
                _lastSeq = _lastSeq == 255 ? (byte)1 : (byte)(_lastSeq + 1);
                if (_pending.TryAdd(_lastSeq, tcs))
                {
                    return _lastSeq;
                }
            }
        }

        throw new StickTransportException("Too many requests waiting for the stick.");
    }

    /// <summary>Writes one frame under the write lock; a failure closes the session.</summary>
    private async Task WriteAsync(byte[] frame, CancellationToken cancellationToken)
    {
        if (IsClosed)
        {
            throw new StickTransportException("The stick session is closed.");
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Transport.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _lastSendTimestamp, _clock.GetTimestamp());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Close($"write failed: {ex.Message}");
            throw new StickTransportException("Writing to the stick failed.", ex);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>Parses received bytes and dispatches the frames (runs on the transport's read thread).</summary>
    private void OnDataReceived(byte[] data)
    {
        System.Collections.Generic.IReadOnlyList<StickFrame> frames;
        lock (_parser)
        {
            frames = _parser.Feed(data);
        }

        foreach (var frame in frames)
        {
            // Time between two received frames; frames come in at least every few 100 ms while PINGs run.
            long now = _clock.GetTimestamp();
            TimeSpan gap = _clock.GetElapsedTime(Volatile.Read(ref _lastReceiveTimestamp), now);
            Volatile.Write(ref _lastReceiveTimestamp, now);
            TraceLateFrame(gap);

            // Responses carry the sequence number of their request; events use SEQ 0.
            if (frame.Seq != 0 && _pending.TryRemove(frame.Seq, out var tcs))
            {
                tcs.TrySetResult(frame);
            }
            else if (frame.Type != StickProtocol.TypePong)   // PONGs of the keep-alive are only proof of life
            {
                // Never run handlers on the read thread: queue them for the dispatcher (keeps the order).
                Interlocked.Increment(ref _undispatched);
                if (!_events.Writer.TryWrite(frame))
                {
                    Interlocked.Decrement(ref _undispatched);   // writer completed (disposed): nobody will handle it
                }
            }
        }
    }

    /// <summary>Single consumer (a long-running task, thread-pool based after its first await): raises <see cref="EventReceived"/> in arrival order, off the read thread.</summary>
    private async Task DispatchEventsAsync()
    {
        var reader = _events.Reader;
        // Asynchronous wait: no thread is held while the queue is empty.
        while (await reader.WaitToReadAsync().ConfigureAwait(false))
        {
            while (reader.TryRead(out var frame))
            {
                try
                {
                    EventReceived?.Invoke(frame);
                }
                catch (Exception ex)
                {
                    // A faulty handler must not kill the dispatcher (all later events would be lost).
                    _logger.LogError(ex, "USB stick: event handler failed for type 0x{Type:X2}", frame.Type);
                }
                finally
                {
                    Interlocked.Decrement(ref _undispatched);
                }
            }
        }
    }

    /// <summary>Test support: waits until all events received so far were handled (or the timeout passes).</summary>
    internal bool WaitForEventsDispatched(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (Volatile.Read(ref _undispatched) > 0 && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(1);
        }

        return Volatile.Read(ref _undispatched) == 0;
    }

    /// <summary>Diagnostic: reports a long silence before a received frame (stick link or host was stalled).</summary>
    private static void TraceLateFrame(TimeSpan gap)
    {
        if (gap > TraceGap)
        {
            UsbStickTrace.Write($"late keep-alive: no frame from the stick for {gap.TotalMilliseconds:F0} ms (stick link or host was stalled)");
        }
    }

    private void OnTransportDisconnected()
    {
        Close("transport disconnected");
    }

    /// <summary>Marks the session closed, fails waiting requests and notifies the owner once.</summary>
    private void Close(string reason)
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
        {
            return;
        }

        UsbStickTrace.Write($"session closed: {reason}");
        _logger.LogWarning("USB stick session closed: {Reason}", reason);
        FailPending(reason);
        Closed?.Invoke(reason);
    }

    private void FailPending(string reason)
    {
        foreach (var tcs in _pending.Values)
        {
            tcs.TrySetException(new StickTransportException(reason));
        }
    }
}
