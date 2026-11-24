using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// One advertising set (set id) of the stick reserved for one advertising device. It remembers what the
/// device wants (parameters and the latest payload) so the connection can resume the set after the
/// stick was unplugged and plugged in again.
/// <para>
/// Ordering: all requests of a lease run under one gate, so Start, Update and Stop never overtake each
/// other. Updates are coalesced (latest wins): callers that queue up behind a running update find the
/// newest data already sent and return without another frame.
/// </para>
/// </summary>
public sealed class StickSetLease : IDisposable
{
    private readonly UsbStickConnection _connection;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    // The data the device last asked for. Guarded by _dataLock; _version counts changes.
    private readonly object _dataLock = new();
    private ushort _interval;
    private sbyte _txPower;
    private ushort _manufacturerId;
    private byte[] _data = [];
    private long _version;

    // State below is only changed under _gate (except the flags set from the event path).
    private long _sentVersion = -1;
    private bool _wanted;          // the device called Start and not Stop
    private volatile bool _running; // the stick confirmed the set is on air
    private volatile bool _stoppedByUser; // the button on the stick stopped the set
    private int _disposed;

    internal StickSetLease(UsbStickConnection connection, byte setId, ILogger logger)
    {
        _connection = connection;
        SetId = setId;
        _logger = logger;
    }

    /// <summary>
    /// The set id (0..max_sets-1) used on the stick.
    /// </summary>
    public byte SetId { get; }

    /// <summary>
    /// Capabilities of the connected stick (interval limits etc.), if known.
    /// </summary>
    public StickInfo? Info => _connection.Info;

    /// <summary>
    /// Starts advertising (SET_START). If the stick is currently unreachable the request is remembered
    /// and sent as soon as the connection is back.
    /// </summary>
    /// <param name="interval">Advertising interval in 0.625 ms units.</param>
    /// <param name="txPower">TX power in dBm (127 = maximum).</param>
    /// <param name="manufacturerId">Company identifier of the manufacturer data.</param>
    /// <param name="data">Manufacturer data, already in the Android layout (at most 24 bytes).</param>
    public async Task StartAsync(ushort interval, sbyte txPower, ushort manufacturerId, byte[] data)
    {
        lock (_dataLock)
        {
            _interval = interval;
            _txPower = txPower;
            _manufacturerId = manufacturerId;
            _data = (byte[])data.Clone();
            _version++;
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _wanted = true;
            _stoppedByUser = false; // an explicit start resumes a set stopped by the button
            await SendStartAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Replaces the payload of the running set (SET_UPDATE). Ignored while the set is not wanted or was
    /// stopped by the button. Concurrent updates are coalesced.
    /// </summary>
    public async Task UpdateAsync(ushort manufacturerId, byte[] data)
    {
        lock (_dataLock)
        {
            _manufacturerId = manufacturerId;
            _data = (byte[])data.Clone();
            _version++;
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_wanted || _stoppedByUser)
            {
                return;
            }

            if (Volatile.Read(ref _sentVersion) == CurrentVersion())
            {
                return; // a newer update already carried this data
            }

            if (_running)
            {
                await SendUpdateAsync().ConfigureAwait(false);
            }
            else
            {
                // The set is not on air (earlier start failed or stick was away): try to start it now.
                await SendStartAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Stops advertising (SET_STOP); the lease (and its set id) stays reserved.
    /// </summary>
    public async Task StopAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _wanted = false;
            if (!_running)
            {
                return;
            }

            _running = false;
            try
            {
                var ack = await _connection.RequestAckAsync(StickProtocol.TypeSetStop, StickFrameEncoder.SetStopPayload(SetId)).ConfigureAwait(false);
                LogIfFailed("SET_STOP", ack, StickStatus.SetNotFound);
            }
            catch (StickException ex)
            {
                // Stick gone or silent: it stops advertising by itself (keep-alive / power loss).
                _logger.LogDebug("USB stick set {SetId}: stop not confirmed: {Message}", SetId, ex.Message);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Stops the set and frees the set id (asynchronously, so Dispose never blocks the caller).
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _ = StopAndReleaseAsync();
        }
    }

    // ------------------------------------------------------------------ called by the connection

    /// <summary>
    /// The button on the stick stopped the set: stay silent until the device starts again.
    /// </summary>
    internal void OnStoppedByUser()
    {
        _stoppedByUser = true;
        _running = false;
    }

    /// <summary>
    /// The stick reported that the set ended on its own (BLE controller error): restart it once.
    /// </summary>
    internal async Task OnSetStoppedAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _running = false;
            if (_wanted && !_stoppedByUser)
            {
                await SendStartAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// The connection came back (HELLO stopped all sets): start the set again with the latest data.
    /// </summary>
    internal async Task ResumeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _running = false;
            if (_wanted && !_stoppedByUser)
            {
                await SendStartAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    // ------------------------------------------------------------------ internals (gate held)

    private long CurrentVersion()
    {
        lock (_dataLock)
        {
            return _version;
        }
    }

    /// <summary>
    /// Snapshot of the data that should be on air.
    /// </summary>
    private (long Version, ushort Interval, sbyte Tx, ushort Mfr, byte[] Data) Snapshot()
    {
        lock (_dataLock)
        {
            return (_version, _interval, _txPower, _manufacturerId, _data);
        }
    }

    /// <summary>
    /// Sends SET_START with the latest data. ALREADY_RUNNING (duplicate after a retry) counts as success.
    /// </summary>
    private async Task SendStartAsync()
    {
        var (version, interval, tx, mfr, data) = Snapshot();
        try
        {
            var ack = await _connection.RequestAckAsync(
                StickProtocol.TypeSetStart,
                StickFrameEncoder.SetStartPayload(SetId, StickProtocol.SetFlagsWithAdFlags, interval, tx, mfr, data)).ConfigureAwait(false);

            if (ack.Status == StickStatus.Ok)
            {
                _running = true;
                Volatile.Write(ref _sentVersion, version);
            }
            else if (ack.Status == StickStatus.AlreadyRunning)
            {
                // Our first SET_START arrived although its answer was lost: bring the data up to date.
                _running = true;
                await SendUpdateAsync().ConfigureAwait(false);
            }
            else
            {
                _running = false;
                LogIfFailed("SET_START", ack);
            }
        }
        catch (StickException ex)
        {
            // Stick unreachable: remembered, started again by ResumeAsync after the reconnect.
            _running = false;
            _logger.LogDebug("USB stick set {SetId}: start deferred: {Message}", SetId, ex.Message);
        }
    }

    /// <summary>
    /// Sends SET_UPDATE with the latest data; a vanished set is started again.
    /// </summary>
    private async Task SendUpdateAsync()
    {
        var (version, _, _, mfr, data) = Snapshot();
        try
        {
            var ack = await _connection.RequestAckAsync(
                StickProtocol.TypeSetUpdate,
                StickFrameEncoder.SetUpdatePayload(SetId, mfr, data)).ConfigureAwait(false);

            if (ack.Status == StickStatus.Ok)
            {
                Volatile.Write(ref _sentVersion, version);
            }
            else if (ack.Status == StickStatus.SetNotFound)
            {
                _running = false;
                await SendStartAsync().ConfigureAwait(false);
            }
            else
            {
                LogIfFailed("SET_UPDATE", ack);
            }
        }
        catch (StickException ex)
        {
            _running = false;
            _logger.LogDebug("USB stick set {SetId}: update failed: {Message}", SetId, ex.Message);
        }
    }

    private async Task StopAndReleaseAsync()
    {
        try
        {
            await StopAsync().ConfigureAwait(false);
        }
        finally
        {
            await _connection.ReleaseLeaseAsync(this).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Logs an unexpected status code (anything except OK and the tolerated one).
    /// </summary>
    private void LogIfFailed(string request, StickAck ack, StickStatus? tolerated = null)
    {
        if (ack.Status != StickStatus.Ok && ack.Status != tolerated)
        {
            _logger.LogWarning("USB stick set {SetId}: {Request} answered {Status}", SetId, request, ack.Status);
        }
    }
}
