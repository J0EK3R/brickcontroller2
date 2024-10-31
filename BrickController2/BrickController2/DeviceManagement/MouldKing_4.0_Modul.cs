using BrickController2.PlatformServices.BluetoothLE;

namespace BrickController2.DeviceManagement
{
    /// <summary>
    /// Mould King 4.0 Modul
    /// </summary>
    internal class MouldKing_4_0_Modul : MouldKingBaseNibble
    {
        #region Constants
        public const string Device1_3 = "Device1-3";

        /// <summary>
        /// Telegram wich is sent to connect - crypted
        /// 
        /// Header:   0x6D, 0xB6, 0x43, 0xCF, 0x7E, 0x8F, 0x47, 0x11, 
        /// ID:       0x48, 0x66, 0x59, 
        /// Channels: 0x38, 0xD1, 0x7A, 0x65, 
        /// ID:       0xE6, 
        /// Checksum: 0x34, 0x7F, 
        /// Filler:   0x13, 0x14, 0x15, 0x16, 0x17, 0x18
        /// </summary>
        private static readonly byte[] Telegram_Connect = new byte[] { 0xAD, 0x7B, 0xA7, 0x80, 0x80, 0x80, 0x4F, 0x52 };

        /// <summary>
        /// Telegram wich is sent after no connection - crypted
        /// 
        /// Header:   0x6D, 0xB6, 0x43, 0xCF, 0x7E, 0x8F, 0x47, 0x11, 
        /// ID:       0x98, 0x66, 0x59, 
        /// Channels: 0x30, 0xD9, 0x72, 0xA2, 0x3C, 0x6F, 
        /// ID:       0x56, 
        /// Checksum: 0x11, 0x26, 
        /// Filler:   0x15, 0x16, 0x17, 0x18
        /// </summary>
        private static readonly byte[] Telegram_No_Connect = new byte[] { 0x7D, 0x7B, 0xA7, 0x88, 0x88, 0x88, 0x88, 0x88, 0x88, 0x82 };

        /// <summary>
        /// Base Telegram for Device 1-3 - crypted
        /// 
        /// Header:              0x6D, 0xB6, 0x43, 0xCF, 0x7E, 0x8F, 0x47, 0x11, 
        /// ID:                  0x98, 0x66, 0x59, 
        /// Device1 Channel A+B: 0x30, 
        /// Device1 Channel C+D: 0xD9, 
        /// Device2 Channel A+B: 0x72, 
        /// Device2 Channel C+D: 0xA2, 
        /// Device3 Channel A+B: 0x3C, 
        /// Device3 Channel C+D: 0x6F, 
        /// ID:                  0x56, 
        /// Checksum:            0x11, 0x26, 
        /// Filler:              0x15, 0x16, 0x17, 0x18
        /// </summary>
        private static readonly byte[] Telegram_Base = new byte[] { 0x7D, 0x7B, 0xA7, 0x88, 0x88, 0x88, 0x88, 0x88, 0x88, 0x82 };
        #endregion

        #region Fields
        #endregion
        #region Properties
        public override DeviceType DeviceType => DeviceType.MouldKing_4_0_Modul;

        /// <summary>
        /// 3 Devices with 4 channels each
        /// </summary>
        public override int NumberOfChannels => 12;
        #endregion

        #region Constructor
        public MouldKing_4_0_Modul(string name, string address, byte[] deviceData, IDeviceRepository deviceRepository, IBluetoothLEService bleService)
          : base(name, address, deviceData, deviceRepository, bleService, MouldKing_4_0_Modul.Telegram_Connect, MouldKing_4_0_Modul.Telegram_Base)
        {
        }
        #endregion
    }
}
