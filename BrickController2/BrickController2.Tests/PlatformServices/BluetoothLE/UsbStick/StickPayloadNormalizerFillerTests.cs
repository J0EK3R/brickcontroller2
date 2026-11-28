using System;
using System.Linq;
using BrickController2.DeviceManagement.CaDA;
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
/// Golden check for the filler recomputation in <see cref="StickPayloadNormalizer"/>: for many deterministic
/// pseudo-random telegrams, the output of the (upstream) WinUI platform services, normalised for the stick,
/// must equal the output of the Android platform services byte for byte.
/// </summary>
public class StickPayloadNormalizerFillerTests
{
    private const int TelegramCount = 5000;
    private const int FixedSeed = 20251124;

    /// <summary>
    /// Creates reproducible pseudo-random telegrams of the given length.
    /// </summary>
    private static byte[][] RandomTelegrams(int length)
    {
        var random = new Random(FixedSeed + length);
        return Enumerable.Range(0, TelegramCount)
            .Select(_ => { var t = new byte[length]; random.NextBytes(t); return t; })
            .ToArray();
    }

    [Theory]
    [InlineData(8)]
    [InlineData(10)]
    public void MouldKing_RandomTelegrams_WindowsNormalisedEqualsAndroid(int telegramLength)
    {
        foreach (var telegram in RandomTelegrams(telegramLength))
        {
            new AndroidMK().TryGetRfPayload(telegram, out var android);
            new WinMK().TryGetRfPayload(telegram, out var windows);

            Normalize(MKProtocol.ManufacturerID, windows).Should().Equal(android, $"telegram {Convert.ToHexString(telegram)}");
        }
    }

    [Fact]
    public void JieStar_RandomTelegrams_WindowsNormalisedEqualsAndroid()
    {
        foreach (var telegram in RandomTelegrams(8))
        {
            new AndroidJieStar().TryGetRfPayload(0x25, telegram, out var android);
            new WinJieStar().TryGetRfPayload(0x25, telegram, out var windows);

            Normalize(JieStarProtocol.ManufacturerID, windows).Should().Equal(android, $"telegram {Convert.ToHexString(telegram)}");
        }
    }

    [Fact]
    public void PowerBox_RandomTelegrams_WindowsNormalisedEqualsAndroid()
    {
        foreach (var telegram in RandomTelegrams(8))
        {
            new AndroidPowerBox().TryGetRfPayload(telegram, out var android);
            new WinPowerBox().TryGetRfPayload(telegram, out var windows);

            Normalize(PowerBoxProtocol.ManufacturerID, windows).Should().Equal(android, $"telegram {Convert.ToHexString(telegram)}");
        }
    }

    [Fact]
    public void CaDAV1_RandomTelegrams_WindowsNormalisedEqualsAndroid()
    {
        foreach (var telegram in RandomTelegrams(16))
        {
            new AndroidCaDA().TryGetRfPayload(telegram, out var android);
            new WinCaDA().TryGetRfPayload(telegram, out var windows);

            Normalize(CaDAProtocol.ManufacturerID, windows).Should().Equal(android, $"telegram {Convert.ToHexString(telegram)}");
        }
    }

    [Fact]
    public void AndroidLayoutInput_IsUnchanged()
    {
        foreach (var telegram in RandomTelegrams(8).Take(200))
        {
            new AndroidMK().TryGetRfPayload(telegram, out var android);

            Normalize(MKProtocol.ManufacturerID, android).Should().Equal(android);
        }
    }

    [Fact]
    public void WindowsLayoutWithoutWindowsFillerPattern_IsOnlyStripped()
    {
        // 27 bytes whose tail does not follow the (position + 4) pattern: only the 3 offset bytes are removed.
        byte[] raw = Enumerable.Range(0, 27).Select(i => (byte)(i * 3 + 1)).ToArray();

        Normalize(MKProtocol.ManufacturerID, raw).Should().Equal(raw.Skip(3));
    }

    [Fact]
    public void NormalizerDoesNotMutateTheCallersArray()
    {
        new WinMK().TryGetRfPayload(new byte[8], out var windows);
        byte[] copy = windows.ToArray();

        Normalize(MKProtocol.ManufacturerID, windows);

        windows.Should().Equal(copy);
    }

    [Fact]
    public void FortyByteInput_IsStillRejected()
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
