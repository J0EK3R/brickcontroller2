// Reacts to a USB arrival of the stick by probing it right away, so that re-plugging the stick is noticed without
// the user having to open the settings page or start a connection. Windows-free: the watcher, the connection and the
// delay are injected, so the decision and retry logic is unit tested on Linux.

using System;
using System.Threading;
using System.Threading.Tasks;
using BrickController2.Diagnostics;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Subscribes to an <see cref="IUsbArrivalWatcher"/> and, when a device arrives, probes the stick with a forced probe
/// (ignoring the negative probe cache). The COM port is often not openable immediately after the arrival, so the
/// probe is retried on a schedule. No probe is made when the stick feature is disabled by the settings or when a
/// session is already open. It also forwards removals: the owner can mark the stick lost at once instead of waiting
/// for the keep-alive silence (the callback is expected to be idempotent with that later detection).
/// </summary>
public sealed class UsbArrivalProbe : IDisposable
{
    /// <summary>
    /// Default waits before the 1st, 2nd and 3rd attempt (about 3 s in total).
    /// </summary>
    public static readonly TimeSpan[] DefaultSchedule =
    [
        TimeSpan.FromMilliseconds(300),
        TimeSpan.FromMilliseconds(1000),
        TimeSpan.FromMilliseconds(1500),
    ];

    private readonly IUsbArrivalWatcher _watcher;
    private readonly Func<bool> _isEnabled;
    private readonly Func<bool> _isConnected;
    private readonly Func<CancellationToken, Task<bool>> _probe;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly TimeSpan[] _schedule;
    private readonly Action? _onArrived;
    private readonly Action? _onRemoved;
    private readonly CancellationTokenSource _cts = new();

    // 1 while a probe run (all attempts of one arrival) is active; further arrivals during that time are covered by it.
    private int _running;
    private bool _disposed;

    /// <param name="watcher">Source of arrival events; started by this constructor.</param>
    /// <param name="isEnabled">True when the settings allow the stick to be used (advertiser not Off or GATT not WindowsOnly).</param>
    /// <param name="isConnected">True when a stick session is already open (nothing to do then).</param>
    /// <param name="probe">Forced probe of the stick; returns true when a session is open afterwards.</param>
    /// <param name="delay">Waits for the given time (injected so tests do not sleep).</param>
    /// <param name="schedule">Wait before each attempt; its length is the number of attempts. Defaults to <see cref="DefaultSchedule"/>.</param>
    /// <param name="onArrived">Called on every arrival before the probe decision (even when no probe is made).</param>
    /// <param name="onRemoved">Called on every removal (e.g. marks the stick session lost); exceptions are swallowed.</param>
    public UsbArrivalProbe(
        IUsbArrivalWatcher watcher,
        Func<bool> isEnabled,
        Func<bool> isConnected,
        Func<CancellationToken, Task<bool>> probe,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        TimeSpan[]? schedule = null,
        Action? onArrived = null,
        Action? onRemoved = null)
    {
        _onArrived = onArrived;
        _onRemoved = onRemoved;
        _watcher = watcher;
        _isEnabled = isEnabled;
        _isConnected = isConnected;
        _probe = probe;
        _delay = delay ?? Task.Delay;
        _schedule = schedule ?? DefaultSchedule;

        _watcher.Arrived += OnArrived;
        _watcher.Removed += OnRemoved;
        _watcher.Start();
    }

    /// <summary>
    /// Stops listening and cancels a probe run that is waiting or running.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _watcher.Arrived -= OnArrived;
        _watcher.Removed -= OnRemoved;
        _cts.Cancel();
        _watcher.Dispose();
        _cts.Dispose();
    }

    /// <summary>
    /// Watcher callback (arbitrary thread): starts one probe run unless one is already active or none is needed.
    /// </summary>
    private void OnArrived()
    {
        if (_disposed)
        {
            return;
        }

        InvokeCallback(_onArrived);
        if (!ShouldProbe())
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            return;
        }

        // Task.Run keeps the watcher thread free and moves the synchronous start of the probe off it.
        _ = Task.Run(() => RunAsync(_cts.Token));
    }

    /// <summary>
    /// Watcher callback (arbitrary thread): forwards the removal to the owner.
    /// </summary>
    private void OnRemoved()
    {
        if (!_disposed)
        {
            InvokeCallback(_onRemoved);
        }
    }

    /// <summary>
    /// Runs an owner callback; a failing callback must not break the watcher thread.
    /// </summary>
    private static void InvokeCallback(Action? callback)
    {
        try
        {
            callback?.Invoke();
        }
        catch (Exception ex)
        {
            UsbStickTrace.Write($"USB watcher callback failed: {ex.Message}");
        }
    }

    /// <summary>
    /// True when the settings allow the stick and no session is open yet.
    /// </summary>
    private bool ShouldProbe() => _isEnabled() && !_isConnected();

    /// <summary>
    /// One probe run: waits, probes, and retries until the stick answers or the attempts are used up.
    /// </summary>
    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            UsbStickTrace.Write("USB arrival detected -> probing");

            for (int attempt = 1; attempt <= _schedule.Length; attempt++)
            {
                await _delay(_schedule[attempt - 1], token).ConfigureAwait(false);

                // The situation may have changed while waiting (setting switched off, another path connected).
                if (!ShouldProbe())
                {
                    return;
                }

                bool connected = await TryProbeAsync(token).ConfigureAwait(false);
                UsbStickTrace.Write($"USB arrival probe attempt {attempt}/{_schedule.Length}: {(connected ? "stick connected" : "no answer")}");
                if (connected)
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Disposed while waiting.
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }
    }

    /// <summary>
    /// Runs one forced probe; an unexpected error counts as "no answer" so the next attempt still happens.
    /// </summary>
    private async Task<bool> TryProbeAsync(CancellationToken token)
    {
        try
        {
            return await _probe(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            UsbStickTrace.Write($"USB arrival probe failed: {ex.Message}");
            return false;
        }
    }
}
