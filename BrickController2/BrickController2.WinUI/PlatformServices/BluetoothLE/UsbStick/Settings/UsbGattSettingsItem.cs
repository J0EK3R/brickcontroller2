// Settings row "Bluetooth LE devices (GATT)": mode Auto/WindowsOnly/StickOnly plus the connection count.
using System;
using System.Threading.Tasks;
using BrickController2.UI.Services.Dialog;
using BrickController2.UI.Services.Translation;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick.Settings;

/// <summary>
/// Port of the former SettingsPageViewModel USB GATT members to the platform settings hook.
/// </summary>
public sealed class UsbGattSettingsItem : UsbSettingsItemBase
{
    private readonly IDialogService _dialogService;
    private readonly ITranslationService _translationService;
    private readonly IUsbStickStrings _strings;

    public UsbGattSettingsItem(
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
    public override int Order => 110;

    /// <inheritdoc />
    public override string Title => _strings.Get("UsbGatt");

    /// <inheritdoc />
    public override string Value => Gatt.GattMode.ToString();

    /// <inheritdoc />
    public override string Status => BuildStatusText();

    /// <summary>
    /// Translated GATT status, e.g. "USB stick connections (used/max): 2/9, Scanning through the USB stick".
    /// </summary>
    private string BuildStatusText()
    {
        if (Gatt.GattMode == UsbGattMode.WindowsOnly)
        {
            return string.Empty;
        }

        var status = Gatt.GattStatus;
        if (!status.Supported)
        {
            // Only mention missing GATT support when a stick is connected but its firmware lacks it.
            return Advertiser.Status.State == UsbStickState.Connected ? _strings.Get("UsbStickNoGatt") : string.Empty;
        }

        var text = _strings.Get("UsbStickLinks", $"{status.LinksInUse}/{status.MaxLinks}");
        return status.Scanning ? text + ", " + _strings.Get("UsbStickScanning") : text;
    }

    /// <inheritdoc />
    protected override async Task SelectAsync()
    {
        var result = await _dialogService.ShowSelectionDialogAsync(
            Enum.GetNames<UsbGattMode>(),
            Title,
            _translationService.Translate("Cancel"),
            DialogToken);

        if (result.IsOk && Enum.TryParse<UsbGattMode>(result.SelectedItem, out var mode))
        {
            Gatt.GattMode = mode;
            if (mode != UsbGattMode.WindowsOnly)
            {
                // Look for the stick now so the status line shows its connection count (result arrives via StatusChanged).
                _ = Advertiser.RefreshAsync();
            }
        }
    }
}
