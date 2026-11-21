// Behavioural tests of StickGattDevice: how BC2's BluetoothDevice uses IBluetoothLEDevice, including
// device-shaped scenarios (DUPLO train hub, MouldKing DIY, WeDo 2.0) against the simulated stick.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BrickController2.PlatformServices.BluetoothLE;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

public class StickGattDeviceTests
{
    /// <summary>Everything a test needs around one device.</summary>
    private sealed class Rig : IDisposable
    {
        public StickHarness H { get; }
        public StickAddressBook Book { get; } = new();
        public StickGattDevice Device { get; private set; } = null!;
        public ConcurrentQueue<(Guid Uuid, byte[]? Data)> Notifications { get; } = new();
        public int Disconnects;

        public Rig(string address, FakePeripheral peripheral, int maxConns = 4)
        {
            H = new StickHarness(maxConns: maxConns);
            H.Stick!.AddPeripheral(peripheral);
            Address = address;
        }

        public string Address { get; }

        /// <summary>Creates the device (reserves a slot) after the stick is connected.</summary>
        public async Task<Rig> StartAsync()
        {
            await H.ConnectStickAsync();
            var link = H.Connection.TryAcquireLink()!;
            Device = new StickGattDevice(Address, link, () => H.Connection.TryAcquireLink(), Book, NullLogger.Instance);
            return this;
        }

        public Task<IEnumerable<IGattService>?> ConnectAsync(bool autoConnect = false, CancellationToken token = default)
            => Device.ConnectAndDiscoverServicesAsync(autoConnect, (uuid, data) => Notifications.Enqueue((uuid, data)), _ => Interlocked.Increment(ref Disconnects), token);

        public async Task<List<IGattService>> ConnectedServicesAsync(bool autoConnect = false)
        {
            var services = await H.RunAsync(ConnectAsync(autoConnect));
            services.Should().NotBeNull();
            return services!.ToList();
        }

        public void Dispose() => H.Dispose();
    }

    private static IGattCharacteristic Char(IEnumerable<IGattService> services, Guid uuid)
        => services.SelectMany(s => s.Characteristics).First(c => c.Uuid == uuid);

    private static IGattCharacteristic Char(IEnumerable<IGattService> services, ushort uuid16) => Char(services, StickUuid.FromShort(uuid16));

    // ------------------------------------------------------------------ connect / discover

    [Fact]
    public async Task Connect_ReturnsServicesAndBecomesConnected()
    {
        using var rig = await new Rig(GattFixtures.GenericAddress, GattFixtures.Generic()).StartAsync();

        rig.Device.State.Should().Be(BluetoothLEDeviceState.Disconnected);
        var services = await rig.ConnectedServicesAsync();

        rig.Device.State.Should().Be(BluetoothLEDeviceState.Connected);
        services.Select(s => s.Uuid).Should().Equal(StickUuid.FromShort(0x180A), StickUuid.FromShort(0xFFF0));
        services[1].Characteristics.Should().HaveCount(3);
    }

    [Fact]
    public async Task Connect_StateIsConnectingWhileWaiting()
    {
        var p = GattFixtures.Generic();
        p.ConnectDelayMs = 400;
        using var rig = await new Rig(GattFixtures.GenericAddress, p).StartAsync();

        var task = rig.ConnectAsync();
        await rig.H.PumpAsync(TimeSpan.FromMilliseconds(100));

        rig.Device.State.Should().Be(BluetoothLEDeviceState.Connecting);
        (await rig.H.RunAsync(task)).Should().NotBeNull();
    }

    [Theory]
    [InlineData(true, StickProtocol.ConnectFlagBackground)]
    [InlineData(false, 0)]
    public async Task Connect_AutoConnectMapsToBackgroundFlag(bool autoConnect, int expectedFlags)
    {
        using var rig = await new Rig(GattFixtures.GenericAddress, GattFixtures.Generic()).StartAsync();

        await rig.ConnectedServicesAsync(autoConnect);

        rig.H.Stick!.ConnectRequests.Single().Flags.Should().Be((byte)expectedFlags);
    }

    [Fact]
    public async Task Connect_UnknownAddressUsesAutoType_ScannedAddressUsesRememberedType()
    {
        using var rig = await new Rig(GattFixtures.GenericAddress, GattFixtures.Generic()).StartAsync();
        await rig.ConnectedServicesAsync();
        rig.H.Stick!.ConnectRequests.Single().AddressType.Should().Be(0xFF);
        await rig.H.RunAsync(rig.Device.DisconnectAsync());

        rig.Book.Remember(GattFixtures.GenericAddress, 1);
        await rig.ConnectedServicesAsync();

        rig.H.Stick.ConnectRequests.Last().AddressType.Should().Be(1);
    }

    [Fact]
    public async Task Connect_Failure_ReturnsNull_AndSlotIsFree()
    {
        var p = GattFixtures.Generic();
        p.RefuseConnection = true;
        using var rig = await new Rig(GattFixtures.GenericAddress, p, maxConns: 1).StartAsync();

        (await rig.H.RunAsync(rig.ConnectAsync())).Should().BeNull();

        rig.Device.State.Should().Be(BluetoothLEDeviceState.Disconnected);
        (await rig.H.PumpUntilAsync(() => rig.H.Connection.LinkCount == 0)).Should().BeTrue();
    }

    [Fact]
    public async Task Connect_AfterFailure_ReacquiresASlot()
    {
        var p = GattFixtures.Generic();
        p.RefuseConnection = true;
        using var rig = await new Rig(GattFixtures.GenericAddress, p, maxConns: 1).StartAsync();
        await rig.H.RunAsync(rig.ConnectAsync());
        await rig.H.PumpUntilAsync(() => rig.H.Connection.LinkCount == 0);
        p.RefuseConnection = false;

        (await rig.H.RunAsync(rig.ConnectAsync())).Should().NotBeNull();
    }

    [Fact]
    public async Task Connect_NoSlotAvailable_ReturnsNull()
    {
        using var rig = await new Rig(GattFixtures.GenericAddress, GattFixtures.Generic(), maxConns: 1).StartAsync();
        await rig.H.RunAsync(rig.Device.DisconnectAsync()); // gives the only slot back
        var thief = rig.H.Connection.TryAcquireLink();
        thief.Should().NotBeNull();

        (await rig.H.RunAsync(rig.ConnectAsync())).Should().BeNull();
    }

    [Fact]
    public async Task Connect_SecondCallWhileConnected_ReturnsNull()
    {
        using var rig = await new Rig(GattFixtures.GenericAddress, GattFixtures.Generic()).StartAsync();
        await rig.ConnectedServicesAsync();

        (await rig.H.RunAsync(rig.ConnectAsync())).Should().BeNull();
        rig.Device.State.Should().Be(BluetoothLEDeviceState.Connected, "a failed second call does not disturb the connection");
    }

    [Fact]
    public async Task Connect_CancelledWhileConnecting_ReturnsNullAndAbortsOnTheStick()
    {
        var p = GattFixtures.Generic();
        p.ConnectDelayMs = 5000;
        using var rig = await new Rig(GattFixtures.GenericAddress, p).StartAsync();
        using var cts = new CancellationTokenSource();

        var task = rig.ConnectAsync(token: cts.Token);
        await rig.H.PumpAsync(TimeSpan.FromMilliseconds(200));
        cts.Cancel();

        (await rig.H.RunAsync(task)).Should().BeNull();
        rig.Device.State.Should().Be(BluetoothLEDeviceState.Disconnected);
        rig.H.Stick!.DisconnectRequests.Should().NotBeEmpty();
        rig.H.Stick.ActiveLinks.Should().BeEmpty();
        (await rig.H.PumpUntilAsync(() => rig.H.Connection.LinkCount == 0)).Should().BeTrue();
    }

    [Fact]
    public async Task Connect_CancelledWhileDiscovering_ReturnsNull()
    {
        using var rig = await new Rig(GattFixtures.GenericAddress, GattFixtures.Generic()).StartAsync();
        rig.H.Stick!.StallDiscovery = true;
        using var cts = new CancellationTokenSource();

        var task = rig.ConnectAsync(token: cts.Token);
        await rig.H.PumpUntilAsync(() => rig.Device.State == BluetoothLEDeviceState.Discovering);
        cts.Cancel();

        (await rig.H.RunAsync(task)).Should().BeNull();
        rig.Device.State.Should().Be(BluetoothLEDeviceState.Disconnected);
        rig.H.Stick.ActiveLinks.Should().BeEmpty();
    }

    [Fact]
    public async Task Connect_AlreadyCancelledToken_ReturnsNullWithoutTouchingTheStick()
    {
        using var rig = await new Rig(GattFixtures.GenericAddress, GattFixtures.Generic()).StartAsync();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        (await rig.H.RunAsync(rig.ConnectAsync(token: cts.Token))).Should().BeNull();
        rig.H.Stick!.ConnectRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task Connect_StickUnpluggedWhileConnecting_ReturnsNull_WithoutDisconnectCallback()
    {
        var p = GattFixtures.Generic();
        p.ConnectDelayMs = 3000;
        using var rig = await new Rig(GattFixtures.GenericAddress, p).StartAsync();

        var task = rig.ConnectAsync();
        await rig.H.PumpAsync(TimeSpan.FromMilliseconds(100));
        rig.H.Locator.Ports.Clear();
        rig.H.Locator.Created.Last().Unplug();

        (await rig.H.RunAsync(task)).Should().BeNull();
        await rig.H.PumpAsync(TimeSpan.FromSeconds(1));
        rig.Disconnects.Should().Be(0, "a failed connect is reported through the null result");
    }

    // ------------------------------------------------------------------ operations

    [Fact]
    public async Task Read_Write_WriteNoResponse_MapToStickOperations()
    {
        using var rig = await new Rig(GattFixtures.GenericAddress, GattFixtures.Generic()).StartAsync();
        var services = await rig.ConnectedServicesAsync();

        (await rig.H.RunAsync(rig.Device.ReadAsync(Char(services, 0x2A26), default))).Should().Equal(0x31, 0x2E, 0x30);
        (await rig.H.RunAsync(rig.Device.WriteAsync(Char(services, 0xFFF1), [1, 2], default))).Should().BeTrue();
        (await rig.H.RunAsync(rig.Device.WriteNoResponseAsync(Char(services, 0xFFF2), [3], default))).Should().BeTrue();

        rig.H.Stick!.Writes.Select(w => (w.Handle, w.WithResponse)).Should().Equal(((ushort)0x21, true), ((ushort)0x23, false));
    }

    [Fact]
    public async Task Operations_ReturnFalseOrNull_OnStickErrors()
    {
        using var rig = await new Rig(GattFixtures.GenericAddress, GattFixtures.Generic()).StartAsync();
        var services = await rig.ConnectedServicesAsync();
        var stick = rig.H.Stick!;

        stick.ProcedureResults.Enqueue((StickStatus.AttError, 3));
        (await rig.H.RunAsync(rig.Device.WriteAsync(Char(services, 0xFFF1), [1], default))).Should().BeFalse();
        stick.ProcedureResults.Enqueue((StickStatus.Busy, 0));
        (await rig.H.RunAsync(rig.Device.ReadAsync(Char(services, 0x2A26), default))).Should().BeNull();
        stick.DropNextResponses = 1;
        (await rig.H.RunAsync(rig.Device.WriteNoResponseAsync(Char(services, 0xFFF2), [1], default))).Should().BeFalse();
        (await rig.H.RunAsync(rig.Device.WriteAsync(Char(services, 0xFFF1), new byte[100], default))).Should().BeFalse("too large for the stick");
        rig.Device.State.Should().Be(BluetoothLEDeviceState.Connected, "a failed operation does not end the connection");
    }

    [Fact]
    public async Task Operations_WhenNotConnected_ReturnFalseOrNull()
    {
        using var rig = await new Rig(GattFixtures.GenericAddress, GattFixtures.Generic()).StartAsync();
        var services = await rig.ConnectedServicesAsync();
        var chr = Char(services, 0xFFF1);
        await rig.H.RunAsync(rig.Device.DisconnectAsync());

        (await rig.H.RunAsync(rig.Device.WriteAsync(chr, [1], default))).Should().BeFalse();
        (await rig.H.RunAsync(rig.Device.WriteNoResponseAsync(chr, [1], default))).Should().BeFalse();
        (await rig.H.RunAsync(rig.Device.ReadAsync(chr, default))).Should().BeNull();
        (await rig.H.RunAsync(rig.Device.EnableNotificationAsync(chr, default))).Should().BeFalse();
    }

    [Fact]
    public async Task ForeignCharacteristicObjects_AreRejected()
    {
        using var rig = await new Rig(GattFixtures.GenericAddress, GattFixtures.Generic()).StartAsync();
        await rig.ConnectedServicesAsync();
        var foreign = new ForeignCharacteristic();

        (await rig.H.RunAsync(rig.Device.WriteAsync(foreign, [1], default))).Should().BeFalse();
        (await rig.H.RunAsync(rig.Device.ReadAsync(foreign, default))).Should().BeNull();
    }

    [Fact]
    public async Task WriteWithCancelledToken_ThrowsLikeTheWindowsLock()
    {
        using var rig = await new Rig(GattFixtures.GenericAddress, GattFixtures.Generic()).StartAsync();
        var services = await rig.ConnectedServicesAsync();
        rig.H.Stick!.DeferProcedures = true;
        using var cts = new CancellationTokenSource();

        var write = rig.Device.WriteAsync(Char(services, 0xFFF1), [1], cts.Token);
        await rig.H.PumpAsync(TimeSpan.FromMilliseconds(50));
        cts.Cancel();

        await FluentActions.Awaiting(() => rig.H.RunAsync(write)).Should().ThrowAsync<OperationCanceledException>();
    }

    // ------------------------------------------------------------------ notifications

    [Fact]
    public async Task Notifications_OnlyForCharacteristicsWithNotifyProperty()
    {
        using var rig = await new Rig(GattFixtures.GenericAddress, GattFixtures.Generic()).StartAsync();
        var services = await rig.ConnectedServicesAsync();

        (await rig.H.RunAsync(rig.Device.EnableNotificationAsync(Char(services, 0xFFF1), default))).Should().BeFalse("no notify property");
        (await rig.H.RunAsync(rig.Device.EnableNotificationAsync(Char(services, 0xFFF3), default))).Should().BeTrue();

        rig.H.Stick!.Subscribes.Should().Equal(((byte)0, (ushort)0x26, (byte)1));
    }

    [Fact]
    public async Task Notifications_AreDeliveredWithUuid_InOrder_UntilDisabled()
    {
        using var rig = await new Rig(GattFixtures.GenericAddress, GattFixtures.Generic()).StartAsync();
        var services = await rig.ConnectedServicesAsync();
        var chr = Char(services, 0xFFF3);
        await rig.H.RunAsync(rig.Device.EnableNotificationAsync(chr, default));

        for (int i = 0; i < 300; i++)
        {
            rig.H.Stick!.Notify(0, 0x25, [(byte)i]);
        }

        (await rig.H.PumpUntilAsync(() => rig.Notifications.Count == 300)).Should().BeTrue();
        rig.Notifications.Select(n => (int)n.Data![0]).Should().Equal(Enumerable.Range(0, 300).Select(i => i & 255));
        rig.Notifications.Should().OnlyContain(n => n.Uuid == StickUuid.FromShort(0xFFF3));

        await rig.H.RunAsync(rig.Device.DisableNotificationAsync(chr, default));
        rig.H.Stick!.Notify(0, 0x25, [1]);
        await rig.H.PumpAsync(TimeSpan.FromMilliseconds(100));
        rig.Notifications.Count.Should().Be(300, "nothing is delivered after unsubscribing");
        rig.H.Stick.Subscribes.Last().Mode.Should().Be(0);
    }

    [Fact]
    public async Task Notifications_ForNotSubscribedCharacteristics_AreIgnored()
    {
        using var rig = await new Rig(GattFixtures.GenericAddress, GattFixtures.Generic()).StartAsync();
        await rig.ConnectedServicesAsync();

        rig.H.Stick!.Notify(0, 0x25, [1]);
        rig.H.Stick.Notify(0, 0x99, [2]);
        await rig.H.PumpAsync(TimeSpan.FromMilliseconds(100));

        rig.Notifications.Should().BeEmpty();
    }

    // ------------------------------------------------------------------ disconnect

    [Fact]
    public async Task Disconnect_IsIdempotent_FreesSlot_AndRaisesNoCallback()
    {
        using var rig = await new Rig(GattFixtures.GenericAddress, GattFixtures.Generic()).StartAsync();
        await rig.ConnectedServicesAsync();

        await rig.H.RunAsync(rig.Device.DisconnectAsync());
        await rig.H.RunAsync(rig.Device.DisconnectAsync());

        rig.Device.State.Should().Be(BluetoothLEDeviceState.Disconnected);
        (await rig.H.PumpUntilAsync(() => rig.H.Connection.LinkCount == 0)).Should().BeTrue();
        rig.H.Stick!.DisconnectRequests.Should().HaveCount(1);
        await rig.H.PumpAsync(TimeSpan.FromMilliseconds(200));
        rig.Disconnects.Should().Be(0);
    }

    [Fact]
    public async Task Disconnect_WithoutEverConnecting_FreesTheSlot()
    {
        using var rig = await new Rig(GattFixtures.GenericAddress, GattFixtures.Generic()).StartAsync();

        await rig.H.RunAsync(rig.Device.DisconnectAsync());

        (await rig.H.PumpUntilAsync(() => rig.H.Connection.LinkCount == 0)).Should().BeTrue();
    }

    [Fact]
    public async Task PeerDisconnect_RaisesCallbackExactlyOnce_AndStateIsDisconnectedInsideIt()
    {
        using var rig = await new Rig(GattFixtures.GenericAddress, GattFixtures.Generic()).StartAsync();
        var services = await rig.ConnectedServicesAsync();
        BluetoothLEDeviceState? stateInCallback = null;
        var device = rig.Device;
        await rig.H.RunAsync(rig.Device.DisconnectAsync());
        await rig.H.PumpUntilAsync(() => rig.H.Connection.LinkCount == 0);
        // reconnect with a callback that records the state
        var again = await rig.H.RunAsync(device.ConnectAndDiscoverServicesAsync(false, (_, _) => { }, d => { stateInCallback = d.State; Interlocked.Increment(ref rig.Disconnects); }, default));
        again.Should().NotBeNull();

        rig.H.Stick!.DropLink(0, 0x08);
        rig.H.Stick.DropLink(0, 0x08);
        await rig.H.PumpUntilAsync(() => rig.Disconnects == 1);
        await rig.H.PumpAsync(TimeSpan.FromMilliseconds(300));

        rig.Disconnects.Should().Be(1);
        stateInCallback.Should().Be(BluetoothLEDeviceState.Disconnected);
        (await rig.H.PumpUntilAsync(() => rig.H.Connection.LinkCount == 0)).Should().BeTrue("the slot is given back");
        _ = services;
    }

    [Fact]
    public async Task StickUnplugged_RaisesCallbackOnce_AndOperationsFail()
    {
        using var rig = await new Rig(GattFixtures.GenericAddress, GattFixtures.Generic()).StartAsync();
        var services = await rig.ConnectedServicesAsync();

        rig.H.Locator.Ports.Clear();
        rig.H.Locator.Created.Last().Unplug();
        (await rig.H.PumpUntilAsync(() => rig.Disconnects == 1)).Should().BeTrue();
        await rig.H.PumpAsync(TimeSpan.FromSeconds(2));

        rig.Disconnects.Should().Be(1);
        (await rig.H.RunAsync(rig.Device.WriteAsync(Char(services, 0xFFF1), [1], default))).Should().BeFalse();
        rig.Device.State.Should().Be(BluetoothLEDeviceState.Disconnected);
    }

    [Fact]
    public async Task ButtonPressed_RaisesCallbackOnce()
    {
        using var rig = await new Rig(GattFixtures.GenericAddress, GattFixtures.Generic()).StartAsync();
        await rig.ConnectedServicesAsync();

        rig.H.Stick!.PressButton();

        (await rig.H.PumpUntilAsync(() => rig.Disconnects == 1)).Should().BeTrue();
        await rig.H.PumpAsync(TimeSpan.FromMilliseconds(500));
        rig.Disconnects.Should().Be(1);
    }

    [Fact]
    public async Task PendingWrite_FailsWhenThePeerDisconnects()
    {
        using var rig = await new Rig(GattFixtures.GenericAddress, GattFixtures.Generic()).StartAsync();
        var services = await rig.ConnectedServicesAsync();
        rig.H.Stick!.DeferProcedures = true;

        var write = rig.Device.WriteAsync(Char(services, 0xFFF1), [1], default);
        await rig.H.PumpAsync(TimeSpan.FromMilliseconds(100));
        rig.H.Stick.DropLink(0);

        (await rig.H.RunAsync(write)).Should().BeFalse();
    }

    [Fact]
    public async Task WriteLoop_10msFor5Seconds_ThenPeerDisappears_DeviceSeesFailureAndCallback()
    {
        using var rig = await new Rig(GattFixtures.GenericAddress, GattFixtures.Generic()).StartAsync();
        var services = await rig.ConnectedServicesAsync();
        var chr = Char(services, 0xFFF2);
        int ok = 0, failed = 0;
        using var cts = new CancellationTokenSource();

        var loop = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                if (await rig.Device.WriteNoResponseAsync(chr, [1, 2, 3], cts.Token)) { Interlocked.Increment(ref ok); } else { Interlocked.Increment(ref failed); }
                try { await Task.Delay(TimeSpan.FromMilliseconds(10), rig.H.Clock, cts.Token); } catch (OperationCanceledException) { }
            }
        });

        await rig.H.PumpAsync(TimeSpan.FromSeconds(5));
        rig.H.Stick!.DropLink(0, 0x13);
        await rig.H.PumpUntilAsync(() => rig.Disconnects == 1 && Volatile.Read(ref failed) > 0);
        cts.Cancel();
        await loop;

        ok.Should().BeGreaterThan(300, "about 100 writes per second for 5 s");
        failed.Should().BeGreaterThan(0);
        rig.Disconnects.Should().Be(1);
        rig.H.Stick.Writes.Count.Should().Be(ok, "every successful write reached the stick exactly once");
    }

    // ------------------------------------------------------------------ device-shaped scenarios

    [Fact]
    public async Task Duplo_BackgroundConnect_SubscribeWithImmediateNotifications_WritesAndRead()
    {
        using var rig = await new Rig(GattFixtures.DuploAddress, GattFixtures.Duplo()).StartAsync();
        var lwp = new Guid("00001624-1212-efde-1623-785feabcd123");
        // attached-IO style notifications start right after the CCCD write
        rig.H.Stick!.NotifyAfterSubscribe.Add((0x12, [0x0F, 0x00, 0x04, 0x01, 0x01]));
        rig.H.Stick.NotifyAfterSubscribe.Add((0x12, [0x0F, 0x00, 0x04, 0x02, 0x01]));

        var services = await rig.ConnectedServicesAsync(autoConnect: true);
        var chr = Char(services, lwp);

        rig.H.Stick.ConnectRequests.Single().Flags.Should().Be(StickProtocol.ConnectFlagBackground, "ControlPlus devices use a background connect");
        (await rig.H.RunAsync(rig.Device.EnableNotificationAsync(chr, default))).Should().BeTrue();
        (await rig.H.PumpUntilAsync(() => rig.Notifications.Count == 2)).Should().BeTrue("notifications next to the SUBSCRIBE ACK must not be lost");
        rig.Notifications.Select(n => n.Data![3]).Should().Equal(1, 2);

        // hub property read + 8 byte writes with response (motor command)
        (await rig.H.RunAsync(rig.Device.ReadAsync(chr, default))).Should().Equal(0x0F, 0x00, 0x01);
        for (int i = 0; i < 20; i++)
        {
            (await rig.H.RunAsync(rig.Device.WriteAsync(chr, [0x08, 0x00, 0x81, 0x00, 0x11, 0x51, 0x00, (byte)i], default))).Should().BeTrue();
        }

        rig.H.Stick.Writes.Should().HaveCount(20).And.OnlyContain(w => w.WithResponse && w.Data.Length == 8);
    }

    [Fact]
    public async Task MkDiy_100WritesPerSecondWithoutResponse_AllReachTheStick()
    {
        using var rig = await new Rig(GattFixtures.MkDiyAddress, GattFixtures.MkDiy()).StartAsync();
        var services = await rig.ConnectedServicesAsync(); // normal connect with timeout, no background flag
        var chr = Char(services, 0xFFE1);
        rig.H.Stick!.ConnectRequests.Single().Flags.Should().Be(0);

        int ok = 0;
        for (int i = 0; i < 300; i++)
        {
            var write = rig.Device.WriteNoResponseAsync(chr, new byte[18], default);
            if (await rig.H.RunAsync(write)) { ok++; }
            await rig.H.PumpAsync(TimeSpan.FromMilliseconds(10));
        }

        ok.Should().Be(300);
        rig.H.Stick.Writes.Should().HaveCount(300).And.OnlyContain(w => !w.WithResponse && w.Data.Length == 18);
        rig.H.Stick.Requests(StickProtocol.TypeWriteCmd).Should().HaveCount(300, "no retransmission");
        rig.H.Connection.Status.State.Should().Be(UsbStickState.Connected);
    }

    [Fact]
    public async Task Wedo2_Mtu23_FirmwareRead_Writes_VoltageNotifications_UnsubscribeBeforeDisconnect()
    {
        using var rig = await new Rig(GattFixtures.Wedo2Address, GattFixtures.Wedo2()).StartAsync();
        var services = await rig.ConnectedServicesAsync();
        var sensor = Char(services, new Guid("00001560-1212-efde-1523-785feabcd123"));
        var input = Char(services, new Guid("00001563-1212-efde-1523-785feabcd123"));
        var motor = Char(services, new Guid("00001565-1212-efde-1523-785feabcd123"));

        // firmware revision (2A26 is a 16 bit UUID, expanded to the Bluetooth base UUID)
        var firmware = await rig.H.RunAsync(rig.Device.ReadAsync(Char(services, 0x2A26), default));
        System.Text.Encoding.ASCII.GetString(firmware!).Should().Be("1.1.0");

        // voltage command (input characteristic), then enable the sensor notifications
        (await rig.H.RunAsync(rig.Device.WriteAsync(input, [0x01, 0x02, 0x06, 0x17, 0x01, 0x00, 0x00, 0x00, 0x00, 0x02, 0x01], default))).Should().BeTrue("11 bytes fit into mtu 23");
        (await rig.H.RunAsync(rig.Device.EnableNotificationAsync(sensor, default))).Should().BeTrue();

        // two 4 byte writes per cycle (one per motor port)
        for (int cycle = 0; cycle < 10; cycle++)
        {
            (await rig.H.RunAsync(rig.Device.WriteAsync(motor, [0x01, 0x01, 0x01, (byte)cycle], default))).Should().BeTrue();
            (await rig.H.RunAsync(rig.Device.WriteAsync(motor, [0x02, 0x01, 0x01, (byte)cycle], default))).Should().BeTrue();
        }

        rig.H.Stick!.Notify(0, 0x42, [0x04, 0x00, 0x11, 0x22]);
        (await rig.H.PumpUntilAsync(() => rig.Notifications.Count == 1)).Should().BeTrue();
        rig.Notifications.Single().Uuid.Should().Be(new Guid("00001560-1212-efde-1523-785feabcd123"));

        // unsubscribe before disconnect, as Wedo2Device does
        (await rig.H.RunAsync(rig.Device.DisableNotificationAsync(sensor, default))).Should().BeTrue();
        await rig.H.RunAsync(rig.Device.DisconnectAsync());
        var types = rig.H.Stick.RequestTypes.Where(t => t is StickProtocol.TypeSubscribe or StickProtocol.TypeDisconnect).ToList();
        types.Should().Equal(StickProtocol.TypeSubscribe, StickProtocol.TypeSubscribe, StickProtocol.TypeDisconnect);
        rig.H.Stick.Writes.Should().HaveCount(21);

        // a write larger than mtu-3 is refused locally
        rig.Disconnects.Should().Be(0);
    }

    [Fact]
    public async Task Wedo2_WriteOfMoreThan20Bytes_IsRejected()
    {
        using var rig = await new Rig(GattFixtures.Wedo2Address, GattFixtures.Wedo2()).StartAsync();
        var services = await rig.ConnectedServicesAsync();
        var motor = Char(services, new Guid("00001565-1212-efde-1523-785feabcd123"));

        (await rig.H.RunAsync(rig.Device.WriteAsync(motor, new byte[21], default))).Should().BeFalse();
        rig.H.Stick!.Writes.Should().BeEmpty();
    }

    [Fact]
    public async Task ThreeDevicesAtOnce_UseDifferentConnIds()
    {
        using var h = new StickHarness(maxConns: 3);
        h.Stick!.AddPeripheral(GattFixtures.Duplo());
        h.Stick.AddPeripheral(GattFixtures.MkDiy());
        h.Stick.AddPeripheral(GattFixtures.Wedo2());
        await h.ConnectStickAsync();
        var book = new StickAddressBook();
        var devices = new[] { GattFixtures.DuploAddress, GattFixtures.MkDiyAddress, GattFixtures.Wedo2Address }
            .Select(a => new StickGattDevice(a, h.Connection.TryAcquireLink()!, () => h.Connection.TryAcquireLink(), book, NullLogger.Instance))
            .ToArray();

        foreach (var device in devices)
        {
            (await h.RunAsync(device.ConnectAndDiscoverServicesAsync(false, (_, _) => { }, _ => { }, default))).Should().NotBeNull();
        }

        h.Stick.ActiveLinks.Should().BeEquivalentTo(new byte[] { 0, 1, 2 });
        h.Connection.HasFreeLink.Should().BeFalse();
    }

    private sealed class ForeignCharacteristic : IGattCharacteristic
    {
        public Guid Uuid => Guid.NewGuid();
    }
}
