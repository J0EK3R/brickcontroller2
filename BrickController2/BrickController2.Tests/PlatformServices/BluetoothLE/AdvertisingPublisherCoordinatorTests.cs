// Unit tests for AdvertisingPublisherCoordinator (coalescing, strategies, timeouts, recovery).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BrickController2.PlatformServices.BluetoothLE;
using FluentAssertions;
using Xunit;

namespace BrickController2.Tests.PlatformServices.BluetoothLE;

public class AdvertisingPublisherCoordinatorTests
{
    private readonly FakeAdvertisingPublisherFactory _factory = new();

    private AdvertisingPublisherCoordinator Create(
        AdvertisingUpdateStrategy strategy = AdvertisingUpdateStrategy.MakeBeforeBreak,
        int statusTimeoutMs = 300,
        int minSwapMs = 0)
        => new(_factory, strategy, TimeSpan.FromMilliseconds(statusTimeoutMs), TimeSpan.FromMilliseconds(minSwapMs));

    private static byte[] Data(int value) => [(byte)value, (byte)(value >> 8)];

    /// <summary>Data of the newest publisher that is Started.</summary>
    private byte[]? OnAirData() => _factory.Publishers.LastOrDefault(p => p.Status == AdvertisingPublisherStatus.Started)?.Data;

    [Fact]
    public async Task StartAsync_CreatesOnePublisherAndAwaitsStarted()
    {
        using var sut = Create();
        _factory.StartDelayMs = 50;

        await sut.StartAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 0x1234, Data(1));

        _factory.Publishers.Should().HaveCount(1);
        _factory.Publishers[0].Status.Should().Be(AdvertisingPublisherStatus.Started);
        _factory.Publishers[0].Data.Should().Equal(Data(1));
    }

    [Fact]
    public async Task UpdateAsync_ManyRapidUpdates_AreCoalescedAndLastPayloadWins()
    {
        using var sut = Create(minSwapMs: 20);
        _factory.StartDelayMs = 10;
        _factory.StopDelayMs = 10;
        await sut.StartAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 1, Data(0));

        var tasks = Enumerable.Range(1, 100).Select(i => sut.UpdateAsync(1, Data(i))).ToArray();
        await Task.WhenAll(tasks);

        _factory.Publishers.Count.Should().BeLessThan(20, "100 updates must collapse into few swaps");
        OnAirData().Should().Equal(Data(100));
    }

    [Fact]
    public async Task UpdateAsync_PayloadNeverRegresses()
    {
        using var sut = Create(minSwapMs: 10);
        await sut.StartAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 1, Data(0));

        for (int i = 1; i <= 30; i++)
        {
            _ = sut.UpdateAsync(1, Data(i));
            await Task.Delay(3);
        }

        await sut.UpdateAsync(1, Data(31));

        // publishers are created in payload order: their data values must be monotonically increasing
        var values = _factory.Publishers.Select(p => p.Data[0]).ToList();
        values.Should().BeInAscendingOrder();
        values.Last().Should().Be(31);
    }

    [Fact]
    public async Task MakeBeforeBreak_StopsOldOnlyAfterNewStarted_NeverZeroOnAir()
    {
        using var sut = Create();
        _factory.StartDelayMs = 30;
        await sut.StartAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 1, Data(1));

        await sut.UpdateAsync(1, Data(2));

        var events = _factory.Events.ToList();
        events.IndexOf("started#2").Should().BeLessThan(events.IndexOf("stop#1"));
        // at no time zero Started publishers between the first start and the end of the swap
        events.IndexOf("stopped#1").Should().BeGreaterThan(events.IndexOf("started#2"));
    }

    [Fact]
    public async Task MakeBeforeBreak_FallsBackToStopStart_WhenSecondPublisherAborts()
    {
        using var sut = Create();
        _factory.AbortOnStartWhenOthersRunning = true;
        await sut.StartAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 1, Data(1));

        await sut.UpdateAsync(1, Data(2));
        await sut.UpdateAsync(1, Data(3));

        OnAirData().Should().Equal(Data(3));
        _factory.RunningCount.Should().Be(1);
        // after the degradation the second swap must not try make-before-break again:
        // stop#N always precedes start#N+1
        var events = _factory.Events.ToList();
        events.IndexOf("stopped#3").Should().BeLessThan(events.IndexOf("start#4"));
    }

    [Fact]
    public async Task StopStartAwaited_StartsNewOnlyAfterOldStopped()
    {
        using var sut = Create(AdvertisingUpdateStrategy.StopStartAwaited);
        _factory.StopDelayMs = 60;
        await sut.StartAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 1, Data(1));

        await sut.UpdateAsync(1, Data(2));

        var events = _factory.Events.ToList();
        events.IndexOf("stopped#1").Should().BeLessThan(events.IndexOf("start#2"));
        OnAirData().Should().Equal(Data(2));
    }

    [Fact]
    public async Task InPlace_WhenSupported_CreatesNoNewPublisher()
    {
        using var sut = Create(AdvertisingUpdateStrategy.InPlace);
        _factory.InPlaceSupported = true;
        await sut.StartAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 1, Data(1));

        await sut.UpdateAsync(1, Data(2));

        _factory.Publishers.Should().HaveCount(1);
        _factory.Publishers[0].Data.Should().Equal(Data(2));
    }

    [Fact]
    public async Task InPlace_WhenRejected_FallsBackToSwap()
    {
        using var sut = Create(AdvertisingUpdateStrategy.InPlace);
        _factory.InPlaceSupported = false;
        await sut.StartAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 1, Data(1));

        await sut.UpdateAsync(1, Data(2));

        _factory.InPlaceCalls.Should().Be(1);
        _factory.Publishers.Should().HaveCount(2);
        OnAirData().Should().Equal(Data(2));
    }

    [Fact]
    public async Task StartAsync_PublisherNeverReportsStarted_CompletesAfterTimeout()
    {
        using var sut = Create(statusTimeoutMs: 150);
        _factory.NeverStart = true;

        var watch = System.Diagnostics.Stopwatch.StartNew();
        await sut.StartAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 1, Data(1));

        watch.ElapsedMilliseconds.Should().BeInRange(120, 1500);
    }

    [Fact]
    public async Task UpdateAsync_NewPublisherNeverStarts_DoesNotHang()
    {
        using var sut = Create(statusTimeoutMs: 100);
        await sut.StartAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 1, Data(1));
        _factory.NeverStart = true;

        var update = sut.UpdateAsync(1, Data(2));

        (await Task.WhenAny(update, Task.Delay(3000))).Should().BeSameAs(update);
    }

    [Fact]
    public async Task AbortedCurrentPublisher_IsRecreatedAutomatically()
    {
        using var sut = Create();
        await sut.StartAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 1, Data(7));

        _factory.Publishers[0].Abort();

        await WaitUntilAsync(() => _factory.Publishers.Count >= 2 && OnAirData() != null);
        OnAirData().Should().Equal(Data(7));
    }

    [Fact]
    public async Task AbortedCurrentPublisher_IsRecreatedByNextUpdate()
    {
        using var sut = Create();
        await sut.StartAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 1, Data(1));
        _factory.AbortAllStarts = true; // automatic restarts fail too
        _factory.Publishers[0].Abort();
        await Task.Delay(1200); // let the bounded automatic retries run out
        _factory.AbortAllStarts = false;

        await sut.UpdateAsync(1, Data(9));

        OnAirData().Should().Equal(Data(9));
    }

    [Fact]
    public async Task StopAsync_StopsPublisher_IsIdempotent_AndUpdateAfterStopIsNoOp()
    {
        using var sut = Create();
        await sut.StartAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 1, Data(1));

        await sut.StopAsync();
        await sut.StopAsync();
        await sut.UpdateAsync(1, Data(2));

        _factory.Publishers.Should().HaveCount(1);
        _factory.RunningCount.Should().Be(0);
        _factory.Publishers[0].Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task Dispose_DuringInFlightSwap_DoesNotThrowAndLeavesNothingOnAir()
    {
        var sut = Create();
        _factory.StartDelayMs = 100;
        await sut.StartAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 1, Data(1));
        var update = sut.UpdateAsync(1, Data(2));
        await Task.Delay(30);

        var act = () => sut.Dispose();

        act.Should().NotThrow();
        await update; // must complete, not hang
        await Task.Delay(300);
        _factory.RunningCount.Should().Be(0);
    }

    [Fact]
    public async Task MinSwapInterval_IsRespected()
    {
        using var sut = Create(minSwapMs: 150);
        await sut.StartAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 1, Data(0));
        await sut.UpdateAsync(1, Data(1)); // first swap: no wait needed

        var watch = System.Diagnostics.Stopwatch.StartNew();
        await sut.UpdateAsync(1, Data(2));

        watch.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(120);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && watch.ElapsedMilliseconds < timeoutMs)
        {
            await Task.Delay(10);
        }

        condition().Should().BeTrue("condition should become true within {0} ms", timeoutMs);
    }

    [Fact]
    public async Task StartAsync_WhenPublisherStartThrows_DisposesPublisherAndRethrows()
    {
        using var sut = Create();
        _factory.ThrowOnStart = true;

        var act = () => sut.StartAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 1, Data(1));

        await act.Should().ThrowAsync<InvalidOperationException>();
        _factory.Publishers.Should().HaveCount(1);
        _factory.Publishers[0].Disposed.Should().BeTrue();
        (await sut.UpdateAsync(1, Data(2)).WaitAsync(TimeSpan.FromSeconds(2)).ContinueWith(t => t.IsCompletedSuccessfully))
            .Should().BeTrue("a failed start leaves the coordinator inactive and updates are no-ops");
        _factory.Publishers.Should().HaveCount(1);
    }

    [Fact]
    public async Task UpdateAsync_WhileFirstPublisherStarts_DoesNotLeakPublisher()
    {
        using var sut = Create(minSwapMs: 0);
        _factory.StartDelayMs = 100;
        _factory.StopDelayMs = 10;

        var start = sut.StartAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 1, Data(1));
        await Task.Delay(30); // first publisher is created but not yet Started
        var update = sut.UpdateAsync(1, Data(2));
        await Task.WhenAll(start, update).WaitAsync(TimeSpan.FromSeconds(3));

        // exactly one publisher stays on air and it carries the newer payload
        _factory.Publishers.Count(p => p.Status == AdvertisingPublisherStatus.Started).Should().Be(1);
        OnAirData().Should().Equal(Data(2));
    }

    [Fact]
    public async Task MakeBeforeBreak_SecondPublisherOnlyTimesOut_DoesNotDowngradePermanently()
    {
        using var sut = Create(statusTimeoutMs: 100);
        await sut.StartAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 1, Data(0));

        _factory.NeverStart = true; // second publisher only times out, it does not abort
        _factory.StopDelayMs = 0;
        await sut.UpdateAsync(1, Data(1));
        _factory.NeverStart = false;
        _factory.Log("recovered");

        await sut.UpdateAsync(1, Data(2));

        // after recovery the swap is make-before-break again: new started before the old one is stopped
        var events = _factory.Events.SkipWhile(e => e != "recovered").ToList();
        var lastId = _factory.Publishers.Last().Id;
        var prevId = lastId - 1;
        events.IndexOf($"started#{lastId}").Should().BeLessThan(events.IndexOf($"stop#{prevId}"));
        OnAirData().Should().Equal(Data(2));
    }
}
