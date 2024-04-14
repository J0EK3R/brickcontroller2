using BrickController2.PlatformServices.BluetoothLE;
using System;
using System.Linq;

namespace BrickController2.DeviceManagement
{
    /// <summary>
    /// Mould King baseclass for devices with one byte per channel
    /// </summary>
    internal abstract class MouldKingBaseByte : MouldKingBase
    {
        #region Constants
        #endregion

        #region Fields
        #endregion
        #region Properties
        #endregion

        #region Constructor
        protected MouldKingBaseByte(string name, string address, byte[] deviceData, IDeviceRepository deviceRepository, IBluetoothLEService bleService, byte[] telegram_Connect, byte[] telegram_Base)
                : base(name, address, deviceData, deviceRepository, bleService, telegram_Connect, telegram_Base)
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
                    currentData[currentChannelStartOffset] = (byte)(0x80 - Math.Min(-this._Channel_A_Value * 0x80, 0x80));
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
                    currentData[currentChannelStartOffset] = (byte)(0x80 - Math.Min(-this._Channel_B_Value * 0x80, 0x80));
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
                    currentData[currentChannelStartOffset] = (byte)(0x80 - Math.Min(-this._Channel_C_Value * 0x80, 0x80));
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
                    currentData[currentChannelStartOffset] = (byte)(0x80 - Math.Min(-this._Channel_D_Value * 0x80, 0x80));
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
                    currentData[currentChannelStartOffset] = (byte)(0x80 - Math.Min(-this._Channel_E_Value * 0x80, 0x80));
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
                    currentData[currentChannelStartOffset] = (byte)(0x80 - Math.Min(-this._Channel_F_Value * 0x80, 0x80));
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
            #region Channel G
            currentChannelStartOffset = this.ChannelStartOffset + 6;

            if (this.NumberOfChannels >= 7 &&
                channelDataLength >= currentChannelStartOffset)
            {
                if (this._Channel_G_Value < 0)
                {
                    // Range [-1..0] -> 0x80 - [0x7F .. 0x00] = [0x01 .. 0x80]
                    currentData[currentChannelStartOffset] = (byte)(0x80 - Math.Min(-this._Channel_G_Value * 0x80, 0x80));
                    allZero = false;
                }
                else if (this._Channel_G_Value > 0)
                {
                    // Range [0..1] -> 0x80 + [0x00 .. 0x7F] = [0x80 .. 0xFF]
                    currentData[currentChannelStartOffset] = (byte)(0x80 + Math.Min(this._Channel_G_Value * 0x7F, 0x7F));
                    allZero = false;
                }
                else
                {
                    currentData[currentChannelStartOffset] = 0x80;
                }
            }
            #endregion
            #region Channel H
            currentChannelStartOffset = this.ChannelStartOffset + 7;

            if (this.NumberOfChannels >= 8 &&
                channelDataLength >= currentChannelStartOffset)
            {
                if (this._Channel_H_Value < 0)
                {
                    // Range [-1..0] -> 0x80 - [0x7F .. 0x00] = [0x01 .. 0x80]
                    currentData[currentChannelStartOffset] = (byte)(0x80 - Math.Min(-this._Channel_H_Value * 0x80, 0x80));
                    allZero = false;
                }
                else if (this._Channel_H_Value > 0)
                {
                    // Range [0..1] -> 0x80 + [0x00 .. 0x7F] = [0x80 .. 0xFF]
                    currentData[currentChannelStartOffset] = (byte)(0x80 + Math.Min(this._Channel_H_Value * 0x7F, 0x7F));
                    allZero = false;
                }
                else
                {
                    currentData[currentChannelStartOffset] = 0x80;
                }
            }
            #endregion
            #region Channel I
            currentChannelStartOffset = this.ChannelStartOffset + 8;

            if (this.NumberOfChannels >= 9 &&
                channelDataLength >= currentChannelStartOffset)
            {
                if (this._Channel_I_Value < 0)
                {
                    // Range [-1..0] -> 0x80 - [0x7F .. 0x00] = [0x01 .. 0x80]
                    currentData[currentChannelStartOffset] = (byte)(0x80 - Math.Min(-this._Channel_I_Value * 0x80, 0x80));
                    allZero = false;
                }
                else if (this._Channel_I_Value > 0)
                {
                    // Range [0..1] -> 0x80 + [0x00 .. 0x7F] = [0x80 .. 0xFF]
                    currentData[currentChannelStartOffset] = (byte)(0x80 + Math.Min(this._Channel_I_Value * 0x7F, 0x7F));
                    allZero = false;
                }
                else
                {
                    currentData[currentChannelStartOffset] = 0x80;
                }
            }
            #endregion
            #region Channel J
            currentChannelStartOffset = this.ChannelStartOffset + 9;

            if (this.NumberOfChannels >= 10 &&
                channelDataLength >= currentChannelStartOffset)
            {
                if (this._Channel_J_Value < 0)
                {
                    // Range [-1..0] -> 0x80 - [0x7F .. 0x00] = [0x01 .. 0x80]
                    currentData[currentChannelStartOffset] = (byte)(0x80 - Math.Min(-this._Channel_J_Value * 0x80, 0x80));
                    allZero = false;
                }
                else if (this._Channel_J_Value > 0)
                {
                    // Range [0..1] -> 0x80 + [0x00 .. 0x7F] = [0x80 .. 0xFF]
                    currentData[currentChannelStartOffset] = (byte)(0x80 + Math.Min(this._Channel_J_Value * 0x7F, 0x7F));
                    allZero = false;
                }
                else
                {
                    currentData[currentChannelStartOffset] = 0x80;
                }
            }
            #endregion
            #region Channel K
            currentChannelStartOffset = this.ChannelStartOffset + 10;

            if (this.NumberOfChannels >= 11 &&
                channelDataLength >= currentChannelStartOffset)
            {
                if (this._Channel_K_Value < 0)
                {
                    // Range [-1..0] -> 0x80 - [0x7F .. 0x00] = [0x01 .. 0x80]
                    currentData[currentChannelStartOffset] = (byte)(0x80 - Math.Min(-this._Channel_K_Value * 0x80, 0x80));
                    allZero = false;
                }
                else if (this._Channel_K_Value > 0)
                {
                    // Range [0..1] -> 0x80 + [0x00 .. 0x7F] = [0x80 .. 0xFF]
                    currentData[currentChannelStartOffset] = (byte)(0x80 + Math.Min(this._Channel_K_Value * 0x7F, 0x7F));
                    allZero = false;
                }
                else
                {
                    currentData[currentChannelStartOffset] = 0x80;
                }
            }
            #endregion
            #region Channel L
            currentChannelStartOffset = this.ChannelStartOffset + 11;

            if (this.NumberOfChannels >= 12 &&
                channelDataLength >= currentChannelStartOffset)
            {
                if (this._Channel_L_Value < 0)
                {
                    // Range [-1..0] -> 0x80 - [0x7F .. 0x00] = [0x01 .. 0x80]
                    currentData[currentChannelStartOffset] = (byte)(0x80 - Math.Min(-this._Channel_L_Value * 0x80, 0x80));
                    allZero = false;
                }
                else if (this._Channel_L_Value > 0)
                {
                    // Range [0..1] -> 0x80 + [0x00 .. 0x7F] = [0x80 .. 0xFF]
                    currentData[currentChannelStartOffset] = (byte)(0x80 + Math.Min(this._Channel_L_Value * 0x7F, 0x7F));
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
                this.SetStateText("Sending Connect Data");
            }
            else if (allZero)
            {
                this.SetStateText("Sending Stopp");
            }
            else
            {
                this.allZeroStopwatch.Restart();
                this.SetStateText("Sending Data");
            }

            currentData = MouldKingCrypt.Crypt(currentData);
            return true;
        }
        #endregion
    }
}
