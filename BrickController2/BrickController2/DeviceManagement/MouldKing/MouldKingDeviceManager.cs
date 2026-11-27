using System;
using System.Linq;
using System.Text;
using BrickController2.PlatformServices.BluetoothLE;
using BrickController2.UI.Services.AppIdentifier;

namespace BrickController2.DeviceManagement.MouldKing;

/// <summary>
/// Manager for MouldKing devices
/// </summary>
public class MouldKingDeviceManager : BluetoothDeviceManagerBase,
    IMouldKingDeviceManager
{
    private const int AppIdentifierLength = 2; // MouldKing protocol defines 2 bytes for the app identifier

    /// <summary>
    /// Local name prefixes (case-insensitive) of modules that use the MKH4.0 protocol with four channels.
    /// Further module types (e.g. with six channels) can be added here later.
    /// </summary>
    private static readonly string[] MKH4NamePrefixes = ["YX-01", "YX-05"];

    private readonly ReadOnlyMemory<byte> _appIdentifier;

    public MouldKingDeviceManager(IAppIdentifierService appIdentifierService)
    {
        _appIdentifier = appIdentifierService.GetAppId(AppIdentifierLength);
    }

    public ReadOnlyMemory<byte> GetAppId() => _appIdentifier;

    protected override bool TryGetDeviceByServiceUiid(FoundDevice template, Guid serviceGuid, out FoundDevice device)
    {
        // this is prefered way however UUID might not be available on some platforms
        device = serviceGuid switch
        {
            // MouldKing MKH4.0
            { } when serviceGuid == MKH4.MOULD_KING_HUB_SCAN_SERVICE_UUID && !string.IsNullOrEmpty(template.DeviceName) => template with { DeviceType = DeviceType.MKH4 },

            _ => default
        };

        return device.DeviceType != DeviceType.Unknown;
    }

    /// <summary>
    /// Detects the module by its advertised complete local name, which also works if the advertisement
    /// contains no service UUID (see <see cref="TryGetDeviceByServiceUiid"/>).
    /// </summary>
    protected override bool TryGetDeviceByCompleteLocalName(FoundDevice template, ReadOnlySpan<byte> localName, out FoundDevice device)
    {
        // same requirement as the UUID based detection: the device needs a name
        if (!string.IsNullOrEmpty(template.DeviceName) && IsMKH4Name(localName))
        {
            device = template with { DeviceType = DeviceType.MKH4 };
            return true;
        }

        device = default;
        return false;
    }

    /// <summary>
    /// True if the local name (ASCII/UTF-8 bytes) starts with one of the known MKH4.0 prefixes.
    /// </summary>
    internal static bool IsMKH4Name(ReadOnlySpan<byte> localName)
    {
        if (localName.IsEmpty)
        {
            return false;
        }

        var name = Encoding.UTF8.GetString(localName);
        return MKH4NamePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    protected override bool TryGetDeviceByManufacturerData(ScanResult scanResult,
        FoundDevice template,
        ushort manufacturerId,
        ReadOnlySpan<byte> manufacturerData,
        out FoundDevice device)
    {
        switch (manufacturerId)
        {
            case 0xac33:
                device = template with { DeviceType = DeviceType.MK_DIY };
                return true;
        }

        // no match
        device = default;
        return false;
    }
}
