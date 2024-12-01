using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using BrickController2.DeviceManagement;
using BrickController2.UI.Commands;

namespace BrickController2.UI.Controls
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class DeviceChannelSelector : ContentView
    {
        public DeviceChannelSelector()
        {
            InitializeComponent();

            SBrickChannel0.Command = new SafeCommand(() => SelectedChannel = 0);
            SBrickChannel1.Command = new SafeCommand(() => SelectedChannel = 1);
            SBrickChannel2.Command = new SafeCommand(() => SelectedChannel = 2);
            SBrickChannel3.Command = new SafeCommand(() => SelectedChannel = 3);
            BuWizzChannel0.Command = new SafeCommand(() => SelectedChannel = 0);
            BuWizzChannel1.Command = new SafeCommand(() => SelectedChannel = 1);
            BuWizzChannel2.Command = new SafeCommand(() => SelectedChannel = 2);
            BuWizzChannel3.Command = new SafeCommand(() => SelectedChannel = 3);
            BuWizz3Channel0.Command = new SafeCommand(() => SelectedChannel = 0);
            BuWizz3Channel1.Command = new SafeCommand(() => SelectedChannel = 1);
            BuWizz3Channel2.Command = new SafeCommand(() => SelectedChannel = 2);
            BuWizz3Channel3.Command = new SafeCommand(() => SelectedChannel = 3);
            BuWizz3Channel4.Command = new SafeCommand(() => SelectedChannel = 4);
            BuWizz3Channel5.Command = new SafeCommand(() => SelectedChannel = 5);
            InfraredChannel0.Command = new SafeCommand(() => SelectedChannel = 0);
            InfraredChannel1.Command = new SafeCommand(() => SelectedChannel = 1);
            PoweredUpChannel0.Command = new SafeCommand(() => SelectedChannel = 0);
            PoweredUpChannel1.Command = new SafeCommand(() => SelectedChannel = 1);
            BoostChannelA.Command = new SafeCommand(() => SelectedChannel = 0);
            BoostChannelB.Command = new SafeCommand(() => SelectedChannel = 1);
            BoostChannelC.Command = new SafeCommand(() => SelectedChannel = 2);
            BoostChannelD.Command = new SafeCommand(() => SelectedChannel = 3);
            TechnicHubChannel0.Command = new SafeCommand(() => SelectedChannel = 0);
            TechnicHubChannel1.Command = new SafeCommand(() => SelectedChannel = 1);
            TechnicHubChannel2.Command = new SafeCommand(() => SelectedChannel = 2);
            TechnicHubChannel3.Command = new SafeCommand(() => SelectedChannel = 3);
            DuploTrainHubChannel0.Command = new SafeCommand(() => SelectedChannel = 0);
            CircuitCubesA.Command = new SafeCommand(() => SelectedChannel = 0);
            CircuitCubesB.Command = new SafeCommand(() => SelectedChannel = 1);
            CircuitCubesC.Command = new SafeCommand(() => SelectedChannel = 2);
            WedoChannel0.Command = new SafeCommand(() => SelectedChannel = 0);
            WedoChannel1.Command = new SafeCommand(() => SelectedChannel = 1);

            XPBlock_XC5Channel0.Command = new SafeCommand(() => SelectedChannel = 0);
            XPBlock_XC5Channel1.Command = new SafeCommand(() => SelectedChannel = 1);
            XPBlock_XC5Channel2.Command = new SafeCommand(() => SelectedChannel = 2);
            XPBlock_XC5Channel3.Command = new SafeCommand(() => SelectedChannel = 3);
            XPBlock_XC5Channel4.Command = new SafeCommand(() => SelectedChannel = 4);
            XPBlock_XC5Channel5.Command = new SafeCommand(() => SelectedChannel = 5);

            MouldKing_DIYChannel0.Command = new SafeCommand(() => SelectedChannel = 0);
            MouldKing_DIYChannel1.Command = new SafeCommand(() => SelectedChannel = 1);
            MouldKing_DIYChannel2.Command = new SafeCommand(() => SelectedChannel = 2);
            MouldKing_DIYChannel3.Command = new SafeCommand(() => SelectedChannel = 3);
            MouldKing_DIYChannel4.Command = new SafeCommand(() => SelectedChannel = 4);
            MouldKing_DIYChannel5.Command = new SafeCommand(() => SelectedChannel = 5);

            MouldKing_15059Channel0.Command = new SafeCommand(() => SelectedChannel = 0);
            MouldKing_15059Channel1.Command = new SafeCommand(() => SelectedChannel = 1);

            MouldKing_4_0_ModuleChannel0.Command = new SafeCommand(() => SelectedChannel = 0);
            MouldKing_4_0_ModuleChannel1.Command = new SafeCommand(() => SelectedChannel = 1);
            MouldKing_4_0_ModuleChannel2.Command = new SafeCommand(() => SelectedChannel = 2);
            MouldKing_4_0_ModuleChannel3.Command = new SafeCommand(() => SelectedChannel = 3);
            MouldKing_4_0_ModuleChannel4.Command = new SafeCommand(() => SelectedChannel = 4);
            MouldKing_4_0_ModuleChannel5.Command = new SafeCommand(() => SelectedChannel = 5);
            MouldKing_4_0_ModuleChannel6.Command = new SafeCommand(() => SelectedChannel = 6);
            MouldKing_4_0_ModuleChannel7.Command = new SafeCommand(() => SelectedChannel = 7);
            MouldKing_4_0_ModuleChannel8.Command = new SafeCommand(() => SelectedChannel = 8);
            MouldKing_4_0_ModuleChannel9.Command = new SafeCommand(() => SelectedChannel = 9);
            MouldKing_4_0_ModuleChannel10.Command = new SafeCommand(() => SelectedChannel = 10);
            MouldKing_4_0_ModuleChannel11.Command = new SafeCommand(() => SelectedChannel = 11);

            MouldKing_6_0_ModuleChannel0.Command = new SafeCommand(() => SelectedChannel = 0);
            MouldKing_6_0_ModuleChannel1.Command = new SafeCommand(() => SelectedChannel = 1);
            MouldKing_6_0_ModuleChannel2.Command = new SafeCommand(() => SelectedChannel = 2);
            MouldKing_6_0_ModuleChannel3.Command = new SafeCommand(() => SelectedChannel = 3);
            MouldKing_6_0_ModuleChannel4.Command = new SafeCommand(() => SelectedChannel = 4);
            MouldKing_6_0_ModuleChannel5.Command = new SafeCommand(() => SelectedChannel = 5);

            MouldKing_Mecanum_ATVChannel0.Command = new SafeCommand(() => SelectedChannel = 0);
            MouldKing_Mecanum_ATVChannel1.Command = new SafeCommand(() => SelectedChannel = 1);
            MouldKing_Mecanum_ATVChannel2.Command = new SafeCommand(() => SelectedChannel = 2);
            MouldKing_Mecanum_ATVChannel3.Command = new SafeCommand(() => SelectedChannel = 3);
            MouldKing_Mecanum_ATVChannel4.Command = new SafeCommand(() => SelectedChannel = 4);
            MouldKing_Mecanum_ATVChannel5.Command = new SafeCommand(() => SelectedChannel = 5);

            HOGOKIDS_8051Channel0.Command = new SafeCommand(() => SelectedChannel = 0);
            HOGOKIDS_8051Channel1.Command = new SafeCommand(() => SelectedChannel = 1);
            HOGOKIDS_8051Channel2.Command = new SafeCommand(() => SelectedChannel = 2);
            HOGOKIDS_8051Channel3.Command = new SafeCommand(() => SelectedChannel = 3);
            HOGOKIDS_8051Channel4.Command = new SafeCommand(() => SelectedChannel = 4);
            HOGOKIDS_8051Channel5.Command = new SafeCommand(() => SelectedChannel = 5);

            CaDA_RaceCarChannel0.Command = new SafeCommand(() => SelectedChannel = 0);
            CaDA_RaceCarChannel1.Command = new SafeCommand(() => SelectedChannel = 1);
            CaDA_RaceCarChannel2.Command = new SafeCommand(() => SelectedChannel = 2);
            CaDA_RaceCarChannel3.Command = new SafeCommand(() => SelectedChannel = 3);
            CaDA_RaceCarChannel4.Command = new SafeCommand(() => SelectedChannel = 4);
            CaDA_RaceCarChannel5.Command = new SafeCommand(() => SelectedChannel = 5);

            PowerBox_M_BatteryChannel0.Command = new SafeCommand(() => SelectedChannel = 0);
        }

        public static BindableProperty DeviceTypeProperty = BindableProperty.Create(nameof(DeviceType), typeof(DeviceType), typeof(DeviceChannelSelector), default(DeviceType), BindingMode.OneWay, null, OnDeviceTypeChanged);
        public static BindableProperty SelectedChannelProperty = BindableProperty.Create(nameof(SelectedChannel), typeof(int), typeof(DeviceChannelSelector), 0, BindingMode.TwoWay, null, OnSelectedChannelChanged);

        public DeviceType DeviceType
        {
            get => (DeviceType)GetValue(DeviceTypeProperty);
            set => SetValue(DeviceTypeProperty, value);
        }

        public int SelectedChannel
        {
            get => (int)GetValue(SelectedChannelProperty);
            set => SetValue(SelectedChannelProperty, value);
        }

        private static void OnDeviceTypeChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is DeviceChannelSelector dcs)
            {
                var deviceType = (DeviceType)newValue;
                dcs.SbrickSection.IsVisible = deviceType == DeviceType.SBrick;
                dcs.BuWizzSection.IsVisible = deviceType == DeviceType.BuWizz || deviceType == DeviceType.BuWizz2;
                dcs.BuWizz3Section.IsVisible = deviceType == DeviceType.BuWizz3;
                dcs.InfraredSection.IsVisible = deviceType == DeviceType.Infrared;
                dcs.PoweredUpSection.IsVisible = deviceType == DeviceType.PoweredUp;
                dcs.BoostSection.IsVisible = deviceType == DeviceType.Boost;
                dcs.TechnicHubSection.IsVisible = deviceType == DeviceType.TechnicHub;
                dcs.DuploTrainHubSection.IsVisible = deviceType == DeviceType.DuploTrainHub;
                dcs.CircuitCubes.IsVisible = deviceType == DeviceType.CircuitCubes;
                dcs.Wedo2Section.IsVisible = deviceType == DeviceType.WeDo2;

                dcs.XPBlock_XC5Section.IsVisible = deviceType == DeviceType.XPBlock_XC5;
                dcs.MouldKing_DIYSection.IsVisible = deviceType == DeviceType.MouldKing_DIY;
                dcs.MouldKing_15059Section.IsVisible = deviceType == DeviceType.MouldKing_15059;
                dcs.MouldKing_4_0_ModuleSection.IsVisible = deviceType == DeviceType.MouldKing_4_0_Module;
                dcs.MouldKing_6_0_ModuleSection.IsVisible = deviceType == DeviceType.MouldKing_6_0_Module;
                dcs.MouldKing_Mecanum_ATVSection.IsVisible = deviceType == DeviceType.MouldKing_Mecanum_ATV;
                dcs.HOGOKIDS_8051Section.IsVisible = deviceType == DeviceType.MouldKing_Mecanum_ATV;
                dcs.CaDA_RaceCarSection.IsVisible = deviceType == DeviceType.CaDA_RaceCar;
                dcs.PowerBox_M_BatterySection.IsVisible = deviceType == DeviceType.PowerBox_M_Battery;
            }
        }

        private static void OnSelectedChannelChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is DeviceChannelSelector dcs)
            {
                int selectedChannel = (int)newValue;
                dcs.SBrickChannel0.SelectedChannel = selectedChannel;
                dcs.SBrickChannel1.SelectedChannel = selectedChannel;
                dcs.SBrickChannel2.SelectedChannel = selectedChannel;
                dcs.SBrickChannel3.SelectedChannel = selectedChannel;
                dcs.BuWizzChannel0.SelectedChannel = selectedChannel;
                dcs.BuWizzChannel1.SelectedChannel = selectedChannel;
                dcs.BuWizzChannel2.SelectedChannel = selectedChannel;
                dcs.BuWizzChannel3.SelectedChannel = selectedChannel;
                dcs.BuWizz3Channel0.SelectedChannel = selectedChannel;
                dcs.BuWizz3Channel1.SelectedChannel = selectedChannel;
                dcs.BuWizz3Channel2.SelectedChannel = selectedChannel;
                dcs.BuWizz3Channel3.SelectedChannel = selectedChannel;
                dcs.BuWizz3Channel4.SelectedChannel = selectedChannel;
                dcs.BuWizz3Channel5.SelectedChannel = selectedChannel;
                dcs.InfraredChannel0.SelectedChannel = selectedChannel;
                dcs.InfraredChannel1.SelectedChannel = selectedChannel;
                dcs.PoweredUpChannel0.SelectedChannel = selectedChannel;
                dcs.PoweredUpChannel1.SelectedChannel = selectedChannel;
                dcs.BoostChannelA.SelectedChannel = selectedChannel;
                dcs.BoostChannelB.SelectedChannel = selectedChannel;
                dcs.BoostChannelC.SelectedChannel = selectedChannel;
                dcs.BoostChannelD.SelectedChannel = selectedChannel;
                dcs.TechnicHubChannel0.SelectedChannel = selectedChannel;
                dcs.TechnicHubChannel1.SelectedChannel = selectedChannel;
                dcs.TechnicHubChannel2.SelectedChannel = selectedChannel;
                dcs.TechnicHubChannel3.SelectedChannel = selectedChannel;
                dcs.DuploTrainHubChannel0.SelectedChannel = selectedChannel;
                dcs.CircuitCubesA.SelectedChannel = selectedChannel;
                dcs.CircuitCubesB.SelectedChannel = selectedChannel;
                dcs.CircuitCubesC.SelectedChannel = selectedChannel;
                dcs.WedoChannel0.SelectedChannel = selectedChannel;
                dcs.WedoChannel1.SelectedChannel = selectedChannel;

                dcs.XPBlock_XC5Channel0.SelectedChannel = selectedChannel;
                dcs.XPBlock_XC5Channel1.SelectedChannel = selectedChannel;
                dcs.XPBlock_XC5Channel2.SelectedChannel = selectedChannel;
                dcs.XPBlock_XC5Channel3.SelectedChannel = selectedChannel;
                dcs.XPBlock_XC5Channel4.SelectedChannel = selectedChannel;
                dcs.XPBlock_XC5Channel5.SelectedChannel = selectedChannel;

                dcs.MouldKing_DIYChannel0.SelectedChannel = selectedChannel;
                dcs.MouldKing_DIYChannel1.SelectedChannel = selectedChannel;
                dcs.MouldKing_DIYChannel2.SelectedChannel = selectedChannel;
                dcs.MouldKing_DIYChannel3.SelectedChannel = selectedChannel;
                dcs.MouldKing_DIYChannel4.SelectedChannel = selectedChannel;
                dcs.MouldKing_DIYChannel5.SelectedChannel = selectedChannel;

                dcs.MouldKing_15059Channel0.SelectedChannel = selectedChannel;
                dcs.MouldKing_15059Channel1.SelectedChannel = selectedChannel;

                dcs.MouldKing_4_0_ModuleChannel0.SelectedChannel = selectedChannel;
                dcs.MouldKing_4_0_ModuleChannel1.SelectedChannel = selectedChannel;
                dcs.MouldKing_4_0_ModuleChannel2.SelectedChannel = selectedChannel;
                dcs.MouldKing_4_0_ModuleChannel3.SelectedChannel = selectedChannel;
                dcs.MouldKing_4_0_ModuleChannel4.SelectedChannel = selectedChannel;
                dcs.MouldKing_4_0_ModuleChannel5.SelectedChannel = selectedChannel;
                dcs.MouldKing_4_0_ModuleChannel6.SelectedChannel = selectedChannel;
                dcs.MouldKing_4_0_ModuleChannel7.SelectedChannel = selectedChannel;
                dcs.MouldKing_4_0_ModuleChannel8.SelectedChannel = selectedChannel;
                dcs.MouldKing_4_0_ModuleChannel9.SelectedChannel = selectedChannel;
                dcs.MouldKing_4_0_ModuleChannel10.SelectedChannel = selectedChannel;
                dcs.MouldKing_4_0_ModuleChannel11.SelectedChannel = selectedChannel;

                dcs.MouldKing_6_0_ModuleChannel0.SelectedChannel = selectedChannel;
                dcs.MouldKing_6_0_ModuleChannel1.SelectedChannel = selectedChannel;
                dcs.MouldKing_6_0_ModuleChannel2.SelectedChannel = selectedChannel;
                dcs.MouldKing_6_0_ModuleChannel3.SelectedChannel = selectedChannel;
                dcs.MouldKing_6_0_ModuleChannel4.SelectedChannel = selectedChannel;
                dcs.MouldKing_6_0_ModuleChannel5.SelectedChannel = selectedChannel;

                dcs.MouldKing_Mecanum_ATVChannel0.SelectedChannel = selectedChannel;
                dcs.MouldKing_Mecanum_ATVChannel1.SelectedChannel = selectedChannel;
                dcs.MouldKing_Mecanum_ATVChannel2.SelectedChannel = selectedChannel;
                dcs.MouldKing_Mecanum_ATVChannel3.SelectedChannel = selectedChannel;
                dcs.MouldKing_Mecanum_ATVChannel4.SelectedChannel = selectedChannel;
                dcs.MouldKing_Mecanum_ATVChannel5.SelectedChannel = selectedChannel;

                dcs.HOGOKIDS_8051Channel0.SelectedChannel = selectedChannel;
                dcs.HOGOKIDS_8051Channel1.SelectedChannel = selectedChannel;
                dcs.HOGOKIDS_8051Channel2.SelectedChannel = selectedChannel;
                dcs.HOGOKIDS_8051Channel3.SelectedChannel = selectedChannel;
                dcs.HOGOKIDS_8051Channel4.SelectedChannel = selectedChannel;
                dcs.HOGOKIDS_8051Channel5.SelectedChannel = selectedChannel;

                dcs.CaDA_RaceCarChannel0.SelectedChannel = selectedChannel;
                dcs.CaDA_RaceCarChannel1.SelectedChannel = selectedChannel;
                dcs.CaDA_RaceCarChannel2.SelectedChannel = selectedChannel;
                dcs.CaDA_RaceCarChannel3.SelectedChannel = selectedChannel;
                dcs.CaDA_RaceCarChannel4.SelectedChannel = selectedChannel;
                dcs.CaDA_RaceCarChannel5.SelectedChannel = selectedChannel;

                dcs.PowerBox_M_BatteryChannel0.SelectedChannel = selectedChannel;
            }
        }
    }
}