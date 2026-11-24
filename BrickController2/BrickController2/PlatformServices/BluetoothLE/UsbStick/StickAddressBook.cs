// Remembers what the stick's scans learned about peripherals, mainly the address type needed by CONNECT.

using System.Collections.Concurrent;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Address (as "AA:BB:CC:DD:EE:FF") to address type memory shared by the scanners and the connect path.
/// The Windows stack hides the address type; the stick needs it for CONNECT (0 public, 1 random).
/// A device that is not in the book is connected with 0xFF = "auto" (the stick looks it up in its own scan cache).
/// </summary>
public sealed class StickAddressBook
{
    // Bounded: a scan in a crowded place must not grow the book without limit.
    private const int MaxEntries = 2048;

    private readonly ConcurrentDictionary<string, byte> _types = new();

    /// <summary>Records the address type reported by a scan.</summary>
    internal void Remember(string address, byte addressType)
    {
        if (_types.Count >= MaxEntries && !_types.ContainsKey(address))
        {
            _types.Clear();
        }

        _types[address] = addressType;
    }

    /// <summary>Returns true when a stick scan has seen the address.</summary>
    internal bool WasSeen(string address) => _types.ContainsKey(address.ToUpperInvariant());

    /// <summary>The remembered type, or 0xFF (auto) for an address no stick scan has seen.</summary>
    internal byte GetAddressType(string address)
        => _types.TryGetValue(address.ToUpperInvariant(), out byte type) ? type : StickProtocol.AddressTypeAuto;
}
