using System;
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
