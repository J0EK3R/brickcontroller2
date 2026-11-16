using BrickController2.InputDeviceManagement.Sensors;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using BrickController2.PlatformServices.InputDeviceService;
using BrickController2.UI.Commands;
using BrickController2.UI.Services.Dialog;
using BrickController2.UI.Services.Localization;
using BrickController2.UI.Services.Navigation;
using BrickController2.UI.Services.Theme;
using BrickController2.UI.Services.Translation;
using Microsoft.Maui.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace BrickController2.UI.ViewModels
{
    public class SettingsPageViewModel : PageViewModelBase
    {
        private const int ProgressDialogDelayMs = 500;

        private readonly IThemeService _themeService;
        private readonly ILocalizationService _localizationService;
        private readonly IInputDeviceService<OrientationSensorController> _orientationSensorService;
        private readonly CreationListPageViewModel _parentViewModel;
        private readonly IDialogService _dialogService;
        private readonly IUsbAdvertiserService? _usbAdvertiserService;

        public SettingsPageViewModel(
            INavigationService navigationService,
            ITranslationService translationService,
            IDialogService dialogService,
            IThemeService themeService,
            ILocalizationService localizationService,
            IInputDeviceService<OrientationSensorController> orientationSensorService,
            NavigationParameters parameters,
            IUsbAdvertiserService? usbAdvertiserService = null) : 
            base(navigationService, translationService)
        {
            _themeService = themeService;
            _dialogService = dialogService;
            _localizationService = localizationService;
            _orientationSensorService = orientationSensorService;
            _parentViewModel = parameters.Get<CreationListPageViewModel>("parent");
            SelectThemeCommand = new SafeCommand(SelectThemeAsync);
            SelectLanguageCommand = new SafeCommand(SelectAppLanguageAsync);
            _usbAdvertiserService = usbAdvertiserService;
            SelectUsbAdvertiserModeCommand = new SafeCommand(SelectUsbAdvertiserModeAsync);
        }

        public ThemeType CurrentTheme
        {
            get => _themeService.CurrentTheme;
            set
            {
                if (CurrentTheme != value)
                {
                    _themeService.CurrentTheme = value;
                    RaisePropertyChanged();
                }
            }
        }

        public Language CurrentLanguage
        {
            get => _localizationService.CurrentLanguage;
            set
            {
                if (_localizationService.CurrentLanguage != value)
                {
                    _localizationService.CurrentLanguage = value;
                    RaisePropertyChanged();
                }
            }
        }

        public ICommand SelectThemeCommand { get; }
        public ICommand SelectLanguageCommand { get; }
        public ICommand SelectUsbAdvertiserModeCommand { get; }

        /// <summary>
        /// True on platforms that can use the USB BLE advertising stick (Windows); the settings rows are hidden elsewhere.
        /// </summary>
        public bool IsUsbAdvertiserSupported => _usbAdvertiserService != null;

        /// <summary>
        /// Selected USB advertiser mode (Auto / Off).
        /// </summary>
        public UsbAdvertiserMode CurrentUsbAdvertiserMode => _usbAdvertiserService?.Mode ?? UsbAdvertiserMode.Off;

        /// <summary>
        /// Translated status line of the USB stick, e.g. "Connected (firmware 0.1.0, COM7)".
        /// </summary>
        public string UsbStickStatusText => BuildUsbStickStatusText();

        public override void OnAppearing()
        {
            base.OnAppearing();

            if (_usbAdvertiserService != null)
            {
                _usbAdvertiserService.StatusChanged += OnUsbAdvertiserStatusChanged;

                // Look for the stick now so the status line is up to date (result arrives via StatusChanged).
                _ = _usbAdvertiserService.RefreshAsync();
            }
        }

        public override void OnDisappearing()
        {
            if (_usbAdvertiserService != null)
            {
                _usbAdvertiserService.StatusChanged -= OnUsbAdvertiserStatusChanged;
            }

            base.OnDisappearing();
        }

        public bool IsOrientationSensorSupported => _orientationSensorService.IsSupported;

        public bool IsOrientationSensorEnabled
        {
            get => _orientationSensorService.IsEnabled;
            set
            {
                if (IsOrientationSensorEnabled != value)
                {
                    _orientationSensorService.IsEnabled = value;
                    RaisePropertyChanged();
                }
            }
        }

        private void OnUsbAdvertiserStatusChanged(object? sender, EventArgs e)
        {
            RaisePropertyChanged(nameof(CurrentUsbAdvertiserMode));
            RaisePropertyChanged(nameof(UsbStickStatusText));
        }

        private string BuildUsbStickStatusText()
        {
            if (_usbAdvertiserService == null)
            {
                return string.Empty;
            }

            if (_usbAdvertiserService.Mode == UsbAdvertiserMode.Off)
            {
                return Translate("UsbStickDisabled");
            }

            var status = _usbAdvertiserService.Status;
            var details = string.Join(", ", new[] { status.FirmwareVersion, status.PortName }.Where(d => !string.IsNullOrEmpty(d)));

            return status.State switch
            {
                UsbStickState.Connected or UsbStickState.Idle => Translate("UsbStickConnected", details),
                UsbStickState.Lost => Translate("UsbStickLost"),
                UsbStickState.Incompatible => Translate("UsbStickIncompatible"),
                _ => Translate("UsbStickNotFound"),
            };
        }

        private async Task SelectUsbAdvertiserModeAsync()
        {
            if (_usbAdvertiserService == null)
            {
                return;
            }

            var result = await _dialogService.ShowSelectionDialogAsync(
                Enum.GetNames<UsbAdvertiserMode>(),
                Translate("UsbAdvertiser"),
                Translate("Cancel"),
                DisappearingToken);

            if (result.IsOk && Enum.TryParse<UsbAdvertiserMode>(result.SelectedItem, out var mode))
            {
                _usbAdvertiserService.Mode = mode;
                if (mode == UsbAdvertiserMode.Auto)
                {
                    _ = _usbAdvertiserService.RefreshAsync();
                }
            }
        }

        private async Task SelectThemeAsync()
        {
            var result = await _dialogService.ShowSelectionDialogAsync(
                Enum.GetNames<ThemeType>(),
                Translate("Theme"),
                Translate("Cancel"),
                DisappearingToken);

            if (result.IsOk)
            {
                CurrentTheme = Enum.Parse<ThemeType>(result.SelectedItem);
            }
        }

        private async Task SelectAppLanguageAsync()
        {
            var result = await _dialogService.ShowSelectionDialogAsync(
                Enum.GetNames<Language>(),
                Translate("Language"),
                Translate("Cancel"),
                DisappearingToken);

            if (result.IsOk && Enum.TryParse<Language>(result.SelectedItem, out var currentLanguage))
            {
                // apply the change
                CurrentLanguage = currentLanguage;

                // use some notification via progress dialog
                await _dialogService.ShowProgressDialogAsync(
                    false,
                    (progressDialog, token) =>
                    {
                        // recreate the root page to apply the change
                        if (Application.Current is App myApp)
                        {
                            myApp.ReloadRootPage();
                        }
                        // some delay to show the progress dialog
                        return Task.Delay(ProgressDialogDelayMs, token);
                    },
                    Translate("Applying"),
                    token: DisappearingToken);

                // back to the previous page
                await NavigationService.NavigateBackAsync();
                // workaround for settings cmd available
                _parentViewModel.OpenSettingsPageCommand.RaiseCanExecuteChanged();
            }
        }
    }
}
