using System;
using System.Linq;
using System.Text;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using FluentAssertions;
using Xunit;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// CRC, encoder and response decoder against the golden vectors of the protocol contract.
/// </summary>
public class StickCodecTests
{
    [Fact]
    public void Crc16_CheckValueOfStandardString_Is29B1()
    {
        Crc16.Compute(Encoding.ASCII.GetBytes("123456789")).Should().Be(0x29B1);
    }

    [Fact]
    public void Encoder_Hello_MatchesGoldenVector()
    {
        var payload = StickFrameEncoder.HelloPayload(1, 0, 1000);
        StickFrameEncoder.Encode(StickProtocol.TypeHello, 1, payload).Should().Equal(GoldenVectors.Bytes(GoldenVectors.Hello));
    }

    [Fact]
    public void Encoder_Ping_MatchesGoldenVector()
    {
        StickFrameEncoder.Encode(StickProtocol.TypePing, 2, []).Should().Equal(GoldenVectors.Bytes(GoldenVectors.Ping));
    }

    [Fact]
    public void Encoder_SetStart_MatchesGoldenVector()
    {
        var payload = StickFrameEncoder.SetStartPayload(0, 0x01, 32, 127, 0xFFF0, [1, 2, 3, 4, 5, 6, 7, 8]);
        StickFrameEncoder.Encode(StickProtocol.TypeSetStart, 3, payload).Should().Equal(GoldenVectors.Bytes(GoldenVectors.SetStart));
    }

    [Fact]
    public void Encoder_SetUpdate_MatchesGoldenVector()
    {
        var payload = StickFrameEncoder.SetUpdatePayload(0, 0xFFF0, [0xA1, 0xA2, 0xA3, 0xA4]);
        StickFrameEncoder.Encode(StickProtocol.TypeSetUpdate, 4, payload).Should().Equal(GoldenVectors.Bytes(GoldenVectors.SetUpdate));
    }

    [Fact]
    public void Encoder_SetStop_MatchesGoldenVector()
    {
        StickFrameEncoder.Encode(StickProtocol.TypeSetStop, 5, StickFrameEncoder.SetStopPayload(0)).Should().Equal(GoldenVectors.Bytes(GoldenVectors.SetStop));
    }

    [Fact]
    public void Encoder_StopAll_MatchesGoldenVector()
    {
        StickFrameEncoder.Encode(StickProtocol.TypeStopAll, 6, []).Should().Equal(GoldenVectors.Bytes(GoldenVectors.StopAll));
    }

    [Fact]
    public void Encoder_GetStatus_MatchesGoldenVector()
    {
        StickFrameEncoder.Encode(StickProtocol.TypeGetStatus, 7, StickFrameEncoder.GetStatusPayload(0xFF)).Should().Equal(GoldenVectors.Bytes(GoldenVectors.GetStatus));
    }

    [Fact]
    public void Encoder_TooLargePayload_Throws()
    {
        var act = () => StickFrameEncoder.Encode(StickProtocol.TypeSetUpdate, 1, new byte[61]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Decoder_HelloResponse_ReturnsCapabilities()
    {
        var frame = ParseSingle(GoldenVectors.HelloRsp);

        StickResponseDecoder.TryDecodeHello(frame, out var info).Should().BeTrue();

        info!.ProtoMajor.Should().Be(1);
        info.ProtoMinor.Should().Be(0);
        info.FirmwareVersion.Should().Be("0.1.0");
        info.MaxSets.Should().Be(5);
        info.MaxDataWithFlags.Should().Be(24);
        info.MaxDataNoFlags.Should().Be(27);
        info.MinInterval.Should().Be(32);
        info.MaxInterval.Should().Be(16384);
        info.KeepAliveMs.Should().Be(1000);
        info.DeviceId.Should().Be("24:0A:BC:11:22:33");
    }

    [Fact]
    public void Decoder_HelloResponseWithWrongMagic_IsRejected()
    {
        var payload = ParseSingle(GoldenVectors.HelloRsp).Payload.ToArray();
        payload[0] = (byte)'X';

        StickResponseDecoder.TryDecodeHello(new StickFrame(StickProtocol.TypeHelloRsp, 1, payload), out _).Should().BeFalse();
    }

    [Fact]
    public void Decoder_HelloResponseWithTrailingBytes_IsAccepted()
    {
        var payload = ParseSingle(GoldenVectors.HelloRsp).Payload.Concat(new byte[] { 1, 2, 3 }).ToArray();

        StickResponseDecoder.TryDecodeHello(new StickFrame(StickProtocol.TypeHelloRsp, 1, payload), out var info).Should().BeTrue();
        info!.MaxSets.Should().Be(5);
    }

    [Fact]
    public void Decoder_AckForSetStart_ReturnsStatusAndAppliedPower()
    {
        StickResponseDecoder.TryDecodeAck(ParseSingle(GoldenVectors.AckSetStart), out var ack).Should().BeTrue();

        ack!.OriginalType.Should().Be(StickProtocol.TypeSetStart);
        ack.Status.Should().Be(StickStatus.Ok);
        ack.Extra.Should().Equal(0x7F);
    }

    [Fact]
    public void Decoder_AckForSetUpdate_ReturnsProcessingTimeExtra()
    {
        StickResponseDecoder.TryDecodeAck(ParseSingle(GoldenVectors.AckSetUpdate), out var ack).Should().BeTrue();

        ack!.OriginalType.Should().Be(StickProtocol.TypeSetUpdate);
        BitConverter.ToUInt32(ack.Extra).Should().Be(891u);
    }

    [Fact]
    public void Decoder_Pong_ReturnsState()
    {
        StickResponseDecoder.TryDecodePong(ParseSingle(GoldenVectors.Pong), out var pong).Should().BeTrue();

        pong!.Advertising.Should().BeTrue();
        pong.ActiveMask.Should().Be(1);
        pong.ParseErrors.Should().Be(0);
        pong.UptimeMs.Should().Be(10000u);
    }

    [Fact]
    public void Decoder_TimeoutEvent_ReturnsTimeout()
    {
        StickResponseDecoder.TryDecodeEvent(ParseSingle(GoldenVectors.EvtTimeout), out var evt).Should().BeTrue();

        evt!.Kind.Should().Be(StickEventKind.Timeout);
        evt.TimeoutMs.Should().Be(1000);
    }

    [Fact]
    public void Decoder_UnknownEvent_IsIgnored()
    {
        StickResponseDecoder.TryDecodeEvent(new StickFrame(0xE5, 0, []), out _).Should().BeFalse();
    }

    private static StickFrame ParseSingle(string hex)
    {
        var frames = new StickFrameParser(TimeProvider.System).Feed(GoldenVectors.Bytes(hex));
        frames.Should().HaveCount(1);
        return frames[0];
    }
}
