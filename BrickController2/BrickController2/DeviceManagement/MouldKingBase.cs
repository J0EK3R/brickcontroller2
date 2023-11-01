using BrickController2.PlatformServices.BluetoothLE;
using System;
using System.Diagnostics;
using System.Dynamic;
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

        protected const int MaxSupportedChannels = 6;
        #endregion

        #region Fields
        protected readonly Stopwatch allZeroStopwatch = Stopwatch.StartNew();

        private readonly int numberOfCahnnels;

        /// <summary>
        /// Telegram to connect to the device
        /// This telegram is sent on init and if the reconnect condition matches
        /// </summary>
        protected readonly byte[] _Telegram_Connect;

        /// <summary>
        /// base telgram
        /// </summary>
        protected readonly byte[] _Telegram_Base;

        protected float _Channel_A_Value = 0.0f;
        protected float _Channel_B_Value = 0.0f;
        protected float _Channel_C_Value = 0.0f;
        protected float _Channel_D_Value = 0.0f;
        protected float _Channel_E_Value = 0.0f;
        protected float _Channel_F_Value = 0.0f;

        protected bool isInitialized = false;
        /// <summary>
        /// after this timespan und all channels are zero the connect telegram is sent
        /// </summary>
        protected TimeSpan reconnectTimeSpan = TimeSpan.FromSeconds(3);
        #endregion
        #region Properties
        public override string BatteryVoltageSign => string.Empty;

        /// <summary>
        /// offset to position of first channel in base telegram
        /// </summary>
        protected virtual int ChannelStartOffset => 3;

        /// <summary>
        /// offset of last bytes wich can't be used
        /// </summary>
        protected virtual int ChannelEndOffset => 1;

        /// <summary>
        /// Calculates the number of channels
        /// </summary>
        public override int NumberOfChannels
        {
            get
            {
                return this.numberOfCahnnels;
            }
        }
        #endregion

        #region Constructor
        public MouldKingBase(string name, string address, byte[] deviceData, IDeviceRepository deviceRepository, IBluetoothLEService bleService, byte[] telegram_Connect, byte[] telegram_Base)
                : base(name, address, deviceData, deviceRepository, bleService)
        {
            this._Telegram_Connect = telegram_Connect;
            this._Telegram_Base = telegram_Base;

            this.numberOfCahnnels = Math.Min(MaxSupportedChannels, Math.Max(0, (this._Telegram_Base?.Length ?? 0) - this.ChannelEndOffset - this.ChannelStartOffset));
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
            currentData = this._Telegram_Base.ToArray(); // copy
            int channelDataLength = currentData.Length - this.ChannelEndOffset;
            int currentChannelStartOffset;
            bool allZero = true;

            #region Channel A
            currentChannelStartOffset = this.ChannelStartOffset + 0;

            if (this.NumberOfChannels >= 1 &&
                channelDataLength >= currentChannelStartOffset)
            {
                if (this._Channel_A_Value < 0)
                {
                    // Range [-1..0] -> 0x80 - [0x7F .. 0x00] = [0x01 .. 0x80]
                    currentData[currentChannelStartOffset] = (byte)(0x80 - Math.Min(-this._Channel_A_Value * 0x7F, 0x7F));
                    allZero = false;
                }
                else if (this._Channel_A_Value > 0)
                {
                    // Range [0..1] -> 0x80 + [0x00 .. 0x7F] = [0x80 .. 0xFF]
                    currentData[currentChannelStartOffset] = (byte)(0x80 + Math.Min(this._Channel_A_Value * 0x7F, 0x7F));
                    allZero = false;
                }
                else
                {
                    currentData[currentChannelStartOffset] = 0x80;
                }
            }
            #endregion
            #region Channel B
            currentChannelStartOffset = this.ChannelStartOffset + 1;

            if (this.NumberOfChannels >= 2 &&
                channelDataLength >= currentChannelStartOffset)
            {
                if (this._Channel_B_Value < 0)
                {
                    // Range [-1..0] -> 0x80 - [0x7F .. 0x00] = [0x01 .. 0x80]
                    currentData[currentChannelStartOffset] = (byte)(0x80 - Math.Min(-this._Channel_B_Value * 0x7F, 0x7F));
                    allZero = false;
                }
                else if (this._Channel_B_Value > 0)
                {
                    // Range [0..1] -> 0x80 + [0x00 .. 0x7F] = [0x80 .. 0xFF]
                    currentData[currentChannelStartOffset] = (byte)(0x80 + Math.Min(this._Channel_B_Value * 0x7F, 0x7F));
                    allZero = false;
                }
                else
                {
                    currentData[currentChannelStartOffset] = 0x80;
                }
            }
            #endregion
            #region Channel C
            currentChannelStartOffset = this.ChannelStartOffset + 2;

            if (this.NumberOfChannels >= 3 &&
                channelDataLength >= currentChannelStartOffset)
            {
                if (this._Channel_C_Value < 0)
                {
                    // Range [-1..0] -> 0x80 - [0x7F .. 0x00] = [0x01 .. 0x80]
                    currentData[currentChannelStartOffset] = (byte)(0x80 - Math.Min(-this._Channel_C_Value * 0x7F, 0x7F));
                    allZero = false;
                }
                else if (this._Channel_C_Value > 0)
                {
                    // Range [0..1] -> 0x80 + [0x00 .. 0x7F] = [0x80 .. 0xFF]
                    currentData[currentChannelStartOffset] = (byte)(0x80 + Math.Min(this._Channel_C_Value * 0x7F, 0x7F));
                    allZero = false;
                }
                else
                {
                    currentData[currentChannelStartOffset] = 0x80;
                }
            }
            #endregion
            #region Channel D
            currentChannelStartOffset = this.ChannelStartOffset + 3;

            if (this.NumberOfChannels >= 4 &&
                channelDataLength >= currentChannelStartOffset)
            {
                if (this._Channel_D_Value < 0)
                {
                    // Range [-1..0] -> 0x80 - [0x7F .. 0x00] = [0x01 .. 0x80]
                    currentData[currentChannelStartOffset] = (byte)(0x80 - Math.Min(-this._Channel_D_Value * 0x7F, 0x7F));
                    allZero = false;
                }
                else if (this._Channel_D_Value > 0)
                {
                    // Range [0..1] -> 0x80 + [0x00 .. 0x7F] = [0x80 .. 0xFF]
                    currentData[currentChannelStartOffset] = (byte)(0x80 + Math.Min(this._Channel_D_Value * 0x7F, 0x7F));
                    allZero = false;
                }
                else
                {
                    currentData[currentChannelStartOffset] = 0x80;
                }
            }
            #endregion
            #region Channel E
            currentChannelStartOffset = this.ChannelStartOffset + 4;

            if (this.NumberOfChannels >= 5 &&
                channelDataLength >= currentChannelStartOffset)
            {
                if (this._Channel_E_Value < 0)
                {
                    // Range [-1..0] -> 0x80 - [0x7F .. 0x00] = [0x01 .. 0x80]
                    currentData[currentChannelStartOffset] = (byte)(0x80 - Math.Min(-this._Channel_E_Value * 0x7F, 0x7F));
                    allZero = false;
                }
                else if (this._Channel_E_Value > 0)
                {
                    // Range [0..1] -> 0x80 + [0x00 .. 0x7F] = [0x80 .. 0xFF]
                    currentData[currentChannelStartOffset] = (byte)(0x80 + Math.Min(this._Channel_E_Value * 0x7F, 0x7F));
                    allZero = false;
                }
                else
                {
                    currentData[currentChannelStartOffset] = 0x80;
                }
            }
            #endregion
            #region Channel F
            currentChannelStartOffset = this.ChannelStartOffset + 5;

            if (this.NumberOfChannels >= 6 &&
                channelDataLength >= currentChannelStartOffset)
            {
                if (this._Channel_F_Value < 0)
                {
                    // Range [-1..0] -> 0x80 - [0x7F .. 0x00] = [0x01 .. 0x80]
                    currentData[currentChannelStartOffset] = (byte)(0x80 - Math.Min(-this._Channel_F_Value * 0x7F, 0x7F));
                    allZero = false;
                }
                else if (this._Channel_F_Value > 0)
                {
                    // Range [0..1] -> 0x80 + [0x00 .. 0x7F] = [0x80 .. 0xFF]
                    currentData[currentChannelStartOffset] = (byte)(0x80 + Math.Min(this._Channel_F_Value * 0x7F, 0x7F));
                    allZero = false;
                }
                else
                {
                    currentData[currentChannelStartOffset] = 0x80;
                }
            }
            #endregion

            if (!this.isInitialized ||
                (allZero && this.allZeroStopwatch.Elapsed > this.reconnectTimeSpan))
            {
                currentData = this._Telegram_Connect;

                this.isInitialized = true;
                this.SetStateText("Connecting");
            }
            else if(allZero)
            {
                this.SetStateText("Stopp");
            }
            else
            {
                this.allZeroStopwatch.Restart();
                this.SetStateText("Connected");
            }

            currentData = MouldKingCrypt.Crypt(currentData);
            return true;
        }
        #endregion
    }
}
