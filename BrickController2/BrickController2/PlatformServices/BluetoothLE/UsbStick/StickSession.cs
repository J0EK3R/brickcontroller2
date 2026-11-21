using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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

    public StickSession(IStickTransport transport, TimeProvider clock, ILogger logger)
    {
        Transport = transport;
        _clock = clock;
        _logger = logger;
        _parser = new StickFrameParser(clock);
        _lastSendTimestamp = clock.GetTimestamp();
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

    /// <summary>Closes the transport without raising <see cref="Closed"/>.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _closed, 1) == 0)
        {
            FailPending("Session disposed.");
        }

        Transport.DataReceived -= OnDataReceived;
        Transport.Disconnected -= OnTransportDisconnected;
        Transport.Dispose();
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
            // Responses carry the sequence number of their request; events use SEQ 0.
            if (frame.Seq != 0 && _pending.TryRemove(frame.Seq, out var tcs))
            {
                tcs.TrySetResult(frame);
            }
            else
            {
                EventReceived?.Invoke(frame);
            }
        }
    }

    private void OnTransportDisconnected() => Close("transport disconnected");

    /// <summary>Marks the session closed, fails waiting requests and notifies the owner once.</summary>
    private void Close(string reason)
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
        {
            return;
        }

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
