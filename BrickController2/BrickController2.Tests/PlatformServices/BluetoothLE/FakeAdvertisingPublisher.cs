// Test doubles for IAdvertisingPublisher / IAdvertisingPublisherFactory used by the
// AdvertisingPublisherCoordinator tests. Behaviour (delays, never-start, abort) is controllable.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BrickController2.PlatformServices.BluetoothLE;

namespace BrickController2.Tests.PlatformServices.BluetoothLE;

/// <summary>Controllable fake publisher; status changes happen after configurable delays.</summary>
internal sealed class FakeAdvertisingPublisher : IAdvertisingPublisher
{
    private readonly FakeAdvertisingPublisherFactory _factory;

    public FakeAdvertisingPublisher(FakeAdvertisingPublisherFactory factory, int id, byte[] data)
    {
        _factory = factory;
        Id = id;
        Data = data;
    }

    private volatile bool _stopRequested;

    public int Id { get; }
    public byte[] Data { get; set; }
    public bool Disposed { get; private set; }
    public AdvertisingPublisherStatus Status { get; private set; } = AdvertisingPublisherStatus.Created;
    public event Action<AdvertisingPublisherStatus>? StatusChanged;

    public void Start()
    {
        _factory.Log($"start#{Id}");
        if (_factory.AbortOnStartWhenOthersRunning && _factory.RunningCount > 0)
        {
            Set(AdvertisingPublisherStatus.Aborted);
        }
        else if (_factory.AbortAllStarts)
        {
            Set(AdvertisingPublisherStatus.Aborted);
        }
        else if (!_factory.NeverStart)
        {
            Transition(AdvertisingPublisherStatus.Started, _factory.StartDelayMs);
        }
    }

    public void Stop()
    {
        _factory.Log($"stop#{Id}");
        _stopRequested = true; // a pending delayed Start must not override the stop (like a real stack)
        Transition(AdvertisingPublisherStatus.Stopped, _factory.StopDelayMs);
    }

    public void Dispose() => Disposed = true;

    /// <summary>Simulates the stack aborting the publisher on its own.</summary>
    public void Abort() => Set(AdvertisingPublisherStatus.Aborted);

    private void Transition(AdvertisingPublisherStatus target, int delayMs)
    {
        if (delayMs <= 0)
        {
            Set(target);
            return;
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(delayMs);
            if (target == AdvertisingPublisherStatus.Started && _stopRequested)
            {
                return;
            }

            Set(target);
        });
    }

    private void Set(AdvertisingPublisherStatus status)
    {
        Status = status;
        _factory.Log($"{status.ToString().ToLowerInvariant()}#{Id}");
        StatusChanged?.Invoke(status);
    }
}

/// <summary>Factory that records every created publisher and a global ordered event log.</summary>
internal sealed class FakeAdvertisingPublisherFactory : IAdvertisingPublisherFactory
{
    private readonly object _lock = new();
    private readonly List<string> _events = new();
    private readonly List<FakeAdvertisingPublisher> _publishers = new();

    public int StartDelayMs { get; set; }
    public int StopDelayMs { get; set; }
    public bool NeverStart { get; set; }
    public bool AbortAllStarts { get; set; }
    public bool AbortOnStartWhenOthersRunning { get; set; }
    public bool InPlaceSupported { get; set; }
    public int InPlaceCalls { get; private set; }

    public IReadOnlyList<FakeAdvertisingPublisher> Publishers { get { lock (_lock) { return _publishers.ToList(); } } }
    public IReadOnlyList<string> Events { get { lock (_lock) { return _events.ToList(); } } }

    /// <summary>Number of publishers currently Started or Stopping.</summary>
    public int RunningCount => Publishers.Count(p => p.Status is AdvertisingPublisherStatus.Started or AdvertisingPublisherStatus.Stopping);

    public void Log(string text) { lock (_lock) { _events.Add(text); } }

    public IAdvertisingPublisher Create(ushort manufacturerId, byte[] rawData, AdvertisingInterval interval, TxPowerLevel power)
    {
        lock (_lock)
        {
            var publisher = new FakeAdvertisingPublisher(this, _publishers.Count + 1, rawData);
            _publishers.Add(publisher);
            return publisher;
        }
    }

    public bool TryUpdateInPlace(IAdvertisingPublisher publisher, ushort manufacturerId, byte[] rawData)
    {
        InPlaceCalls++;
        if (!InPlaceSupported)
        {
            return false;
        }

        ((FakeAdvertisingPublisher)publisher).Data = rawData;
        return true;
    }
}
