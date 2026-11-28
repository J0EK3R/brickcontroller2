// Abstraction over "Windows tells us that a USB device with the stick's identity was plugged in".
// The real implementation (WinRT DeviceWatcher) is Windows-only; tests use a fake that raises the event by hand.

using System;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Source of USB arrival notifications for the stick identity (ESP32-S3 USB Serial/JTAG, VID 303A / PID 1001).
/// </summary>
public interface IUsbArrivalWatcher : IDisposable
{
    /// <summary>
    /// Raised (on an arbitrary thread) when a matching device appears after the watcher has started.
    /// Devices that were already present at start are NOT reported.
    /// </summary>
    event Action? Arrived;

    /// <summary>
    /// Raised (on an arbitrary thread) when a matching device is removed (unplugged). Usually arrives about 3 s before
    /// the keep-alive silence would reveal the loss.
    /// </summary>
    event Action? Removed;

    /// <summary>
    /// Starts watching. Calling it twice has no effect.
    /// </summary>
    void Start();
}
