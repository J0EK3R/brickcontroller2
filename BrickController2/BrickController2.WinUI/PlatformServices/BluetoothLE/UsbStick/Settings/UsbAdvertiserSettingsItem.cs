// Settings row "USB advertiser": mode Auto/Off plus the status line of the stick.
using System;
using System.Linq;
using System.Threading.Tasks;
using BrickController2.UI.Services.Dialog;
using BrickController2.UI.Services.Translation;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick.Settings;

/// <summary>
/// Port of the former SettingsPageViewModel USB advertiser members to the platform settings hook.
/// </summary>
public sealed class UsbAdvertiserSettingsItem : UsbSettingsItemBase
{
    private readonly IDialogService _dialogService;
    private readonly ITranslationService _translationService;
    private readonly IUsbStickStrings _strings;

    public UsbAdvertiserSettingsItem(
        IUsbAdvertiserService advertiser,
        IUsbGattService gatt,
        IDialogService dialogService,
        ITranslationService translationService,
        IUsbStickStrings strings,
        Action<Action> dispatch)
        : base(advertiser, gatt, dispatch)
    {
        _dialogService = dialogService;
        _translationService = translationService;
        _strings = strings;
    }

    /// <inheritdoc />
    public override string Title => _strings.Get("UsbAdvertiser");

    /// <inheritdoc />
    public override string Value => Advertiser.Mode.ToString();

    /// <inheritdoc />
    public override string Status => BuildStatusText();

    /// <summary>
    /// Subscribes and looks for the stick now so the status line is up to date (result arrives via StatusChanged).
    /// </summary>
    public override void Attach()
    {
        base.Attach();
        _ = Advertiser.RefreshAsync();
    }

    /// <summary>
    /// Translated status line, e.g. "USB stick connected: 0.1.0, COM7".
    /// </summary>
    private string BuildStatusText()
    {
        if (Advertiser.Mode == UsbAdvertiserMode.Off)
        {
            return _strings.Get("UsbStickDisabled");
        }

        var status = Advertiser.Status;
        var details = string.Join(", ", new[] { status.FirmwareVersion, status.PortName }.Where(d => !string.IsNullOrEmpty(d)));

        return status.State switch
        {
            UsbStickState.Connected or UsbStickState.Idle => _strings.Get("UsbStickConnected", details),
            UsbStickState.Lost => _strings.Get("UsbStickLost"),
            UsbStickState.Incompatible => _strings.Get("UsbStickIncompatible"),
            _ => _strings.Get("UsbStickNotFound"),
        };
    }

    /// <inheritdoc />
    protected override async Task SelectAsync()
    {
        var result = await _dialogService.ShowSelectionDialogAsync(
            Enum.GetNames<UsbAdvertiserMode>(),
            Title,
            _translationService.Translate("Cancel"),
            DialogToken);

        if (result.IsOk && Enum.TryParse<UsbAdvertiserMode>(result.SelectedItem, out var mode))
        {
            Advertiser.Mode = mode;
            if (mode == UsbAdvertiserMode.Auto)
            {
                _ = Advertiser.RefreshAsync();
            }
        }
    }
}
