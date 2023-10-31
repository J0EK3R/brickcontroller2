using BrickController2.PlatformServices.BluetoothLE;
using System;
using System.Diagnostics;
using System.Linq;

namespace BrickController2.DeviceManagement
{
    /// <summary>
    /// Mould King 6.0 Modul
    /// </summary>
    internal class MouldKing_6_0_Modul : BluetoothAdvertisingDevice
    {
        #region Constants
        public const ushort ManufacturerID = 0xFFF0;

        #region Telegram_Connect
        /// <summary>
        /// </summary>
        private static readonly byte[] Telegram_Connect = new byte[] { 0x6D, 0x7B, 0xA7, 0x80, 0x80, 0x80, 0x80, 0x92, };
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

        #region Fields
        private readonly Stopwatch allZeroStopwatch = Stopwatch.StartNew();

        private float _Channel_A_Value = 0.0f;
        private float _Channel_B_Value = 0.0f;
        private float _Channel_C_Value = 0.0f;
        private float _Channel_D_Value = 0.0f;
        private float _Channel_E_Value = 0.0f;
        private float _Channel_F_Value = 0.0f;

        private bool isInitialized = false;
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

        #region InitOutputTask()()
        /// <summary>
        /// This method sets the device to initial state before advertising starts
        /// </summary>
        protected override void InitOutputTask()
        {
            this.isInitialized = false;
        }
        #endregion

        #region SetOutput(int channel, float value)
        protected override bool SetChannel(int channel, float value)
        {
            switch (channel)
            {
                #region Channel A
                case 0:
                    if (this._Channel_A_Value == value)
                    {
                        return false;
                    }
                    else
                    {
                        this._Channel_A_Value = value;
                    }
                    break;
                #endregion
                #region Channel B
                case 1:
                    if (this._Channel_B_Value == value)
                    {
                        return false;
                    }
                    else
                    {
                        this._Channel_B_Value = value;
                    }
                    break;
                #endregion
                #region Channel C
                case 2:
                    if (this._Channel_C_Value == value)
                    {
                        return false;
                    }
                    else
                    {
                        this._Channel_C_Value = value;
                    }
                    break;
                #endregion
                #region Channel D
                case 3:
                    if (this._Channel_D_Value == value)
                    {
                        return false;
                    }
                    else
                    {
                        this._Channel_D_Value = value;
                    }
                    break;
                #endregion
                #region Channel E
                case 4:
                    if (this._Channel_E_Value == value)
                    {
                        return false;
                    }
                    else
                    {
                        this._Channel_E_Value = value;
                    }
                    break;
                #endregion
                #region Channel F
                case 5:
                    if (this._Channel_F_Value == value)
                    {
                        return false;
                    }
                    else
                    {
                        this._Channel_F_Value = value;
                    }
                    break;
                #endregion
                default:
                    return false;
            }

            lock (this._outputLock)
            {

            }

            return true;
        }
        #endregion
        #region TryGetTelegram(out byte[] currentData)
        public override bool TryGetTelegram(out byte[] currentData)
        {
            currentData = Telegram_Stopp.ToArray(); // copy
            bool allZero = true;

            #region Channel A
            if (this._Channel_A_Value < 0)
            {
                // Range [-1..0] -> 0x80 - [0x7F .. 0x00] = [0x01 .. 0x80]
                currentData[3] = (byte)(0x80 - Math.Min(-this._Channel_A_Value * 0x7F, 0x7F));
                allZero = false;
            }
            else if (this._Channel_A_Value > 0)
            {
                // Range [0..1] -> 0x80 + [0x00 .. 0x7F] = [0x80 .. 0xFF]
                currentData[3] = (byte)(0x80 + Math.Min(this._Channel_A_Value * 0x7F, 0x7F));
                allZero = false;
            }
            else
            {
                currentData[3] = 0x80;
            }
            #endregion
            #region Channel B
            if (this._Channel_B_Value < 0)
            {
                // Range [-1..0] -> 0x80 - [0x7F .. 0x00] = [0x01 .. 0x80]
                currentData[4] = (byte)(0x80 - Math.Min(-this._Channel_B_Value * 0x7F, 0x7F));
                allZero = false;
            }
            else if (this._Channel_B_Value > 0)
            {
                // Range [0..1] -> 0x80 + [0x00 .. 0x7F] = [0x80 .. 0xFF]
                currentData[4] = (byte)(0x80 + Math.Min(this._Channel_B_Value * 0x7F, 0x7F));
                allZero = false;
            }
            else
            {
                currentData[4] = 0x80;
            }
            #endregion
            #region Channel C
            if (this._Channel_C_Value < 0)
            {
                // Range [-1..0] -> 0x80 - [0x7F .. 0x00] = [0x01 .. 0x80]
                currentData[5] = (byte)(0x80 - Math.Min(-this._Channel_C_Value * 0x7F, 0x7F));
                allZero = false;
            }
            else if (this._Channel_C_Value > 0)
            {
                // Range [0..1] -> 0x80 + [0x00 .. 0x7F] = [0x80 .. 0xFF]
                currentData[5] = (byte)(0x80 + Math.Min(this._Channel_C_Value * 0x7F, 0x7F));
                allZero = false;
            }
            else
            {
                currentData[5] = 0x80;
            }
            #endregion
            #region Channel D
            if (this._Channel_D_Value < 0)
            {
                // Range [-1..0] -> 0x80 - [0x7F .. 0x00] = [0x01 .. 0x80]
                currentData[6] = (byte)(0x80 - Math.Min(-this._Channel_D_Value * 0x7F, 0x7F));
                allZero = false;
            }
            else if (this._Channel_D_Value > 0)
            {
                // Range [0..1] -> 0x80 + [0x00 .. 0x7F] = [0x80 .. 0xFF]
                currentData[6] = (byte)(0x80 + Math.Min(this._Channel_D_Value * 0x7F, 0x7F));
                allZero = false;
            }
            else
            {
                currentData[6] = 0x80;
            }
            #endregion
            #region Channel E
            if (this._Channel_E_Value < 0)
            {
                // Range [-1..0] -> 0x80 - [0x7F .. 0x00] = [0x01 .. 0x80]
                currentData[7] = (byte)(0x80 - Math.Min(-this._Channel_E_Value * 0x7F, 0x7F));
                allZero = false;
            }
            else if (this._Channel_E_Value > 0)
            {
                // Range [0..1] -> 0x80 + [0x00 .. 0x7F] = [0x80 .. 0xFF]
                currentData[7] = (byte)(0x80 + Math.Min(this._Channel_E_Value * 0x7F, 0x7F));
                allZero = false;
            }
            else
            {
                currentData[7] = 0x80;
            }
            #endregion
            #region Channel F
            if (this._Channel_F_Value < 0)
            {
                // Range [-1..0] -> 0x80 - [0x7F .. 0x00] = [0x01 .. 0x80]
                currentData[8] = (byte)(0x80 - Math.Min(-this._Channel_F_Value * 0x7F, 0x7F));
                allZero = false;
            }
            else if (this._Channel_F_Value > 0)
            {
                // Range [0..1] -> 0x80 + [0x00 .. 0x7F] = [0x80 .. 0xFF]
                currentData[8] = (byte)(0x80 + Math.Min(this._Channel_F_Value * 0x7F, 0x7F));
                allZero = false;
            }
            else
            {
                currentData[8] = 0x80;
            }
            #endregion

            if (!this.isInitialized ||
                allZero && this.allZeroStopwatch.Elapsed > TimeSpan.FromSeconds(3))
            {
                currentData = Telegram_Connect;

                this.isInitialized = true;
            }
            else
            {
                this.allZeroStopwatch.Restart();
            }

            currentData = MouldKingCrypt.Crypt(currentData);
            return true;
        }
        #endregion
    }
}
