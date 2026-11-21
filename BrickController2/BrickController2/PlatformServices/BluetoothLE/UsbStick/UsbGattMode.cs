// User setting and pure policy function that decide which Bluetooth path serves scanning and GATT devices
// when Windows Bluetooth and the USB stick may both be available.

using System;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// User setting "Bluetooth LE devices (GATT)": which path scans for and connects to GATT devices.
/// Independent of <see cref="UsbAdvertiserMode"/> (advertising only).
/// </summary>
public enum UsbGattMode
{
    /// <summary>The stick has priority whenever it is plugged in (also with Windows Bluetooth on), otherwise Windows Bluetooth.</summary>
    Auto,

    /// <summary>Always Windows Bluetooth; the stick is never used for scanning or GATT.</summary>
    WindowsOnly,

    /// <summary>Only the stick; without a (GATT capable) stick scanning and connecting fail cleanly.</summary>
    StickOnly,
}

/// <summary>The path chosen for one scan or one device connection.</summary>
public enum GattPath
{
    /// <summary>Nothing can serve the request (scan returns false, no device object).</summary>
    None,

    /// <summary>The Windows Bluetooth stack.</summary>
    Windows,

    /// <summary>The USB stick.</summary>
    Stick,
}

/// <summary>
/// The policy as a pure function, decided per scan and per device connect (never live for an existing connection).
/// </summary>
public static class GattPathSelector
{
    /// <summary>
    /// Chooses the path.
    /// <list type="bullet">
    /// <item><b>Auto</b>: a stick that is available has priority, even with the Windows radio on. If the stick has no
    /// free connection slot the device overflows to Windows when the radio is on (otherwise: none). Without stick: Windows if the radio is on.</item>
    /// <item><b>WindowsOnly</b>: Windows if the radio is on, otherwise none.</item>
    /// <item><b>StickOnly</b>: the stick if it is available and has a free slot, otherwise none.</item>
    /// </list>
    /// </summary>
    /// <param name="radioOn">The Windows Bluetooth radio is available and on.</param>
    /// <param name="stickAvailable">A stick is connected (or connectable) and its HELLO_RSP says it can do GATT with at least one slot.</param>
    /// <param name="stickHasFreeSlot">The stick has a free conn_id (scans do not need one: pass true).</param>
    /// <param name="mode">The user setting.</param>
    public static GattPath Choose(bool radioOn, bool stickAvailable, bool stickHasFreeSlot, UsbGattMode mode)
    {
        GattPath windows = radioOn ? GattPath.Windows : GattPath.None;

        return mode switch
        {
            UsbGattMode.WindowsOnly => windows,
            UsbGattMode.StickOnly => stickAvailable && stickHasFreeSlot ? GattPath.Stick : GattPath.None,
            UsbGattMode.Auto when !stickAvailable => windows,
            UsbGattMode.Auto => stickHasFreeSlot ? GattPath.Stick : windows,
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }
}
