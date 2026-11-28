using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// <see cref="IBluetoothLEAdvertiserDevice"/> that advertises through the USB stick instead of the
/// operating system's Bluetooth stack. One instance owns one set (lease) of the stick.
/// Mapping (protocol.md section 11): Start = SET_START, Update = SET_UPDATE, Stop = SET_STOP.
/// </summary>
public sealed class StickAdvertiserDevice : IBluetoothLEAdvertiserDevice
{
    private readonly StickSetLease _lease;
    private readonly ILogger _logger;

    public StickAdvertiserDevice(StickSetLease lease, ILogger logger)
    {
        _lease = lease;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAdvertiseAsync(AdvertisingInterval advertisingIterval, TxPowerLevel txPowerLevel, ushort manufacturerId, byte[] rawData)
    {
        if (!StickPayloadNormalizer.TryNormalize(manufacturerId, rawData, out var data))
        {
            _logger.LogError("USB stick: payload of {Length} bytes does not fit (max {Max}), not advertising", rawData.Length, StickProtocol.MaxDataWithFlags);
            return;
        }

        ushort interval = StickParameterMapper.ClampInterval(StickParameterMapper.ToInterval(advertisingIterval), _lease.Info);
        await _lease.StartAsync(interval, StickParameterMapper.ToTxPower(txPowerLevel), manufacturerId, data).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateAdvertisedDataAsync(ushort manufacturerId, byte[] rawData)
    {
        if (!StickPayloadNormalizer.TryNormalize(manufacturerId, rawData, out var data))
        {
            _logger.LogError("USB stick: payload of {Length} bytes does not fit (max {Max}), update ignored", rawData.Length, StickProtocol.MaxDataWithFlags);
            return;
        }

        await _lease.UpdateAsync(manufacturerId, data).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task StopAdvertiseAsync() => _lease.StopAsync();

    /// <summary>
    /// Stops the set and releases the set id for other devices.
    /// </summary>
    public void Dispose() => _lease.Dispose();
}
