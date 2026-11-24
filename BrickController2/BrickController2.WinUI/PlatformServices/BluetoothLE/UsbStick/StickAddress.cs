// Helpers for the two binary encodings the stick protocol uses for identifiers: Bluetooth addresses
// (6 bytes in display order) and UUIDs (2 or 16 bytes in ATT little-endian wire order).

using System;
using System.Globalization;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Conversion between "AA:BB:CC:DD:EE:FF" strings and the 6 address bytes of the protocol
/// (byte 0 is the first octet of the displayed address).
/// </summary>
internal static class StickAddress
{
    /// <summary>
    /// Number of bytes of a Bluetooth address.
    /// </summary>
    public const int Length = 6;

    /// <summary>
    /// Formats 6 bytes as "AA:BB:CC:DD:EE:FF".
    /// </summary>
    public static string Format(ReadOnlySpan<byte> bytes)
        => string.Join(':', Enumerate(bytes));

    private static string[] Enumerate(ReadOnlySpan<byte> bytes)
    {
        var parts = new string[bytes.Length];
        for (int i = 0; i < bytes.Length; i++)
        {
            parts[i] = bytes[i].ToString("X2", CultureInfo.InvariantCulture);
        }

        return parts;
    }

    /// <summary>
    /// Parses "AA:BB:CC:DD:EE:FF" (case insensitive, ':' or '-' separators).
    /// </summary>
    /// <returns>False when the text is not a valid address.</returns>
    public static bool TryParse(string? text, out byte[] bytes)
    {
        bytes = new byte[Length];
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        string[] parts = text.Split(':', '-');
        if (parts.Length != Length)
        {
            return false;
        }

        for (int i = 0; i < Length; i++)
        {
            if (parts[i].Length != 2 || !byte.TryParse(parts[i], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out bytes[i]))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Conversion of GATT UUIDs from the wire format to <see cref="Guid"/>. 16 bit UUIDs are expanded with the
/// Bluetooth base UUID (0000xxxx-0000-1000-8000-00805F9B34FB), like Windows and Android do, so that
/// <c>Guid</c> comparisons with the constants used by BC2 work.
/// </summary>
internal static class StickUuid
{
    /// <summary>
    /// Bluetooth base UUID with the 16 bit part zeroed.
    /// </summary>
    private static readonly Guid BaseUuid = new("00000000-0000-1000-8000-00805F9B34FB");

    /// <summary>
    /// Expands a 16 bit UUID to a full <see cref="Guid"/>.
    /// </summary>
    public static Guid FromShort(ushort value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BaseUuid.TryWriteBytes(bytes, bigEndian: true, out _);
        bytes[2] = (byte)(value >> 8);
        bytes[3] = (byte)value;
        return new Guid(bytes, bigEndian: true);
    }

    /// <summary>
    /// Converts a wire UUID (2 bytes or 16 bytes, little-endian ATT order) to a <see cref="Guid"/>.
    /// </summary>
    /// <returns>False when the length is neither 2 nor 16.</returns>
    public static bool TryFromWire(ReadOnlySpan<byte> wire, out Guid uuid)
    {
        uuid = Guid.Empty;
        if (wire.Length == 2)
        {
            uuid = FromShort((ushort)(wire[0] | (wire[1] << 8)));
            return true;
        }

        if (wire.Length != 16)
        {
            return false;
        }

        // The ATT wire order is the reverse of the big-endian textual order of the 128 bit value.
        Span<byte> reversed = stackalloc byte[16];
        for (int i = 0; i < 16; i++)
        {
            reversed[i] = wire[15 - i];
        }

        uuid = new Guid(reversed, bigEndian: true);
        return true;
    }
}
