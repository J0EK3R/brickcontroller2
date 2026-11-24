using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Deterministic <see cref="TimeProvider"/> for tests: time only moves when <see cref="Advance"/> is called,
/// and timers (also those created by Task.Delay / Task.WaitAsync) fire in due order while advancing.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly object _lock = new();
    private readonly List<ManualTimer> _timers = new();
    private long _now; // in ticks (TimestampFrequency = TimeSpan.TicksPerSecond)

    /// <inheritdoc />
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc />
    public override long GetTimestamp()
    {
        lock (_lock)
        {
            return _now;
        }
    }

    /// <summary>Elapsed fake time since the provider was created.</summary>
    public TimeSpan Now => TimeSpan.FromTicks(GetTimestamp());

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        lock (_lock)
        {
            _timers.Add(timer);
        }

        return timer;
    }

    /// <summary>Moves the clock forward and fires every timer that becomes due, in order.</summary>
    public void Advance(TimeSpan delta)
    {
        long target;
        lock (_lock)
        {
            target = _now + delta.Ticks;
        }

        while (true)
        {
            ManualTimer? next;
            lock (_lock)
            {
                next = _timers.Where(t => t.IsArmed && t.DueTicks <= target).OrderBy(t => t.DueTicks).FirstOrDefault();
                if (next == null)
                {
                    _now = target;
                    return;
                }

                _now = Math.Max(_now, next.DueTicks);
            }

            next.Fire();
        }
    }

    private void Remove(ManualTimer timer)
    {
        lock (_lock)
        {
            _timers.Remove(timer);
        }
    }

    /// <summary>Timer driven by <see cref="ManualTimeProvider.Advance"/>.</summary>
    private sealed class ManualTimer : ITimer
    {
        private readonly ManualTimeProvider _owner;
        private readonly TimerCallback _callback;
        private readonly object? _state;
        private long _periodTicks;

        public ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state)
        {
            _owner = owner;
            _callback = callback;
            _state = state;
        }

        public bool IsArmed { get; private set; }

        public long DueTicks { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime == Timeout.InfiniteTimeSpan)
            {
                IsArmed = false;
                return true;
            }

            DueTicks = _owner.GetTimestamp() + dueTime.Ticks;
            _periodTicks = period == Timeout.InfiniteTimeSpan ? 0 : period.Ticks;
            IsArmed = true;
            return true;
        }

        public void Fire()
        {
            // Re-arm (periodic) or disarm (one-shot) before running the callback, like a real timer.
            if (_periodTicks > 0)
            {
                DueTicks += _periodTicks;
            }
            else
            {
                IsArmed = false;
            }

            _callback(_state);
        }

        public void Dispose()
        {
            IsArmed = false;
            _owner.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
