// The complete policy table of the GATT path selector: mode x radio x stick available x free slot.

using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using FluentAssertions;
using Xunit;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

public class GattPathSelectorTests
{
    // mode, radioOn, stickAvailable, stickHasFreeSlot, expected path. Written out explicitly (not derived from the implementation).
    [Theory]
    // Auto: the stick has priority whenever it is available, with Windows Bluetooth on or off
    [InlineData(UsbGattMode.Auto, true, true, true, GattPath.Stick)]
    [InlineData(UsbGattMode.Auto, false, true, true, GattPath.Stick)]
    // Auto: no free slot -> overflow to Windows if the radio is on, otherwise nothing
    [InlineData(UsbGattMode.Auto, true, true, false, GattPath.Windows)]
    [InlineData(UsbGattMode.Auto, false, true, false, GattPath.None)]
    // Auto: no stick -> Windows if the radio is on
    [InlineData(UsbGattMode.Auto, true, false, true, GattPath.Windows)]
    [InlineData(UsbGattMode.Auto, true, false, false, GattPath.Windows)]
    [InlineData(UsbGattMode.Auto, false, false, true, GattPath.None)]
    [InlineData(UsbGattMode.Auto, false, false, false, GattPath.None)]
    // WindowsOnly: never the stick
    [InlineData(UsbGattMode.WindowsOnly, true, true, true, GattPath.Windows)]
    [InlineData(UsbGattMode.WindowsOnly, true, true, false, GattPath.Windows)]
    [InlineData(UsbGattMode.WindowsOnly, true, false, true, GattPath.Windows)]
    [InlineData(UsbGattMode.WindowsOnly, true, false, false, GattPath.Windows)]
    [InlineData(UsbGattMode.WindowsOnly, false, true, true, GattPath.None)]
    [InlineData(UsbGattMode.WindowsOnly, false, true, false, GattPath.None)]
    [InlineData(UsbGattMode.WindowsOnly, false, false, true, GattPath.None)]
    [InlineData(UsbGattMode.WindowsOnly, false, false, false, GattPath.None)]
    // StickOnly: never Windows, no overflow
    [InlineData(UsbGattMode.StickOnly, true, true, true, GattPath.Stick)]
    [InlineData(UsbGattMode.StickOnly, false, true, true, GattPath.Stick)]
    [InlineData(UsbGattMode.StickOnly, true, true, false, GattPath.None)]
    [InlineData(UsbGattMode.StickOnly, false, true, false, GattPath.None)]
    [InlineData(UsbGattMode.StickOnly, true, false, true, GattPath.None)]
    [InlineData(UsbGattMode.StickOnly, true, false, false, GattPath.None)]
    [InlineData(UsbGattMode.StickOnly, false, false, true, GattPath.None)]
    [InlineData(UsbGattMode.StickOnly, false, false, false, GattPath.None)]
    public void Choose_FollowsThePolicyTable(UsbGattMode mode, bool radioOn, bool stickAvailable, bool freeSlot, GattPath expected)
        => GattPathSelector.Choose(radioOn, stickAvailable, freeSlot, mode).Should().Be(expected);

    [Fact]
    public void Choose_UnknownMode_Throws()
        => FluentActions.Invoking(() => GattPathSelector.Choose(true, true, true, (UsbGattMode)99)).Should().Throw<System.ArgumentOutOfRangeException>();
}
