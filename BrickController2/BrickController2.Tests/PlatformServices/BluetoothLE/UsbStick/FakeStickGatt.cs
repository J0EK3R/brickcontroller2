// GATT / scan simulation of the fake stick (protocol 1.1): scriptable peripherals, connect behaviour,
// deferred procedures, notifications, BUSY / ATT errors and link loss. Used by all GATT tests.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Characteristic of a simulated peripheral.
/// </summary>
internal sealed record FakeChar(ushort Uuid16, ushort ValueHandle, ushort CccdHandle, byte Properties, byte[]? Uuid128 = null);

/// <summary>
/// Service of a simulated peripheral (characteristics live in the handle range).
/// </summary>
internal sealed record FakeService(ushort Uuid16, ushort Start, ushort End, byte[]? Uuid128 = null);

/// <summary>
/// A simulated BLE peripheral the fake stick can scan and connect to.
/// </summary>
internal sealed class FakePeripheral
{
    public FakePeripheral(string address, byte addressType = 1)
    {
        Address = address;
        AddressType = addressType;
    }

    /// <summary>
    /// Address as "AA:BB:CC:DD:EE:FF".
    /// </summary>
    public string Address { get; }

    /// <summary>
    /// 0 public, 1 random.
    /// </summary>
    public byte AddressType { get; }

    /// <summary>
    /// MTU reported in EVT_CONNECTED.
    /// </summary>
    public ushort Mtu { get; set; } = 64;

    /// <summary>
    /// Raw AD data of the advertising PDU.
    /// </summary>
    public byte[] AdvData { get; set; } = [];

    /// <summary>
    /// Raw AD data of the scan response PDU (empty = none).
    /// </summary>
    public byte[] ScanResponseData { get; set; } = [];

    /// <summary>
    /// Fake time between CONNECT and EVT_CONNECTED (0 = immediately after the ACK).
    /// </summary>
    public int ConnectDelayMs { get; set; }

    /// <summary>
    /// When set the connect attempt fails with EVT_DISCONNECTED (phase 0, reason 0x3E).
    /// </summary>
    public bool RefuseConnection { get; set; }

    /// <summary>
    /// When set the peripheral never answers: no EVT_CONNECTED, only the stick's own timeout.
    /// </summary>
    public bool NeverConnects { get; set; }

    /// <summary>
    /// Services in handle order.
    /// </summary>
    public List<FakeService> Services { get; } = new();

    /// <summary>
    /// Characteristics in handle order.
    /// </summary>
    public List<FakeChar> Chars { get; } = new();

    /// <summary>
    /// Values returned by READ, by value handle.
    /// </summary>
    public Dictionary<ushort, byte[]> ReadValues { get; } = new();

    /// <summary>
    /// Adds a service with 16 bit UUID covering the given handle range.
    /// </summary>
    public FakePeripheral WithService(ushort uuid16, ushort start, ushort end)
    {
        Services.Add(new FakeService(uuid16, start, end));
        return this;
    }

    /// <summary>
    /// Adds a characteristic (cccd 0 = none).
    /// </summary>
    public FakePeripheral WithChar(ushort uuid16, ushort valueHandle, ushort cccdHandle, byte properties, byte[]? uuid128 = null)
    {
        Chars.Add(new FakeChar(uuid16, valueHandle, cccdHandle, properties, uuid128));
        return this;
    }
}

/// <summary>
/// One write seen by the fake stick.
/// </summary>
internal sealed record FakeWrite(byte ConnId, ushort Handle, byte[] Data, bool WithResponse);

/// <summary>
/// State of one connection slot of the fake stick.
/// </summary>
internal sealed class FakeLink
{
    public FakeLink(FakePeripheral peripheral, bool background)
    {
        Peripheral = peripheral;
        Background = background;
    }

    public FakePeripheral Peripheral { get; }

    public bool Background { get; }

    public bool Connected { get; set; }

    public ITimer? Timer { get; set; }

    /// <summary>
    /// Failsafe slots: slot -> (handle, data).
    /// </summary>
    public Dictionary<byte, (ushort Handle, byte[] Data)> Failsafe { get; } = new();
}

/// <summary>
/// GATT part of the simulated stick firmware.
/// </summary>
internal sealed partial class FakeStickDevice
{
    private readonly Dictionary<byte, FakeLink> _links = new();
    private readonly List<byte[]> _followUps = new();
    private StickFrame? _pendingProcedure;
    private byte _pendingConnId;

    /// <summary>
    /// True: HELLO_RSP has 32 bytes and minor 1 (default). False simulates a 1.0 firmware.
    /// </summary>
    public bool GattEnabled { get; set; } = true;

    /// <summary>
    /// Number of connection slots reported in HELLO_RSP (0 = no GATT in the build).
    /// </summary>
    public int MaxConns { get; }

    /// <summary>
    /// Peripherals the stick can see, by address.
    /// </summary>
    public Dictionary<string, FakePeripheral> Peripherals { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// True between SCAN_START and SCAN_STOP.
    /// </summary>
    public bool Scanning { get; private set; }

    /// <summary>
    /// Flags byte of the last SCAN_START (bit0 = active).
    /// </summary>
    public byte? LastScanFlags { get; private set; }

    /// <summary>
    /// When true SCAN_START is answered with NO_RESOURCES (controller busy).
    /// </summary>
    public bool RefuseScan { get; set; }

    /// <summary>
    /// Number of SCAN_START requests received.
    /// </summary>
    public int ScanStartCount { get; private set; }

    /// <summary>
    /// Number of SCAN_STOP requests received.
    /// </summary>
    public int ScanStopCount { get; private set; }

    /// <summary>
    /// Every CONNECT: conn_id, address, addr_type, flags.
    /// </summary>
    public List<(byte ConnId, string Address, byte AddressType, byte Flags)> ConnectRequests { get; } = new();

    /// <summary>
    /// Every WRITE / WRITE_CMD that reached the stick (also those answered with an error).
    /// </summary>
    public List<FakeWrite> Writes { get; } = new();

    /// <summary>
    /// Every SUBSCRIBE: conn_id, cccd handle, mode.
    /// </summary>
    public List<(byte ConnId, ushort Cccd, byte Mode)> Subscribes { get; } = new();

    /// <summary>
    /// Conn ids that received a DISCONNECT request.
    /// </summary>
    public List<byte> DisconnectRequests { get; } = new();

    /// <summary>
    /// Slots (conn ids) with a link or an attempt.
    /// </summary>
    public IReadOnlyCollection<byte> ActiveLinks
    {
        get
        {
            lock (_lock)
            {
                return _links.Keys.ToArray();
            }
        }
    }

    /// <summary>
    /// Status the next procedures (READ, WRITE, SUBSCRIBE) answer with; consumed one per request. Empty = OK.
    /// </summary>
    public Queue<(StickStatus Status, byte AttError)> ProcedureResults { get; } = new();

    /// <summary>
    /// Number of upcoming WRITE_CMD requests answered with BUSY.
    /// </summary>
    public int BusyWriteCommands { get; set; }

    /// <summary>
    /// When true READ / WRITE / SUBSCRIBE are not answered until <see cref="ResolvePending"/> (or a link loss).
    /// </summary>
    public bool DeferProcedures { get; set; }

    /// <summary>
    /// Status of EVT_DISCOVER_DONE (anything but OK sends no further characteristics).
    /// </summary>
    public StickStatus DiscoverStatus { get; set; } = StickStatus.Ok;

    /// <summary>
    /// When true discovery never finishes (no EVT_DISCOVER_DONE).
    /// </summary>
    public bool StallDiscovery { get; set; }

    /// <summary>
    /// When true a DISCONNECT is acknowledged but no EVT_DISCONNECTED follows.
    /// </summary>
    public bool SuppressDisconnectEvent { get; set; }

    /// <summary>
    /// Notifications sent right after the next SUBSCRIBE ACK (value handle, data), like the DUPLO hub does.
    /// </summary>
    public List<(ushort Handle, byte[] Data)> NotifyAfterSubscribe { get; } = new();

    /// <summary>
    /// Request types the stick answered since the start (for ordering assertions).
    /// </summary>
    public List<byte> RequestTypes
    {
        get
        {
            lock (_lock)
            {
                return Received.Select(r => r.Frame.Type).ToList();
            }
        }
    }

    // ------------------------------------------------------------------ scripting API

    /// <summary>
    /// Adds a peripheral the stick can scan and connect to.
    /// </summary>
    public FakePeripheral AddPeripheral(FakePeripheral peripheral)
    {
        lock (_lock)
        {
            Peripherals[peripheral.Address] = peripheral;
        }

        return peripheral;
    }

    /// <summary>
    /// Sends the advertising PDU (and the scan response when the peripheral has one and the scan is active).
    /// </summary>
    public void EmitAdvertising(FakePeripheral peripheral, sbyte rssi = -50)
    {
        SendScanReport(peripheral, peripheral.AdvData, scanResponse: false, rssi);
        if (peripheral.ScanResponseData.Length > 0 && (LastScanFlags & 1) == 1)
        {
            SendScanReport(peripheral, peripheral.ScanResponseData, scanResponse: true, rssi);
        }
    }

    /// <summary>
    /// Sends one EVT_SCAN_REPORT.
    /// </summary>
    public void SendScanReport(FakePeripheral peripheral, byte[] data, bool scanResponse, sbyte rssi = -50)
    {
        StickAddress.TryParse(peripheral.Address, out byte[] addr);
        byte flags = (byte)(0x01 | (scanResponse ? 0x02 : 0));
        SendEvent(StickProtocol.TypeEvtScanReport, [.. addr, peripheral.AddressType, (byte)rssi, flags, (byte)data.Length, .. data]);
    }

    /// <summary>
    /// Sends an EVT_NOTIFY for the connection.
    /// </summary>
    public void Notify(byte connId, ushort handle, byte[] data)
        => SendEvent(StickProtocol.TypeEvtNotify, [connId, (byte)handle, (byte)(handle >> 8), 0, .. data]);

    /// <summary>
    /// The peripheral drops the link (supervision timeout, peer switched off): EVT_DISCONNECTED.
    /// </summary>
    public void DropLink(byte connId, byte reason = 0x08)
    {
        bool wasConnected;
        lock (_lock)
        {
            if (!_links.Remove(connId, out FakeLink? link))
            {
                return;
            }

            link.Timer?.Dispose();
            wasConnected = link.Connected;
        }

        FailPendingProcedureFor(connId);
        SendEvent(StickProtocol.TypeEvtDisconnected, [connId, (byte)(wasConnected ? 1 : 0), reason]);
    }

    /// <summary>
    /// The stick's safety logic terminates every link and sends EVT_DISCONNECTED (reason 0xF2) for each.
    /// </summary>
    public void DropAllLinks(byte reason)
    {
        foreach (byte id in ActiveLinks)
        {
            DropLink(id, reason);
        }

        lock (_lock)
        {
            Scanning = false;
        }
    }

    /// <summary>
    /// Completes the deferred procedure (READ / WRITE / SUBSCRIBE) with the given status.
    /// </summary>
    public void ResolvePending(StickStatus status = StickStatus.Ok, byte attError = 0)
    {
        StickFrame? frame;
        lock (_lock)
        {
            frame = _pendingProcedure;
            _pendingProcedure = null;
        }

        if (frame == null)
        {
            return;
        }

        Transport?.Receive(status == StickStatus.Ok
            ? Respond(frame, StickProtocol.TypeAck, [frame.Type, 0])
            : Respond(frame, StickProtocol.TypeAck, status == StickStatus.AttError ? [frame.Type, (byte)status, attError] : [frame.Type, (byte)status]));
    }

    /// <summary>
    /// Completes the deferred procedure with an OK ACK that names a different request type (a mixed-up answer).
    /// </summary>
    public void ResolvePendingWithWrongType(byte originalType)
    {
        StickFrame? frame;
        lock (_lock)
        {
            frame = _pendingProcedure;
            _pendingProcedure = null;
        }

        if (frame != null)
        {
            Transport?.Receive(Respond(frame, StickProtocol.TypeAck, [originalType, 0]));
        }
    }

    /// <summary>
    /// True while a procedure waits for <see cref="ResolvePending"/>.
    /// </summary>
    public bool HasPendingProcedure
    {
        get
        {
            lock (_lock)
            {
                return _pendingProcedure != null;
            }
        }
    }

    /// <summary>
    /// Writes with the given handle (all connections).
    /// </summary>
    public List<FakeWrite> WritesTo(ushort handle)
    {
        lock (_lock)
        {
            return Writes.Where(w => w.Handle == handle).ToList();
        }
    }

    // ------------------------------------------------------------------ request handling (called under _lock)

    private static bool IsGattRequest(byte type) => type is >= StickProtocol.TypeScanStart and <= StickProtocol.TypeSetFailsafe;

    private List<byte[]> TakeFollowUps()
    {
        var list = _followUps.ToList();
        _followUps.Clear();
        return list;
    }

    private void ResetGatt()
    {
        foreach (var link in _links.Values)
        {
            link.Timer?.Dispose();
        }

        _links.Clear();
        _pendingProcedure = null;
        Scanning = false;
    }

    private void QueueEvent(byte type, byte[] payload) => _followUps.Add(StickFrameEncoder.Encode(type, 0, payload));

    private byte[] HandleGatt(StickFrame frame)
    {
        if (!GattEnabled)
        {
            return Ack(frame, StickStatus.UnknownCmd);
        }

        return frame.Type switch
        {
            StickProtocol.TypeScanStart => HandleScanStart(frame),
            StickProtocol.TypeScanStop => HandleScanStop(frame),
            StickProtocol.TypeConnect => HandleConnect(frame),
            StickProtocol.TypeDisconnect => HandleDisconnect(frame),
            StickProtocol.TypeDiscover => HandleDiscover(frame),
            StickProtocol.TypeRead => HandleRead(frame),
            StickProtocol.TypeWrite or StickProtocol.TypeWriteCmd => HandleWrite(frame),
            StickProtocol.TypeSubscribe => HandleSubscribe(frame),
            StickProtocol.TypeGetGattStatus => HandleGattStatus(frame),
            StickProtocol.TypeSetFailsafe => HandleFailsafe(frame),
            _ => Ack(frame, StickStatus.UnknownCmd),
        };
    }

    private byte[] HandleScanStart(StickFrame frame)
    {
        ScanStartCount++;
        if (RefuseScan)
        {
            return Ack(frame, StickStatus.NoResources);
        }

        if (Scanning)
        {
            return Ack(frame, StickStatus.AlreadyRunning);
        }

        Scanning = true;
        LastScanFlags = frame.Payload[0];
        return Ack(frame, StickStatus.Ok);
    }

    private byte[] HandleScanStop(StickFrame frame)
    {
        ScanStopCount++;
        Scanning = false;
        return Ack(frame, StickStatus.Ok);
    }

    private byte[] HandleConnect(StickFrame frame)
    {
        byte connId = frame.Payload[0];
        string address = StickAddress.Format(frame.Payload.AsSpan(1, 6));
        byte addressType = frame.Payload[7];
        byte flags = frame.Payload[8];
        ConnectRequests.Add((connId, address, addressType, flags));

        if (connId >= MaxConns)
        {
            return Ack(frame, StickStatus.BadParam);
        }

        if (_links.ContainsKey(connId) || _links.Values.Any(l => l.Peripheral.Address == address))
        {
            return Ack(frame, StickStatus.AlreadyRunning);
        }

        if (!Peripherals.TryGetValue(address, out FakePeripheral? peripheral))
        {
            // Unknown device: the attempt ends with the stick's own connect timeout (not simulated further).
            peripheral = new FakePeripheral(address) { NeverConnects = true };
        }

        var link = new FakeLink(peripheral, (flags & StickProtocol.ConnectFlagBackground) != 0);
        _links[connId] = link;

        if (peripheral.NeverConnects)
        {
            if (!link.Background)
            {
                link.Timer = _clock.CreateTimer(_ => ConnectAttemptTimedOut(connId), null, TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);
            }
        }
        else if (peripheral.RefuseConnection)
        {
            _links.Remove(connId);
            QueueEvent(StickProtocol.TypeEvtDisconnected, [connId, 0, 0x3E]);
        }
        else if (peripheral.ConnectDelayMs > 0)
        {
            link.Timer = _clock.CreateTimer(_ => CompleteConnect(connId), null, TimeSpan.FromMilliseconds(peripheral.ConnectDelayMs), Timeout.InfiniteTimeSpan);
        }
        else
        {
            link.Connected = true;
            QueueEvent(StickProtocol.TypeEvtConnected, ConnectedPayload(connId, link));
        }

        return Ack(frame, StickStatus.Ok);
    }

    private static byte[] ConnectedPayload(byte connId, FakeLink link)
    {
        StickAddress.TryParse(link.Peripheral.Address, out byte[] addr);
        return [connId, (byte)link.Peripheral.Mtu, (byte)(link.Peripheral.Mtu >> 8), 24, 0, .. addr];
    }

    private void CompleteConnect(byte connId)
    {
        byte[] payload;
        lock (_lock)
        {
            if (!_links.TryGetValue(connId, out FakeLink? link))
            {
                return;
            }

            link.Connected = true;
            payload = ConnectedPayload(connId, link);
        }

        SendEvent(StickProtocol.TypeEvtConnected, payload);
    }

    private void ConnectAttemptTimedOut(byte connId)
    {
        lock (_lock)
        {
            if (!_links.Remove(connId))
            {
                return;
            }
        }

        SendEvent(StickProtocol.TypeEvtDisconnected, [connId, 0, StickProtocol.ReasonConnectTimeout]);
    }

    private byte[] HandleDisconnect(StickFrame frame)
    {
        byte connId = frame.Payload[0];
        DisconnectRequests.Add(connId);
        if (_links.Remove(connId, out FakeLink? link))
        {
            link.Timer?.Dispose();
            FailPendingProcedureLocked(connId);
            if (!SuppressDisconnectEvent)
            {
                QueueEvent(StickProtocol.TypeEvtDisconnected, [connId, (byte)(link.Connected ? 1 : 0), link.Connected ? StickProtocol.ReasonCancelled : (byte)0x16]);
            }
        }

        return Ack(frame, StickStatus.Ok);
    }

    private byte[] HandleDiscover(StickFrame frame)
    {
        byte connId = frame.Payload[0];
        if (!_links.TryGetValue(connId, out FakeLink? link) || !link.Connected)
        {
            return Ack(frame, StickStatus.NotConnected);
        }

        foreach (FakeService s in link.Peripheral.Services)
        {
            byte[] uuid = s.Uuid128 ?? [(byte)s.Uuid16, (byte)(s.Uuid16 >> 8)];
            QueueEvent(StickProtocol.TypeEvtGattService, [connId, (byte)s.Start, (byte)(s.Start >> 8), (byte)s.End, (byte)(s.End >> 8), (byte)uuid.Length, .. uuid]);
        }

        if (DiscoverStatus == StickStatus.Ok)
        {
            foreach (FakeChar c in link.Peripheral.Chars)
            {
                byte[] uuid = c.Uuid128 ?? [(byte)c.Uuid16, (byte)(c.Uuid16 >> 8)];
                QueueEvent(StickProtocol.TypeEvtGattChar, [connId, (byte)c.ValueHandle, (byte)(c.ValueHandle >> 8), (byte)c.CccdHandle, (byte)(c.CccdHandle >> 8), c.Properties, (byte)uuid.Length, .. uuid]);
            }
        }

        if (!StallDiscovery)
        {
            QueueEvent(StickProtocol.TypeEvtDiscoverDone, [connId, (byte)DiscoverStatus, (byte)link.Peripheral.Services.Count, (byte)link.Peripheral.Chars.Count]);
        }

        return Ack(frame, StickStatus.Ok);
    }

    /// <summary>
    /// Common checks of READ / WRITE / SUBSCRIBE; returns the early answer or null to continue.
    /// </summary>
    private byte[]? CheckProcedure(StickFrame frame, out FakeLink? link)
    {
        byte connId = frame.Payload[0];
        if (!_links.TryGetValue(connId, out link) || !link.Connected)
        {
            link = null;
            return Ack(frame, StickStatus.NotConnected);
        }

        return null;
    }

    private byte[]? NextProcedureAnswer(StickFrame frame)
    {
        if (DeferProcedures)
        {
            _pendingProcedure = frame;
            _pendingConnId = frame.Payload[0];
            return []; // marker: no answer now
        }

        if (ProcedureResults.TryDequeue(out var result) && result.Status != StickStatus.Ok)
        {
            return result.Status == StickStatus.AttError
                ? Ack(frame, StickStatus.AttError, result.AttError)
                : Ack(frame, result.Status);
        }

        return null;
    }

    private byte[] HandleRead(StickFrame frame)
    {
        byte[]? early = CheckProcedure(frame, out FakeLink? link);
        if (early != null)
        {
            return early;
        }

        byte[]? answer = NextProcedureAnswer(frame);
        if (answer != null)
        {
            return answer.Length == 0 ? null! : answer;
        }

        ushort handle = (ushort)(frame.Payload[1] | (frame.Payload[2] << 8));
        if (!link!.Peripheral.ReadValues.TryGetValue(handle, out byte[]? value))
        {
            return Ack(frame, StickStatus.AttError, 0x0A);
        }

        return Respond(frame, StickProtocol.TypeReadRsp, [frame.Payload[0], frame.Payload[1], frame.Payload[2], 0, .. value]);
    }

    private byte[] HandleWrite(StickFrame frame)
    {
        byte[]? early = CheckProcedure(frame, out FakeLink? link);
        if (early != null)
        {
            return early;
        }

        ushort handle = (ushort)(frame.Payload[1] | (frame.Payload[2] << 8));
        byte[] data = frame.Payload[3..];
        bool withResponse = frame.Type == StickProtocol.TypeWrite;
        if (data.Length > Math.Min(56, link!.Peripheral.Mtu - 3))
        {
            return Ack(frame, StickStatus.PayloadTooLarge);
        }

        if (!withResponse && BusyWriteCommands > 0)
        {
            BusyWriteCommands--;
            return Ack(frame, StickStatus.Busy);
        }

        Writes.Add(new FakeWrite(frame.Payload[0], handle, data, withResponse));
        if (!withResponse)
        {
            return Ack(frame, StickStatus.Ok);
        }

        byte[]? answer = NextProcedureAnswer(frame);
        return answer == null ? Ack(frame, StickStatus.Ok) : answer.Length == 0 ? null! : answer;
    }

    private byte[] HandleSubscribe(StickFrame frame)
    {
        byte[]? early = CheckProcedure(frame, out _);
        if (early != null)
        {
            return early;
        }

        Subscribes.Add((frame.Payload[0], (ushort)(frame.Payload[1] | (frame.Payload[2] << 8)), frame.Payload[3]));
        byte[]? answer = NextProcedureAnswer(frame);
        if (answer != null)
        {
            return answer.Length == 0 ? null! : answer;
        }

        // Notifications that start right after the CCCD write (DUPLO attached-IO messages).
        foreach (var (handle, data) in NotifyAfterSubscribe.ToList())
        {
            QueueEvent(StickProtocol.TypeEvtNotify, [frame.Payload[0], (byte)handle, (byte)(handle >> 8), 0, .. data]);
        }

        NotifyAfterSubscribe.Clear();
        return Ack(frame, StickStatus.Ok);
    }

    private byte[] HandleGattStatus(StickFrame frame)
    {
        int connecting = _links.Where(l => !l.Value.Connected).Aggregate(0, (m, l) => m | (1 << l.Key));
        int connected = _links.Where(l => l.Value.Connected).Aggregate(0, (m, l) => m | (1 << l.Key));
        return Respond(frame, StickProtocol.TypeGattStatusRsp, [(byte)(Scanning ? 1 : 0), (byte)connecting, (byte)(connecting >> 8), (byte)connected, (byte)(connected >> 8), 0, 0, 0, 0]);
    }

    private byte[] HandleFailsafe(StickFrame frame)
    {
        byte connId = frame.Payload[0];
        byte slot = frame.Payload[1];
        if (slot >= 4)
        {
            return Ack(frame, StickStatus.BadParam);
        }

        if (!_links.TryGetValue(connId, out FakeLink? link) || !link.Connected)
        {
            return Ack(frame, StickStatus.NotConnected);
        }

        if (frame.Payload.Length == 4)
        {
            link.Failsafe.Remove(slot);
        }
        else
        {
            link.Failsafe[slot] = ((ushort)(frame.Payload[2] | (frame.Payload[3] << 8)), frame.Payload[4..]);
        }

        return Ack(frame, StickStatus.Ok);
    }

    // ------------------------------------------------------------------ pending procedure helpers

    private void FailPendingProcedureFor(byte connId)
    {
        StickFrame? frame = null;
        lock (_lock)
        {
            if (_pendingProcedure != null && _pendingConnId == connId)
            {
                frame = _pendingProcedure;
                _pendingProcedure = null;
            }
        }

        if (frame != null)
        {
            Transport?.Receive(Ack(frame, StickStatus.Disconnected));
        }
    }

    private void FailPendingProcedureLocked(byte connId)
    {
        if (_pendingProcedure != null && _pendingConnId == connId)
        {
            _followUps.Insert(0, Ack(_pendingProcedure, StickStatus.Disconnected));
            _pendingProcedure = null;
        }
    }
}
