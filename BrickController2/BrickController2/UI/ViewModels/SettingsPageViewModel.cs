using BrickController2.InputDeviceManagement.Sensors;
using BrickController2.PlatformServices.InputDeviceService;
using BrickController2.UI.Commands;
using BrickController2.UI.Services.Dialog;
using BrickController2.UI.Services.Localization;
using BrickController2.UI.Services.Navigation;
using BrickController2.UI.Services.Settings;
using BrickController2.UI.Services.Theme;
using BrickController2.UI.Services.Translation;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        private readonly PickerSettingsItem _themeItem;
        private readonly PickerSettingsItem _languageItem;

        public SettingsPageViewModel(
            INavigationService navigationService,
            ITranslationService translationService,
            IDialogService dialogService,
            IThemeService themeService,
            ILocalizationService localizationService,
            IInputDeviceService<OrientationSensorController> orientationSensorService,
            NavigationParameters parameters,
            IEnumerable<ISettingsItem>? platformSettings = null) : 
            base(navigationService, translationService)
        {
            _themeService = themeService;
            _dialogService = dialogService;
            _localizationService = localizationService;
            _orientationSensorService = orientationSensorService;
            _parentViewModel = parameters.Get<CreationListPageViewModel>("parent");

            // built-in rows need page-bound services, so they are created here instead of being registered in DI
            _themeItem = new PickerSettingsItem(Translate("Theme"), () => CurrentTheme.ToString(), SelectThemeAsync);
            _languageItem = new PickerSettingsItem(Translate("Language"), () => CurrentLanguage.ToString(), SelectAppLanguageAsync);
            var orientationItem = new ToggleSettingsItem(
                Translate("OrientationSensor"),
                () => _orientationSensorService.IsSupported,
                () => _orientationSensorService.IsEnabled,
                value => _orientationSensorService.IsEnabled = value);

            // fixed order: theme, language, orientation sensor, then the rows contributed by the platform;
            // Autofac injects an empty list (or null in tests) when the platform registered no items
            SettingsItems = new ISettingsItem[] { _themeItem, _languageItem, orientationItem }
                .Concat(platformSettings ?? Enumerable.Empty<ISettingsItem>())
                .ToList();
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
                    _themeItem.RaiseValueChanged();
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
                    _languageItem.RaiseValueChanged();
                }
            }
        }

        /// <summary>
        /// All rows of the settings page in display order: built-in rows first, then platform rows.
        /// </summary>
        public IReadOnlyList<ISettingsItem> SettingsItems { get; }

        public override void OnAppearing()
        {
            base.OnAppearing();
            foreach (var item in SettingsItems)
            {
                item.Attach();
            }
        }

        public override void OnDisappearing()
        {
            foreach (var item in SettingsItems)
            {
                item.Detach();
            }
            base.OnDisappearing();
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
