using BrickController2.DeviceManagement.PowerBox;
using BrickController2.Protocols;

namespace BrickController2.Windows.PlatformServices.DeviceManagement.PowerBox;

public class PowerBoxPlatformService : IPowerBoxPlatformService
{
    private const int HeaderOffset = 15;
    private const int PayloadOffset = 3;
    // NOTE: the Android service uses 18 bytes (3 header + 5 seed + 8 telegram + 2 checksum, no filler), while this
    // Windows publisher service pads to 23 + offset = 26 bytes (5 filler bytes after the real data). The padding is kept
    // unchanged for the Windows publisher; the USB stick advertiser cuts the payload to the Android length of 18 bytes
    // (see StickPayloadNormalizer).
    private const int PayloadLength = 23 + PayloadOffset;

    public bool TryGetRfPayload(byte[] rawData, out byte[] rfPayload)
    {
        rfPayload = new byte[PayloadLength];
        int payloadLength = CryptTools.GetRfPayload(PowerBoxProtocol.SeedArray, PowerBoxProtocol.HeaderArray, rawData, HeaderOffset, PowerBoxProtocol.CTXValue1, PowerBoxProtocol.CTXValue2, rfPayload, PayloadOffset);

        // fill rest of array
        for (int index = payloadLength + PayloadOffset; index < PayloadLength; index++)
        {
            rfPayload[index] = (byte)(index + 1);
        }

        return true;
    }
}
