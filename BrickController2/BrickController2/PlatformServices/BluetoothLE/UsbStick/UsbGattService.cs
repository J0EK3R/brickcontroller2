// Glue between the user setting (UsbGattMode), the stick connection and the BC2 services: decides whether a
// scan or a device connection goes through the stick and creates the stick based objects.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BrickController2.UI.Services.Preferences;
using BrickController2.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>Snapshot of the stick's GATT activity for the settings page.</summary>
/// <param name="Supported">A stick is connected and can do GATT.</param>
/// <param name="MaxLinks">Number of connection slots reported by the stick (max_conns).</param>
/// <param name="LinksInUse">Slots currently reserved.</param>
/// <param name="Scanning">A scan through the stick is running.</param>
public sealed record UsbGattStatus(bool Supported, int MaxLinks, int LinksInUse, bool Scanning);

/// <summary>
/// Setting and status of GATT through the stick, used by the settings page. Only registered on platforms that support the stick.
/// </summary>
public interface IUsbGattService
{
    /// <summary>Selected mode, persisted in the preferences (default Auto).</summary>
    UsbGattMode GattMode { get; set; }

    /// <summary>Current links and scan state.</summary>
    UsbGattStatus GattStatus { get; }

    /// <summary>Raised when the mode or the status changed.</summary>
    event EventHandler? StatusChanged;
}

/// <summary>
/// Implements the policy of task 010: a plugged-in stick has priority for scanning and GATT (setting Auto),
/// with Windows Bluetooth as fallback and overflow path. All decisions are made here (and in the pure
/// <see cref="GattPathSelector"/>), so the platform decorator stays thin.
/// </summary>
public sealed class UsbGattService : IUsbGattService
{
    private const string ModePreferenceKey = "UsbGattMode";

    private readonly UsbStickConnection _connection;
    private readonly UsbAdvertiserService _advertiser;
    private readonly StickAddressBook _addressBook;
    private readonly IPreferencesService _preferences;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    public UsbGattService(
        UsbStickConnection connection,
        UsbAdvertiserService advertiser,
        StickAddressBook addressBook,
        IPreferencesService preferences,
        ILoggerFactory loggerFactory)
    {
        _connection = connection;
        _advertiser = advertiser;
        _addressBook = addressBook;
        _preferences = preferences;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<UsbGattService>();
        _connection.StatusChanged += (_, e) => StatusChanged?.Invoke(this, e);
    }

    /// <inheritdoc />
    public event EventHandler? StatusChanged;

    /// <summary>Raised (rarely) only when the stored mode changes, unlike <see cref="StatusChanged"/> which also fires on link/scan activity.</summary>
    public event EventHandler? ModeChanged;

    // Running stick scans; cancelled when the mode no longer allows the stick (see ScanAsync).
    private readonly object _scanLock = new();
    private readonly List<CancellationTokenSource> _policyScans = new();

    /// <inheritdoc />
    public UsbGattMode GattMode
    {
        get => ReadMode();
        set
        {
            if (GattMode == value)
            {
                return;
            }

            _preferences.Set(ModePreferenceKey, value);
            _logger.LogInformation("USB GATT mode set to {Mode}", value);
            ModeChanged?.Invoke(this, EventArgs.Empty);
            StatusChanged?.Invoke(this, EventArgs.Empty);

            if (value == UsbGattMode.WindowsOnly)
            {
                // The stick may no longer serve GATT: give the peers back so they advertise again for Windows.
                _closeAllTask = Task.Run(CloseStickGattAsync);
            }
        }
    }

    // Last teardown after a switch to WindowsOnly (observed by tests; errors are handled inside).
    private Task _closeAllTask = Task.CompletedTask;

    /// <summary>Completes when the teardown started by the last switch to WindowsOnly has finished.</summary>
    internal Task WhenGattClosedAsync() => _closeAllTask;

    /// <summary>
    /// Disconnects all stick GATT links (BC2 devices get their normal disconnect callback and reconnect through
    /// Windows) and stops the stick scans (the scan call returns false so the caller continues on Windows).
    /// </summary>
    private async Task CloseStickGattAsync()
    {
        try
        {
            CancellationTokenSource[] scans;
            lock (_scanLock)
            {
                scans = _policyScans.ToArray();
            }

            foreach (CancellationTokenSource scan in scans)
            {
                scan.Cancel();
            }

            int closed = await _connection.CloseAllGattAsync().ConfigureAwait(false);
            UsbStickTrace.Write($"GATT mode changed to WindowsOnly: {closed} stick link(s) closed, {scans.Length} scan(s) stopped");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Closing the stick GATT links failed: {Message}", ex.Message);
        }
    }

    /// <summary>Reads the stored mode; a corrupted preference value falls back to Auto instead of throwing.</summary>
    private UsbGattMode ReadMode()
    {
        try
        {
            return _preferences.Get(ModePreferenceKey, UsbGattMode.Auto);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException or InvalidCastException)
        {
            _logger.LogWarning("Invalid USB GATT mode preference, using Auto: {Message}", ex.Message);
            return UsbGattMode.Auto;
        }
    }

    /// <inheritdoc />
    public UsbGattStatus GattStatus
        => new(_connection.IsConnected && _connection.MaxLinks > 0, _connection.MaxLinks, _connection.LinkCount, _connection.IsScanning);

    // ------------------------------------------------------------------ availability

    /// <summary>
    /// True when a stick that can do GATT is connected or can be connected right now. The probe runs on the
    /// thread pool (never blocks the UI thread); a failed probe is cached by the connection for 10 s.
    /// </summary>
    public async Task<bool> IsStickGattAvailableAsync()
    {
        if (!await IsStickPresentAsync().ConfigureAwait(false))
        {
            return false;
        }

        return _connection.Info is { SupportsGatt: true };
    }

    /// <summary>True when a stick (of any firmware) is connected or can be connected.</summary>
    private async Task<bool> IsStickPresentAsync()
    {
        try
        {
            return _connection.IsConnected || await Task.Run(() => _connection.TryConnectAsync()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("USB stick availability check failed: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Answers "is Bluetooth usable for BC2?" (no "please enable Bluetooth" prompt needed) using
    /// <see cref="BluetoothAvailability"/>. The stick is only probed when the radio alone does not decide.
    /// </summary>
    /// <param name="radioOn">State of the Windows radio.</param>
    public async Task<bool> IsBluetoothUsableAsync(bool radioOn)
    {
        UsbGattMode mode = GattMode;
        UsbAdvertiserMode advertiserMode = _advertiser.Mode;

        // Radio on is enough unless the user insists on the stick; then nothing needs to be probed.
        if (radioOn && mode != UsbGattMode.StickOnly)
        {
            return true;
        }

        bool present = await IsStickPresentAsync().ConfigureAwait(false);
        bool gattCapable = present && _connection.Info is { SupportsGatt: true };

        // A stick without GATT cannot serve scan/GATT, so "Auto" then means "Windows" for the GATT part.
        UsbGattMode effective = mode == UsbGattMode.Auto && !gattCapable ? UsbGattMode.WindowsOnly : mode;
        bool stickAvailable = mode == UsbGattMode.StickOnly ? gattCapable : present;
        return BluetoothAvailability.IsBluetoothUsable(radioOn, stickAvailable, advertiserMode, effective);
    }

    // ------------------------------------------------------------------ path decisions

    /// <summary>Decides who scans: the stick, Windows or nobody (see <see cref="GattPathSelector"/>).</summary>
    public async Task<GattPath> ChooseScanPathAsync(bool radioOn)
    {
        UsbGattMode mode = GattMode;
        bool stickAvailable = mode != UsbGattMode.WindowsOnly && await IsStickGattAvailableAsync().ConfigureAwait(false);
        GattPath path = GattPathSelector.Choose(radioOn, stickAvailable, stickHasFreeSlot: true, mode);
        return path;
    }

    /// <summary>Decides who serves a device connection (also looks at the free connection slots of the stick).</summary>
    public async Task<GattPath> ChooseDevicePathAsync(bool radioOn)
    {
        UsbGattMode mode = GattMode;
        bool stickAvailable = mode != UsbGattMode.WindowsOnly && await IsStickGattAvailableAsync().ConfigureAwait(false);
        bool freeLink = stickAvailable && _connection.HasFreeLink;
        GattPath path = GattPathSelector.Choose(radioOn, stickAvailable, freeLink, mode);
        UsbStickTrace.Write($"device routed via {path} (mode={mode}, windowsRadioOn={radioOn}, stickGattAvailable={stickAvailable}, stickHasFreeSlot={freeLink})");
        return path;
    }

    // ------------------------------------------------------------------ stick backed objects

    /// <summary>
    /// Scans through the stick until the token is cancelled (like the Windows scan). Returns false when the stick
    /// cannot start the scan, so the caller may fall back to Windows.
    /// </summary>
    public async Task<bool> ScanAsync(Action<ScanResult> scanCallback, CancellationToken token)
    {
        // Linked to the caller's token and to the policy: a switch to WindowsOnly ends the stick scan.
        using var policyScan = CancellationTokenSource.CreateLinkedTokenSource(token);
        lock (_scanLock)
        {
            _policyScans.Add(policyScan);
        }

        var scanner = new StickScanner(_connection, scanCallback, _addressBook, _loggerFactory.CreateLogger<StickScanner>());
        try
        {
            if (!await scanner.StartAsync().ConfigureAwait(false))
            {
                UsbStickTrace.Write("scan through the stick could not be started");
                return false;
            }

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, policyScan.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // normal end of a scan, or ended by the policy (reported below)
            }
            finally
            {
                await scanner.StopAsync().ConfigureAwait(false);
            }

            // false = "the stick did not serve this scan": the caller continues with Windows.
            return token.IsCancellationRequested || GattMode != UsbGattMode.WindowsOnly;
        }
        finally
        {
            lock (_scanLock)
            {
                _policyScans.Remove(policyScan);
            }
        }
    }

    /// <summary>
    /// Creates a GATT device on a free stick slot, or null when no slot is free right now (checked again
    /// here because another device may have taken the slot after the path decision).
    /// </summary>
    /// <param name="address">Peer address "AA:BB:CC:DD:EE:FF".</param>
    public IBluetoothLEDevice? TryCreateDevice(string address)
    {
        StickGattLink? link = _connection.TryAcquireLink();
        if (link == null)
        {
            UsbStickTrace.Write($"TryCreateDevice {address}: no free stick slot");
            return null;
        }

        _logger.LogInformation("GATT device {Address} on USB stick connection {ConnId}", address, link.ConnId);
        return new StickGattDevice(address, link, () => _connection.TryAcquireLink(), _addressBook, _loggerFactory.CreateLogger<StickGattDevice>());
    }
}
