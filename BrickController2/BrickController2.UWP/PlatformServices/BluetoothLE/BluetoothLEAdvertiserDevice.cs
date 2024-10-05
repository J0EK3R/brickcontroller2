using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using BrickController2.PlatformServices.BluetoothLE;
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

            this.CreateData(manufacturerId, rawData);


            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 10))
            {
                //publisher.UseExtendedAdvertisement = true;
                //publisher.IsAnonymous = true;
                //publisher.IncludeTransmitPowerLevel = includeTxPower;
                //publisher.PreferredTransmitPowerLevelInDBm = txPower;
            }


            //this.publisher.UseExtendedAdvertisement = true;
            //this.publisher.IsAnonymous = true;

            //this.publisher.Advertisement.Flags = BluetoothLEAdvertisementFlags.None;
            this.publisher.Advertisement.ManufacturerData.Add(this.bluetoothLEManufacturerData);

            // From old code (Which is not commented)
            //var data = new BluetoothLEAdvertisementDataSection
            //{
            //    DataType = 0x02,
            //    Data = new byte[] { 0x01, 0x02 }.AsBuffer()
            //};
            //this.publisher.Advertisement.DataSections.Add(data);

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
            //byte[] preinsertArray = new byte[]
            //{
            //    0x02,
            //    0x01,
            //    0x02
            //};

            //byte[] targetArray = new byte[preinsertArray.Length + sourceArray.Length];
            //preinsertArray.CopyTo(targetArray, 0);
            //sourceArray.CopyTo(targetArray, preinsertArray.Length);

            //byte[] dataArray = new byte[] 
            //{
            //    // last 2 bytes of Apple's iBeacon
            //    0x02, 0x15,
            //    // UUID e2 c5 6d b5 df fb 48 d2 b0 60 d0 f5 a7 10 96 e0
            //    0xe2, 0xc5, 0x6d, 0xb5,
            //    0xdf, 0xfb, 0x48, 0xd2,
            //    0xb0, 0x60, 0xd0, 0xf5,
            //    0xa7, 0x10, 0x96, 0xe0,
            //    // Major
            //    0x00, 0x00,
            //    // Minor
            //    0x00, 0x01,
            //    // TX power
            //    0xc5
            //};

            this.bluetoothLEManufacturerData.CompanyId = manufacturerId;
            this.bluetoothLEManufacturerData.Data = sourceArray.AsBuffer();
        }
        #endregion
    }
}
