using BrickController2.PlatformServices.BluetoothLE;
using System;

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

        private static readonly byte[] magicNumberArray_0x43_0x41_0x52 =
        {
            67, // 0x43
            65, // 0x41
            82, // 0x52
        };
        #endregion
        #region static Fields
        public static readonly int MobileSerial =
          (0x00 << 24) +
          (0x00 << 16) +
          (0x00 << 8) +
          (0x00 << 0);
        #endregion

        #region CaDARaceCar()
        /// <summary>
        /// class constuctor
        /// </summary>
        static CaDARaceCar()
        {
            CaDARaceCar.MobileSerial = CaDARaceCar.CreateMobileSerial();
        }
        #endregion
        #region static int CreateMobileSerial()
        /// <summary>
        /// creates an unique serial number
        /// </summary>
        /// <returns>unique serial number</returns>
        private static int CreateMobileSerial()
        {
            return
              (0x00 << 24) +
              (0x00 << 16) +
              (0x00 << 8) +
              (0x00 << 0);
        }
        #endregion

        #region Fields
        /// <summary>
        /// 3 bytes device address
        /// </summary>
        public int deviceAddress = 0;

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

        public readonly byte[] pairingDataArray = new byte[] // 16
        {
            0x75, //  [0] const 0x75 (117)
            0x10, //  [1] 0x17 (23) STATUS_UNPAIRING - else - 0x10 (16)
            0x00, //  [2] LastAddress
            0x00, //  [3] LastAddress
            0x00, //  [4] LastAddress
            0x00, //  [5] MobileSerialChecksum
            0x00, //  [6] MobileSerialChecksum
            0x00, //  [7] MobileSerialChecksum
            0x00, //  [8] 
            0x00, //  [9] 
            0x80, // [10] min 128
            0x80, // [11] min 128
            0x00, // [12] 
            0x00, // [13] 
            0x00, // [14] 
            0x00, // [15] 
        };
        #endregion
        #region Properties
        public override DeviceType DeviceType => DeviceType.CaDA_RaceCar;

        public override int NumberOfChannels => 2;

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
            if(deviceData?.Length == 18)
            {
                this.deviceAddress =
                  (0x00 << 24) +
                  (deviceData[4] << 16) +
                  (deviceData[5] << 8) +
                  (deviceData[6] << 0);

                byte[] maskArray = ArrayTools.CreateMaskArray(this.deviceAddress, 3);
                Array.Copy(maskArray, 0, this.controlDataArray, 2, 3);
                Array.Copy(maskArray, 0, this.pairingDataArray, 2, 3);

                this.controlDataArray[5] = (byte)((CaDARaceCar.MobileSerial >> 0) & 0xFF);
                this.controlDataArray[6] = (byte)((CaDARaceCar.MobileSerial >> 8) & 0xFF);
                this.controlDataArray[7] = (byte)((CaDARaceCar.MobileSerial >> 16) & 0xFF);
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
                (byte)(0x80 - Math.Min(-this._Channel0_Value * 0x80, 0x80)),
                (byte)(0x80 - Math.Min(-this._Channel1_Value * 0x80, 0x80)),
                0, //(byte)(0x80 - Math.Min(-this._Channel2_Value * 0x80, 0x80)),
                0,
                0,
                0
            };

            BLEUtils.encry(channelDataArray);

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

            BLEUtils.get_rf_payload(CaDARaceCar.magicNumberArray_0x43_0x41_0x52, this.controlDataArray, out currentData);

            return true;
        }
        #endregion

    }
}
