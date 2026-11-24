// Windows-only adapter: reports USB arrivals of the stick through the WinRT DeviceWatcher.
// Excluded from the Linux test project (see BrickController2.Tests.csproj); the logic lives in UsbArrivalProbe.
// It is only an accelerator: the reliable path is the presence poll (UsbStickPresencePoller).

using System;
using BrickController2.Diagnostics;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using Windows.Devices.Enumeration;

namespace BrickController2.Windows.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Watches the COM port interfaces (device interface class GUID_DEVINTERFACE_COMPORT) and reports those that belong
/// to a USB device with VID 303A / PID 1001 (ESP32-S3 USB Serial/JTAG). Devices that exist when the watcher starts
/// are swallowed (until <c>EnumerationCompleted</c>); only devices that are plugged in afterwards raise
/// <see cref="Arrived"/>.
/// <para>
/// Why not <c>SerialDevice.GetDeviceSelectorFromUsbVidPid</c>: the stick is a composite USB device whose COM port
/// lives on interface MI_00; that selector is not guaranteed to match composite interfaces. The generic COM port
/// interface class is always matched, and the VID/PID is filtered in code from the interface id
/// (<c>\\?\USB#VID_303A&amp;PID_1001&amp;MI_00#...</c>).
/// </para>
/// </summary>
internal sealed class WinRtUsbArrivalWatcher : IUsbArrivalWatcher
{
    // Substring of the device interface id that identifies the stick's USB device (case-insensitive compare).
    private const string IdentityFragment = "VID_303A&PID_1001";

    // AQS: enabled device interfaces of the COM port class {86E0D1E0-8089-11D0-9CE4-08003E301F73}.
    private const string Selector =
        "System.Devices.InterfaceClassGuid:=\"{86E0D1E0-8089-11D0-9CE4-08003E301F73}\" AND " +
        "System.Devices.InterfaceEnabled:=System.StructuredQueryType.Boolean#True";

    private readonly object _lock = new();
    private DeviceWatcher? _watcher;
    private bool _initialEnumerationDone;

    /// <inheritdoc />
    public event Action? Arrived;

    /// <inheritdoc />
    public event Action? Removed;

    /// <inheritdoc />
    public void Start()
    {
        lock (_lock)
        {
            if (_watcher != null)
            {
                return;
            }

            UsbStickTrace.Write($"USB watcher: starting, selector: {Selector}");
            _watcher = DeviceInformation.CreateWatcher(Selector);
            _watcher.Added += OnAdded;
            _watcher.Removed += OnRemoved;
            _watcher.EnumerationCompleted += OnEnumerationCompleted;
            _watcher.Stopped += OnStopped;
            _watcher.Start();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_lock)
        {
            if (_watcher == null)
            {
                return;
            }

            _watcher.Added -= OnAdded;
            _watcher.Removed -= OnRemoved;
            _watcher.EnumerationCompleted -= OnEnumerationCompleted;
            _watcher.Stopped -= OnStopped;

            // Stop() throws when the watcher is not in a running state (e.g. already aborted).
            if (_watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
            {
                _watcher.Stop();
            }

            _watcher = null;
        }
    }

    /// <summary>
    /// After this event every further <c>Added</c> is a real plug-in, not an already present device.
    /// </summary>
    private void OnEnumerationCompleted(DeviceWatcher sender, object args)
    {
        lock (_lock)
        {
            _initialEnumerationDone = true;
        }

        UsbStickTrace.Write("USB watcher: initial enumeration completed");
    }

    /// <summary>
    /// A COM port interface appeared; reports it when it is the stick and not part of the initial enumeration.
    /// </summary>
    private void OnAdded(DeviceWatcher sender, DeviceInformation args)
    {
        if (!IsStick(args.Id))
        {
            return;
        }

        bool initial;
        lock (_lock)
        {
            initial = !_initialEnumerationDone;
        }

        UsbStickTrace.Write($"USB watcher: added {args.Id} (initial={initial})");
        if (!initial)
        {
            Arrived?.Invoke();
        }
    }

    /// <summary>
    /// A COM port interface disappeared; reports it when it is the stick.
    /// </summary>
    private void OnRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        if (IsStick(args.Id))
        {
            UsbStickTrace.Write($"USB watcher: removed {args.Id}");
            Removed?.Invoke();
        }
    }

    /// <summary>
    /// The watcher ended (stopped by us or aborted by the system); traces the final status.
    /// </summary>
    private void OnStopped(DeviceWatcher sender, object args)
    {
        UsbStickTrace.Write($"USB watcher: stopped, status={sender.Status}");
    }

    /// <summary>
    /// True when the device interface id belongs to the stick's USB identity.
    /// </summary>
    private static bool IsStick(string interfaceId) =>
        interfaceId.Contains(IdentityFragment, StringComparison.OrdinalIgnoreCase);
}
