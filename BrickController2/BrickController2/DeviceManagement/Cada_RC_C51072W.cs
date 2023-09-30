using BrickController2.PlatformServices.BluetoothLE;

namespace BrickController2.DeviceManagement
{
    /// <summary>
    /// Cada C51072W Race Car
    /// 
    /// https://www.amazon.de/gp/product/B0B6P9JG2J/ref=ppx_yo_dt_b_search_asin_title?ie=UTF8&psc=1
    /// </summary>
    internal class Cada_RC_C51072W : BluetoothAdvertisingDevice<Cada_RC_C51072W.Telegram>
    {
        #region Definitions
        internal enum Telegram
        {
            Connect,
            Stopp,

            C0_F1__C1_F1,
            C0_F1__C1_S,
            C0_F1__C1_B1,

            C0_S__C1_B1,
            C0_S__C1_F1,

            C0_B1__C1_F1,
            C0_B1__C1_S,
            C0_B1__C1_B1,
        }
        #endregion
        #region Constants
        private static readonly byte[] Telegram_Connect = new byte[] {
            0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x41, 0xfa, 0x2a, 0xb4, 0x9e, 0xfd, 0xc7, 0xb6, 0x2e,
            0xa6, 
            0x82, 
            0xc9, 0xf2, 0x0e, 
            0x7f, 0xcf, 0x2e,
        };

        // Stop
        private static readonly byte[] Telegram_Stop = new byte[] {
            0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x42, 0x6c, 0x3a, 0xbc, 0x9e, 0xfd, 0xc7, 0x42, 0xda,
            0x07, 
            0x23, 
            0x00, 0x3b, 0xc7, 
            0xb6, 0xa3, 0x22,
        };

        private static readonly byte[] Telegram_C0_B1__C1_F1 = new byte[] {
            0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x42, 0x6c, 0x3a, 0xbc, 0x9e, 0xfd, 0xc7, 0x42, 0xda,
            0x08, 
            0x7a, 
            0x00, 0x3b, 0xc7, 
            0xb6, 0x2c, 0x3b,
        };

        private static readonly byte[] Telegram_C0_F1__C1_B1 = new byte[] {
            0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x42, 0x6c, 0x3a, 0xbc, 0x9e, 0xfd, 0xc7, 0x42, 0xda,
            0xeb, 
            0xcf, 
            0x00, 0x3b, 0xc7, 
            0xb6, 0x92, 0xcd,
        };

        private static readonly byte[] Telegram_C0_B1__C1_B1 = new byte[] {
            0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x42, 0x6c, 0x3a, 0xbc, 0x9e, 0xfd, 0xc7, 0x42, 0xda,
            0x08,
            0xcf,
            0x00, 0x3b, 0xc7,
            0xb6, 0xfc, 0x43,
        };

        // forward
        private static readonly byte[] Telegram_C0_B1__C1_S = new byte[] {
            0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x42, 0x6c, 0x3a, 0xbc, 0x9e, 0xfd, 0xc7, 0x42, 0xda,
            0x08, 
            0x23, 
            0x00, 0x3b, 0xc7, 
            0xb6, 0x2a, 0x1f,
        };

        private static readonly byte[] Telegram_C0_F1__C1_S = new byte[] {
            0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x42, 0x6c, 0x3a, 0xbc, 0x9e, 0xfd, 0xc7, 0x42, 0xda,
            0xeb, 
            0x23, 
            0x00, 0x3b, 0xc7, 
            0xb6, 0x44, 0x91,
        };

        private static readonly byte[] Telegram_C0_F1__C1_F1 = new byte[] {
            0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x42, 0x6c, 0x3a, 0xbc, 0x9e, 0xfd, 0xc7, 0x42, 0xda,
            0xeb, 
            0x2c, 
            0x00, 0x3b, 0xc7, 
            0xb6, 0xb8, 0xfb,
        };

        // right
        private static readonly byte[] Telegram_C0_S__C1_F1 = new byte[] {
            0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x42, 0x6c, 0x3a, 0xbc, 0x9e, 0xfd, 0xc7, 0x42, 0xda, 
            0x07, 
            0x2c, 
            0x00, 0x3b, 0xc7, 
            0xb6, 0x5f, 0x48,
        };

        // left
        private static readonly byte[] Telegram_C0_S__C1_B1 = new byte[] {
            0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x42, 0x6c, 0x3a, 0xbc, 0x9e, 0xfd, 0xc7, 0x42, 0xda, 
            0x07, 
            0xcf, 
            0x00, 0x3b, 0xc7, 
            0xb6, 0x75, 0x7e, 
        };
        #endregion
        #region static Constructor
        static Cada_RC_C51072W()
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
        public override DeviceType DeviceType => DeviceType.Cada_RC_C51072W;

        public override int NumberOfChannels => 2;

        public override string BatteryVoltageSign => "V";
        #endregion

        #region Constructor
        public Cada_RC_C51072W(string name, string address, byte[] deviceData, IDeviceRepository deviceRepository, IBluetoothLEService bleService)
                : base(name, address, deviceData, deviceRepository, bleService)
        {
        }
        #endregion

        #region InitFirstTelegram()
        protected override Telegram InitFirstTelegram()
        {
            return Telegram.Connect;
        }
        #endregion

        #region SetOutput(int channel, float value)
        public override void SetOutput(int channel, float value)
        {
            switch (channel)
            {
                case 0:
                    if (this._Channel0_Value == value)
                    {
                        return;
                    }
                    else
                    {
                        this._Channel0_Value = value;
                    }
                    break;
                case 1:
                    if (this._Channel1_Value == value)
                    {
                        return;
                    }
                    else
                    {
                        this._Channel1_Value = value;
                    }
                    break;
                default:
                    return;
            }

            lock (_outputLock)
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
        }
        #endregion
    }
}
