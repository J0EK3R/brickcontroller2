// Byte-exact checks against the golden vectors published in the firmware's docs/protocol.md (section 18):
// proves that the BC2 encoders and decoders agree with the firmware implementation, not only with our own reading of the plan.

using System;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using FluentAssertions;
using Xunit;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

public class FirmwareVectorTests
{
    private static byte[] Frame(byte type, byte seq, byte[] payload) => StickFrameEncoder.Encode(type, seq, payload);

    private static byte[] Hex(string hex) => GoldenVectors.Bytes(hex);

    private static StickFrame Parse(string hex)
    {
        var frames = new StickFrameParser(TimeProvider.System).Feed(Hex(hex));
        frames.Should().HaveCount(1);
        return frames[0];
    }

    private static StickGattMessage Decode(string hex)
    {
        StickResponseDecoder.TryDecodeGatt(Parse(hex), out var message).Should().BeTrue();
        return message!;
    }

    [Fact]
    public void HelloRsp_OfFirmware_DecodesCapabilityBlock()
    {
        StickResponseDecoder.TryDecodeHello(Parse(GoldenVectors.FwHelloRsp), out var info).Should().BeTrue();

        info!.ProtoMajor.Should().Be(1);
        info.ProtoMinor.Should().Be(1);
        info.MaxSets.Should().Be(5);
        info.GattFlags.Should().Be(7);
        info.MaxConns.Should().Be(4);
        info.MaxValueLen.Should().Be(56);
        info.MaxScanData.Should().Be(31);
        info.FailsafeSlots.Should().Be(4);
        info.SupportsGatt.Should().BeTrue();
    }

    [Fact]
    public void ScanStart_Defaults_AndDuplicatesVariant()
    {
        Frame(StickProtocol.TypeScanStart, 8, StickFrameEncoder.ScanStartPayload(1, 0, 0, 0)).Should().Equal(Hex(GoldenVectors.FwScanStartDefaults));
        Frame(StickProtocol.TypeScanStart, 9, StickFrameEncoder.ScanStartPayload(3, 160, 80, 1000)).Should().Equal(Hex(GoldenVectors.FwScanStartDup));
        Frame(StickProtocol.TypeScanStop, 10, []).Should().Equal(Hex(GoldenVectors.FwScanStop));
    }

    [Fact]
    public void Connect_BackgroundAuto_AndRandomWithTimeout()
    {
        StickAddress.TryParse("90:84:2B:01:02:03", out var duplo);
        StickAddress.TryParse("AA:BB:CC:DD:EE:FF", out var other);

        Frame(StickProtocol.TypeConnect, 11, StickFrameEncoder.ConnectPayload(0, duplo, 0xFF, 1, 0, 0, 0)).Should().Equal(Hex(GoldenVectors.FwConnectBackground));
        Frame(StickProtocol.TypeConnect, 12, StickFrameEncoder.ConnectPayload(2, other, 1, 0, 5000, 12, 24)).Should().Equal(Hex(GoldenVectors.FwConnectRandom));
    }

    [Fact]
    public void DisconnectDiscoverRead()
    {
        Frame(StickProtocol.TypeDisconnect, 13, StickFrameEncoder.ConnIdPayload(1)).Should().Equal(Hex(GoldenVectors.FwDisconnect));
        Frame(StickProtocol.TypeDiscover, 14, StickFrameEncoder.ConnIdPayload(1)).Should().Equal(Hex(GoldenVectors.FwDiscover));
        Frame(StickProtocol.TypeRead, 15, StickFrameEncoder.ReadPayload(1, 0x0012)).Should().Equal(Hex(GoldenVectors.FwRead));
    }

    [Fact]
    public void Writes_DuploMkDiyAndLargest()
    {
        byte[] duplo = [0x08, 0x00, 0x81, 0x00, 0x11, 0x51, 0x00, 0x00];
        byte[] mkDiy = [0xCC, 0xAA, 0xBB, 0x01, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x66, 0x33];
        byte[] largest = new byte[56];
        for (int i = 0; i < largest.Length; i++)
        {
            largest[i] = (byte)i;
        }

        Frame(StickProtocol.TypeWrite, 16, StickFrameEncoder.WritePayload(0, 0x0010, duplo)).Should().Equal(Hex(GoldenVectors.FwWriteDuplo));
        Frame(StickProtocol.TypeWriteCmd, 17, StickFrameEncoder.WritePayload(1, 0x002A, mkDiy)).Should().Equal(Hex(GoldenVectors.FwWriteCmdMkDiy));
        Frame(StickProtocol.TypeWriteCmd, 18, StickFrameEncoder.WritePayload(1, 0x002A, largest)).Should().Equal(Hex(GoldenVectors.FwWriteCmd56));
    }

    [Fact]
    public void SubscribeStatusAndFailsafe()
    {
        byte[] mkDiy = [0xCC, 0xAA, 0xBB, 0x01, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x66, 0x33];

        Frame(StickProtocol.TypeSubscribe, 19, StickFrameEncoder.SubscribePayload(0, 0x0011, 1)).Should().Equal(Hex(GoldenVectors.FwSubscribe));
        Frame(StickProtocol.TypeGetGattStatus, 20, []).Should().Equal(Hex(GoldenVectors.FwGetGattStatus));
        Frame(StickProtocol.TypeSetFailsafe, 21, StickFrameEncoder.SetFailsafePayload(1, 0, 0x002A, mkDiy)).Should().Equal(Hex(GoldenVectors.FwSetFailsafe));
        Frame(StickProtocol.TypeSetFailsafe, 22, StickFrameEncoder.SetFailsafePayload(1, 1, 0x002A, [])).Should().Equal(Hex(GoldenVectors.FwSetFailsafeClear));
    }

    [Fact]
    public void Responses_ReadRsp_GattStatus17Bytes_Acks()
    {
        Decode(GoldenVectors.FwReadRsp).Should().BeEquivalentTo(new StickReadResponse(1, 0x12, false, [0xA1, 0xB2]));
        Decode(GoldenVectors.FwGattStatusRsp).Should().Be(new StickGattStatus(true, 4, 3, 2, 0), "the 17 byte form of the firmware is accepted, heap figures are ignored");

        StickResponseDecoder.TryDecodeAck(Parse(GoldenVectors.FwAckAttError), out var att).Should().BeTrue();
        att!.Status.Should().Be(StickStatus.AttError);
        att.Extra.Should().Equal(0x03);
        StickResponseDecoder.TryDecodeAck(Parse(GoldenVectors.FwAckConnect), out var ok).Should().BeTrue();
        ok!.OriginalType.Should().Be(StickProtocol.TypeConnect);
        ok.Status.Should().Be(StickStatus.Ok);
    }

    [Fact]
    public void Events_AllDecode()
    {
        Decode(GoldenVectors.FwEvtNotify).Should().BeEquivalentTo(new StickNotify(0, 0x11, 0, [0x04, 0x00, 0x45, 0x00, 0x01]));
        Decode(GoldenVectors.FwEvtConnected).Should().Be(new StickConnected(0, 64, 24, "90:84:2B:01:02:03"));
        Decode(GoldenVectors.FwEvtDisconnected).Should().Be(new StickDisconnected(0, true, 0x08));
        Decode(GoldenVectors.FwEvtServiceLego).Should().Be(new StickGattServiceEvent(0, 0x10, 0x20, new Guid("00001623-1212-efde-1623-785feabcd123")));
        Decode(GoldenVectors.FwEvtService180A).Should().Be(new StickGattServiceEvent(0, 0x30, 0x3F, StickUuid.FromShort(0x180A)));
        Decode(GoldenVectors.FwEvtChar).Should().Be(new StickGattCharEvent(0, 0x11, 0x12, 0x1E, new Guid("00001624-1212-efde-1623-785feabcd123")));
        Decode(GoldenVectors.FwEvtDiscoverDone).Should().Be(new StickDiscoverDone(0, StickStatus.Ok, 2, 5));
        Decode(GoldenVectors.FwEvtScanStopped).Should().Be(new StickScanStopped(1, 15));
        Decode(GoldenVectors.FwEvtDropped).Should().Be(new StickDropped(2, 42));
    }

    [Fact]
    public void ScanReport_LegoManufacturerData()
    {
        var report = (StickScanReport)Decode(GoldenVectors.FwEvtScanReport);

        report.Address.Should().Be("90:84:2B:01:02:03");
        report.AddressType.Should().Be(1);
        report.Rssi.Should().Be(-60);
        report.IsScanResponse.Should().BeFalse();
        var structures = StickAdvertisementParser.Parse(report.Data);
        structures[0xFF].Should().Equal(0x97, 0x03, 0x00, 0x41, 0x20, 0x00, 0x00, 0x00);
    }
}
