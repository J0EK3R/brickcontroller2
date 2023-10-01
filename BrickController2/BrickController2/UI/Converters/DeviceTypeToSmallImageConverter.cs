﻿using BrickController2.DeviceManagement;
using System;
using System.Globalization;
using BrickController2.Helpers;
using Xamarin.Forms;

namespace BrickController2.UI.Converters
{
    public class DeviceTypeToSmallImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var deviceType = (DeviceType)value;
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

                case DeviceType.MouldKing_15059_Robot:
                    return ResourceHelper.GetImageResource("mouldking_15059_image_small.png");

                case DeviceType.HOGOKIDS_8051:
                    return ResourceHelper.GetImageResource("hogokids_8051_image_small.png");

                case DeviceType.Cada_C51072W_RaceCar:
                    return ResourceHelper.GetImageResource("cada_c51072w_image_small.png");

                default:
                    return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
