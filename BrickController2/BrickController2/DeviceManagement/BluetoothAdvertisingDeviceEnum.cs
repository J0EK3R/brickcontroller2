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
    /// Baseclass for devices wich communicate over BluetoothAdvertising
    /// </summary>
    /// <typeparam name="_TelegramType"></typeparam>
    internal abstract class BluetoothAdvertisingDeviceEnum<_TelegramType> : BluetoothAdvertisingDevice
    {
        #region Constants
        /// <summary>
        /// static dictionary to resolve telegrams by enum
        /// </summary>
        protected static readonly Dictionary<_TelegramType, byte[]> Telegrams = new Dictionary<_TelegramType, byte[]>();
        #endregion
        #region Fields
        /// <summary>
        /// Value of current telegram
        /// </summary>
        protected _TelegramType _currentTelegram;
        #endregion
        #region Properties
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
        protected BluetoothAdvertisingDeviceEnum(string name, string address, byte[] deviceData, IDeviceRepository deviceRepository, IBluetoothLEService bleService)
            : base(name, address, deviceData, deviceRepository, bleService)
        {
        }
        #endregion

        #region TryGetTelegram(out byte[] currentData)
        /// <summary>
        /// this method returns current telegramdata
        /// </summary>
        /// <param name="currentData"></param>
        /// <returns></returns>
        public override bool TryGetTelegram(out byte[] currentData)
        {
            return Telegrams.TryGetValue(this._currentTelegram, out currentData);
        }
        #endregion
    }
}
