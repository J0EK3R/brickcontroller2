using BrickController2.DeviceManagement.MouldKing;
using FluentAssertions;
using System.Linq;
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

    [Fact]
    public void BuildSpeedPayload_HasFiveDigitsPerChannel()
    {
        var payload = MKH4.BuildSpeedPayload(new int[MKH4.CHANNEL_COUNT], new string('0', MKH4.CHANNEL_COUNT));

        payload.Should().HaveLength(5 * MKH4.CHANNEL_COUNT);
        payload.Should().Be(new string('0', 5 * MKH4.CHANNEL_COUNT));
    }

    [Fact]
    public void BuildSpeedPayload_MixedValues()
    {
        MKH4.BuildSpeedPayload([0x7FFF, 0, -0x7FFF, 1], "0000").Should().Be("07FFF" + "00000" + "0FFFF" + "00001");
    }

    [Fact]
    public void BuildSpeedPayload_FlagsAreUsedPerChannel()
    {
        MKH4.BuildSpeedPayload([1, 2, 3, 4], "1001").Should().Be("10001" + "00002" + "00003" + "10004");
    }

    [Fact]
    public void BuildSpeedPayload_AllLimitsDisabled_EqualsPlainZeroFlags()
    {
        // regression: the default flags must give the same bytes as before the settings existed
        var flags = new string(MKH4Config.LimitDisabledFlag, 4);

        MKH4.BuildSpeedPayload([0x7FFF, 0, -0x7FFF, 1], flags)
            .Should().Be(MKH4.BuildSpeedPayload([0x7FFF, 0, -0x7FFF, 1], "0000"));
    }

    [Fact]
    public void BuildStallProtectionMessage_OnAndOff()
    {
        MKH4.BuildStallProtectionMessage(true).Should().Be("T01F1W");
        MKH4.BuildStallProtectionMessage(false).Should().Be("T01F0W");
    }

    [Theory]
    [InlineData(0, "T02b00W")]
    [InlineData(50, "T02b32W")]
    [InlineData(100, "T02b64W")]
    [InlineData(-5, "T02b00W")]
    [InlineData(250, "T02b64W")]
    public void BuildMotorStrengthMessage_ClampsAndFormats(int percent, string expected)
    {
        MKH4.BuildMotorStrengthMessage(percent).Should().Be(expected);
    }

    [Fact]
    public void BuildLimitMessage_ChannelA()
    {
        MKH4.BuildLimitMessage(0, -100980, 100980).Should().Be("T14901+00018A74-00018A74W");
    }

    [Fact]
    public void BuildLimitMessage_ChannelDUsesMask08()
    {
        MKH4.BuildLimitMessage(3, -100980, 100980).Should().StartWith("T14908");
    }

    [Fact]
    public void BuildLimitMessage_SmallValuesBecomeZero()
    {
        MKH4.BuildLimitMessage(0, -999, 999).Should().Be("T14901+00000000+00000000W");
    }

    [Fact]
    public void BuildLimitMessage_SignsAreKept()
    {
        // negative max and positive min are formatted with their own sign, max first
        MKH4.BuildLimitMessage(1, 2000, -3000).Should().Be("T14902-00000BB8+000007D0W");
    }

    [Fact]
    public void BuildReadLimitsMessage_IsT00BW()
    {
        MKH4.BuildReadLimitsMessage().Should().Be("T00BW");
    }

    [Fact]
    public void GetResponseCommand_IsCaseSensitive()
    {
        MKH4.GetResponseCommand('b').Should().Be("7b");
        MKH4.GetResponseCommand('B').Should().Be("7B");
        MKH4.GetResponseCommand('C').Should().Be("7C");
    }

    /// <summary>
    /// Builds the answer of the module: the values of port p are the values 2p and 2p+1.
    /// </summary>
    private static string BuildLimitsPayload(params int[] values)
        => string.Concat(values.Select(v => (v >= 0 ? "+" : "-") + System.Math.Abs(v).ToString("X8")));

    [Fact]
    public void TryParseLimits_ValidPayload_ReturnsMinMaxPerChannel()
    {
        // channel A: 100000 / -100000 (order does not matter), B: 5000 / -6000, C and D: 0
        var payload = BuildLimitsPayload(100000, -100000, -6000, 5000, 0, 0, 0, 0);
        // channel B uses the values 2 and 3
        MKH4.TryParseLimits(payload, out var limits).Should().BeTrue();

        limits.Should().Equal((-100000, 100000), (-6000, 5000), (0, 0), (0, 0));
    }

    [Theory]
    [InlineData("")]
    [InlineData("+00000000")]
    public void TryParseLimits_WrongLength_ReturnsFalse(string payload)
    {
        MKH4.TryParseLimits(payload, out _).Should().BeFalse();
    }

    [Fact]
    public void TryParseLimits_BadSignOrDigits_ReturnsFalse()
    {
        var good = BuildLimitsPayload(1, 2, 3, 4, 5, 6, 7, 8);

        MKH4.TryParseLimits("*" + good.Substring(1), out _).Should().BeFalse();
        MKH4.TryParseLimits(good.Substring(0, 71) + "G", out _).Should().BeFalse();
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

    [Fact]
    public void BuildPositionPayload_HasTwelveDigitsPerChannel()
    {
        var targets = new int?[MKH4.CHANNEL_COUNT];
        targets[0] = 1;

        MKH4.BuildPositionPayload(targets, 1f)!.Should().HaveLength(12 * MKH4.CHANNEL_COUNT);
    }

    [Theory]
    [InlineData("02", 0x02)]
    [InlineData("0F", 0x0F)]
    [InlineData("00", 0x00)]
    [InlineData("FF", 0x0F)] // bits above the channel count are ignored
    public void TryParseMotorMask_ValidPayload(string payload, int expected)
    {
        MKH4.TryParseMotorMask(payload, out var mask).Should().BeTrue();
        mask.Should().Be(expected);
    }

    [Theory]
    [InlineData("ZZ")]
    [InlineData("2")]
    [InlineData("")]
    [InlineData("002")]
    public void TryParseMotorMask_InvalidPayload_ReturnsFalse(string payload)
    {
        MKH4.TryParseMotorMask(payload, out _).Should().BeFalse();
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
