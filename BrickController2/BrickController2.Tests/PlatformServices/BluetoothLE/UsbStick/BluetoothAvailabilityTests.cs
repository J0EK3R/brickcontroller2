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
}
