using BrickController2.DeviceManagement.MouldKing;
using BrickController2.Protocols;

namespace BrickController2.Windows.PlatformServices.DeviceManagement.MouldKing;

public class MKPlatformService : IMKPlatformService
{
    private const int HeaderOffset = 15;
    private const int PayloadOffset = 3;
    private const int PayloadLength = 24 + PayloadOffset;

    public bool TryGetRfPayload(byte[] rawData, out byte[] rfPayload)
    {
        rfPayload = new byte[PayloadLength];
        int payloadLength = CryptTools.GetRfPayload(MKProtocol.SeedArray, MKProtocol.HeaderArray, rawData, HeaderOffset, MKProtocol.CTXValue1, MKProtocol.CTXValue2, rfPayload, PayloadOffset);

        // fill rest of array. The filler value is derived from the position WITHOUT the Windows offset, so that
        // the bytes equal those of the Android service once the offset is removed (USB stick advertiser, see
        // StickPayloadNormalizer). The 3 offset bytes themselves are not part of the on-air payload of the stick.
        for (int index = payloadLength + PayloadOffset; index < PayloadLength; index++)
        {
            rfPayload[index] = (byte)(index - PayloadOffset + 1);
        }

        return true;
    }
}
