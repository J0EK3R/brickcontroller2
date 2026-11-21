using System;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Capabilities of the stick reported in HELLO_RSP (protocol.md section 5).
/// </summary>
/// <param name="ProtoMajor">Protocol major version of the firmware.</param>
/// <param name="ProtoMinor">Protocol minor version of the firmware.</param>
/// <param name="FirmwareVersion">Firmware version as text, e.g. "0.1.0".</param>
/// <param name="MaxSets">Number of advertising sets the stick can run in parallel.</param>
/// <param name="MaxDataWithFlags">Largest manufacturer data when the AD flags field is added.</param>
/// <param name="MaxDataNoFlags">Largest manufacturer data without the AD flags field.</param>
/// <param name="MinInterval">Smallest advertising interval in 0.625 ms units.</param>
/// <param name="MaxInterval">Largest advertising interval in 0.625 ms units.</param>
/// <param name="KeepAliveMs">Effective keep-alive timeout negotiated with the stick.</param>
/// <param name="DeviceId">Bluetooth MAC of the stick in display order, e.g. "24:0A:BC:11:22:33".</param>
/// <param name="GattFlags">HELLO_RSP gatt_flags (bit0 scan, bit1 GATT central, bit2 failsafe); 0 for a 1.0 stick.</param>
/// <param name="MaxConns">Number of GATT links the stick supports at the same time (0 = no GATT). Never assume a value.</param>
/// <param name="MaxValueLen">Largest GATT write / read / notification payload.</param>
/// <param name="MaxScanData">Largest AD data of one scan report.</param>
/// <param name="FailsafeSlots">Number of SET_FAILSAFE slots per connection.</param>
public sealed record StickInfo(
    byte ProtoMajor,
    byte ProtoMinor,
    string FirmwareVersion,
    byte MaxSets,
    byte MaxDataWithFlags,
    byte MaxDataNoFlags,
    ushort MinInterval,
    ushort MaxInterval,
    ushort KeepAliveMs,
    string DeviceId,
    byte GattFlags = 0,
    byte MaxConns = 0,
    byte MaxValueLen = 0,
    byte MaxScanData = 0,
    byte FailsafeSlots = 0)
{
    /// <summary>
    /// True when the stick can be used as GATT central: protocol 1.1 or newer, the central flag set and
    /// at least one connection slot. A 1.0 stick (27 byte HELLO_RSP) never qualifies.
    /// </summary>
    public bool SupportsGatt => ProtoMinor >= StickProtocol.GattMinor && (GattFlags & StickProtocol.GattFlagCentral) != 0 && MaxConns > 0;

    /// <summary>True when the stick can scan (and GATT is possible at all).</summary>
    public bool SupportsScan => SupportsGatt && (GattFlags & StickProtocol.GattFlagScan) != 0;

    /// <summary>True when SET_FAILSAFE is available.</summary>
    public bool SupportsFailsafe => SupportsGatt && (GattFlags & StickProtocol.GattFlagFailsafe) != 0 && FailsafeSlots > 0;
}

/// <summary>
/// Generic response of the stick: the original request type, a status and request specific extra bytes.
/// </summary>
internal sealed record StickAck(byte OriginalType, StickStatus Status, byte[] Extra);

/// <summary>
/// Kinds of asynchronous events sent by the stick.
/// </summary>
internal enum StickEventKind
{
    /// <summary>A set ended without a host request (SetId and Reason are valid).</summary>
    SetStopped,

    /// <summary>The user pressed the button on the stick; all sets were stopped, the session stays.</summary>
    ButtonPressed,

    /// <summary>The keep-alive timed out; all sets were stopped and the session ended.</summary>
    Timeout,
}

/// <summary>
/// Decoded asynchronous event (SET id and reason are only meaningful for <see cref="StickEventKind.SetStopped"/>).
/// </summary>
internal sealed record StickEvent(StickEventKind Kind, byte SetId = 0, byte Reason = 0, ushort TimeoutMs = 0);

/// <summary>
/// Response to PING and to GET_STATUS(0xFF).
/// </summary>
/// <param name="Advertising">True when at least one set is running.</param>
/// <param name="ActiveMask">Bit n set = set n is running.</param>
/// <param name="ParseErrors">Parse error counter of the stick.</param>
/// <param name="UptimeMs">Stick uptime in milliseconds.</param>
internal sealed record StickPong(bool Advertising, ushort ActiveMask, ushort ParseErrors, uint UptimeMs);

/// <summary>
/// Exception base for problems talking to the stick.
/// </summary>
public class StickException : Exception
{
    public StickException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

/// <summary>
/// The stick did not answer within the timeout (after all retries).
/// </summary>
public sealed class StickTimeoutException : StickException
{
    public StickTimeoutException(string message) : base(message)
    {
    }
}

/// <summary>
/// The serial transport is closed or failed (unplugged stick, write error).
/// </summary>
public sealed class StickTransportException : StickException
{
    public StickTransportException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

// ---------------------------------------------------------------------------------------------
// Protocol 1.1 (GATT) messages, see tasks/009-usb-stick-gatt-fw/plan.md section 3.
// ---------------------------------------------------------------------------------------------

/// <summary>Base type of all GATT / scan events and responses decoded from the stick.</summary>
internal abstract record StickGattMessage;

/// <summary>EVT_SCAN_REPORT: one advertising or scan response PDU seen by the stick.</summary>
/// <param name="Address">Peer address as "AA:BB:CC:DD:EE:FF" (display order).</param>
/// <param name="AddressType">0 public, 1 random.</param>
/// <param name="Rssi">Signal strength in dBm.</param>
/// <param name="Flags">bit0 connectable, bit1 scan response, bit2 directed.</param>
/// <param name="Data">Raw AD structures (at most 31 bytes).</param>
internal sealed record StickScanReport(string Address, byte AddressType, sbyte Rssi, byte Flags, byte[] Data) : StickGattMessage
{
    /// <summary>True when the PDU is a scan response (not the advertising PDU).</summary>
    public bool IsScanResponse => (Flags & 0x02) != 0;
}

/// <summary>EVT_CONNECTED: the link is usable (MTU exchange of the stick finished).</summary>
/// <param name="ConnId">Connection id chosen by the host in CONNECT.</param>
/// <param name="Mtu">Negotiated ATT MTU (23 if the exchange failed).</param>
/// <param name="Interval">Connection interval in 1.25 ms units.</param>
/// <param name="Address">Peer address.</param>
internal sealed record StickConnected(byte ConnId, ushort Mtu, ushort Interval, string Address) : StickGattMessage;

/// <summary>EVT_DISCONNECTED: the link or the connect attempt ended.</summary>
/// <param name="ConnId">Connection id.</param>
/// <param name="WasConnected">False if the attempt was still connecting.</param>
/// <param name="Reason">HCI error code or a 0xF0.. stick specific reason.</param>
internal sealed record StickDisconnected(byte ConnId, bool WasConnected, byte Reason) : StickGattMessage;

/// <summary>EVT_NOTIFY: notification or indication from a characteristic.</summary>
/// <param name="ConnId">Connection id.</param>
/// <param name="Handle">Value handle of the characteristic.</param>
/// <param name="Flags">bit0 indication, bit1 data truncated.</param>
/// <param name="Data">The value.</param>
internal sealed record StickNotify(byte ConnId, ushort Handle, byte Flags, byte[] Data) : StickGattMessage;

/// <summary>EVT_GATT_SERVICE: one primary service found by DISCOVER.</summary>
internal sealed record StickGattServiceEvent(byte ConnId, ushort StartHandle, ushort EndHandle, Guid Uuid) : StickGattMessage;

/// <summary>EVT_GATT_CHAR: one characteristic found by DISCOVER (cccd 0 = none).</summary>
internal sealed record StickGattCharEvent(byte ConnId, ushort ValueHandle, ushort CccdHandle, byte Properties, Guid Uuid) : StickGattMessage;

/// <summary>EVT_DISCOVER_DONE: end of a DISCOVER run.</summary>
internal sealed record StickDiscoverDone(byte ConnId, StickStatus Status, byte ServiceCount, byte CharCount) : StickGattMessage;

/// <summary>EVT_SCAN_STOPPED: the scan ended without a host request (1 controller error, 2 safety/button).</summary>
internal sealed record StickScanStopped(byte Reason, ushort Rc) : StickGattMessage;

/// <summary>EVT_DROPPED: the stick had to drop frames (kind 1 = scan reports, 2 = notifications).</summary>
internal sealed record StickDropped(byte Kind, ushort Count) : StickGattMessage;

/// <summary>READ_RSP: value of a characteristic read.</summary>
internal sealed record StickReadResponse(byte ConnId, ushort Handle, bool Truncated, byte[] Data) : StickGattMessage;

/// <summary>GATT_STATUS_RSP: scan and link summary of the stick (masks: bit n = conn_id n).</summary>
internal sealed record StickGattStatus(bool Scanning, ushort ConnectingMask, ushort ConnectedMask, ushort DroppedScan, ushort DroppedNotify) : StickGattMessage;

/// <summary>The request failed with an error status of the stick (ATT error code in <see cref="AttError"/> when relevant).</summary>
public sealed class StickRequestException : StickException
{
    internal StickRequestException(string message, StickStatus status, byte attError = 0) : base(message)
    {
        Status = status;
        AttError = attError;
    }

    internal StickStatus Status { get; }

    internal byte AttError { get; }
}
