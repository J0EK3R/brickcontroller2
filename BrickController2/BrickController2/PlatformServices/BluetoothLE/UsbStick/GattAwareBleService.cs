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
    private readonly TimeProvider _clock;

    // Cache of the wrapped service's answers. On Windows every query is a set of WinRT calls (BluetoothAdapter,
    // Radio) that throw first-chance exceptions internally (0x80070490); under a debugger each one stalls the whole
    // process, which starved the stick keep-alive. The device manager and view models ask very often, so the
    // answers are reused for a few seconds.
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(5);
    private (bool Value, long Stamp)? _radioCache;
    private (bool Value, long Stamp)? _supportedCache;

    public GattAwareBleService(IBluetoothLEService inner, UsbAdvertiserService usbAdvertiser, UsbGattService usbGatt, TimeProvider? clock = null)
    {
        _inner = inner;
        _usbAdvertiser = usbAdvertiser;
        _usbGatt = usbGatt;
        _clock = clock ?? TimeProvider.System;

        // A changed setting makes the cached radio state meaningless (e.g. StickOnly reported "radio off" without asking).
        _usbGatt.ModeChanged += (_, _) => InvalidateCaches();
        _usbAdvertiser.ModeChanged += (_, _) => InvalidateCaches();
    }

    /// <summary>Forgets the cached answers of the wrapped service so the next decision asks again.</summary>
    private void InvalidateCaches()
    {
        _radioCache = null;
        _supportedCache = null;
        UsbStickTrace.Write("USB mode changed: Windows radio/supported cache invalidated");
    }

    /// <summary>
    /// Returns a cached answer of <paramref name="query"/> while it is younger than <see cref="CacheLifetime"/>.
    /// A race of two callers only costs one extra query; the cache is a pair of immutable values.
    /// </summary>
    private async Task<bool> CachedAsync(Func<Task<bool>> query, Func<(bool Value, long Stamp)?> read, Action<(bool Value, long Stamp)> write)
    {
        var cached = read();
        if (cached is { } c && _clock.GetElapsedTime(c.Stamp) < CacheLifetime)
        {
            return c.Value;
        }

        bool value = await query().ConfigureAwait(false);
        write((value, _clock.GetTimestamp()));
        return value;
    }

    /// <summary>
    /// State of the Windows radio as input for the path decisions. With the setting StickOnly the radio is
    /// irrelevant (see <see cref="BluetoothAvailability"/>), so it is reported as off and the Windows
    /// Bluetooth APIs are not touched at all; otherwise the answer is cached for a few seconds.
    /// </summary>
    private Task<bool> RadioOnAsync()
        => _usbGatt.GattMode == UsbGattMode.StickOnly
            ? Task.FromResult(false)
            : CachedAsync(_inner.IsBluetoothOnAsync, () => _radioCache, v => _radioCache = v);

    /// <inheritdoc />
    public Task<bool> IsBluetoothLESupportedAsync()
        => _usbGatt.GattMode == UsbGattMode.StickOnly
            ? Task.FromResult(true) // not needed: the stick path never asks the OS
            : CachedAsync(_inner.IsBluetoothLESupportedAsync, () => _supportedCache, v => _supportedCache = v);

    /// <inheritdoc />
    public Task<bool> IsBluetoothLEAdvertisingSupportedAsync() => _inner.IsBluetoothLEAdvertisingSupportedAsync();

    /// <inheritdoc />
    public async Task<bool> IsBluetoothOnAsync()
        => await _usbGatt.IsBluetoothUsableAsync(await RadioOnAsync().ConfigureAwait(false)).ConfigureAwait(false);

    /// <summary>
    /// Scans through the path chosen for this scan. With the stick as path the Windows radio is not used at all.
    /// With no path (radio off or setting excludes the stick) the call completes at once: successfully when BC2
    /// considers Bluetooth usable (advertising through the stick still runs in the device manager and no error
    /// is shown), otherwise with false like the Windows service.
    /// </summary>
    public async Task<bool> ScanDevicesAsync(Action<ScanResult> scanCallback, CancellationToken token)
    {
        bool radioOn = await RadioOnAsync().ConfigureAwait(false);

        switch (await _usbGatt.ChooseScanPathAsync(radioOn).ConfigureAwait(false))
        {
            case GattPath.Stick:
                if (await _usbGatt.ScanAsync(scanCallback, token).ConfigureAwait(false))
                {
                    return true;
                }

                // The stick refused to scan or was switched off by the setting: Windows takes over when it can
                // (asked again, the first answer may be the "radio off" shortcut of StickOnly).
                radioOn = await RadioOnAsync().ConfigureAwait(false);
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
        bool radioOn = await RadioOnAsync().ConfigureAwait(false);
        GattPath path = await _usbGatt.ChooseDevicePathAsync(radioOn).ConfigureAwait(false);

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
