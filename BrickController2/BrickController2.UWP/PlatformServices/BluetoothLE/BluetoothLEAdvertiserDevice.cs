using System;
using System.Threading.Tasks;
using BrickController2.PlatformServices.BluetoothLE;

namespace BrickController2.Windows.PlatformServices.BluetoothLE
{
    internal class BluetoothLEAdvertiserDevice :
        IBluetoothLEAdvertiserDevice
    {
        private readonly object _advertiser;

        public BluetoothLEAdvertiserDevice(object advertiser)
        {
            this._advertiser = advertiser;
        }

        public void Dispose()
        {

        }

        public async Task<bool> StartAdvertiseAsync(AdvertisingInterval advertisingIterval, TxPowerLevel txPowerLevel, int manufacturerId, byte[] rawData)
        {
            #region convert advertisingIterval
            //int advertisingIterval_value = AdvertisingSetParameters.IntervalMax;
            //switch (advertisingIterval)
            //{
            //    case AdvertisingInterval.Min:
            //        advertisingIterval_value = AdvertisingSetParameters.IntervalMin;
            //        break;
            //    case AdvertisingInterval.High:
            //        advertisingIterval_value = AdvertisingSetParameters.IntervalHigh;
            //        break;
            //    case AdvertisingInterval.Medium:
            //        advertisingIterval_value = AdvertisingSetParameters.IntervalMedium;
            //        break;
            //    case AdvertisingInterval.Low:
            //        advertisingIterval_value = AdvertisingSetParameters.IntervalLow;
            //        break;
            //    case AdvertisingInterval.Max:
            //    default:
            //        advertisingIterval_value = AdvertisingSetParameters.IntervalMax; 
            //        break;
            //}
            #endregion
            #region convert txPowerLevel
            //AdvertiseTxPower advertiseTxPower;
            //switch (txPowerLevel)
            //{
            //    case TxPowerLevel.UltraLow:
            //        advertiseTxPower = AdvertiseTxPower.UltraLow;
            //        break;
            //    case TxPowerLevel.Low:
            //        advertiseTxPower = AdvertiseTxPower.Low;
            //        break;
            //    case TxPowerLevel.Medium:
            //        advertiseTxPower = AdvertiseTxPower.Medium;
            //        break;
            //    case TxPowerLevel.High:
            //        advertiseTxPower = AdvertiseTxPower.High;
            //        break;
            //    case TxPowerLevel.Max:
            //        advertiseTxPower = AdvertiseTxPower.Max;
            //        break;
            //    case TxPowerLevel.Min:
            //    default:
            //        advertiseTxPower = AdvertiseTxPower.Min;
            //        break;
            //}
            #endregion

            //AdvertisingSetParameters settings = new AdvertisingSetParameters.Builder()
            //    .SetLegacyMode(true)
            //    .SetConnectable(true)
            //    .SetScannable(true)
            //    .SetInterval(advertisingIterval_value)
            //    .SetTxPowerLevel(advertiseTxPower)
            //    .Build();

            //AdvertiseData data = new AdvertiseData.Builder()
            //    .AddManufacturerData(manufacturerId, rawData)
            //    .Build();

            //this._advertiser.StartAdvertisingSet(
            //    settings,
            //    data,
            //    null,
            //    null,
            //    null,
            //    this);

            return await Task.FromResult(false);
        }

        public async Task<bool> StopAdvertiseAsync()
        {
            //this._advertiser.StopAdvertisingSet(this);


            return await Task.FromResult(false);
        }

        public bool ChangeAdvertiseAsync(int manufacturerId, byte[] rawData)
        {
            //if(this._advertisingSet != null)
            //{
            //    AdvertiseData data = new AdvertiseData.Builder()
            //        .AddManufacturerData(manufacturerId, rawData)
            //        .Build();

            //    this._advertisingSet.SetAdvertisingData(data);

            //    return true;
            //}
            //else
            {
                return false;
            }
        }
    }
}