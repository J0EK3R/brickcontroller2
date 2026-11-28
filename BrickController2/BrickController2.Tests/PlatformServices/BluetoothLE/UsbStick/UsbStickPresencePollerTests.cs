// Tests of the stick presence poll: it must find a (re-)plugged stick while a settings row watches or the stick is
// lost, open no port while no candidate exists, and do nothing when the feature is disabled.

using System;
using System.Linq;
using System.Threading.Tasks;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using FluentAssertions;
using Xunit;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Verifies <see cref="UsbStickPresencePoller"/> with the fake stick, locator and fake clock.
/// </summary>
public class UsbStickPresencePollerTests
{
    private static readonly byte[] Payload = [1, 2, 3];

    /// <summary>
    /// Creates a poller whose delay runs on the harness' fake clock (so Pump advances the poll).
    /// </summary>
    private static UsbStickPresencePoller CreatePoller(StickHarness h, Func<bool> enabled) =>
        new(h.Connection, enabled, (t, ct) => Task.Delay(t, h.Clock, ct));

    [Fact]
    public async Task NotFound_WhileWatching_FindsStickPluggedInLater()
    {
        using var h = new StickHarness(plugged: false);
        using var poller = CreatePoller(h, () => true);
        int statusChanges = 0;
        h.Connection.StatusChanged += (_, _) => statusChanges++;

        using var watch = poller.BeginWatching();
        await h.PumpAsync(TimeSpan.FromSeconds(3));
        h.Connection.Status.State.Should().Be(UsbStickState.NotFound);

        h.Plug("COM9");
        (await h.PumpUntilAsync(() => h.Connection.Status.State == UsbStickState.Connected)).Should().BeTrue();

        h.Connection.Status.PortName.Should().Be("COM9");
        statusChanges.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task NoCandidate_OpensNoPort_ButReadsThePortList()
    {
        using var h = new StickHarness(plugged: false);
        using var poller = CreatePoller(h, () => true);

        using var watch = poller.BeginWatching();
        await h.PumpAsync(TimeSpan.FromSeconds(5));

        h.Locator.Created.Should().BeEmpty("only the registry/port list may be read while no candidate exists");
        h.Locator.ListCalls.Should().BeGreaterThan(2);
    }

    [Fact]
    public async Task Lost_FindsReplugWithoutAnyWatcher()
    {
        using var h = new StickHarness();
        var lease = await h.ConnectAndLeaseAsync();
        await lease.StartAsync(32, 127, 0xFFF0, Payload);
        using var poller = CreatePoller(h, () => true);

        h.Locator.Ports.Clear();
        h.Locator.Created.Last().Unplug();
        (await h.PumpUntilAsync(() => h.Connection.Status.State == UsbStickState.Lost)).Should().BeTrue();
        await h.PumpAsync(TimeSpan.FromSeconds(3));

        var newStick = h.Plug("COM12");
        (await h.PumpUntilAsync(() => h.Connection.Status.State == UsbStickState.Connected)).Should().BeTrue();
        (await h.PumpUntilAsync(() => newStick.Sets.ContainsKey(0))).Should().BeTrue("the leased set is resumed");
        h.Connection.Status.PortName.Should().Be("COM12");
    }

    [Fact]
    public async Task ConnectedStickLostWhileWatching_IsFoundAgain()
    {
        using var h = new StickHarness();
        await h.ConnectStickAsync();
        using var poller = CreatePoller(h, () => true);
        using var watch = poller.BeginWatching();

        // no lease: the connection ends as NotFound (no reconnect loop), only the poll can bring it back
        h.Locator.Ports.Clear();
        h.Locator.Created.Last().Unplug();
        (await h.PumpUntilAsync(() => h.Connection.Status.State == UsbStickState.NotFound)).Should().BeTrue();

        h.Plug("COM9");
        (await h.PumpUntilAsync(() => h.Connection.Status.State == UsbStickState.Connected)).Should().BeTrue();
    }

    [Fact]
    public async Task ModeOff_DoesNothing()
    {
        using var h = new StickHarness(plugged: false);
        using var poller = CreatePoller(h, () => false);

        using var watch = poller.BeginWatching();
        h.Plug("COM9");
        await h.PumpAsync(TimeSpan.FromSeconds(5));

        h.Locator.ListCalls.Should().Be(0);
        h.Locator.Created.Should().BeEmpty();
        h.Connection.Status.State.Should().Be(UsbStickState.NotFound);
    }

    [Fact]
    public async Task NotWatchedAndNotLost_DoesNothing()
    {
        using var h = new StickHarness(plugged: false);
        using var poller = CreatePoller(h, () => true);

        h.Plug("COM9");
        await h.PumpAsync(TimeSpan.FromSeconds(5));

        h.Locator.ListCalls.Should().Be(0);
        h.Connection.Status.State.Should().Be(UsbStickState.NotFound);
    }

    [Fact]
    public async Task DisposedWatch_StopsPolling()
    {
        using var h = new StickHarness(plugged: false);
        using var poller = CreatePoller(h, () => true);

        var watch = poller.BeginWatching();
        await h.PumpAsync(TimeSpan.FromSeconds(2));
        watch.Dispose();
        await h.PumpAsync(TimeSpan.FromSeconds(2));   // lets the loop notice and end
        int calls = h.Locator.ListCalls;

        await h.PumpAsync(TimeSpan.FromSeconds(5));
        h.Locator.ListCalls.Should().Be(calls);
    }
}
