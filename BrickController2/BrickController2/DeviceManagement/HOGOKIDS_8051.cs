using BrickController2.Helpers;
using BrickController2.PlatformServices.BluetoothLE;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BrickController2.DeviceManagement
{
    /// <summary>
    /// HOGOKIDS 8051
    /// 
    /// https://www.amazon.de/dp/B0C9Q61RJ2?ref=ppx_yo2ov_dt_b_product_details&th=1
    /// </summary>
    internal class HOGOKIDS_8051 : BluetoothAdvertisingDevice<HOGOKIDS_8051.Telegram>
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
        /// <summary>
        /// ManufacturerID for BLEAdvertisments
        /// hex: 0x6CBC
        /// dec: 27836
        /// </summary>
        public const ushort ManufacturerID = 0x6CBC;

        // byte[10]
        //
        // ss00 bbaa
        //
        // ss - speed 
        //  00: 0
        //  01: 1
        //  10: 2
        //
        // aa - channel 0
        //  00: stop
        //  10: forward
        //  01: backward
        //
        // bb - channel 1
        //  00: stop
        //  10: forward
        //  01: backward

        // Color: byte[8]
        //
        // 0x00 0000  *default* -> pink
        // 0x01 0001  *off*
        // 0x02 0010  red
        // 0x03 0011  green
        // 0x04 0100  blue
        // 0x05 0101  yellow
        // 0x06 0110  violett
        // 0x07 0111  turquoise
        // 0x08 1000  white
        // >0x08      *default* -> pink

        // Response from HK Robot Company: 0xF1FF
        private static readonly byte[] Telegram_ResponseFromDevice = new byte[] {
            0xbc, 0x6c, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00,
            0xe1, 0x99 };


        private static readonly byte[] Telegram_Connect_ = new byte[] {
            0xc7, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
            0x00, 0x00, 0x00, 0x00, 0x00, 
            0xe1, 0x99 };

        private static readonly byte[] Telegram_Connect = new byte[] {
            0xc7, 0x45, 0x86, 0x05, 0x00, 0x00, 0x00, 
            0xc0, 0x02, 0x00, 0xc0, 0x02, 
            0xc3, 0x99 };

        private static readonly byte[] Telegram_Stopp = new byte[] {
            0xc7, 0x45, 0x86, 0x05, 0x00, 0x00, 0x00, 
            0xc0, 0x02, 0x00, 0xc0, 0x02, // c0: 1100 0000
            0xc3, 0x99 };

        private static readonly byte[] Telegram_C0_F1__C1_F1 = new byte[] {
            0xc7, 0x45, 0x86, 0x05, 0x00, 0x00, 0x00, 
            0x4a, 0x02, 0x00, 0x4a, 0x03, // 4a: 0100 1010
            0xc3, 0x99 };

        private static readonly byte[] Telegram_C0_B1__C1_B1 = new byte[] {
            0xc7, 0x45, 0x86, 0x05, 0x00, 0x00, 0x00, 
            0x45, 0x02, 0x00, 0x45, 0x03, // 45: 0100 0101
            0xc3, 0x99 };

        private static readonly byte[] Telegram_C0_F1__C1_B1 = new byte[] {
            0xc7, 0x45, 0x86, 0x05, 0x00, 0x00, 0x00, 
            0x46, 0x00, 0x00, 0x46, 0x03, // 46: 0100 0110
            0xc3, 0x99 };

        private static readonly byte[] Telegram_C0_F1__C1_S = new byte[] { 
            0xc7, 0x45, 0x86, 0x05, 0x00, 0x00, 0x00, 
            0x42, 0x00, 0x00, 0x42, 0x02, // 42: 0100 0010
            0xc3, 0x99 };

        private static readonly byte[] Telegram_C0_F2__C1_S = new byte[] {
            0xc7, 0x45, 0x86, 0x05, 0x00, 0x00, 0x00,
            0x82, 0x00, 0x00, 0x82, 0x02, // 82: 1000 0010
            0xc3, 0x99 };

        private static readonly byte[] Telegram_C0_F2__C1_F2 = new byte[] {
            0xc7, 0x45, 0x86, 0x05, 0x00, 0x00, 0x00,
            0x8A, 0x00, 0x00, 0x8A, 0x03, // 8A: 1000 1010
            0xc3, 0x99 };

        private static readonly byte[] Telegram_C0_F3__C1_S = new byte[] {
            0xc7, 0x45, 0x86, 0x05, 0x00, 0x00, 0x00,
            0xC2, 0x00, 0x00, 0xC2, 0x03, // C2: 1100 0010
            0xc3, 0x99 };

        private static readonly byte[] Telegram_C0_F3__C1_F3 = new byte[] {
            0xc7, 0x45, 0x86, 0x05, 0x00, 0x00, 0x00,
            0xCA, 0x00, 0x00, 0xCA, 0x04, // CA: 1100 1010
            0xc3, 0x99 };

        private static readonly byte[] Telegram_C0_B1__C1_S = new byte[] {
            0xc7, 0x45, 0x86, 0x05, 0x00, 0x00, 0x00, 
            0x41, 0x02, 0x00, 0x41, 0x02, // 41: 0100 0001
            0xc3, 0x99 };

        private static readonly byte[] Telegram_C0_B1__C1_F1 = new byte[] {
            0xc7, 0x45, 0x86, 0x05, 0x00, 0x00, 0x00, 
            0x49, 0x02, 0x00, 0x49, 0x03, // 49: 0100 1001
            0xc3, 0x99 };

        private static readonly byte[] Telegram_C0_S__C1_F1 = new byte[] {
            0xc7, 0x45, 0x86, 0x05, 0x00, 0x00, 0x00, 
            0x48, 0x02, 0x00, 0x48, 0x02, // 48: 0100 1000
            0xc3, 0x99 };

        private static readonly byte[] Telegram_C0_S__C1_F2 = new byte[] {
            0xc7, 0x45, 0x86, 0x05, 0x00, 0x00, 0x00,
            0x88, 0x02, 0x00, 0x88, 0x02, // 88: 1000 1000
            0xc3, 0x99 };

        private static readonly byte[] Telegram_C0_S__C1_F3 = new byte[] {
            0xc7, 0x45, 0x86, 0x05, 0x00, 0x00, 0x00,
            0xc8, 0x02, 0x00, 0xc8, 0x03, // 88: 1100 1000
            0xc3, 0x99 };

        private static readonly byte[] Telegram_C0_S__C1_B1 = new byte[] {
            0xc7, 0x45, 0x86, 0x05, 0x00, 0x00, 0x00, 
            0x44, 0x02, 0x00, 0x44, 0x02, // 44: 0100 0100
            0xc3, 0x99 };

        #endregion
        #region static Constructor
        static HOGOKIDS_8051()
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
        public override DeviceType DeviceType => DeviceType.HOGOKIDS_8051;

        public override int NumberOfChannels => 2;

        public override string BatteryVoltageSign => "V";
        #endregion

        #region Constructor
        public HOGOKIDS_8051(string name, string address, byte[] deviceData, IDeviceRepository deviceRepository, IBluetoothLEService bleService)
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
