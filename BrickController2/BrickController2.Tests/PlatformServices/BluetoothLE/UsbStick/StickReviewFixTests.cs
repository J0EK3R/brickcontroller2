// Regression tests for the review findings of task 010: SEQ reuse (S1), conn_id release / stale events (S2),
// scan callback thread, idle links on stick loss and the scanning status after the button.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

public class StickReviewFixTests
{
    private static readonly byte[] GenericAddressBytes = Bytes(GattFixtures.GenericAddress);

    private static byte[] Bytes(string address)
    {
        StickAddress.TryParse(address, out var bytes);
        return bytes;
    }

    /// <summary>
    /// Harness with a connected link on a generic peripheral.
    /// </summary>
    private static async Task<(StickHarness H, StickGattLink Link)> ConnectedAsync()
    {
        var h = new StickHarness(maxConns: 4);
        h.Stick!.AddPeripheral(GattFixtures.Generic());
        var link = await h.ConnectAndAcquireLinkAsync();
        (await h.RunAsync(link.ConnectAsync(GenericAddressBytes, 1, false, CancellationToken.None))).IsOk.Should().BeTrue();
        return (h, link);
    }

    // ------------------------------------------------------------------ S1: sequence numbers

    /// <summary>
    /// A write waits for its answer while hundreds of other requests are sent (the sequence numbers wrap around).
    /// Its sequence number must not be handed out again: the late answer still has to reach the first caller.
    /// </summary>
    [Fact]
    public async Task PendingRequest_KeepsItsSeq_WhileSequenceNumbersWrapAround()
    {
        var (h, link) = await ConnectedAsync();
        using var _h = h;
        h.Stick!.DeferProcedures = true;
        var first = link.WriteAsync(0x21, [1], CancellationToken.None);
        (await h.PumpUntilAsync(() => h.Stick.HasPendingProcedure, TimeSpan.FromSeconds(1))).Should().BeTrue();
        h.Stick.DeferProcedures = false;

        for (int i = 0; i < 300; i++)
        {
            (await h.RunAsync(link.WriteCommandAsync(0x22, [2], CancellationToken.None))).IsOk.Should().BeTrue();
        }

        first.IsCompleted.Should().BeFalse("its answer has not been sent yet");
        h.Stick.ResolvePending();
        (await h.RunAsync(first)).IsOk.Should().BeTrue("the answer belongs to the first request, not to one that reused its SEQ");
    }

    /// <summary>
    /// An ACK that names another request type is not accepted as the answer.
    /// </summary>
    [Fact]
    public async Task Ack_OfAnotherRequestType_IsNotTakenAsResult()
    {
        var (h, link) = await ConnectedAsync();
        using var _h = h;
        h.Stick!.DeferProcedures = true;
        var write = link.WriteAsync(0x21, [1], CancellationToken.None);
        (await h.PumpUntilAsync(() => h.Stick.HasPendingProcedure, TimeSpan.FromSeconds(1))).Should().BeTrue();

        h.Stick.ResolvePendingWithWrongType(StickProtocol.TypeSubscribe);

        (await h.RunAsync(write)).IsOk.Should().BeFalse();
    }

    // ------------------------------------------------------------------ S2: abort and stale events

    /// <summary>
    /// After a cancelled connect the conn_id is only handed back once the stick confirmed (or after the bounded wait).
    /// </summary>
    [Fact]
    public async Task CancelledConnect_WaitsForStickConfirmationBeforeReturning()
    {
        using var h = new StickHarness(maxConns: 4);
        h.Stick!.AddPeripheral(GattFixtures.Generic()).ConnectDelayMs = 5000;
        h.Stick.SuppressDisconnectEvent = true; // the confirmation is late
        var link = await h.ConnectAndAcquireLinkAsync();
        using var cts = new CancellationTokenSource();
        var task = link.ConnectAsync(GenericAddressBytes, 1, false, cts.Token);
        await h.PumpAsync(TimeSpan.FromMilliseconds(100));

        cts.Cancel();
        await h.PumpAsync(TimeSpan.FromMilliseconds(1000));
        task.IsCompleted.Should().BeFalse("the abort waits for EVT_DISCONNECTED so the conn_id is not reused too early");

        await FluentActions.Awaiting(() => h.RunAsync(task)).Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>
    /// A late EVT_DISCONNECTED (cancelled) of an aborted attempt must not fail the next connect on the same conn_id.
    /// </summary>
    [Fact]
    public async Task StaleCancelledEvent_DoesNotFailTheNewConnectAttempt()
    {
        using var h = new StickHarness(maxConns: 4);
        h.Stick!.AddPeripheral(GattFixtures.Generic()).ConnectDelayMs = 500;
        var link = await h.ConnectAndAcquireLinkAsync();
        var task = link.ConnectAsync(GenericAddressBytes, 1, false, CancellationToken.None);
        await h.PumpAsync(TimeSpan.FromMilliseconds(50));

        h.Stick.SendEvent(StickProtocol.TypeEvtDisconnected, [link.ConnId, 0, StickProtocol.ReasonCancelled]);

        (await h.RunAsync(task)).IsOk.Should().BeTrue();
        link.IsConnected.Should().BeTrue();
    }

    /// <summary>
    /// A "was not connected" EVT_DISCONNECTED cannot belong to an established link.
    /// </summary>
    [Fact]
    public async Task StaleAttemptEvent_DoesNotEndAnEstablishedLink()
    {
        var (h, link) = await ConnectedAsync();
        using var _h = h;
        int lost = 0;
        link.LinkLost += _ => Interlocked.Increment(ref lost);

        h.Stick!.SendEvent(StickProtocol.TypeEvtDisconnected, [link.ConnId, 0, 0x16]);
        await h.PumpAsync(TimeSpan.FromMilliseconds(100));

        link.IsConnected.Should().BeTrue();
        lost.Should().Be(0);
    }

    /// <summary>
    /// A real peer disconnect still ends the link (the stale filter must not swallow it).
    /// </summary>
    [Fact]
    public async Task RealPeerDisconnect_StillEndsTheLink()
    {
        var (h, link) = await ConnectedAsync();
        using var _h = h;
        int lost = 0;
        link.LinkLost += _ => Interlocked.Increment(ref lost);

        h.Stick!.DropLink(link.ConnId);
        await h.PumpAsync(TimeSpan.FromMilliseconds(100));

        link.IsConnected.Should().BeFalse();
        lost.Should().Be(1);
    }

    // ------------------------------------------------------------------ nits

    /// <summary>
    /// A slow BC2 scan callback must not block the transport read thread (it runs on its own reader task).
    /// </summary>
    [Fact]
    public async Task ScanCallback_CannotBlockTheReadThread()
    {
        using var h = new StickHarness(maxConns: 4);
        await h.ConnectStickAsync();
        var release = new ManualResetEventSlim(false);
        var scanner = new StickScanner(h.Connection, _ =>
        {
            release.Wait(TimeSpan.FromSeconds(10)); // a slow BC2 consumer
        }, new StickAddressBook(), NullLogger.Instance);
        (await h.RunAsync(scanner.StartAsync())).Should().BeTrue();
        h.Stick!.AfterEvent = null; // do not wait for the callback in this test
        var peripheral = h.Stick.AddPeripheral(GattFixtures.MkDiy());

        var feed = Task.Run(() => h.Stick.EmitAdvertising(peripheral)); // stands in for the transport read thread

        bool returned = feed.Wait(TimeSpan.FromSeconds(3));
        release.Set();
        returned.Should().BeTrue("the read loop must not wait for the scan callback");
        scanner.WaitUntilDelivered(TimeSpan.FromSeconds(5));
        await h.RunAsync(scanner.StopAsync());
    }

    /// <summary>
    /// Scan results arrive in order, also after Stop (late reports are dropped without errors).
    /// </summary>
    [Fact]
    public async Task ScanCallback_KeepsOrder_AndStopDropsLateReports()
    {
        using var h = new StickHarness(maxConns: 4);
        await h.ConnectStickAsync();
        var seen = new System.Collections.Generic.List<string>();
        var scanner = new StickScanner(h.Connection, r => { lock (seen) { seen.Add(r.DeviceAddress); } }, new StickAddressBook(), NullLogger.Instance);
        (await h.RunAsync(scanner.StartAsync())).Should().BeTrue();
        var a = h.Stick!.AddPeripheral(GattFixtures.MkDiy());

        for (int i = 0; i < 20; i++)
        {
            h.Stick.EmitAdvertising(a);
        }

        lock (seen) { seen.Should().HaveCount(20); }
        await h.RunAsync(scanner.StopAsync());
        h.Stick.EmitAdvertising(a); // must not throw or deliver
        lock (seen) { seen.Should().HaveCount(20); }
    }

    /// <summary>
    /// A link that was never connected survives the button / stick loss and can still connect (no AlreadyRunning).
    /// </summary>
    [Fact]
    public async Task IdleLink_AfterButton_CanStillConnect()
    {
        using var h = new StickHarness(maxConns: 4);
        h.Stick!.AddPeripheral(GattFixtures.Generic());
        var link = await h.ConnectAndAcquireLinkAsync();

        h.Stick.PressButton();
        await h.PumpAsync(TimeSpan.FromMilliseconds(100));

        var result = await h.RunAsync(link.ConnectAsync(GenericAddressBytes, 1, false, CancellationToken.None));
        result.Status.Should().NotBe(StickStatus.AlreadyRunning);
        result.IsOk.Should().BeTrue();
    }

    /// <summary>
    /// A link that was never connected does not report AlreadyRunning after the stick was lost either.
    /// </summary>
    [Fact]
    public async Task IdleLink_AfterStickLoss_FailsWithoutAlreadyRunning()
    {
        using var h = new StickHarness(maxConns: 4);
        h.Stick!.AddPeripheral(GattFixtures.Generic());
        var link = await h.ConnectAndAcquireLinkAsync();

        h.Locator.Ports.Clear();
        h.Locator.Created.Last().Unplug();
        await h.PumpUntilAsync(() => h.Connection.Status.State != UsbStickState.Connected);

        var result = await h.RunAsync(link.ConnectAsync(GenericAddressBytes, 1, false, CancellationToken.None));
        result.IsOk.Should().BeFalse();
        result.Status.Should().NotBe(StickStatus.AlreadyRunning);
    }

    /// <summary>
    /// After the button the scanners stay registered but the stick no longer scans: the status must say so.
    /// </summary>
    [Fact]
    public async Task ScanningStatus_IsFalseAfterButtonPress()
    {
        using var h = new StickHarness(maxConns: 4);
        await h.ConnectStickAsync();
        var scanner = new StickScanner(h.Connection, _ => { }, new StickAddressBook(), NullLogger.Instance);
        (await h.RunAsync(scanner.StartAsync())).Should().BeTrue();
        h.Connection.IsScanning.Should().BeTrue();

        h.Stick!.PressButton();
        await h.PumpAsync(TimeSpan.FromMilliseconds(100));

        h.Connection.IsScanning.Should().BeFalse();
    }
}
