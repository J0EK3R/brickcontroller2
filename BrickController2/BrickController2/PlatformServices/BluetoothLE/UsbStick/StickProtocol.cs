using System;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Status codes returned by the stick in ACK frames (protocol.md section 6).
/// </summary>
internal enum StickStatus : byte
{
    Ok = 0,
    UnknownCmd = 1,
    BadLength = 2,
    BadParam = 3,
    NoSession = 4,
    VersionUnsupported = 5,
    AlreadyRunning = 6,
    SetNotFound = 7,
    PayloadTooLarge = 8,
    BleError = 9,
    NoResources = 10,
}

/// <summary>
/// Constants of the serial protocol 1.0 between BrickController2 and the USB BLE advertising stick
/// (see workspace/ble-usb-advertiser/docs/protocol.md, which is the contract).
/// </summary>
internal static class StickProtocol
{
    // ---- framing (section 2) ----

    /// <summary>Start-of-frame marker.</summary>
    public const byte Sof = 0xB2;

    /// <summary>Smallest valid LEN value (TYPE + SEQ).</summary>
    public const int MinLen = 2;

    /// <summary>Largest valid LEN value (TYPE + SEQ + 60 payload bytes).</summary>
    public const int MaxLen = 62;

    /// <summary>Bytes on the wire in addition to LEN: SOF, LEN itself and the 2 CRC bytes.</summary>
    public const int FrameOverhead = 4;

    /// <summary>Largest payload of one frame.</summary>
    public const int MaxPayload = MaxLen - 2;

    /// <summary>A frame that is incomplete after this time is dropped by the parser.</summary>
    public static readonly TimeSpan FrameTimeout = TimeSpan.FromMilliseconds(200);

    // ---- message types (section 3) ----

    public const byte TypeHello = 0x01;
    public const byte TypePing = 0x02;
    public const byte TypeSetStart = 0x10;
    public const byte TypeSetUpdate = 0x11;
    public const byte TypeSetStop = 0x12;
    public const byte TypeStopAll = 0x13;
    public const byte TypeGetStatus = 0x14;
    public const byte TypeAck = 0x80;
    public const byte TypeHelloRsp = 0x81;
    public const byte TypePong = 0x82;
    public const byte TypeSetStatusRsp = 0x83;
    public const byte TypeEvtSetStopped = 0xE0;
    public const byte TypeEvtButton = 0xE1;
    public const byte TypeEvtTimeout = 0xE2;

    // ---- protocol parameters ----

    /// <summary>Protocol major version implemented by this host.</summary>
    public const byte ProtoMajor = 1;

    /// <summary>Protocol minor version implemented by this host.</summary>
    public const byte ProtoMinor = 0;

    /// <summary>Keep-alive timeout requested in HELLO (the stick answers with the effective value).</summary>
    public const ushort RequestedKeepAliveMs = 1000;

    /// <summary>Magic at the start of HELLO_RSP that identifies the firmware on any COM port.</summary>
    public static readonly byte[] HelloMagic = "BC2ADV"u8.ToArray();

    /// <summary>AD flags field enabled (like Android); bits 1-2 = 0 select ADV_IND.</summary>
    public const byte SetFlagsWithAdFlags = 0x01;

    /// <summary>Maximum manufacturer data bytes when the AD flags field is enabled.</summary>
    public const int MaxDataWithFlags = 24;

    /// <summary>TX power value meaning "maximum supported by the controller".</summary>
    public const sbyte TxPowerMax = 127;

    /// <summary>set_id value of GET_STATUS that selects the device summary.</summary>
    public const byte StatusSummary = 0xFF;
}
