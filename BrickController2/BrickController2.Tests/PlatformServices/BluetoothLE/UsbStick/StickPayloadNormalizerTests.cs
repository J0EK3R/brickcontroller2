using System;
using System.Linq;
using BrickController2.DeviceManagement.CaDA;
using BrickController2.DeviceManagement.JieStar;
using BrickController2.DeviceManagement.MouldKing;
using BrickController2.DeviceManagement.PowerBox;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using BrickController2.Protocols;
using FluentAssertions;
using Xunit;
using AndroidCaDA = BrickController2.Droid.PlatformServices.DeviceManagement.CaDA.CaDAPlatformService;
using AndroidJieStar = BrickController2.Droid.PlatformServices.DeviceManagement.JieStar.JieStarPlatformService;
using AndroidMK = BrickController2.Droid.PlatformServices.DeviceManagement.MouldKing.MKPlatformService;
using AndroidPowerBox = BrickController2.Droid.PlatformServices.DeviceManagement.PowerBox.PowerBoxPlatformService;
using WinCaDA = BrickController2.Windows.PlatformServices.DeviceManagement.CaDA.CaDAPlatformService;
using WinJieStar = BrickController2.Windows.PlatformServices.DeviceManagement.JieStar.JieStarPlatformService;
using WinMK = BrickController2.Windows.PlatformServices.DeviceManagement.MouldKing.MKPlatformService;
using WinPowerBox = BrickController2.Windows.PlatformServices.DeviceManagement.PowerBox.PowerBoxPlatformService;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Verifies with the REAL Android and WinUI platform services (compiled into this test project) that the
/// stick payload derived from the WinUI layout equals the Android layout for every vendor, and documents
/// the raw sizes of both platforms (including the PowerBox 26 vs 18 byte difference).
/// </summary>
public class StickPayloadNormalizerTests
{
    private static byte[] Telegram(int length) => Enumerable.Range(0, length).Select(i => (byte)(0x40 + i * 7)).ToArray();

    [Theory]
    [InlineData(8)]
    [InlineData(10)]
    public void MouldKing_WindowsLayoutNormalisesToAndroidLayout(int telegramLength)
    {
        new AndroidMK().TryGetRfPayload(Telegram(telegramLength), out var android);
        new WinMK().TryGetRfPayload(Telegram(telegramLength), out var windows);

        android.Should().HaveCount(24);
        windows.Should().HaveCount(27);
        Normalize(MKProtocol.ManufacturerID, windows).Should().Equal(android);
    }

    [Fact]
    public void JieStar_WindowsLayoutNormalisesToAndroidLayout()
    {
        new AndroidJieStar().TryGetRfPayload(0x12, Telegram(8), out var android);
        new WinJieStar().TryGetRfPayload(0x12, Telegram(8), out var windows);

        android.Should().HaveCount(24);
        windows.Should().HaveCount(27);
        Normalize(JieStarProtocol.ManufacturerID, windows).Should().Equal(android);
    }

    [Fact]
    public void CaDAV1_WindowsLayoutNormalisesToAndroidLayout()
    {
        new AndroidCaDA().TryGetRfPayload(Telegram(16), out var android);
        new WinCaDA().TryGetRfPayload(Telegram(16), out var windows);

        android.Should().HaveCount(24, "3 header + 3 seed + 16 telegram + 2 checksum, no padding");
        windows.Should().HaveCount(27);
        Normalize(CaDAProtocol.ManufacturerID, windows).Should().Equal(android);
    }

    [Fact]
    public void PowerBox_WindowsPayloadHas26BytesAndroid18_StickSendsTheAndroidLayout()
    {
        new AndroidPowerBox().TryGetRfPayload(Telegram(8), out var android);
        new WinPowerBox().TryGetRfPayload(Telegram(8), out var windows);

        android.Should().HaveCount(18, "3 header + 5 seed + 8 telegram + 2 checksum");
        windows.Should().HaveCount(26, "WinUI pads 23 + 3 offset bytes: 18 real bytes plus 5 filler bytes");

        // the first 18 bytes after the offset are the real data, identical to Android
        windows.Skip(3).Take(18).Should().Equal(android);
        Normalize(PowerBoxProtocol.ManufacturerID, windows).Should().Equal(android);
    }

    [Fact]
    public void CaDAV2_UnencryptedSixteenBytes_PassThroughUnchanged()
    {
        byte[] v2 = Telegram(16);

        Normalize(CaDAProtocol.ManufacturerID, v2).Should().Equal(v2);
    }

    [Fact]
    public void AndroidLayouts_PassThroughUnchanged()
    {
        new AndroidMK().TryGetRfPayload(Telegram(10), out var mk);

        Normalize(MKProtocol.ManufacturerID, mk).Should().Equal(mk);
    }

    [Fact]
    public void TooLongPayload_IsRejected()
    {
        StickPayloadNormalizer.TryNormalize(MKProtocol.ManufacturerID, new byte[40], out var data).Should().BeFalse();
        data.Should().BeNull();
    }

    private static byte[] Normalize(ushort manufacturerId, byte[] raw)
    {
        StickPayloadNormalizer.TryNormalize(manufacturerId, raw, out var data).Should().BeTrue();
        return data!;
    }
}
