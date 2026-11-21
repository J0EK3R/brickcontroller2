// Diagnostic trace of the USB stick GATT path (stick connection, link, device and the BC2 connect flow).
// Output goes to System.Diagnostics.Debug, i.e. to the "Output" window of Visual Studio (Debug builds only).

using System;
using System.Diagnostics;
using System.Text;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;

namespace BrickController2.Diagnostics;

/// <summary>
/// Central place for the "[USBSTICK]" diagnostic lines. Every call site uses <see cref="Write"/>, which is marked
/// <c>[Conditional("DEBUG")]</c>: in Release builds the call (including the evaluation of its arguments) is removed
/// by the compiler, so the trace is completely silent and costs nothing. In Debug builds a line looks like
/// <c>[USBSTICK] 14:03:21.457 t12 conn 0: TX CONNECT seq=5 ...</c> (time with milliseconds, managed thread id).
/// The helpers below only format values (frame names, HCI reasons, hex dumps) and are safe to call anywhere.
/// </summary>
internal static class UsbStickTrace
{
    /// <summary>Prefix of every line, easy to filter for in the Visual Studio Output window.</summary>
    public const string Prefix = "[USBSTICK]";

    /// <summary>Writes one trace line to the debugger output (Debug builds only).</summary>
    /// <param name="message">Free text; callers include the conn_id where one exists.</param>
    [Conditional("DEBUG")]
    public static void Write(string message)
        => Debug.WriteLine($"{Prefix} {DateTime.Now:HH:mm:ss.fff} t{Environment.CurrentManagedThreadId} {message}");

    /// <summary>Hex dump of at most <paramref name="max"/> bytes, with the total length and "..." when cut.</summary>
    public static string Hex(byte[]? data, int max = 32)
    {
        if (data == null)
        {
            return "null";
        }

        int shown = Math.Min(data.Length, max);
        string hex = Convert.ToHexString(data, 0, shown);
        return data.Length > shown ? $"[{data.Length}] {hex}..." : $"[{data.Length}] {hex}";
    }

    /// <summary>Name of a stick frame type byte (protocol.md section 3), "0x.." when unknown.</summary>
    public static string FrameName(byte type) => type switch
    {
        StickProtocol.TypeHello => "HELLO",
        StickProtocol.TypePing => "PING",
        StickProtocol.TypeSetStart => "SET_START",
        StickProtocol.TypeSetUpdate => "SET_UPDATE",
        StickProtocol.TypeSetStop => "SET_STOP",
        StickProtocol.TypeStopAll => "STOP_ALL",
        StickProtocol.TypeGetStatus => "GET_STATUS",
        StickProtocol.TypeScanStart => "SCAN_START",
        StickProtocol.TypeScanStop => "SCAN_STOP",
        StickProtocol.TypeConnect => "CONNECT",
        StickProtocol.TypeDisconnect => "DISCONNECT",
        StickProtocol.TypeDiscover => "DISCOVER",
        StickProtocol.TypeRead => "READ",
        StickProtocol.TypeWrite => "WRITE",
        StickProtocol.TypeWriteCmd => "WRITE_CMD",
        StickProtocol.TypeSubscribe => "SUBSCRIBE",
        StickProtocol.TypeGetGattStatus => "GET_GATT_STATUS",
        StickProtocol.TypeSetFailsafe => "SET_FAILSAFE",
        StickProtocol.TypeReadRsp => "READ_RSP",
        StickProtocol.TypeGattStatusRsp => "GATT_STATUS_RSP",
        StickProtocol.TypeEvtNotify => "EVT_NOTIFY",
        StickProtocol.TypeEvtConnected => "EVT_CONNECTED",
        StickProtocol.TypeEvtDisconnected => "EVT_DISCONNECTED",
        StickProtocol.TypeEvtScanReport => "EVT_SCAN_REPORT",
        StickProtocol.TypeEvtGattService => "EVT_GATT_SERVICE",
        StickProtocol.TypeEvtGattChar => "EVT_GATT_CHAR",
        StickProtocol.TypeEvtDiscoverDone => "EVT_DISCOVER_DONE",
        StickProtocol.TypeEvtScanStopped => "EVT_SCAN_STOPPED",
        StickProtocol.TypeEvtDropped => "EVT_DROPPED",
        StickProtocol.TypeAck => "ACK",
        StickProtocol.TypeHelloRsp => "HELLO_RSP",
        StickProtocol.TypePong => "PONG",
        StickProtocol.TypeSetStatusRsp => "SET_STATUS_RSP",
        StickProtocol.TypeEvtSetStopped => "EVT_SET_STOPPED",
        StickProtocol.TypeEvtButton => "EVT_BUTTON",
        StickProtocol.TypeEvtTimeout => "EVT_TIMEOUT",
        _ => $"0x{type:X2}",
    };

    /// <summary>
    /// Decoded disconnect reason: the stick's own 0xF0.. reasons or the HCI error code (Bluetooth Core spec,
    /// Vol 1 Part F). Format: "0x08 (connection timeout / supervision timeout)".
    /// </summary>
    public static string ReasonName(byte reason)
    {
        string name = reason switch
        {
            StickProtocol.ReasonConnectTimeout => "stick: connect timeout, peer not reachable",
            StickProtocol.ReasonCancelled => "stick: cancelled by host",
            StickProtocol.ReasonSafetyStop => "stick: safety stop / session lost",
            StickProtocol.ReasonStickError => "stick: internal error",
            0x00 => "success",
            0x05 => "authentication failure",
            0x06 => "PIN or key missing",
            0x08 => "connection timeout (supervision timeout)",
            0x0C => "command disallowed",
            0x13 => "remote user terminated connection",
            0x14 => "remote device terminated: low resources",
            0x15 => "remote device terminated: power off",
            0x16 => "connection terminated by local host",
            0x1A => "unsupported remote feature",
            0x1F => "unspecified error",
            0x22 => "LL response timeout",
            0x28 => "instant passed",
            0x2A => "different transaction collision",
            0x3A => "controller busy",
            0x3B => "unacceptable connection parameters",
            0x3C => "advertising timeout",
            0x3D => "connection terminated due to MIC failure",
            0x3E => "connection failed to be established",
            _ => "unknown",
        };

        return $"0x{reason:X2} ({name})";
    }

    /// <summary>Characteristic property bits (GATT) as text, e.g. "READ|WRITE_NR|NOTIFY".</summary>
    public static string PropertyNames(byte properties)
    {
        var text = new StringBuilder();
        (byte Bit, string Name)[] bits =
        [
            (0x01, "BROADCAST"), (0x02, "READ"), (0x04, "WRITE_NR"), (0x08, "WRITE"),
            (0x10, "NOTIFY"), (0x20, "INDICATE"), (0x40, "SIGNED_WRITE"), (0x80, "EXT_PROPS"),
        ];

        foreach (var (bit, name) in bits)
        {
            if ((properties & bit) != 0)
            {
                text.Append(text.Length > 0 ? "|" : string.Empty).Append(name);
            }
        }

        return text.Length > 0 ? text.ToString() : "none";
    }
}
