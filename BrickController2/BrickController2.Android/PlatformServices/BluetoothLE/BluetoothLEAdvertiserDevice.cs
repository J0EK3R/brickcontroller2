using System;
using System.Threading.Tasks;
using System.Threading;
using Android.Bluetooth;
using Android.Bluetooth.LE;
using BrickController2.PlatformServices.BluetoothLE;
using Android.Runtime;

namespace BrickController2.Droid.PlatformServices.BluetoothLE
{
    internal class BluetoothLEAdvertiserDevice : AdvertisingSetCallback,
        IBluetoothLEAdvertiserDevice
    {
        private readonly BluetoothLeAdvertiser _advertiser;
        private AdvertisingSet _advertisingSet;

        public BluetoothLEAdvertiserDevice(BluetoothLeAdvertiser advertiser)
        {
            this._advertiser = advertiser;
        }

        public async Task<bool> StartAdvertiseAsync(int manufacturerId, byte[] rawData)
        {
            AdvertisingSetParameters settings = new AdvertisingSetParameters.Builder()
                .SetLegacyMode(true)
                .SetConnectable(true)
                .SetScannable(true)
                .SetInterval(AdvertisingSetParameters.IntervalMedium)
                .SetTxPowerLevel(AdvertiseTxPower.Max)
                .Build();

            AdvertiseData data = new AdvertiseData.Builder()
                .AddManufacturerData(manufacturerId, rawData)
                .Build();

            this._advertiser.StartAdvertisingSet(
                settings,
                data,
                null,
                null,
                null,
                this);

            return await Task.FromResult(true);
        }

        public async Task<bool> StopAdvertiseAsync()
        {
            this._advertiser.StopAdvertisingSet(this);


            return await Task.FromResult(true);
        }

        public bool ChangeAdvertiseAsync(int manufacturerId, byte[] rawData)
        {
            if(this._advertisingSet != null)
            {
                AdvertiseData data = new AdvertiseData.Builder()
                    .AddManufacturerData(manufacturerId, rawData)
                    .Build();

                this._advertisingSet.SetAdvertisingData(data);

                return true;
            }
            else
            {
                return false;
            }
        }

        public override void OnAdvertisingSetStarted(AdvertisingSet advertisingSet, int txPower, [GeneratedEnum] AdvertiseResult status)
        {
            base.OnAdvertisingSetStarted(advertisingSet, txPower, status);

            this._advertisingSet = advertisingSet;
        }
    }
}