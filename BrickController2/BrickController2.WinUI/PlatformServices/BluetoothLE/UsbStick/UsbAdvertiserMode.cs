namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// User setting for the USB advertising stick.
/// </summary>
public enum UsbAdvertiserMode
{
    /// <summary>
    /// Use the stick when one is plugged in, otherwise the operating system's Bluetooth.
    /// </summary>
    Auto,

    /// <summary>
    /// Never use the stick.
    /// </summary>
    Off,
}

/// <summary>
/// Setting and status of the USB advertising stick, used by the settings page. Only registered on
/// platforms that support the stick (Windows).
/// </summary>
public interface IUsbAdvertiserService
{
    /// <summary>
    /// Selected mode, persisted in the preferences.
    /// </summary>
    UsbAdvertiserMode Mode { get; set; }

    /// <summary>
    /// Current stick state.
    /// </summary>
    UsbStickStatus Status { get; }

    /// <summary>
    /// Raised when <see cref="Status"/> or <see cref="Mode"/> changed.
    /// </summary>
    event System.EventHandler? StatusChanged;

    /// <summary>
    /// Probes for the stick now (ignores the negative cache), e.g. when the settings page opens.
    /// </summary>
    System.Threading.Tasks.Task RefreshAsync();

    /// <summary>
    /// Asks for a cheap periodic presence check of the stick (no port is opened unless a new candidate port appears)
    /// for as long as the returned handle is not disposed, e.g. while the settings page is open. May return null
    /// when no poller exists.
    /// </summary>
    System.IDisposable? BeginWatching();
}
