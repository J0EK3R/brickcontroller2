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

    // ---- Golden vectors of the firmware (workspace/ble-usb-advertiser docs/protocol.md section 18, branch task/009-usb-stick-gatt-fw),
    // verified byte-exact by the firmware's own C and Python tests. ----
    public const string FwScanStartDefaults = "B2 09 30 08 01 00 00 00 00 00 00 8E 96";
    public const string FwScanStartDup = "B2 09 30 09 03 A0 00 50 00 E8 03 D5 8C";
    public const string FwScanStop = "B2 02 31 0A 12 35";
    public const string FwConnectBackground = "B2 11 32 0B 00 90 84 2B 01 02 03 FF 01 00 00 00 00 00 00 D0 EB";
    public const string FwConnectRandom = "B2 11 32 0C 02 AA BB CC DD EE FF 01 00 88 13 0C 00 18 00 D3 7C";
    public const string FwDisconnect = "B2 03 33 0D 01 94 E5";
    public const string FwDiscover = "B2 03 34 0E 01 57 35";
    public const string FwRead = "B2 05 35 0F 01 12 00 67 E4";
    public const string FwWriteDuplo = "B2 0D 36 10 00 10 00 08 00 81 00 11 51 00 00 03 58";
    public const string FwWriteCmdMkDiy = "B2 17 37 11 01 2A 00 CC AA BB 01 80 80 80 80 80 80 80 80 80 80 80 80 66 33 83 62";
    public const string FwWriteCmd56 = "B2 3D 37 12 01 2A 00 00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F 10 11 12 13 14 15 16 17 18 19 1A 1B 1C 1D 1E 1F 20 21 22 23 24 25 26 27 28 29 2A 2B 2C 2D 2E 2F 30 31 32 33 34 35 36 37 23 F4";
    public const string FwSubscribe = "B2 06 38 13 00 11 00 01 DF FC";
    public const string FwGetGattStatus = "B2 02 39 14 44 4F";
    public const string FwSetFailsafe = "B2 18 3A 15 01 00 2A 00 CC AA BB 01 80 80 80 80 80 80 80 80 80 80 80 80 66 33 34 1C";
    public const string FwSetFailsafeClear = "B2 06 3A 16 01 01 2A 00 13 98";
    public const string FwReadRsp = "B2 08 A0 0F 01 12 00 00 A1 B2 22 91";
    public const string FwGattStatusRsp = "B2 13 A1 14 01 04 00 03 00 02 00 00 00 00 80 02 00 00 40 01 00 56 F8";
    public const string FwAckAttError = "B2 05 80 10 36 0E 03 0F 10";
    public const string FwAckConnect = "B2 04 80 0B 32 00 34 D6";
    public const string FwEvtNotify = "B2 0B C0 00 00 11 00 00 04 00 45 00 01 AC 42";
    public const string FwEvtConnected = "B2 0D C1 00 00 40 00 18 00 90 84 2B 01 02 03 B0 12";
    public const string FwEvtDisconnected = "B2 05 C2 00 00 01 08 13 88";
    public const string FwEvtScanReport = "B2 19 C3 00 90 84 2B 01 02 03 01 C4 01 0D 02 01 06 09 FF 97 03 00 41 20 00 00 00 B9 69";
    public const string FwEvtServiceLego = "B2 18 C4 00 00 10 00 20 00 10 23 D1 BC EA 5F 78 23 16 DE EF 12 12 23 16 00 00 FB 31";
    public const string FwEvtService180A = "B2 0A C4 00 00 30 00 3F 00 02 0A 18 21 CF";
    public const string FwEvtChar = "B2 19 C5 00 00 11 00 12 00 1E 10 23 D1 BC EA 5F 78 23 16 DE EF 12 12 24 16 00 00 8F 18";
    public const string FwEvtDiscoverDone = "B2 06 C6 00 00 00 02 05 FD 55";
    public const string FwEvtScanStopped = "B2 05 C7 00 01 0F 00 73 3E";
    public const string FwEvtDropped = "B2 05 C8 00 02 2A 00 C9 FB";

    /// <summary>Converts "B2 06 ..." into bytes.</summary>
    public static byte[] Bytes(string hex) => Convert.FromHexString(hex.Replace(" ", string.Empty));
}
