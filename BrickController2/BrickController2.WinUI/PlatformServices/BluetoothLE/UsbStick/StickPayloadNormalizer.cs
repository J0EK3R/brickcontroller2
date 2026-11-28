using System;
using System.Diagnostics.CodeAnalysis;
using BrickController2.Protocols;

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
/// <para>
/// MouldKing and JieStar (both use the same company id, header and seed): the WinUI services fill the unused tail
/// of the 27 bytes with <c>(byte)(index + 1)</c> where <c>index</c> counts the 3 offset bytes, while Android uses
/// <c>(byte)(position + 1)</c> without them. After the offset is stripped the filler would therefore differ by 3
/// from the Android bytes, so the filler is recomputed here (see <see cref="RestoreAndroidFiller"/>). This keeps the
/// WinUI platform services identical to upstream.
/// </para>
/// Data that is already in the Android layout (24 bytes or less) passes through unchanged.
/// </summary>
internal static class StickPayloadNormalizer
{
    /// <summary>
    /// Number of dummy bytes the WinUI platform services put in front of the payload.
    /// </summary>
    public const int WindowsPayloadOffset = 3;

    /// <summary>
    /// Payload length of the Android PowerBox service (3 header + 5 seed + 8 telegram + 2 checksum).
    /// </summary>
    public const int PowerBoxPayloadLength = 18;

    /// <summary>
    /// Company identifier of PowerBox (see PowerBoxProtocol.ManufacturerID).
    /// </summary>
    public const ushort PowerBoxManufacturerId = 0xFF00;

    /// <summary>
    /// Header + seed length of the MK/JieStar protocols (3 + 5 bytes).
    /// </summary>
    private static readonly int MkHeaderAndSeedLength = MKProtocol.HeaderArray.Length + MKProtocol.SeedArray.Length;

    /// <summary>
    /// Number of CRC checksum bytes the cipher appends to the telegram.
    /// </summary>
    private const int ChecksumLength = 2;

    /// <summary>
    /// Telegram lengths used by the MouldKing devices (MK3/MK3.8/MK4/MK5 and JieStar: 8 bytes, MK4/MK6: 10 bytes).
    /// Ordered ascending: the smallest length that matches the filler pattern wins.
    /// </summary>
    private static readonly int[] MkTelegramLengths = [8, 10];

    /// <summary>
    /// Distance between the filler value written by the WinUI services and the Android value at the same stick position.
    /// </summary>
    private const int WindowsFillerValueShift = WindowsPayloadOffset;

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

            // MouldKing / JieStar: the filler written by the unmodified WinUI services is shifted by the offset.
            if (manufacturerId == MKProtocol.ManufacturerID || manufacturerId == JieStarProtocol.ManufacturerID)
            {
                data = RestoreAndroidFiller(data);
            }
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

    /// <summary>
    /// Rewrites the filler bytes of a stripped WinUI MouldKing/JieStar payload to the values the Android service writes.
    /// <para>
    /// WinUI writes <c>(byte)(p + 1 + 3)</c> at stick position <c>p</c>, Android <c>(byte)(p + 1)</c>. The position
    /// where the filler starts equals the real payload length, which depends on the telegram length (8 or 10 bytes).
    /// The smallest candidate length whose whole tail matches the WinUI filler pattern is taken. This is exact except
    /// for a 10 byte telegram whose two checksum bytes happen to equal the filler pattern (about 2^-16 per telegram).
    /// Data without the pattern (e.g. services that already write Android filler) is returned unchanged.
    /// </para>
    /// </summary>
    /// <param name="data">Payload with the offset bytes already removed.</param>
    /// <returns>A new array with the Android filler, or the unchanged input when no candidate matches.</returns>
    private static ReadOnlySpan<byte> RestoreAndroidFiller(ReadOnlySpan<byte> data)
    {
        foreach (int telegramLength in MkTelegramLengths)
        {
            int realLength = MkHeaderAndSeedLength + telegramLength + ChecksumLength;
            if (!HasWindowsFillerFrom(data, realLength))
            {
                continue;
            }

            byte[] fixedData = data.ToArray();   // never mutate the caller's array
            for (int p = realLength; p < fixedData.Length; p++)
            {
                fixedData[p] = (byte)(p + 1);
            }

            return fixedData;
        }

        return data;
    }

    /// <summary>
    /// True when every byte from <paramref name="start"/> on equals the WinUI filler value <c>p + 1 + 3</c>.
    /// </summary>
    private static bool HasWindowsFillerFrom(ReadOnlySpan<byte> data, int start)
    {
        for (int p = start; p < data.Length; p++)
        {
            if (data[p] != (byte)(p + 1 + WindowsFillerValueShift))
            {
                return false;
            }
        }

        return true;
    }
}
