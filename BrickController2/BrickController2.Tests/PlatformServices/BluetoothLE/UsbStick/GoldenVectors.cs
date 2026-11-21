using System;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Golden frames of protocol.md section 10 (workspace/ble-usb-advertiser/docs/protocol.md), as hex strings.
/// </summary>
internal static class GoldenVectors
{
    public const string Hello = "B2 06 01 01 01 00 E8 03 D6 60";
    public const string Ping = "B2 02 02 02 DC E4";
    public const string SetStart = "B2 12 10 03 00 01 20 00 7F F0 FF 08 01 02 03 04 05 06 07 08 F5 48";
    public const string SetUpdate = "B2 0A 11 04 00 F0 FF 04 A1 A2 A3 A4 52 B1";
    public const string SetStop = "B2 03 12 05 00 EA CD";
    public const string StopAll = "B2 02 13 06 1A 94";
    public const string GetStatus = "B2 03 14 07 FF D8 07";
    public const string HelloRsp = "B2 1D 81 01 42 43 32 41 44 56 00 01 00 00 01 00 05 18 1B 20 00 00 40 E8 03 24 0A BC 11 22 33 40 AD";
    public const string AckSetStart = "B2 05 80 03 10 00 7F 06 38";
    public const string AckSetUpdate = "B2 08 80 04 11 00 7B 03 00 00 D4 57";
    public const string Pong = "B2 0B 82 02 01 01 00 00 00 10 27 00 00 5C D9";
    public const string EvtTimeout = "B2 04 E2 00 E8 03 F0 58";

    // ---- protocol 1.1 (GATT). Derived from tasks/009-usb-stick-gatt-fw/plan.md section 3 with an independent
    // Python implementation of the frame format and CRC; replace with the vectors of the firmware's
    // docs/protocol.md once they are published (the byte layout is the contract). ----
    public const string Hello11 = "B2 06 01 01 01 01 E8 03 E6 57";
    public const string ScanStart = "B2 09 30 08 01 A0 00 50 00 E8 03 E5 AB";
    public const string ScanStop = "B2 02 31 09 71 05";
    public const string Connect = "B2 11 32 0A 02 AA BB CC DD EE FF 01 00 10 27 0C 00 18 00 66 FA";
    public const string Disconnect = "B2 03 33 0B 02 51 7F";
    public const string Discover = "B2 03 34 0C 02 56 63";
    public const string Read = "B2 05 35 0D 02 12 00 5F 50";
    public const string Write = "B2 09 36 0E 02 14 00 01 02 03 04 69 85";
    public const string WriteCmd = "B2 07 37 0F 02 14 00 A0 B0 9C 5C";
    public const string Subscribe = "B2 06 38 10 02 15 00 01 A5 23";
    public const string GetGattStatus = "B2 02 39 11 E1 1F";
    public const string SetFailsafe = "B2 09 3A 12 02 01 14 00 00 00 00 A0 C5";
    public const string HelloRsp11 = "B2 22 81 01 42 43 32 41 44 56 00 01 01 00 01 00 05 18 1B 20 00 00 40 E8 03 24 0A BC 11 22 33 07 06 38 1F 04 DA 35";
    public const string EvtNotify = "B2 09 C0 00 02 14 00 00 11 22 33 7D 4A";
    public const string EvtConnected = "B2 0D C1 00 02 40 00 18 00 AA BB CC DD EE FF E4 CC";
    public const string EvtDisconnected = "B2 05 C2 00 02 01 13 29 45";
    public const string EvtScanReport = "B2 11 C3 00 AA BB CC DD EE FF 01 C4 03 05 02 01 06 03 09 6E EA";
    public const string EvtGattService = "B2 0A C4 00 02 01 00 09 00 02 0A 18 00 60";
    public const string EvtGattChar = "B2 0B C5 00 02 12 00 13 00 1A 02 24 16 17 DB";
    public const string EvtGattChar128 = "B2 19 C5 00 02 12 00 13 00 1A 10 23 C1 E4 A1 B2 C3 D4 E5 F6 07 18 29 3A 4B 5C 6D 84 12";
    public const string EvtDiscoverDone = "B2 06 C6 00 02 00 03 07 E6 AB";
    public const string EvtScanStopped = "B2 05 C7 00 02 00 00 1D 77";
    public const string EvtDropped = "B2 05 C8 00 02 05 00 11 ED";
    public const string ReadRsp = "B2 09 A0 0D 02 12 00 00 31 2E 30 15 9A";
    public const string GattStatusRsp = "B2 0B A1 11 01 02 00 05 00 03 00 04 00 18 EA";
    public const string AckAttError = "B2 05 80 0E 36 0E 03 F2 A9";

    /// <summary>Converts "B2 06 ..." into bytes.</summary>
    public static byte[] Bytes(string hex) => Convert.FromHexString(hex.Replace(" ", string.Empty));
}
