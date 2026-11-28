// Cheap presence poll that finds a (re-)plugged stick without relying on Windows device notifications.
// Windows-free: the connection, the enabled check and the delay are injected, so the logic is unit tested on Linux.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BrickController2.Diagnostics;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Polls (once per second) the list of candidate COM ports - a registry read, no port is opened - and probes the stick
/// through <see cref="UsbStickConnection.ProbeAfterArrivalAsync"/> as soon as a candidate port shows up. The poll runs
/// only while it is useful: the stick feature is enabled, no session is open, and either a settings row is watching
/// (<see cref="BeginWatching"/>) or the stick is <see cref="UsbStickState.Lost"/>. When nothing needs the stick the
/// poll costs nothing (no timer, no task).
/// <para>
/// Probing happens when the set of candidate ports changed since the last probe (new port appeared) and, for a
/// candidate that stays present but did not answer (e.g. the chip is still booting), again after a retry interval.
/// </para>
/// </summary>
public sealed class UsbStickPresencePoller : IDisposable
{
    /// <summary>
    /// Default time between two port list reads.
    /// </summary>
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Default time after which an unchanged but unanswered candidate list is probed again.
    /// </summary>
    public static readonly TimeSpan DefaultRetryInterval = TimeSpan.FromSeconds(5);

    private readonly UsbStickConnection _connection;
    private readonly Func<bool> _isEnabled;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _retryInterval;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _lock = new();

    private int _watchers;          // number of open BeginWatching handles
    private bool _running;          // true while a poll loop task is active
    private bool _disposed;

    /// <param name="connection">The stick connection: provides the port list, the state and the probe.</param>
    /// <param name="isEnabled">True when the settings allow the stick to be used.</param>
    /// <param name="delay">Waits for the given time (injected so tests do not sleep); defaults to Task.Delay.</param>
    /// <param name="pollInterval">Time between port list reads; defaults to <see cref="DefaultPollInterval"/>.</param>
    /// <param name="retryInterval">Time before an unanswered, unchanged candidate list is probed again.</param>
    public UsbStickPresencePoller(
        UsbStickConnection connection,
        Func<bool> isEnabled,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        TimeSpan? pollInterval = null,
        TimeSpan? retryInterval = null)
    {
        _connection = connection;
        _isEnabled = isEnabled;
        _delay = delay ?? Task.Delay;
        _pollInterval = pollInterval ?? DefaultPollInterval;
        _retryInterval = retryInterval ?? DefaultRetryInterval;

        // The Lost state (also the loss of a connected stick) starts the poll without any watcher.
        _connection.StatusChanged += OnStatusChanged;
    }

    /// <summary>
    /// Requests polling until the returned handle is disposed (e.g. while the settings page is open).
    /// </summary>
    public IDisposable BeginWatching()
    {
        lock (_lock)
        {
            _watchers++;
        }

        EnsureRunning();
        return new Watch(this);
    }

    /// <summary>
    /// Stops the poll and unsubscribes from the connection.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _connection.StatusChanged -= OnStatusChanged;
        _cts.Cancel();
        _cts.Dispose();
    }

    /// <summary>
    /// The connection state changed (e.g. to Lost): start the poll if it is needed now.
    /// </summary>
    private void OnStatusChanged(object? sender, EventArgs e) => EnsureRunning();

    /// <summary>
    /// True when polling is useful right now.
    /// </summary>
    private bool IsWanted()
    {
        UsbStickState state = _connection.Status.State;
        bool watched;
        lock (_lock)
        {
            watched = _watchers > 0;
        }

        return !_disposed && _isEnabled() && state != UsbStickState.Connected && (watched || state == UsbStickState.Lost);
    }

    /// <summary>
    /// Starts the poll loop unless it already runs or is not needed.
    /// </summary>
    private void EnsureRunning()
    {
        lock (_lock)
        {
            if (_running || _disposed || !IsWanted())
            {
                return;
            }

            _running = true;
        }

        // Task.Run keeps callers (UI thread, StatusChanged raisers) free of the first port list read.
        _ = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    /// <summary>
    /// Reads the port list every interval and probes when a new candidate appeared; ends when polling is not needed.
    /// </summary>
    private async Task PollLoopAsync(CancellationToken token)
    {
        string[] lastProbed = Array.Empty<string>();
        long ticksSinceProbe = long.MaxValue;
        long retryTicks = Math.Max(1, (long)Math.Ceiling(_retryInterval / _pollInterval));

        try
        {
            while (!token.IsCancellationRequested && IsWanted())
            {
                string[] ports = _connection.ListCandidatePortNames().OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
                bool changed = !ports.SequenceEqual(lastProbed, StringComparer.OrdinalIgnoreCase);

                if (ports.Length == 0)
                {
                    // Nothing plugged in: forget the old list so the next arrival counts as a change.
                    lastProbed = ports;
                }
                else if (changed || ticksSinceProbe >= retryTicks)
                {
                    UsbStickTrace.Write($"presence poll: candidate port(s) {string.Join(",", ports)} -> probing");
                    lastProbed = ports;
                    ticksSinceProbe = 0;
                    await ProbeAsync(token).ConfigureAwait(false);
                }
                else
                {
                    ticksSinceProbe++;
                }

                await _delay(_pollInterval, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Disposed while waiting.
        }
        finally
        {
            lock (_lock)
            {
                _running = false;
            }
        }

        // A watcher or state change that arrived while the loop was ending must not be lost.
        if (!token.IsCancellationRequested)
        {
            EnsureRunning();
        }
    }

    /// <summary>
    /// One probe; an unexpected error counts as "no answer" so the poll continues.
    /// </summary>
    private async Task ProbeAsync(CancellationToken token)
    {
        try
        {
            bool connected = await _connection.ProbeAfterArrivalAsync(token).ConfigureAwait(false);
            UsbStickTrace.Write($"presence poll: probe {(connected ? "connected" : "no answer")}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            UsbStickTrace.Write($"presence poll: probe failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle returned by <see cref="BeginWatching"/>; disposing it ends that watch (idempotent).
    /// </summary>
    private sealed class Watch : IDisposable
    {
        private UsbStickPresencePoller? _owner;

        public Watch(UsbStickPresencePoller owner) => _owner = owner;

        public void Dispose()
        {
            UsbStickPresencePoller? owner = Interlocked.Exchange(ref _owner, null);
            if (owner == null)
            {
                return;
            }

            lock (owner._lock)
            {
                owner._watchers--;
            }
        }
    }
}
