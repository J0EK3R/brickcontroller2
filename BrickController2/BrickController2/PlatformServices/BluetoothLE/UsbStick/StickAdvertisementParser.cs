// Splits the raw AD structures of a scan report into (type, data) pairs and offers the name helpers
// BC2's scan logic needs. Mirrors what the Windows watcher delivers as "data sections".

using System;
using System.Collections.Generic;
using System.Text;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Parser for advertising data (a sequence of <c>len, type, data[len-1]</c> structures, Bluetooth Core Spec Vol 3 Part C 11).
/// </summary>
internal static class StickAdvertisementParser
{
    /// <summary>AD type: shortened local name.</summary>
    public const byte AdShortLocalName = 0x08;

    /// <summary>AD type: complete local name.</summary>
    public const byte AdCompleteLocalName = 0x09;

    /// <summary>AD type: incomplete list of 128 bit service UUIDs.</summary>
    public const byte AdIncompleteService128 = 0x06;

    /// <summary>AD type: complete list of 128 bit service UUIDs.</summary>
    public const byte AdCompleteService128 = 0x07;

    /// <summary>AD type: manufacturer specific data.</summary>
    public const byte AdManufacturerSpecific = 0xFF;

    /// <summary>
    /// The AD types BC2's device managers look at (same set as the Windows <c>BleScanner</c>): manufacturer
    /// data, 128 bit service lists and the complete local name. Other types are dropped.
    /// </summary>
    public static readonly IReadOnlySet<byte> ReportedTypes = new HashSet<byte>
    {
        AdManufacturerSpecific,
        AdCompleteService128,
        AdIncompleteService128,
        AdCompleteLocalName,
    };

    /// <summary>
    /// Parses all AD structures. A length of 0 ends the data (padding); a structure that does not fit stops
    /// parsing, keeping what was read so far. When a type occurs twice the last one wins.
    /// </summary>
    public static Dictionary<byte, byte[]> Parse(ReadOnlySpan<byte> data)
    {
        var result = new Dictionary<byte, byte[]>();
        int pos = 0;
        while (pos < data.Length)
        {
            int len = data[pos];
            if (len == 0 || pos + 1 + len > data.Length)
            {
                break;
            }

            result[data[pos + 1]] = data.Slice(pos + 2, len - 1).ToArray();
            pos += 1 + len;
        }

        return result;
    }

    /// <summary>
    /// Returns the local name of the structures (complete preferred over shortened) or null when there is none
    /// or it is empty. Same rule as <c>IsValidDeviceName</c> of the Windows code: a name must not be empty.
    /// </summary>
    public static string? GetLocalName(IReadOnlyDictionary<byte, byte[]> structures)
    {
        if (!structures.TryGetValue(AdCompleteLocalName, out byte[]? raw) && !structures.TryGetValue(AdShortLocalName, out raw))
        {
            return null;
        }

        string name = Encoding.UTF8.GetString(raw);
        return IsValidDeviceName(name) ? name : null;
    }

    /// <summary>A device name is valid when it is not empty (copy of the Windows <c>IsValidDeviceName</c> rule).</summary>
    public static bool IsValidDeviceName(string? name) => !string.IsNullOrEmpty(name);
}
