// WinUI implementation of IBluetoothLEAdvertiserDevice: a thin adapter around the shared
// AdvertisingPublisherCoordinator (task 003-win-advertising).
//
// Why not "stop + new publisher + start" per update (the previous implementation)?
// BluetoothLEAdvertisementPublisher.Stop()/Start() are asynchronous inside the Windows Bluetooth stack.
// The shared output loop calls UpdateAdvertisedDataAsync up to 10 times per second while a joystick moves;
// destroying and re-creating a publisher each time, without waiting for Stopped/Started, queues up work in
// the stack and the data really on air lags seconds behind the input. The coordinator coalesces updates,
// applies them one at a time, awaits the real publisher status and recovers from aborted publishers.

using BrickController2.PlatformServices.BluetoothLE;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace BrickController2.Windows.PlatformServices.BluetoothLE;

internal class BleAdvertiserDevice : IBluetoothLEAdvertiserDevice
{
    /// <summary>
    /// Environment variable to select the update strategy without rebuilding (A/B tests on hardware):
    /// InPlace | MakeBeforeBreak | StopStartAwaited.
    /// </summary>
    internal const string StrategyEnvironmentVariable = "BC2_WIN_ADV_STRATEGY";

    /// <summary>Maximum time to wait for a publisher status change (Started/Stopped).</summary>
    private static readonly TimeSpan StatusTimeout = TimeSpan.FromMilliseconds(300);

    /// <summary>Minimum time between two publisher swaps; intermediate payloads are dropped (latest wins).</summary>
    private static readonly TimeSpan MinSwapInterval = TimeSpan.FromMilliseconds(50);

    private readonly ILogger _logger;
    private readonly AdvertisingPublisherCoordinator _coordinator;

    public BleAdvertiserDevice(ILogger logger)
    {
        _logger = logger;

        var strategy = ReadStrategy();
        _logger.LogInformation("BLE advertising update strategy: {Strategy}", strategy);

        _coordinator = new AdvertisingPublisherCoordinator(
            new WinRtAdvertisingPublisherFactory(logger),
            strategy,
            StatusTimeout,
            MinSwapInterval,
            logger);
    }

    /// <inheritdoc />
    public Task StartAdvertiseAsync(AdvertisingInterval advertisingInterval,
        TxPowerLevel txPowerLevel,
        ushort manufacturerId,
        byte[] rawData)
        => _coordinator.StartAsync(advertisingInterval, txPowerLevel, manufacturerId, rawData);

    /// <inheritdoc />
    public Task StopAdvertiseAsync()
        => _coordinator.StopAsync();

    /// <inheritdoc />
    public Task UpdateAdvertisedDataAsync(ushort manufacturerId, byte[] rawData)
        => _coordinator.UpdateAsync(manufacturerId, rawData);

    /// <inheritdoc />
    public void Dispose()
        => _coordinator.Dispose();

    /// <summary>Reads the strategy override from the environment; default is MakeBeforeBreak.</summary>
    private static AdvertisingUpdateStrategy ReadStrategy()
    {
        var value = Environment.GetEnvironmentVariable(StrategyEnvironmentVariable);
        return Enum.TryParse<AdvertisingUpdateStrategy>(value, ignoreCase: true, out var strategy)
            ? strategy
            : AdvertisingUpdateStrategy.MakeBeforeBreak;
    }
}
