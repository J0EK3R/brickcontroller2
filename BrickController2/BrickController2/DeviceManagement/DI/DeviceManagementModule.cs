using Autofac;

namespace BrickController2.DeviceManagement.DI
{
    public class DeviceManagementModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<BluetoothDeviceManager>().As<IBluetoothDeviceManager>().SingleInstance();
            builder.RegisterType<InfraredDeviceManager>().As<IInfraredDeviceManager>().SingleInstance();

            builder.RegisterType<DeviceRepository>().As<IDeviceRepository>().SingleInstance();
            builder.RegisterType<DeviceManager>().As<IDeviceManager>().SingleInstance();

            builder.RegisterType<SBrickDevice>().Keyed<Device>(DeviceType.SBrick);
            builder.RegisterType<BuWizzDevice>().Keyed<Device>(DeviceType.BuWizz);
            builder.RegisterType<BuWizz2Device>().Keyed<Device>(DeviceType.BuWizz2);
            builder.RegisterType<BuWizz3Device>().Keyed<Device>(DeviceType.BuWizz3);
            builder.RegisterType<InfraredDevice>().Keyed<Device>(DeviceType.Infrared);
            builder.RegisterType<PoweredUpDevice>().Keyed<Device>(DeviceType.PoweredUp);
            builder.RegisterType<BoostDevice>().Keyed<Device>(DeviceType.Boost);
            builder.RegisterType<TechnicHubDevice>().Keyed<Device>(DeviceType.TechnicHub);
            builder.RegisterType<DuploTrainHubDevice>().Keyed<Device>(DeviceType.DuploTrainHub);
            builder.RegisterType<CircuitCubeDevice>().Keyed<Device>(DeviceType.CircuitCubes);
            builder.RegisterType<Wedo2Device>().Keyed<Device>(DeviceType.WeDo2);

            // XPBlock
            builder.RegisterType<XPBlock_XC5>().Keyed<Device>(DeviceType.XPBlock_XC5);

            // MouldKing
            builder.RegisterType<MouldKing_15059>().Keyed<Device>(DeviceType.MouldKing_15059);
            builder.RegisterType<MouldKing_4_0_Modul>().Keyed<Device>(DeviceType.MouldKing_4_0_Modul);
            builder.RegisterType<MouldKing_6_0_Modul>().Keyed<Device>(DeviceType.MouldKing_6_0_Modul);
            builder.RegisterType<MouldKing_Mecanum_Modul>().Keyed<Device>(DeviceType.MouldKing_Mecanum_Modul);

            // Hogokids
            builder.RegisterType<HOGOKIDS_8051>().Keyed<Device>(DeviceType.HOGOKIDS_8051);

            // Cada
            builder.RegisterType<Cada_C51072W>().Keyed<Device>(DeviceType.Cada_C51072W);

            // TestModel
            //builder.RegisterType<TestModel>().Keyed<Device>(DeviceType.TestModel);

            builder.Register<DeviceFactory>(c =>
            {
                IComponentContext ctx = c.Resolve<IComponentContext>();
                return (deviceType, name, address, deviceData) => ctx.ResolveKeyed<Device>(deviceType, new NamedParameter("name", name), new NamedParameter("address", address), new NamedParameter("deviceData", deviceData), new NamedParameter("settings", null));
            });
        }
    }
}
