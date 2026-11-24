// Setting storage, stick availability and path decisions of UsbGattService.

using System.Threading.Tasks;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using FluentAssertions;
using Xunit;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

public class UsbGattServiceTests
{
    [Fact]
    public void Mode_DefaultsToAuto_AndIsPersisted()
    {
        using var rig = new GattServiceRig(plugged: false);

        rig.Gatt.GattMode.Should().Be(UsbGattMode.Auto);
        rig.Gatt.GattMode = UsbGattMode.StickOnly;

        rig.Gatt.GattMode.Should().Be(UsbGattMode.StickOnly);
        rig.Preferences.Store["UsbGattMode"].Should().Be(UsbGattMode.StickOnly);
    }

    [Fact]
    public void Mode_CorruptedPreference_FallsBackToAuto()
    {
        using var rig = new GattServiceRig(plugged: false);
        rig.Preferences.Store["UsbGattMode"] = "garbage"; // wrong type: reading throws InvalidCastException

        rig.Gatt.GattMode.Should().Be(UsbGattMode.Auto);
    }

    [Fact]
    public void Mode_ChangeRaisesStatusChanged_SameValueDoesNot()
    {
        using var rig = new GattServiceRig(plugged: false);
        int raised = 0;
        rig.Gatt.StatusChanged += (_, _) => raised++;

        rig.Gatt.GattMode = UsbGattMode.WindowsOnly;
        rig.Gatt.GattMode = UsbGattMode.WindowsOnly;

        raised.Should().Be(1);
    }

    [Fact]
    public async Task StickAvailable_WithGattStick_IsTrue()
    {
        using var rig = new GattServiceRig(maxConns: 4);

        (await rig.Harness.RunAsync(rig.Gatt.IsStickGattAvailableAsync())).Should().BeTrue();
    }

    [Fact]
    public async Task StickAvailable_V10StickWithoutGatt_IsFalse()
    {
        using var rig = new GattServiceRig(maxConns: 4);
        rig.Stick.GattEnabled = false;

        (await rig.Harness.RunAsync(rig.Gatt.IsStickGattAvailableAsync())).Should().BeFalse();
    }

    [Fact]
    public async Task StickAvailable_StickWithZeroConns_IsFalse()
    {
        using var rig = new GattServiceRig(maxConns: 0);

        (await rig.Harness.RunAsync(rig.Gatt.IsStickGattAvailableAsync())).Should().BeFalse();
    }

    [Fact]
    public async Task StickAvailable_NoStick_IsFalse()
    {
        using var rig = new GattServiceRig(plugged: false);

        (await rig.Harness.RunAsync(rig.Gatt.IsStickGattAvailableAsync())).Should().BeFalse();
    }

    [Theory]
    [InlineData(UsbGattMode.Auto, true, GattPath.Stick)]
    [InlineData(UsbGattMode.Auto, false, GattPath.Stick)]
    [InlineData(UsbGattMode.WindowsOnly, true, GattPath.Windows)]
    [InlineData(UsbGattMode.WindowsOnly, false, GattPath.None)]
    [InlineData(UsbGattMode.StickOnly, true, GattPath.Stick)]
    [InlineData(UsbGattMode.StickOnly, false, GattPath.Stick)]
    public async Task DevicePath_WithStick(UsbGattMode mode, bool radioOn, GattPath expected)
    {
        using var rig = new GattServiceRig();
        rig.Gatt.GattMode = mode;

        (await rig.Harness.RunAsync(rig.Gatt.ChooseDevicePathAsync(radioOn))).Should().Be(expected);
    }

    [Theory]
    [InlineData(UsbGattMode.Auto, true, GattPath.Windows)]
    [InlineData(UsbGattMode.Auto, false, GattPath.None)]
    [InlineData(UsbGattMode.StickOnly, true, GattPath.None)]
    [InlineData(UsbGattMode.StickOnly, false, GattPath.None)]
    [InlineData(UsbGattMode.WindowsOnly, true, GattPath.Windows)]
    public async Task DevicePath_WithoutStick(UsbGattMode mode, bool radioOn, GattPath expected)
    {
        using var rig = new GattServiceRig(plugged: false);
        rig.Gatt.GattMode = mode;

        (await rig.Harness.RunAsync(rig.Gatt.ChooseDevicePathAsync(radioOn))).Should().Be(expected);
    }

    [Fact]
    public async Task DevicePath_AllSlotsUsed_OverflowsToWindowsInAutoOnly()
    {
        using var rig = new GattServiceRig(maxConns: 2);
        await rig.Harness.ConnectStickAsync();
        rig.Harness.Connection.TryAcquireLink();
        rig.Harness.Connection.TryAcquireLink();

        (await rig.Harness.RunAsync(rig.Gatt.ChooseDevicePathAsync(radioOn: true))).Should().Be(GattPath.Windows);
        (await rig.Harness.RunAsync(rig.Gatt.ChooseDevicePathAsync(radioOn: false))).Should().Be(GattPath.None);
        rig.Gatt.GattMode = UsbGattMode.StickOnly;
        (await rig.Harness.RunAsync(rig.Gatt.ChooseDevicePathAsync(radioOn: true))).Should().Be(GattPath.None);
    }

    [Fact]
    public async Task ScanPath_IgnoresFreeSlots()
    {
        using var rig = new GattServiceRig(maxConns: 1);
        await rig.Harness.ConnectStickAsync();
        rig.Harness.Connection.TryAcquireLink();

        (await rig.Harness.RunAsync(rig.Gatt.ChooseScanPathAsync(radioOn: true))).Should().Be(GattPath.Stick);
    }

    [Fact]
    public async Task WindowsOnly_NeverTouchesTheStick()
    {
        using var rig = new GattServiceRig(plugged: false);
        rig.Gatt.GattMode = UsbGattMode.WindowsOnly;
        rig.Harness.Plug("COM7", maxConns: 4);

        await rig.Harness.RunAsync(rig.Gatt.ChooseDevicePathAsync(true));
        await rig.Harness.RunAsync(rig.Gatt.ChooseScanPathAsync(true));

        rig.Harness.Locator.Created.Should().BeEmpty("the stick port is not even probed");
    }

    // ------------------------------------------------------------------ Bluetooth usable

    [Theory]
    [InlineData(UsbGattMode.Auto, UsbAdvertiserMode.Auto, true, true, true)]     // radio on -> usable, no probe needed
    [InlineData(UsbGattMode.Auto, UsbAdvertiserMode.Auto, false, true, true)]    // radio off, GATT stick -> usable
    [InlineData(UsbGattMode.Auto, UsbAdvertiserMode.Off, false, true, true)]     // radio off, advertiser off, GATT stick -> usable
    [InlineData(UsbGattMode.Auto, UsbAdvertiserMode.Auto, false, false, false)]  // radio off, nothing -> prompt
    [InlineData(UsbGattMode.WindowsOnly, UsbAdvertiserMode.Off, false, true, false)]
    [InlineData(UsbGattMode.WindowsOnly, UsbAdvertiserMode.Auto, false, true, true)] // stick advertises
    [InlineData(UsbGattMode.StickOnly, UsbAdvertiserMode.Off, true, true, true)]
    [InlineData(UsbGattMode.StickOnly, UsbAdvertiserMode.Off, true, false, false)]   // radio on but only the stick is wanted
    [InlineData(UsbGattMode.StickOnly, UsbAdvertiserMode.Off, false, true, true)]
    public async Task IsBluetoothUsable_CombinesRadioStickAndSettings(UsbGattMode gatt, UsbAdvertiserMode adv, bool radioOn, bool stickPlugged, bool expected)
    {
        using var rig = new GattServiceRig(plugged: stickPlugged);
        rig.Gatt.GattMode = gatt;
        rig.Advertiser.Mode = adv;

        (await rig.Harness.RunAsync(rig.Gatt.IsBluetoothUsableAsync(radioOn))).Should().Be(expected);
    }

    [Fact]
    public async Task IsBluetoothUsable_RadioOnAndNotStickOnly_DoesNotProbeTheStick()
    {
        using var rig = new GattServiceRig();

        (await rig.Harness.RunAsync(rig.Gatt.IsBluetoothUsableAsync(true))).Should().BeTrue();

        rig.Harness.Locator.Created.Should().BeEmpty();
    }

    [Fact]
    public async Task IsBluetoothUsable_V10StickWithAutoGatt_OnlyAdvertisingCounts()
    {
        using var rig = new GattServiceRig(maxConns: 4);
        rig.Stick.GattEnabled = false;
        rig.Advertiser.Mode = UsbAdvertiserMode.Off;

        (await rig.Harness.RunAsync(rig.Gatt.IsBluetoothUsableAsync(false))).Should().BeFalse("a 1.0 stick cannot serve GATT and advertising is off");
    }

    [Fact]
    public async Task GattStatus_ReportsLinksAndScan()
    {
        using var rig = new GattServiceRig(maxConns: 6);
        rig.Gatt.GattStatus.Supported.Should().BeFalse("not connected yet");
        await rig.Harness.ConnectStickAsync();
        rig.Harness.Connection.TryAcquireLink();
        rig.Harness.Connection.TryAcquireLink();

        rig.Gatt.GattStatus.Should().Be(new UsbGattStatus(true, 6, 2, false));
    }

    [Fact]
    public async Task TryCreateDevice_ReturnsNullWhenNoSlotIsFree()
    {
        using var rig = new GattServiceRig(maxConns: 1);
        await rig.Harness.ConnectStickAsync();

        rig.Gatt.TryCreateDevice("AA:BB:CC:00:00:01").Should().NotBeNull();
        rig.Gatt.TryCreateDevice("AA:BB:CC:00:00:02").Should().BeNull();
    }
}
