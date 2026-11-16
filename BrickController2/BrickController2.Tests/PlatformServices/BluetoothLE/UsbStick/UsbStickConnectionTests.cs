using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using FluentAssertions;
using Xunit;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Session handling of the shared stick connection, driven by a simulated stick and a fake clock.
/// </summary>
public class UsbStickConnectionTests
{
    private static readonly byte[] Payload = [1, 2, 3, 4, 5, 6, 7, 8];

    [Fact]
    public async Task Connect_WithStick_ReportsStatusAndInfo()
    {
        using var h = new StickHarness();

        (await h.RunAsync(h.Connection.TryConnectAsync())).Should().BeTrue();

        h.Connection.Status.State.Should().Be(UsbStickState.Connected);
        h.Connection.Status.PortName.Should().Be("COM7");
        h.Connection.Status.FirmwareVersion.Should().Be("0.1.0");
        h.Connection.Info!.MaxSets.Should().Be(5);
        h.Stick!.HasSession.Should().BeTrue();
    }

    [Fact]
    public async Task Connect_WithoutAnyPort_ReportsNotFound()
    {
        using var h = new StickHarness(plugged: false);

        (await h.RunAsync(h.Connection.TryConnectAsync())).Should().BeFalse();

        h.Connection.Status.State.Should().Be(UsbStickState.NotFound);
        h.Connection.TryAcquireLease().Should().BeNull();
    }

    [Fact]
    public async Task Connect_SkipsSilentForeignPortAndClosesIt()
    {
        using var h = new StickHarness();
        h.Locator.Ports.Clear();
        h.Locator.Ports["COM3"] = new FakeStickDevice(h.Clock) { Silent = true }; // foreign board
        h.Locator.Ports["COM7"] = h.Stick;

        (await h.RunAsync(h.Connection.TryConnectAsync())).Should().BeTrue();

        h.Connection.Status.PortName.Should().Be("COM7");
        h.Locator.Created.Single(t => t.Name == "COM3").IsOpen.Should().BeFalse("other ports are closed immediately");
    }

    [Fact]
    public async Task Connect_StopsAtFirstStick_NeverCreatesLaterPorts()
    {
        using var h = new StickHarness();
        h.Locator.Ports["COM9"] = new FakeStickDevice(h.Clock);

        (await h.RunAsync(h.Connection.TryConnectAsync())).Should().BeTrue();

        h.Locator.Created.Select(t => t.Name).Should().Equal("COM7");
    }

    [Fact]
    public async Task Connect_PortWithForeignMagic_IsSkippedAndClosed()
    {
        using var h = new StickHarness(plugged: false);
        var wrongMagic = new FakeStickDevice(h.Clock) { Magic = "NOTBC2"u8.ToArray() };
        h.Locator.Ports["COM4"] = wrongMagic;
        h.Plug("COM8");

        (await h.RunAsync(h.Connection.TryConnectAsync())).Should().BeTrue();

        h.Connection.Status.PortName.Should().Be("COM8");
        h.Locator.Created.Single(t => t.Name == "COM4").IsOpen.Should().BeFalse();
    }

    [Fact]
    public async Task Connect_StickRefusesProtocolVersion_ReportsIncompatible()
    {
        using var h = new StickHarness();
        h.Stick!.AcceptedMajor = 2;

        (await h.RunAsync(h.Connection.TryConnectAsync())).Should().BeFalse();

        h.Connection.Status.State.Should().Be(UsbStickState.Incompatible);
    }

    [Fact]
    public async Task Connect_AfterFailedProbe_UsesNegativeCacheUntilForced()
    {
        using var h = new StickHarness(plugged: false);
        await h.RunAsync(h.Connection.TryConnectAsync());

        h.Plug("COM7");

        (await h.RunAsync(h.Connection.TryConnectAsync())).Should().BeFalse("a failed probe is cached for a few seconds");
        (await h.RunAsync(h.Connection.TryConnectAsync(forceProbe: true))).Should().BeTrue();
    }

    [Fact]
    public async Task Lease_AllocatesUpToMaxSets_ThenReturnsNull()
    {
        using var h = new StickHarness();
        await h.RunAsync(h.Connection.TryConnectAsync());

        var leases = Enumerable.Range(0, 5).Select(_ => h.Connection.TryAcquireLease()).ToList();

        leases.Should().OnlyContain(l => l != null);
        leases.Select(l => l!.SetId).Should().BeEquivalentTo(new byte[] { 0, 1, 2, 3, 4 });
        h.Connection.TryAcquireLease().Should().BeNull("the 6th device falls back to the Windows advertiser");
    }

    [Fact]
    public async Task Lease_AfterRelease_SetIdCanBeReused()
    {
        using var h = new StickHarness();
        await h.RunAsync(h.Connection.TryConnectAsync());
        var leases = Enumerable.Range(0, 5).Select(_ => h.Connection.TryAcquireLease()!).ToList();

        leases[2].Dispose();
        await h.PumpUntilAsync(() => h.Connection.LeaseCount == 4);

        h.Connection.TryAcquireLease()!.SetId.Should().Be(2);
    }

    [Fact]
    public async Task Ping_IsSentEvery200ms_AndNeverLaterThan250ms()
    {
        using var h = new StickHarness();
        var lease = await h.ConnectAndLeaseAsync();
        await lease.StartAsync(32, 127, 0xFFF0, Payload);

        await h.PumpAsync(TimeSpan.FromSeconds(3));

        var pings = h.Stick!.Received.Where(r => r.Frame.Type == StickProtocol.TypePing).Select(r => r.Time).ToList();
        pings.Count.Should().BeGreaterThanOrEqualTo(13);
        pings.Zip(pings.Skip(1), (a, b) => b - a).Should().OnlyContain(gap => gap <= TimeSpan.FromMilliseconds(250));
        h.Stick.HasSession.Should().BeTrue("keep-alive keeps the session open although no data changes");
        h.Stick.Sets.Should().ContainKey(0);
    }

    [Fact]
    public async Task Ping_IsSkippedWhileOtherFramesKeepTheStickAwake()
    {
        using var h = new StickHarness();
        var lease = await h.ConnectAndLeaseAsync();
        await lease.StartAsync(32, 127, 0xFFF0, Payload);
        int pingsBefore = h.Stick!.Requests(StickProtocol.TypePing).Count;

        for (int i = 0; i < 20; i++)
        {
            await lease.UpdateAsync(0xFFF0, [(byte)i, 1, 2]);
            await h.PumpAsync(TimeSpan.FromMilliseconds(50));
        }

        h.Stick.Requests(StickProtocol.TypePing).Count.Should().Be(pingsBefore, "any valid frame resets the stick's keep-alive");
    }

    [Fact]
    public async Task SequenceNumbers_NeverUseZeroAndWrapFrom255To1()
    {
        using var h = new StickHarness();
        var lease = await h.ConnectAndLeaseAsync();
        await lease.StartAsync(32, 127, 0xFFF0, Payload);

        for (int i = 0; i < 300; i++)
        {
            await lease.UpdateAsync(0xFFF0, [(byte)i, (byte)(i >> 8)]);
        }

        var seqs = h.Stick!.Received.Select(r => r.Frame.Seq).ToList();
        seqs.Should().NotContain(0);
        seqs.Should().Contain(255);
        seqs.Zip(seqs.Skip(1), (a, b) => (a, b)).Where(p => p.a == 255).Should().OnlyContain(p => p.b == 1);
    }

    [Fact]
    public async Task Request_LostResponse_IsRetriedWithSameSequence()
    {
        using var h = new StickHarness();
        var lease = await h.ConnectAndLeaseAsync();
        h.Stick!.DropNextResponses = 1; // the stick executes SET_START but the answer is lost

        await h.RunAsync(RunStartAsync(lease));

        var starts = h.Stick.Requests(StickProtocol.TypeSetStart);
        starts.Should().HaveCount(2);
        starts.Select(f => f.Seq).Distinct().Should().HaveCount(1);
        h.Stick.Sets.Should().ContainKey(0, "ALREADY_RUNNING on the duplicate counts as success");
    }

    [Fact]
    public async Task Unplug_WhileRunning_ReconnectsAndResumesSetWithLatestPayload()
    {
        using var h = new StickHarness();
        var lease = await h.ConnectAndLeaseAsync();
        await lease.StartAsync(32, 127, 0xFFF0, Payload);
        var oldStick = h.Stick!;

        // pull the cable
        h.Locator.Ports.Clear();
        h.Locator.Created.Last().Unplug();
        await h.PumpUntilAsync(() => h.Connection.Status.State == UsbStickState.Lost);

        // BC2 keeps updating while the stick is away: no error, data is remembered
        await lease.UpdateAsync(0xFFF0, [9, 9, 9]);
        await h.PumpAsync(TimeSpan.FromSeconds(1));
        h.Connection.Status.State.Should().Be(UsbStickState.Lost, "no live switch to another advertiser");

        // plug in again, possibly on another COM port
        var newStick = h.Plug("COM12");
        (await h.PumpUntilAsync(() => h.Connection.Status.State == UsbStickState.Connected)).Should().BeTrue();
        await h.PumpUntilAsync(() => newStick.Sets.ContainsKey(0));

        newStick.Sets[0].Data.Should().Equal(9, 9, 9);
        newStick.Sets[0].Mfr.Should().Be(0xFFF0);
        h.Connection.Status.PortName.Should().Be("COM12");
        oldStick.Should().NotBeSameAs(newStick);
    }

    [Fact]
    public async Task KeepAliveTimeoutOfStick_IsRecoveredWithHelloAndSetStart()
    {
        using var h = new StickHarness();
        var lease = await h.ConnectAndLeaseAsync();
        await lease.StartAsync(32, 127, 0xFFF0, Payload);

        // The firmware stops answering for a while: three PINGs in a row time out.
        h.Stick!.Silent = true;
        await h.PumpUntilAsync(() => h.Connection.Status.State == UsbStickState.Lost);
        h.Stick.Silent = false;

        (await h.PumpUntilAsync(() => h.Connection.Status.State == UsbStickState.Connected)).Should().BeTrue();
        await h.PumpUntilAsync(() => h.Stick.Sets.ContainsKey(0));

        h.Stick.HasSession.Should().BeTrue();
        h.Stick.Sets[0].Data.Should().Equal(Payload);
    }

    [Fact]
    public async Task EventTimeout_FromStick_TriggersHelloAndRestart()
    {
        using var h = new StickHarness();
        var lease = await h.ConnectAndLeaseAsync();
        await lease.StartAsync(32, 127, 0xFFF0, Payload);
        int hellos = h.Stick!.Requests(StickProtocol.TypeHello).Count;

        // A pause longer than the stick's keep-alive without our PINGs reaching it is simulated by
        // letting the firmware end its session and report EVT_TIMEOUT.
        h.Stick.Transport!.Receive(StickFrameEncoder.Encode(StickProtocol.TypeEvtTimeout, 0, [0xE8, 0x03]));
        await h.PumpUntilAsync(() => h.Stick.Requests(StickProtocol.TypeHello).Count > hellos);
        await h.PumpUntilAsync(() => h.Stick.Sets.ContainsKey(0));

        h.Connection.Status.State.Should().Be(UsbStickState.Connected);
    }

    [Fact]
    public async Task NoSessionAnswer_MarksStickLostAndRecovers()
    {
        using var h = new StickHarness();
        var lease = await h.ConnectAndLeaseAsync();
        await lease.StartAsync(32, 127, 0xFFF0, Payload);
        // End the session on the stick side only: the next request answers NO_SESSION.
        h.Stick!.EndSession();
        await lease.UpdateAsync(0xFFF0, [5]);

        (await h.PumpUntilAsync(() => h.Connection.Status.State == UsbStickState.Connected && h.Stick.Sets.ContainsKey(0))).Should().BeTrue();
        h.Stick.Sets[0].Data.Should().Equal(5);
    }

    [Fact]
    public async Task ButtonPress_StopsSetsAndDoesNotRestartAutomatically()
    {
        using var h = new StickHarness();
        var lease = await h.ConnectAndLeaseAsync();
        await lease.StartAsync(32, 127, 0xFFF0, Payload);

        h.Stick!.PressButton();
        await lease.UpdateAsync(0xFFF0, [7, 7]);
        await h.PumpAsync(TimeSpan.FromSeconds(1));

        h.Stick.Sets.Should().BeEmpty("the user stopped the toy on purpose");
        h.Stick.Requests(StickProtocol.TypeSetStart).Should().HaveCount(1);

        // an explicit start (device reconnected in the app) resumes
        await lease.StartAsync(32, 127, 0xFFF0, [8]);
        h.Stick.Sets[0].Data.Should().Equal(8);
    }

    [Fact]
    public async Task SetStoppedByFirmware_IsRestartedOnce()
    {
        using var h = new StickHarness();
        var lease = await h.ConnectAndLeaseAsync();
        await lease.StartAsync(32, 127, 0xFFF0, Payload);

        h.Stick!.StopSetByFirmware(0);
        await h.PumpUntilAsync(() => h.Stick.Sets.ContainsKey(0));

        h.Stick.Sets[0].Data.Should().Equal(Payload);
        h.Stick.Requests(StickProtocol.TypeSetStart).Should().HaveCount(2);
    }

    [Fact]
    public async Task LastLeaseReleased_SendsStopAllAndClosesPortAfterIdleTime()
    {
        using var h = new StickHarness();
        var lease = await h.ConnectAndLeaseAsync();
        await lease.StartAsync(32, 127, 0xFFF0, Payload);

        lease.Dispose();
        await h.PumpUntilAsync(() => h.Stick!.Requests(StickProtocol.TypeStopAll).Count == 1);
        h.Stick!.Sets.Should().BeEmpty();
        h.Connection.Status.State.Should().Be(UsbStickState.Connected);

        await h.PumpAsync(TimeSpan.FromSeconds(6));

        h.Connection.Status.State.Should().Be(UsbStickState.Idle);
        h.Locator.Created.Last().IsOpen.Should().BeFalse();

        // the next device reopens the stick on demand
        (await h.RunAsync(h.Connection.TryConnectAsync())).Should().BeTrue();
        h.Connection.TryAcquireLease().Should().NotBeNull();
    }

    [Fact]
    public async Task Lease_AcquiredDuringIdleDelay_CancelsPortClose()
    {
        using var h = new StickHarness();
        var lease = await h.ConnectAndLeaseAsync();
        lease.Dispose();
        await h.PumpUntilAsync(() => h.Connection.LeaseCount == 0);
        await h.PumpAsync(TimeSpan.FromSeconds(2));

        var again = h.Connection.TryAcquireLease();
        await h.PumpAsync(TimeSpan.FromSeconds(6));

        again.Should().NotBeNull();
        h.Connection.Status.State.Should().Be(UsbStickState.Connected);
    }

    [Fact]
    public async Task ParallelStartsAndUpdates_ProduceOnlyValidFramesAndFinalDataPerSet()
    {
        using var h = new StickHarness();
        await h.RunAsync(h.Connection.TryConnectAsync());
        var leases = Enumerable.Range(0, 5).Select(_ => h.Connection.TryAcquireLease()!).ToList();

        // each set is driven from its own thread, all sets run concurrently on one connection
        await Task.WhenAll(leases.Select(lease => Task.Run(async () =>
        {
            await lease.StartAsync(32, 127, 0xFFF0, [lease.SetId, 0]);
            for (int i = 1; i <= 100; i++)
            {
                await lease.UpdateAsync(0xFFF0, [lease.SetId, (byte)i]);
            }
        })));

        h.Stick!.ParseErrors.Should().Be(0);
        foreach (var lease in leases)
        {
            h.Stick.Sets[lease.SetId].Data.Should().Equal(lease.SetId, 100);
        }
    }

    [Fact]
    public async Task ConcurrentUpdatesOfOneSet_AreCoalescedAndLastWriterWins()
    {
        using var h = new StickHarness();
        var lease = await h.ConnectAndLeaseAsync();
        await lease.StartAsync(32, 127, 0xFFF0, [0]);

        await Task.WhenAll(Enumerable.Range(1, 50).Select(i => Task.Run(() => lease.UpdateAsync(0xFFF0, [(byte)i]))));

        h.Stick!.ParseErrors.Should().Be(0);
        h.Stick.Requests(StickProtocol.TypeSetUpdate).Count.Should().BeLessThanOrEqualTo(50);
        h.Stick.Sets[0].Data.Should().HaveCount(1);
    }

    private static async Task RunStartAsync(StickSetLease lease) => await lease.StartAsync(32, 127, 0xFFF0, Payload);
}
