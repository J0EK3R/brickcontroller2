using BrickController2.PlatformServices.BluetoothLE;

namespace BrickController2.DeviceManagement
{
    /// <summary>
    /// Mould King 15059 Roboter
    /// 
    /// https://www.amazon.de/dp/B09TPHJ5L7?psc=1&ref=ppx_yo2ov_dt_b_product_details
    /// </summary>
    internal class MouldKing_15059 : MouldKing<MouldKing_15059.Telegram>
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
        #region Telegram_Connect
        /// <summary>
        /// Length: 8 - 0xAA, 0x7B, 0xA7, 0x00, 0x00, 0x00, 0x00, 0x55,
        /// </summary>
        //private static readonly byte[] Telegram_Connect = new byte[] { 0x6d, 0xb6, 0x43, 0xcf, 0x7e, 0x8f, 0x47, 0x11,
        //    0x4f, 0x66, 0x59,
        //    0xb8, 0x51,
        //    0xfa, 0x2a, 0xe1,
        //    0xfc, 0xf9,
        //    0x13, 0x14, 0x15, 0x16, 0x17, 0x18 };
        private static readonly byte[] Telegram_Connect = new byte[] { 0xAA, 0x7B, 0xA7, 0x00, 0x00, 0x00, 0x00, 0x55 };
        #endregion
        #region Telegram_Stopp
        /// <summary>
        /// Length: 8 - 0x66, 0x7B, 0xA7, 0x80, 0x80, 0x80, 0x80, 0x99, 
        /// </summary>
        //private static readonly byte[] Telegram_Stopp = new byte[] { 0x6D, 0xB6, 0x43, 0xCF, 0x7E, 0x8F, 0x47, 0x11,
        //    0x83, 0x66, 0x59,
        //    0x38, 0xD1,
        //    0x7A, 0xAA, 0x2D,
        //    0xF6, 0x47,
        //    0x13, 0x14, 0x15, 0x16, 0x17, 0x18 };
        private static readonly byte[] Telegram_Stopp = new byte[] { 0x66, 0x7B, 0xA7, 0x80, 0x80, 0x80, 0x80, 0x99 };
        #endregion
        #region Telegram_C0_F1__C1_F1
        /// <summary>
        /// Length: 8 - 0x66, 0x7B, 0xA7, 0x80, 0xFF, 0x80, 0x80, 0x99,
        /// </summary>
        //private static readonly byte[] Telegram_C0_F1__C1_F1 = new byte[] { 0x6D, 0xB6, 0x43, 0xCF, 0x7E, 0x8F, 0x47,
        //    0x11, 0x83, 0x66, 0x59,
        //    0x38, 0xAE,
        //    0x7A, 0xAA, 0x2D,
        //    0x4A, 0xAF,
        //    0x13, 0x14, 0x15, 0x16, 0x17, 0x18 };
        private static readonly byte[] Telegram_C0_F1__C1_F1 = new byte[] { 0x66, 0x7B, 0xA7, 0x80, 0xFF, 0x80, 0x80, 0x99 };
        #endregion
        #region Telegram_C0_B1__C1_B1
        /// <summary>
        /// Length: 8 - 0x66, 0x7B, 0xA7, 0x80, 0x00, 0x80, 0x80, 0x99,
        /// </summary>
        //private static readonly byte[] Telegram_C0_B1__C1_B1 = new byte[] { 0x6D, 0xB6, 0x43, 0xCF, 0x7E, 0x8F, 0x47,
        //    0x11, 0x83, 0x66, 0x59,
        //    0x38, 0x51,
        //    0x7A, 0xAA, 0x2D,
        //    0x98, 0x6A,
        //    0x13, 0x14, 0x15, 0x16, 0x17, 0x18 };
        private static readonly byte[] Telegram_C0_B1__C1_B1 = new byte[] { 0x66, 0x7B, 0xA7, 0x80, 0x00, 0x80, 0x80, 0x99 };
        #endregion
        #region Telegram_C0_F1__C1_B1
        /// <summary>
        /// Length: 8 - 0x66, 0x7B, 0xA7, 0x00, 0x80, 0x80, 0x80, 0x99, 
        /// </summary>
        //private static readonly byte[] Telegram_C0_F1__C1_B1 = new byte[] { 0x6D, 0xB6, 0x43, 0xCF, 0x7E, 0x8F, 0x47,
        //    0x11, 0x83, 0x66, 0x59,
        //    0xB8, 0xD1,
        //    0x7A, 0xAA, 0x2D,
        //    0xA3, 0xCD,
        //    0x13, 0x14, 0x15, 0x16, 0x17, 0x18 };
        private static readonly byte[] Telegram_C0_F1__C1_B1 = new byte[] { 0x66, 0x7B, 0xA7, 0x00, 0x80, 0x80, 0x80, 0x99 };
        #endregion
        #region Telegram_C0_F1__C1_S
        /// <summary>
        /// Length: 8 - 0x66, 0x7B, 0xA7, 0x00, 0xFF, 0x80, 0x80, 0x99, 
        /// </summary>
        //private static readonly byte[] Telegram_C0_F1__C1_S = new byte[] { 0x6D, 0xB6, 0x43, 0xCF, 0x7E, 0x8F, 0x47,
        //    0x11, 0x83, 0x66, 0x59,
        //    0xB8, 0xAE,
        //    0x7A, 0xAA, 0x2D,
        //    0x1F, 0x25,
        //    0x13, 0x14, 0x15, 0x16, 0x17, 0x18 };
        private static readonly byte[] Telegram_C0_F1__C1_S = new byte[] { 0x66, 0x7B, 0xA7, 0x00, 0xFF, 0x80, 0x80, 0x99 };
        #endregion
        #region Telegram_C0_B1__C1_S
        /// <summary>
        /// Length: 8 - 0x66, 0x7B, 0xA7, 0x00, 0x00, 0x80, 0x80, 0x99, 
        /// </summary>
        //private static readonly byte[] Telegram_C0_B1__C1_S = new byte[] { 0x6D, 0xB6, 0x43, 0xCF, 0x7E, 0x8F, 0x47,
        //    0x11, 0x83, 0x66, 0x59,
        //    0xB8, 0x51,
        //    0x7A, 0xAA, 0x2D,
        //    0xCD, 0xE0,
        //    0x13, 0x14, 0x15, 0x16, 0x17, 0x18 };
        private static readonly byte[] Telegram_C0_B1__C1_S = new byte[] { 0x66, 0x7B, 0xA7, 0x00, 0x00, 0x80, 0x80, 0x99 };
        #endregion
        #region Telegram_C0_B1__C1_F1
        /// <summary>
        /// Length: 8 - 0x66, 0x7B, 0xA7, 0xFF, 0x80, 0x80, 0x80, 0x99, 
        /// </summary>
        //private static readonly byte[] Telegram_C0_B1__C1_F1 = new byte[] { 0x6D, 0xB6, 0x43, 0xCF, 0x7E, 0x8F, 0x47,
        //    0x11, 0x83, 0x66, 0x59,
        //    0x47, 0xD1,
        //    0x7A, 0xAA, 0x2D,
        //    0xF9, 0x38,
        //    0x13, 0x14, 0x15, 0x16, 0x17, 0x18 };
        private static readonly byte[] Telegram_C0_B1__C1_F1 = new byte[] { 0x66, 0x7B, 0xA7, 0xFF, 0x80, 0x80, 0x80, 0x99 };
        #endregion
        #region Telegram_C0_S__C1_F1
        /// <summary>
        /// Length: 8 - 0x66, 0x7B, 0xA7, 0xFF, 0xFF, 0x80, 0x80, 0x99,
        /// </summary>
        //private static readonly byte[] Telegram_C0_S__C1_F1 = new byte[] { 0x6D, 0xB6, 0x43, 0xCF, 0x7E, 0x8F, 0x47,
        //    0x11, 0x83, 0x66, 0x59,
        //    0x47, 0xAE,
        //    0x7A, 0xAA, 0x2D,
        //    0x45, 0xD0,
        //    0x13, 0x14, 0x15, 0x16, 0x17, 0x18 };
        private static readonly byte[] Telegram_C0_S__C1_F1 = new byte[] { 0x66, 0x7B, 0xA7, 0xFF, 0xFF, 0x80, 0x80, 0x99 };
        #endregion
        #region Telegram_C0_S__C1_B1
        /// <summary>
        /// Length: 8 - 0x66, 0x7B, 0xA7, 0xFF, 0x00, 0x80, 0x80, 0x99, 
        /// </summary>
        //private static readonly byte[] Telegram_C0_S__C1_B1 = new byte[] { 0x6D, 0xB6, 0x43, 0xCF, 0x7E, 0x8F, 0x47,
        //    0x11, 0x83, 0x66, 0x59,
        //    0x47, 0x51,
        //    0x7A, 0xAA, 0x2D,
        //    0x97, 0x15,
        //    0x13, 0x14, 0x15, 0x16, 0x17, 0x18 };
        private static readonly byte[] Telegram_C0_S__C1_B1 = new byte[] { 0x66, 0x7B, 0xA7, 0xFF, 0x00, 0x80, 0x80, 0x99 };
        #endregion
        #endregion
        #region static Constructor
        static MouldKing_15059()
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
        public override DeviceType DeviceType => DeviceType.MouldKing_15059;

        public override int NumberOfChannels => 2;

        public override string BatteryVoltageSign => string.Empty;
        #endregion

        #region Constructor
        public MouldKing_15059(string name, string address, byte[] deviceData, IDeviceRepository deviceRepository, IBluetoothLEService bleService)
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
