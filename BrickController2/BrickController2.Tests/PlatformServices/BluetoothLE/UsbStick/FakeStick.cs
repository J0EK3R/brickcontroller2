using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Simulator of the stick firmware behind a <see cref="FakeStickTransport"/>: speaks the real frame
/// format (using the production parser/encoder, which are verified against the golden vectors), keeps
/// a session, a set table and the keep-alive timeout, and records everything it receives.
/// </summary>
internal sealed class FakeStickDevice
{
    private readonly ManualTimeProvider _clock;
    private readonly StickFrameParser _parser;
    private readonly object _lock = new();
    private readonly ITimer _keepAliveTimer;
    private TimeSpan _lastValidFrame;
    private int _keepAliveMs = 1000;

    public FakeStickDevice(ManualTimeProvider clock, int maxSets = 5)
    {
        _clock = clock;
        MaxSets = maxSets;
        _parser = new StickFrameParser(clock);
        _lastValidFrame = clock.Now;

        // The firmware checks its keep-alive timer in its own task; poll every 10 ms of fake time.
        _keepAliveTimer = clock.CreateTimer(_ => CheckKeepAlive(), null, TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10));
    }

    /// <summary>Number of sets the simulated stick supports.</summary>
    public int MaxSets { get; }

    /// <summary>When true the stick ignores everything (hung firmware, or a foreign board on the port).</summary>
    public bool Silent { get; set; }

    /// <summary>Protocol major reported in HELLO answers; a different value makes the stick refuse our HELLO.</summary>
    public byte AcceptedMajor { get; set; } = 1;

    /// <summary>Magic in HELLO_RSP; anything else simulates a foreign firmware.</summary>
    public byte[] Magic { get; set; } = StickProtocol.HelloMagic;

    /// <summary>All valid request frames received, with the fake time they arrived.</summary>
    public List<(TimeSpan Time, StickFrame Frame)> Received { get; } = new();

    /// <summary>True between HELLO and keep-alive timeout.</summary>
    public bool HasSession { get; private set; }

    /// <summary>Running sets: id -> (mfr, data).</summary>
    public Dictionary<byte, (ushort Mfr, byte[] Data, ushort Interval, sbyte Tx, byte Flags)> Sets { get; } = new();

    /// <summary>Number of malformed frames the stick saw (must stay 0 in the tests).</summary>
    public int ParseErrors => _parser.ParseErrors;

    /// <summary>The transport currently connected to the stick (set by the transport).</summary>
    public FakeStickTransport? Transport { get; set; }

    /// <summary>Number of upcoming requests that are processed but whose answer is dropped.</summary>
    public int DropNextResponses { get; set; }

    /// <summary>Request frames of one type that were received.</summary>
    public List<StickFrame> Requests(byte type)
    {
        lock (_lock)
        {
            return Received.Where(r => r.Frame.Type == type).Select(r => r.Frame).ToList();
        }
    }

    /// <summary>Ends the session like the firmware's own keep-alive timeout does (sets stop, requests answer NO_SESSION).</summary>
    public void EndSession()
    {
        lock (_lock)
        {
            HasSession = false;
            Sets.Clear();
        }
    }

    /// <summary>Simulates a button press: all sets stop, the session stays.</summary>
    public void PressButton()
    {
        lock (_lock)
        {
            Sets.Clear();
        }

        SendEvent(StickProtocol.TypeEvtButton, []);
    }

    /// <summary>Simulates a BLE controller error: the set vanishes and the stick reports it.</summary>
    public void StopSetByFirmware(byte setId)
    {
        lock (_lock)
        {
            Sets.Remove(setId);
        }

        SendEvent(StickProtocol.TypeEvtSetStopped, [setId, 1]);
    }

    /// <summary>Feeds bytes written by the host into the firmware.</summary>
    public void OnBytesFromHost(byte[] data)
    {
        if (Silent)
        {
            return;
        }

        IReadOnlyList<StickFrame> frames;
        lock (_lock)
        {
            frames = _parser.Feed(data);
        }

        foreach (var frame in frames)
        {
            lock (_lock)
            {
                _lastValidFrame = _clock.Now;
                Received.Add((_clock.Now, frame));
            }

            HandleRequest(frame);
        }
    }

    private void HandleRequest(StickFrame frame)
    {
        byte[]? response = null;
        lock (_lock)
        {
            response = frame.Type switch
            {
                StickProtocol.TypeHello => HandleHello(frame),
                _ when !HasSession && frame.Type is StickProtocol.TypePing or StickProtocol.TypeSetStart or StickProtocol.TypeSetUpdate or StickProtocol.TypeSetStop or StickProtocol.TypeStopAll
                    => Ack(frame, StickStatus.NoSession),
                StickProtocol.TypePing => Respond(frame, StickProtocol.TypePong, [(byte)(Sets.Count > 0 ? 1 : 0), (byte)ActiveMask(), (byte)(ActiveMask() >> 8), 0, 0, 0, 0, 0, 0]),
                StickProtocol.TypeSetStart => HandleSetStart(frame),
                StickProtocol.TypeSetUpdate => HandleSetUpdate(frame),
                StickProtocol.TypeSetStop => HandleSetStop(frame),
                StickProtocol.TypeStopAll => HandleStopAll(frame),
                _ => Ack(frame, StickStatus.UnknownCmd),
            };

            if (DropNextResponses > 0)
            {
                DropNextResponses--;
                response = null;
            }
        }

        if (response != null)
        {
            Transport?.Receive(response);
        }
    }

    private byte[] HandleHello(StickFrame frame)
    {
        if (frame.Payload[0] != AcceptedMajor)
        {
            return Ack(frame, StickStatus.VersionUnsupported);
        }

        Sets.Clear();
        HasSession = true;
        _keepAliveMs = Math.Clamp(frame.Payload[2] | (frame.Payload[3] << 8), 200, 10000);

        var payload = new List<byte>();
        payload.AddRange(Magic);
        payload.AddRange([0, 1, 0, 0, 1, 0, (byte)MaxSets, 24, 27, 32, 0, 0x00, 0x40, (byte)_keepAliveMs, (byte)(_keepAliveMs >> 8), 0x24, 0x0A, 0xBC, 0x11, 0x22, 0x33]);
        return Respond(frame, StickProtocol.TypeHelloRsp, payload.ToArray());
    }

    private byte[] HandleSetStart(StickFrame frame)
    {
        byte id = frame.Payload[0];
        if (id >= MaxSets)
        {
            return Ack(frame, StickStatus.BadParam);
        }

        if (Sets.ContainsKey(id))
        {
            return Ack(frame, StickStatus.AlreadyRunning);
        }

        int len = frame.Payload[7];
        Sets[id] = ((ushort)(frame.Payload[5] | (frame.Payload[6] << 8)), frame.Payload[8..(8 + len)],
            (ushort)(frame.Payload[2] | (frame.Payload[3] << 8)), (sbyte)frame.Payload[4], frame.Payload[1]);
        return Ack(frame, StickStatus.Ok, frame.Payload[4]);
    }

    private byte[] HandleSetUpdate(StickFrame frame)
    {
        byte id = frame.Payload[0];
        if (!Sets.TryGetValue(id, out var set))
        {
            return Ack(frame, StickStatus.SetNotFound);
        }

        int len = frame.Payload[3];
        Sets[id] = (((ushort)(frame.Payload[1] | (frame.Payload[2] << 8))), frame.Payload[4..(4 + len)], set.Interval, set.Tx, set.Flags);
        return Ack(frame, StickStatus.Ok, 0x7B, 0x03, 0, 0);
    }

    private byte[] HandleSetStop(StickFrame frame)
        => Ack(frame, Sets.Remove(frame.Payload[0]) ? StickStatus.Ok : StickStatus.SetNotFound);

    private byte[] HandleStopAll(StickFrame frame)
    {
        Sets.Clear();
        return Ack(frame, StickStatus.Ok);
    }

    private int ActiveMask() => Sets.Keys.Aggregate(0, (mask, id) => mask | (1 << id));

    private static byte[] Ack(StickFrame request, StickStatus status, params byte[] extra)
        => Respond(request, StickProtocol.TypeAck, new[] { request.Type, (byte)status }.Concat(extra).ToArray());

    private static byte[] Respond(StickFrame request, byte type, byte[] payload)
        => StickFrameEncoder.Encode(type, request.Seq, payload);

    private void SendEvent(byte type, byte[] payload)
        => Transport?.Receive(StickFrameEncoder.Encode(type, 0, payload));

    /// <summary>Firmware keep-alive: no valid frame for keepalive_ms ends the session and stops all sets.</summary>
    private void CheckKeepAlive()
    {
        bool timedOut;
        lock (_lock)
        {
            timedOut = HasSession && !Silent && _clock.Now - _lastValidFrame > TimeSpan.FromMilliseconds(_keepAliveMs);
            if (timedOut)
            {
                HasSession = false;
                Sets.Clear();
            }
        }

        if (timedOut)
        {
            SendEvent(StickProtocol.TypeEvtTimeout, [(byte)_keepAliveMs, (byte)(_keepAliveMs >> 8)]);
        }
    }
}

/// <summary>
/// Transport that is wired to a <see cref="FakeStickDevice"/> (or to nothing, for a dead port).
/// Writes are delivered synchronously, so responses arrive before WriteAsync returns.
/// </summary>
internal sealed class FakeStickTransport : IStickTransport
{
    private readonly FakeStickDevice? _device;

    public FakeStickTransport(string name, FakeStickDevice? device)
    {
        Name = name;
        _device = device;
    }

    public string Name { get; }

    /// <summary>True after Open and before Close/Dispose.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>True when the port was opened at least once.</summary>
    public bool WasOpened { get; private set; }

    /// <summary>When set, Open throws (port in use by another program).</summary>
    public bool FailOpen { get; set; }

    /// <summary>Splits received data into chunks of this size (0 = deliver as is).</summary>
    public int ChunkSize { get; set; }

    public event Action<byte[]>? DataReceived;

    public event Action? Disconnected;

    public void Open()
    {
        if (FailOpen)
        {
            throw new IOException("Access denied.");
        }

        IsOpen = true;
        WasOpened = true;
        if (_device != null)
        {
            _device.Transport = this;
        }
    }

    public void Close() => IsOpen = false;

    public void Dispose() => IsOpen = false;

    public Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        if (!IsOpen)
        {
            throw new IOException("Port closed.");
        }

        _device?.OnBytesFromHost(data.ToArray());
        return Task.CompletedTask;
    }

    /// <summary>Delivers bytes from the stick to the host.</summary>
    public void Receive(byte[] data)
    {
        if (!IsOpen)
        {
            return;
        }

        if (ChunkSize <= 0)
        {
            DataReceived?.Invoke(data);
            return;
        }

        foreach (var chunk in data.Chunk(ChunkSize))
        {
            DataReceived?.Invoke(chunk);
        }
    }

    /// <summary>Simulates pulling the USB cable: the port reports a disconnect and writes fail.</summary>
    public void Unplug()
    {
        IsOpen = false;
        Disconnected?.Invoke();
    }
}

/// <summary>
/// Locator whose candidate list the test controls ("plug in" = add a device, "unplug" = remove).
/// </summary>
internal sealed class FakeStickLocator : IStickLocator
{
    /// <summary>Ports that currently exist: name -> device behind it (null = dead port without a device).</summary>
    public Dictionary<string, FakeStickDevice?> Ports { get; } = new();

    /// <summary>Every transport handed out so far (for assertions on closing).</summary>
    public List<FakeStickTransport> Created { get; } = new();

    public IEnumerable<IStickTransport> EnumerateCandidates()
    {
        foreach (var (name, device) in Ports.ToArray())
        {
            var transport = new FakeStickTransport(name, device);
            Created.Add(transport);
            yield return transport;
        }
    }
}
