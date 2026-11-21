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
    string DeviceId);

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
