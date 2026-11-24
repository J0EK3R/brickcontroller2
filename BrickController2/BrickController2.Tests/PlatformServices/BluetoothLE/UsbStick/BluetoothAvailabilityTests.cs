// Tests for the "is Bluetooth usable" decision: radio x stick x setting, all combinations.
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using FluentAssertions;
using Xunit;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

public class BluetoothAvailabilityTests
{
    /// <summary>The prompt is only needed when the radio is off and the stick is not usable (Auto + present).</summary>
    [Theory]
    [InlineData(true, true, UsbAdvertiserMode.Auto, true)]
    [InlineData(true, false, UsbAdvertiserMode.Auto, true)]
    [InlineData(false, true, UsbAdvertiserMode.Auto, true)]
    [InlineData(false, false, UsbAdvertiserMode.Auto, false)]
    [InlineData(true, true, UsbAdvertiserMode.Off, true)]
    [InlineData(true, false, UsbAdvertiserMode.Off, true)]
    [InlineData(false, true, UsbAdvertiserMode.Off, false)]
    [InlineData(false, false, UsbAdvertiserMode.Off, false)]
    public void IsBluetoothUsable_CoversAllCombinations(bool radioOn, bool stick, UsbAdvertiserMode mode, bool expected)
        => BluetoothAvailability.IsBluetoothUsable(radioOn, stick, mode).Should().Be(expected);

    /// <summary>The 4 argument overload with WindowsOnly is exactly the original behaviour.</summary>
    [Theory]
    [InlineData(true, true, UsbAdvertiserMode.Auto)]
    [InlineData(true, false, UsbAdvertiserMode.Auto)]
    [InlineData(false, true, UsbAdvertiserMode.Auto)]
    [InlineData(false, false, UsbAdvertiserMode.Auto)]
    [InlineData(true, true, UsbAdvertiserMode.Off)]
    [InlineData(false, true, UsbAdvertiserMode.Off)]
    [InlineData(false, false, UsbAdvertiserMode.Off)]
    public void IsBluetoothUsable_WindowsOnlyGatt_IsTheOldBehaviour(bool radioOn, bool stick, UsbAdvertiserMode mode)
        => BluetoothAvailability.IsBluetoothUsable(radioOn, stick, mode, UsbGattMode.WindowsOnly)
            .Should().Be(BluetoothAvailability.IsBluetoothUsable(radioOn, stick, mode));

    // radio, stick, advertiser, gatt, expected - the full truth table of the extended formula
    [Theory]
    // StickOnly: only the stick counts, the radio is irrelevant
    [InlineData(true, true, UsbAdvertiserMode.Auto, UsbGattMode.StickOnly, true)]
    [InlineData(false, true, UsbAdvertiserMode.Auto, UsbGattMode.StickOnly, true)]
    [InlineData(true, true, UsbAdvertiserMode.Off, UsbGattMode.StickOnly, true)]
    [InlineData(false, true, UsbAdvertiserMode.Off, UsbGattMode.StickOnly, true)]
    [InlineData(true, false, UsbAdvertiserMode.Auto, UsbGattMode.StickOnly, false)]
    [InlineData(false, false, UsbAdvertiserMode.Auto, UsbGattMode.StickOnly, false)]
    [InlineData(true, false, UsbAdvertiserMode.Off, UsbGattMode.StickOnly, false)]
    [InlineData(false, false, UsbAdvertiserMode.Off, UsbGattMode.StickOnly, false)]
    // Auto GATT: the stick suffices with the radio off, even if the advertiser is Off
    [InlineData(false, true, UsbAdvertiserMode.Off, UsbGattMode.Auto, true)]
    [InlineData(false, true, UsbAdvertiserMode.Auto, UsbGattMode.Auto, true)]
    [InlineData(false, false, UsbAdvertiserMode.Auto, UsbGattMode.Auto, false)]
    [InlineData(true, false, UsbAdvertiserMode.Off, UsbGattMode.Auto, true)]
    [InlineData(true, true, UsbAdvertiserMode.Off, UsbGattMode.Auto, true)]
    [InlineData(false, false, UsbAdvertiserMode.Off, UsbGattMode.Auto, false)]
    // WindowsOnly GATT with the advertiser Auto: stick only for advertising, which keeps the prompt away
    [InlineData(false, true, UsbAdvertiserMode.Auto, UsbGattMode.WindowsOnly, true)]
    [InlineData(false, true, UsbAdvertiserMode.Off, UsbGattMode.WindowsOnly, false)]
    public void IsBluetoothUsable_WithGattMode(bool radioOn, bool stick, UsbAdvertiserMode adv, UsbGattMode gatt, bool expected)
        => BluetoothAvailability.IsBluetoothUsable(radioOn, stick, adv, gatt).Should().Be(expected);
}
