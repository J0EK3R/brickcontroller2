using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BrickController2.Diagnostics;
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
/// <item>hands out up to <c>max_conns</c> GATT links (one per GATT device) and shares one BLE scan between scanners,</item>
/// <item>detects unplug / keep-alive loss and reconnects with backoff, resuming all leased sets and the scan
/// (GATT links are NOT resumed: they end and the device reconnect logic of BC2 takes over),</item>
/// <item>closes the port again after a short idle time (no lease, link or scan) so other tools can use the stick.</item>
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
    private readonly Dictionary<byte, StickGattLink> _links = new();
    private readonly List<StickScanner> _scanners = new();

    // Serialises SCAN_START / SCAN_STOP so that scanners starting and stopping concurrently stay consistent.
    private readonly SemaphoreSlim _scanGate = new(1, 1);
    private bool _scanOnStick;
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

    /// <summary>Number of GATT links currently reserved (connecting, connected or not yet released).</summary>
    public int LinkCount
    {
        get
        {
            lock (_stateLock)
            {
                return _links.Count;
            }
        }
    }

    /// <summary>
    /// Number of GATT links the stick supports (<c>max_conns</c> of the last HELLO_RSP); 0 when unknown or without GATT.
    /// </summary>
    public int MaxLinks => Info is { SupportsGatt: true } info ? info.MaxConns : 0;

    /// <summary>
    /// True while the stick really scans for at least one scanner. After EVT_BUTTON / EVT_SCAN_STOPPED the scanners
    /// stay registered (the scan is not restarted on purpose) but nothing is scanned, so this is false then.
    /// </summary>
    public bool IsScanning
    {
        get
        {
            lock (_stateLock)
            {
                return _scanners.Count > 0 && _scanOnStick;
            }
        }
    }

    /// <summary>Blocks until all scan results queued so far reached their callbacks (test support).</summary>
    internal void WaitForScanCallbacks()
    {
        StickScanner[] scanners;
        lock (_stateLock)
        {
            scanners = _scanners.ToArray();
        }

        foreach (var scanner in scanners)
        {
            scanner.WaitUntilDelivered(TimeSpan.FromSeconds(5));
        }
    }

    /// <summary>True when the session is open, the stick supports GATT and a conn_id is free.</summary>
    public bool HasFreeLink
    {
        get
        {
            lock (_stateLock)
            {
                return CanUseGattLocked() && FindFreeConnIdLocked() != null;
            }
        }
    }

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

    /// <summary>
    /// Reserves the lowest free <c>conn_id</c> (below <c>max_conns</c> of the stick, whatever that is). Returns null when
    /// no session is open, the stick has no GATT support or all connection slots are in use.
    /// </summary>
    internal StickGattLink? TryAcquireLink()
    {
        StickGattLink link;
        lock (_stateLock)
        {
            if (!CanUseGattLocked())
            {
                UsbStickTrace.Write($"TryAcquireLink refused: stick state={_state} disposed={_disposed} supportsGatt={_info?.SupportsGatt}");
                return null;
            }

            byte? freeId = FindFreeConnIdLocked();
            if (freeId == null)
            {
                UsbStickTrace.Write($"TryAcquireLink refused: all {_info!.MaxConns} GATT links are in use");
                _logger.LogInformation("USB stick: all {Count} GATT links are in use", _info!.MaxConns);
                return null;
            }

            UsbStickTrace.Write($"conn {freeId.Value}: slot reserved ({_links.Count + 1}/{_info!.MaxConns} in use)");

            link = new StickGattLink(this, freeId.Value, _clock, _logger);
            _links.Add(freeId.Value, link);

            // New activity cancels a pending idle close.
            _idleTimer?.Dispose();
            _idleTimer = null;
        }

        RaiseStatusChanged();
        return link;
    }

    /// <summary>Must be called with <see cref="_stateLock"/> held.</summary>
    private bool CanUseGattLocked() => !_disposed && _state == UsbStickState.Connected && _info is { SupportsGatt: true };

    /// <summary>Lowest conn_id below max_conns that is not reserved; must be called with <see cref="_stateLock"/> held.</summary>
    private byte? FindFreeConnIdLocked()
    {
        for (int id = 0; id < _info!.MaxConns; id++)
        {
            if (!_links.ContainsKey((byte)id))
            {
                return (byte)id;
            }
        }

        return null;
    }

    /// <summary>
    /// Called by a link on dispose: frees its conn_id (only if it still owns it: after a stick loss a new
    /// link may already use the same id) and lets the idle timer run when nothing else is in use.
    /// </summary>
    internal Task ReleaseLinkAsync(StickGattLink link)
    {
        bool removed;
        lock (_stateLock)
        {
            removed = _links.TryGetValue(link.ConnId, out var owner) && ReferenceEquals(owner, link) && _links.Remove(link.ConnId);
        }

        UsbStickTrace.Write($"conn {link.ConnId}: slot released (removed={removed})");

        if (removed)
        {
            RaiseStatusChanged();
            StartIdleTimerIfUnused();
        }

        return Task.CompletedTask;
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
    /// Sends a request exactly once (no retransmission, needed for GATT writes) and waits for any response
    /// frame (ACK or a specific response such as READ_RSP). Any NO_SESSION answer marks the stick as lost.
    /// </summary>
    /// <param name="type">Request type.</param>
    /// <param name="payload">Request payload.</param>
    /// <param name="timeout">How long to wait for the response.</param>
    /// <param name="cancellationToken">Stops waiting (the request stays on the wire).</param>
    /// <exception cref="StickTransportException">There is no open session or it failed.</exception>
    /// <exception cref="StickTimeoutException">No response within the timeout.</exception>
    internal async Task<StickFrame> RequestOnceAsync(byte type, byte[] payload, TimeSpan timeout, CancellationToken cancellationToken)
    {
        StickSession session;
        lock (_stateLock)
        {
            session = _session ?? throw new StickTransportException("USB stick is not connected.");
        }

        StickFrame frame = await session.RequestAsync(type, payload, timeout, 1, cancellationToken).ConfigureAwait(false);
        if (StickResponseDecoder.TryDecodeAck(frame, out var ack) && ack.Status == StickStatus.NoSession)
        {
            await MarkLostAsync("stick reported NO_SESSION").ConfigureAwait(false);
            throw new StickTransportException("The stick ended the session.");
        }

        return frame;
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

            wasLast = _leases.Count == 0 && _scanners.Count == 0;

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

        // No STOP_ALL here: every lease already stopped its own set before releasing itself, and a
        // STOP_ALL written late could stop the set of a lease acquired in the meantime.
        StartIdleTimerIfUnused();
    }

    /// <summary>
    /// Everything that needs the port to stay open: advertising leases, GATT links and scanners.
    /// Must be called with <see cref="_stateLock"/> held.
    /// </summary>
    private int UserCountLocked() => _leases.Count + _links.Count + _scanners.Count;

    /// <summary>
    /// With no lease, link or scan left: a connected stick closes after the idle delay, a lost stick (reconnect loop
    /// cancelled) becomes NotFound. Safe to call at any time; does nothing while leases exist.
    /// </summary>
    private void StartIdleTimerIfUnused()
    {
        bool markNotFound = false;
        lock (_stateLock)
        {
            if (UserCountLocked() > 0 || _disposed)
            {
                return;
            }

            if (_state == UsbStickState.Connected)
            {
                _idleTimer?.Dispose();
                _idleTimer = _clock.CreateTimer(_ => _ = CloseIdleAsync(), null, IdleCloseDelay, Timeout.InfiniteTimeSpan);
            }
            else if (_state == UsbStickState.Lost)
            {
                markNotFound = true;
            }
        }

        if (markNotFound)
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
        UsbStickTrace.Write($"stick found on {chosen.Transport.Name}: firmware={info.FirmwareVersion} id={info.DeviceId} keepAlive={info.KeepAliveMs}ms sets={info.MaxSets} gattFlags=0x{info.GattFlags:X2} maxConns={info.MaxConns} maxValueLen={info.MaxValueLen} maxScanData={info.MaxScanData} failsafeSlots={info.FailsafeSlots}");

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
            // The timer ticks at half the interval and a tick only skips when a frame went out within the last
            // half interval. A frame sent just before a tick therefore delays the next frame by at most
            // 2 x interval / 2 = interval (200 ms), staying below the 250 ms of protocol.md section 11.
            TimeSpan tick = _pingInterval / 2;
            _pingTimer = _clock.CreateTimer(_ => OnPingTimer(), null, tick, tick);
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

            // Skip only if a frame was written within the last half interval (the timer ticks at that rate).
            if (session == null || session.TimeSinceLastSend < interval / 2)
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
        if (StickResponseDecoder.TryDecodeEvent(frame, out var stickEvent))
        {
            OnLegacyEvent(stickEvent);
        }
        else if (StickResponseDecoder.TryDecodeGatt(frame, out var message))
        {
            RouteGattMessage(message);
        }

        // anything else is ignored (protocol.md section 5)
    }

    /// <summary>Events of protocol 1.0 (set stopped, button, keep-alive timeout).</summary>
    private void OnLegacyEvent(StickEvent stickEvent)
    {
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

                OnStickStoppedGatt();
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

    /// <summary>
    /// Routes a protocol 1.1 event to the link (by conn_id) or the scanners. Runs on the transport thread:
    /// only cheap, non blocking work happens here (notifications go into the link's channel).
    /// </summary>
    private void RouteGattMessage(StickGattMessage message)
    {
        switch (message)
        {
            case StickScanReport report:
                StickScanner[] scanners;
                lock (_stateLock)
                {
                    scanners = _scanners.ToArray();
                }

                foreach (var scanner in scanners)
                {
                    scanner.OnReport(report);
                }

                break;

            case StickScanStopped stopped:
                UsbStickTrace.Write($"EVT_SCAN_STOPPED by the stick: reason={stopped.Reason} rc={stopped.Rc}");
                _logger.LogWarning("USB stick: scan stopped by the stick (reason {Reason}, rc {Rc})", stopped.Reason, stopped.Rc);
                lock (_stateLock)
                {
                    _scanOnStick = false;
                }

                break;

            case StickDropped dropped:
                UsbStickTrace.Write($"EVT_DROPPED: the stick dropped {dropped.Count} frame(s) of kind {dropped.Kind} (1 scan reports, 2 notifications)");
                _logger.LogDebug("USB stick dropped {Count} frames of kind {Kind}", dropped.Count, dropped.Kind);
                break;

            case StickNotify n:
                FindLink(n.ConnId)?.OnMessage(message);
                break;

            case StickConnected c:
                FindLink(c.ConnId)?.OnMessage(message);
                break;

            case StickDisconnected d:
                FindLink(d.ConnId)?.OnMessage(message);
                break;

            case StickGattServiceEvent sv:
                FindLink(sv.ConnId)?.OnMessage(message);
                break;

            case StickGattCharEvent ch:
                FindLink(ch.ConnId)?.OnMessage(message);
                break;

            case StickDiscoverDone dd:
                FindLink(dd.ConnId)?.OnMessage(message);
                break;

            // READ_RSP / GATT_STATUS_RSP are responses (matched by SEQ); a late one is of no interest.
        }
    }

    private StickGattLink? FindLink(byte connId)
    {
        StickGattLink? link;
        lock (_stateLock)
        {
            link = _links.GetValueOrDefault(connId);
        }

        // Diagnostic: an event for a conn_id nobody reserved is silently dropped otherwise.
        if (link == null)
        {
            UsbStickTrace.Write($"event for conn {connId} DROPPED: no link reserved on that conn_id");
        }

        return link;
    }

    /// <summary>
    /// The stick ended all GATT activity on its own while the session stays (button): links are dead, the scan is
    /// off, and nothing is restarted automatically (the user pressed the safety button on purpose).
    /// </summary>
    private void OnStickStoppedGatt()
    {
        StickGattLink[] links;
        lock (_stateLock)
        {
            links = _links.Values.ToArray();
            _scanOnStick = false;
        }

        foreach (var link in links)
        {
            link.OnStickLost();
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
        StickGattLink[] links;
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

            // GATT links are not resumed: the stick drops all of them. They are forgotten here (their conn_ids
            // become free for the new session) and told that the link is gone.
            links = _links.Values.ToArray();
            _links.Clear();
            _scanOnStick = false;

            // Advertising sets and the scan are stateless and resume after the reconnect.
            reconnect = _leases.Count > 0 || _scanners.Count > 0;
        }

        UsbStickTrace.Write($"STICK LOST: {reason}; {links.Length} GATT link(s) are told that they are dead");
        _logger.LogWarning("USB stick lost: {Reason}", reason);
        session?.Dispose();
        SetState(reconnect ? UsbStickState.Lost : UsbStickState.NotFound);

        foreach (var link in links)
        {
            link.OnStickLost();
        }

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
            while (!token.IsCancellationRequested && ResumableUserCount > 0)
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

                    // The last lease may have been released while the connect was already running:
                    // then nobody needs the port, so let it close (and stop PING) after the idle delay.
                    StartIdleTimerIfUnused();
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

    /// <summary>Users that survive a stick loss: advertising leases and scanners (links do not).</summary>
    private int ResumableUserCount
    {
        get
        {
            lock (_stateLock)
            {
                return _leases.Count + _scanners.Count;
            }
        }
    }

    /// <summary>Starts every leased set and the scan again after a reconnect (HELLO stopped everything).</summary>
    private async Task ResumeLeasesAsync()
    {
        StickSetLease[] leases;
        bool resumeScan;
        lock (_stateLock)
        {
            leases = _leases.ToArray();
            resumeScan = _scanners.Count > 0;
        }

        foreach (var lease in leases)
        {
            await lease.ResumeAsync().ConfigureAwait(false);
        }

        if (resumeScan)
        {
            await EnsureScanRunningAsync().ConfigureAwait(false);
        }
    }

    // ------------------------------------------------------------------ scanning (shared by all scanners)

    /// <summary>
    /// Registers a scanner and starts the stick's scan if it is not running yet. All scanners share one scan
    /// and receive every report. Returns false (and registers nothing) when the stick cannot scan.
    /// </summary>
    internal async Task<bool> StartScanAsync(StickScanner scanner)
    {
        lock (_stateLock)
        {
            if (_disposed || _state != UsbStickState.Connected || _info is not { SupportsScan: true })
            {
                return false;
            }

            _scanners.Add(scanner);
            _idleTimer?.Dispose();
            _idleTimer = null;
        }

        RaiseStatusChanged();
        if (await EnsureScanRunningAsync().ConfigureAwait(false))
        {
            return true;
        }

        await StopScanAsync(scanner).ConfigureAwait(false);
        return false;
    }

    /// <summary>Sends SCAN_START (active scan, firmware defaults for timing) unless the scan already runs.</summary>
    private async Task<bool> EnsureScanRunningAsync()
    {
        await _scanGate.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_stateLock)
            {
                if (_scanOnStick)
                {
                    return true;
                }
            }

            // Active scan always: DUPLO / WeDo 2.0 only show their name in the scan response.
            byte[] payload = StickFrameEncoder.ScanStartPayload(StickProtocol.ScanFlagActive, 0, 0, 0);
            StickFrame frame = await RequestOnceAsync(StickProtocol.TypeScanStart, payload, StickGattLink.ImmediateAckTimeout, CancellationToken.None).ConfigureAwait(false);
            if (!StickResponseDecoder.TryDecodeAck(frame, out var ack) || ack.Status is not (StickStatus.Ok or StickStatus.AlreadyRunning))
            {
                _logger.LogWarning("USB stick: SCAN_START refused");
                return false;
            }

            lock (_stateLock)
            {
                _scanOnStick = true;
            }

            return true;
        }
        catch (StickException ex)
        {
            _logger.LogWarning("USB stick: SCAN_START failed: {Message}", ex.Message);
            return false;
        }
        finally
        {
            _scanGate.Release();
        }
    }

    /// <summary>Unregisters a scanner; the last one stops the stick's scan.</summary>
    internal async Task StopScanAsync(StickScanner scanner)
    {
        bool stopScan;
        lock (_stateLock)
        {
            if (!_scanners.Remove(scanner))
            {
                return;
            }

            stopScan = _scanners.Count == 0 && _scanOnStick;
            if (_scanners.Count == 0 && _state == UsbStickState.Lost)
            {
                _reconnectCts?.Cancel();
            }
        }

        RaiseStatusChanged();

        if (stopScan)
        {
            await _scanGate.WaitAsync().ConfigureAwait(false);
            try
            {
                bool stillUnused;
                lock (_stateLock)
                {
                    stillUnused = _scanners.Count == 0 && _scanOnStick;
                    if (stillUnused)
                    {
                        _scanOnStick = false;
                    }
                }

                if (stillUnused)
                {
                    await RequestOnceAsync(StickProtocol.TypeScanStop, [], StickGattLink.ImmediateAckTimeout, CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (StickException)
            {
                // The stick is gone or silent: its safety logic stops the scan anyway.
            }
            finally
            {
                _scanGate.Release();
            }
        }

        StartIdleTimerIfUnused();
    }

    /// <summary>Closes the port after the idle delay if nobody leased a set in the meantime.</summary>
    private Task CloseIdleAsync()
    {
        StickSession? session;
        lock (_stateLock)
        {
            if (UserCountLocked() > 0 || _state != UsbStickState.Connected || _disposed)
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

        RaiseStatusChanged();
    }

    /// <summary>Notifies the UI that state or activity (links, scan) changed.</summary>
    private void RaiseStatusChanged() => StatusChanged?.Invoke(this, EventArgs.Empty);
}
