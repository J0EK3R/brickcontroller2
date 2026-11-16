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
    public Task<bool> IsBluetoothOnAsync() => _inner.IsBluetoothOnAsync();

    /// <inheritdoc />
    public Task<bool> ScanDevicesAsync(Action<ScanResult> scanCallback, CancellationToken token)
        => _inner.ScanDevicesAsync(scanCallback, token);

    /// <inheritdoc />
    public Task<IBluetoothLEDevice?> GetKnownDeviceAsync(string address) => _inner.GetKnownDeviceAsync(address);

    /// <summary>
    /// Uses a stick set if possible, otherwise the Windows publisher.
    /// </summary>
    public IBluetoothLEAdvertiserDevice? CreateBluetoothLEAdvertiserDevice()
        => _usbAdvertiser.TryCreateAdvertiserDevice() ?? _inner.CreateBluetoothLEAdvertiserDevice();
}
