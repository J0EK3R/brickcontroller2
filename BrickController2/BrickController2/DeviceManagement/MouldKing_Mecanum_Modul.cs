using BrickController2.PlatformServices.BluetoothLE;

namespace BrickController2.DeviceManagement
{
    /// <summary>
    /// Mould King 6.0 Modul
    /// </summary>
    internal class MouldKing_Mecanum_Modul : MouldKingBase
    {
        #region Constants
        /// <summary>
        /// </summary>
        private static readonly byte[] Telegram_Connect = new byte[] { 0x6D, 0x7B, 0xA7, 0x80, 0x80, 0x80, 0x80, 0x92, };

        /// <summary>
        /// </summary>
        private static readonly byte[] Telegram_Stopp = new byte[] { 0x61, 0x7B, 0xA7, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x9E };
        #endregion

        #region Fields
        #endregion
        #region Properties
        public override DeviceType DeviceType => DeviceType.MouldKing_Mecanum_Modul;

        public override int NumberOfChannels => 6;
        #endregion

        #region Constructor
        public MouldKing_Mecanum_Modul(string name, string address, byte[] deviceData, IDeviceRepository deviceRepository, IBluetoothLEService bleService)
                : base(name, address, deviceData, deviceRepository, bleService, Telegram_Connect, Telegram_Stopp)
        {
        }
        #endregion
    }
}
