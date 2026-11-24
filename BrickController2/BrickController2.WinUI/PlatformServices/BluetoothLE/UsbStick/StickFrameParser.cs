using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Streaming parser for the byte stream coming from the stick. It implements the resync rules of
/// protocol.md section 2: garbage is skipped, a bad LEN or CRC only discards the SOF byte (the
/// buffered bytes are scanned again so a valid frame inside a corrupted one is not lost) and a
/// frame that is incomplete for more than 200 ms is dropped when the next byte arrives.
/// Not thread-safe: the caller serialises calls to <see cref="Feed"/>.
/// </summary>
internal sealed class StickFrameParser
{
    private readonly List<byte> _buffer = new();
    private readonly TimeProvider _clock;

    // Timestamp at which the bytes at the start of the buffer were first seen (frame candidate).
    private long _candidateStart;
    private bool _hasCandidate;

    public StickFrameParser(TimeProvider clock)
    {
        _clock = clock;
    }

    /// <summary>
    /// Number of malformed frames seen so far (bad LEN, bad CRC, 200 ms timeout).
    /// </summary>
    public int ParseErrors { get; private set; }

    /// <summary>
    /// Adds received bytes and returns all frames that became complete.
    /// </summary>
    /// <param name="data">Newly received bytes (any chunking is allowed).</param>
    /// <returns>Zero or more complete, CRC-checked frames in arrival order.</returns>
    public IReadOnlyList<StickFrame> Feed(ReadOnlySpan<byte> data)
    {
        var frames = new List<StickFrame>();

        // Rule 4: an old half frame must not block the parser. The age is checked when new bytes arrive.
        if (data.Length > 0 && _hasCandidate && _clock.GetElapsedTime(_candidateStart) > StickProtocol.FrameTimeout)
        {
            DiscardSof();
            Scan(frames);
        }

        foreach (byte b in data)
        {
            _buffer.Add(b);
            Scan(frames);
        }

        return frames;
    }

    /// <summary>
    /// Extracts every complete frame from the buffer and applies the resync rules.
    /// </summary>
    private void Scan(List<StickFrame> frames)
    {
        while (true)
        {
            // Rule 1: skip garbage up to the next SOF.
            int sof = _buffer.IndexOf(StickProtocol.Sof);
            if (sof < 0)
            {
                _buffer.Clear();
                _hasCandidate = false;
                return;
            }

            if (sof > 0)
            {
                _buffer.RemoveRange(0, sof);
                _hasCandidate = false; // a new candidate starts here
            }

            // Remember when this candidate frame started (used for the 200 ms rule).
            if (!_hasCandidate)
            {
                _candidateStart = _clock.GetTimestamp();
                _hasCandidate = true;
            }

            if (_buffer.Count < 2)
            {
                return; // LEN not received yet
            }

            // Rule 2: invalid LEN -> discard only the SOF byte and hunt again.
            int len = _buffer[1];
            if (len < StickProtocol.MinLen || len > StickProtocol.MaxLen)
            {
                DiscardSof();
                continue;
            }

            int total = len + StickProtocol.FrameOverhead;
            if (_buffer.Count < total)
            {
                return; // wait for the rest of the frame
            }

            // Rule 3: CRC mismatch -> discard only the SOF byte and re-scan the buffered bytes.
            byte[] frameBytes = new byte[total];
            _buffer.CopyTo(0, frameBytes, 0, total);

            ushort expected = BinaryPrimitives.ReadUInt16LittleEndian(frameBytes.AsSpan(len + 2));
            if (Crc16.Compute(frameBytes.AsSpan(1, len + 1)) != expected)
            {
                DiscardSof();
                continue;
            }

            frames.Add(new StickFrame(frameBytes[2], frameBytes[3], frameBytes.AsSpan(4, len - 2).ToArray()));
            _buffer.RemoveRange(0, total);
            _hasCandidate = false;
        }
    }

    /// <summary>
    /// Drops the first buffered byte (the SOF of a rejected candidate) and counts a parse error.
    /// </summary>
    private void DiscardSof()
    {
        _buffer.RemoveAt(0);
        _hasCandidate = false;
        ParseErrors++;
    }
}
