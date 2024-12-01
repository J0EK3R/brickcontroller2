using BluetoothEnDeCrypt.PowerBox;
using BrickController2.PlatformServices.BluetoothLE;
using System;
using System.Diagnostics;
using System.Linq;

namespace BrickController2.DeviceManagement
{
    /// <summary>
    /// PowerBox M Battery
    /// </summary>
    internal class PowerBoxMBattery : BluetoothAdvertisingDevice
    {
        #region Constants
        /// <summary>
        /// ManufacturerID for BLEAdvertisments
        /// </summary>
        public const ushort ManufacturerID = 0xff00;
        #endregion
        #region Fields
        /// <summary>
        /// stopwatch
        /// </summary>
        protected readonly Stopwatch allZeroStopwatch = Stopwatch.StartNew();

        /// <summary>
        /// true if initialized
        /// </summary>
        protected bool isInitialized = false;

        /// <summary>
        /// after this timespan und all channels are zero the connect telegram is sent
        /// </summary>
        protected TimeSpan reconnectTimeSpan = TimeSpan.FromSeconds(3);

        /// <summary>
        /// channel0
        /// </summary>
        private float _Channel0_Value = 0.0f;

        /// <summary>
        /// base telegram
        /// </summary>
        protected readonly byte[] _Telegram_Base;

        /// <summary>
        /// Telegram to connect to the device
        /// This telegram is sent on init and if the reconnect condition matches
        /// </summary>
        protected readonly byte[] _Telegram_Connect;
        #endregion
        #region Properties
        public override DeviceType DeviceType => DeviceType.PowerBox_M_Battery;

        public override int NumberOfChannels => 1;

        public override string BatteryVoltageSign => string.Empty;
        #endregion

        #region Constructor
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name"></param>
        /// <param name="address"></param>
        /// <param name="deviceData"></param>
        /// <param name="deviceRepository"></param>
        /// <param name="bleService"></param>
        public PowerBoxMBattery(string name, string address, byte[] deviceData, IDeviceRepository deviceRepository, IBluetoothLEService bleService)
                : base(name, address, deviceData, deviceRepository, bleService)
        {
            this._Telegram_Connect = new byte[] { 0xA4, 0xFE, 0x19, 0x80, 0x80, 0x80, 0x00, 0x5B };
            this._Telegram_Base = new byte[] { 0x40, 0xFE, 0x19, 0x00, 0x00, 0x00, 0x00, 0xBF };
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

        #region SetChannel(int channel, float value)
        protected override bool SetChannel(int channel, float value)
        {
            switch (channel)
            {
                #region Channel A
                case 0:
                    if (this._Channel0_Value == value)
                    {
                        return false;
                    }
                    else
                    {
                        this._Channel0_Value = value;
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
            bool allZero = true;
            int currentChannelStartOffset;

            #region Channel A
            currentChannelStartOffset = 3;
            if (this.NumberOfChannels >= 1)
            {
                // Length = 0008:  0xA4 0xFE 0x19 0x80 0x80 0x80 0x00 0x5B - 128 - 10000000 Connect
                // Length = 0008:  0x40 0xFE 0x19 0x00 0x00 0x00 0x00 0xBF - 0 - 0          Stop
                // Length = 0008:  0x40 0xFE 0x19 0x19 0x19 0x00 0x00 0xBF - 25 - 11001     Speed +1
                // Length = 0008:  0x40 0xFE 0x19 0x4C 0x4C 0x00 0x00 0xBF - 76 - 1001100   Speed +2
                // Length = 0008:  0x40 0xFE 0x19 0x7F 0x7F 0x00 0x00 0xBF - 127 - 1111111  Speed +3
                // Length = 0008:  0x40 0xFE 0x19 0x91 0x91 0x00 0x00 0xBF - 145 - 10010001 Speed -1
                // Length = 0008:  0x40 0xFE 0x19 0xC4 0xC4 0x00 0x00 0xBF - 196 - 11000100 Speed -2
                // Length = 0008:  0x40 0xFE 0x19 0xF7 0xF7 0x00 0x00 0xBF - 247 - 11110111 Speed -3

                if (this._Channel0_Value < 0)
                {
                    int scale = (int)(-this._Channel0_Value * 2) * 51 + 145;

                    currentData[currentChannelStartOffset] = (byte)Math.Max(0, Math.Min(scale, 0xFF));
                    allZero = false;
                }
                else if (this._Channel0_Value > 0)
                {
                    int scale = (int)(this._Channel0_Value * 2) * 51 + 25;
                    currentData[currentChannelStartOffset] = (byte)Math.Max(0, Math.Min(scale, 0xFF));
                    allZero = false;
                }
                else
                {
                    currentData[currentChannelStartOffset] = (byte)Math.Max(0, Math.Min(this._Channel0_Value, 0xFF));
                }
                // value is duplicated on next byte
                currentData[currentChannelStartOffset + 1] = currentData[currentChannelStartOffset];
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

            PowerBoxBLEUtils.Get_rf_payload(PowerBoxBLEUtils.AddressArray, currentData, PowerBoxBLEUtils.CTXValue, out currentData);
            return true;
        }
        #endregion
    }
}
