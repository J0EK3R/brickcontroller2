using System;
using System.Threading.Tasks;
using BrickController2.UI.Services.Preferences;
using Microsoft.Extensions.Logging;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Combines the user setting (Auto/Off) with the stick connection. It decides whether a new advertising
/// device uses the stick; the caller falls back to the OS advertiser when this returns null.
/// </summary>
public sealed class UsbAdvertiserService : IUsbAdvertiserService
{
    private const string ModePreferenceKey = "UsbAdvertiserMode";

    // Longest time a device creation may wait for the stick probe (creation is synchronous in BC2).
    private static readonly TimeSpan CreateProbeTimeout = TimeSpan.FromSeconds(1);

    private readonly UsbStickConnection _connection;
    private readonly IPreferencesService _preferences;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    public UsbAdvertiserService(UsbStickConnection connection, IPreferencesService preferences, ILoggerFactory loggerFactory)
    {
        _connection = connection;
        _preferences = preferences;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<UsbAdvertiserService>();
        _connection.StatusChanged += (_, e) => StatusChanged?.Invoke(this, e);
    }

    /// <inheritdoc />
    public event EventHandler? StatusChanged;

    /// <inheritdoc />
    public UsbAdvertiserMode Mode
    {
        get => ReadMode();
        set
        {
            if (Mode == value)
            {
                return;
            }

            _preferences.Set(ModePreferenceKey, value);
            _logger.LogInformation("USB advertiser mode set to {Mode}", value);
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Reads the stored mode; a corrupted preference value falls back to Auto instead of throwing.</summary>
    private UsbAdvertiserMode ReadMode()
    {
        try
        {
            return _preferences.Get(ModePreferenceKey, UsbAdvertiserMode.Auto);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException or InvalidCastException)
        {
            _logger.LogWarning("Invalid USB advertiser mode preference, using Auto: {Message}", ex.Message);
            return UsbAdvertiserMode.Auto;
        }
    }

    /// <inheritdoc />
    public UsbStickStatus Status => _connection.Status;

    /// <inheritdoc />
    public Task RefreshAsync() => Mode == UsbAdvertiserMode.Off
        ? Task.CompletedTask
        : _connection.TryConnectAsync(forceProbe: true);

    /// <summary>
    /// True when the setting is Auto and a stick is connected or can be connected right now. Fully async
    /// (never blocks the caller); a failed probe is cached by the connection for 10 s, so repeated calls
    /// (e.g. from every page that checks Bluetooth) stay cheap.
    /// </summary>
    public async Task<bool> IsStickAvailableAsync()
    {
        if (Mode == UsbAdvertiserMode.Off)
        {
            return false;
        }

        try
        {
            return _connection.IsConnected || await _connection.TryConnectAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("USB stick availability check failed: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Creates an advertiser device on a free stick set, or returns null when the stick must not or cannot
    /// be used (mode Off, no stick, stick lost, all 5 sets in use). No live switching happens afterwards:
    /// an already created device keeps its set and resumes when the stick returns.
    /// </summary>
    public IBluetoothLEAdvertiserDevice? TryCreateAdvertiserDevice()
    {
        if (Mode == UsbAdvertiserMode.Off)
        {
            return null;
        }

        if (!_connection.IsConnected && !ProbeBlocking())
        {
            _logger.LogInformation("USB stick not available, using the Windows advertiser");
            return null;
        }

        StickSetLease? lease = _connection.TryAcquireLease();
        if (lease == null)
        {
            _logger.LogInformation("No free USB stick set, using the Windows advertiser for this device");
            return null;
        }

        _logger.LogInformation("Advertising through USB stick set {SetId}", lease.SetId);
        return new StickAdvertiserDevice(lease, _loggerFactory.CreateLogger<StickAdvertiserDevice>());
    }

    /// <summary>
    /// Runs the (usually instant) probe from the synchronous device creation. Runs on the thread pool and
    /// waits at most one second so the UI cannot hang on a misbehaving port. Known limitation: several
    /// silent ESP32 candidates (3 x 200 ms HELLO each) can exceed that second; the device is then created on
    /// the Windows advertiser although the probe may still find the stick afterwards (see doc/UsbAdvertiser.md).
    /// </summary>
    private bool ProbeBlocking()
    {
        try
        {
            Task<bool> probe = Task.Run(() => _connection.TryConnectAsync());
            return probe.Wait(CreateProbeTimeout) && probe.Result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("USB stick probe failed: {Message}", ex.Message);
            return false;
        }
    }
}
