using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using BrickController2.DeviceManagement;
using BrickController2.Helpers;

namespace BrickController2.UI.Converters
{
    public class DeviceTypeToSmallImageConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var deviceType = (DeviceType)value!;
            switch (deviceType)
            {
                case DeviceType.BuWizz:
                case DeviceType.BuWizz2:
                   return ResourceHelper.GetImageResource("buwizz_image_small.png");

                case DeviceType.BuWizz3:
                    return ResourceHelper.GetImageResource("buwizz3_image_small.png");

                case DeviceType.SBrick:
                    return ResourceHelper.GetImageResource("sbrick_image_small.png");

                case DeviceType.Infrared:
                    return ResourceHelper.GetImageResource("infra_image_small.png");

                case DeviceType.PoweredUp:
                    return ResourceHelper.GetImageResource("poweredup_image_small.png");

                case DeviceType.Boost:
                    return ResourceHelper.GetImageResource("boost_image_small.png");

                case DeviceType.TechnicHub:
                    return ResourceHelper.GetImageResource("technichub_image_small.png");

                case DeviceType.DuploTrainHub:
                    return ResourceHelper.GetImageResource("duplotrainhub_image_small.png");

                case DeviceType.CircuitCubes:
                    return ResourceHelper.GetImageResource("circuitcubes_image_small.png");

                case DeviceType.WeDo2:
                    return ResourceHelper.GetImageResource("wedo2hub_image_small.png");

                case DeviceType.XPBlock_XC5:
                  return ResourceHelper.GetImageResource("xpblock_xc5_image_small.png");

                case DeviceType.MouldKing_DIY:
                  return ResourceHelper.GetImageResource("mouldking_diy_image_small.png");
                case DeviceType.MouldKing_4_0_Module:
                    return ResourceHelper.GetImageResource("mouldking_4_0_image_small.png");
                case DeviceType.MouldKing_6_0_Module:
                    return ResourceHelper.GetImageResource("mouldking_6_0_image_small.png");
                case DeviceType.MouldKing_Mecanum_ATV:
                    return ResourceHelper.GetImageResource("mouldking_mecanum_image_small.png");
                case DeviceType.MouldKing_15059:
                    return ResourceHelper.GetImageResource("mouldking_15059_image_small.png");

                case DeviceType.HOGOKIDS_8051:
                    return ResourceHelper.GetImageResource("hogokids_8051_image_small.png");

                case DeviceType.CaDA_RaceCar:
                    return ResourceHelper.GetImageResource("cada_racecar_image_small.png");

                case DeviceType.PowerBox_M_Battery:
                    return ResourceHelper.GetImageResource("powerbox_mbattery_image_small.png");

                default:
                    return ResourceHelper.GetImageResource("technichub_image_small.png");
            }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
