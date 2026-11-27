using BrickController2.DeviceManagement;
using BrickController2.DeviceManagement.MouldKing;
using BrickController2.PlatformServices.BluetoothLE;
using BrickController2.Resources;
using BrickController2.Settings;
using FluentAssertions;
using Moq;
using System.Globalization;
using System.Linq;
using System.Resources;
using Xunit;

namespace BrickController2.Tests.DeviceManagement.MouldKing;

/// <summary>
/// Tests of the registered device settings of the MKH4.0 module.
/// </summary>
public class MKH4SettingsTests
{
    private static MKH4 CreateDevice(params NamedSetting[] settings)
        => new("Module", "address", settings, Mock.Of<IDeviceRepository>(), Mock.Of<IBluetoothLEService>());

    [Fact]
    public void Settings_AreRegisteredWithDefaults()
    {
        var device = CreateDevice();

        device.HasSettings.Should().BeTrue();

        var byName = device.CurrentSettings.ToDictionary(s => s.Name);
        byName.Should().HaveCount(4 + 4 * 3);

        byName[MKH4Config.StallProtectionName].Value.Should().Be(true);
        byName[MKH4Config.MoveSpeedName].Value.Should().Be(new Percent(62.5f));
        byName[MKH4Config.MotorStrengthEnabledName].Value.Should().Be(false);
        byName[MKH4Config.MotorStrengthName].Value.Should().Be(new Percent(100f));
        byName[MKH4Config.MotorStrengthName].Group.Should().Be(MKH4Config.MotorStrengthGroupName);

        byName["MKH4LimitEnabledA"].Group.Should().Be("MKH4GroupLimitA");
        byName["MKH4LimitEnabledA"].Value.Should().Be(false);
        byName["MKH4LimitMaxD"].Value.Should().Be(new Percent(17f));
        byName["MKH4LimitMinD"].Group.Should().Be("MKH4GroupLimitD");
    }

    [Fact]
    public void Settings_OnlyBoolAndPercent_SoThePageCanRenderThem()
    {
        var device = CreateDevice();

        device.CurrentSettings.Should().OnlyContain(s => s.IsBoolType || s.IsPercentType);
    }

    [Fact]
    public void Settings_StoredValuesAreTakenOver()
    {
        var device = CreateDevice(new NamedSetting { Name = MKH4Config.StallProtectionName, Value = false });

        device.CurrentSettings.Single(s => s.Name == MKH4Config.StallProtectionName).Value.Should().Be(false);
    }

    [Theory]
    [InlineData("")]
    [InlineData("de")]
    public void Settings_HaveTranslations(string culture)
    {
        var cultureInfo = new CultureInfo(culture);
        var device = CreateDevice();

        // look at the culture itself (without fallback), the neutral resources must contain all keys as well
        var resourceSet = TranslationResources.ResourceManager.GetResourceSet(cultureInfo, createIfNotExists: true, tryParents: false);
        resourceSet.Should().NotBeNull();

        var keys = device.CurrentSettings.Select(s => s.Name)
            .Concat(device.CurrentSettings.Select(s => s.Group))
            .Where(key => !string.IsNullOrEmpty(key))
            .Distinct();

        foreach (var key in keys)
        {
            resourceSet!.GetString(key).Should().NotBeNullOrEmpty($"the key {key} needs a translation ({culture})");
        }
    }
}
