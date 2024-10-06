using BluetoothEnDeCrypt.CaDA;
using BrickController2.PlatformServices.BluetoothLE;
using System;
using System.Collections.Generic;

namespace BrickController2.DeviceManagement
{
    /// <summary>
    /// CaDA RaceCar
    /// </summary>
    internal class CaDARaceCar : BluetoothAdvertisingDevice
    {
        #region Constants
        /// <summary>
        /// ManufacturerID for BLEAdvertisments
        /// hex: 0xC200
        /// dec: 49664
        /// </summary>
        public const ushort ManufacturerID = 0xC200;
        #endregion

        #region Fields
        /// <summary>
        /// 3 bytes device address
        /// </summary>
        public int deviceAddress = 0;

        /// <summary>
        /// 3 bytes device address
        /// </summary>
        public int mobileSerialChecksum = 0;

        /// <summary>
        /// verticalValue
        /// </summary>
        private float _Channel0_Value = 0.0f;

        /// <summary>
        /// horizontalValue
        /// </summary>
        private float _Channel1_Value = 0.0f;

        /// <summary>
        /// lightValue
        /// </summary>
        private float _Channel2_Value = 0.0f;

        /// <summary>
        /// 
        /// </summary>
        public readonly byte[] controlDataArray = new byte[] // 16
        {
            0x75, //  [0] const 0x75 (117)
            0x13, //  [1] 0x13 (19) STATUS_CONTROL
            0x00, //  [2] LastAddress
            0x00, //  [3] LastAddress
            0x00, //  [4] LastAddress
            0x00, //  [5] MobileSerialChecksum
            0x00, //  [6] MobileSerialChecksum
            0x00, //  [7] MobileSerialChecksum
            0x00, //  [8] ChannelData random
            0x00, //  [9] ChannelData random
            0x80, // [10] ChannelData verticalValue (min= 0x80 (128))
            0x80, // [11] ChannelData horizontalValue (min= 0x80 (128))
            0x00, // [12] ChannelData lightValue
            0x00, // [13] ChannelData 
            0x00, // [14] ChannelData 
            0x00, // [15] ChannelData 
        };
        #endregion
        #region Properties
        public override DeviceType DeviceType => DeviceType.CaDA_RaceCar;

        public override int NumberOfChannels => 3;

        public override string BatteryVoltageSign => "V";
        #endregion

        #region Constructor
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name"></param>
        /// <param name="address"></param>
        /// <param name="deviceData"></param>
        /// <param name="deviceRepository"></param>
        /// <param name="bleService"></param>
        public CaDARaceCar(string name, string address, byte[] deviceData, IDeviceRepository deviceRepository, IBluetoothLEService bleService)
                : base(name, address, deviceData, deviceRepository, bleService)
        {
            if (deviceData?.Length == 18)
            {
                this.deviceAddress =
                  (0x00 << 24) +
                  (deviceData[4] << 16) +
                  (deviceData[5] << 8) +
                  (deviceData[6] << 0);

                byte[] maskArray = CaDABLEUtils.CreateMaskArray(this.deviceAddress, 3);
                this.controlDataArray[2] = maskArray[0];
                this.controlDataArray[3] = maskArray[1];
                this.controlDataArray[4] = maskArray[2];

                this.mobileSerialChecksum =
                    (_bleService.DeviceID[0] << 0 ) +
                    (_bleService.DeviceID[1] << 8 ) +
                    (_bleService.DeviceID[2] << 16) ;

                byte[] mobileSerialChecksumMaskArray = CaDABLEUtils.CreateMaskArray(this.mobileSerialChecksum, 3);

                this.controlDataArray[5] = mobileSerialChecksumMaskArray[0];
                this.controlDataArray[6] = mobileSerialChecksumMaskArray[1];
                this.controlDataArray[7] = mobileSerialChecksumMaskArray[2];
            }
        }
        #endregion

        #region InitOutputTask()()
        /// <summary>
        /// This method sets the device to initial state before advertising starts
        /// </summary>
        protected override void InitOutputTask()
        {
        }
        #endregion

        #region SetOutput(int channel, float value)
        protected override bool SetChannel(int channel, float value)
        {
            switch (channel)
            {
                case 0:
                    this._Channel0_Value = value;
                    break;
                case 1:
                    this._Channel1_Value = value;
                    break;
                case 2:
                    this._Channel2_Value = value;
                    break;
                default:
                    break;
            }
            return true;
        }
        #endregion

        #region TryGetTelegram(out byte[] currentData)
        public override bool TryGetTelegram(out byte[] currentData)
        {
            int random = 0; // (int)(new Random().Next() * 100000.0d);

            byte[] channelDataArray = // 8
            {
                (byte)(random & 255),
                (byte)((random >> 8) & 255),
                (byte)Math.Max(0, Math.Min(0x80 - (this._Channel0_Value * 0x7F), 0xFF)),
                (byte)Math.Max(0, Math.Min(0x80 + (this._Channel1_Value * 0x7F), 0xFF)),
                (byte)Math.Max(0, Math.Min(0x80 + (this._Channel2_Value * 0x7F), 0xFF)),
                0,
                0,
                0
            };

            CaDABLEUtils.Encrypt(channelDataArray);

            Array.Copy(channelDataArray, 0, this.controlDataArray, 8, 8);

            this.controlDataArray[0] = 0x75; // 0x75 (117)
            this.controlDataArray[1] = 0x13; // 0x13 (19);

            // device address
            // this.controlDataArray[2]
            // this.controlDataArray[3]
            // this.controlDataArray[4]

            // mobile serial
            // this.controlDataArray[5]
            // this.controlDataArray[6]
            // this.controlDataArray[7]

            CaDABLEUtils.Get_rf_payload(CaDABLEUtils.AddressArray, this.controlDataArray, CaDABLEUtils.CTXValue, out currentData);

            return true;
        }
        #endregion

        #region static void AddAdvertisingData(IBluetoothLEService _bleService, List<Tuple<ushort, byte[]>> advertiseList)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="_bleService"></param>
        /// <param name="advertiseList"></param>
        public static void AddAdvertisingData(IBluetoothLEService _bleService, List<Tuple<ushort, byte[]>> advertiseList)
        {
            int mobileSerialChecksum =
                (_bleService.DeviceID[0] << 0 ) +
                (_bleService.DeviceID[1] << 8 ) +
                (_bleService.DeviceID[2] << 16) ;

            byte[] mobileSerialChecksumMaskArray = CaDABLEUtils.CreateMaskArray(mobileSerialChecksum, 3);

            byte[] pairingDataArray = new byte[] // 16
            {
              0x75, //  [0] const 0x75 (117)
              0x10, //  [1] 0x17 (23) STATUS_UNPAIRING - else - 0x10 (16)
              0x00, //  [2] LastAddress
              0x00, //  [3] LastAddress
              0x00, //  [4] LastAddress
              mobileSerialChecksumMaskArray[0], //  [5] MobileSerialChecksum
              mobileSerialChecksumMaskArray[1], //  [6] MobileSerialChecksum
              mobileSerialChecksumMaskArray[2], //  [7] MobileSerialChecksum
              0x00, //  [8] 
              0x00, //  [9] 
              0x80, // [10] min 128
              0x80, // [11] min 128
              0x00, // [12] 
              0x00, // [13] 
              0x00, // [14] 
              0x00, // [15] 
            };

            byte[] rf_payload_Array;
            CaDABLEUtils.Get_rf_payload(CaDABLEUtils.AddressArray, pairingDataArray, CaDABLEUtils.CTXValue, out rf_payload_Array);

            advertiseList.Add(new Tuple<ushort, byte[]>(ManufacturerID, rf_payload_Array));
        }
        #endregion
    }
}
