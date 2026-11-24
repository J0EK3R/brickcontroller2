// Tests of the stick keep-alive and receive path after the hardware log of tasks 009/010 (Windows, VS debugger):
// the PING must not depend on the thread pool / callbacks / slow close, and the trace must never block.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BrickController2.Diagnostics;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

/// <summary>Keep-alive, event dispatch and trace behaviour of the stick connection.</summary>
public class StickKeepAliveTests
{
    /// <summary>
    /// A blocking event handler (stands in for a "UI"/notification callback) must not stall the read thread:
    /// feeding more bytes returns at once and PINGs keep flowing across the blocked period.
    /// </summary>
    [Fact]
    public async Task BlockedEventHandler_DoesNotBlockReadThread_AndPingKeepsFlowing()
    {
        using var h = new StickHarness(maxConns: 4);
        await h.ConnectStickAsync();
        h.Stick!.AfterEvent = null;                       // the test does not wait for dispatch
        using var release = new ManualResetEventSlim(false);
        var seen = new List<byte>();
        var scanner = new StickScanner(h.Connection, _ => release.Wait(TimeSpan.FromSeconds(10)), new StickAddressBook(), NullLogger.Instance);
        (await h.RunAsync(scanner.StartAsync())).Should().BeTrue();
        var peripheral = h.Stick.AddPeripheral(GattFixtures.MkDiy());
        int pingsBefore = h.Stick.Requests(StickProtocol.TypePing).Count;

        // Events whose handlers block ("UI busy"); the feeding thread (= transport read thread) must come back.
        var feed = Task.Run(() => { for (int i = 0; i < 5; i++) { h.Stick.EmitAdvertising(peripheral); } });
        feed.Wait(TimeSpan.FromSeconds(3)).Should().BeTrue("the read thread must never run the callbacks itself");

        await h.PumpAsync(TimeSpan.FromSeconds(2));
        h.Stick.Requests(StickProtocol.TypePing).Count.Should().BeGreaterThan(pingsBefore + 5, "PING (200 ms) continues while callbacks block");
        h.Connection.Status.State.Should().Be(UsbStickState.Connected);

        release.Set();
        scanner.WaitUntilDelivered(TimeSpan.FromSeconds(5));
        await h.RunAsync(scanner.StopAsync());
        seen.Should().BeEmpty();
    }

    /// <summary>
    /// Events are delivered in arrival order on one consumer even though they leave the read thread.
    /// </summary>
    [Fact]
    public void Session_DispatchesEventsInOrder_OffTheReadThread()
    {
        var clock = new ManualTimeProvider();
        var transport = new FakeStickTransport("COM1", null);
        using var session = new StickSession(transport, clock, NullLogger.Instance);
        var order = new List<int>();
        int handlerThread = -1;
        session.EventReceived += frame =>
        {
            handlerThread = Environment.CurrentManagedThreadId;
            Thread.Sleep(2);                                  // slow handler
            lock (order) { order.Add(frame.Payload[0]); }
        };
        session.Open();

        int readThread = Environment.CurrentManagedThreadId;
        var sw = Stopwatch.StartNew();
        for (byte i = 0; i < 20; i++)
        {
            transport.Receive(StickFrameEncoder.Encode(StickProtocol.TypeEvtButton, 0, [i]));
        }

        sw.ElapsedMilliseconds.Should().BeLessThan(30, "20 x 2 ms of handler time must not be spent on the read thread");
        session.WaitForEventsDispatched(TimeSpan.FromSeconds(5)).Should().BeTrue();
        lock (order) { order.Should().Equal(Enumerable.Range(0, 20)); }
        handlerThread.Should().NotBe(readThread);
    }

    /// <summary>
    /// The port needs a long time to close (SerialPort.Close): PINGs continue until it is really closed, so the
    /// stick never runs into its keep-alive timeout while the host side is still closing.
    /// </summary>
    [Fact]
    public async Task IdleClose_KeepsSendingPing_UntilThePortIsReallyClosed()
    {
        using var gate = new ManualResetEventSlim(false);   // declared first: disposed after the harness
        using var h = new StickHarness();
        var lease = await h.ConnectAndLeaseAsync();
        h.Locator.Created.Last(t => t.WasOpened).CloseGate = gate;   // closing blocks until the gate opens
        lease.Dispose();                                    // last user gone: idle timer starts

        // Let the idle delay (5 s) pass: the close starts and blocks inside the transport.
        (await h.PumpUntilAsync(() => h.Connection.Status.State == UsbStickState.Idle)).Should().BeTrue();
        int pingsAtClose = h.Stick!.Requests(StickProtocol.TypePing).Count;

        await h.PumpAsync(TimeSpan.FromSeconds(1.5));       // 1.5 s while the port is still closing
        h.Stick.Requests(StickProtocol.TypePing).Count.Should().BeGreaterThan(pingsAtClose + 3, "PING must continue while the port closes");
        h.Stick.HasSession.Should().BeTrue("the stick must not have timed out");

        gate.Set();                                         // the port is closed now
        await h.PumpAsync(TimeSpan.FromMilliseconds(300));
        int pingsAfterClose = h.Stick.Requests(StickProtocol.TypePing).Count;
        await h.PumpAsync(TimeSpan.FromSeconds(1));
        h.Stick.Requests(StickProtocol.TypePing).Count.Should().Be(pingsAfterClose, "no PING on a closed port");
    }

    /// <summary>A stick that stops sending frames altogether is declared lost (PONG not needed for the PING itself).</summary>
    [Fact]
    public async Task SilentStick_IsDeclaredLost_ByMissingFramesNotByPongRoundTrips()
    {
        using var h = new StickHarness();
        var lease = await h.ConnectAndLeaseAsync();
        h.Stick!.Silent = true;

        (await h.PumpUntilAsync(() => h.Connection.Status.State == UsbStickState.Lost)).Should().BeTrue();
        lease.Dispose();
    }

    /// <summary>A stick silent for less than the 2 s limit is not declared lost.</summary>
    [Fact]
    public async Task SilentStick_IsNotLost_Before2Seconds()
    {
        using var h = new StickHarness();
        var lease = await h.ConnectAndLeaseAsync();
        h.Stick!.Silent = true;

        await h.PumpAsync(TimeSpan.FromSeconds(1.5));
        h.Connection.Status.State.Should().Be(UsbStickState.Connected);
        lease.Dispose();
    }

    /// <summary>A pump tick that came more than two intervals after the previous one (stalled host) is "late" and gives no verdict.</summary>
    [Theory]
    [InlineData(100, false)]
    [InlineData(400, false)]
    [InlineData(401, true)]
    [InlineData(5000, true)]
    public void LateTick_IsDetected_AfterTwoIntervals(int sinceLastMs, bool expectedLate)
    {
        UsbStickConnection.IsLateTick(TimeSpan.FromMilliseconds(sinceLastMs), TimeSpan.FromMilliseconds(200)).Should().Be(expectedLate);
    }

    /// <summary>
    /// Stopping the pump thread while it wakes up must never throw on the background thread (it would kill the app).
    /// </summary>
    [Fact]
    public async Task PingPump_ShutdownRace_NeverCrashes()
    {
        var unhandled = new List<Exception>();
        UnhandledExceptionEventHandler handler = (_, e) => { lock (unhandled) { unhandled.Add((Exception)e.ExceptionObject); } };
        AppDomain.CurrentDomain.UnhandledException += handler;
        try
        {
            for (int i = 0; i < 100; i++)
            {
                using var h = new StickHarness();
                var lease = await h.ConnectAndLeaseAsync();
                lease.Dispose();
                await Task.Delay(i % 5);                    // varies where the pump thread is while it is stopped
            }

            await Task.Delay(300);                          // let all pump threads exit
        }
        finally
        {
            AppDomain.CurrentDomain.UnhandledException -= handler;
        }

        lock (unhandled) { unhandled.Should().BeEmpty(); }
    }

    /// <summary>
    /// The trace never blocks its callers, however slow the debugger output is, keeps the newest lines and
    /// reports how many were dropped.
    /// </summary>
    [Fact]
    public void Trace_DoesNotBlockCallers_AndReportsDroppedLines()
    {
        var lines = new List<string>();
        var old = UsbStickTrace.Sink;
        UsbStickTrace.Sink = line => { Thread.Sleep(1); lock (lines) { lines.Add(line); } };   // very slow "debugger"
        try
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < UsbStickTrace.QueueCapacity * 2; i++)
            {
                UsbStickTrace.Write($"line {i}");
            }

            sw.ElapsedMilliseconds.Should().BeLessThan(1000, "enqueueing must not wait for the sink");
            // Other tests trace concurrently into the same queue, so only the overflow report is awaited.
            SpinWait.SpinUntil(() => { lock (lines) { return lines.Any(l => l.Contains("overflow")); } }, TimeSpan.FromSeconds(30)).Should().BeTrue();
            lock (lines)
            {
                lines.Should().Contain(l => l.Contains("[USBSTICK]") && l.Contains(" t") && l.Contains("line "));
            }
        }
        finally
        {
            UsbStickTrace.Sink = old;
        }
    }
}
