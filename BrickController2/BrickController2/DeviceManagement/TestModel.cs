using BrickController2.PlatformServices.BluetoothLE;
using System;
using System.Linq;

namespace BrickController2.DeviceManagement
{
    /// <summary>
    /// TestModel
    /// </summary>
    internal class TestModel : MouldKingBase
    {
        #region Constants
        public new const ushort ManufacturerID = 0x6CBC;

        /// <summary>
        /// </summary>
        private static readonly byte[] Telegram_Connect = new byte[] {
            0xc7, 0x45, 0x86, 0x05, 0x00, 0x00, 0x00,
            0xc0, 0x02, 0x00, 0xc0, 0x02,
            0xc3, 0x99 };

        /// <summary>
        /// </summary>
        private static readonly byte[] Telegram_Stop = new byte[] {
            0xc7, 0x45, 0x86, 0x05, 0x00, 0x00, 0x00,
            0xc0, 0x02, 0x00, 0xc0, 0x02, // c0: 1100 0000
            0xc3, 0x99 };
        #endregion

        #region Fields
        #endregion
        #region Properties
        public override DeviceType DeviceType => DeviceType.TestModel;

        protected override int ChannelStartOffset => 7;

        protected override int ChannelEndOffset => 2;
        #endregion

        #region Constructor
        public TestModel(string name, string address, byte[] deviceData, IDeviceRepository deviceRepository, IBluetoothLEService bleService)
                : base(name, address, deviceData, deviceRepository, bleService, Telegram_Connect, Telegram_Stop)
        {
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
            else if (allZero)
            {
                this.SetStateText("Stopp");
            }
            else
            {
                this.allZeroStopwatch.Restart();
                this.SetStateText("Connected");
            }

            return true;
        }
        #endregion
    }
}
