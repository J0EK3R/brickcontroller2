﻿using System;
using System.Globalization;
using BrickController2.DeviceManagement;
using BrickController2.Helpers;
using Xamarin.Forms;

namespace BrickController2.UI.Converters
{
    public class DeviceTypeToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var deviceType = (DeviceType)value;
            return Convert(deviceType);
        }

        public ImageSource Convert(DeviceType deviceType)
        {
            switch (deviceType)
            {
                case DeviceType.BuWizz:
                case DeviceType.BuWizz2:
                    return ResourceHelper.GetImageResource("buwizz_image.png");

                case DeviceType.BuWizz3:
                    return ResourceHelper.GetImageResource("buwizz3_image.png");

                case DeviceType.SBrick:
                    return ResourceHelper.GetImageResource("sbrick_image.png");

                case DeviceType.Infrared:
                    return ResourceHelper.GetImageResource("infra_image.png");

                case DeviceType.PoweredUp:
                    return ResourceHelper.GetImageResource("poweredup_image.png");

                case DeviceType.Boost:
                    return ResourceHelper.GetImageResource("boost_image.png");

                case DeviceType.TechnicHub:
                    return ResourceHelper.GetImageResource("technichub_image.png");

                case DeviceType.DuploTrainHub:
                    return ResourceHelper.GetImageResource("duplotrainhub_image.png");

                case DeviceType.CircuitCubes:
                    return ResourceHelper.GetImageResource("circuitcubes_image.png");

                case DeviceType.WeDo2:
                    return ResourceHelper.GetImageResource("wedo2hub_image.png");

                case DeviceType.MouldKing_15059:
                case DeviceType.MouldKing_4_0_Modul:
                case DeviceType.MouldKing_6_0_Modul:
                case DeviceType.MouldKing_Mecanum_Modul:
                    return ResourceHelper.GetImageResource("mouldking_15059_image.png");

                case DeviceType.HOGOKIDS_8051:
                    return ResourceHelper.GetImageResource("hogokids_8051_image.png");

                case DeviceType.Cada_C51072W:
                    return ResourceHelper.GetImageResource("cada_c51072w_image.png");

                default:
                    return ResourceHelper.GetImageResource("technichub_image.png");
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
