using System;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// CRC-16/CCITT-FALSE (poly 0x1021, init 0xFFFF, no reflection, xorout 0) as used by the
/// serial protocol of the USB BLE advertising stick (protocol.md section 2).
/// The check value of the ASCII string "123456789" is 0x29B1.
/// </summary>
internal static class Crc16
{
    private const ushort Polynomial = 0x1021;
    private const ushort InitialValue = 0xFFFF;

    /// <summary>
    /// Computes the CRC over the given bytes.
    /// </summary>
    /// <param name="data">Bytes to protect (LEN, TYPE, SEQ and PAYLOAD of a frame).</param>
    /// <returns>The 16 bit CRC.</returns>
    public static ushort Compute(ReadOnlySpan<byte> data)
    {
        ushort crc = InitialValue;

        foreach (byte b in data)
        {
            // Feed the next byte into the high byte of the register.
            crc ^= (ushort)(b << 8);

            // Shift out 8 bits, applying the polynomial whenever the top bit is set.
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 0x8000) != 0
                    ? (ushort)((crc << 1) ^ Polynomial)
                    : (ushort)(crc << 1);
            }
        }

        return crc;
    }
}
