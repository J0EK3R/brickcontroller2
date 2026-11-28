// Test rig for the policy layer: in-memory preferences, a fake "Windows" Bluetooth service and the real
// UsbGattService / GattAwareBleService on top of the simulated stick.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BrickController2.PlatformServices.BluetoothLE;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using BrickController2.UI.Services.Preferences;
using Microsoft.Extensions.Logging.Abstractions;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Dictionary backed preferences; a value of the wrong type behaves like a corrupted setting (cast exception).
/// </summary>
internal sealed class InMemoryPreferences : IPreferencesService
{
    public Dictionary<string, object> Store { get; } = new();

    public bool ContainsKey(string key, string? section = null) => Store.ContainsKey(key);

    public T Get<T>(string key, string? section = null) where T : notnull => (T)Store[key];

    public T Get<T>(string key, T defaultValue, string? section = null) where T : notnull
        => Store.TryGetValue(key, out var value) ? (T)value : defaultValue;

    public void Set<T>(string key, T value, string? section = null) where T : notnull => Store[key] = value;
}

/// <summary>
/// A device of the fake Windows service.
/// </summary>
internal sealed class FakeWindowsDevice : IBluetoothLEDevice
{
    public BluetoothLEDeviceState State => BluetoothLEDeviceState.Disconnected;

    public Task<IEnumerable<IGattService>?> ConnectAndDiscoverServicesAsync(bool autoConnect, Action<Guid, byte[]?> onCharacteristicChanged, Action<IBluetoothLEDevice> onDeviceDisconnected, CancellationToken token)
        => Task.FromResult<IEnumerable<IGattService>?>([]);

    public Task DisconnectAsync() => Task.CompletedTask;

    public Task<bool> EnableNotificationAsync(IGattCharacteristic characteristic, CancellationToken token) => Task.FromResult(true);

    public Task<bool> DisableNotificationAsync(IGattCharacteristic characteristic, CancellationToken token) => Task.FromResult(true);

    public Task<byte[]?> ReadAsync(IGattCharacteristic characteristic, CancellationToken token) => Task.FromResult<byte[]?>(null);

    public Task<bool> WriteAsync(IGattCharacteristic characteristic, byte[] data, CancellationToken token) => Task.FromResult(true);

    public Task<bool> WriteNoResponseAsync(IGattCharacteristic characteristic, byte[] data, CancellationToken token) => Task.FromResult(true);
}

/// <summary>
/// Stands in for the Windows Bluetooth service and counts what is asked of it.
/// </summary>
internal sealed class FakeWindowsBle : IBluetoothLEService
{
    public bool RadioOn { get; set; } = true;

    public int ScanCalls { get; private set; }

    public int KnownDeviceCalls { get; private set; }

    public int AdvertiserCalls { get; private set; }

    public int RadioChecks { get; private set; }

    public int SupportChecks { get; private set; }

    public Task<bool> IsBluetoothLESupportedAsync()
    {
        SupportChecks++;
        return Task.FromResult(true);
    }

    public Task<bool> IsBluetoothLEAdvertisingSupportedAsync() => Task.FromResult(true);

    public Task<bool> IsBluetoothOnAsync()
    {
        RadioChecks++;
        return Task.FromResult(RadioOn);
    }

    public async Task<bool> ScanDevicesAsync(Action<ScanResult> scanCallback, CancellationToken token)
    {
        ScanCalls++;
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }
        catch (OperationCanceledException)
        {
        }

        return RadioOn;
    }

    public Task<IBluetoothLEDevice?> GetKnownDeviceAsync(string address)
    {
        KnownDeviceCalls++;
        return Task.FromResult<IBluetoothLEDevice?>(RadioOn ? new FakeWindowsDevice() : null);
    }

    public IBluetoothLEAdvertiserDevice? CreateBluetoothLEAdvertiserDevice()
    {
        AdvertiserCalls++;
        return null;
    }
}

/// <summary>
/// The whole policy stack around one simulated stick.
/// </summary>
internal sealed class GattServiceRig : IDisposable
{
    public GattServiceRig(bool plugged = true, int maxConns = 4, int maxSets = 5)
    {
        Harness = new StickHarness(plugged, maxSets, maxConns);
        Preferences = new InMemoryPreferences();
        Advertiser = new UsbAdvertiserService(Harness.Connection, Preferences, NullLoggerFactory.Instance);
        AddressBook = new StickAddressBook();
        Gatt = new UsbGattService(Harness.Connection, Advertiser, AddressBook, Preferences, NullLoggerFactory.Instance);
        Windows = new FakeWindowsBle();
        Service = new GattAwareBleService(Windows, Advertiser, Gatt, Harness.Clock);
    }

    public StickHarness Harness { get; }

    public InMemoryPreferences Preferences { get; }

    public UsbAdvertiserService Advertiser { get; }

    public StickAddressBook AddressBook { get; }

    public UsbGattService Gatt { get; }

    public FakeWindowsBle Windows { get; }

    public GattAwareBleService Service { get; }

    public FakeStickDevice Stick => Harness.Stick!;

    public void Dispose() => Harness.Dispose();
}
