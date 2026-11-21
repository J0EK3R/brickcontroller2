// GATT related behaviour of the shared stick connection: conn_id allocation from max_conns, idle close with
// links open, stick loss (links end, sets and scan resume), button, PING under notification load.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using FluentAssertions;
using Xunit;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

public class UsbStickConnectionGattTests
{
    private static readonly byte[] Payload = [1, 2, 3, 4, 5, 6, 7, 8];

    private static byte[] Bytes(string address)
    {
        StickAddress.TryParse(address, out var bytes);
        return bytes;
    }

    // ------------------------------------------------------------------ conn_id allocation

    /// <summary>The number of links comes only from HELLO_RSP: several values, none of them special.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(9)]
    [InlineData(16)]
    public async Task AcquireLink_HandsOutExactlyMaxConnsIds(int maxConns)
    {
        using var h = new StickHarness(maxConns: maxConns);
        await h.ConnectStickAsync();

        var links = Enumerable.Range(0, maxConns).Select(_ => h.Connection.TryAcquireLink()).ToList();

        links.Should().OnlyContain(l => l != null);
        links.Select(l => (int)l!.ConnId).Should().Equal(Enumerable.Range(0, maxConns));
        h.Connection.TryAcquireLink().Should().BeNull("all slots are in use");
        h.Connection.HasFreeLink.Should().BeFalse();
        h.Connection.MaxLinks.Should().Be(maxConns);
        h.Connection.LinkCount.Should().Be(maxConns);
    }

    [Fact]
    public async Task ReleasedConnId_IsReusedLowestFirst()
    {
        using var h = new StickHarness(maxConns: 4);
        await h.ConnectStickAsync();
        var links = Enumerable.Range(0, 4).Select(_ => h.Connection.TryAcquireLink()!).ToList();

        links[1].Dispose();
        await h.PumpUntilAsync(() => h.Connection.LinkCount == 3);
        links[2].Dispose();
        await h.PumpUntilAsync(() => h.Connection.LinkCount == 2);

        h.Connection.TryAcquireLink()!.ConnId.Should().Be(1);
        h.Connection.TryAcquireLink()!.ConnId.Should().Be(2);
    }

    [Fact]
    public async Task Stick10WithoutGatt_HasNoLinks()
    {
        using var h = new StickHarness(maxConns: 0);
        await h.ConnectStickAsync();

        h.Connection.TryAcquireLink().Should().BeNull();
        h.Connection.MaxLinks.Should().Be(0);
        h.Connection.Info!.SupportsGatt.Should().BeFalse();
    }

    [Fact]
    public async Task Stick10Firmware_With27ByteHello_HasNoLinks()
    {
        using var h = new StickHarness(maxConns: 4);
        h.Stick!.GattEnabled = false;
        await h.ConnectStickAsync();

        h.Connection.TryAcquireLink().Should().BeNull();
        (await h.RunAsync(h.Connection.TryConnectAsync())).Should().BeTrue("advertising still works");
    }

    [Fact]
    public async Task MaxConns_IsReadAgainAfterReconnect()
    {
        using var h = new StickHarness(maxConns: 4);
        var lease = await h.ConnectAndLeaseAsync();
        await lease.StartAsync(32, 127, 0xFFF0, Payload);
        h.Connection.MaxLinks.Should().Be(4);

        h.Locator.Ports.Clear();
        h.Locator.Created.Last().Unplug();
        await h.PumpUntilAsync(() => h.Connection.Status.State == UsbStickState.Lost);
        h.Plug("COM9", maxConns: 7);
        (await h.PumpUntilAsync(() => h.Connection.Status.State == UsbStickState.Connected)).Should().BeTrue();

        h.Connection.MaxLinks.Should().Be(7, "another firmware build may measure another maximum");
    }

    // ------------------------------------------------------------------ idle close

    [Fact]
    public async Task OpenLink_KeepsPortOpenPastIdleDelay()
    {
        using var h = new StickHarness(maxConns: 4);
        var link = await h.ConnectAndAcquireLinkAsync();

        await h.PumpAsync(TimeSpan.FromSeconds(20));
        h.Connection.Status.State.Should().Be(UsbStickState.Connected);

        link.Dispose();
        (await h.PumpUntilAsync(() => h.Connection.Status.State == UsbStickState.Idle, TimeSpan.FromSeconds(10))).Should().BeTrue();
    }

    [Fact]
    public async Task ReleasedLinkWithSets_StaysConnected()
    {
        using var h = new StickHarness(maxConns: 4);
        var lease = await h.ConnectAndLeaseAsync();
        var link = h.Connection.TryAcquireLink()!;
        link.Dispose();

        await h.PumpAsync(TimeSpan.FromSeconds(10));

        h.Connection.Status.State.Should().Be(UsbStickState.Connected);
        lease.Dispose();
    }

    [Fact]
    public async Task Scanner_KeepsPortOpen_AndStopClosesIt()
    {
        using var h = new StickHarness(maxConns: 4);
        await h.ConnectStickAsync();
        var scanner = new StickScanner(h.Connection, _ => { }, new StickAddressBook(), Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        (await h.RunAsync(scanner.StartAsync())).Should().BeTrue();
        await h.PumpAsync(TimeSpan.FromSeconds(15));
        h.Connection.Status.State.Should().Be(UsbStickState.Connected);
        h.Stick!.Scanning.Should().BeTrue();

        await h.RunAsync(scanner.StopAsync());
        h.Stick.Scanning.Should().BeFalse();
        (await h.PumpUntilAsync(() => h.Connection.Status.State == UsbStickState.Idle, TimeSpan.FromSeconds(10))).Should().BeTrue();
    }

    // ------------------------------------------------------------------ stick loss

    [Fact]
    public async Task StickUnplugged_EveryConnectedLinkReportsLossExactlyOnce()
    {
        using var h = new StickHarness(maxConns: 4);
        h.Stick!.AddPeripheral(GattFixtures.Generic());
        h.Stick.AddPeripheral(GattFixtures.Duplo());
        await h.ConnectStickAsync();
        var lost = new List<int>();
        var links = new List<StickGattLink>();
        foreach (string addr in new[] { GattFixtures.GenericAddress, GattFixtures.DuploAddress })
        {
            var link = h.Connection.TryAcquireLink()!;
            link.LinkLost += _ => { lock (lost) { lost.Add(link.ConnId); } };
            (await h.RunAsync(link.ConnectAsync(Bytes(addr), 1, false, CancellationToken.None))).IsOk.Should().BeTrue();
            links.Add(link);
        }

        h.Locator.Ports.Clear();
        h.Locator.Created.Last().Unplug();
        await h.PumpUntilAsync(() => h.Connection.Status.State != UsbStickState.Connected);
        await h.PumpAsync(TimeSpan.FromSeconds(3));

        lost.OrderBy(x => x).Should().Equal(0, 1);
        h.Connection.LinkCount.Should().Be(0, "stale connections are forgotten");
        h.Connection.Status.State.Should().Be(UsbStickState.NotFound, "links are not resumed, so nothing keeps the reconnect loop alive");
    }

    [Fact]
    public async Task StickReplugged_LinksAreNotResumed_ButSetsAre()
    {
        using var h = new StickHarness(maxConns: 4);
        h.Stick!.AddPeripheral(GattFixtures.Generic());
        var lease = await h.ConnectAndLeaseAsync();
        await lease.StartAsync(32, 127, 0xFFF0, Payload);
        var link = h.Connection.TryAcquireLink()!;
        await h.RunAsync(link.ConnectAsync(Bytes(GattFixtures.GenericAddress), 1, false, CancellationToken.None));

        h.Locator.Ports.Clear();
        h.Locator.Created.Last().Unplug();
        await h.PumpUntilAsync(() => h.Connection.Status.State == UsbStickState.Lost);
        var newStick = h.Plug("COM12", maxConns: 4);
        newStick.AddPeripheral(GattFixtures.Generic());
        (await h.PumpUntilAsync(() => newStick.Sets.ContainsKey(0))).Should().BeTrue("advertising sets resume");
        await h.PumpAsync(TimeSpan.FromSeconds(2));

        newStick.Requests(StickProtocol.TypeConnect).Should().BeEmpty("a GATT link is never reconnected behind the device's back");
        link.IsConnected.Should().BeFalse();
        h.Connection.TryAcquireLink()!.ConnId.Should().Be(0, "the slot is free again");
    }

    [Fact]
    public async Task OldLinkAfterReconnect_NeverSendsAnything()
    {
        using var h = new StickHarness(maxConns: 4);
        h.Stick!.AddPeripheral(GattFixtures.Generic());
        var lease = await h.ConnectAndLeaseAsync();
        await lease.StartAsync(32, 127, 0xFFF0, Payload);
        var link = h.Connection.TryAcquireLink()!;
        await h.RunAsync(link.ConnectAsync(Bytes(GattFixtures.GenericAddress), 1, false, CancellationToken.None));
        h.Locator.Ports.Clear();
        h.Locator.Created.Last().Unplug();
        await h.PumpUntilAsync(() => h.Connection.Status.State == UsbStickState.Lost);
        var newStick = h.Plug("COM12", maxConns: 4);
        await h.PumpUntilAsync(() => h.Connection.Status.State == UsbStickState.Connected);
        var fresh = h.Connection.TryAcquireLink()!; // reuses conn id 0 on the new session

        await h.RunAsync(link.DisconnectAsync()); // the stale device cleans up late
        link.Dispose();
        await h.PumpAsync(TimeSpan.FromMilliseconds(200));

        newStick.Requests(StickProtocol.TypeDisconnect).Should().BeEmpty("a stale link must not kill the new link with the same conn id");
        h.Connection.LinkCount.Should().Be(1);
        fresh.ConnId.Should().Be(0);
    }

    [Fact]
    public async Task KeepAliveTimeout_EndsLinksToo()
    {
        using var h = new StickHarness(maxConns: 4);
        h.Stick!.AddPeripheral(GattFixtures.Generic());
        var lease = await h.ConnectAndLeaseAsync();
        await lease.StartAsync(32, 127, 0xFFF0, Payload);
        var link = h.Connection.TryAcquireLink()!;
        await h.RunAsync(link.ConnectAsync(Bytes(GattFixtures.GenericAddress), 1, false, CancellationToken.None));
        int lost = 0;
        link.LinkLost += _ => Interlocked.Increment(ref lost);

        h.Stick.Transport!.Receive(StickFrameEncoder.Encode(StickProtocol.TypeEvtTimeout, 0, [0xE8, 0x03]));
        await h.PumpUntilAsync(() => Volatile.Read(ref lost) > 0);

        lost.Should().Be(1);
    }

    [Fact]
    public async Task ButtonPress_DisconnectsLinks_AndDoesNotRestartAnything()
    {
        using var h = new StickHarness(maxConns: 4);
        h.Stick!.AddPeripheral(GattFixtures.Generic());
        var lease = await h.ConnectAndLeaseAsync();
        await lease.StartAsync(32, 127, 0xFFF0, Payload);
        var link = h.Connection.TryAcquireLink()!;
        await h.RunAsync(link.ConnectAsync(Bytes(GattFixtures.GenericAddress), 1, false, CancellationToken.None));
        int lost = 0;
        link.LinkLost += _ => Interlocked.Increment(ref lost);

        h.Stick.PressButton();
        await h.PumpAsync(TimeSpan.FromSeconds(2));

        lost.Should().Be(1, "exactly one loss callback even though the stick also sends EVT_DISCONNECTED");
        h.Stick.Sets.Should().BeEmpty("sets are not restarted after the user pressed the button");
        h.Stick.Requests(StickProtocol.TypeConnect).Should().HaveCount(1);
        h.Connection.Status.State.Should().Be(UsbStickState.Connected, "the session stays");
    }

    [Fact]
    public async Task Scan_ResumesAfterReplug()
    {
        using var h = new StickHarness(maxConns: 4);
        await h.ConnectStickAsync();
        var scanner = new StickScanner(h.Connection, _ => { }, new StickAddressBook(), Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        await h.RunAsync(scanner.StartAsync());

        h.Locator.Ports.Clear();
        h.Locator.Created.Last().Unplug();
        await h.PumpUntilAsync(() => h.Connection.Status.State == UsbStickState.Lost);
        var newStick = h.Plug("COM12", maxConns: 4);

        (await h.PumpUntilAsync(() => newStick.Scanning)).Should().BeTrue("the scan is stateless and restarted");
        (newStick.LastScanFlags & 1).Should().Be(1, "active scan again");
    }

    [Fact]
    public async Task TwoScanners_ShareOneStickScan_LastStopStopsIt()
    {
        using var h = new StickHarness(maxConns: 4);
        await h.ConnectStickAsync();
        var book = new StickAddressBook();
        var one = new StickScanner(h.Connection, _ => { }, book, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        var two = new StickScanner(h.Connection, _ => { }, book, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        await h.RunAsync(one.StartAsync());
        await h.RunAsync(two.StartAsync());
        h.Stick!.ScanStartCount.Should().Be(1);

        await h.RunAsync(one.StopAsync());
        h.Stick.Scanning.Should().BeTrue();
        await h.RunAsync(two.StopAsync());
        h.Stick.Scanning.Should().BeFalse();
    }

    [Fact]
    public async Task ScanWithoutGattSupport_IsRefused()
    {
        using var h = new StickHarness(maxConns: 0);
        await h.ConnectStickAsync();
        var scanner = new StickScanner(h.Connection, _ => { }, new StickAddressBook(), Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        (await h.RunAsync(scanner.StartAsync())).Should().BeFalse();
        h.Stick!.ScanStartCount.Should().Be(0);
    }

    // ------------------------------------------------------------------ keep-alive under load

    [Fact]
    public async Task PingGaps_StayBelow250ms_WhileNotificationsFlood()
    {
        using var h = new StickHarness(maxConns: 4);
        h.Stick!.AddPeripheral(GattFixtures.Generic());
        var link = await h.ConnectAndAcquireLinkAsync();
        await h.RunAsync(link.ConnectAsync(Bytes(GattFixtures.GenericAddress), 1, false, CancellationToken.None));
        int delivered = 0;
        link.NotificationHandler = (_, _) => Interlocked.Increment(ref delivered);

        for (int round = 0; round < 100; round++)
        {
            for (int i = 0; i < 20; i++)
            {
                h.Stick.Notify(link.ConnId, 0x25, [1, 2, 3, 4]);
            }

            await h.PumpAsync(TimeSpan.FromMilliseconds(30));
        }

        var times = h.Stick.Received.Where(r => r.Frame.Type is StickProtocol.TypePing).Select(r => r.Time).ToList();
        times.Should().NotBeEmpty();
        // every gap between frames of the host (PING or anything else) stays below 250 ms
        var all = h.Stick.Received.Select(r => r.Time).ToList();
        all.Zip(all.Skip(1), (a, b) => b - a).Should().OnlyContain(gap => gap <= TimeSpan.FromMilliseconds(250));
        h.Connection.Status.State.Should().Be(UsbStickState.Connected);
        (await h.PumpUntilAsync(() => Volatile.Read(ref delivered) == 2000)).Should().BeTrue();
    }

    [Fact]
    public async Task LongPendingWrite_DoesNotBlockPingsOrOtherRequests()
    {
        using var h = new StickHarness(maxConns: 4);
        h.Stick!.AddPeripheral(GattFixtures.Generic());
        var link = await h.ConnectAndAcquireLinkAsync();
        await h.RunAsync(link.ConnectAsync(Bytes(GattFixtures.GenericAddress), 1, false, CancellationToken.None));
        h.Stick.DeferProcedures = true;

        var write = link.WriteAsync(0x21, [1], CancellationToken.None);
        int pingsBefore = h.Stick.Requests(StickProtocol.TypePing).Count;
        await h.PumpAsync(TimeSpan.FromSeconds(2));
        h.Stick.DeferProcedures = false;
        var command = await h.RunAsync(link.WriteCommandAsync(0x23, [5], CancellationToken.None));

        h.Stick.Requests(StickProtocol.TypePing).Count.Should().BeGreaterThan(pingsBefore, "the keep-alive continues while a write is pending");
        command.IsOk.Should().BeTrue("another request is answered while the first is pending (matched by SEQ)");
        h.Stick.ResolvePending();
        (await h.RunAsync(write)).IsOk.Should().BeTrue();
    }
}
