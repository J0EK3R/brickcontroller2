// Unit tests for the settings page view model: item order, titles, visibility and built-in behaviour.
using BrickController2.InputDeviceManagement.Sensors;
using BrickController2.PlatformServices.InputDeviceService;
using BrickController2.UI.Services.Dialog;
using BrickController2.UI.Services.Localization;
using BrickController2.UI.Services.Navigation;
using BrickController2.UI.Services.Settings;
using BrickController2.UI.Services.Theme;
using BrickController2.UI.Services.Translation;
using BrickController2.UI.ViewModels;
using FluentAssertions;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace BrickController2.Tests.UI.ViewModels;

public class SettingsPageViewModelTests
{
    private readonly Mock<ITranslationService> _translation = new();
    private readonly Mock<IDialogService> _dialog = new();
    private readonly Mock<IThemeService> _theme = new();
    private readonly Mock<ILocalizationService> _localization = new();
    private readonly Mock<IInputDeviceService<OrientationSensorController>> _orientation = new();

    public SettingsPageViewModelTests()
    {
        // the stub returns the key so titles are easy to assert
        _translation.Setup(t => t.Translate(It.IsAny<string>())).Returns((string key) => key);
        _orientation.SetupProperty(o => o.IsEnabled);
        _orientation.SetupGet(o => o.IsSupported).Returns(true);
    }

    private SettingsPageViewModel CreateViewModel(IEnumerable<ISettingsItem>? platformItems = null)
    {
        // the parent view model is only used after a language change; an uninitialized instance avoids its many dependencies
        var parent = (CreationListPageViewModel)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(CreationListPageViewModel));
        return new SettingsPageViewModel(
            Mock.Of<INavigationService>(),
            _translation.Object,
            _dialog.Object,
            _theme.Object,
            _localization.Object,
            _orientation.Object,
            new NavigationParameters(("parent", parent)),
            platformItems);
    }

    [Fact]
    public void SettingsItems_AreOrderedBuiltInFirstThenPlatform()
    {
        var platform = new Mock<ISettingsItem>();
        platform.SetupGet(i => i.Title).Returns("Platform");

        var viewModel = CreateViewModel(new[] { platform.Object });

        viewModel.SettingsItems.Select(i => i.Title)
            .Should().Equal("Theme", "Language", "OrientationSensor", "Platform");
        viewModel.SettingsItems[2].Should().BeAssignableTo<ISettingsToggleItem>();
    }

    [Fact]
    public void SettingsItems_WithoutPlatformItems_ContainsOnlyBuiltInItems()
    {
        CreateViewModel().SettingsItems.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OrientationItem_VisibilityFollowsSupport(bool supported)
    {
        _orientation.SetupGet(o => o.IsSupported).Returns(supported);

        CreateViewModel().SettingsItems[2].IsVisible.Should().Be(supported);
    }

    [Fact]
    public void OrientationItem_ToggleWritesServiceState()
    {
        var item = (ISettingsToggleItem)CreateViewModel().SettingsItems[2];

        item.IsToggled = true;

        _orientation.Object.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task ThemeItem_SelectingThemeUpdatesServiceAndValue()
    {
        var current = ThemeType.Light;
        _theme.SetupGet(t => t.CurrentTheme).Returns(() => current);
        _theme.SetupSet(t => t.CurrentTheme = It.IsAny<ThemeType>()).Callback<ThemeType>(v => current = v);
        _dialog.Setup(d => d.ShowSelectionDialogAsync(It.IsAny<IEnumerable<string>>(), "Theme", "Cancel", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SelectionDialogResult<string>(true, ThemeType.Dark.ToString()));
        var item = CreateViewModel().SettingsItems[0];
        var valueChanged = false;
        item.PropertyChanged += (_, e) => valueChanged |= e.PropertyName == nameof(ISettingsItem.Value);

        item.Command.Execute(null);
        await Task.Delay(100, TestContext.Current.CancellationToken); // SafeCommand runs the async delegate fire-and-forget

        current.Should().Be(ThemeType.Dark);
        item.Value.Should().Be(nameof(ThemeType.Dark));
        valueChanged.Should().BeTrue();
    }

    [Fact]
    public void OnAppearingAndDisappearing_AttachAndDetachPlatformItems()
    {
        var platform = new Mock<ISettingsItem>();
        var viewModel = CreateViewModel(new[] { platform.Object });

        viewModel.OnAppearing();
        viewModel.OnDisappearing();

        platform.Verify(i => i.Attach(), Times.Once);
        platform.Verify(i => i.Detach(), Times.Once);
    }
}
