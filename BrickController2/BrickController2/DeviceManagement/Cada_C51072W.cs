using BrickController2.PlatformServices.BluetoothLE;

namespace BrickController2.DeviceManagement
{
    /// <summary>
    /// Cada C51072W Race Car
    /// 
    /// https://www.amazon.de/gp/product/B0B6P9JG2J/ref=ppx_yo_dt_b_search_asin_title?ie=UTF8&psc=1
    /// </summary>
    internal class Cada_C51072W : BluetoothAdvertisingDeviceEnum<Cada_C51072W.Telegram>
    {
        #region Definitions
        internal enum Telegram
        {
            Connect,
            Stopp,

            /// <summary>
            /// forward - right
            /// </summary>
            C0_F1__C1_F1,
            /// <summary>
            /// forward
            /// </summary>
            C0_F1__C1_S,
            /// <summary>
            /// forward - left
            /// </summary>
            C0_F1__C1_B1,

            /// <summary>
            /// stop - left
            /// </summary>
            C0_S__C1_B1,
            /// <summary>
            /// stop - right
            /// </summary>
            C0_S__C1_F1,

            /// <summary>
            /// backward - right
            /// </summary>
            C0_B1__C1_F1,

            /// <summary>
            /// backward
            /// </summary>
            C0_B1__C1_S,

            /// <summary>
            /// backward - left
            /// </summary>
            C0_B1__C1_B1,
        }
        #endregion
        #region Constants
        /// <summary>
        /// ManufacturerID for BLEAdvertisments
        /// hex: 0xC200
        /// dec: 49664
        /// </summary>
        public const ushort ManufacturerID = 0xC200;

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
        #region static Constructor
        static Cada_C51072W()
        {
            Telegrams.Add(Telegram.Connect, Telegram_Connect);
            Telegrams.Add(Telegram.Stopp, Telegram_Stop);
            Telegrams.Add(Telegram.C0_F1__C1_F1, Telegram_C0_F1__C1_F1);
            Telegrams.Add(Telegram.C0_F1__C1_S, Telegram_C0_F1__C1_S);
            Telegrams.Add(Telegram.C0_F1__C1_B1, Telegram_C0_F1__C1_B1);
            Telegrams.Add(Telegram.C0_S__C1_B1, Telegram_C0_S__C1_B1);
            Telegrams.Add(Telegram.C0_S__C1_F1, Telegram_C0_S__C1_F1);
            Telegrams.Add(Telegram.C0_B1__C1_F1, Telegram_C0_B1__C1_F1);
            Telegrams.Add(Telegram.C0_B1__C1_S, Telegram_C0_B1__C1_S);
            Telegrams.Add(Telegram.C0_B1__C1_B1, Telegram_C0_B1__C1_B1);
        }
        #endregion

        #region Fields
        private float _Channel0_Value = 0.0f;
        private float _Channel1_Value = 0.0f;
        #endregion
        #region Properties
        public override DeviceType DeviceType => DeviceType.Cada_C51072W;

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
        public Cada_C51072W(string name, string address, byte[] deviceData, IDeviceRepository deviceRepository, IBluetoothLEService bleService)
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
            this._currentTelegram = Telegram.Connect;
        }
        #endregion

        #region SetOutput(int channel, float value)
        protected override bool SetChannel(int channel, float value)
        {
            switch (channel)
            {
                case 0:
                    if (this._Channel0_Value == value)
                    {
                        return false;
                    }
                    else
                    {
                        this._Channel0_Value = value;
                    }
                    break;
                case 1:
                    if (this._Channel1_Value == value)
                    {
                        return false;
                    }
                    else
                    {
                        this._Channel1_Value = value;
                    }
                    break;
                default:
                    return false;
            }

            lock (this._outputLock)
            {
                if (this._Channel0_Value == 0)
                {
                    if (this._Channel1_Value == 0) // Stopp
                    {
                        _currentTelegram = Telegram.Stopp;
                    }
                    else if (this._Channel1_Value > 0) // C0_S C1_F1
                    {
                        _currentTelegram = Telegram.C0_S__C1_F1;
                    }
                    else /*if (this._Channel1_Value < 0)*/ // C0_S C1_B1
                    {
                        _currentTelegram = Telegram.C0_S__C1_B1;
                    }
                }
                else if (this._Channel0_Value > 0)
                {
                    if (this._Channel1_Value == 0) // C0_F1 C1_S
                    {
                        _currentTelegram = Telegram.C0_F1__C1_S;
                    }
                    else if (this._Channel1_Value > 0) // C0_F1 C1_F1
                    {
                        _currentTelegram = Telegram.C0_F1__C1_F1;
                    }
                    else /*if (this._Channel1_Value < 0)*/ // C0_F1 C1_B1
                    {
                        _currentTelegram = Telegram.C0_F1__C1_B1;
                    }
                }
                else if (this._Channel0_Value < 0)
                {
                    if (this._Channel1_Value == 0) // C0_B1 C1_S
                    {
                        _currentTelegram = Telegram.C0_B1__C1_S;
                    }
                    else if (this._Channel1_Value > 0) // C0_B1 C1_F1
                    {
                        _currentTelegram = Telegram.C0_B1__C1_F1;
                    }
                    else /*if (this._Channel1_Value < 0)*/ // C0_B1 C1_B1
                    {
                        _currentTelegram = Telegram.C0_B1__C1_B1;
                    }
                }
            }
            return true;
        }
        #endregion
    }
}
