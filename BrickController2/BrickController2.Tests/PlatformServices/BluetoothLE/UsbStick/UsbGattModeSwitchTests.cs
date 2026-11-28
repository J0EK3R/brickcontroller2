// Tests for the scenario "stick in use (Auto) -> user switches the setting -> device connects through Windows".

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BrickController2.PlatformServices.BluetoothLE;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using FluentAssertions;
using Xunit;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

public class UsbGattModeSwitchTests
{
    /// <summary>
    /// Connects a device through the stick (mode Auto) and returns it with a disconnect counter.
    /// </summary>
    private static async Task<(IBluetoothLEDevice Device, Func<int> Disconnects)> ConnectViaStickAsync(GattServiceRig rig)
    {
        rig.Stick.AddPeripheral(GattFixtures.Generic());
        var device = await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync(GattFixtures.GenericAddress));
        device.Should().BeOfType<StickGattDevice>();

        int disconnects = 0;
        var services = await rig.Harness.RunAsync(device!.ConnectAndDiscoverServicesAsync(false, (_, _) => { }, _ => Interlocked.Increment(ref disconnects), default));
        services.Should().NotBeNull();
        return (device, () => Volatile.Read(ref disconnects));
    }

    [Fact]
    public async Task SwitchToWindowsOnly_ClosesStickLinks_RaisesDisconnectCallback_AndNextConnectUsesWindows()
    {
        using var rig = new GattServiceRig();
        var (device, disconnects) = await ConnectViaStickAsync(rig);
        rig.Harness.Connection.LinkCount.Should().Be(1);

        rig.Gatt.GattMode = UsbGattMode.WindowsOnly;
        await rig.Harness.RunAsync(rig.Gatt.WhenGattClosedAsync());
        await rig.Harness.PumpUntilAsync(() => disconnects() == 1);

        disconnects().Should().Be(1, "BC2 must see a normal disconnect so that it reconnects");
        rig.Stick.DisconnectRequests.Should().HaveCount(1, "the peer is really released on the stick so it advertises again");
        await rig.Harness.PumpUntilAsync(() => rig.Harness.Connection.LinkCount == 0);
        rig.Harness.Connection.LinkCount.Should().Be(0);
        device.State.Should().Be(BluetoothLEDeviceState.Disconnected);

        (await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync(GattFixtures.GenericAddress))).Should().BeOfType<FakeWindowsDevice>();
    }

    [Fact]
    public async Task SwitchToWindowsOnly_StopsRunningStickScan_AndContinuesOnWindows()
    {
        using var rig = new GattServiceRig();
        var scan = rig.Service.ScanDevicesAsync(_ => { }, CancellationToken.None);
        await rig.Harness.PumpAsync(TimeSpan.FromMilliseconds(100));
        rig.Stick.Scanning.Should().BeTrue();

        rig.Gatt.GattMode = UsbGattMode.WindowsOnly;
        await rig.Harness.RunAsync(rig.Gatt.WhenGattClosedAsync());
        await rig.Harness.PumpUntilAsync(() => rig.Windows.ScanCalls == 1);

        rig.Stick.Scanning.Should().BeFalse();
        rig.Windows.ScanCalls.Should().Be(1, "the scan goes on through Windows");
        scan.IsCompleted.Should().BeFalse();
    }

    [Theory]
    [InlineData(UsbGattMode.Auto)]
    [InlineData(UsbGattMode.StickOnly)]
    public async Task SwitchAwayFromWindowsOnly_DoesNotTouchExistingConnections(UsbGattMode target)
    {
        using var rig = new GattServiceRig();
        rig.Gatt.GattMode = UsbGattMode.WindowsOnly;
        var windowsDevice = await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync(GattFixtures.GenericAddress));
        windowsDevice.Should().BeOfType<FakeWindowsDevice>();

        rig.Gatt.GattMode = target;
        await rig.Harness.RunAsync(rig.Gatt.WhenGattClosedAsync());

        rig.Stick.DisconnectRequests.Should().BeEmpty();
        rig.Windows.KnownDeviceCalls.Should().Be(1);
    }

    [Fact]
    public async Task SwitchToStickOnly_KeepsStickLinks()
    {
        using var rig = new GattServiceRig();
        var (_, disconnects) = await ConnectViaStickAsync(rig);

        rig.Gatt.GattMode = UsbGattMode.StickOnly;
        await rig.Harness.RunAsync(rig.Gatt.WhenGattClosedAsync());

        disconnects().Should().Be(0);
        rig.Harness.Connection.LinkCount.Should().Be(1);
    }

    [Fact]
    public async Task AdvertiserOff_DoesNotDisturbStickGattLinks()
    {
        using var rig = new GattServiceRig();
        var (_, disconnects) = await ConnectViaStickAsync(rig);

        rig.Advertiser.Mode = UsbAdvertiserMode.Off;
        await rig.Harness.PumpAsync(TimeSpan.FromSeconds(20)); // longer than the idle close delay

        disconnects().Should().Be(0);
        rig.Harness.Connection.LinkCount.Should().Be(1);
        rig.Harness.Connection.IsConnected.Should().BeTrue("a live GATT link keeps the serial connection open");
        rig.Stick.DisconnectRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task ModeChange_InvalidatesRadioCache()
    {
        using var rig = new GattServiceRig();
        await rig.Harness.RunAsync(rig.Service.IsBluetoothOnAsync());
        int checks = rig.Windows.RadioChecks;
        await rig.Harness.RunAsync(rig.Service.IsBluetoothOnAsync());
        rig.Windows.RadioChecks.Should().Be(checks, "cached");

        rig.Windows.RadioOn = false;
        rig.Advertiser.Mode = UsbAdvertiserMode.Off;
        await rig.Harness.RunAsync(rig.Service.IsBluetoothOnAsync());
        rig.Windows.RadioChecks.Should().Be(checks + 1, "advertiser mode change invalidates the cache");

        rig.Gatt.GattMode = UsbGattMode.WindowsOnly;
        await rig.Harness.RunAsync(rig.Service.IsBluetoothOnAsync());
        rig.Windows.RadioChecks.Should().Be(checks + 2, "GATT mode change invalidates the cache");
    }

    [Fact]
    public async Task StickOnlyToWindowsOnly_RadioIsAskedAfresh_NotTheStickOnlyShortcut()
    {
        using var rig = new GattServiceRig();
        rig.Gatt.GattMode = UsbGattMode.StickOnly;
        (await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync(GattFixtures.GenericAddress))).Should().BeOfType<StickGattDevice>();

        rig.Gatt.GattMode = UsbGattMode.WindowsOnly;

        (await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync(GattFixtures.GenericAddress))).Should().BeOfType<FakeWindowsDevice>();
    }
}
