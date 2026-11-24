// Common behaviour of the two USB stick rows of the settings page.
using System;
using System.ComponentModel;
using System.Threading;
using System.Windows.Input;
using BrickController2.UI.Commands;
using BrickController2.UI.Services.PlatformSettings;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick.Settings;

/// <summary>
/// Base class of the USB stick settings rows: implements the change notification, the stick status
/// subscription (both rows depend on the advertiser AND the GATT status) and the cancellation token that
/// replaces the page's DisappearingToken for the selection dialog.
/// </summary>
public abstract class UsbSettingsItemBase : IPlatformSettingsItem
{
    private readonly IUsbAdvertiserService _advertiser;
    private readonly IUsbGattService _gatt;
    private readonly Action<Action> _dispatch;
    private CancellationTokenSource? _cts;
    private IDisposable? _presenceWatch;   // keeps the stick presence poll alive while the page is open

    /// <param name="advertiser">Advertiser setting/status service.</param>
    /// <param name="gatt">GATT setting/status service.</param>
    /// <param name="dispatch">Runs a notification on the UI thread (the stick events come from background threads).</param>
    protected UsbSettingsItemBase(IUsbAdvertiserService advertiser, IUsbGattService gatt, Action<Action> dispatch)
    {
        _advertiser = advertiser;
        _gatt = gatt;
        _dispatch = dispatch;
        Command = new SafeCommand(SelectAsync);
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    public abstract string Title { get; }

    /// <inheritdoc />
    public abstract string Value { get; }

    /// <inheritdoc />
    public abstract string Status { get; }

    /// <inheritdoc />
    public ICommand Command { get; }

    /// <summary>
    /// Token cancelled when the page disappears; pass it to dialogs.
    /// </summary>
    protected CancellationToken DialogToken => _cts?.Token ?? default;

    /// <summary>
    /// The advertiser service, for derived classes.
    /// </summary>
    protected IUsbAdvertiserService Advertiser => _advertiser;

    /// <summary>
    /// The GATT service, for derived classes.
    /// </summary>
    protected IUsbGattService Gatt => _gatt;

    /// <inheritdoc />
    public virtual void Attach()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _advertiser.StatusChanged += OnStatusChanged;
        _gatt.StatusChanged += OnStatusChanged;

        // Poll cheaply for a (re-)plugged stick while the settings page is visible.
        _presenceWatch?.Dispose();
        _presenceWatch = _advertiser.BeginWatching();
    }

    /// <inheritdoc />
    public virtual void Detach()
    {
        _advertiser.StatusChanged -= OnStatusChanged;
        _gatt.StatusChanged -= OnStatusChanged;
        _presenceWatch?.Dispose();
        _presenceWatch = null;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    /// <summary>
    /// Opens the selection dialog and applies the choice (command handler of the row button).
    /// </summary>
    protected abstract System.Threading.Tasks.Task SelectAsync();

    /// <summary>
    /// Raises change notifications for Title, Value and Status on the UI thread.
    /// </summary>
    protected void RaiseAll() => _dispatch(() =>
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
    });

    private void OnStatusChanged(object? sender, EventArgs e) => RaiseAll();
}
