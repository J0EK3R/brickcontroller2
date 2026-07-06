using Autofac;
using BrickController2.DeviceManagement;
using BrickController2.DeviceManagement.DI;
using BrickController2.DeviceManagement.JieStar;
using BrickController2.PlatformServices.BluetoothLE;
using BrickController2.UI.Services.AppIdentifier;
using FluentAssertions;
using Moq;
using System;
using Xunit;
using JieStarVendor = BrickController2.DeviceManagement.JieStar.JieStar;

namespace BrickController2.Tests.DeviceManagement.DI;

public class JieStarVendorTests
{
    private DeviceFactory _deviceFactory;

    public JieStarVendorTests()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterInstance(Mock.Of<IDeviceRepository>());
        builder.RegisterInstance(Mock.Of<IBluetoothLEService>());
        builder.RegisterInstance(Mock.Of<IJieStarPlatformService>());

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

        // execute registration of vendor JIESTAR
        builder.RegisterAssemblyModules<JieStarVendor>(typeof(DeviceManagementModule).Assembly);

        // Act
        var container = builder.Build();
        _deviceFactory = container.Resolve<DeviceFactory>();
    }

    [Theory]
    [InlineData("Device1")]
    [InlineData("Device2")]
    [InlineData("Device3")]
    public void RegisterDevice_JieStar_SCM4_ReturnedDevice(string address)
    {
        DeviceType deviceType = DeviceType.JieStarSCM4;
        string name = "TestDevice";
        byte[] deviceData = [1, 2, 3];

        var device = _deviceFactory(deviceType, name, address, deviceData, []);

        device.Should().NotBeNull();
        device.Should().BeOfType<JieStarSCM4>();
    }

    [Theory]
    [InlineData("Device")]
    [InlineData("IllegalDevice")]
    public void RegisterDevice_JieStar_SCM4_IllegalDevice(string address)
    {
        DeviceType deviceType = DeviceType.JieStarSCM4;
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
    public void RegisterDevice_JieStar_SCM8_ReturnedDevice(string address)
    {
        DeviceType deviceType = DeviceType.JieStarSCM8;
        string name = "TestDevice";
        byte[] deviceData = [1, 2, 3];

        var device = _deviceFactory(deviceType, name, address, deviceData, []);

        device.Should().NotBeNull();
        device.Should().BeOfType<JieStarSCM8>();
    }

    [Theory]
    [InlineData("Device")]
    [InlineData("IllegalDevice")]
    public void RegisterDevice_JieStar_SCM8_IllegalDevice(string address)
    {
        DeviceType deviceType = DeviceType.JieStarSCM8;
        string name = "TestDevice";
        byte[] deviceData = [1, 2, 3];

        Action act = () => _deviceFactory(deviceType, name, address, deviceData, []);

        act.Should().Throw<Autofac.Core.DependencyResolutionException>()
            .WithInnerException<Autofac.Core.DependencyResolutionException>()
            .WithInnerException<ArgumentException>()
            .Which.ParamName.Should().Be(nameof(address));
    }
}
