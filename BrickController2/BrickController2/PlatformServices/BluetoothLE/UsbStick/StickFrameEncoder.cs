using System;
using System.Buffers.Binary;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Builds request frames and request payloads of the stick protocol (protocol.md sections 2 and 4).
/// All multi-byte integers are little-endian.
/// </summary>
internal static class StickFrameEncoder
{
    /// <summary>
    /// Wraps a payload into a wire frame: SOF, LEN, TYPE, SEQ, PAYLOAD, CRC16 (low byte first).
    /// </summary>
    /// <param name="type">Message type.</param>
    /// <param name="seq">Sequence number (1..255, 0 is reserved for events).</param>
    /// <param name="payload">Payload, at most 60 bytes.</param>
    /// <returns>The bytes to write to the serial port.</returns>
    public static byte[] Encode(byte type, byte seq, ReadOnlySpan<byte> payload)
    {
        if (payload.Length > StickProtocol.MaxPayload)
        {
            throw new ArgumentException("Payload too large for one frame.", nameof(payload));
        }

        int len = payload.Length + 2; // TYPE + SEQ + PAYLOAD
        var frame = new byte[len + StickProtocol.FrameOverhead];
        frame[0] = StickProtocol.Sof;
        frame[1] = (byte)len;
        frame[2] = type;
        frame[3] = seq;
        payload.CopyTo(frame.AsSpan(4));

        // The CRC covers LEN, TYPE, SEQ and PAYLOAD (everything between SOF and the CRC).
        ushort crc = Crc16.Compute(frame.AsSpan(1, len + 1));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(len + 2), crc);
        return frame;
    }

    /// <summary>HELLO payload: proto_major, proto_minor, keepalive_ms.</summary>
    public static byte[] HelloPayload(byte protoMajor, byte protoMinor, ushort keepAliveMs)
    {
        var payload = new byte[4];
        payload[0] = protoMajor;
        payload[1] = protoMinor;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2), keepAliveMs);
        return payload;
    }

    /// <summary>SET_START payload: set_id, flags, interval, tx_power, mfr_id, data_len, data.</summary>
    public static byte[] SetStartPayload(byte setId, byte flags, ushort interval, sbyte txPower, ushort manufacturerId, ReadOnlySpan<byte> data)
    {
        var payload = new byte[8 + data.Length];
        payload[0] = setId;
        payload[1] = flags;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2), interval);
        payload[4] = (byte)txPower;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(5), manufacturerId);
        payload[7] = (byte)data.Length;
        data.CopyTo(payload.AsSpan(8));
        return payload;
    }

    /// <summary>SET_UPDATE payload: set_id, mfr_id, data_len, data.</summary>
    public static byte[] SetUpdatePayload(byte setId, ushort manufacturerId, ReadOnlySpan<byte> data)
    {
        var payload = new byte[4 + data.Length];
        payload[0] = setId;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(1), manufacturerId);
        payload[3] = (byte)data.Length;
        data.CopyTo(payload.AsSpan(4));
        return payload;
    }

    /// <summary>SET_STOP payload: set_id.</summary>
    public static byte[] SetStopPayload(byte setId) => [setId];

    /// <summary>GET_STATUS payload: set_id (0xFF = device summary).</summary>
    public static byte[] GetStatusPayload(byte setId) => [setId];
}
