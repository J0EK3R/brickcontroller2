using System;
using System.Collections.Generic;
using System.Linq;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using FluentAssertions;
using Xunit;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Resync rules of the streaming parser (protocol.md section 2).
/// </summary>
public class StickFrameParserTests
{
    private static readonly byte[] Pong = GoldenVectors.Bytes(GoldenVectors.Pong);
    private static readonly byte[] Ack = GoldenVectors.Bytes(GoldenVectors.AckSetStart);

    [Fact]
    public void Feed_GoldenFrame_ReturnsFrameWithTypeSeqAndPayload()
    {
        var frames = new StickFrameParser(TimeProvider.System).Feed(Pong);

        frames.Should().ContainSingle();
        frames[0].Type.Should().Be(StickProtocol.TypePong);
        frames[0].Seq.Should().Be(2);
        frames[0].Payload.Should().HaveCount(9);
    }

    [Fact]
    public void Feed_ByteWise_ReturnsFrameOnLastByte()
    {
        var parser = new StickFrameParser(TimeProvider.System);
        var all = new List<StickFrame>();

        foreach (byte b in Pong)
        {
            all.AddRange(parser.Feed([b]));
        }

        all.Should().ContainSingle();
        parser.ParseErrors.Should().Be(0);
    }

    [Fact]
    public void Feed_TwoFramesSplitAtEveryPosition_AreAlwaysReturned()
    {
        byte[] stream = Pong.Concat(Ack).ToArray();

        for (int split = 0; split <= stream.Length; split++)
        {
            var parser = new StickFrameParser(TimeProvider.System);
            var frames = parser.Feed(stream[..split]).Concat(parser.Feed(stream[split..])).ToList();

            frames.Select(f => f.Type).Should().Equal(new[] { StickProtocol.TypePong, StickProtocol.TypeAck }, $"split at {split}");
            parser.ParseErrors.Should().Be(0);
        }
    }

    [Fact]
    public void Feed_GarbageBeforeFrame_IsSkippedWithoutError()
    {
        var parser = new StickFrameParser(TimeProvider.System);

        var frames = parser.Feed(new byte[] { 0x00, 0x41, 0x42 }.Concat(Pong).ToArray());

        frames.Should().ContainSingle();
        parser.ParseErrors.Should().Be(0);
    }

    [Fact]
    public void Feed_InvalidLen_DiscardsOnlySofAndFindsLaterFrame()
    {
        var parser = new StickFrameParser(TimeProvider.System);

        // SOF followed by LEN 0xFF (too large), then a valid frame.
        var frames = parser.Feed(new byte[] { StickProtocol.Sof, 0xFF }.Concat(Pong).ToArray());

        frames.Should().ContainSingle();
        parser.ParseErrors.Should().Be(1);
    }

    [Fact]
    public void Feed_LenBelowMinimum_IsAParseError()
    {
        var parser = new StickFrameParser(TimeProvider.System);

        var frames = parser.Feed(new byte[] { StickProtocol.Sof, 0x01 }.Concat(Pong).ToArray());

        frames.Should().ContainSingle();
        parser.ParseErrors.Should().Be(1);
    }

    [Fact]
    public void Feed_BadCrc_IsDroppedAndCounted()
    {
        var parser = new StickFrameParser(TimeProvider.System);
        var corrupt = (byte[])Pong.Clone();
        corrupt[^1] ^= 0xFF;

        parser.Feed(corrupt).Should().BeEmpty();
        parser.ParseErrors.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Feed_ValidFrameInsideCorruptFrame_IsNotLost()
    {
        var parser = new StickFrameParser(TimeProvider.System);

        // A corrupt frame header whose announced length swallows a complete valid frame.
        // SOF, LEN 0x20 ... then the valid ACK; the CRC of the outer candidate cannot match.
        var outer = new byte[] { StickProtocol.Sof, 0x20, 0x80, 0x01 }.Concat(Ack).Concat(new byte[40]).ToArray();

        var frames = parser.Feed(outer);

        frames.Should().ContainSingle(f => f.Type == StickProtocol.TypeAck && f.Seq == 3);
    }

    [Fact]
    public void Feed_HalfFrameOlderThan200ms_IsDroppedWhenNextByteArrives()
    {
        var clock = new ManualTimeProvider();
        var parser = new StickFrameParser(clock);

        // First half of a frame, then silence for 250 ms.
        parser.Feed(Pong[..5]).Should().BeEmpty();
        clock.Advance(TimeSpan.FromMilliseconds(250));

        // A complete new frame must now parse; the stale half frame would otherwise swallow its bytes.
        var frames = parser.Feed(Ack);

        frames.Should().ContainSingle(f => f.Type == StickProtocol.TypeAck);
        parser.ParseErrors.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Feed_FrameCompletedWithin200ms_IsAccepted()
    {
        var clock = new ManualTimeProvider();
        var parser = new StickFrameParser(clock);

        parser.Feed(Pong[..5]);
        clock.Advance(TimeSpan.FromMilliseconds(150));

        parser.Feed(Pong[5..]).Should().ContainSingle();
        parser.ParseErrors.Should().Be(0);
    }
}
