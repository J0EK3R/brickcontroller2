using BrickController2.DeviceManagement;
using BrickController2.UI.Commands;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

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

            // XP_Block_XC5
            XP_Block_XC5_Channel0.Command = new SafeCommand(() => SelectedChannel = 0);
            XP_Block_XC5_Channel1.Command = new SafeCommand(() => SelectedChannel = 1);
            XP_Block_XC5_Channel2.Command = new SafeCommand(() => SelectedChannel = 2);
            XP_Block_XC5_Channel3.Command = new SafeCommand(() => SelectedChannel = 3);
            XP_Block_XC5_Channel4.Command = new SafeCommand(() => SelectedChannel = 4);
            XP_Block_XC5_Channel5.Command = new SafeCommand(() => SelectedChannel = 5);


            // MouldKing_15059
            MouldKing_15059_Channel0.Command = new SafeCommand(() => SelectedChannel = 0);
            MouldKing_15059_Channel1.Command = new SafeCommand(() => SelectedChannel = 1);

            // MouldKing4.0
            MouldKing_4_0_Module_Channel0.Command = new SafeCommand(() => SelectedChannel = 0);
            MouldKing_4_0_Module_Channel1.Command = new SafeCommand(() => SelectedChannel = 1);
            MouldKing_4_0_Module_Channel2.Command = new SafeCommand(() => SelectedChannel = 2);
            MouldKing_4_0_Module_Channel3.Command = new SafeCommand(() => SelectedChannel = 3);
            MouldKing_4_0_Module_Channel4.Command = new SafeCommand(() => SelectedChannel = 4);
            MouldKing_4_0_Module_Channel5.Command = new SafeCommand(() => SelectedChannel = 5);
            MouldKing_4_0_Module_Channel6.Command = new SafeCommand(() => SelectedChannel = 6);
            MouldKing_4_0_Module_Channel7.Command = new SafeCommand(() => SelectedChannel = 7);
            MouldKing_4_0_Module_Channel8.Command = new SafeCommand(() => SelectedChannel = 8);
            MouldKing_4_0_Module_Channel9.Command = new SafeCommand(() => SelectedChannel = 9);
            MouldKing_4_0_Module_Channel10.Command = new SafeCommand(() => SelectedChannel = 10);
            MouldKing_4_0_Module_Channel11.Command = new SafeCommand(() => SelectedChannel = 11);

            // MouldKing6.0
            MouldKing_6_0_Module_Channel0.Command = new SafeCommand(() => SelectedChannel = 0);
            MouldKing_6_0_Module_Channel1.Command = new SafeCommand(() => SelectedChannel = 1);
            MouldKing_6_0_Module_Channel2.Command = new SafeCommand(() => SelectedChannel = 2);
            MouldKing_6_0_Module_Channel3.Command = new SafeCommand(() => SelectedChannel = 3);
            MouldKing_6_0_Module_Channel4.Command = new SafeCommand(() => SelectedChannel = 4);
            MouldKing_6_0_Module_Channel5.Command = new SafeCommand(() => SelectedChannel = 5);

            // MouldKing_15059
            MouldKing_Mecanum_Module_Channel0.Command = new SafeCommand(() => SelectedChannel = 0);
            MouldKing_Mecanum_Module_Channel1.Command = new SafeCommand(() => SelectedChannel = 1);
            MouldKing_Mecanum_Module_Channel2.Command = new SafeCommand(() => SelectedChannel = 2);
            MouldKing_Mecanum_Module_Channel3.Command = new SafeCommand(() => SelectedChannel = 3);
            MouldKing_Mecanum_Module_Channel4.Command = new SafeCommand(() => SelectedChannel = 4);
            MouldKing_Mecanum_Module_Channel5.Command = new SafeCommand(() => SelectedChannel = 5);

            // HOGOKIDS_8051
            HOGOKIDS_8051_Channel0.Command = new SafeCommand(() => SelectedChannel = 0);
            HOGOKIDS_8051_Channel1.Command = new SafeCommand(() => SelectedChannel = 1);

            // Cada_C51072W
            Cada_C51072W_Channel0.Command = new SafeCommand(() => SelectedChannel = 0);
            Cada_C51072W_Channel1.Command = new SafeCommand(() => SelectedChannel = 1);

            // TestModel
            TestModel_Channel0.Command = new SafeCommand(() => SelectedChannel = 0);
            TestModel_Channel1.Command = new SafeCommand(() => SelectedChannel = 1);
            TestModel_Channel2.Command = new SafeCommand(() => SelectedChannel = 2);
            TestModel_Channel3.Command = new SafeCommand(() => SelectedChannel = 3);
            TestModel_Channel4.Command = new SafeCommand(() => SelectedChannel = 4);
            TestModel_Channel5.Command = new SafeCommand(() => SelectedChannel = 5);
        }

        public static BindableProperty DeviceProperty = BindableProperty.Create(nameof(DeviceManagement.Device), typeof(DeviceManagement.Device), typeof(DeviceChannelSelector), null, BindingMode.OneWay, null, OnDeviceChanged);
        public static BindableProperty SelectedChannelProperty = BindableProperty.Create(nameof(SelectedChannel), typeof(int), typeof(DeviceChannelSelector), 0, BindingMode.TwoWay, null, OnSelectedChannelChanged);

        public DeviceManagement.Device Device
        {
            get => (DeviceManagement.Device)GetValue(DeviceProperty);
            set => SetValue(DeviceProperty, value);
        }

        public int SelectedChannel
        {
            get => (int)GetValue(SelectedChannelProperty);
            set => SetValue(SelectedChannelProperty, value);
        }

        private static void OnDeviceChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is DeviceChannelSelector dcs)
            {
                DeviceManagement.Device device = newValue as DeviceManagement.Device;
                DeviceType deviceType = device?.DeviceType ?? DeviceType.Unknown;

                #region sections
                dcs.SbrickSection.IsVisible = false;
                dcs.BuWizzSection.IsVisible = false;
                dcs.BuWizz3Section.IsVisible = false;
                dcs.InfraredSection.IsVisible = false;
                dcs.PoweredUpSection.IsVisible = false;
                dcs.BoostSection.IsVisible = false;
                dcs.TechnicHubSection.IsVisible = false;
                dcs.DuploTrainHubSection.IsVisible = false;
                dcs.CircuitCubes.IsVisible = false;
                dcs.Wedo2Section.IsVisible = false;

                dcs.XP_Block_XC5_Section.IsVisible = false;

                // MouldKing
                dcs.MouldKing_15059_Section.IsVisible = false;
                dcs.MouldKing_4_0_Module_Section.IsVisible = false;
                dcs.MouldKing_6_0_Module_Section.IsVisible = false;
                dcs.MouldKing_Mecanum_Module_Section.IsVisible = false;

                // Hogokids
                dcs.HOGOKIDS_8051_Section.IsVisible = false;

                // Cada
                dcs.Cada_C51072W_Section.IsVisible = false;

                // TestModel
                dcs.TestModel_Section.IsVisible = false;
                #endregion

                switch (deviceType)
                 {
                    #region DeviceType.SBrick
                    case DeviceType.SBrick:
                        dcs.SbrickSection.IsVisible = true;
                        break;
                    #endregion
                    #region BuWizz BuWizz2
                    case DeviceType.BuWizz:
                    case DeviceType.BuWizz2:
                        dcs.BuWizzSection.IsVisible = true;
                        break;
                    #endregion
                    #region BuWizz3
                    case DeviceType.BuWizz3:
                        dcs.BuWizz3Section.IsVisible = true;
                        break;
                    #endregion
                    #region Infrared
                    case DeviceType.Infrared:
                        dcs.InfraredSection.IsVisible = true;
                        break;
                    #endregion
                    #region Boost
                    case DeviceType.Boost:
                        dcs.BoostSection.IsVisible = true;
                        break;
                    #endregion
                    #region PowerUp
                    case DeviceType.PoweredUp:
                        dcs.PoweredUpSection.IsVisible = true;
                        break;
                    #endregion
                    #region TechnicHub
                    case DeviceType.TechnicHub:
                        dcs.TechnicHubSection.IsVisible = true;
                        break;
                    #endregion
                    #region DuploTrainHub
                    case DeviceType.DuploTrainHub:
                        dcs.DuploTrainHubSection.IsVisible = true;
                        break;
                    #endregion
                    #region CircuitCubes
                    case DeviceType.CircuitCubes:
                        dcs.CircuitCubes.IsVisible = true;
                        break;
                    #endregion
                    #region WeDo2
                    case DeviceType.WeDo2:
                        dcs.Wedo2Section.IsVisible = true;
                        break;
                    #endregion

                    #region XPBlock_XC5
                    case DeviceType.XPBlock_XC5:
                        dcs.XP_Block_XC5_Section.IsVisible = true;
                        break;
                    #endregion

                    #region MouldKing 15059
                    case DeviceType.MouldKing_15059:
                        dcs.MouldKing_15059_Section.IsVisible = true;
                        break;
                    #endregion
                    #region MouldKing 4.0
                    case DeviceType.MouldKing_4_0_Modul:
                        dcs.MouldKing_4_0_Module_Section.IsVisible = true;
                        #region channels
                        dcs.MouldKing_4_0_Module_Channel0.IsVisible = device.NumberOfChannels >= 1;
                        dcs.MouldKing_4_0_Module_Channel1.IsVisible = device.NumberOfChannels >= 2;
                        dcs.MouldKing_4_0_Module_Channel2.IsVisible = device.NumberOfChannels >= 3;
                        dcs.MouldKing_4_0_Module_Channel3.IsVisible = device.NumberOfChannels >= 4;
                        dcs.MouldKing_4_0_Module_Channel4.IsVisible = device.NumberOfChannels >= 5;
                        dcs.MouldKing_4_0_Module_Channel5.IsVisible = device.NumberOfChannels >= 6;
                        dcs.MouldKing_4_0_Module_Channel6.IsVisible = device.NumberOfChannels >= 7;
                        dcs.MouldKing_4_0_Module_Channel7.IsVisible = device.NumberOfChannels >= 8;
                        dcs.MouldKing_4_0_Module_Channel8.IsVisible = device.NumberOfChannels >= 9;
                        dcs.MouldKing_4_0_Module_Channel9.IsVisible = device.NumberOfChannels >= 10;
                        dcs.MouldKing_4_0_Module_Channel10.IsVisible = device.NumberOfChannels >= 11;
                        dcs.MouldKing_4_0_Module_Channel11.IsVisible = device.NumberOfChannels >= 12;
                        #endregion
                        break;
                    #endregion
                    #region MouldKing 6.0
                    case DeviceType.MouldKing_6_0_Modul:
                        dcs.MouldKing_6_0_Module_Section.IsVisible = true;
                        break;
                    #endregion
                    #region MouldKing Mecanum
                    case DeviceType.MouldKing_Mecanum_Modul:
                        dcs.MouldKing_Mecanum_Module_Section.IsVisible = true;
                        break;
                    #endregion

                    #region HogoKids
                    case DeviceType.HOGOKIDS_8051:
                        // Hogokids
                        dcs.HOGOKIDS_8051_Section.IsVisible = true;
                        break;
                    #endregion

                    #region Cada
                    case DeviceType.Cada_C51072W:
                        // Cada
                        dcs.Cada_C51072W_Section.IsVisible = true;
                        break;
                    #endregion
                    #region TestModel
                    case DeviceType.TestModel:
                        // Cada
                        dcs.Cada_C51072W_Section.IsVisible = false;
                        break;
                    #endregion
                    #region Unknown
                    case DeviceType.Unknown:
                    default:
                        break;
                        #endregion
                }
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

                // XP_Block_XC5
                dcs.XP_Block_XC5_Channel0.SelectedChannel = selectedChannel;
                dcs.XP_Block_XC5_Channel1.SelectedChannel = selectedChannel;
                dcs.XP_Block_XC5_Channel2.SelectedChannel = selectedChannel;
                dcs.XP_Block_XC5_Channel3.SelectedChannel = selectedChannel;
                dcs.XP_Block_XC5_Channel4.SelectedChannel = selectedChannel;
                dcs.XP_Block_XC5_Channel5.SelectedChannel = selectedChannel;

                // MouldKing_15059
                dcs.MouldKing_15059_Channel0.SelectedChannel = selectedChannel;
                dcs.MouldKing_15059_Channel1.SelectedChannel = selectedChannel;

                // MouldKing_4_0
                dcs.MouldKing_4_0_Module_Channel0.SelectedChannel = selectedChannel;
                dcs.MouldKing_4_0_Module_Channel1.SelectedChannel = selectedChannel;
                dcs.MouldKing_4_0_Module_Channel2.SelectedChannel = selectedChannel;
                dcs.MouldKing_4_0_Module_Channel3.SelectedChannel = selectedChannel;
                dcs.MouldKing_4_0_Module_Channel4.SelectedChannel = selectedChannel;
                dcs.MouldKing_4_0_Module_Channel5.SelectedChannel = selectedChannel;
                dcs.MouldKing_4_0_Module_Channel6.SelectedChannel = selectedChannel;
                dcs.MouldKing_4_0_Module_Channel7.SelectedChannel = selectedChannel;
                dcs.MouldKing_4_0_Module_Channel8.SelectedChannel = selectedChannel;
                dcs.MouldKing_4_0_Module_Channel9.SelectedChannel = selectedChannel;
                dcs.MouldKing_4_0_Module_Channel10.SelectedChannel = selectedChannel;
                dcs.MouldKing_4_0_Module_Channel11.SelectedChannel = selectedChannel;

                // MouldKing_6_0
                dcs.MouldKing_6_0_Module_Channel0.SelectedChannel = selectedChannel;
                dcs.MouldKing_6_0_Module_Channel1.SelectedChannel = selectedChannel;
                dcs.MouldKing_6_0_Module_Channel2.SelectedChannel = selectedChannel;
                dcs.MouldKing_6_0_Module_Channel3.SelectedChannel = selectedChannel;
                dcs.MouldKing_6_0_Module_Channel4.SelectedChannel = selectedChannel;
                dcs.MouldKing_6_0_Module_Channel5.SelectedChannel = selectedChannel;

                // MouldKing_Mecanum_Module
                dcs.MouldKing_Mecanum_Module_Channel0.SelectedChannel = selectedChannel;
                dcs.MouldKing_Mecanum_Module_Channel1.SelectedChannel = selectedChannel;
                dcs.MouldKing_Mecanum_Module_Channel2.SelectedChannel = selectedChannel;
                dcs.MouldKing_Mecanum_Module_Channel3.SelectedChannel = selectedChannel;
                dcs.MouldKing_Mecanum_Module_Channel4.SelectedChannel = selectedChannel;
                dcs.MouldKing_Mecanum_Module_Channel5.SelectedChannel = selectedChannel;

                // HOGOKIDS_8051
                dcs.HOGOKIDS_8051_Channel0.SelectedChannel = selectedChannel;
                dcs.HOGOKIDS_8051_Channel1.SelectedChannel = selectedChannel;

                // Cada RC
                dcs.Cada_C51072W_Channel0.SelectedChannel = selectedChannel;
                dcs.Cada_C51072W_Channel1.SelectedChannel = selectedChannel;

                // TestModel
                dcs.TestModel_Channel0.SelectedChannel = selectedChannel;
                dcs.TestModel_Channel1.SelectedChannel = selectedChannel;
                dcs.TestModel_Channel2.SelectedChannel = selectedChannel;
                dcs.TestModel_Channel3.SelectedChannel = selectedChannel;
                dcs.TestModel_Channel4.SelectedChannel = selectedChannel;
                dcs.TestModel_Channel5.SelectedChannel = selectedChannel;
            }
        }
    }
}