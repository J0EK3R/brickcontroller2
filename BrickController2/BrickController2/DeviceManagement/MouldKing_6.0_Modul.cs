using BrickController2.PlatformServices.BluetoothLE;

namespace BrickController2.DeviceManagement
{
  /// <summary>
  /// Mould King 6.0 Modul
  /// </summary>
  internal class MouldKing_6_0_Modul : MouldKingBase
  {
    #region Constants
    public const string DeviceA = "DeviceA";
    public const string DeviceB = "DeviceB";
    public const string DeviceC = "DeviceC";

    /// <summary>
    /// Telegram wich is sent to connect - crypted
    /// 
    /// Header:   0x6D, 0xB6, 0x43, 0xCF, 0x7E, 0x8F, 0x47, 0x11, 
    /// ID:       0x88, 0x66, 0x59, 
    /// Channels: 0x38, 0xD1, 0x7A, 0xAA, 
    /// ID:       0x26, 
    /// Checksum: 0x49, 0x5E, 
    /// Filler:   0x13, 0x14, 0x15, 0x16, 0x17, 0x18
    /// </summary>
    private static readonly byte[] Telegram_Connect = new byte[] { 0x6D, 0x7B, 0xA7, 0x80, 0x80, 0x80, 0x80, 0x92, };

    /// <summary>
    /// Base Telegram for Device A - crypted
    /// 
    /// Header:   0x6D, 0xB6, 0x43, 0xCF, 0x7E, 0x8F, 0x47, 0x11, 
    /// ID:       0x84, 0x66, 0x59, 
    /// Channels: 0x38, 0xD1, 0x7A, 0xAA, 0x34, 0x67, 
    /// ID:       0x4A, 
    /// Checksum: 0x55, 0xBF, 
    /// Filler:   0x15, 0x16, 0x17, 0x18
    /// </summary>
    private static readonly byte[] Telegram_Base_DeviceA = new byte[] { 0x61, 0x7B, 0xA7, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x9E };

    /// <summary>
    /// Base Telegram for Device B - crypted
    /// 
    /// Header:   0x6D, 0xB6, 0x43, 0xCF, 0x7E, 0x8F, 0x47, 0x11, 
    /// ID:       0x87, 
    /// Channels: 0x66, 0x59, 0x38, 0xD1, 0x7A, 0xAA, 0x34, 0x67, 
    /// ID:       0x49, 
    /// Checksum: 0xA7, 0xF9, 
    /// Filler:   0x15, 0x16, 0x17, 0x18
    /// </summary>
    private static readonly byte[] Telegram_Base_DeviceB = new byte[] { 0x62, 0x7B, 0xA7, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x9D };

    /// <summary>
    /// Base Telegram for Device C - crypted
    /// 
    /// Header:   0x6D, 0xB6, 0x43, 0xCF, 0x7E, 0x8F, 0x47, 0x11, 
    /// ID:       0x86, 
    /// Channels: 0x66, 0x59, 0x38, 0xD1, 0x7A, 0xAA, 0x34, 0x67, 
    /// ID:       0x48, 
    /// Checksum: 0x09, 0xC4, 
    /// Filler:   0x15, 0x16, 0x17, 0x18
    /// </summary>
    private static readonly byte[] Telegram_Base_DeviceC = new byte[] { 0x63, 0x7B, 0xA7, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x9C };
    #endregion

    #region Fields
    #endregion
    #region Properties
    public override DeviceType DeviceType => DeviceType.MouldKing_6_0_Modul;
    #endregion

    #region Constructor
    public MouldKing_6_0_Modul(string name, string address, byte[] deviceData, IDeviceRepository deviceRepository, IBluetoothLEService bleService)
      : base(name, address, deviceData, deviceRepository, bleService, MouldKing_6_0_Modul.GetTelegramConnect(address), MouldKing_6_0_Modul.GetTelegramBase(address))
    {
    }
    #endregion

    #region GetTelegramConnect(string address)
    /// <summary>
    /// Gets the Connect-Telegram for the given adddress
    /// </summary>
    /// <param name="address">address</param>
    /// <returns>Connect-Telegram</returns>
    private static byte[] GetTelegramConnect(string address)
    {
      if (address == MouldKing_6_0_Modul.DeviceC)
      {
        return Telegram_Connect;
      }
      else if (address == MouldKing_6_0_Modul.DeviceB)
      {
        return Telegram_Connect;
      }
      else if (address == MouldKing_6_0_Modul.DeviceA)
      {
        return Telegram_Connect;
      }
      else
      {
        return Telegram_Connect;
      }
    }
    #endregion
    #region GetTelegramBase(string address)
    /// <summary>
    /// Gets the Base-Telegram for the given address
    /// </summary>
    /// <param name="address">address</param>
    /// <returns>Base-Telegram</returns>
    private static byte[] GetTelegramBase(string address)
    {
      if(address == MouldKing_6_0_Modul.DeviceC)
      {
        return Telegram_Base_DeviceC;
      }
      else if (address == MouldKing_6_0_Modul.DeviceB)
      {
        return Telegram_Base_DeviceB;
      }
      else if (address == MouldKing_6_0_Modul.DeviceA)
      {
        return Telegram_Base_DeviceA;
      }
      else
      {
        return Telegram_Base_DeviceA;
      }
    }
    #endregion
  }
}
