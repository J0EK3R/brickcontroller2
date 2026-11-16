using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Connection state of the USB advertising stick as shown to the user.
/// </summary>
public enum UsbStickState
{
    /// <summary>No stick was found (or none was probed yet).</summary>
    NotFound,

    /// <summary>The session is open and sets can run.</summary>
    Connected,

    /// <summary>The stick was connected before, the port was closed after being idle (it is probed again on demand).</summary>
    Idle,

    /// <summary>The stick vanished or stopped answering; reconnecting while sets are in use.</summary>
    Lost,

    /// <summary>A stick answered but speaks an incompatible protocol major version.</summary>
    Incompatible,
}

/// <summary>
/// Snapshot of the stick state for the UI.
/// </summary>
/// <param name="State">Connection state.</param>
/// <param name="PortName">Port of the stick (e.g. "COM7") if known.</param>
/// <param name="FirmwareVersion">Firmware version if known.</param>
public sealed record UsbStickStatus(UsbStickState State, string? PortName, string? FirmwareVersion);

/// <summary>
/// The single shared connection to the USB advertising stick (protocol.md). It
/// <list type="bullet">
/// <item>finds the stick (HELLO probe of every candidate port) and keeps one session open,</item>
/// <item>sends a PING every min(200 ms, keepalive/5) independent of data changes,</item>
/// <item>hands out up to <c>max_sets</c> set leases (one per advertising device),</item>
/// <item>detects unplug / keep-alive loss and reconnects with backoff, resuming all leased sets,</item>
/// <item>closes the port again after a short idle time so other tools can use the stick.</item>
/// </list>
/// All timing uses a <see cref="TimeProvider"/> so tests run without sleeping.
/// </summary>
public sealed class UsbStickConnection : IDisposable
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan HelloTimeout = TimeSpan.FromMilliseconds(200);
    private const int RequestAttempts = 3;
    private const int HelloAttempts = 3;
    private const int MaxPingFailures = 3;
    private static readonly TimeSpan MaxPingInterval = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan IdleCloseDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan NegativeProbeCache = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ReconnectDelayStart = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ReconnectDelayMax = TimeSpan.FromSeconds(2);

    private readonly IStickLocator _locator;
    private readonly TimeProvider _clock;
    private readonly ILogger _logger;

    // Guards connect / reconnect attempts so only one runs at a time.
    private readonly SemaphoreSlim _connectGate = new(1, 1);

    // Guards state, session, leases and timers (never held across an await).
    private readonly object _stateLock = new();

    private readonly List<StickSetLease> _leases = new();
    private StickSession? _session;
    private StickInfo? _info;
    private string? _portName;
    private UsbStickState _state = UsbStickState.NotFound;
    private ITimer? _pingTimer;
    private ITimer? _idleTimer;
    private CancellationTokenSource? _reconnectCts;
    private TimeSpan _pingInterval = MaxPingInterval;
    private int _pingBusy;
    private int _pingFailures;
    private long _lastFailedProbe;
    private bool _hasFailedProbe;
    private bool _disposed;

    public UsbStickConnection(IStickLocator locator, ILogger<UsbStickConnection> logger, TimeProvider? clock = null)
    {
        _locator = locator;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Raised whenever <see cref="Status"/> changes (on an arbitrary thread).</summary>
    public event EventHandler? StatusChanged;

    /// <summary>Current state, port and firmware version.</summary>
    public UsbStickStatus Status
    {
        get
        {
            lock (_stateLock)
            {
                return new UsbStickStatus(_state, _portName, _info?.FirmwareVersion);
            }
        }
    }

    /// <summary>Capabilities of the stick from the last HELLO_RSP (null if never connected).</summary>
    public StickInfo? Info
    {
        get
        {
            lock (_stateLock)
            {
                return _info;
            }
        }
    }

    /// <summary>True while a session is open.</summary>
    public bool IsConnected => Status.State == UsbStickState.Connected;

    /// <summary>Number of currently leased advertising sets.</summary>
    public int LeaseCount
    {
        get
        {
            lock (_stateLock)
            {
                return _leases.Count;
            }
        }
    }

    /// <summary>
    /// Makes sure a session is open: probes the candidate ports if necessary. A failed probe is
    /// remembered for a few seconds so that creating many devices without a stick stays cheap.
    /// While the stick is lost (reconnect loop running) this returns false: sets already in use resume
    /// by themselves, new devices fall back to another advertiser.
    /// </summary>
    /// <param name="forceProbe">Ignore the negative cache (used when the user opens the settings page).</param>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns>True when a session is open.</returns>
    public async Task<bool> TryConnectAsync(bool forceProbe = false, CancellationToken cancellationToken = default)
    {
        await _connectGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_stateLock)
            {
                if (_disposed || _state == UsbStickState.Lost)
                {
                    return false;
                }

                if (_state == UsbStickState.Connected)
                {
                    return true;
                }

                if (!forceProbe && _hasFailedProbe && _clock.GetElapsedTime(_lastFailedProbe) < NegativeProbeCache)
                {
                    return false;
                }
            }

            return await ConnectCoreAsync(keepLostState: false, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _connectGate.Release();
        }
    }

    /// <summary>
    /// Reserves the lowest free set id. Returns null when no session is open or all sets are in use
    /// (the caller then falls back to the Windows advertiser).
    /// </summary>
    public StickSetLease? TryAcquireLease()
    {
        lock (_stateLock)
        {
            if (_disposed || _state != UsbStickState.Connected || _info == null)
            {
                return null;
            }

            byte? freeId = Enumerable.Range(0, _info.MaxSets)
                .Select(id => (byte)id)
                .Where(id => _leases.All(l => l.SetId != id))
                .Cast<byte?>()
                .FirstOrDefault();

            if (freeId == null)
            {
                _logger.LogInformation("USB stick: all {Count} sets are in use", _info.MaxSets);
                return null;
            }

            var lease = new StickSetLease(this, freeId.Value, _logger);
            _leases.Add(lease);

            // New activity cancels a pending idle close.
            _idleTimer?.Dispose();
            _idleTimer = null;
            return lease;
        }
    }

    public void Dispose()
    {
        StickSession? session;
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            session = _session;
            _session = null;
            _pingTimer?.Dispose();
            _idleTimer?.Dispose();
            _reconnectCts?.Cancel();
        }

        session?.Dispose();
    }

    // ------------------------------------------------------------------ requests used by leases

    /// <summary>
    /// Sends a request on the current session. Any NO_SESSION answer marks the stick as lost.
    /// </summary>
    /// <exception cref="StickTransportException">There is no open session.</exception>
    /// <exception cref="StickTimeoutException">No response after the retries.</exception>
    internal async Task<StickAck> RequestAckAsync(byte type, byte[] payload)
    {
        StickSession session;
        lock (_stateLock)
        {
            session = _session ?? throw new StickTransportException("USB stick is not connected.");
        }

        StickFrame frame = await session.RequestAsync(type, payload, RequestTimeout, RequestAttempts).ConfigureAwait(false);
        if (!StickResponseDecoder.TryDecodeAck(frame, out var ack))
        {
            throw new StickException($"Unexpected response type 0x{frame.Type:X2} to request 0x{type:X2}.");
        }

        if (ack.Status == StickStatus.NoSession)
        {
            await MarkLostAsync("stick reported NO_SESSION").ConfigureAwait(false);
            throw new StickTransportException("The stick ended the session.");
        }

        return ack;
    }

    /// <summary>
    /// Called by a lease on dispose: frees the set id; after the last lease all sets are stopped and
    /// the port is closed after an idle delay.
    /// </summary>
    internal async Task ReleaseLeaseAsync(StickSetLease lease)
    {
        bool wasLast;
        lock (_stateLock)
        {
            if (!_leases.Remove(lease))
            {
                return;
            }

            wasLast = _leases.Count == 0;

            if (wasLast)
            {
                // Nothing left to keep alive for the reconnect loop.
                _reconnectCts?.Cancel();
            }
        }

        if (!wasLast)
        {
            return;
        }

        if (Status.State == UsbStickState.Connected)
        {
            try
            {
                await RequestAckAsync(StickProtocol.TypeStopAll, []).ConfigureAwait(false);
            }
            catch (StickException ex)
            {
                _logger.LogDebug("USB stick: STOP_ALL failed: {Message}", ex.Message);
            }

            lock (_stateLock)
            {
                if (_leases.Count == 0 && _state == UsbStickState.Connected && !_disposed)
                {
                    _idleTimer?.Dispose();
                    _idleTimer = _clock.CreateTimer(_ => _ = CloseIdleAsync(), null, IdleCloseDelay, Timeout.InfiniteTimeSpan);
                }
            }
        }
        else if (Status.State == UsbStickState.Lost)
        {
            SetState(UsbStickState.NotFound);
        }
    }

    // ------------------------------------------------------------------ connect

    /// <summary>
    /// Probes every candidate port with HELLO and keeps the first one that answers with the stick magic.
    /// Must be called with <see cref="_connectGate"/> held.
    /// </summary>
    private async Task<bool> ConnectCoreAsync(bool keepLostState, CancellationToken cancellationToken)
    {
        StickSession? chosen = null;
        StickInfo? info = null;
        bool incompatible = false;

        // The enumeration is lazy: ports after the accepted one are never opened.
        foreach (IStickTransport candidate in _locator.EnumerateCandidates())
        {
            var session = new StickSession(candidate, _clock, _logger);
            try
            {
                session.Open();
                (info, bool versionRejected) = await HelloAsync(session, cancellationToken).ConfigureAwait(false);
                incompatible |= versionRejected;
                if (info != null)
                {
                    chosen = session;
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                session.Dispose();
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("USB stick: probing {Port} failed: {Message}", candidate.Name, ex.Message);
            }

            // Not our stick: close the port immediately, never keep another board's port open.
            session.Dispose();
        }

        if (chosen == null || info == null)
        {
            lock (_stateLock)
            {
                _lastFailedProbe = _clock.GetTimestamp();
                _hasFailedProbe = true;
            }

            if (!keepLostState)
            {
                SetState(incompatible ? UsbStickState.Incompatible : UsbStickState.NotFound);
            }

            return false;
        }

        _logger.LogInformation(
            "USB stick found on {Port}: firmware {Firmware}, {Sets} sets, keep-alive {KeepAlive} ms, id {Id}",
            chosen.Transport.Name, info.FirmwareVersion, info.MaxSets, info.KeepAliveMs, info.DeviceId);

        lock (_stateLock)
        {
            if (_disposed)
            {
                chosen.Dispose();
                return false;
            }

            _session = chosen;
            _info = info;
            _portName = chosen.Transport.Name;
            _hasFailedProbe = false;
            _pingFailures = 0;

            // PING at min(200 ms, keepalive / 5): well below the keepalive / 4 required by the protocol.
            _pingInterval = TimeSpan.FromMilliseconds(Math.Max(20, Math.Min(MaxPingInterval.TotalMilliseconds, info.KeepAliveMs / 5.0)));
            _pingTimer?.Dispose();
            _pingTimer = _clock.CreateTimer(_ => OnPingTimer(), null, _pingInterval, _pingInterval);
        }

        chosen.EventReceived += OnEvent;
        chosen.Closed += OnSessionClosed;
        SetState(UsbStickState.Connected);

        // A stick that was closed because it went idle may have no leases; leases (if any) are resumed by the caller.
        return true;
    }

    /// <summary>
    /// Sends HELLO (with retries, the chip may still be booting) and decodes the answer.
    /// </summary>
    /// <returns>The stick info, or null when the port is not the stick; versionRejected is true if the stick refused our protocol version.</returns>
    private async Task<(StickInfo? Info, bool VersionRejected)> HelloAsync(StickSession session, CancellationToken cancellationToken)
    {
        byte[] payload = StickFrameEncoder.HelloPayload(StickProtocol.ProtoMajor, StickProtocol.ProtoMinor, StickProtocol.RequestedKeepAliveMs);

        try
        {
            StickFrame frame = await session.RequestAsync(StickProtocol.TypeHello, payload, HelloTimeout, HelloAttempts, cancellationToken).ConfigureAwait(false);

            if (StickResponseDecoder.TryDecodeHello(frame, out var info))
            {
                return (info, false);
            }

            if (StickResponseDecoder.TryDecodeAck(frame, out var ack) && ack.Status == StickStatus.VersionUnsupported)
            {
                _logger.LogWarning("USB stick on {Port} rejected protocol version {Major}.{Minor}", session.Transport.Name, StickProtocol.ProtoMajor, StickProtocol.ProtoMinor);
                return (null, true);
            }
        }
        catch (StickTimeoutException)
        {
            // Silent port: some other device.
        }

        return (null, false);
    }

    // ------------------------------------------------------------------ keep-alive

    /// <summary>Timer callback; never blocks and never overlaps itself.</summary>
    private void OnPingTimer()
    {
        if (Interlocked.Exchange(ref _pingBusy, 1) == 1)
        {
            return;
        }

        _ = PingAsync();
    }

    /// <summary>
    /// Sends one PING unless another frame was written recently (any valid frame resets the stick's timer).
    /// Three failures in a row mark the stick as lost.
    /// </summary>
    private async Task PingAsync()
    {
        try
        {
            StickSession? session;
            TimeSpan interval;
            lock (_stateLock)
            {
                session = _session;
                interval = _pingInterval;
            }

            if (session == null || session.TimeSinceLastSend < interval)
            {
                return;
            }

            StickFrame frame = await session.RequestAsync(StickProtocol.TypePing, [], RequestTimeout, 1).ConfigureAwait(false);

            if (StickResponseDecoder.TryDecodeAck(frame, out var ack) && ack.Status == StickStatus.NoSession)
            {
                await MarkLostAsync("PING answered with NO_SESSION").ConfigureAwait(false);
                return;
            }

            Volatile.Write(ref _pingFailures, 0);
        }
        catch (StickTimeoutException)
        {
            if (Interlocked.Increment(ref _pingFailures) >= MaxPingFailures)
            {
                await MarkLostAsync("PING timeouts").ConfigureAwait(false);
            }
        }
        catch (StickException)
        {
            // Transport errors are handled by the session's Closed event.
        }
        finally
        {
            Volatile.Write(ref _pingBusy, 0);
        }
    }

    // ------------------------------------------------------------------ events and loss

    /// <summary>Handles asynchronous events of the stick (runs on the transport thread, never blocks).</summary>
    private void OnEvent(StickFrame frame)
    {
        if (!StickResponseDecoder.TryDecodeEvent(frame, out var stickEvent))
        {
            return; // unknown events are ignored (protocol.md section 5)
        }

        StickSetLease[] leases;
        lock (_stateLock)
        {
            leases = _leases.ToArray();
        }

        switch (stickEvent.Kind)
        {
            case StickEventKind.ButtonPressed:
                // The user pressed the safety button on purpose: do not restart automatically.
                _logger.LogInformation("USB stick: button pressed, all sets stopped");
                foreach (var lease in leases)
                {
                    lease.OnStoppedByUser();
                }

                break;

            case StickEventKind.SetStopped:
                _logger.LogWarning("USB stick: set {SetId} stopped by the stick (reason {Reason})", stickEvent.SetId, stickEvent.Reason);
                _ = leases.FirstOrDefault(l => l.SetId == stickEvent.SetId)?.OnSetStoppedAsync();
                break;

            case StickEventKind.Timeout:
                _ = MarkLostAsync($"EVT_TIMEOUT ({stickEvent.TimeoutMs} ms)");
                break;
        }
    }

    private void OnSessionClosed(string reason) => _ = MarkLostAsync(reason);

    /// <summary>
    /// Ends the current session after an unplug or keep-alive loss. If leases exist a reconnect loop
    /// starts, which resumes the sets when the stick (possibly on another COM port) answers again.
    /// </summary>
    private Task MarkLostAsync(string reason)
    {
        StickSession? session;
        bool reconnect;
        lock (_stateLock)
        {
            if (_state != UsbStickState.Connected || _disposed)
            {
                return Task.CompletedTask;
            }

            session = _session;
            _session = null;
            _pingTimer?.Dispose();
            _pingTimer = null;
            _idleTimer?.Dispose();
            _idleTimer = null;
            reconnect = _leases.Count > 0;
        }

        _logger.LogWarning("USB stick lost: {Reason}", reason);
        session?.Dispose();
        SetState(reconnect ? UsbStickState.Lost : UsbStickState.NotFound);

        if (reconnect)
        {
            StartReconnectLoop();
        }

        return Task.CompletedTask;
    }

    /// <summary>Starts the background loop that waits for the stick to come back.</summary>
    private void StartReconnectLoop()
    {
        CancellationTokenSource cts;
        lock (_stateLock)
        {
            _reconnectCts?.Cancel();
            cts = _reconnectCts = new CancellationTokenSource();
        }

        _ = ReconnectLoopAsync(cts.Token);
    }

    /// <summary>
    /// Retries the connection with backoff (500 ms up to 2 s) while sets are leased. On success all
    /// leased sets are started again with their latest payload.
    /// </summary>
    private async Task ReconnectLoopAsync(CancellationToken token)
    {
        TimeSpan delay = ReconnectDelayStart;

        try
        {
            while (!token.IsCancellationRequested && LeaseCount > 0)
            {
                await Task.Delay(delay, _clock, token).ConfigureAwait(false);

                bool connected;
                await _connectGate.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    connected = await ConnectCoreAsync(keepLostState: true, token).ConfigureAwait(false);
                }
                finally
                {
                    _connectGate.Release();
                }

                if (connected)
                {
                    await ResumeLeasesAsync().ConfigureAwait(false);
                    return;
                }

                delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, ReconnectDelayMax.TotalMilliseconds));
            }
        }
        catch (OperationCanceledException)
        {
            // Last lease released or connection disposed.
        }
    }

    /// <summary>Starts every leased set again after a reconnect (HELLO stopped all sets).</summary>
    private async Task ResumeLeasesAsync()
    {
        StickSetLease[] leases;
        lock (_stateLock)
        {
            leases = _leases.ToArray();
        }

        foreach (var lease in leases)
        {
            await lease.ResumeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Closes the port after the idle delay if nobody leased a set in the meantime.</summary>
    private Task CloseIdleAsync()
    {
        StickSession? session;
        lock (_stateLock)
        {
            if (_leases.Count > 0 || _state != UsbStickState.Connected || _disposed)
            {
                return Task.CompletedTask;
            }

            session = _session;
            _session = null;
            _pingTimer?.Dispose();
            _pingTimer = null;
            _idleTimer?.Dispose();
            _idleTimer = null;
        }

        _logger.LogInformation("USB stick idle: closing the port");
        session?.Dispose();
        SetState(UsbStickState.Idle);
        return Task.CompletedTask;
    }

    private void SetState(UsbStickState state)
    {
        lock (_stateLock)
        {
            if (_state == state)
            {
                return;
            }

            _state = state;
        }

        StatusChanged?.Invoke(this, EventArgs.Empty);
    }
}
