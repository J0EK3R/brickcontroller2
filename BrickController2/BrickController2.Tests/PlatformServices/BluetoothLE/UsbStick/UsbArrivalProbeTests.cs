// Tests of the USB arrival reaction (decision and retry logic) with a fake watcher and an instant delay.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using FluentAssertions;
using Xunit;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Verifies when <see cref="UsbArrivalProbe"/> probes, how often, and that it stops on dispose.
/// </summary>
public class UsbArrivalProbeTests
{
    /// <summary>
    /// Watcher whose arrival event is raised by the test.
    /// </summary>
    private sealed class FakeWatcher : IUsbArrivalWatcher
    {
        public bool Started { get; private set; }
        public bool Disposed { get; private set; }
        public event Action? Arrived;
        public event Action? Removed;
        public void Start() => Started = true;
        public void Dispose() => Disposed = true;
        public void Raise() => Arrived?.Invoke();
        public void RaiseRemoved() => Removed?.Invoke();
        public bool HasSubscribers => Arrived != null;
    }

    /// <summary>
    /// Test rig: records probes, lets the test decide the probe answers and the settings.
    /// </summary>
    private sealed class Rig
    {
        public readonly FakeWatcher Watcher = new();
        public readonly List<TimeSpan> Waits = new();
        public readonly Queue<bool> Answers = new();
        public int Probes;
        public bool Enabled = true;
        public bool Connected;
        public TaskCompletionSource<bool> Done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Func<Task>? BeforeDelay;
        public UsbArrivalProbe Probe;

        public Rig(params bool[] answers)
        {
            foreach (bool a in answers)
            {
                Answers.Enqueue(a);
            }

            Probe = new UsbArrivalProbe(
                Watcher,
                () => Enabled,
                () => Connected,
                _ =>
                {
                    Interlocked.Increment(ref Probes);
                    bool answer = Answers.Count > 0 && Answers.Dequeue();
                    if (answer || Answers.Count == 0)
                    {
                        Done.TrySetResult(answer);
                    }

                    return Task.FromResult(answer);
                },
                async (time, token) =>
                {
                    Waits.Add(time);
                    if (BeforeDelay != null)
                    {
                        await BeforeDelay();
                    }

                    token.ThrowIfCancellationRequested();
                });
        }
    }

    [Fact]
    public void Constructor_StartsWatcher()
    {
        var rig = new Rig();

        rig.Watcher.Started.Should().BeTrue();
    }

    [Fact]
    public async Task Arrival_ForcesProbeAfterShortDelay()
    {
        var rig = new Rig(true);

        rig.Watcher.Raise();
        (await rig.Done.Task.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();

        rig.Probes.Should().Be(1);
        rig.Waits.Should().Equal(TimeSpan.FromMilliseconds(300));
    }

    [Fact]
    public async Task Arrival_RetriesUntilStickAnswers()
    {
        var rig = new Rig(false, false, true);

        rig.Watcher.Raise();
        await rig.Done.Task.WaitAsync(TimeSpan.FromSeconds(5));

        rig.Probes.Should().Be(3);
        rig.Waits.Should().Equal(UsbArrivalProbe.DefaultSchedule);
    }

    [Fact]
    public async Task Arrival_GivesUpAfterAllAttempts()
    {
        var rig = new Rig(false, false, false);

        rig.Watcher.Raise();
        await rig.Done.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50);

        rig.Probes.Should().Be(3, "the schedule has three attempts");
    }

    [Fact]
    public async Task Arrival_ProbeThrows_NextAttemptStillHappens()
    {
        int calls = 0;
        var watcher = new FakeWatcher();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var probe = new UsbArrivalProbe(
            watcher, () => true, () => false,
            _ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    throw new InvalidOperationException("port busy");
                }

                done.SetResult();
                return Task.FromResult(true);
            },
            (_, _) => Task.CompletedTask);

        watcher.Raise();
        await done.Task.WaitAsync(TimeSpan.FromSeconds(5));

        calls.Should().Be(2);
    }

    [Fact]
    public async Task Arrival_AdvertiserAndGattDisabled_DoesNotProbe()
    {
        var rig = new Rig(true) { Enabled = false };

        rig.Watcher.Raise();
        await Task.Delay(100);

        rig.Probes.Should().Be(0);
        rig.Waits.Should().BeEmpty();
    }

    [Fact]
    public async Task Arrival_AlreadyConnected_DoesNotProbe()
    {
        var rig = new Rig(true) { Connected = true };

        rig.Watcher.Raise();
        await Task.Delay(100);

        rig.Probes.Should().Be(0);
    }

    [Fact]
    public async Task Arrival_ConnectedWhileWaiting_StopsWithoutProbe()
    {
        var rig = new Rig(true);
        rig.BeforeDelay = () =>
        {
            rig.Connected = true; // e.g. the user opened the settings page in the meantime
            return Task.CompletedTask;
        };

        rig.Watcher.Raise();
        await Task.Delay(100);

        rig.Probes.Should().Be(0);
    }

    [Fact]
    public async Task Arrival_WhileRunActive_IsCoveredByThatRun()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var rig = new Rig(true) { };
        rig.BeforeDelay = () => gate.Task;

        rig.Watcher.Raise();
        rig.Watcher.Raise();
        gate.SetResult();
        await rig.Done.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50);

        rig.Probes.Should().Be(1);
    }

    [Fact]
    public async Task Dispose_UnsubscribesStopsWatcherAndCancelsPendingRun()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var rig = new Rig(true);
        rig.BeforeDelay = () => gate.Task;

        rig.Watcher.Raise();
        rig.Probe.Dispose();
        gate.SetResult();
        await Task.Delay(100);

        rig.Watcher.Disposed.Should().BeTrue();
        rig.Watcher.HasSubscribers.Should().BeFalse();
        rig.Probes.Should().Be(0, "the pending run is cancelled");

        rig.Watcher.Raise();
        await Task.Delay(50);
        rig.Probes.Should().Be(0);
    }

    [Fact]
    public void Removal_InvokesOnRemovedCallback()
    {
        var watcher = new FakeWatcher();
        int removed = 0;
        using var probe = new UsbArrivalProbe(
            watcher, () => true, () => true, _ => Task.FromResult(true), onRemoved: () => removed++);

        watcher.RaiseRemoved();
        watcher.RaiseRemoved();

        removed.Should().Be(2, "every removal is forwarded; idempotency is the owner's job");
    }

    [Fact]
    public void Removal_ForwardedEvenWhenFeatureDisabled_AndFailingCallbackIsSwallowed()
    {
        var watcher = new FakeWatcher();
        using var probe = new UsbArrivalProbe(
            watcher, () => false, () => false, _ => Task.FromResult(true),
            onRemoved: () => throw new InvalidOperationException("boom"));

        Action act = watcher.RaiseRemoved;

        act.Should().NotThrow();
    }

    [Fact]
    public void Removal_AfterDispose_IsIgnored()
    {
        var watcher = new FakeWatcher();
        int removed = 0;
        var probe = new UsbArrivalProbe(
            watcher, () => true, () => true, _ => Task.FromResult(true), onRemoved: () => removed++);

        probe.Dispose();
        watcher.RaiseRemoved();

        removed.Should().Be(0);
    }

    [Fact]
    public void Arrival_InvokesOnArrivedEvenWhenNoProbeIsNeeded()
    {
        var watcher = new FakeWatcher();
        int arrived = 0;
        using var probe = new UsbArrivalProbe(
            watcher, () => false, () => false, _ => Task.FromResult(true), onArrived: () => arrived++);

        watcher.Raise();

        arrived.Should().Be(1);
    }
}
