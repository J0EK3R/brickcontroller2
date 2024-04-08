using BrickController2.PlatformServices.BluetoothLE;
using System;
using System.Linq;

namespace BrickController2.DeviceManagement
{
    /// <summary>
    /// Mould King baseclass for devices with one nibble per channel
    /// </summary>
    internal abstract class MouldKingBaseNibble : MouldKingBase
    {
        #region Constants
        #endregion

        #region Fields
        #endregion
        #region Properties
        #endregion

        #region Constructor
        protected MouldKingBaseNibble(string name, string address, byte[] deviceData, IDeviceRepository deviceRepository, IBluetoothLEService bleService, byte[] telegram_Connect, byte[] telegram_Base)
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

            #region Channel A + B
            currentChannelStartOffset = this.ChannelStartOffset + 0;

            if (this.NumberOfChannels >= 2 &&
                channelDataLength >= currentChannelStartOffset)
            {
                byte highByte;
                if (this._Channel_A_Value < 0)
                {
                    // Range [-1..0] -> 0x08 - [0x08 .. 0x00] = [0x00 .. 0x08]
                    highByte = (byte)(0x08 - Math.Min(-this._Channel_A_Value * 0x08, 0x08));
                    allZero = false;
                }
                else if (this._Channel_A_Value > 0)
                {
                    // Range [0..1] -> 0x80 + [0x00 .. 0x07] = [0x80 .. 0x0F]
                    highByte = (byte)(0x08 + Math.Min(this._Channel_A_Value * 0x07, 0x07));
                    allZero = false;
                }
                else
                {
                    highByte = 0x08;
                }

                byte lowByte;
                if (this._Channel_B_Value < 0)
                {
                    // Range [-1..0] -> 0x08 - [0x08 .. 0x00] = [0x00 .. 0x08]
                    lowByte = (byte)(0x08 - Math.Min(-this._Channel_B_Value * 0x08, 0x08));
                    allZero = false;
                }
                else if (this._Channel_B_Value > 0)
                {
                    // Range [0..1] -> 0x08 + [0x00 .. 0x07] = [0x80 .. 0x0F]
                    lowByte = (byte)(0x08 + Math.Min(this._Channel_B_Value * 0x07, 0x07));
                    allZero = false;
                }
                else
                {
                    lowByte = 0x08;
                }

                currentData[currentChannelStartOffset] = (byte)((highByte << 4) + lowByte);
            }
            #endregion
            #region Channel C + D
            currentChannelStartOffset = this.ChannelStartOffset + 1;

            if (this.NumberOfChannels >= 4 &&
                channelDataLength >= currentChannelStartOffset)
            {
                byte highByte;
                if (this._Channel_C_Value < 0)
                {
                    // Range [-1..0] -> 0x08 - [0x08 .. 0x00] = [0x00 .. 0x08]
                    highByte = (byte)(0x08 - Math.Min(-this._Channel_C_Value * 0x08, 0x08));
                    allZero = false;
                }
                else if (this._Channel_C_Value > 0)
                {
                    // Range [0..1] -> 0x80 + [0x00 .. 0x07] = [0x80 .. 0x0F]
                    highByte = (byte)(0x08 + Math.Min(this._Channel_C_Value * 0x07, 0x07));
                    allZero = false;
                }
                else
                {
                    highByte = 0x08;
                }

                byte lowByte;
                if (this._Channel_D_Value < 0)
                {
                    // Range [-1..0] -> 0x08 - [0x08 .. 0x00] = [0x00 .. 0x08]
                    lowByte = (byte)(0x08 - Math.Min(-this._Channel_D_Value * 0x08, 0x08));
                    allZero = false;
                }
                else if (this._Channel_D_Value > 0)
                {
                    // Range [0..1] -> 0x08 + [0x00 .. 0x07] = [0x80 .. 0x0F]
                    lowByte = (byte)(0x08 + Math.Min(this._Channel_D_Value * 0x07, 0x07));
                    allZero = false;
                }
                else
                {
                    lowByte = 0x08;
                }

                currentData[currentChannelStartOffset] = (byte)((highByte << 4) + lowByte);
            }
            #endregion
            #region Channel E + F
            currentChannelStartOffset = this.ChannelStartOffset + 2;

            if (this.NumberOfChannels >= 6 &&
                channelDataLength >= currentChannelStartOffset)
            {
                byte highByte;
                if (this._Channel_E_Value < 0)
                {
                    // Range [-1..0] -> 0x08 - [0x08 .. 0x00] = [0x00 .. 0x08]
                    highByte = (byte)(0x08 - Math.Min(-this._Channel_E_Value * 0x08, 0x08));
                    allZero = false;
                }
                else if (this._Channel_E_Value > 0)
                {
                    // Range [0..1] -> 0x80 + [0x00 .. 0x07] = [0x80 .. 0x0F]
                    highByte = (byte)(0x08 + Math.Min(this._Channel_E_Value * 0x07, 0x07));
                    allZero = false;
                }
                else
                {
                    highByte = 0x08;
                }

                byte lowByte;
                if (this._Channel_F_Value < 0)
                {
                    // Range [-1..0] -> 0x08 - [0x08 .. 0x00] = [0x00 .. 0x08]
                    lowByte = (byte)(0x08 - Math.Min(-this._Channel_F_Value * 0x08, 0x08));
                    allZero = false;
                }
                else if (this._Channel_F_Value > 0)
                {
                    // Range [0..1] -> 0x08 + [0x00 .. 0x07] = [0x80 .. 0x0F]
                    lowByte = (byte)(0x08 + Math.Min(this._Channel_F_Value * 0x07, 0x07));
                    allZero = false;
                }
                else
                {
                    lowByte = 0x08;
                }

                currentData[currentChannelStartOffset] = (byte)((highByte << 4) + lowByte);
            }
            #endregion
            #region Channel G + H
            currentChannelStartOffset = this.ChannelStartOffset + 3;

            if (this.NumberOfChannels >= 8 &&
                channelDataLength >= currentChannelStartOffset)
            {
                byte highByte;
                if (this._Channel_G_Value < 0)
                {
                    // Range [-1..0] -> 0x08 - [0x08 .. 0x00] = [0x00 .. 0x08]
                    highByte = (byte)(0x08 - Math.Min(-this._Channel_G_Value * 0x08, 0x08));
                    allZero = false;
                }
                else if (this._Channel_G_Value > 0)
                {
                    // Range [0..1] -> 0x80 + [0x00 .. 0x07] = [0x80 .. 0x0F]
                    highByte = (byte)(0x08 + Math.Min(this._Channel_G_Value * 0x07, 0x07));
                    allZero = false;
                }
                else
                {
                    highByte = 0x08;
                }

                byte lowByte;
                if (this._Channel_H_Value < 0)
                {
                    // Range [-1..0] -> 0x08 - [0x08 .. 0x00] = [0x00 .. 0x08]
                    lowByte = (byte)(0x08 - Math.Min(-this._Channel_H_Value * 0x08, 0x08));
                    allZero = false;
                }
                else if (this._Channel_H_Value > 0)
                {
                    // Range [0..1] -> 0x08 + [0x00 .. 0x07] = [0x80 .. 0x0F]
                    lowByte = (byte)(0x08 + Math.Min(this._Channel_H_Value * 0x07, 0x07));
                    allZero = false;
                }
                else
                {
                    lowByte = 0x08;
                }

                currentData[currentChannelStartOffset] = (byte)((highByte << 4) + lowByte);
            }
            #endregion
            #region Channel I + J
            currentChannelStartOffset = this.ChannelStartOffset + 4;

            if (this.NumberOfChannels >= 10 &&
                channelDataLength >= currentChannelStartOffset)
            {
                byte highByte;
                if (this._Channel_I_Value < 0)
                {
                    // Range [-1..0] -> 0x08 - [0x08 .. 0x00] = [0x00 .. 0x08]
                    highByte = (byte)(0x08 - Math.Min(-this._Channel_I_Value * 0x08, 0x08));
                    allZero = false;
                }
                else if (this._Channel_I_Value > 0)
                {
                    // Range [0..1] -> 0x80 + [0x00 .. 0x07] = [0x80 .. 0x0F]
                    highByte = (byte)(0x08 + Math.Min(this._Channel_I_Value * 0x07, 0x07));
                    allZero = false;
                }
                else
                {
                    highByte = 0x08;
                }

                byte lowByte;
                if (this._Channel_J_Value < 0)
                {
                    // Range [-1..0] -> 0x08 - [0x08 .. 0x00] = [0x00 .. 0x08]
                    lowByte = (byte)(0x08 - Math.Min(-this._Channel_J_Value * 0x08, 0x08));
                    allZero = false;
                }
                else if (this._Channel_J_Value > 0)
                {
                    // Range [0..1] -> 0x08 + [0x00 .. 0x07] = [0x80 .. 0x0F]
                    lowByte = (byte)(0x08 + Math.Min(this._Channel_J_Value * 0x07, 0x07));
                    allZero = false;
                }
                else
                {
                    lowByte = 0x08;
                }

                currentData[currentChannelStartOffset] = (byte)((highByte << 4) + lowByte);
            }
            #endregion
            #region Channel K + L
            currentChannelStartOffset = this.ChannelStartOffset + 5;

            if (this.NumberOfChannels >= 12 &&
                channelDataLength >= currentChannelStartOffset)
            {
                byte highByte;
                if (this._Channel_K_Value < 0)
                {
                    // Range [-1..0] -> 0x08 - [0x08 .. 0x00] = [0x00 .. 0x08]
                    highByte = (byte)(0x08 - Math.Min(-this._Channel_K_Value * 0x08, 0x08));
                    allZero = false;
                }
                else if (this._Channel_K_Value > 0)
                {
                    // Range [0..1] -> 0x80 + [0x00 .. 0x07] = [0x80 .. 0x0F]
                    highByte = (byte)(0x08 + Math.Min(this._Channel_K_Value * 0x07, 0x07));
                    allZero = false;
                }
                else
                {
                    highByte = 0x08;
                }

                byte lowByte;
                if (this._Channel_L_Value < 0)
                {
                    // Range [-1..0] -> 0x08 - [0x08 .. 0x00] = [0x00 .. 0x08]
                    lowByte = (byte)(0x08 - Math.Min(-this._Channel_L_Value * 0x08, 0x08));
                    allZero = false;
                }
                else if (this._Channel_L_Value > 0)
                {
                    // Range [0..1] -> 0x08 + [0x00 .. 0x07] = [0x80 .. 0x0F]
                    lowByte = (byte)(0x08 + Math.Min(this._Channel_L_Value * 0x07, 0x07));
                    allZero = false;
                }
                else
                {
                    lowByte = 0x08;
                }

                currentData[currentChannelStartOffset] = (byte)((highByte << 4) + lowByte);
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

            currentData = MouldKingCrypt.Crypt(currentData);
            return true;
        }
        #endregion
    }
}
