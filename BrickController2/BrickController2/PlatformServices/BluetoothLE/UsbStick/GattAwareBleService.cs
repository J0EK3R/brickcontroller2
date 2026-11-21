// Platform independent decorator of IBluetoothLEService: scan and GATT devices go through the stick or the
// wrapped platform service according to the policy; advertising keeps its own redirect rules.

using System;
using System.Threading;
using System.Threading.Tasks;
using BrickController2.Diagnostics;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Wraps the Windows Bluetooth service. Kept in the shared project (it only uses interfaces) so that all of
/// its decisions are covered by unit tests; the WinUI class derives from it and only adds the DI constructor.
/// <list type="bullet">
/// <item><see cref="ScanDevicesAsync"/> and <see cref="GetKnownDeviceAsync"/> follow <see cref="GattPathSelector"/>;</item>
/// <item>the "Bluetooth on" check also accepts a usable stick, so no prompt appears while only the stick works;</item>
/// <item>advertiser devices use a stick set if possible, else the wrapped service.</item>
/// </list>
/// </summary>
public class GattAwareBleService : IBluetoothLEService
{
    private readonly IBluetoothLEService _inner;
    private readonly UsbAdvertiserService _usbAdvertiser;
    private readonly UsbGattService _usbGatt;

    public GattAwareBleService(IBluetoothLEService inner, UsbAdvertiserService usbAdvertiser, UsbGattService usbGatt)
    {
        _inner = inner;
        _usbAdvertiser = usbAdvertiser;
        _usbGatt = usbGatt;
    }

    /// <inheritdoc />
    public Task<bool> IsBluetoothLESupportedAsync() => _inner.IsBluetoothLESupportedAsync();

    /// <inheritdoc />
    public Task<bool> IsBluetoothLEAdvertisingSupportedAsync() => _inner.IsBluetoothLEAdvertisingSupportedAsync();

    /// <inheritdoc />
    public async Task<bool> IsBluetoothOnAsync()
        => await _usbGatt.IsBluetoothUsableAsync(await _inner.IsBluetoothOnAsync().ConfigureAwait(false)).ConfigureAwait(false);

    /// <summary>
    /// Scans through the path chosen for this scan. With the stick as path the Windows radio is not used at all.
    /// With no path (radio off or setting excludes the stick) the call completes at once: successfully when BC2
    /// considers Bluetooth usable (advertising through the stick still runs in the device manager and no error
    /// is shown), otherwise with false like the Windows service.
    /// </summary>
    public async Task<bool> ScanDevicesAsync(Action<ScanResult> scanCallback, CancellationToken token)
    {
        bool radioOn = await _inner.IsBluetoothOnAsync().ConfigureAwait(false);

        switch (await _usbGatt.ChooseScanPathAsync(radioOn).ConfigureAwait(false))
        {
            case GattPath.Stick:
                if (await _usbGatt.ScanAsync(scanCallback, token).ConfigureAwait(false))
                {
                    return true;
                }

                // The stick refused to scan: Windows takes over when it can.
                return radioOn && await _inner.ScanDevicesAsync(scanCallback, token).ConfigureAwait(false);

            case GattPath.Windows:
                return await _inner.ScanDevicesAsync(scanCallback, token).ConfigureAwait(false);

            default:
                return await _usbGatt.IsBluetoothUsableAsync(radioOn).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Returns a device on the chosen path, or null (a normal connection error for BC2: no crash and no
    /// prompt loop) when no path can serve it, e.g. StickOnly without stick or without a free slot.
    /// </summary>
    public async Task<IBluetoothLEDevice?> GetKnownDeviceAsync(string address)
    {
        bool radioOn = await _inner.IsBluetoothOnAsync().ConfigureAwait(false);
        GattPath path = await _usbGatt.ChooseDevicePathAsync(radioOn).ConfigureAwait(false);
        UsbStickTrace.Write($"GetKnownDeviceAsync {address}: path={path}");

        switch (path)
        {
            case GattPath.Stick:
                // The slot may have been taken since the decision; Auto then overflows to Windows.
                IBluetoothLEDevice? stickDevice = _usbGatt.TryCreateDevice(address);
                if (stickDevice != null)
                {
                    return stickDevice;
                }

                bool overflow = radioOn && _usbGatt.GattMode == UsbGattMode.Auto;
                UsbStickTrace.Write($"GetKnownDeviceAsync {address}: stick slot lost after the decision -> {(overflow ? "falling back to the Windows service" : "returning null (connect will fail)")}");
                return overflow ? await _inner.GetKnownDeviceAsync(address).ConfigureAwait(false) : null;

            case GattPath.Windows:
                return await _inner.GetKnownDeviceAsync(address).ConfigureAwait(false);

            default:
                UsbStickTrace.Write($"GetKnownDeviceAsync {address}: no path available -> returning null (connect will fail)");
                return null;
        }
    }

    /// <summary>Uses a stick set if possible, otherwise the wrapped service's publisher.</summary>
    public IBluetoothLEAdvertiserDevice? CreateBluetoothLEAdvertiserDevice()
        => _usbAdvertiser.TryCreateAdvertiserDevice() ?? _inner.CreateBluetoothLEAdvertiserDevice();
}
