// Encoder and decoder tests for the protocol 1.1 (GATT) messages against golden frames.
using System;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using FluentAssertions;
using Xunit;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Golden vector tests for every new request, event and response plus malformed-payload handling.
/// </summary>
public class GattCodecTests
{
    private static readonly byte[] Address = [0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF];

    // ------------------------------------------------------------------ encoders

    [Fact]
    public void Encoder_Hello_AnnouncesProtocol11()
    {
        var payload = StickFrameEncoder.HelloPayload(StickProtocol.ProtoMajor, StickProtocol.ProtoMinor, 1000); // golden vector of the protocol document uses 1000 ms
        StickFrameEncoder.Encode(StickProtocol.TypeHello, 1, payload).Should().Equal(GoldenVectors.Bytes(GoldenVectors.Hello11));
    }

    [Fact]
    public void Encoder_Hello_RequestsTwoSecondKeepAlive()
    {
        StickProtocol.RequestedKeepAliveMs.Should().Be(2000);
        var payload = StickFrameEncoder.HelloPayload(StickProtocol.ProtoMajor, StickProtocol.ProtoMinor, StickProtocol.RequestedKeepAliveMs);
        System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(2)).Should().Be(2000);
    }

    [Fact]
    public void Encoder_ScanStart_MatchesGoldenVector()
        => Frame(StickProtocol.TypeScanStart, 8, StickFrameEncoder.ScanStartPayload(StickProtocol.ScanFlagActive, 160, 80, 1000)).Should().Equal(GoldenVectors.Bytes(GoldenVectors.ScanStart));

    [Fact]
    public void Encoder_ScanStop_MatchesGoldenVector()
        => Frame(StickProtocol.TypeScanStop, 9, []).Should().Equal(GoldenVectors.Bytes(GoldenVectors.ScanStop));

    [Fact]
    public void Encoder_Connect_MatchesGoldenVector()
        => Frame(StickProtocol.TypeConnect, 10, StickFrameEncoder.ConnectPayload(2, Address, 1, 0, 10000, 12, 24)).Should().Equal(GoldenVectors.Bytes(GoldenVectors.Connect));

    [Fact]
    public void Encoder_Disconnect_MatchesGoldenVector()
        => Frame(StickProtocol.TypeDisconnect, 11, StickFrameEncoder.ConnIdPayload(2)).Should().Equal(GoldenVectors.Bytes(GoldenVectors.Disconnect));

    [Fact]
    public void Encoder_Discover_MatchesGoldenVector()
        => Frame(StickProtocol.TypeDiscover, 12, StickFrameEncoder.ConnIdPayload(2)).Should().Equal(GoldenVectors.Bytes(GoldenVectors.Discover));

    [Fact]
    public void Encoder_Read_MatchesGoldenVector()
        => Frame(StickProtocol.TypeRead, 13, StickFrameEncoder.ReadPayload(2, 0x0012)).Should().Equal(GoldenVectors.Bytes(GoldenVectors.Read));

    [Fact]
    public void Encoder_Write_MatchesGoldenVector()
        => Frame(StickProtocol.TypeWrite, 14, StickFrameEncoder.WritePayload(2, 0x0014, [1, 2, 3, 4])).Should().Equal(GoldenVectors.Bytes(GoldenVectors.Write));

    [Fact]
    public void Encoder_WriteCmd_MatchesGoldenVector()
        => Frame(StickProtocol.TypeWriteCmd, 15, StickFrameEncoder.WritePayload(2, 0x0014, [0xA0, 0xB0])).Should().Equal(GoldenVectors.Bytes(GoldenVectors.WriteCmd));

    [Fact]
    public void Encoder_Subscribe_MatchesGoldenVector()
        => Frame(StickProtocol.TypeSubscribe, 16, StickFrameEncoder.SubscribePayload(2, 0x0015, 1)).Should().Equal(GoldenVectors.Bytes(GoldenVectors.Subscribe));

    [Fact]
    public void Encoder_GetGattStatus_MatchesGoldenVector()
        => Frame(StickProtocol.TypeGetGattStatus, 17, []).Should().Equal(GoldenVectors.Bytes(GoldenVectors.GetGattStatus));

    [Fact]
    public void Encoder_SetFailsafe_MatchesGoldenVector()
        => Frame(StickProtocol.TypeSetFailsafe, 18, StickFrameEncoder.SetFailsafePayload(2, 1, 0x0014, [0, 0, 0])).Should().Equal(GoldenVectors.Bytes(GoldenVectors.SetFailsafe));

    // ------------------------------------------------------------------ HELLO_RSP

    [Fact]
    public void Decoder_Hello32Bytes_ReturnsGattCapabilities()
    {
        StickResponseDecoder.TryDecodeHello(Parse(GoldenVectors.HelloRsp11), out var info).Should().BeTrue();

        info!.ProtoMinor.Should().Be(1);
        info.GattFlags.Should().Be(7);
        info.MaxConns.Should().Be(6);
        info.MaxValueLen.Should().Be(56);
        info.MaxScanData.Should().Be(31);
        info.FailsafeSlots.Should().Be(4);
        info.SupportsGatt.Should().BeTrue();
        info.SupportsScan.Should().BeTrue();
        info.SupportsFailsafe.Should().BeTrue();
    }

    [Fact]
    public void Decoder_Hello27Bytes_MeansNoGatt()
    {
        StickResponseDecoder.TryDecodeHello(Parse(GoldenVectors.HelloRsp), out var info).Should().BeTrue();

        info!.MaxConns.Should().Be(0);
        info.SupportsGatt.Should().BeFalse();
        info.SupportsScan.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 6, false)] // central flag missing
    [InlineData(2, 0, false)] // no connection slots
    [InlineData(2, 1, true)]
    [InlineData(2, 16, true)]
    public void StickInfo_SupportsGatt_NeedsCentralFlagAndSlots(byte flags, byte maxConns, bool expected)
    {
        var info = new StickInfo(1, 1, "x", 5, 24, 27, 32, 16384, 1000, "id", flags, maxConns, 56, 31, 4);
        info.SupportsGatt.Should().Be(expected);
    }

    [Fact]
    public void StickInfo_Protocol10WithGattBits_DoesNotSupportGatt()
    {
        new StickInfo(1, 0, "x", 5, 24, 27, 32, 16384, 1000, "id", 7, 6, 56, 31, 4).SupportsGatt.Should().BeFalse();
    }

    // ------------------------------------------------------------------ events

    [Fact]
    public void Decoder_Notify_ReturnsValue()
    {
        Decode(GoldenVectors.EvtNotify).Should().BeEquivalentTo(new StickNotify(2, 0x0014, 0, [0x11, 0x22, 0x33]));
    }

    [Fact]
    public void Decoder_Connected_ReturnsMtuIntervalAddress()
    {
        Decode(GoldenVectors.EvtConnected).Should().Be(new StickConnected(2, 64, 24, "AA:BB:CC:DD:EE:FF"));
    }

    [Fact]
    public void Decoder_Disconnected_ReturnsPhaseAndReason()
    {
        Decode(GoldenVectors.EvtDisconnected).Should().Be(new StickDisconnected(2, true, 0x13));
    }

    [Fact]
    public void Decoder_ScanReport_ReturnsAddressRssiAndRawAd()
    {
        var report = (StickScanReport)Decode(GoldenVectors.EvtScanReport);

        report.Address.Should().Be("AA:BB:CC:DD:EE:FF");
        report.AddressType.Should().Be(1);
        report.Rssi.Should().Be(-60);
        report.Flags.Should().Be(3);
        report.IsScanResponse.Should().BeTrue();
        report.Data.Should().Equal(2, 1, 6, 3, 9);
    }

    [Fact]
    public void Decoder_GattService_ExpandsShortUuid()
    {
        Decode(GoldenVectors.EvtGattService).Should().Be(new StickGattServiceEvent(2, 1, 9, new Guid("0000180a-0000-1000-8000-00805f9b34fb")));
    }

    [Fact]
    public void Decoder_GattChar_ReturnsHandlesAndProperties()
    {
        Decode(GoldenVectors.EvtGattChar).Should().Be(new StickGattCharEvent(2, 0x12, 0x13, 0x1A, new Guid("00001624-0000-1000-8000-00805f9b34fb")));
    }

    [Fact]
    public void Decoder_GattChar128BitUuid_ReversesWireOrder()
    {
        // Wire bytes 23 C1 .. 6D (little-endian) are the 128 bit value 6D5C4B3A-2918-07F6-E5D4-C3B2A1E4C123.
        var chr = (StickGattCharEvent)Decode(GoldenVectors.EvtGattChar128);
        chr.Uuid.Should().Be(new Guid("6d5c4b3a-2918-07f6-e5d4-c3b2a1e4c123"));
    }

    [Fact]
    public void Decoder_DiscoverDone_ReturnsCounts()
    {
        Decode(GoldenVectors.EvtDiscoverDone).Should().Be(new StickDiscoverDone(2, StickStatus.Ok, 3, 7));
    }

    [Fact]
    public void Decoder_ScanStoppedAndDropped()
    {
        Decode(GoldenVectors.EvtScanStopped).Should().Be(new StickScanStopped(2, 0));
        Decode(GoldenVectors.EvtDropped).Should().Be(new StickDropped(2, 5));
    }

    [Fact]
    public void Decoder_ReadResponse_ReturnsData()
    {
        Decode(GoldenVectors.ReadRsp).Should().BeEquivalentTo(new StickReadResponse(2, 0x12, false, [0x31, 0x2E, 0x30]));
    }

    [Fact]
    public void Decoder_GattStatus_ReturnsMasks()
    {
        Decode(GoldenVectors.GattStatusRsp).Should().Be(new StickGattStatus(true, 2, 5, 3, 4));
    }

    [Fact]
    public void Decoder_AttErrorAck_CarriesAttErrorCode()
    {
        StickResponseDecoder.TryDecodeAck(Parse(GoldenVectors.AckAttError), out var ack).Should().BeTrue();
        ack!.Status.Should().Be(StickStatus.AttError);
        ack.Extra.Should().Equal(0x03);
    }

    [Theory]
    [InlineData(StickProtocol.TypeEvtNotify, 2)]
    [InlineData(StickProtocol.TypeEvtConnected, 10)]
    [InlineData(StickProtocol.TypeEvtDisconnected, 2)]
    [InlineData(StickProtocol.TypeEvtScanReport, 9)]
    [InlineData(StickProtocol.TypeEvtGattService, 5)]
    [InlineData(StickProtocol.TypeEvtGattChar, 6)]
    [InlineData(StickProtocol.TypeEvtDiscoverDone, 3)]
    [InlineData(StickProtocol.TypeEvtScanStopped, 2)]
    [InlineData(StickProtocol.TypeEvtDropped, 2)]
    [InlineData(StickProtocol.TypeReadRsp, 3)]
    [InlineData(StickProtocol.TypeGattStatusRsp, 8)]
    public void Decoder_TruncatedPayload_IsRejected(byte type, int length)
    {
        StickResponseDecoder.TryDecodeGatt(new StickFrame(type, 0, new byte[length]), out _).Should().BeFalse();
    }

    [Fact]
    public void Decoder_ScanReportWithLyingDataLength_IsRejected()
    {
        byte[] payload = [1, 2, 3, 4, 5, 6, 0, 0xC4, 3, 20, 1, 2]; // data_len 20 but only 2 bytes follow
        StickResponseDecoder.TryDecodeGatt(new StickFrame(StickProtocol.TypeEvtScanReport, 0, payload), out _).Should().BeFalse();
    }

    [Fact]
    public void Decoder_ServiceWithOddUuidLength_IsRejected()
    {
        byte[] payload = [2, 1, 0, 9, 0, 3, 1, 2, 3];
        StickResponseDecoder.TryDecodeGatt(new StickFrame(StickProtocol.TypeEvtGattService, 0, payload), out _).Should().BeFalse();
    }

    [Fact]
    public void Decoder_UnknownGattType_IsIgnored()
    {
        StickResponseDecoder.TryDecodeGatt(new StickFrame(0xC9, 0, []), out _).Should().BeFalse();
    }

    [Fact]
    public void Decoder_OldEventsAreNotGattMessages()
    {
        StickResponseDecoder.TryDecodeGatt(Parse(GoldenVectors.EvtTimeout), out _).Should().BeFalse();
        StickResponseDecoder.TryDecodeEvent(Parse(GoldenVectors.EvtNotify), out _).Should().BeFalse();
    }

    // ------------------------------------------------------------------ helpers (address / uuid)

    [Fact]
    public void Address_RoundTrips()
    {
        StickAddress.TryParse("aa:bb:cc:dd:ee:0f", out var bytes).Should().BeTrue();
        bytes.Should().Equal(0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0x0F);
        StickAddress.Format(bytes).Should().Be("AA:BB:CC:DD:EE:0F");
    }

    [Theory]
    [InlineData("")]
    [InlineData("AA:BB:CC")]
    [InlineData("AA:BB:CC:DD:EE:GG")]
    [InlineData("AAA:BB:CC:DD:EE:FF")]
    public void Address_Invalid_IsRejected(string text) => StickAddress.TryParse(text, out _).Should().BeFalse();

    [Fact]
    public void Uuid_ShortIsExpandedWithBluetoothBase()
    {
        StickUuid.FromShort(0x2A26).Should().Be(new Guid("00002a26-0000-1000-8000-00805f9b34fb"));
    }

    // ------------------------------------------------------------------ test helpers

    private static byte[] Frame(byte type, byte seq, byte[] payload) => StickFrameEncoder.Encode(type, seq, payload);

    private static StickFrame Parse(string hex)
    {
        var frames = new StickFrameParser(TimeProvider.System).Feed(GoldenVectors.Bytes(hex));
        frames.Should().HaveCount(1);
        return frames[0];
    }

    private static StickGattMessage Decode(string hex)
    {
        StickResponseDecoder.TryDecodeGatt(Parse(hex), out var message).Should().BeTrue();
        return message!;
    }
}
