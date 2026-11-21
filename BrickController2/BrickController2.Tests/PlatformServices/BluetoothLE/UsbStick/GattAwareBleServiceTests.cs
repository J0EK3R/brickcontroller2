// Decorator tests with a fake Windows service: path decisions for scan and device creation, the "Bluetooth on"
// answer, overflow and fallbacks, and end-to-end flows that include simulated advertising sets.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BrickController2.DeviceManagement;
using BrickController2.PlatformServices.BluetoothLE;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using FluentAssertions;
using Xunit;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

public class GattAwareBleServiceTests
{
    private static async Task<(Task<bool> Scan, CancellationTokenSource Cts, List<ScanResult> Results)> StartScanAsync(GattServiceRig rig)
    {
        var cts = new CancellationTokenSource();
        var results = new List<ScanResult>();
        var scan = rig.Service.ScanDevicesAsync(r => { lock (results) { results.Add(r); } }, cts.Token);
        await rig.Harness.PumpAsync(TimeSpan.FromMilliseconds(100));
        return (scan, cts, results);
    }

    // ------------------------------------------------------------------ priority and fallback

    [Fact]
    public async Task Auto_StickPlugged_RadioOn_ScanAndDevicesUseTheStick()
    {
        using var rig = new GattServiceRig();
        rig.Stick.AddPeripheral(GattFixtures.Generic());

        var (scan, cts, _) = await StartScanAsync(rig);
        rig.Stick.Scanning.Should().BeTrue();
        rig.Windows.ScanCalls.Should().Be(0, "the stick has priority");
        cts.Cancel();
        (await rig.Harness.RunAsync(scan)).Should().BeTrue();
        rig.Stick.Scanning.Should().BeFalse("the stick scan stops with the token");

        var device = await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync(GattFixtures.GenericAddress));
        device.Should().BeOfType<StickGattDevice>();
        rig.Windows.KnownDeviceCalls.Should().Be(0);
    }

    [Fact]
    public async Task Auto_RadioOff_StickWorksWithoutPrompt()
    {
        using var rig = new GattServiceRig();
        rig.Windows.RadioOn = false;
        rig.Stick.AddPeripheral(GattFixtures.Duplo());

        (await rig.Harness.RunAsync(rig.Service.IsBluetoothOnAsync())).Should().BeTrue("no enable-Bluetooth prompt while the stick works");
        var (scan, cts, results) = await StartScanAsync(rig);
        rig.Stick.EmitAdvertising(rig.Stick.Peripherals[GattFixtures.DuploAddress]);
        results.Should().NotBeEmpty();
        cts.Cancel();
        (await rig.Harness.RunAsync(scan)).Should().BeTrue();

        var device = await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync(GattFixtures.DuploAddress));
        var services = await rig.Harness.RunAsync(device!.ConnectAndDiscoverServicesAsync(true, (_, _) => { }, _ => { }, default));
        services.Should().NotBeNull();
        rig.Windows.ScanCalls.Should().Be(0);
        rig.Windows.KnownDeviceCalls.Should().Be(0);
    }

    [Fact]
    public async Task Auto_StickUnplugged_FallsBackToWindows()
    {
        using var rig = new GattServiceRig(plugged: false);

        var (scan, cts, _) = await StartScanAsync(rig);
        rig.Windows.ScanCalls.Should().Be(1);
        cts.Cancel();
        await rig.Harness.RunAsync(scan);

        var device = await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync(GattFixtures.GenericAddress));
        device.Should().BeOfType<FakeWindowsDevice>();
    }

    [Fact]
    public async Task Auto_StickUnpluggedAfterStickScan_NextConnectUsesWindows()
    {
        using var rig = new GattServiceRig();
        rig.Stick.AddPeripheral(GattFixtures.Generic());
        var (scan, cts, _) = await StartScanAsync(rig);
        rig.Stick.EmitAdvertising(rig.Stick.Peripherals[GattFixtures.GenericAddress]);
        cts.Cancel();
        await rig.Harness.RunAsync(scan);

        rig.Harness.Locator.Ports.Clear();
        rig.Harness.Locator.Created.Last().Unplug();
        await rig.Harness.PumpUntilAsync(() => rig.Harness.Connection.Status.State != UsbStickState.Connected);
        await rig.Harness.PumpAsync(TimeSpan.FromSeconds(11)); // negative probe cache of the connection expires

        var device = await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync(GattFixtures.GenericAddress));
        device.Should().BeOfType<FakeWindowsDevice>();
    }

    [Fact]
    public async Task WindowsOnly_UsesWindowsEvenWithStick()
    {
        using var rig = new GattServiceRig();
        rig.Gatt.GattMode = UsbGattMode.WindowsOnly;

        var (scan, cts, _) = await StartScanAsync(rig);
        rig.Windows.ScanCalls.Should().Be(1);
        rig.Stick.Scanning.Should().BeFalse();
        cts.Cancel();
        await rig.Harness.RunAsync(scan);

        (await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync(GattFixtures.GenericAddress))).Should().BeOfType<FakeWindowsDevice>();
        rig.Stick.ConnectRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task StickOnly_WithoutStick_FailsCleanly_WithoutPromptLoop()
    {
        using var rig = new GattServiceRig(plugged: false);
        rig.Gatt.GattMode = UsbGattMode.StickOnly;

        (await rig.Harness.RunAsync(rig.Service.ScanDevicesAsync(_ => { }, CancellationToken.None))).Should().BeFalse();
        (await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync(GattFixtures.GenericAddress))).Should().BeNull();
        rig.Windows.ScanCalls.Should().Be(0);
        rig.Windows.KnownDeviceCalls.Should().Be(0);

        // answered repeatedly without hanging or throwing; BC2 shows its normal "Bluetooth not available" message once
        for (int i = 0; i < 3; i++)
        {
            (await rig.Harness.RunAsync(rig.Service.IsBluetoothOnAsync())).Should().BeFalse();
        }
    }

    [Fact]
    public async Task StickOnly_WithStick_NeverUsesWindows_EvenWithRadioOn()
    {
        using var rig = new GattServiceRig();
        rig.Gatt.GattMode = UsbGattMode.StickOnly;
        rig.Stick.AddPeripheral(GattFixtures.Generic());

        (await rig.Harness.RunAsync(rig.Service.IsBluetoothOnAsync())).Should().BeTrue();
        (await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync(GattFixtures.GenericAddress))).Should().BeOfType<StickGattDevice>();
        rig.Windows.KnownDeviceCalls.Should().Be(0);
    }

    [Fact]
    public async Task V10Stick_WithRadioOff_AdvertisingOnlyUser_ScanCompletesWithoutError()
    {
        using var rig = new GattServiceRig(maxConns: 4);
        rig.Stick.GattEnabled = false;
        rig.Windows.RadioOn = false;

        (await rig.Harness.RunAsync(rig.Service.IsBluetoothOnAsync())).Should().BeTrue("the stick still advertises (advertiser Auto)");
        (await rig.Harness.RunAsync(rig.Service.ScanDevicesAsync(_ => { }, CancellationToken.None))).Should().BeTrue("no error for the advertising part");
        (await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync(GattFixtures.GenericAddress))).Should().BeNull();
        rig.Windows.ScanCalls.Should().Be(0);
    }

    [Fact]
    public async Task RadioOffAndNothingUsable_ScanReturnsFalse()
    {
        using var rig = new GattServiceRig(plugged: false);
        rig.Windows.RadioOn = false;

        (await rig.Harness.RunAsync(rig.Service.IsBluetoothOnAsync())).Should().BeFalse();
        (await rig.Harness.RunAsync(rig.Service.ScanDevicesAsync(_ => { }, CancellationToken.None))).Should().BeFalse();
        (await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync(GattFixtures.GenericAddress))).Should().BeNull();
    }

    [Fact]
    public async Task StickRefusesScan_AutoWithRadioOn_FallsBackToWindowsScan()
    {
        using var rig = new GattServiceRig();
        rig.Stick.RefuseScan = true;

        var (scan, cts, _) = await StartScanAsync(rig);

        rig.Windows.ScanCalls.Should().Be(1);
        cts.Cancel();
        await rig.Harness.RunAsync(scan);
    }

    // ------------------------------------------------------------------ slots / overflow

    [Fact]
    public async Task Auto_AllSlotsUsed_RadioOn_OverflowDeviceUsesWindows()
    {
        using var rig = new GattServiceRig(maxConns: 2);
        var first = await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync("AA:BB:CC:00:00:01"));
        var second = await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync("AA:BB:CC:00:00:02"));

        var third = await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync("AA:BB:CC:00:00:03"));

        first.Should().BeOfType<StickGattDevice>();
        second.Should().BeOfType<StickGattDevice>();
        third.Should().BeOfType<FakeWindowsDevice>("overflow to Windows (Auto)");
    }

    [Fact]
    public async Task Auto_AllSlotsUsed_RadioOff_NoDevice()
    {
        using var rig = new GattServiceRig(maxConns: 1);
        rig.Windows.RadioOn = false;
        (await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync("AA:BB:CC:00:00:01"))).Should().BeOfType<StickGattDevice>();

        (await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync("AA:BB:CC:00:00:02"))).Should().BeNull();
    }

    [Fact]
    public async Task StickOnly_AllSlotsUsed_FailsCleanly()
    {
        using var rig = new GattServiceRig(maxConns: 1);
        rig.Gatt.GattMode = UsbGattMode.StickOnly;
        (await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync("AA:BB:CC:00:00:01"))).Should().BeOfType<StickGattDevice>();

        (await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync("AA:BB:CC:00:00:02"))).Should().BeNull();
        rig.Windows.KnownDeviceCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(9)]
    public async Task SlotCountComesFromTheStick_NotFromAConstant(int maxConns)
    {
        using var rig = new GattServiceRig(maxConns: maxConns);

        var devices = new List<IBluetoothLEDevice?>();
        for (int i = 0; i < maxConns + 2; i++)
        {
            devices.Add(await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync($"AA:BB:CC:00:01:{i:X2}")));
        }

        devices.Count(d => d is StickGattDevice).Should().Be(maxConns);
        devices.Count(d => d is FakeWindowsDevice).Should().Be(2);
    }

    // ------------------------------------------------------------------ advertising keeps working

    [Fact]
    public async Task AdvertiserDevices_StillUseStickSetsFirst()
    {
        using var rig = new GattServiceRig();
        await rig.Harness.ConnectStickAsync();

        var device = rig.Service.CreateBluetoothLEAdvertiserDevice();

        device.Should().BeOfType<StickAdvertiserDevice>();
        rig.Windows.AdvertiserCalls.Should().Be(0);
    }

    [Fact]
    public async Task AdvertiserDevices_AdvertiserOff_FallBackToInner()
    {
        using var rig = new GattServiceRig();
        rig.Advertiser.Mode = UsbAdvertiserMode.Off;

        rig.Service.CreateBluetoothLEAdvertiserDevice();

        rig.Windows.AdvertiserCalls.Should().Be(1);
        await Task.CompletedTask;
    }

    // ------------------------------------------------------------------ end to end

    [Fact]
    public async Task ThreeAdvertisingSets_AndGattDevicesFillAllSlots_OverflowGoesToWindows()
    {
        using var rig = new GattServiceRig(maxConns: 3, maxSets: 5);
        rig.Stick.AddPeripheral(GattFixtures.Duplo());
        rig.Stick.AddPeripheral(GattFixtures.MkDiy());
        rig.Stick.AddPeripheral(GattFixtures.Wedo2());
        var leases = new List<StickSetLease>();
        await rig.Harness.ConnectStickAsync();
        for (int i = 0; i < 3; i++)
        {
            var lease = rig.Harness.Connection.TryAcquireLease()!;
            await lease.StartAsync(32, 127, 0xFFF0, [1, 2, 3, 4]);
            leases.Add(lease);
        }

        var devices = new List<IBluetoothLEDevice>();
        foreach (string address in new[] { GattFixtures.DuploAddress, GattFixtures.MkDiyAddress, GattFixtures.Wedo2Address })
        {
            var device = (await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync(address)))!;
            (await rig.Harness.RunAsync(device.ConnectAndDiscoverServicesAsync(false, (_, _) => { }, _ => { }, default))).Should().NotBeNull();
            devices.Add(device);
        }

        rig.Stick.Sets.Should().HaveCount(3, "the sets keep running next to the GATT links");
        rig.Stick.ActiveLinks.Should().HaveCount(3);
        (await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync("AA:BB:CC:00:00:99"))).Should().BeOfType<FakeWindowsDevice>();
        rig.Stick.Scanning.Should().BeFalse();
    }

    [Fact]
    public async Task ScanAndAdvertisingAtTheSameTime_AreNormal()
    {
        using var rig = new GattServiceRig();
        await rig.Harness.ConnectStickAsync();
        var lease = rig.Harness.Connection.TryAcquireLease()!;
        await lease.StartAsync(32, 127, 0xFFF0, [1, 2, 3, 4]);
        rig.Stick.AddPeripheral(GattFixtures.MkDiy());

        var (scan, cts, results) = await StartScanAsync(rig);
        rig.Stick.EmitAdvertising(rig.Stick.Peripherals[GattFixtures.MkDiyAddress]);

        rig.Stick.Sets.Should().HaveCount(1);
        rig.Stick.Scanning.Should().BeTrue();
        results.Should().NotBeEmpty();
        cts.Cancel();
        await rig.Harness.RunAsync(scan);
        rig.Stick.Sets.Should().HaveCount(1, "stopping the scan does not touch the sets");
    }

    [Fact]
    public async Task UnplugDuringWriteLoop_DevicesSeeDisconnect_SetsResume_LinksDoNot()
    {
        using var rig = new GattServiceRig(maxConns: 3);
        rig.Stick.AddPeripheral(GattFixtures.MkDiy());
        await rig.Harness.ConnectStickAsync();
        var lease = rig.Harness.Connection.TryAcquireLease()!;
        await lease.StartAsync(32, 127, 0xFFF0, [1, 2, 3, 4]);
        var device = (await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync(GattFixtures.MkDiyAddress)))!;
        int disconnects = 0;
        var services = (await rig.Harness.RunAsync(device.ConnectAndDiscoverServicesAsync(false, (_, _) => { }, _ => Interlocked.Increment(ref disconnects), default)))!.ToList();
        var chr = services.SelectMany(s => s.Characteristics).First();

        int ok = 0;
        for (int i = 0; i < 30; i++)
        {
            if (await rig.Harness.RunAsync(device.WriteNoResponseAsync(chr, new byte[18], default))) { ok++; }
            await rig.Harness.PumpAsync(TimeSpan.FromMilliseconds(10));
        }

        rig.Harness.Locator.Ports.Clear();
        rig.Harness.Locator.Created.Last().Unplug();
        await rig.Harness.PumpUntilAsync(() => Volatile.Read(ref disconnects) == 1);
        (await rig.Harness.RunAsync(device.WriteNoResponseAsync(chr, new byte[18], default))).Should().BeFalse();

        var newStick = rig.Harness.Plug("COM12", maxConns: 3);
        newStick.AddPeripheral(GattFixtures.MkDiy());
        (await rig.Harness.PumpUntilAsync(() => newStick.Sets.ContainsKey(0))).Should().BeTrue();
        await rig.Harness.PumpAsync(TimeSpan.FromSeconds(2));

        ok.Should().Be(30);
        disconnects.Should().Be(1);
        newStick.Requests(StickProtocol.TypeConnect).Should().BeEmpty("links are not resumed");
        newStick.Sets.Should().HaveCount(1);
    }

    [Fact]
    public async Task ButtonPressed_LinksDisconnected_SetsNotRestarted()
    {
        using var rig = new GattServiceRig(maxConns: 3);
        rig.Stick.AddPeripheral(GattFixtures.Duplo());
        await rig.Harness.ConnectStickAsync();
        var lease = rig.Harness.Connection.TryAcquireLease()!;
        await lease.StartAsync(32, 127, 0xFFF0, [1, 2, 3, 4]);
        var device = (await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync(GattFixtures.DuploAddress)))!;
        int disconnects = 0;
        await rig.Harness.RunAsync(device.ConnectAndDiscoverServicesAsync(true, (_, _) => { }, _ => Interlocked.Increment(ref disconnects), default));

        rig.Stick.PressButton();
        (await rig.Harness.PumpUntilAsync(() => Volatile.Read(ref disconnects) == 1)).Should().BeTrue();
        await rig.Harness.PumpAsync(TimeSpan.FromSeconds(2));

        rig.Stick.Sets.Should().BeEmpty();
        disconnects.Should().Be(1);
        rig.Harness.Connection.Status.State.Should().Be(UsbStickState.Connected);
    }

    [Fact]
    public async Task StickScan_ThenConnect_UsesRememberedAddressType()
    {
        using var rig = new GattServiceRig();
        var p = rig.Stick.AddPeripheral(GattFixtures.Generic());
        var (scan, cts, _) = await StartScanAsync(rig);
        rig.Stick.EmitAdvertising(p); // random address (type 1)
        p.AdvData = GattFixtures.Ad(0xFF, 1, 2);
        rig.Stick.EmitAdvertising(p);
        cts.Cancel();
        await rig.Harness.RunAsync(scan);

        var device = (await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync(GattFixtures.GenericAddress)))!;
        await rig.Harness.RunAsync(device.ConnectAndDiscoverServicesAsync(false, (_, _) => { }, _ => { }, default));

        rig.Stick.ConnectRequests.Single().AddressType.Should().Be(1);
    }

    [Fact]
    public async Task DeviceFoundByWindowsScan_ConnectsViaStickWithAutoType()
    {
        // The stick was plugged in after a Windows scan: stick priority applies, the type is unknown -> 0xFF.
        using var rig = new GattServiceRig();
        rig.Stick.AddPeripheral(GattFixtures.Generic());

        var device = (await rig.Harness.RunAsync(rig.Service.GetKnownDeviceAsync(GattFixtures.GenericAddress)))!;
        await rig.Harness.RunAsync(device.ConnectAndDiscoverServicesAsync(false, (_, _) => { }, _ => { }, default));

        rig.Stick.ConnectRequests.Single().AddressType.Should().Be(0xFF);
    }
}
