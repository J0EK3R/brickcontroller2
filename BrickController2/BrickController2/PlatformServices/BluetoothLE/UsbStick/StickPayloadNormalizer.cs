using System;
using System.Diagnostics.CodeAnalysis;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Converts the "raw data" produced by the platform services into the Android layout that the stick
/// sends (flags field added by the stick, at most 24 manufacturer data bytes).
/// <para>
/// The WinUI platform services create their data for the Windows publisher, which cannot add the AD
/// flags field: they place 3 dummy bytes in front of the encrypted payload so that it sits at the same
/// offset as on Android (see the comment in the WinUI CaDAPlatformService). The stick adds the flags
/// itself, so those 3 bytes must be removed. Per vendor (verified against the real services, see
/// StickPayloadNormalizerTests):
/// </para>
/// <list type="table">
/// <item><description>MouldKing, JieStar, CaDA V1: WinUI 24 + 3 = 27 bytes -> strip 3 -> 24 (Android: 24).</description></item>
/// <item><description>PowerBox: WinUI 23 + 3 = 26 bytes (18 real bytes + 5 filler bytes), Android 18 bytes without filler.
/// Strip 3 and cut to 18 bytes. The inconsistency (the WinUI service pads 5 bytes more than Android) is
/// resolved here; the Windows publisher path is left unchanged.</description></item>
/// <item><description>CaDA V2 (Rev2 encoder): 16 bytes unencrypted without offset on every platform -> unchanged.</description></item>
/// </list>
/// Data that is already in the Android layout (24 bytes or less) passes through unchanged.
/// </summary>
internal static class StickPayloadNormalizer
{
    /// <summary>Number of dummy bytes the WinUI platform services put in front of the payload.</summary>
    public const int WindowsPayloadOffset = 3;

    /// <summary>Payload length of the Android PowerBox service (3 header + 5 seed + 8 telegram + 2 checksum).</summary>
    public const int PowerBoxPayloadLength = 18;

    /// <summary>Company identifier of PowerBox (see PowerBoxProtocol.ManufacturerID).</summary>
    public const ushort PowerBoxManufacturerId = 0xFF00;

    /// <summary>
    /// Produces the data for the stick.
    /// </summary>
    /// <param name="manufacturerId">Company identifier of the device.</param>
    /// <param name="rawData">Data as handed to the advertiser device.</param>
    /// <param name="stickData">Data in the Android layout when the method returns true.</param>
    /// <returns>False when the data cannot fit the stick (more than 24 bytes after normalising).</returns>
    public static bool TryNormalize(ushort manufacturerId, byte[] rawData, [NotNullWhen(true)] out byte[]? stickData)
    {
        ReadOnlySpan<byte> data = rawData;

        // Longer than the largest Android payload: it carries the Windows offset.
        if (data.Length > StickProtocol.MaxDataWithFlags)
        {
            data = data[WindowsPayloadOffset..];
        }

        // PowerBox: the Android layout has no filler after the 18 real bytes.
        if (manufacturerId == PowerBoxManufacturerId && data.Length > PowerBoxPayloadLength)
        {
            data = data[..PowerBoxPayloadLength];
        }

        if (data.Length > StickProtocol.MaxDataWithFlags)
        {
            stickData = null;
            return false;
        }

        stickData = data.ToArray();
        return true;
    }
}
