using BrickController2.PlatformServices.BluetoothLE;
using System;
using System.Diagnostics;
using System.Linq;

namespace BrickController2.DeviceManagement
{
    /// <summary>
    /// Mould King 6.0 Modul
    /// </summary>
    internal abstract class MouldKingBase : BluetoothAdvertisingDevice
    {
        #region Constants
        public const ushort ManufacturerID = 0xFFF0;
        #endregion

        #region Fields
        private readonly Stopwatch allZeroStopwatch = Stopwatch.StartNew();

        /// <summary>
        /// </summary>
        private readonly byte[] _Telegram_Connect;

        /// <summary>
        /// </summary>
        private readonly byte[] _Telegram_Stop;

        private float _Channel_A_Value = 0.0f;
        private float _Channel_B_Value = 0.0f;
        private float _Channel_C_Value = 0.0f;
        private float _Channel_D_Value = 0.0f;
        private float _Channel_E_Value = 0.0f;
        private float _Channel_F_Value = 0.0f;

        private bool isInitialized = false;
        #endregion
        #region Properties
        public override string BatteryVoltageSign => string.Empty;
        #endregion

        #region Constructor
        public MouldKingBase(string name, string address, byte[] deviceData, IDeviceRepository deviceRepository, IBluetoothLEService bleService, byte[] telegram_Connect, byte[] telegram_Stop)
                : base(name, address, deviceData, deviceRepository, bleService)
        {
            this._Telegram_Connect = telegram_Connect;
            this._Telegram_Stop = telegram_Stop;
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

            return true;
        }
        #endregion
        #region TryGetTelegram(out byte[] currentData)
        public override bool TryGetTelegram(out byte[] currentData)
        {
            currentData = this._Telegram_Stop.ToArray(); // copy
            bool allZero = true;

            #region Channel A
            if (this.NumberOfChannels >= 1 &&
                currentData.Length > 4) // last byte is reserverd
            {
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
            }
            #endregion
            #region Channel B
            if (this.NumberOfChannels >= 2 &&
                currentData.Length > 5) // last byte is reserverd
            {
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
            }
            #endregion
            #region Channel C
            if (this.NumberOfChannels >= 3 &&
                currentData.Length > 6) // last byte is reserverd
            {
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
            }
            #endregion
            #region Channel D
            if (this.NumberOfChannels >= 4 &&
                currentData.Length > 7) // last byte is reserverd
            {
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
            }
            #endregion
            #region Channel E
            if (this.NumberOfChannels >= 5 &&
                currentData.Length > 8) // last byte is reserverd
            {
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
            }
            #endregion
            #region Channel F
            if (this.NumberOfChannels >= 6 &&
                currentData.Length > 9) // last byte is reserverd
            {
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
            }
            #endregion

            if (!this.isInitialized ||
                allZero && this.allZeroStopwatch.Elapsed > TimeSpan.FromSeconds(3))
            {
                currentData = this._Telegram_Connect;

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
