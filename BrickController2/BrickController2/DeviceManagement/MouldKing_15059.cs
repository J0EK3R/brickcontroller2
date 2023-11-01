using BrickController2.PlatformServices.BluetoothLE;

namespace BrickController2.DeviceManagement
{
    /// <summary>
    /// Mould King 15059 Roboter
    /// 
    /// https://www.amazon.de/dp/B09TPHJ5L7?psc=1&ref=ppx_yo2ov_dt_b_product_details
    /// </summary>
    internal class MouldKing_15059 : MouldKingBase
    {
        #region Constants
        /// <summary>
        /// </summary>
        private static readonly byte[] Telegram_Connect = new byte[] { 0xAA, 0x7B, 0xA7, 0x00, 0x00, 0x00, 0x00, 0x55 };

        /// <summary>
        /// </summary>
        private static readonly byte[] Telegram_Base = new byte[] { 0x66, 0x7B, 0xA7, 0x80, 0x80, 0x80, 0x80, 0x99 };
        #endregion

        #region Fields
        #endregion
        #region Properties
        public override DeviceType DeviceType => DeviceType.MouldKing_15059;

        //public override int NumberOfChannels => 2;
        #endregion

        #region Constructor
        public MouldKing_15059(string name, string address, byte[] deviceData, IDeviceRepository deviceRepository, IBluetoothLEService bleService)
                : base(name, address, deviceData, deviceRepository, bleService, Telegram_Connect, Telegram_Base)
        {
        }
        #endregion
    }
}
