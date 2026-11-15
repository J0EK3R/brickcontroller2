// Tests of the shared BluetoothAdvertisingDeviceHandler pipeline: proves that data changes reach the
// platform advertiser quickly and are not blocked by a slow platform call (task 003-win-advertising).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using BrickController2.DeviceManagement;
using BrickController2.PlatformServices.BluetoothLE;
using FluentAssertions;
using Moq;
using Xunit;

namespace BrickController2.Tests.PlatformServices.BluetoothLE;

public class BluetoothAdvertisingDeviceHandlerTests
{
    /// <summary>Platform advertiser recording calls with timestamps; update duration is configurable.</summary>
    private sealed class RecordingAdvertiser : IBluetoothLEAdvertiserDevice
    {
        public readonly object Lock = new();
        public readonly List<(string Call, long Ms)> Calls = new();
        public readonly Stopwatch Clock = Stopwatch.StartNew();
        public int UpdateDelayMs { get; set; }
        public int Count(string call) { lock (Lock) { return Calls.Count(c => c.Call == call); } }

        private void Record(string call) { lock (Lock) { Calls.Add((call, Clock.ElapsedMilliseconds)); } }

        public Task StartAdvertiseAsync(AdvertisingInterval i, TxPowerLevel p, ushort m, byte[] d) { Record("start"); return Task.CompletedTask; }
        public Task StopAdvertiseAsync() { Record("stop"); return Task.CompletedTask; }
        public async Task UpdateAdvertisedDataAsync(ushort m, byte[] d) { Record("update"); await Task.Delay(UpdateDelayMs); }
        public void Dispose() => Record("dispose");
    }

    private readonly RecordingAdvertiser _advertiser = new();
    private readonly BluetoothAdvertisingDeviceHandler _handler;
    private readonly BluetoothAdvertisingDevice _requester;
    private int _value;

    public BluetoothAdvertisingDeviceHandlerTests()
    {
        var ble = new Mock<IBluetoothLEService>();
        ble.Setup(b => b.CreateBluetoothLEAdvertiserDevice()).Returns(_advertiser);

        _handler = new BluetoothAdvertisingDeviceHandler(ble.Object, 0x1234, TryGetTelegram, TimeSpan.FromSeconds(30));

        // the handler only uses the device as a list key, so a loose mock is sufficient
        _requester = new Mock<BluetoothAdvertisingDevice>("n", "a", Mock.Of<IDeviceRepository>(), ble.Object).Object;
    }

    private bool TryGetTelegram(bool connect, out byte[] data)
    {
        data = [(byte)Volatile.Read(ref _value)];
        return true;
    }

    private async Task StartAsync()
    {
        (await _handler.TryConnectAsync(_requester)).Should().BeTrue();
        (await _handler.StartOutputTaskAsync(_requester)).Should().BeTrue();
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var watch = Stopwatch.StartNew();
        while (!condition() && watch.ElapsedMilliseconds < timeoutMs)
        {
            await Task.Delay(5);
        }
    }

    [Fact]
    public async Task NotifyDataChanged_ReachesPlatformAdvertiserQuickly()
    {
        await StartAsync();
        await WaitUntilAsync(() => _advertiser.Count("update") >= 1);
        await Task.Delay(150); // let the loop reach its wait state
        int before = _advertiser.Count("update");

        var watch = Stopwatch.StartNew();
        Volatile.Write(ref _value, 5);
        _handler.NotifyDataChanged();
        await WaitUntilAsync(() => _advertiser.Count("update") > before);

        _advertiser.Count("update").Should().BeGreaterThan(before);
        watch.ElapsedMilliseconds.Should().BeLessThan(400, "shared code may add at most ~100 ms");

        await _handler.TryDisconnectAsync(_requester);
    }

    [Fact]
    public async Task NotifyDataChanged_RightAfterStart_IsNotLost()
    {
        await StartAsync();
        Volatile.Write(ref _value, 9);
        _handler.NotifyDataChanged(); // immediately after StartOutputTaskAsync returned

        await WaitUntilAsync(() => _advertiser.Count("update") >= 2, 1000);

        _advertiser.Count("update").Should().BeGreaterThanOrEqualTo(2, "the change must not wait for the 2 s refresh");
        await _handler.TryDisconnectAsync(_requester);
    }

    [Fact]
    public async Task SlowPlatformUpdate_DoesNotBlockNotifyDataChanged()
    {
        _advertiser.UpdateDelayMs = 1000;
        await StartAsync();
        await WaitUntilAsync(() => _advertiser.Count("update") >= 1);

        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            _handler.NotifyDataChanged();
        }

        watch.ElapsedMilliseconds.Should().BeLessThan(100, "callers must never wait for the platform");
        await _handler.TryDisconnectAsync(_requester);
    }

    [Fact]
    public async Task Disconnect_StopsAdvertisingAndDisposesDevice()
    {
        await StartAsync();

        await _handler.TryDisconnectAsync(_requester);

        _advertiser.Count("stop").Should().Be(1);
        _advertiser.Count("dispose").Should().Be(1);
    }
}
