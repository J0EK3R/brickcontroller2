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
        private static readonly byte[] Telegram_Connect = new byte[] { 0x6d, 0xb6, 0x43, 0xcf, 0x7e, 0x8f, 0x47, 0x11,
            0x4f, 0x66, 0x59,
            0xb8, 0x51,
            0xfa, 0x2a, 0xe1,
            0xfc, 0xf9,
            0x13, 0x14, 0x15, 0x16, 0x17, 0x18 };

        private static readonly byte[] Telegram_Stopp = new byte[] { 0x6D, 0xB6, 0x43, 0xCF, 0x7E, 0x8F, 0x47, 0x11,
            0x83, 0x66, 0x59,
            0x38, 0xD1,
            0x7A, 0xAA, 0x2D,
            0xF6, 0x47,
            0x13, 0x14, 0x15, 0x16, 0x17, 0x18 };

        private static readonly byte[] Telegram_C0_F1__C1_F1 = new byte[] { 0x6D, 0xB6, 0x43, 0xCF, 0x7E, 0x8F, 0x47,
            0x11, 0x83, 0x66, 0x59,
            0x38, 0xAE,
            0x7A, 0xAA, 0x2D,
            0x4A, 0xAF,
            0x13, 0x14, 0x15, 0x16, 0x17, 0x18 };

        private static readonly byte[] Telegram_C0_B1__C1_B1 = new byte[] { 0x6D, 0xB6, 0x43, 0xCF, 0x7E, 0x8F, 0x47,
            0x11, 0x83, 0x66, 0x59,
            0x38, 0x51,
            0x7A, 0xAA, 0x2D,
            0x98, 0x6A,
            0x13, 0x14, 0x15, 0x16, 0x17, 0x18 };

        private static readonly byte[] Telegram_C0_F1__C1_B1 = new byte[] { 0x6D, 0xB6, 0x43, 0xCF, 0x7E, 0x8F, 0x47,
            0x11, 0x83, 0x66, 0x59,
            0xB8, 0xD1,
            0x7A, 0xAA, 0x2D,
            0xA3, 0xCD,
            0x13, 0x14, 0x15, 0x16, 0x17, 0x18 };

        private static readonly byte[] Telegram_C0_F1__C1_S = new byte[] { 0x6D, 0xB6, 0x43, 0xCF, 0x7E, 0x8F, 0x47,
            0x11, 0x83, 0x66, 0x59,
            0xB8, 0xAE,
            0x7A, 0xAA, 0x2D,
            0x1F, 0x25,
            0x13, 0x14, 0x15, 0x16, 0x17, 0x18 };

        private static readonly byte[] Telegram_C0_B1__C1_S = new byte[] { 0x6D, 0xB6, 0x43, 0xCF, 0x7E, 0x8F, 0x47,
            0x11, 0x83, 0x66, 0x59,
            0xB8, 0x51,
            0x7A, 0xAA, 0x2D,
            0xCD, 0xE0,
            0x13, 0x14, 0x15, 0x16, 0x17, 0x18 };

        private static readonly byte[] Telegram_C0_B1__C1_F1 = new byte[] { 0x6D, 0xB6, 0x43, 0xCF, 0x7E, 0x8F, 0x47,
            0x11, 0x83, 0x66, 0x59,
            0x47, 0xD1,
            0x7A, 0xAA, 0x2D,
            0xF9, 0x38,
            0x13, 0x14, 0x15, 0x16, 0x17, 0x18 };

        // right
        private static readonly byte[] Telegram_C0_S__C1_F1 = new byte[] {
            0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x42, 0x6c, 0x3a, 0xbc, 0x9e, 0xfd, 0xc7, 0x42, 0xda, 0x07, 
            0x2c, 
            0x00, 0x3b, 0xc7, 
            0xb6, 0x5f, 0x48 };

        // left
        private static readonly byte[] Telegram_C0_S__C1_B1 = new byte[] {
            0xee, 0x1b, 0xc8, 0xaf, 0x9f, 0x3c, 0xcd, 0x42, 0x6c, 0x3a, 0xbc, 0x9e, 0xfd, 0xc7, 0x42, 0xda, 0x07, 
            0xcf, 
            0x00, 0x3b, 0xc7, 
            0xb6, 0x75, 0x7e };
        #endregion
        #region static Constructor
        static Cada_RC_C51072W()
        {
            Telegrams.Add(Telegram.Connect, Telegram_Connect);
            Telegrams.Add(Telegram.Stopp, Telegram_Stopp);
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
