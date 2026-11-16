using System;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Maps the BC2 advertising enums to the numeric values of the stick protocol (protocol.md section 11).
/// </summary>
internal static class StickParameterMapper
{
    /// <summary>
    /// Interval in 0.625 ms units: Min 20 ms, Low 100 ms, Medium 250 ms, High 1 s, Max 10.24 s.
    /// </summary>
    public static ushort ToInterval(AdvertisingInterval interval) => interval switch
    {
        AdvertisingInterval.Min => 32,
        AdvertisingInterval.Low => 160,
        AdvertisingInterval.Medium => 400,
        AdvertisingInterval.High => 1600,
        AdvertisingInterval.Max => 16384,
        _ => throw new ArgumentOutOfRangeException(nameof(interval), interval, null),
    };

    /// <summary>
    /// TX power in dBm; <see cref="TxPowerLevel.Max"/> maps to 127 ("maximum supported by the controller").
    /// The firmware clamps the other values to the range of the controller.
    /// </summary>
    public static sbyte ToTxPower(TxPowerLevel level) => level switch
    {
        TxPowerLevel.Min => -20,
        TxPowerLevel.UltraLow => -12,
        TxPowerLevel.Low => -6,
        TxPowerLevel.Medium => 0,
        TxPowerLevel.High => 9,
        TxPowerLevel.Max => StickProtocol.TxPowerMax,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
    };

    /// <summary>Clamps an interval into the range the stick reported in HELLO_RSP.</summary>
    public static ushort ClampInterval(ushort interval, StickInfo? info)
        => info == null ? interval : Math.Clamp(interval, info.MinInterval, info.MaxInterval);
}
