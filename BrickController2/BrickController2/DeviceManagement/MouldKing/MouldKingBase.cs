using BrickController2.PlatformServices.BluetoothLE;
using System;
using System.Diagnostics;

namespace BrickController2.DeviceManagement
{
    /// <summary>
    /// MouldKing baseclass
    /// </summary>
    internal abstract class MouldKingBase : BluetoothAdvertisingDevice
    {
        #region Constants
        /// <summary>
        /// ManufacturerID for MouldKing
        /// </summary>
        public const ushort ManufacturerID = 0xFFF0;

        /// <summary>
        /// Number of max count supported channels
        /// </summary>
        protected const int MaxSupportedChannels = 12;
        #endregion

        #region Fields
        /// <summary>
        /// stopwatch
        /// </summary>
        protected readonly Stopwatch allZeroStopwatch = Stopwatch.StartNew();

        /// <summary>
        /// Telegram to connect to the device
        /// This telegram is sent on init and if the reconnect condition matches
        /// </summary>
        protected readonly byte[] _Telegram_Connect;

        /// <summary>
        /// base telegram
        /// </summary>
        protected readonly byte[] _Telegram_Base;

        /// <summary>
        /// current value for channel A
        /// </summary>
        protected float _Channel_A_Value = 0.0f;

        /// <summary>
        /// current value for channel B
        /// </summary>
        protected float _Channel_B_Value = 0.0f;

        /// <summary>
        /// current value for channel C
        /// </summary>
        protected float _Channel_C_Value = 0.0f;

        /// <summary>
        /// current value for channel D
        /// </summary>
        protected float _Channel_D_Value = 0.0f;

        /// <summary>
        /// current value for channel E
        /// </summary>
        protected float _Channel_E_Value = 0.0f;

        /// <summary>
        /// current value for channel F
        /// </summary>
        protected float _Channel_F_Value = 0.0f;

        /// <summary>
        /// current value for channel G
        /// </summary>
        protected float _Channel_G_Value = 0.0f;

        /// <summary>
        /// current value for channel H
        /// </summary>
        protected float _Channel_H_Value = 0.0f;

        /// <summary>
        /// current value for channel I
        /// </summary>
        protected float _Channel_I_Value = 0.0f;

        /// <summary>
        /// current value for channel J
        /// </summary>
        protected float _Channel_J_Value = 0.0f;

        /// <summary>
        /// current value for channel K
        /// </summary>
        protected float _Channel_K_Value = 0.0f;

        /// <summary>
        /// current value for channel L
        /// </summary>
        protected float _Channel_L_Value = 0.0f;

        /// <summary>
        /// true if initialized
        /// </summary>
        protected bool isInitialized = false;

        /// <summary>
        /// after this timespan und all channels are zero the connect telegram is sent
        /// </summary>
        protected TimeSpan reconnectTimeSpan = TimeSpan.FromSeconds(3);
        #endregion
        #region Properties
        /// <summary>
        /// No voltage
        /// </summary>
        public override string BatteryVoltageSign => string.Empty;

        /// <summary>
        /// offset to position of first channel in base telegram
        /// </summary>
        protected virtual int ChannelStartOffset => 3;

        /// <summary>
        /// offset of last bytes wich can't be used
        /// </summary>
        protected virtual int ChannelEndOffset => 1;
        #endregion

        #region Constructor
        protected MouldKingBase(string name, string address, byte[] deviceData, IDeviceRepository deviceRepository, IBluetoothLEService bleService, byte[] telegram_Connect, byte[] telegram_Base)
                : base(name, address, deviceData, deviceRepository, bleService)
        {
            this._Telegram_Connect = telegram_Connect;
            this._Telegram_Base = telegram_Base;
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
                #region Channel G
                case 6:
                    if (this._Channel_G_Value == value)
                    {
                        return false;
                    }
                    else
                    {
                        this._Channel_G_Value = value;
                    }
                    break;
                #endregion
                #region Channel H
                case 7:
                    if (this._Channel_H_Value == value)
                    {
                        return false;
                    }
                    else
                    {
                        this._Channel_H_Value = value;
                    }
                    break;
                #endregion
                #region Channel I
                case 8:
                    if (this._Channel_I_Value == value)
                    {
                        return false;
                    }
                    else
                    {
                        this._Channel_I_Value = value;
                    }
                    break;
                #endregion
                #region Channel J
                case 9:
                    if (this._Channel_J_Value == value)
                    {
                        return false;
                    }
                    else
                    {
                        this._Channel_J_Value = value;
                    }
                    break;
                #endregion
                #region Channel K
                case 10:
                    if (this._Channel_K_Value == value)
                    {
                        return false;
                    }
                    else
                    {
                        this._Channel_K_Value = value;
                    }
                    break;
                #endregion
                #region Channel L
                case 11:
                    if (this._Channel_L_Value == value)
                    {
                        return false;
                    }
                    else
                    {
                        this._Channel_L_Value = value;
                    }
                    break;
                #endregion
                default:
                    return false;
            }

            return true;
        }
        #endregion
    }
}
