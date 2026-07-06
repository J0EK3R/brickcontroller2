using Autofac;
using BrickController2.DeviceManagement;
using BrickController2.DeviceManagement.DI;
using BrickController2.DeviceManagement.MouldKing;
using BrickController2.PlatformServices.BluetoothLE;
using BrickController2.UI.Services.AppIdentifier;
using FluentAssertions;
using Moq;
using System;
using Xunit;
using MouldKingVendor = BrickController2.DeviceManagement.MouldKing.MouldKing;

namespace BrickController2.Tests.DeviceManagement.DI;

public class MouldKingVendorTests
{
    private DeviceFactory _deviceFactory;

    public MouldKingVendorTests()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterInstance(Mock.Of<IDeviceRepository>());
        builder.RegisterInstance(Mock.Of<IBluetoothLEService>());
        builder.RegisterInstance(Mock.Of<IMKPlatformService>());

        Mock<IAppIdentifierService> appIdentifierService = new();
        appIdentifierService.Setup(x => x.GetAppId(2)).Returns(new byte[] { 0x01, 0x02 });

        builder.RegisterInstance(appIdentifierService.Object);

        builder.Register<DeviceFactory>(c =>
        {
            IComponentContext ctx = c.Resolve<IComponentContext>();
            return (deviceType, name, address, deviceData, settings) => ctx.ResolveOptionalKeyed<Device>(deviceType,
                new NamedParameter("name", name),
                new NamedParameter("address", address),
                new NamedParameter("deviceData", deviceData),
                new NamedParameter("settings", settings));
        });

        // execute registration of vendor MouldKing
        builder.RegisterAssemblyModules<MouldKingVendor>(typeof(DeviceManagementModule).Assembly);

        // Act
        var container = builder.Build();

        _deviceFactory = container.Resolve<DeviceFactory>();
    }

    [Theory]
    [InlineData("Device")]        // The address is not relevant for this device
    [InlineData("IllegalDevice")] // The address is not relevant for this device
    public void RegisterDevice_MK3_8_ReturnedDevice(string address)
    {
        DeviceType deviceType = DeviceType.MK3_8;
        string name = "TestDevice";
        byte[] deviceData = [1, 2, 3];

        var device = _deviceFactory(deviceType, name, address, deviceData, []);

        device.Should().NotBeNull();
        device.Should().BeOfType<MK3_8>();
    }

    [Theory]
    [InlineData("Device")]
    [InlineData("IllegalDevice")]
    public void RegisterDevice_MK4_IllegalDevice(string address)
    {
        DeviceType deviceType = DeviceType.MK4;
        string name = "TestDevice";
        byte[] deviceData = [1, 2, 3];

        Action act = () => _deviceFactory(deviceType, name, address, deviceData, []);

        act.Should().Throw<Autofac.Core.DependencyResolutionException>()
            .WithInnerException<Autofac.Core.DependencyResolutionException>()
            .WithInnerException<ArgumentException>()
            .Which.ParamName.Should().Be(nameof(address));
    }

    [Theory]
    [InlineData("Device1")]
    [InlineData("Device2")]
    [InlineData("Device3")]
    public void RegisterDevice_MK4_ReturnedDevice(string address)
    {
        DeviceType deviceType = DeviceType.MK4;
        string name = "TestDevice";
        byte[] deviceData = [1, 2, 3];

        var device = _deviceFactory(deviceType, name, address, deviceData, []);

        device.Should().NotBeNull();
        device.Should().BeOfType<MK4>();
    }

    [Theory]
    [InlineData("Device")]        // The address is not relevant for this device
    [InlineData("IllegalDevice")] // The address is not relevant for this device
    public void RegisterDevice_MK5_ReturnedDevice(string address)
    {
        DeviceType deviceType = DeviceType.MK5;
        string name = "TestDevice";
        byte[] deviceData = [1, 2, 3];

        var device = _deviceFactory(deviceType, name, address, deviceData, []);

        device.Should().NotBeNull();
        device.Should().BeOfType<MK5>();
    }

    [Theory]
    [InlineData("Device1")]
    [InlineData("Device2")]
    [InlineData("Device3")]
    public void RegisterDevice_MK6_ReturnedDevice(string address)
    {
        DeviceType deviceType = DeviceType.MK6;
        string name = "TestDevice";
        byte[] deviceData = [1, 2, 3];

        var device = _deviceFactory(deviceType, name, address, deviceData, []);

        device.Should().NotBeNull();
        device.Should().BeOfType<MK6>();
    }

    [Theory]
    [InlineData("Device")]
    [InlineData("IllegalDevice")]
    public void RegisterDevice_MK6_IllegalDevice(string address)
    {
        DeviceType deviceType = DeviceType.MK6;
        string name = "TestDevice";
        byte[] deviceData = [1, 2, 3];

        Action act = () => _deviceFactory(deviceType, name, address, deviceData, []);

        act.Should().Throw<Autofac.Core.DependencyResolutionException>()
            .WithInnerException<Autofac.Core.DependencyResolutionException>()
            .WithInnerException<ArgumentException>()
            .Which.ParamName.Should().Be(nameof(address));
    }
}
