using System;
using System.Threading;
using System.Threading.Tasks;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Byte transport to one candidate port of the stick (a virtual COM port on Windows).
/// The protocol logic lives above this interface so it can be tested with a fake.
/// </summary>
public interface IStickTransport : IDisposable
{
    /// <summary>Human readable name of the port, e.g. "COM7".</summary>
    string Name { get; }

    /// <summary>
    /// Opens the port with DTR/RTS de-asserted and discards pending input.
    /// Throws when the port cannot be opened.
    /// </summary>
    void Open();

    /// <summary>Closes the port. Safe to call several times.</summary>
    void Close();

    /// <summary>Writes bytes to the port. Throws when the port is closed or unplugged.</summary>
    Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken);

    /// <summary>
    /// Raised (possibly on a background thread) when bytes were received. The array is owned by the handler.
    /// </summary>
    event Action<byte[]>? DataReceived;

    /// <summary>Raised once when the port failed or the device was unplugged.</summary>
    event Action? Disconnected;
}

/// <summary>
/// Finds the candidate ports that may belong to the stick (platform specific).
/// </summary>
public interface IStickLocator
{
    /// <summary>
    /// Lazily enumerates a transport for each candidate port (VID 303A / PID 1001 on Windows).
    /// The enumeration is lazy so ports after the accepted one are never created.
    /// The caller owns and disposes every returned transport.
    /// </summary>
    System.Collections.Generic.IEnumerable<IStickTransport> EnumerateCandidates();
}
