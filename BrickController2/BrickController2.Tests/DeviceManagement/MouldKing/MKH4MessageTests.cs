using BrickController2.DeviceManagement.MouldKing;
using FluentAssertions;
using Xunit;

namespace BrickController2.Tests.DeviceManagement.MouldKing;

/// <summary>
/// Tests of the pure message builders and parsers of the MKH4.0 module (no hardware needed).
/// </summary>
public class MKH4MessageTests
{
    [Theory]
    [InlineData('2', "", "T002W")]
    [InlineData('C', "", "T00CW")]
    [InlineData('1', "AABB", "T041AABBW")]
    public void BuildMessage_ReturnsFramedMessage(char command, string payload, string expected)
    {
        MKH4.BuildMessage(command, payload).Should().Be(expected);
    }

    [Fact]
    public void BuildDisconnectMessage_IsT002W()
    {
        MKH4.BuildDisconnectMessage().Should().Be("T002W");
    }

    [Theory]
    [InlineData(0, "00000")]
    [InlineData(0x7FFF, "07FFF")]
    [InlineData(-0x7FFF, "0FFFF")]
    [InlineData(1, "00001")]
    [InlineData(-1, "08001")]
    public void FormatSpeed_SetsDirectionBit(int value, string expected)
    {
        MKH4.FormatSpeed(value, '0').Should().Be(expected);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    public void BuildSpeedPayload_HasFiveDigitsPerChannel(int channelCount)
    {
        var payload = MKH4.BuildSpeedPayload(new int[channelCount]);

        payload.Should().HaveLength(5 * channelCount);
        payload.Should().Be(new string('0', 5 * channelCount));
    }

    [Fact]
    public void BuildSpeedPayload_MixedValues()
    {
        MKH4.BuildSpeedPayload([0x7FFF, 0, -0x7FFF, 1]).Should().Be("07FFF" + "00000" + "0FFFF" + "00001");
    }

    [Fact]
    public void BuildPositionPayload_NoTarget_ReturnsNull()
    {
        MKH4.BuildPositionPayload(new int?[4], 0.5f).Should().BeNull();
    }

    [Fact]
    public void BuildPositionPayload_IdleChannelsAndTargets()
    {
        // 0.625 -> 0x4FFF; positive target sets bit 15 (0xCFFF), negative target does not (0x4FFF)
        var payload = MKH4.BuildPositionPayload([100, null, -256, null], 0.625f);

        payload.Should().Be("CFFF" + "00000064" + "8000" + "00000000" + "4FFF" + "00000100" + "8000" + "00000000");
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    public void BuildPositionPayload_HasTwelveDigitsPerChannel(int channelCount)
    {
        var targets = new int?[channelCount];
        targets[0] = 1;

        MKH4.BuildPositionPayload(targets, 1f)!.Should().HaveLength(12 * channelCount);
    }

    [Theory]
    [InlineData("02", 4, 0x02)]
    [InlineData("0F", 4, 0x0F)]
    [InlineData("00", 4, 0x00)]
    [InlineData("FF", 4, 0x0F)] // bits above the channel count are ignored
    [InlineData("FF", 6, 0x3F)]
    public void TryParseMotorMask_ValidPayload(string payload, int channelCount, int expected)
    {
        MKH4.TryParseMotorMask(payload, channelCount, out var mask).Should().BeTrue();
        mask.Should().Be(expected);
    }

    [Theory]
    [InlineData("ZZ")]
    [InlineData("2")]
    [InlineData("")]
    [InlineData("002")]
    public void TryParseMotorMask_InvalidPayload_ReturnsFalse(string payload)
    {
        MKH4.TryParseMotorMask(payload, 4, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("410012", 12)]
    [InlineData("410006", 6)]
    [InlineData("410000", 0)]
    public void TryParseEdition_ValidPayload(string payload, int expected)
    {
        MKH4.TryParseEdition(payload, out var edition).Should().BeTrue();
        edition.Should().Be(expected);
    }

    [Theory]
    [InlineData("41000A")]
    [InlineData("4100")]
    [InlineData("")]
    [InlineData("41001G")]
    [InlineData("4100123")]
    public void TryParseEdition_InvalidPayload_ReturnsFalse(string payload)
    {
        MKH4.TryParseEdition(payload, out _).Should().BeFalse();
    }
}
