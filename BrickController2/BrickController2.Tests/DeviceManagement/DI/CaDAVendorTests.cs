using Autofac;
using BrickController2.DeviceManagement;
using BrickController2.DeviceManagement.DI;
using BrickController2.DeviceManagement.CaDA;
using BrickController2.PlatformServices.BluetoothLE;
using BrickController2.UI.Services.AppIdentifier;
using FluentAssertions;
using Moq;
using System;
using Xunit;
using CaDAVendor = BrickController2.DeviceManagement.CaDA.CaDA;

namespace BrickController2.Tests.DeviceManagement.DI;

public class CaDAVendorTests
{
    private DeviceFactory _deviceFactory;

    public CaDAVendorTests()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterInstance(Mock.Of<IDeviceRepository>());
        builder.RegisterInstance(Mock.Of<IBluetoothLEService>());
        builder.RegisterInstance(Mock.Of<ICaDAPlatformService>());

        Mock<IAppIdentifierService> appIdentifierService = new();
        appIdentifierService.Setup(x => x.GetAppId(3)).Returns(new byte[] { 0x01, 0x02, 0x03 }); // CaDA needs an AppId of 3 Bytes

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

        // additional dependencies
        builder.RegisterInstance(Random.Shared);

        // execute registration of vendor MouldKing
        builder.RegisterAssemblyModules<CaDAVendor>(typeof(DeviceManagementModule).Assembly);

        // Act
        var container = builder.Build();

        _deviceFactory = container.Resolve<DeviceFactory>();
    }

    [Theory]
    [InlineData("Device", new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 })] // devicedata.Length of 18
    [InlineData("Device_Rev2", new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 })]    // devicedata.Length of 16
    public void RegisterDevice_CaDARaceCar_ReturnedDevice(string address, byte[] deviceData)
    {
        DeviceType deviceType = DeviceType.CaDA_RaceCar;
        string name = "TestDevice";

        var device = _deviceFactory(deviceType, name, address, deviceData, []);

        device.Should().NotBeNull();
        device.Should().BeOfType<CaDARaceCar>();
    }

    [Theory]
    [InlineData("IllegalDevice", new byte[] { 1, 2, 3, 4 })] // MessageEncoderFactory.Create needs devicedata.Length of 16 or 18
    public void RegisterDevice_CaDARaceCar_IllegalDeviceDataSize(string address, byte[] deviceData)
    {
        DeviceType deviceType = DeviceType.CaDA_RaceCar;
        string name = "TestDevice";

        Action act = () => _deviceFactory(deviceType, name, address, deviceData, []);

        act.Should().Throw<Autofac.Core.DependencyResolutionException>()
            .WithInnerException<Autofac.Core.DependencyResolutionException>()
            .WithInnerException<InvalidOperationException>()
            .Which.Message.Should().Be("Unsupported device data format.");
    }
}
