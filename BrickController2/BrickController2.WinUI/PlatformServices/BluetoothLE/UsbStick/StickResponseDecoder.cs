using System;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Converts received frames into typed messages (protocol.md section 5). Unknown trailing bytes
/// in responses and unknown event types are ignored on purpose (forward compatibility).
/// </summary>
internal static class StickResponseDecoder
{
    private const int HelloPayloadLength = 27;
    private const int HelloGattOffset = 27;
    private const int PongPayloadLength = 9;

    /// <summary>
    /// Decodes a HELLO_RSP. Fails when the frame has another type, is too short or the magic is wrong
    /// (which means the port belongs to a different device).
    /// </summary>
    public static bool TryDecodeHello(StickFrame frame, [NotNullWhen(true)] out StickInfo? info)
    {
        info = null;
        ReadOnlySpan<byte> p = frame.Payload;

        if (frame.Type != StickProtocol.TypeHelloRsp ||
            p.Length < HelloPayloadLength ||
            !p[..StickProtocol.HelloMagic.Length].SequenceEqual(StickProtocol.HelloMagic) ||
            p[6] != (byte)StickStatus.Ok)
        {
            return false;
        }

        info = new StickInfo(
            ProtoMajor: p[7],
            ProtoMinor: p[8],
            FirmwareVersion: $"{p[9]}.{p[10]}.{p[11]}",
            MaxSets: p[12],
            MaxDataWithFlags: p[13],
            MaxDataNoFlags: p[14],
            MinInterval: BinaryPrimitives.ReadUInt16LittleEndian(p[15..]),
            MaxInterval: BinaryPrimitives.ReadUInt16LittleEndian(p[17..]),
            KeepAliveMs: BinaryPrimitives.ReadUInt16LittleEndian(p[19..]),
            DeviceId: string.Join(':', p.Slice(21, 6).ToArray().Select(b => b.ToString("X2"))),
            // Protocol 1.1 adds 5 bytes; a 27 byte answer of a 1.0 stick leaves them 0 (= no GATT).
            GattFlags: p.Length > HelloGattOffset ? p[HelloGattOffset] : (byte)0,
            MaxConns: p.Length > HelloGattOffset + 1 ? p[HelloGattOffset + 1] : (byte)0,
            MaxValueLen: p.Length > HelloGattOffset + 2 ? p[HelloGattOffset + 2] : (byte)0,
            MaxScanData: p.Length > HelloGattOffset + 3 ? p[HelloGattOffset + 3] : (byte)0,
            FailsafeSlots: p.Length > HelloGattOffset + 4 ? p[HelloGattOffset + 4] : (byte)0);
        return true;
    }

    /// <summary>
    /// Decodes an ACK frame (original type, status, extra bytes).
    /// </summary>
    public static bool TryDecodeAck(StickFrame frame, [NotNullWhen(true)] out StickAck? ack)
    {
        ack = null;
        if (frame.Type != StickProtocol.TypeAck || frame.Payload.Length < 2)
        {
            return false;
        }

        ack = new StickAck(frame.Payload[0], (StickStatus)frame.Payload[1], frame.Payload[2..]);
        return true;
    }

    /// <summary>
    /// Decodes a PONG frame.
    /// </summary>
    public static bool TryDecodePong(StickFrame frame, [NotNullWhen(true)] out StickPong? pong)
    {
        pong = null;
        ReadOnlySpan<byte> p = frame.Payload;
        if (frame.Type != StickProtocol.TypePong || p.Length < PongPayloadLength)
        {
            return false;
        }

        pong = new StickPong(
            Advertising: p[0] == 1,
            ActiveMask: BinaryPrimitives.ReadUInt16LittleEndian(p[1..]),
            ParseErrors: BinaryPrimitives.ReadUInt16LittleEndian(p[3..]),
            UptimeMs: BinaryPrimitives.ReadUInt32LittleEndian(p[5..]));
        return true;
    }

    /// <summary>
    /// Decodes an asynchronous event. Unknown event types (including those from reserved ranges) return false.
    /// </summary>
    public static bool TryDecodeEvent(StickFrame frame, [NotNullWhen(true)] out StickEvent? stickEvent)
    {
        stickEvent = null;
        ReadOnlySpan<byte> p = frame.Payload;

        switch (frame.Type)
        {
            case StickProtocol.TypeEvtSetStopped when p.Length >= 2:
                stickEvent = new StickEvent(StickEventKind.SetStopped, SetId: p[0], Reason: p[1]);
                return true;

            case StickProtocol.TypeEvtButton:
                stickEvent = new StickEvent(StickEventKind.ButtonPressed);
                return true;

            case StickProtocol.TypeEvtTimeout when p.Length >= 2:
                stickEvent = new StickEvent(StickEventKind.Timeout, TimeoutMs: BinaryPrimitives.ReadUInt16LittleEndian(p));
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Decodes a protocol 1.1 event or response (EVT_NOTIFY .. EVT_DROPPED, READ_RSP, GATT_STATUS_RSP).
    /// Malformed payloads and unknown types return false (the caller ignores them).
    /// </summary>
    public static bool TryDecodeGatt(StickFrame frame, [NotNullWhen(true)] out StickGattMessage? message)
    {
        byte[] x = frame.Payload;
        message = frame.Type switch
        {
            StickProtocol.TypeEvtNotify when x.Length >= 4 => new StickNotify(x[0], U16(x, 1), x[3], x[4..].ToArray()),
            StickProtocol.TypeEvtConnected when x.Length >= 11 => new StickConnected(x[0], U16(x, 1), U16(x, 3), StickAddress.Format(x.AsSpan(5, 6))),
            StickProtocol.TypeEvtDisconnected when x.Length >= 3 => new StickDisconnected(x[0], x[1] == 1, x[2]),
            StickProtocol.TypeEvtScanReport => DecodeScanReport(x),
            StickProtocol.TypeEvtGattService => DecodeService(x),
            StickProtocol.TypeEvtGattChar => DecodeChar(x),
            StickProtocol.TypeEvtDiscoverDone when x.Length >= 4 => new StickDiscoverDone(x[0], (StickStatus)x[1], x[2], x[3]),
            StickProtocol.TypeEvtScanStopped when x.Length >= 3 => new StickScanStopped(x[0], U16(x, 1)),
            StickProtocol.TypeEvtDropped when x.Length >= 3 => new StickDropped(x[0], U16(x, 1)),
            StickProtocol.TypeReadRsp when x.Length >= 4 => new StickReadResponse(x[0], U16(x, 1), (x[3] & 1) != 0, x[4..].ToArray()),
            StickProtocol.TypeGattStatusRsp when x.Length >= 9 => new StickGattStatus(x[0] != 0, U16(x, 1), U16(x, 3), U16(x, 5), U16(x, 7)),
            _ => null,
        };

        return message != null;
    }

    private static ushort U16(byte[] data, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset));

    /// <summary>
    /// EVT_SCAN_REPORT: addr[6], addr_type, rssi, flags, data_len, data.
    /// </summary>
    private static StickScanReport? DecodeScanReport(byte[] x)
    {
        if (x.Length < 10 || x.Length < 10 + x[9])
        {
            return null;
        }

        return new StickScanReport(StickAddress.Format(x.AsSpan(0, 6)), x[6], (sbyte)x[7], x[8], x.AsSpan(10, x[9]).ToArray());
    }

    /// <summary>
    /// EVT_GATT_SERVICE: conn_id, start, end, uuid_len, uuid.
    /// </summary>
    private static StickGattServiceEvent? DecodeService(byte[] x)
    {
        if (x.Length < 6 || x.Length < 6 + x[5] || !StickUuid.TryFromWire(x.AsSpan(6, x[5]), out Guid uuid))
        {
            return null;
        }

        return new StickGattServiceEvent(x[0], U16(x, 1), U16(x, 3), uuid);
    }

    /// <summary>
    /// EVT_GATT_CHAR: conn_id, value_handle (1), cccd_handle (3), props (5), uuid_len (6), uuid (7..).
    /// </summary>
    private static StickGattCharEvent? DecodeChar(byte[] x)
    {
        if (x.Length < 7 || x.Length < 7 + x[6] || !StickUuid.TryFromWire(x.AsSpan(7, x[6]), out Guid uuid))
        {
            return null;
        }

        return new StickGattCharEvent(x[0], U16(x, 1), U16(x, 3), x[5], uuid);
    }
}
