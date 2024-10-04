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
        #region Fields

        private static readonly byte[] Telegram_Connect = new byte[] {
            0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x41, 0xfa, 0x2a, 0xb4, 0x9e, 0xfd, 0xc7, 0xb6, 0x2e,
            0xa6,
            0x82,
            0xc9, 0xf2, 0x0e,
            0x7f,
            0xcf, 0x2e,
        };

        // Stop
        private static readonly byte[] Telegram_Stop = new byte[] {
            0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x42, 0x6c, 0x3a, 0xbc, 0x9e, 0xfd, 0xc7, 0x42, 0xda,
            0x07, // stop
            0x23, // straight
            0x00, 0x3b, 0xc7,
            0xb6,
            0xa3, 0x22,
        };

        // stop - left
        private static readonly byte[] Telegram_C0_S__C1_B1 = new byte[] {
            0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x42, 0x6c, 0x3a, 0xbc, 0x9e, 0xfd, 0xc7, 0x42, 0xda,
            0x07, // stop
            0xcf, // left
            0x00, 0x3b, 0xc7,
            0xb6,
            0x75, 0x7e,
        };

        // stop - right
        private static readonly byte[] Telegram_C0_S__C1_F1 = new byte[] {
            0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x42, 0x6c, 0x3a, 0xbc, 0x9e, 0xfd, 0xc7, 0x42, 0xda,
            0x07, // stop
            0x2c, // right
            0x00, 0x3b, 0xc7,
            0xb6,
            0x5f, 0x48,
        };

        // forward
        private static readonly byte[] Telegram_C0_F1__C1_S = new byte[] {
            0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x42, 0x6c, 0x3a, 0xbc, 0x9e, 0xfd, 0xc7, 0x42, 0xda,
            0xeb, // fw
            0x23, // straight
            0x00, 0x3b, 0xc7,
            0xb6,
            0x44, 0x91,
        };

        // forward - left
        private static readonly byte[] Telegram_C0_F1__C1_B1 = new byte[] {
            0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x42, 0x6c, 0x3a, 0xbc, 0x9e, 0xfd, 0xc7, 0x42, 0xda,
            0xeb, // fw
            0xcf, // left
            0x00, 0x3b, 0xc7,
            0xb6,
            0x92, 0xcd,
        };

        // forward - right
        private static readonly byte[] Telegram_C0_F1__C1_F1 = new byte[] {
            0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x42, 0x6c, 0x3a, 0xbc, 0x9e, 0xfd, 0xc7, 0x42, 0xda,
            0xeb, // fw
            0x2c, // right
            0x00, 0x3b, 0xc7,
            0xb6,
            0xb8, 0xfb,
        };

        // backward
        private static readonly byte[] Telegram_C0_B1__C1_S = new byte[] {
            0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x42, 0x6c, 0x3a, 0xbc, 0x9e, 0xfd, 0xc7, 0x42, 0xda,
            0x08, // bw
            0x23, // straight
            0x00, 0x3b, 0xc7,
            0xb6,
            0x2a, 0x1f,
        };

        // backward - left
        private static readonly byte[] Telegram_C0_B1__C1_B1 = new byte[] {
            0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x42, 0x6c, 0x3a, 0xbc, 0x9e, 0xfd, 0xc7, 0x42, 0xda,
            0x08, // bw
            0xcf, // left
            0x00, 0x3b, 0xc7,
            0xb6,
            0xfc, 0x43,
        };

        // backward - right
        private static readonly byte[] Telegram_C0_B1__C1_F1 = new byte[] {
            0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x42, 0x6c, 0x3a, 0xbc, 0x9e, 0xfd, 0xc7, 0x42, 0xda,
            0x08, // bw
            0x7a, // right ???
            0x00, 0x3b, 0xc7,
            0xb6,
            0x2c, 0x3b,
        };
        #region telegrams
        // speed 3 - forward - light on
        //0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x42, 0x6c, 0x3a, 0xbc, 0x9e, 0xfd, 0xc7, 0x42, 0xda,
        //0x3c, 
        //0x23, 
        //0xd3,
        //0x3b, 0xc7, 
        //0xb6, 0xe3, 0x15,

        // speed 3 - forward - light off
        //0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x42, 0x6c, 0x3a, 0xbc, 0x9e, 0xfd, 0xc7, 0x42, 0xda,
        //0x3c, 
        //0x23, 
        //0x00,
        //0x3b, 0xc7, 
        //0xb6,
        //0x56, 0xc8,

        // speed 2 - forward - light off
        //0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x42, 0x6c, 0x3a, 0xbc, 0x9e, 0xfd, 0xc7, 0x42, 0xda,
        //0x08, 
        //0x23, 
        //0x00,
        //0x3b, 0xc7, 
        //0xb6,
        //0x2a, 0x1f,

        // speed 2 - forward - light on
        //0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x42, 0x6c, 0x3a, 0xbc, 0x9e, 0xfd, 0xc7, 0x42, 0xda,
        //0x07, 
        //0x23, 
        //0x00,
        //0x3b, 0xc7, 
        //0xb6,
        //0xa3, 0x22,

        // speed 1 - forward - light off
        //0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x42, 0x6c, 0x3a, 0xbc, 0x9e, 0xfd, 0xc7, 0x42, 0xda,
        //0x07, 
        //0x23, 
        //0xd3,
        //0x3b, 0xc7, 
        //0xb6, 0x16, 0xff,

        // speed 1 - forward - light on
        //0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x42, 0x6c, 0x3a, 0xbc, 0x9e, 0xfd, 0xc7, 0x42, 0xda,
        //0x07,
        //0x23,
        //0x00,
        //0x3b, 0xc7,
        //0xb6,
        //0xa3, 0x22,
        #endregion
        #endregion

        #region Fields
        public int lastAddress =
          (0x00 << 24) +
          (0x96 << 16) +
          (0x10 << 8) + 
          (0x08 << 0);

        public int mobileSerial =
          (0x00 << 24) +
          (0xcb << 16) +
          (0x29 << 8) +
          (0x79 << 0);

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

            byte[] maskArray = ArrayTools.CreateMaskArray(this.lastAddress, 3);
            Array.Copy(maskArray, 0, this.controlDataArray, 2, 3);
            Array.Copy(maskArray, 0, this.pairingDataArray, 2, 3);

            this.controlDataArray[5] = (byte)((this.mobileSerial >> 0) & 0xFF);
            this.controlDataArray[6] = (byte)((this.mobileSerial >> 8) & 0xFF);
            this.controlDataArray[7] = (byte)((this.mobileSerial >> 16) & 0xFF);

            BLEUtils.get_rf_payload(CaDARaceCar.magicNumberArray_0x43_0x41_0x52, this.controlDataArray, out currentData);

            return true;
        }
        #endregion

    }
}
