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

    /// <summary>Converts "B2 06 ..." into bytes.</summary>
    public static byte[] Bytes(string hex) => Convert.FromHexString(hex.Replace(" ", string.Empty));
}
