// Tests of one GATT link against the simulated stick: connect, discover, read, write, subscribe, failures.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using FluentAssertions;
using Xunit;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

public class StickGattLinkTests
{
    private static readonly byte[] GenericAddressBytes = Bytes(GattFixtures.GenericAddress);

    private static byte[] Bytes(string address)
    {
        StickAddress.TryParse(address, out var bytes);
        return bytes;
    }

    /// <summary>Harness with a stick (4 links), a generic peripheral and a link that is connected and discovered.</summary>
    private static async Task<(StickHarness H, StickGattLink Link, FakePeripheral P)> ConnectedAsync(int maxConns = 4)
    {
        var h = new StickHarness(maxConns: maxConns);
        var p = h.Stick!.AddPeripheral(GattFixtures.Generic());
        var link = await h.ConnectAndAcquireLinkAsync();
        (await h.RunAsync(link.ConnectAsync(GenericAddressBytes, 1, false, CancellationToken.None))).IsOk.Should().BeTrue();
        return (h, link, p);
    }

    [Fact]
    public async Task Connect_SendsConnectAndWaitsForConnectedEvent()
    {
        using var h = new StickHarness(maxConns: 4);
        h.Stick!.AddPeripheral(GattFixtures.Generic()).ConnectDelayMs = 300;
        var link = await h.ConnectAndAcquireLinkAsync();

        var task = link.ConnectAsync(GenericAddressBytes, 1, false, CancellationToken.None);
        await h.PumpAsync(TimeSpan.FromMilliseconds(100));
        task.IsCompleted.Should().BeFalse("EVT_CONNECTED has not arrived yet");

        (await h.RunAsync(task)).IsOk.Should().BeTrue();
        link.IsConnected.Should().BeTrue();
        link.Mtu.Should().Be(64);
        h.Stick.ConnectRequests.Should().ContainSingle().Which.Should().Be((link.ConnId, GattFixtures.GenericAddress, (byte)1, (byte)0));
    }

    [Fact]
    public async Task Connect_BackgroundFlagIsSent()
    {
        using var h = new StickHarness(maxConns: 4);
        h.Stick!.AddPeripheral(GattFixtures.Generic());
        var link = await h.ConnectAndAcquireLinkAsync();

        await h.RunAsync(link.ConnectAsync(GenericAddressBytes, StickProtocol.AddressTypeAuto, true, CancellationToken.None));

        h.Stick.ConnectRequests.Single().Flags.Should().Be(StickProtocol.ConnectFlagBackground);
        h.Stick.ConnectRequests.Single().AddressType.Should().Be(0xFF);
    }

    [Fact]
    public async Task Connect_Refused_ReportsFailure()
    {
        using var h = new StickHarness(maxConns: 4);
        h.Stick!.AddPeripheral(GattFixtures.Generic()).RefuseConnection = true;
        var link = await h.ConnectAndAcquireLinkAsync();

        var result = await h.RunAsync(link.ConnectAsync(GenericAddressBytes, 1, false, CancellationToken.None));

        result.IsOk.Should().BeFalse();
        link.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task Connect_StickTimeout_ReportsFailure()
    {
        using var h = new StickHarness(maxConns: 4);
        h.Stick!.AddPeripheral(GattFixtures.Generic()).NeverConnects = true;
        var link = await h.ConnectAndAcquireLinkAsync();

        var result = await h.RunAsync(link.ConnectAsync(GenericAddressBytes, 1, false, CancellationToken.None));

        result.IsOk.Should().BeFalse();
    }

    [Fact]
    public async Task Connect_CancelledWhileConnecting_SendsDisconnect()
    {
        using var h = new StickHarness(maxConns: 4);
        h.Stick!.AddPeripheral(GattFixtures.Generic()).ConnectDelayMs = 5000;
        var link = await h.ConnectAndAcquireLinkAsync();
        using var cts = new CancellationTokenSource();

        var task = link.ConnectAsync(GenericAddressBytes, 1, false, cts.Token);
        await h.PumpAsync(TimeSpan.FromMilliseconds(100));
        cts.Cancel();

        await FluentActions.Awaiting(() => h.RunAsync(task)).Should().ThrowAsync<OperationCanceledException>();
        h.Stick.DisconnectRequests.Should().Equal(link.ConnId);
        link.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task Connect_AddressAlreadyConnected_StickAnswersAlreadyRunning()
    {
        var (h, link, _) = await ConnectedAsync(maxConns: 2);
        using var _h = h;
        var second = h.Connection.TryAcquireLink()!;

        var result = await h.RunAsync(second.ConnectAsync(GenericAddressBytes, 1, false, CancellationToken.None));

        result.Status.Should().Be(StickStatus.AlreadyRunning);
        link.IsConnected.Should().BeTrue("the first link is untouched");
    }

    [Fact]
    public async Task Discover_BuildsServiceTreeByHandleRange()
    {
        var (h, link, _) = await ConnectedAsync();
        using var _h = h;

        var (result, services) = await h.RunAsync(link.DiscoverAsync(CancellationToken.None));

        result.IsOk.Should().BeTrue();
        services.Should().HaveCount(2);
        services[0].Uuid.Should().Be(StickUuid.FromShort(0x180A));
        services[0].Characteristics.Should().ContainSingle().Which.ValueHandle.Should().Be(0x12);
        services[1].Characteristics.Select(c => c.ValueHandle).Should().Equal(0x21, 0x23, 0x25);
        services[1].Characteristics[2].CccdHandle.Should().Be(0x26);
        services[1].Characteristics[2].CanNotify.Should().BeTrue();
        services[1].Characteristics[0].CanNotify.Should().BeFalse();
    }

    [Fact]
    public async Task Discover_128BitUuids_AreConvertedToGuids()
    {
        using var h = new StickHarness(maxConns: 4);
        h.Stick!.AddPeripheral(GattFixtures.Duplo());
        var link = await h.ConnectAndAcquireLinkAsync();
        await h.RunAsync(link.ConnectAsync(Bytes(GattFixtures.DuploAddress), 1, false, CancellationToken.None));

        var (result, services) = await h.RunAsync(link.DiscoverAsync(CancellationToken.None));

        result.IsOk.Should().BeTrue();
        services.Single().Uuid.Should().Be(new Guid("00001623-1212-efde-1623-785feabcd123"));
        services.Single().Characteristics.Single().Uuid.Should().Be(new Guid("00001624-1212-efde-1623-785feabcd123"));
    }

    [Fact]
    public async Task Discover_FailureStatus_ReturnsError()
    {
        var (h, link, _) = await ConnectedAsync();
        using var _h = h;
        h.Stick!.DiscoverStatus = StickStatus.BleError;

        var (result, services) = await h.RunAsync(link.DiscoverAsync(CancellationToken.None));

        result.Status.Should().Be(StickStatus.BleError);
        services.Should().BeEmpty();
    }

    [Fact]
    public async Task Discover_Stalled_TimesOutAfter15Seconds()
    {
        var (h, link, _) = await ConnectedAsync();
        using var _h = h;
        h.Stick!.StallDiscovery = true;

        var task = link.DiscoverAsync(CancellationToken.None);
        await h.PumpAsync(TimeSpan.FromSeconds(14));
        task.IsCompleted.Should().BeFalse();

        var (result, _) = await h.RunAsync(task);
        result.Status.Should().Be(StickStatus.Timeout);
    }

    [Fact]
    public async Task Read_ReturnsValue()
    {
        var (h, link, _) = await ConnectedAsync();
        using var _h = h;

        var (result, data) = await h.RunAsync(link.ReadAsync(0x12, CancellationToken.None));

        result.IsOk.Should().BeTrue();
        data.Should().Equal(0x31, 0x2E, 0x30);
    }

    [Fact]
    public async Task Read_AttError_ReturnsErrorCode()
    {
        var (h, link, _) = await ConnectedAsync();
        using var _h = h;

        var (result, data) = await h.RunAsync(link.ReadAsync(0x99, CancellationToken.None));

        result.Status.Should().Be(StickStatus.AttError);
        result.AttError.Should().Be(0x0A);
        data.Should().BeNull();
    }

    [Fact]
    public async Task Write_WithResponse_WaitsForAck()
    {
        var (h, link, _) = await ConnectedAsync();
        using var _h = h;
        h.Stick!.DeferProcedures = true;

        var task = link.WriteAsync(0x21, [1, 2, 3], CancellationToken.None);
        await h.PumpAsync(TimeSpan.FromSeconds(2)); // longer than any immediate-ACK timeout
        task.IsCompleted.Should().BeFalse("a write with response completes with the peer's confirmation");

        h.Stick.ResolvePending();
        (await h.RunAsync(task)).IsOk.Should().BeTrue();
        h.Stick.Writes.Single().Should().BeEquivalentTo(new FakeWrite(link.ConnId, 0x21, [1, 2, 3], true));
    }

    [Fact]
    public async Task Write_NoResponse_IsAckedImmediately()
    {
        var (h, link, _) = await ConnectedAsync();
        using var _h = h;

        (await h.RunAsync(link.WriteCommandAsync(0x23, [9, 8], CancellationToken.None))).IsOk.Should().BeTrue();

        h.Stick!.Writes.Single().WithResponse.Should().BeFalse();
    }

    [Fact]
    public async Task Write_AttError_IsMappedToAttErrorStatus()
    {
        var (h, link, _) = await ConnectedAsync();
        using var _h = h;
        h.Stick!.ProcedureResults.Enqueue((StickStatus.AttError, 0x03));

        var result = await h.RunAsync(link.WriteAsync(0x21, [1], CancellationToken.None));

        result.Should().Be(new GattResult(StickStatus.AttError, 0x03));
    }

    [Fact]
    public async Task WriteCommand_Busy_IsRetriedAndThenSucceeds()
    {
        var (h, link, _) = await ConnectedAsync();
        using var _h = h;
        h.Stick!.BusyWriteCommands = 2;

        (await h.RunAsync(link.WriteCommandAsync(0x23, [1], CancellationToken.None))).IsOk.Should().BeTrue();

        h.Stick.Requests(StickProtocol.TypeWriteCmd).Should().HaveCount(3, "two BUSY answers (not queued) and the accepted one");
        h.Stick.Writes.Should().ContainSingle("only the accepted write was applied");
    }

    [Fact]
    public async Task WriteCommand_PermanentlyBusy_GivesUp()
    {
        var (h, link, _) = await ConnectedAsync();
        using var _h = h;
        h.Stick!.BusyWriteCommands = 100;

        (await h.RunAsync(link.WriteCommandAsync(0x23, [1], CancellationToken.None))).Status.Should().Be(StickStatus.Busy);

        h.Stick.Requests(StickProtocol.TypeWriteCmd).Count.Should().BeLessThan(10);
    }

    [Fact]
    public async Task Write_Busy_IsNotRetried_BecauseOnlyWriteCommandMayBe()
    {
        var (h, link, _) = await ConnectedAsync();
        using var _h = h;
        h.Stick!.ProcedureResults.Enqueue((StickStatus.Busy, 0));

        (await h.RunAsync(link.WriteAsync(0x21, [1], CancellationToken.None))).Status.Should().Be(StickStatus.Busy);

        h.Stick.Requests(StickProtocol.TypeWrite).Should().HaveCount(1);
    }

    [Fact]
    public async Task Write_LostResponse_FailsWithoutRetransmission()
    {
        var (h, link, _) = await ConnectedAsync();
        using var _h = h;
        h.Stick!.DropNextResponses = 1;

        var result = await h.RunAsync(link.WriteAsync(0x21, [1], CancellationToken.None));

        result.Status.Should().Be(StickStatus.Timeout);
        h.Stick.Requests(StickProtocol.TypeWrite).Should().HaveCount(1, "a duplicate write could be harmful");
    }

    [Fact]
    public async Task Write_StickAnswersLate_IsTimeoutAfterSixSeconds()
    {
        var (h, link, _) = await ConnectedAsync();
        using var _h = h;
        h.Stick!.DeferProcedures = true;

        var task = link.WriteAsync(0x21, [1], CancellationToken.None);
        await h.PumpAsync(TimeSpan.FromSeconds(5));
        task.IsCompleted.Should().BeFalse();

        (await h.RunAsync(task)).Status.Should().Be(StickStatus.Timeout);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(38, true)]  // BuWizz 3 sized write
    [InlineData(56, true)]  // max_value_len of the stick (mtu 64 would allow 61)
    [InlineData(57, false)]
    [InlineData(61, false)]
    public async Task Write_PayloadLimit_IsMinOfMaxValueLenAndMtu(int length, bool accepted)
    {
        var (h, link, _) = await ConnectedAsync();
        using var _h = h;

        var result = await h.RunAsync(link.WriteAsync(0x21, new byte[length], CancellationToken.None));

        result.IsOk.Should().Be(accepted);
        if (!accepted)
        {
            result.Status.Should().Be(StickStatus.PayloadTooLarge);
            h.Stick!.Requests(StickProtocol.TypeWrite).Should().BeEmpty("oversized data never reaches the wire");
        }
    }

    [Fact]
    public async Task Write_LimitFollowsMtu_ForWedo2WithMtu23()
    {
        using var h = new StickHarness(maxConns: 4);
        h.Stick!.AddPeripheral(GattFixtures.Wedo2());
        var link = await h.ConnectAndAcquireLinkAsync();
        await h.RunAsync(link.ConnectAsync(Bytes(GattFixtures.Wedo2Address), 1, false, CancellationToken.None));

        link.MaxPayload.Should().Be(20);
        (await h.RunAsync(link.WriteAsync(0x45, new byte[20], CancellationToken.None))).IsOk.Should().BeTrue();
        (await h.RunAsync(link.WriteAsync(0x45, new byte[21], CancellationToken.None))).Status.Should().Be(StickStatus.PayloadTooLarge);
        h.Stick.Writes.Should().ContainSingle();
    }

    [Fact]
    public async Task Write_EmptyData_IsRejectedLocally()
    {
        var (h, link, _) = await ConnectedAsync();
        using var _h = h;

        (await h.RunAsync(link.WriteAsync(0x21, [], CancellationToken.None))).Status.Should().Be(StickStatus.PayloadTooLarge);
        h.Stick!.Requests(StickProtocol.TypeWrite).Should().BeEmpty();
    }

    [Fact]
    public async Task Subscribe_SendsCccdHandleAndMode()
    {
        var (h, link, _) = await ConnectedAsync();
        using var _h = h;

        (await h.RunAsync(link.SubscribeAsync(0x26, 1, CancellationToken.None))).IsOk.Should().BeTrue();
        (await h.RunAsync(link.SubscribeAsync(0x26, 0, CancellationToken.None))).IsOk.Should().BeTrue();

        h.Stick!.Subscribes.Should().Equal((link.ConnId, (ushort)0x26, (byte)1), (link.ConnId, (ushort)0x26, (byte)0));
    }

    [Fact]
    public async Task Subscribe_AttError_IsReported()
    {
        var (h, link, _) = await ConnectedAsync();
        using var _h = h;
        h.Stick!.ProcedureResults.Enqueue((StickStatus.AttError, 0x05));

        (await h.RunAsync(link.SubscribeAsync(0x26, 1, CancellationToken.None))).Status.Should().Be(StickStatus.AttError);
    }

    [Fact]
    public async Task SetFailsafe_SendsSlotHandleAndData()
    {
        var (h, link, _) = await ConnectedAsync();
        using var _h = h;

        (await h.RunAsync(link.SetFailsafeAsync(1, 0x21, [0, 0], CancellationToken.None))).IsOk.Should().BeTrue();
        (await h.RunAsync(link.SetFailsafeAsync(7, 0x21, [0], CancellationToken.None))).Status.Should().Be(StickStatus.BadParam);

        var request = h.Stick!.Requests(StickProtocol.TypeSetFailsafe).First();
        request.Payload.Should().Equal(link.ConnId, 1, 0x21, 0, 0, 0);
    }

    [Fact]
    public async Task Operations_BeforeConnect_FailWithoutHittingTheWire()
    {
        using var h = new StickHarness(maxConns: 4);
        var link = await h.ConnectAndAcquireLinkAsync();

        (await h.RunAsync(link.WriteAsync(0x21, [1], CancellationToken.None))).Status.Should().Be(StickStatus.NotConnected);
        (await h.RunAsync(link.ReadAsync(0x21, CancellationToken.None))).Result.Status.Should().Be(StickStatus.NotConnected);
        (await h.RunAsync(link.SubscribeAsync(0x21, 1, CancellationToken.None))).Status.Should().Be(StickStatus.NotConnected);
        h.Stick!.Requests(StickProtocol.TypeWrite).Should().BeEmpty();
    }

    [Fact]
    public async Task PeerDisconnect_RaisesLinkLostOnce_AndFailsPendingWrite()
    {
        var (h, link, _) = await ConnectedAsync();
        using var _h = h;
        int lost = 0;
        link.LinkLost += _ => Interlocked.Increment(ref lost);
        h.Stick!.DeferProcedures = true;

        var write = link.WriteAsync(0x21, [1], CancellationToken.None);
        await h.PumpAsync(TimeSpan.FromMilliseconds(50));
        h.Stick.DropLink(link.ConnId, 0x08);

        (await h.RunAsync(write)).Status.Should().Be(StickStatus.Disconnected);
        lost.Should().Be(1);
        link.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task Disconnect_IsIdempotent_AndDoesNotRaiseLinkLost()
    {
        var (h, link, _) = await ConnectedAsync();
        using var _h = h;
        int lost = 0;
        link.LinkLost += _ => Interlocked.Increment(ref lost);

        await h.RunAsync(link.DisconnectAsync());
        await h.RunAsync(link.DisconnectAsync());

        h.Stick!.DisconnectRequests.Should().HaveCount(1);
        lost.Should().Be(0);
        h.Stick.ActiveLinks.Should().BeEmpty();
    }

    [Fact]
    public async Task Disconnect_WaitsForEventButGivesUpAfterTwoSeconds()
    {
        var (h, link, _) = await ConnectedAsync();
        using var _h = h;
        h.Stick!.SuppressDisconnectEvent = true;

        await h.RunAsync(link.DisconnectAsync()); // must not hang

        h.Stick.DisconnectRequests.Should().HaveCount(1);
    }

    [Fact]
    public async Task Notifications_AreDeliveredInOrder_WithoutBlockingTheReadLoop()
    {
        var (h, link, _) = await ConnectedAsync();
        using var _h = h;
        var received = new System.Collections.Concurrent.ConcurrentQueue<int>();
        link.NotificationHandler = (handle, data) =>
        {
            handle.Should().Be(0x25);
            Thread.Sleep(0); // a slow consumer must not matter
            received.Enqueue(data[0] | (data[1] << 8));
        };

        // Re-register (handler is set before the pump starts reading in real use; here the pump already runs).
        for (int i = 0; i < 1000; i++)
        {
            h.Stick!.Notify(link.ConnId, 0x25, [(byte)i, (byte)(i >> 8)]);
        }

        (await h.PumpUntilAsync(() => received.Count == 1000)).Should().BeTrue();
        received.Should().Equal(Enumerable.Range(0, 1000));
    }

    [Fact]
    public async Task FaultyNotificationHandler_DoesNotStopDelivery()
    {
        var (h, link, _) = await ConnectedAsync();
        using var _h = h;
        int count = 0;
        link.NotificationHandler = (_, data) =>
        {
            Interlocked.Increment(ref count);
            if (data[0] == 1)
            {
                throw new InvalidOperationException("boom");
            }
        };

        h.Stick!.Notify(link.ConnId, 0x25, [1]);
        h.Stick.Notify(link.ConnId, 0x25, [2]);

        (await h.PumpUntilAsync(() => Volatile.Read(ref count) == 2)).Should().BeTrue();
    }
}
