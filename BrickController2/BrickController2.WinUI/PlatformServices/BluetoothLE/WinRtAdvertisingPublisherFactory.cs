// WinRT adapter for the platform independent advertising abstractions:
//  * WinRtAdvertisingPublisher wraps a BluetoothLEAdvertisementPublisher and maps its status,
//  * WinRtAdvertisingPublisherFactory creates and configures such publishers.
// Kept deliberately thin: all state handling lives in AdvertisingPublisherCoordinator (shared, unit tested).

using BrickController2.PlatformServices.BluetoothLE;
using Microsoft.Extensions.Logging;
using System;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Foundation.Metadata;

namespace BrickController2.Windows.PlatformServices.BluetoothLE;

/// <summary>
/// Wraps one WinRT <see cref="BluetoothLEAdvertisementPublisher"/> behind <see cref="IAdvertisingPublisher"/>.
/// </summary>
internal sealed class WinRtAdvertisingPublisher : IAdvertisingPublisher
{
    private readonly BluetoothLEAdvertisementPublisher _publisher;
    private readonly ILogger _logger;

    /// <summary>Creates the wrapper and subscribes to the WinRT status event.</summary>
    public WinRtAdvertisingPublisher(BluetoothLEAdvertisementPublisher publisher, ILogger logger)
    {
        _publisher = publisher;
        _logger = logger;
        _publisher.StatusChanged += OnWinRtStatusChanged;
    }

    /// <inheritdoc />
    public event Action<AdvertisingPublisherStatus>? StatusChanged;

    /// <summary>The wrapped WinRT publisher (needed by the factory for in-place updates).</summary>
    public BluetoothLEAdvertisementPublisher Publisher => _publisher;

    /// <inheritdoc />
    public AdvertisingPublisherStatus Status => Map(_publisher.Status);

    /// <inheritdoc />
    public void Start() => _publisher.Start();

    /// <inheritdoc />
    public void Stop() => _publisher.Stop();

    /// <summary>Unsubscribes from the WinRT event (avoids leaks); the publisher itself has no Dispose.</summary>
    public void Dispose() => _publisher.StatusChanged -= OnWinRtStatusChanged;

    /// <summary>Forwards the WinRT status change as platform independent status; logs the error of an abort.</summary>
    private void OnWinRtStatusChanged(BluetoothLEAdvertisementPublisher sender, BluetoothLEAdvertisementPublisherStatusChangedEventArgs args)
    {
        if (args.Status == BluetoothLEAdvertisementPublisherStatus.Aborted)
        {
            _logger.LogWarning("Advertisement publisher aborted, error {Error}", args.Error);
        }

        StatusChanged?.Invoke(Map(args.Status));
    }

    /// <summary>Maps the WinRT status enum to the shared enum (same member names).</summary>
    private static AdvertisingPublisherStatus Map(BluetoothLEAdvertisementPublisherStatus status) => status switch
    {
        BluetoothLEAdvertisementPublisherStatus.Created => AdvertisingPublisherStatus.Created,
        BluetoothLEAdvertisementPublisherStatus.Waiting => AdvertisingPublisherStatus.Waiting,
        BluetoothLEAdvertisementPublisherStatus.Started => AdvertisingPublisherStatus.Started,
        BluetoothLEAdvertisementPublisherStatus.Stopping => AdvertisingPublisherStatus.Stopping,
        BluetoothLEAdvertisementPublisherStatus.Stopped => AdvertisingPublisherStatus.Stopped,
        _ => AdvertisingPublisherStatus.Aborted,
    };
}

/// <summary>
/// Creates configured WinRT publishers and supports the experimental in-place payload update.
/// </summary>
internal sealed class WinRtAdvertisingPublisherFactory : IAdvertisingPublisherFactory
{
    /// <summary>Highest transmit power (dBm) accepted by the WinRT API (range -127..20).</summary>
    private const short MaxTransmitPowerDbm = 20;

    private readonly ILogger _logger;

    public WinRtAdvertisingPublisherFactory(ILogger logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public IAdvertisingPublisher Create(ushort manufacturerId, byte[] rawData, AdvertisingInterval interval, TxPowerLevel power)
    {
        var advertisement = new BluetoothLEAdvertisement();
        advertisement.ManufacturerData.Add(new BluetoothLEManufacturerData(manufacturerId, rawData.AsBuffer()));

        var publisher = new BluetoothLEAdvertisementPublisher(advertisement);
        ConfigureOptionalProperties(publisher, power);

        // payload content only at trace level (it is sent very frequently)
        _logger.LogTrace("Created publisher, manufacturer 0x{Id:X4}, data {Data}", manufacturerId, Convert.ToHexString(rawData));
        return new WinRtAdvertisingPublisher(publisher, _logger);
    }

    /// <inheritdoc />
    public bool TryUpdateInPlace(IAdvertisingPublisher publisher, ushort manufacturerId, byte[] rawData)
    {
        try
        {
            // The payload of a started publisher is documented as immutable: this may throw or be ignored.
            var data = ((WinRtAdvertisingPublisher)publisher).Publisher.Advertisement.ManufacturerData;
            data.Clear();
            data.Add(new BluetoothLEManufacturerData(manufacturerId, rawData.AsBuffer()));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "In-place advertisement update not possible");
            return false;
        }
    }

    /// <summary>
    /// Requests legacy advertising PDUs (payload is &lt; 31 bytes) and maximum power. Every property is
    /// optional (Windows version / driver dependent), so failures are only logged.
    /// </summary>
    private void ConfigureOptionalProperties(BluetoothLEAdvertisementPublisher publisher, TxPowerLevel power)
    {
        TrySet("UseExtendedAdvertisement", () => publisher.UseExtendedAdvertisement = false);
        TrySet("IsAnonymous", () => publisher.IsAnonymous = false);
        TrySet("IncludeTransmitPowerLevel", () => publisher.IncludeTransmitPowerLevel = false);

        if (power == TxPowerLevel.Max &&
            ApiInformation.IsPropertyPresent(
                "Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementPublisher",
                "PreferredTransmitPowerLevelInDBm"))
        {
            TrySet("PreferredTransmitPowerLevelInDBm", () => publisher.PreferredTransmitPowerLevelInDBm = MaxTransmitPowerDbm);
        }
    }

    /// <summary>Runs a property setter and swallows failures of optional properties.</summary>
    private void TrySet(string name, Action setter)
    {
        try
        {
            setter();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Optional publisher property {Name} not supported", name);
        }
    }
}
