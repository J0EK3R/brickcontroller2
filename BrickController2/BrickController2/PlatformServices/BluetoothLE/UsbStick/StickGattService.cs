// Implementations of the platform independent GATT tree interfaces on top of the handles the stick reports.

using System;
using System.Collections.Generic;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Characteristic found by the stick's DISCOVER. Besides the UUID BC2 uses, it carries the handles the stick
/// protocol addresses (value handle for read/write, CCCD handle for SUBSCRIBE) and the ATT property bits.
/// </summary>
internal sealed class StickGattCharacteristic : IGattCharacteristic
{
    /// <summary>ATT characteristic property: write without response.</summary>
    public const byte PropertyWriteWithoutResponse = 0x04;

    /// <summary>ATT characteristic property: notify.</summary>
    public const byte PropertyNotify = 0x10;

    /// <summary>ATT characteristic property: indicate.</summary>
    public const byte PropertyIndicate = 0x20;

    public StickGattCharacteristic(Guid uuid, ushort valueHandle, ushort cccdHandle, byte properties)
    {
        Uuid = uuid;
        ValueHandle = valueHandle;
        CccdHandle = cccdHandle;
        Properties = properties;
    }

    /// <inheritdoc />
    public Guid Uuid { get; }

    /// <summary>Handle used for READ / WRITE / WRITE_CMD and reported in EVT_NOTIFY.</summary>
    public ushort ValueHandle { get; }

    /// <summary>Handle of the client characteristic configuration descriptor (0 = the characteristic has none).</summary>
    public ushort CccdHandle { get; }

    /// <summary>ATT properties byte of the characteristic declaration.</summary>
    public byte Properties { get; }

    /// <summary>True when notifications can be enabled (notify property and a CCCD), like the WinUI device requires.</summary>
    public bool CanNotify => (Properties & PropertyNotify) != 0 && CccdHandle != 0;
}

/// <summary>
/// Primary service found by DISCOVER with its characteristics (assigned by handle range).
/// </summary>
internal sealed class StickGattService : IGattService
{
    public StickGattService(Guid uuid, ushort startHandle, ushort endHandle, IReadOnlyList<StickGattCharacteristic> characteristics)
    {
        Uuid = uuid;
        StartHandle = startHandle;
        EndHandle = endHandle;
        Characteristics = characteristics;
    }

    /// <inheritdoc />
    public Guid Uuid { get; }

    /// <summary>First handle of the service.</summary>
    public ushort StartHandle { get; }

    /// <summary>Last handle of the service.</summary>
    public ushort EndHandle { get; }

    /// <inheritdoc cref="IGattService.Characteristics" />
    public IReadOnlyList<StickGattCharacteristic> Characteristics { get; }

    IEnumerable<IGattCharacteristic> IGattService.Characteristics => Characteristics;
}
