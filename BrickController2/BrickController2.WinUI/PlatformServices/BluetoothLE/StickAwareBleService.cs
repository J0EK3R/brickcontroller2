using BrickController2.PlatformServices.BluetoothLE.UsbStick;

namespace BrickController2.Windows.PlatformServices.BluetoothLE;

/// <summary>
/// Windows decorator around the Windows <see cref="BleService"/>. All decisions (stick priority for scanning
/// and GATT devices, overflow to Windows, "Bluetooth on" check, advertiser redirect) live in the shared, unit
/// tested <see cref="GattAwareBleService"/>; this class only gives it the Windows Bluetooth service and the
/// constructor Autofac resolves.
/// </summary>
internal sealed class StickAwareBleService : GattAwareBleService
{
    public StickAwareBleService(BleService inner, UsbAdvertiserService usbAdvertiser, UsbGattService usbGatt)
        : base(inner, usbAdvertiser, usbGatt)
    {
    }
}
