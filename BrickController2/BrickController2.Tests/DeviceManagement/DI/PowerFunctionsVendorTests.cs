using Autofac;
using BrickController2.DeviceManagement;
using BrickController2.DeviceManagement.DI;
using BrickController2.DeviceManagement.PowerFunctions;
using BrickController2.PlatformServices.Infrared;
using BrickController2.UI.Images;
using FluentAssertions;
using Moq;
using Xunit;
using PowerFunctionsVendor = BrickController2.DeviceManagement.PowerFunctions.PowerFunctions;

namespace BrickController2.Tests.DeviceManagement.DI;

public class PowerFunctionsVendorTests
{
    private DeviceFactory _deviceFactory;

    public PowerFunctionsVendorTests()
    {
        // Arrange
        var builder = new ContainerBuilder();
        builder.RegisterInstance(Mock.Of<IDeviceRepository>());
        builder.RegisterInstance(Mock.Of<IInfraredService>());
        builder.RegisterInstance(Mock.Of<IPowerFunctionsManager>());
        builder.RegisterInstance(Mock.Of<IDeviceImageRegistry>()); // needed cause RegisterDevice<PowerFunctionsDevice>().WithImages("powerfunctions_image.png", "powerfunctions_image_small.png")

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
        builder.RegisterAssemblyModules<PowerFunctionsVendor>(typeof(DeviceManagementModule).Assembly);

        // Act
        var container = builder.Build();

        _deviceFactory = container.Resolve<DeviceFactory>();
    }

    [Theory]
    [InlineData("0", "PF Infra 1")]
    [InlineData("1", "PF Infra 2")]
    [InlineData("2", "PF Infra 3")]
    [InlineData("3", "PF Infra 4")]
    public void RegisterDevice_PowerFunctionsDevice_ReturnedDevice(string address, string name)
    {
        DeviceType deviceType = DeviceType.Infrared;

        var device = _deviceFactory(deviceType, name, address, [], []);

        device.Should().NotBeNull();
        device.Should().BeOfType<PowerFunctionsDevice>();
    }
}
