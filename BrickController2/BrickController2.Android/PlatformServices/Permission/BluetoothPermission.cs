﻿using BrickController2.PlatformServices.Permission;
using System.Collections.Generic;
using static Xamarin.Essentials.Permissions;

namespace BrickController2.Droid.PlatformServices.Permission
{
    internal class BluetoothPermission : BasePlatformPermission, IBluetoothPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions
        {
            get
            {
                var permissions = new List<(string androidPermission, bool isRuntime)>();

                if (Android.OS.Build.VERSION.SdkInt <= Android.OS.BuildVersionCodes.R)
                {
                    permissions.Add((Android.Manifest.Permission.Bluetooth, true));
                    permissions.Add((Android.Manifest.Permission.BluetoothAdmin, true));
                    permissions.Add((Android.Manifest.Permission.AccessFineLocation, true));
                    permissions.Add((Android.Manifest.Permission.AccessCoarseLocation, true));
                    permissions.Add((Android.Manifest.Permission.BluetoothAdvertise, true));
                }
                else
                {
                    permissions.Add((Android.Manifest.Permission.BluetoothConnect, true));
                    permissions.Add((Android.Manifest.Permission.BluetoothScan, true));
                    permissions.Add((Android.Manifest.Permission.BluetoothAdvertise, true));
                }

                return permissions.ToArray();
            }
        }
    }
}