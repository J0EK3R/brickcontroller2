namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// One decoded protocol frame (without SOF, LEN and CRC, which are only part of the wire format).
/// </summary>
/// <param name="Type">Message type (protocol.md section 3).</param>
/// <param name="Seq">Sequence number chosen by the host; events use 0.</param>
/// <param name="Payload">Message payload, 0..60 bytes.</param>
internal sealed record StickFrame(byte Type, byte Seq, byte[] Payload);
