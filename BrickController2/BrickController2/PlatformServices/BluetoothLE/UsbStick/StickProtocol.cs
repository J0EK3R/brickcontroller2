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

    // ---- protocol 1.1 (GATT) ----

    /// <summary>A procedure is already running on this connection, or the controller has no buffer.</summary>
    Busy = 11,

    /// <summary>The conn_id has no connected link.</summary>
    NotConnected = 12,

    /// <summary>A GATT procedure did not finish within 5 s (the stick also terminates the link).</summary>
    Timeout = 13,

    /// <summary>The peer answered with an ATT error (ACK extra = att_error).</summary>
    AttError = 14,

    /// <summary>The link was lost while the request was pending.</summary>
    Disconnected = 15,

    /// <summary>The feature is disabled in this firmware build.</summary>
    NotSupported = 16,
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

    // GATT requests (protocol 1.1, host -> stick)
    public const byte TypeScanStart = 0x30;
    public const byte TypeScanStop = 0x31;
    public const byte TypeConnect = 0x32;
    public const byte TypeDisconnect = 0x33;
    public const byte TypeDiscover = 0x34;
    public const byte TypeRead = 0x35;
    public const byte TypeWrite = 0x36;
    public const byte TypeWriteCmd = 0x37;
    public const byte TypeSubscribe = 0x38;
    public const byte TypeGetGattStatus = 0x39;
    public const byte TypeSetFailsafe = 0x3A;

    // GATT responses (stick -> host)
    public const byte TypeReadRsp = 0xA0;
    public const byte TypeGattStatusRsp = 0xA1;

    // GATT events (stick -> host, SEQ 0)
    public const byte TypeEvtNotify = 0xC0;
    public const byte TypeEvtConnected = 0xC1;
    public const byte TypeEvtDisconnected = 0xC2;
    public const byte TypeEvtScanReport = 0xC3;
    public const byte TypeEvtGattService = 0xC4;
    public const byte TypeEvtGattChar = 0xC5;
    public const byte TypeEvtDiscoverDone = 0xC6;
    public const byte TypeEvtScanStopped = 0xC7;
    public const byte TypeEvtDropped = 0xC8;

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
    public const byte ProtoMinor = 1;

    /// <summary>First protocol minor version that contains the GATT messages.</summary>
    public const byte GattMinor = 1;

    /// <summary>HELLO_RSP gatt_flags bit0: the stick can scan.</summary>
    public const byte GattFlagScan = 0x01;

    /// <summary>HELLO_RSP gatt_flags bit1: the stick can act as GATT central.</summary>
    public const byte GattFlagCentral = 0x02;

    /// <summary>HELLO_RSP gatt_flags bit2: the stick supports SET_FAILSAFE.</summary>
    public const byte GattFlagFailsafe = 0x04;

    /// <summary>conn_id / addr_type value "auto": the stick picks the address type from its scan cache.</summary>
    public const byte AddressTypeAuto = 0xFF;

    /// <summary>CONNECT flags bit0: background connect (no timeout, runs until connected or DISCONNECT).</summary>
    public const byte ConnectFlagBackground = 0x01;

    /// <summary>SCAN_START flags bit0: active scan (request scan responses).</summary>
    public const byte ScanFlagActive = 0x01;

    /// <summary>EVT_DISCONNECTED reason: connect attempt timed out on the stick.</summary>
    public const byte ReasonConnectTimeout = 0xF0;

    /// <summary>EVT_DISCONNECTED reason: cancelled by DISCONNECT while connecting.</summary>
    public const byte ReasonCancelled = 0xF1;

    /// <summary>EVT_DISCONNECTED reason: stopped by the stick's safety logic (timeout, HELLO, button).</summary>
    public const byte ReasonSafetyStop = 0xF2;

    /// <summary>EVT_DISCONNECTED reason: stick internal error (procedure timeout, discovery failure).</summary>
    public const byte ReasonStickError = 0xF3;

    /// <summary>MTU assumed when the stick reports none (ATT default).</summary>
    public const int DefaultMtu = 23;

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
