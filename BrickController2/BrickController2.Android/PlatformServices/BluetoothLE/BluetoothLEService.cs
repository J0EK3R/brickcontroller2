using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using BrickController2.PlatformServices.BluetoothLE;

namespace BrickController2.Droid.PlatformServices.BluetoothLE
{
    public class BluetoothLEService : IBluetoothLEService
    {
        private readonly Context _context;
        private readonly BluetoothAdapter? _bluetoothAdapter;
        private readonly string _DeviceId;

        private bool _isScanning = false;

        public BluetoothLEService(Context context)
        {
            _context = context;

            this._DeviceId = BluetoothLEService.GetDeviceId();

            if (context.PackageManager?.HasSystemFeature(PackageManager.FeatureBluetoothLe) ?? false)
            {
                var bluetoothManager = (BluetoothManager?)context.GetSystemService(Context.BluetoothService);
                _bluetoothAdapter = bluetoothManager?.Adapter;
            }
            else
            {
                _bluetoothAdapter = null;
            }
        }

        public string DeviceID => _DeviceId;

        public bool IsBluetoothLESupported => _bluetoothAdapter != null;
        public bool IsBluetoothOn => _bluetoothAdapter?.IsEnabled ?? false;

        public async Task<bool> ScanDevicesAsync(Action<BrickController2.PlatformServices.BluetoothLE.ScanResult> scanCallback, IEnumerable<Tuple<ushort, byte[]>> advertiseList, CancellationToken token)
        {
            if (!IsBluetoothLESupported || !IsBluetoothOn || _isScanning)
            {
                return false;
            }

            try
            {
                _isScanning = true;
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
                {
                    return await NewScanAsync(scanCallback, advertiseList, token);
                }
                else
                {
                    return await OldScanAsync(scanCallback, advertiseList, token);
                }
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                _isScanning = false;
            }
        }

        public IBluetoothLEDevice? GetKnownDevice(string address)
        {
            if (!IsBluetoothLESupported || _bluetoothAdapter is null)
            {
                return null;
            }

            return new BluetoothLEDevice(_context, _bluetoothAdapter, address);
        }

        private async Task<bool> OldScanAsync(Action<BrickController2.PlatformServices.BluetoothLE.ScanResult> scanCallback, IEnumerable<Tuple<ushort, byte[]>> advertiseList, CancellationToken token)
        {
            try
            {
                var leScanner = new BluetoothLEOldScanner(scanCallback);
#pragma warning disable CS0618 // Type or member is obsolete
                if (!(_bluetoothAdapter?.StartLeScan(leScanner) ?? false))
#pragma warning restore CS0618 // Type or member is obsolete
                {
                    return false;
                }

                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (token.Register(() =>
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    _bluetoothAdapter?.StopLeScan(leScanner);
#pragma warning restore CS0618 // Type or member is obsolete
                    tcs.TrySetResult(true);
                }))
                {
                    return await tcs.Task;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<bool> NewScanAsync(Action<BrickController2.PlatformServices.BluetoothLE.ScanResult> scanCallback, IEnumerable<Tuple<ushort, byte[]>> advertiseList, CancellationToken token)
        {
            try
            {
                IBluetoothLEAdvertiserDevice advertiserDevice = null;
                if (advertiseList != null)
                {
                    advertiserDevice = this.GetBluetoothLEAdvertiserDevice();

                    foreach (Tuple<ushort, byte[]> currentEntry in advertiseList)
                    {
                        await advertiserDevice.StartAdvertiseAsync(AdvertisingInterval.Min, TxPowerLevel.Max, currentEntry.Item1, currentEntry.Item2);
                    }
                }

                var leScanner = new BluetoothLENewScanner(scanCallback);
                var settingsBuilder = new ScanSettings.Builder()?
                    .SetCallbackType(ScanCallbackType.AllMatches)?
                    .SetScanMode(global::Android.Bluetooth.LE.ScanMode.LowLatency);

                _bluetoothAdapter?.BluetoothLeScanner?.StartScan(null, settingsBuilder?.Build(), leScanner);

                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (token.Register(() =>
                {
                    _bluetoothAdapter?.BluetoothLeScanner.StopScan(leScanner);
                    advertiserDevice?.StopAdvertiseAsync();

                    tcs.TrySetResult(true);
                }))
                {
                    return await tcs.Task;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public IBluetoothLEAdvertiserDevice GetBluetoothLEAdvertiserDevice()
        {
            BluetoothLeAdvertiser advertiser = _bluetoothAdapter.BluetoothLeAdvertiser;

            return new BluetoothLEAdvertiserDevice(advertiser);
        }

        private static string GetDeviceId()
        {
            string id = Build.Serial;
            if (string.IsNullOrWhiteSpace(id) || id == Build.Unknown || id == "0")
            {
                //try
                //{
                //    var context = Android.App.Application.Context;
                //    id = Secure.GetString(context.ContentResolver, Secure.AndroidId);
                //}
                //catch (Exception ex)
                //{
                //    Android.Util.Log.Warn("DeviceInfo", "Unable to get id: " + ex.ToString());
                //}
            }

            return id;
        }
    }
}