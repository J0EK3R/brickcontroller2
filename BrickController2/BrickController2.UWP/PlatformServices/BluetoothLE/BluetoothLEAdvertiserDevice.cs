using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using BrickController2.PlatformServices.BluetoothLE;
using BrickController2.Windows.Extensions;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Foundation.Metadata;

namespace BrickController2.Windows.PlatformServices.BluetoothLE
{
    internal class BluetoothLEAdvertiserDevice :
        IBluetoothLEAdvertiserDevice
    {
        #region Fields
        private readonly BluetoothLEAdvertisementPublisher publisher;

        private BluetoothLEManufacturerData bluetoothLEManufacturerData = new BluetoothLEManufacturerData();
        #endregion

        #region Constructor
        /// <summary>
        /// 
        /// </summary>
        public BluetoothLEAdvertiserDevice()
        {
            this.publisher = new BluetoothLEAdvertisementPublisher();
        }
        #endregion

        #region Dispose
        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
        }
        #endregion

        #region StartAdvertiseAsync(AdvertisingInterval advertisingIterval, TxPowerLevel txPowerLevel, ushort manufacturerId, byte[] rawData)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="advertisingIterval"></param>
        /// <param name="txPowerLevel"></param>
        /// <param name="manufacturerId"></param>
        /// <param name="rawData"></param>
        /// <returns></returns>
        public async Task<bool> StartAdvertiseAsync(AdvertisingInterval advertisingIterval, TxPowerLevel txPowerLevel, ushort manufacturerId, byte[] rawData)
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

            //byte[] flags = new byte[] { 0x02, 0x01, 0x02 };
            byte[] flags = new byte[] { 0x02 };

            byte[] data = BitConverter.GetBytes( manufacturerId).Concat(rawData).ToArray();

            this.CreateData(manufacturerId, data);

            BluetoothLEAdvertisementDataSection a = new BluetoothLEAdvertisementDataSection(0x01, flags.ToBuffer());
            BluetoothLEAdvertisementDataSection b = new BluetoothLEAdvertisementDataSection(0xff, data.ToBuffer());
            this.publisher.UseExtendedAdvertisement = true;
            this.publisher.Advertisement.Flags = BluetoothLEAdvertisementFlags.GeneralDiscoverableMode;
            //this.publisher.Advertisement.DataSections.Add(a);
            this.publisher.Advertisement.DataSections.Add(b);

            //this.publisher.Advertisement.Flags = BluetoothLEAdvertisementFlags.GeneralDiscoverableMode;
            //this.publisher.Advertisement.ManufacturerData.Add(this.bluetoothLEManufacturerData);

            this.publisher.Start();

            return await Task.FromResult(false);
        }
        #endregion
        #region StopAdvertiseAsync()
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<bool> StopAdvertiseAsync()
        {
            this.publisher?.Advertisement?.ManufacturerData?.Remove(this.bluetoothLEManufacturerData);
            this.publisher.Stop();

            return await Task.FromResult(false);
        }
        #endregion

        #region ChangeAdvertiseAsync(ushort manufacturerId, byte[] rawData)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="manufacturerId"></param>
        /// <param name="rawData"></param>
        /// <returns></returns>
        public bool ChangeAdvertiseAsync(ushort manufacturerId, byte[] rawData)
        {
            if (this.bluetoothLEManufacturerData != null)
            {
                //this.publisher?.Advertisement?.ManufacturerData?.Remove(this.bluetoothLEManufacturerData);

                this.CreateData(manufacturerId, rawData);

                //this.publisher?.Advertisement?.ManufacturerData?.Add(this.bluetoothLEManufacturerData);

                return true;
            }
            else
            {
                return false;
            }
        }
        #endregion
        #region CreateData(ushort manufacturerId, byte[] sourceArray)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="manufacturerId"></param>
        /// <param name="sourceArray"></param>
        private void CreateData(ushort manufacturerId, byte[] sourceArray)
        {
            this.bluetoothLEManufacturerData.CompanyId = manufacturerId;
            this.bluetoothLEManufacturerData.Data = sourceArray.AsBuffer();
        }
        #endregion
    }
}
