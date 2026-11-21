// IBluetoothLEDevice on top of a stick GATT link: the same contract the Windows BleDevice fulfils for BC2's
// device classes (connect + discover, notifications, read, both kinds of writes, disconnect callbacks).

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// A GATT device reached through the USB stick. Behaviour follows <c>BleDevice</c> (WinUI) so BC2's
/// <c>BluetoothDevice</c> logic needs no changes:
/// <list type="bullet">
/// <item><see cref="ConnectAndDiscoverServicesAsync"/> returns the discovered services, or null on any failure or
/// cancellation (cancelling while connecting aborts the attempt on the stick);</item>
/// <item>operations return false / null on failure (never throw because of the radio or the stick), except that a
/// cancelled token surfaces as <see cref="OperationCanceledException"/> like the Windows lock does;</item>
/// <item><c>onDeviceDisconnected</c> is called exactly once when a connected device loses its link (peer
/// switched off, supervision timeout, stick unplugged or button pressed), and never after a local disconnect;</item>
/// <item>notifications are delivered in order for characteristics that were enabled.</item>
/// </list>
/// One device object serves one connection cycle; the stick connection slot is reserved when the device is
/// created and given back on disconnect or link loss (a later connect asks for a slot again).
/// </summary>
internal sealed class StickGattDevice : IBluetoothLEDevice
{
    private readonly string _address;
    private readonly StickAddressBook _addressBook;
    private readonly Func<StickGattLink?> _acquireLink;
    private readonly ILogger _logger;

    // Serialises connect and the operations of this device (like the lock of the Windows device). Disconnect
    // deliberately does not take it, so a hanging operation can always be aborted.
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    // Guards state, link and callbacks (never held across an await).
    private readonly object _stateLock = new();

    private StickGattLink? _link;
    private BluetoothLEDeviceState _state = BluetoothLEDeviceState.Disconnected;
    private Action<Guid, byte[]?>? _onCharacteristicChanged;
    private Action<IBluetoothLEDevice>? _onDeviceDisconnected;

    // value handle -> characteristic UUID of the current connection, and the handles notifications are enabled for.
    private ConcurrentDictionary<ushort, Guid> _uuidByHandle = new();
    private readonly ConcurrentDictionary<ushort, bool> _subscribedHandles = new();

    /// <param name="address">Peer address "AA:BB:CC:DD:EE:FF".</param>
    /// <param name="initialLink">The connection slot reserved for this device.</param>
    /// <param name="acquireLink">Reserves another slot if the first one was already given back (second connect on the same object).</param>
    /// <param name="addressBook">Source of the address type learned by stick scans.</param>
    /// <param name="logger">Logger.</param>
    public StickGattDevice(string address, StickGattLink initialLink, Func<StickGattLink?> acquireLink, StickAddressBook addressBook, ILogger logger)
    {
        _address = address;
        _link = initialLink;
        _acquireLink = acquireLink;
        _addressBook = addressBook;
        _logger = logger;
    }

    /// <inheritdoc />
    public BluetoothLEDeviceState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<IGattService>?> ConnectAndDiscoverServicesAsync(
        bool autoConnect,
        Action<Guid, byte[]?> onCharacteristicChanged,
        Action<IBluetoothLEDevice> onDeviceDisconnected,
        CancellationToken token)
    {
        try
        {
            await _operationGate.WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        try
        {
            return await ConnectCoreAsync(autoConnect, onCharacteristicChanged, onDeviceDisconnected, token).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task<IEnumerable<IGattService>?> ConnectCoreAsync(
        bool autoConnect,
        Action<Guid, byte[]?> onCharacteristicChanged,
        Action<IBluetoothLEDevice> onDeviceDisconnected,
        CancellationToken token)
    {
        StickGattLink? link;
        lock (_stateLock)
        {
            if (_state != BluetoothLEDeviceState.Disconnected)
            {
                return null;
            }

            link = _link ??= _acquireLink();
            if (link == null || !StickAddress.TryParse(_address, out _))
            {
                return null;
            }

            _state = BluetoothLEDeviceState.Connecting;
            _onCharacteristicChanged = onCharacteristicChanged;
            _onDeviceDisconnected = onDeviceDisconnected;
            _uuidByHandle = new ConcurrentDictionary<ushort, Guid>();
            _subscribedHandles.Clear();
        }

        // Handlers are installed before CONNECT: the link's pump runs from EVT_CONNECTED on, so even a
        // notification right behind the SUBSCRIBE ACK finds its consumer.
        link.NotificationHandler = OnNotification;
        link.LinkLost += OnLinkLost;

        try
        {
            StickAddress.TryParse(_address, out byte[] addressBytes);
            byte addressType = _addressBook.GetAddressType(_address);

            GattResult connected = await link.ConnectAsync(addressBytes, addressType, autoConnect, token).ConfigureAwait(false);
            if (!connected.IsOk)
            {
                _logger.LogInformation("USB stick: connect to {Address} failed ({Status})", _address, connected.Status);
                await ReleaseAfterFailureAsync().ConfigureAwait(false);
                return null;
            }

            lock (_stateLock)
            {
                if (_state == BluetoothLEDeviceState.Connecting)
                {
                    _state = BluetoothLEDeviceState.Discovering;
                }
            }

            var (discovered, services) = await link.DiscoverAsync(token).ConfigureAwait(false);
            if (!discovered.IsOk)
            {
                _logger.LogInformation("USB stick: discovery on {Address} failed ({Status})", _address, discovered.Status);
                await ReleaseAfterFailureAsync().ConfigureAwait(false);
                return null;
            }

            lock (_stateLock)
            {
                // A link lost between the discovery and here must not be reported as connected.
                if (_state != BluetoothLEDeviceState.Discovering || !link.IsConnected)
                {
                    services = [];
                }
                else
                {
                    foreach (StickGattCharacteristic characteristic in services.SelectMany(s => s.Characteristics))
                    {
                        _uuidByHandle[characteristic.ValueHandle] = characteristic.Uuid;
                    }

                    _state = BluetoothLEDeviceState.Connected;
                    return services;
                }
            }

            await ReleaseAfterFailureAsync().ConfigureAwait(false);
            return null;
        }
        catch (OperationCanceledException)
        {
            // Cancelled in any phase: the link already aborted the attempt; clean up and report "no services" like WinUI.
            await ReleaseAfterFailureAsync().ConfigureAwait(false);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync()
    {
        StickGattLink? link = TakeLink(raiseCallback: false, out _);
        await ReleaseLinkAsync(link).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> EnableNotificationAsync(IGattCharacteristic characteristic, CancellationToken token)
    {
        if (characteristic is not StickGattCharacteristic { CanNotify: true } chr)
        {
            return false;
        }

        // Registered before SUBSCRIBE so a notification that follows the ACK immediately is not discarded.
        _subscribedHandles[chr.ValueHandle] = true;
        bool ok = await RunAsync(link => link.SubscribeAsync(chr.CccdHandle, 1, token), token).ConfigureAwait(false) is { IsOk: true };
        if (!ok)
        {
            _subscribedHandles.TryRemove(chr.ValueHandle, out _);
        }

        return ok;
    }

    /// <inheritdoc />
    public async Task<bool> DisableNotificationAsync(IGattCharacteristic characteristic, CancellationToken token)
    {
        if (characteristic is not StickGattCharacteristic { CanNotify: true } chr)
        {
            return false;
        }

        // Stop delivering first (like the Windows device removes its handler), then write the CCCD.
        _subscribedHandles.TryRemove(chr.ValueHandle, out _);
        return await RunAsync(link => link.SubscribeAsync(chr.CccdHandle, 0, token), token).ConfigureAwait(false) is { IsOk: true };
    }

    /// <inheritdoc />
    public async Task<byte[]?> ReadAsync(IGattCharacteristic characteristic, CancellationToken token)
    {
        if (characteristic is not StickGattCharacteristic chr)
        {
            return null;
        }

        var (result, data) = await RunAsync(link => link.ReadAsync(chr.ValueHandle, token), token).ConfigureAwait(false);

        return result.IsOk ? data : null;
    }

    /// <inheritdoc />
    public async Task<bool> WriteAsync(IGattCharacteristic characteristic, byte[] data, CancellationToken token)
        => characteristic is StickGattCharacteristic chr
           && await RunAsync(link => link.WriteAsync(chr.ValueHandle, data, token), token).ConfigureAwait(false) is { IsOk: true };

    /// <inheritdoc />
    public async Task<bool> WriteNoResponseAsync(IGattCharacteristic characteristic, byte[] data, CancellationToken token)
        => characteristic is StickGattCharacteristic chr
           && await RunAsync(link => link.WriteCommandAsync(chr.ValueHandle, data, token), token).ConfigureAwait(false) is { IsOk: true };

    // ------------------------------------------------------------------ plumbing

    /// <summary>
    /// Runs one operation on the connected link under the operation gate. Returns a failed result when the
    /// device is not connected. A cancelled token surfaces as OperationCanceledException (like the Windows lock).
    /// </summary>
    private async Task<GattResult> RunAsync(Func<StickGattLink, Task<GattResult>> operation, CancellationToken token)
    {
        var (result, _) = await RunCoreAsync(async link => (await operation(link).ConfigureAwait(false), 0), token).ConfigureAwait(false);
        return result;
    }

    /// <summary>Same as above for operations returning a value next to the status (read).</summary>
    private async Task<(GattResult Result, byte[]? Data)> RunAsync(Func<StickGattLink, Task<(GattResult Result, byte[]? Data)>> operation, CancellationToken token)
        => await RunCoreAsync(operation, token).ConfigureAwait(false);

    private async Task<(GattResult, T?)> RunCoreAsync<T>(Func<StickGattLink, Task<(GattResult, T?)>> operation, CancellationToken token)
    {
        await _operationGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            StickGattLink? link;
            lock (_stateLock)
            {
                link = _state == BluetoothLEDeviceState.Connected ? _link : null;
            }

            return link == null
                ? (new GattResult(StickStatus.NotConnected), default)
                : await operation(link).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <summary>Delivers a notification of an enabled characteristic to BC2 (runs on the link's pump task, in order).</summary>
    private void OnNotification(ushort handle, byte[] data)
    {
        Action<Guid, byte[]?>? callback;
        lock (_stateLock)
        {
            callback = _state == BluetoothLEDeviceState.Connected || _state == BluetoothLEDeviceState.Discovering ? _onCharacteristicChanged : null;
        }

        if (callback != null && _subscribedHandles.ContainsKey(handle) && _uuidByHandle.TryGetValue(handle, out Guid uuid))
        {
            callback(uuid, data);
        }
    }

    /// <summary>The link of a connected device ended without our request (peer, supervision timeout, stick gone).</summary>
    private void OnLinkLost(byte reason)
    {
        StickGattLink? link = TakeLink(raiseCallback: true, out Action<IBluetoothLEDevice>? callback);
        _logger.LogInformation("USB stick: link to {Address} lost (reason 0x{Reason:X2})", _address, reason);

        // Give the slot back right away; the callback runs after the state is Disconnected (as in WinUI).
        _ = ReleaseLinkAsync(link);

        // On the thread pool: the caller is the stick's read thread, which must never run BC2 logic.
        if (callback != null)
        {
            _ = Task.Run(() => callback(this));
        }
    }

    /// <summary>
    /// Detaches the link and resets the state. With <paramref name="raiseCallback"/> (link loss) the disconnect
    /// callback is returned only when the device was connected, so it fires exactly once.
    /// </summary>
    private StickGattLink? TakeLink(bool raiseCallback, out Action<IBluetoothLEDevice>? callback)
    {
        lock (_stateLock)
        {
            callback = raiseCallback && _state == BluetoothLEDeviceState.Connected ? _onDeviceDisconnected : null;

            // During connect/discover a loss is reported through the failed connect, not through the callback.
            if (raiseCallback && _state != BluetoothLEDeviceState.Connected)
            {
                return null;
            }

            StickGattLink? link = _link;
            _link = null;
            _state = BluetoothLEDeviceState.Disconnected;
            _onDeviceDisconnected = null;
            _onCharacteristicChanged = null;
            _subscribedHandles.Clear();
            return link;
        }
    }

    private async Task ReleaseAfterFailureAsync()
    {
        StickGattLink? link = TakeLink(raiseCallback: false, out _);
        await ReleaseLinkAsync(link).ConfigureAwait(false);
    }

    /// <summary>Detaches the handlers, disconnects (idempotent) and returns the slot to the stick connection.</summary>
    private async Task ReleaseLinkAsync(StickGattLink? link)
    {
        if (link == null)
        {
            return;
        }

        link.LinkLost -= OnLinkLost;
        link.NotificationHandler = null;
        try
        {
            await link.DisconnectAsync().ConfigureAwait(false);
        }
        finally
        {
            link.Dispose();
        }
    }
}
