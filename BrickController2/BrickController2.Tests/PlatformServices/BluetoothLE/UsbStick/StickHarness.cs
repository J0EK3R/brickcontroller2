using System;
using System.Threading.Tasks;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using Microsoft.Extensions.Logging.Abstractions;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Wires a connection, a fake clock, a fake locator and (optionally) a plugged-in fake stick.
/// </summary>
internal sealed class StickHarness : IDisposable
{
    public StickHarness(bool plugged = true, int maxSets = 5, int maxConns = 0)
    {
        Clock = new ManualTimeProvider();
        Locator = new FakeStickLocator();
        Connection = new UsbStickConnection(Locator, NullLogger<UsbStickConnection>.Instance, Clock);

        if (plugged)
        {
            Stick = Plug("COM7", maxSets, maxConns);
        }
    }

    public ManualTimeProvider Clock { get; }

    public FakeStickLocator Locator { get; }

    public UsbStickConnection Connection { get; }

    /// <summary>The stick on COM7 (null if the harness was created unplugged).</summary>
    public FakeStickDevice? Stick { get; private set; }

    /// <summary>Plugs a new stick into the given port and returns it (replaces an older one).</summary>
    public FakeStickDevice Plug(string port, int maxSets = 5, int maxConns = 0)
    {
        var device = new FakeStickDevice(Clock, maxSets, maxConns);
        device.AfterEvent = Connection.WaitForScanCallbacks;
        Locator.Ports[port] = device;
        Stick = device;
        return device;
    }

    /// <summary>Connects and fails the test if no stick answers.</summary>
    public async Task<StickSetLease> ConnectAndLeaseAsync()
    {
        bool connected = await RunAsync(Connection.TryConnectAsync(forceProbe: true));
        if (!connected)
        {
            throw new InvalidOperationException("Stick did not connect.");
        }

        return Connection.TryAcquireLease() ?? throw new InvalidOperationException("No lease.");
    }

    /// <summary>Connects the stick and fails the test if it does not answer (no lease taken).</summary>
    public async Task ConnectStickAsync()
    {
        if (!await RunAsync(Connection.TryConnectAsync(forceProbe: true)))
        {
            throw new InvalidOperationException("Stick did not connect.");
        }
    }

    /// <summary>Connects the stick and reserves a GATT link (conn_id) on it.</summary>
    public async Task<StickGattLink> ConnectAndAcquireLinkAsync()
    {
        await ConnectStickAsync();
        return Connection.TryAcquireLink() ?? throw new InvalidOperationException("No GATT link available.");
    }

    /// <summary>
    /// Advances the fake clock in small steps (letting thread-pool continuations run in between) until the
    /// task completes, then returns its result. Fails after a generous fake-time budget.
    /// </summary>
    public async Task<T> RunAsync<T>(Task<T> task)
    {
        for (int i = 0; i < 3000 && !task.IsCompleted; i++)
        {
            Clock.Advance(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }

        return await task;
    }

    /// <summary>Same as <see cref="RunAsync{T}"/> for tasks without a result.</summary>
    public async Task RunAsync(Task task)
    {
        for (int i = 0; i < 3000 && !task.IsCompleted; i++)
        {
            Clock.Advance(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }

        await task;
    }

    /// <summary>Advances the fake clock by the given time in 10 ms steps, yielding to continuations.</summary>
    public async Task PumpAsync(TimeSpan span)
    {
        for (var elapsed = TimeSpan.Zero; elapsed < span; elapsed += TimeSpan.FromMilliseconds(10))
        {
            Clock.Advance(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }
    }

    /// <summary>Pumps until the condition holds (or the fake-time budget is exhausted) and returns whether it held.</summary>
    public async Task<bool> PumpUntilAsync(Func<bool> condition, TimeSpan? budget = null)
    {
        var limit = budget ?? TimeSpan.FromSeconds(10);
        for (var elapsed = TimeSpan.Zero; elapsed < limit && !condition(); elapsed += TimeSpan.FromMilliseconds(10))
        {
            Clock.Advance(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1);
        }

        return condition();
    }

    public void Dispose() => Connection.Dispose();
}
