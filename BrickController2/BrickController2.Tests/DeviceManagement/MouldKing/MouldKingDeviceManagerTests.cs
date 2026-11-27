using BrickController2.DeviceManagement;
using BrickController2.DeviceManagement.MouldKing;
using BrickController2.PlatformServices.BluetoothLE;
using BrickController2.UI.Services.AppIdentifier;
using BrickController2.UI.Services.Preferences;
using BrickController2.Protocols;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace BrickController2.Tests.DeviceManagement.MouldKing;

public class MouldKingDeviceManagerTests
{
    private const byte AppIdentifier1 = 0x61; // This is the first byte of an randomly chosen AppIdentifier for UnitTesting
    private const byte AppIdentifier2 = 0x62; // This is the second byte of an randomly chosen AppIdentifier for UnitTesting

    private readonly MouldKingDeviceManager _manager;
    private readonly Mock<IAppIdentifierService> _appIdentifierService = new(MockBehavior.Strict);

    public MouldKingDeviceManagerTests()
    {
        _appIdentifierService.Setup(x => x.GetAppId(2)).Returns(new byte[] { AppIdentifier1, AppIdentifier2 });

        _manager = new MouldKingDeviceManager(_appIdentifierService.Object);
    }

    [Fact]
    public void TryGetDevice_MouldKingManufacturerId_ReturnsMouldKingDiyDevice()
    {
        byte[] manufacturerData = [0x33, 0xac];

        var scanResult = new ScanResult(default, default, new Dictionary<byte, byte[]>()
        {
            { 0xFF, manufacturerData }
        });

        var result = _manager.TryGetDevice(scanResult, out var device);

        result.Should().BeTrue();
        device.Should().BeEquivalentTo(new FoundDevice()
        {
            DeviceAddress = scanResult.DeviceAddress,
            DeviceName = scanResult.DeviceName,
            DeviceType = DeviceType.MK_DIY,
            ManufacturerData = manufacturerData
        });
    }

    [Fact]
    public void TryGetDevice_WrongManufacturerId_ReturnsFalse()
    {
        var scanResult = new ScanResult("WrongManufacturerId", default, new Dictionary<byte, byte[]>()
        {
            { 0xFF, [0x0f, 0xff] }
        });

        var result = _manager.TryGetDevice(scanResult, out var device);

        result.Should().BeFalse();
        device.DeviceType.Should().Be(DeviceType.Unknown);
    }

    /// <summary>
    /// Complete local name AD type
    /// </summary>
    private const byte AdTypeCompleteLocalName = 0x09;

    private static ScanResult CreateNameScanResult(string deviceName, string? advertisedName)
    {
        var advertisment = new Dictionary<byte, byte[]>();
        if (advertisedName is not null)
        {
            advertisment[AdTypeCompleteLocalName] = Encoding.ASCII.GetBytes(advertisedName);
        }

        return new ScanResult(deviceName, "address", advertisment);
    }

    [Theory]
    [InlineData("YX-01-1234")]
    [InlineData("YX-05-ABCD")]
    [InlineData("yx-01-1234")]
    [InlineData("yX-05")]
    public void TryGetDevice_MKH4LocalName_ReturnsMKH4(string name)
    {
        var scanResult = CreateNameScanResult(name, name);

        var result = _manager.TryGetDevice(scanResult, out var device);

        result.Should().BeTrue();
        device.DeviceType.Should().Be(DeviceType.MKH4);
        device.DeviceName.Should().Be(name);
    }

    [Theory]
    [InlineData("YX-02-1234")]
    [InlineData("YX-06-1234")]
    [InlineData("YX-0")]
    [InlineData("Other")]
    [InlineData("")]
    public void TryGetDevice_OtherLocalName_ReturnsFalse(string advertisedName)
    {
        var scanResult = CreateNameScanResult("Device", advertisedName);

        var result = _manager.TryGetDevice(scanResult, out var device);

        result.Should().BeFalse();
        device.DeviceType.Should().Be(DeviceType.Unknown);
    }

    [Fact]
    public void TryGetDevice_MKH4LocalNameWithoutDeviceName_ReturnsFalse()
    {
        var scanResult = CreateNameScanResult(string.Empty, "YX-01-1234");

        _manager.TryGetDevice(scanResult, out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetDevice_MKH4ServiceUuid_ReturnsMKH4()
    {
        var scanResult = new ScanResult("Module", "address", new Dictionary<byte, byte[]>()
        {
            { 0x06, BluetoothLowEnergy.GetGuid(0xaf30).To128BitByteArray() }
        });

        var result = _manager.TryGetDevice(scanResult, out var device);

        result.Should().BeTrue();
        device.DeviceType.Should().Be(DeviceType.MKH4);
    }

    [Fact]
    public void IsMKH4Name_EmptySpan_ReturnsFalse()
    {
        MouldKingDeviceManager.IsMKH4Name(ReadOnlySpan<byte>.Empty).Should().BeFalse();
    }

    [Fact]
    public void AppId_TwoBytesInPreferences_AllBytes()
    {
        var appId = _manager.GetAppId();
        appId.Length.Should().Be(2);
        appId.Span[0].Should().Be(0x61); // 'a' = 0x61
        appId.Span[1].Should().Be(0x62); // 'b' = 0x62
    }
}
