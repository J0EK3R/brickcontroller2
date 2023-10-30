using BrickController2.PlatformServices.BluetoothLE;

namespace BrickController2.DeviceManagement
{
    /// <summary>
    /// Mould King 6.0 Modul
    /// </summary>
    internal class MouldKing_6_0_Modul : MouldKing<MouldKing_6_0_Modul.Telegram>
    {
        #region Definitions
        internal enum Telegram
        {
            Connect,
            Stopp,

            CA_F1__CB_F1,
            CA_F1__CB_S,
            CA_F1__CB_B1,

            CA_S__CB_B1,
            CA_S__CB_F1,

            CA_B1__CB_F1,
            CA_B1__CB_S,
            CA_B1__CB_B1,
        }
        #endregion
        #region Constants
        #region Telegram_Connect
        /// <summary>
        /// </summary>
        private static readonly byte[] Telegram_Connect = new byte[] { 0x61, 0x7B, 0xA7, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x9E };
        #endregion
        #region Telegram_Stopp
        /// <summary>
        /// </summary>
        private static readonly byte[] Telegram_Stopp = new byte[] { 0x61, 0x7B, 0xA7, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x9E };
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
        private static readonly byte[] Telegram_CA_F1__CB_F1 = new byte[] { 0x66, 0x7B, 0xA7, 0x80, 0xFF, 0x80, 0x80, 0x99 };
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
        private static readonly byte[] Telegram_CA_B1__CB_B1 = new byte[] { 0x66, 0x7B, 0xA7, 0x80, 0x00, 0x80, 0x80, 0x99 };
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
        private static readonly byte[] Telegram_CA_F1__CB_B1 = new byte[] { 0x66, 0x7B, 0xA7, 0x00, 0x80, 0x80, 0x80, 0x99 };
        #endregion
        #region Telegram_CA_F1__CB_S
        /// <summary>
        /// Channel A Forward max
        /// </summary>
        private static readonly byte[] Telegram_CA_F1__CB_S = new byte[] { 0x61, 0x7B, 0xA7, 0x01, 0x80, 0x80, 0x80, 0x80, 0x80, 0x9E, };
        #endregion
        #region Telegram_CA_B1__CB_S
        /// <summary>
        /// Channel A Backward max
        /// </summary>
        private static readonly byte[] Telegram_CA_B1__CB_S = new byte[] { 0x61, 0x7B, 0xA7, 0xFF, 0x80, 0x80, 0x80, 0x80, 0x80, 0x9E, };
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
        private static readonly byte[] Telegram_C0AB1__CB_F1 = new byte[] { 0x66, 0x7B, 0xA7, 0xFF, 0x80, 0x80, 0x80, 0x99 };
        #endregion
        #region Telegram_CA_S__CB_F1
        /// <summary>
        /// Channel B Forward max
        /// </summary>
        private static readonly byte[] Telegram_CA_S__CB_F1 = new byte[] { 0x61, 0x7B, 0xA7, 0x80, 0x01, 0x80, 0x80, 0x80, 0x80, 0x9E, };
        #endregion
        #region Telegram_CA_S__CB_B1
        /// <summary>
        /// Channel B Backward max
        /// </summary>
        private static readonly byte[] Telegram_CA_S__CB_B1 = new byte[] { 0x61, 0x7B, 0xA7, 0x80, 0xFF, 0x80, 0x80, 0x80, 0x80, 0x9E, };
        #endregion
        #endregion
        #region static Constructor
        static MouldKing_6_0_Modul()
        {
            Telegrams.Add(Telegram.Connect, Telegram_Connect);
            Telegrams.Add(Telegram.Stopp, Telegram_Stopp);
            Telegrams.Add(Telegram.CA_F1__CB_F1, Telegram_CA_F1__CB_F1);
            Telegrams.Add(Telegram.CA_F1__CB_S, Telegram_CA_F1__CB_S);
            Telegrams.Add(Telegram.CA_F1__CB_B1, Telegram_CA_F1__CB_B1);
            Telegrams.Add(Telegram.CA_S__CB_B1, Telegram_CA_S__CB_B1);

            Telegrams.Add(Telegram.CA_S__CB_F1, Telegram_CA_S__CB_F1);
            Telegrams.Add(Telegram.CA_B1__CB_F1, Telegram_C0AB1__CB_F1);
            Telegrams.Add(Telegram.CA_B1__CB_S, Telegram_CA_B1__CB_S);
            Telegrams.Add(Telegram.CA_B1__CB_B1, Telegram_CA_B1__CB_B1);
        }
        #endregion

        #region Fields
        private float _Channel_A_Value = 0.0f;
        private float _Channel_B_Value = 0.0f;
        #endregion
        #region Properties
        public override DeviceType DeviceType => DeviceType.MouldKing_6_0_Modul;

        public override int NumberOfChannels => 6;

        public override string BatteryVoltageSign => string.Empty;
        #endregion

        #region Constructor
        public MouldKing_6_0_Modul(string name, string address, byte[] deviceData, IDeviceRepository deviceRepository, IBluetoothLEService bleService)
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
                    if (this._Channel_A_Value == value)
                    {
                        return;
                    }
                    else
                    {
                        this._Channel_A_Value = value;
                    }
                    break;
                case 1:
                    if (this._Channel_B_Value == value)
                    {
                        return;
                    }
                    else
                    {
                        this._Channel_B_Value = value;
                    }
                    break;
                default:
                    return;
            }

            lock (_outputLock)
            {
                if (this._Channel_A_Value == 0)
                {
                    if (this._Channel_B_Value == 0) // Stopp
                    {
                        _currentTelegram = Telegram.Stopp;
                    }
                    else if (this._Channel_B_Value > 0) // C0_S C1_F1
                    {
                        _currentTelegram = Telegram.CA_S__CB_F1;
                    }
                    else /*if (this._Channel1_Value < 0)*/ // C0_S C1_B1
                    {
                        _currentTelegram = Telegram.CA_S__CB_B1;
                    }
                }
                else if (this._Channel_A_Value > 0)
                {
                    if (this._Channel_B_Value == 0) // C0_F1 C1_S
                    {
                        _currentTelegram = Telegram.CA_F1__CB_S;
                    }
                    else if (this._Channel_B_Value > 0) // C0_F1 C1_F1
                    {
                        _currentTelegram = Telegram.CA_F1__CB_F1;
                    }
                    else /*if (this._Channel1_Value < 0)*/ // C0_F1 C1_B1
                    {
                        _currentTelegram = Telegram.CA_F1__CB_B1;
                    }
                }
                else if (this._Channel_A_Value < 0)
                {
                    if (this._Channel_B_Value == 0) // C0_B1 C1_S
                    {
                        _currentTelegram = Telegram.CA_B1__CB_S;
                    }
                    else if (this._Channel_B_Value > 0) // C0_B1 C1_F1
                    {
                        _currentTelegram = Telegram.CA_B1__CB_F1;
                    }
                    else /*if (this._Channel1_Value < 0)*/ // C0_B1 C1_B1
                    {
                        _currentTelegram = Telegram.CA_B1__CB_B1;
                    }
                }
            }
        }
        #endregion
    }
}
