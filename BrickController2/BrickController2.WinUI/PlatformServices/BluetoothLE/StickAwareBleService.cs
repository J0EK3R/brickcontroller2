using System;
using System.Threading;
using System.Threading.Tasks;
using BrickController2.PlatformServices.BluetoothLE;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;

namespace BrickController2.Windows.PlatformServices.BluetoothLE;

/// <summary>
/// Decorator around the Windows <see cref="BleService"/>: scanning and GATT stay with the Windows
/// Bluetooth stack, only the creation of advertiser devices may be redirected to the USB advertising
/// stick. Without a stick (or with the setting Off, or when all 5 sets are in use) the original Windows
/// advertiser is created, so behaviour is unchanged.
/// </summary>
internal sealed class StickAwareBleService : IBluetoothLEService
{
    private readonly BleService _inner;
    private readonly UsbAdvertiserService _usbAdvertiser;

    public StickAwareBleService(BleService inner, UsbAdvertiserService usbAdvertiser)
    {
        _inner = inner;
        _usbAdvertiser = usbAdvertiser;
    }

    /// <inheritdoc />
    public Task<bool> IsBluetoothLESupportedAsync() => _inner.IsBluetoothLESupportedAsync();

    /// <inheritdoc />
    public Task<bool> IsBluetoothLEAdvertisingSupportedAsync() => _inner.IsBluetoothLEAdvertisingSupportedAsync();

    /// <inheritdoc />
    /// <remarks>
    /// Reports "on" when the Windows radio is on OR (setting Auto and the stick is reachable), so BC2 does
    /// not show "please enable Bluetooth" to a user who only advertises through the stick. The stick check
    /// is async and only runs when the radio is off (the radio-on case costs nothing extra).
    /// </remarks>
    public async Task<bool> IsBluetoothOnAsync()
    {
        bool radioOn = await _inner.IsBluetoothOnAsync();
        if (radioOn)
        {
            return true;
        }

        bool stickAvailable = await _usbAdvertiser.IsStickAvailableAsync();
        return BluetoothAvailability.IsBluetoothUsable(radioOn, stickAvailable, _usbAdvertiser.Mode);
    }

    /// <summary>
    /// Scanning needs the Windows radio. With the radio off (only the stick is usable) there is nothing to
    /// scan with, so this completes successfully and immediately: the device manager still runs the
    /// advertising part of the scan and no error is reported. The inner service would return false here.
    /// </summary>
    public async Task<bool> ScanDevicesAsync(Action<ScanResult> scanCallback, CancellationToken token)
        => !await _inner.IsBluetoothOnAsync() || await _inner.ScanDevicesAsync(scanCallback, token);

    /// <summary>
    /// GATT devices need the Windows radio. With the radio off (only the stick is usable) null is returned,
    /// which the device treats as a normal connection error (no crash, and no prompt loop because
    /// <see cref="IsBluetoothOnAsync"/> reports on). Advertising devices are unaffected.
    /// </summary>
    public async Task<IBluetoothLEDevice?> GetKnownDeviceAsync(string address)
        => await _inner.IsBluetoothOnAsync() ? await _inner.GetKnownDeviceAsync(address) : null;

    /// <summary>
    /// Uses a stick set if possible, otherwise the Windows publisher.
    /// </summary>
    public IBluetoothLEAdvertiserDevice? CreateBluetoothLEAdvertiserDevice()
        => _usbAdvertiser.TryCreateAdvertiserDevice() ?? _inner.CreateBluetoothLEAdvertiserDevice();
}
