using BrickController2.PlatformServices.BluetoothLE;
using System;
using System.Linq;

namespace BrickController2.DeviceManagement
{
    /// <summary>
    /// TestModel
    /// </summary>
    internal class TestModel : MouldKingBaseByte
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
            0xc0, 0x02, 0x00, 0xc0, 0x02,
            0xc3, 0x99 };
        #endregion

        #region Fields
        #endregion
        #region Properties
        public override DeviceType DeviceType => DeviceType.TestModel;

        protected override int ChannelStartOffset => 7;

        protected override int ChannelEndOffset => 2;
        public override int NumberOfChannels => 6;
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

            #region colorByte
            byte colorByte = 0x00;
            if (this._Channel_C_Value < 0)
            {
                // Range [-1..0] -> 0x80 - [0x7F .. 0x00] = [0x01 .. 0x80]
                colorByte = (byte)(Math.Min(-this._Channel_C_Value * 10, 0xFF));
            }
            else if (this._Channel_C_Value > 0)
            {
                // Range [0..1] -> 0x80 + [0x00 .. 0x7F] = [0x80 .. 0xFF]
                colorByte = (byte)Math.Min(this._Channel_C_Value * 10, 0xFF);
            }
            else
            {
            }
            #endregion
            #region speedbyte
            // byte[7]/byte[10]
            //
            // ss00 bbaa
            //
            // ss - speed 
            //  0000: 0 = 0x00
            //  0100: 1 = 0x40
            //  1000: 2 = 0x80
            //  1100: 4 = 0xC0
            //
            // aa - channel 0
            //  bb 00: stop      = 0x00
            //  bb 01: backward  = 0x01
            //  bb 10: forward   = 0x02
            //
            // bb - channel 1
            //  00 aa: stop      = 0x00
            //  01 aa: backward  = 0x04
            //  10 aa: forward   = 0x08

            bool allZero = true;
            byte speedByte;
            byte checkByte;

            //float s = Math.Max(Math.Abs(this._Channel_A_Value) * 100, Math.Abs(this._Channel_B_Value) * 100);
            float speed = Math.Abs(this._Channel_A_Value) * 100;
            bool forward = this._Channel_A_Value >= 0;

            // Stop
            if (speed <= 30)
            {
                speedByte = 0x00;
                checkByte = 0x01;
            }
            // Speed 1
            else if (speed <= 80)
            {
                speedByte = 0x40;
                checkByte = 0x01;
            }
            // Speed 2
            else if (speed <= 90)
            {
                speedByte = 0x80;
                checkByte = 0x01;
            }
            // Speed 3
            else
            {
                speedByte = 0xC0;
                checkByte = 0x02;
            }

            // left
            if (this._Channel_B_Value < 0)
            {
                if (speed == 0)
                {
                    speedByte = 0x40; // speed 1

                    speedByte |= 0x02; // left chain
                    checkByte += 1;

                    speedByte |= 0x04; // right chain
                    checkByte += 1;

                    allZero = false;
                }
                else
                {
                    speedByte |= (byte)(forward ? 0x02 : 0x00); // left chain
                    //checkByte += 0;

                    speedByte |= (byte)(forward ? 0x00 : 0x04); // right chain
                    checkByte += 1;

                    allZero = false;
                }
            }
            // right
            else if (this._Channel_B_Value > 0)
            {
                if (speed == 0)
                {
                    speedByte = 0x40; // speed 1

                    speedByte |= 0x01; // left chain
                    checkByte += 1;

                    speedByte |= 0x08; // right chain
                    checkByte += 1;

                    allZero = false;
                }
                else
                {
                    speedByte |= (byte)(forward ? 0x00 : 0x01); // left chain
                    //checkByte += 1;

                    speedByte |= (byte)(forward ? 0x08 : 0x00); // right chain
                    checkByte += 1;

                    allZero = false;
                }
            }
            // straight
            else
            {
                if (speed == 0)
                {
                }
                else
                {
                    speedByte |= (byte)(forward ? 0x02 : 0x01); // left chain
                    checkByte += 1;

                    speedByte |= (byte)(forward ? 0x08 : 0x04); // right chain
                    checkByte += 1;

                    allZero = false;
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
                currentData[7] = speedByte;
                currentData[10] = speedByte;
                currentData[11] = checkByte;

                this.allZeroStopwatch.Restart();
                this.SetStateText("Connected");
            }

            currentData[8] = colorByte;

            return true;
        }
        #endregion
    }
}
