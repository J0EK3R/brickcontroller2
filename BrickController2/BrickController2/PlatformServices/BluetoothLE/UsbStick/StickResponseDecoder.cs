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
            DeviceId: string.Join(':', p.Slice(21, 6).ToArray().Select(b => b.ToString("X2"))));
        return true;
    }

    /// <summary>Decodes an ACK frame (original type, status, extra bytes).</summary>
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

    /// <summary>Decodes a PONG frame.</summary>
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
}
