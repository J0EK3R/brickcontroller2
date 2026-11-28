// Tests of the AD structure parser and the name helpers.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using FluentAssertions;
using Xunit;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

public class StickAdvertisementParserTests
{
    [Fact]
    public void Parse_ReadsAllStructures()
    {
        byte[] data = [2, 0x01, 0x06, 4, 0xFF, 0x33, 0xAC, 0x01, 5, 0x09, (byte)'T', (byte)'e', (byte)'s', (byte)'t'];

        var result = StickAdvertisementParser.Parse(data);

        result.Should().HaveCount(3);
        result[0x01].Should().Equal(0x06);
        result[0xFF].Should().Equal(0x33, 0xAC, 0x01);
        result[0x09].Should().Equal(Encoding.ASCII.GetBytes("Test"));
    }

    [Fact]
    public void Parse_ZeroLengthEndsTheData()
    {
        StickAdvertisementParser.Parse([2, 0x01, 0x06, 0, 3, 0xFF, 1, 2]).Should().HaveCount(1);
    }

    [Fact]
    public void Parse_TruncatedStructureIsDropped_ButEarlierOnesKept()
    {
        var result = StickAdvertisementParser.Parse([2, 0x01, 0x06, 9, 0xFF, 1, 2]);

        result.Should().ContainKey(0x01).And.NotContainKey(0xFF);
    }

    [Fact]
    public void Parse_EmptyOrGarbage_DoesNotThrow()
    {
        StickAdvertisementParser.Parse([]).Should().BeEmpty();
        StickAdvertisementParser.Parse([0xFF]).Should().BeEmpty();
        StickAdvertisementParser.Parse([1]).Should().BeEmpty();
    }

    [Fact]
    public void Parse_DuplicateType_LastWins()
    {
        StickAdvertisementParser.Parse([2, 0xFF, 1, 2, 0xFF, 2])[0xFF].Should().Equal(2);
    }

    [Fact]
    public void GetLocalName_PrefersCompleteOverShortened()
    {
        var structures = new Dictionary<byte, byte[]> { [0x08] = "Sh"u8.ToArray(), [0x09] = "Complete"u8.ToArray() };

        StickAdvertisementParser.GetLocalName(structures).Should().Be("Complete");
    }

    [Fact]
    public void GetLocalName_ShortenedIsUsedWhenAlone()
    {
        StickAdvertisementParser.GetLocalName(new Dictionary<byte, byte[]> { [0x08] = "Sh"u8.ToArray() }).Should().Be("Sh");
    }

    [Fact]
    public void GetLocalName_EmptyOrMissingIsNull()
    {
        StickAdvertisementParser.GetLocalName(new Dictionary<byte, byte[]>()).Should().BeNull();
        StickAdvertisementParser.GetLocalName(new Dictionary<byte, byte[]> { [0x09] = [] }).Should().BeNull();
    }

    [Fact]
    public void ReportedTypes_AreTheFourOfTheWindowsScanner()
    {
        StickAdvertisementParser.ReportedTypes.OrderBy(t => t).Should().Equal(0x06, 0x07, 0x09, 0xFF);
    }
}
