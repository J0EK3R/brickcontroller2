using BrickController2.DeviceManagement.MouldKing;
using BrickController2.Settings;
using FluentAssertions;
using System.Collections.Generic;
using Xunit;

namespace BrickController2.Tests.DeviceManagement.MouldKing;

/// <summary>
/// Tests of the settings snapshot of the MKH4.0 module (defaults, stored values, change detection).
/// </summary>
public class MKH4ConfigTests
{
    private static NamedSetting Setting(string name, object value) => new() { Name = name, Value = value };

    [Fact]
    public void FromSettings_Empty_UsesDocumentedDefaults()
    {
        var config = MKH4Config.FromSettings([], 4);

        config.StallProtection.Should().BeTrue();
        config.MoveSpeed.Should().Be(0.625f);
        config.MotorStrengthEnabled.Should().BeFalse();
        config.MotorStrength.Should().Be(100);
        config.Limits.Should().HaveCount(4);
        config.Limits.Should().OnlyContain(limit => !limit.Enabled);
        config.ChannelFlags.Should().Equal('0', '0', '0', '0');
    }

    [Fact]
    public void FromSettings_StoredValues_AreTakenOver()
    {
        // values as they come back from JSON: double/long instead of Percent
        var settings = new List<NamedSetting>
        {
            Setting(MKH4Config.StallProtectionName, false),
            Setting(MKH4Config.MoveSpeedName, 50.0),
            Setting(MKH4Config.MotorStrengthEnabledName, true),
            Setting(MKH4Config.MotorStrengthName, 50L),
            Setting(MKH4Config.LimitEnabledName(1), true),
            Setting(MKH4Config.LimitMaxName(1), 17.0),
            Setting(MKH4Config.LimitMinName(1), new Percent(10f)),
        };

        var config = MKH4Config.FromSettings(settings, 4);

        config.StallProtection.Should().BeFalse();
        config.MoveSpeed.Should().Be(0.5f);
        config.MotorStrengthEnabled.Should().BeTrue();
        config.MotorStrength.Should().Be(50);
        config.Limits[1].Should().Be(new MKH4ChannelLimit(true, -59400, 100980));
        config.Limits[0].Enabled.Should().BeFalse();
        config.ChannelFlags.Should().Equal('0', '1', '0', '0');
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(17f, 100980)]
    [InlineData(100f, 594000)]
    [InlineData(-5f, 0)]
    [InlineData(250f, 594000)]
    [InlineData(1f, 5940)]
    public void PercentToUnits_ClampsAndScales(float percent, int expected)
    {
        MKH4Config.PercentToUnits(percent, MKH4MotorType.XL).Should().Be(expected);
    }

    [Theory]
    [InlineData(MKH4MotorType.L, 0f, 0)]
    [InlineData(MKH4MotorType.L, 1f, 3950)]
    [InlineData(MKH4MotorType.L, 17f, 67150)]
    [InlineData(MKH4MotorType.L, 100f, 395000)]
    [InlineData(MKH4MotorType.L, 250f, 395000)]
    [InlineData(MKH4MotorType.M, 0f, 0)]
    [InlineData(MKH4MotorType.M, 1f, 3950)]
    [InlineData(MKH4MotorType.M, 17f, 67150)]
    [InlineData(MKH4MotorType.M, 100f, 395000)]
    [InlineData(MKH4MotorType.M, 250f, 395000)]
    public void PercentToUnits_LAndM_UseTurnOf395000(MKH4MotorType type, float percent, int expected)
    {
        MKH4Config.PercentToUnits(percent, type).Should().Be(expected);
    }

    [Fact]
    public void MotorParameters_AreSeparatePerType()
    {
        MKH4MotorParameters.For(MKH4MotorType.XL).UnitsPerTurn.Should().Be(594000);
        MKH4MotorParameters.For(MKH4MotorType.L).UnitsPerTurn.Should().Be(395000);
        MKH4MotorParameters.For(MKH4MotorType.M).UnitsPerTurn.Should().Be(395000);
        MKH4MotorParameters.For(MKH4MotorType.XL).UnitsPerPercent.Should().Be(5940.0);
        MKH4MotorParameters.For((MKH4MotorType)99).Should().Be(MKH4MotorParameters.XL);
    }

    [Fact]
    public void UnitsPerDegree_DependsOnMotorType()
    {
        MKH4Config.UnitsPerDegree(MKH4MotorType.XL).Should().Be(1650.0);
        MKH4Config.UnitsPerDegree(MKH4MotorType.L).Should().BeApproximately(1097.22, 0.01);
        MKH4Config.UnitsPerDegree(MKH4MotorType.M).Should().BeApproximately(1097.22, 0.01);
    }

    [Fact]
    public void FromSettings_Empty_UsesXLForAllChannels()
    {
        var config = MKH4Config.FromSettings([], 4);

        config.MotorTypes.Should().Equal(MKH4MotorType.XL, MKH4MotorType.XL, MKH4MotorType.XL, MKH4MotorType.XL);
        config.Limits.Should().OnlyContain(limit => limit.MinUnits == -100980 && limit.MaxUnits == 100980);
    }

    [Fact]
    public void FromSettings_MotorTypePerChannel_ScalesLimits()
    {
        var config = MKH4Config.FromSettings(
        [
            Setting(MKH4Config.MotorTypeName(0), 1L), // port A: L (as stored by JSON)
            Setting(MKH4Config.LimitEnabledName(0), true),
            Setting(MKH4Config.LimitEnabledName(1), true),
        ], 4);

        config.Limits[0].Should().Be(new MKH4ChannelLimit(true, -67150, 67150));
        config.Limits[1].Should().Be(new MKH4ChannelLimit(true, -100980, 100980));
        config.UnitsPerDegree(0).Should().BeApproximately(1097.22, 0.01);
        config.UnitsPerDegree(1).Should().Be(1650.0);
    }

    [Fact]
    public void GetValue_MotorType_ConvertsStoredNumbers()
    {
        Setting("x", 1).GetValue(MKH4MotorType.XL).Should().Be(MKH4MotorType.L);
        Setting("x", 1L).GetValue(MKH4MotorType.XL).Should().Be(MKH4MotorType.L);
        Setting("x", 2).GetValue(MKH4MotorType.XL).Should().Be(MKH4MotorType.M);
        Setting("x", 2L).GetValue(MKH4MotorType.XL).Should().Be(MKH4MotorType.M);
        Setting("x", 7).GetValue(MKH4MotorType.XL).Should().Be(MKH4MotorType.XL);
    }

    [Fact]
    public void GetChanges_MotorType_ListsOnlyAffectedEnabledChannel()
    {
        var enabled = new[] { Setting(MKH4Config.LimitEnabledName(0), true), Setting(MKH4Config.LimitEnabledName(1), true) };
        var oldConfig = MKH4Config.FromSettings(enabled, 4);
        var newConfig = MKH4Config.FromSettings([.. enabled, Setting(MKH4Config.MotorTypeName(0), 1)], 4);

        MKH4Config.GetChanges(oldConfig, newConfig).LimitChannels.Should().Equal(0);

        // limits of the channel not enabled: nothing to send
        var noLimits = MKH4Config.FromSettings([Setting(MKH4Config.MotorTypeName(0), 1)], 4);
        MKH4Config.GetChanges(MKH4Config.FromSettings([], 4), noLimits).IsEmpty.Should().BeTrue();
    }

    [Theory]
    [InlineData(62.5f, 0.625f)]
    [InlineData(0f, 0.01f)]
    [InlineData(100f, 1f)]
    public void PercentToMoveSpeed_HasMinimumOfOnePercent(float percent, float expected)
    {
        MKH4Config.PercentToMoveSpeed(percent).Should().Be(expected);
    }

    [Fact]
    public void GetChanges_SameConfig_IsEmpty()
    {
        var config = MKH4Config.FromSettings([], 4);

        MKH4Config.GetChanges(config, MKH4Config.FromSettings([], 4)).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void GetChanges_StallProtection()
    {
        var oldConfig = MKH4Config.FromSettings([], 4);
        var newConfig = MKH4Config.FromSettings([Setting(MKH4Config.StallProtectionName, false)], 4);

        var changes = MKH4Config.GetChanges(oldConfig, newConfig);

        changes.StallProtection.Should().BeTrue();
        changes.MotorStrength.Should().BeFalse();
        changes.LimitChannels.Should().BeEmpty();
    }

    [Fact]
    public void GetChanges_MoveSpeedOnly_SendsNothing()
    {
        var oldConfig = MKH4Config.FromSettings([], 4);
        var newConfig = MKH4Config.FromSettings([Setting(MKH4Config.MoveSpeedName, 30.0)], 4);

        MKH4Config.GetChanges(oldConfig, newConfig).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void GetChanges_MotorStrength_OnlyWhenEnabled()
    {
        var disabled = MKH4Config.FromSettings([Setting(MKH4Config.MotorStrengthName, 20.0)], 4);
        var enabled = MKH4Config.FromSettings([Setting(MKH4Config.MotorStrengthEnabledName, true), Setting(MKH4Config.MotorStrengthName, 20.0)], 4);
        var enabledOther = MKH4Config.FromSettings([Setting(MKH4Config.MotorStrengthEnabledName, true), Setting(MKH4Config.MotorStrengthName, 30.0)], 4);

        // value changed, but not enabled: nothing to send
        MKH4Config.GetChanges(MKH4Config.FromSettings([], 4), disabled).MotorStrength.Should().BeFalse();

        // switched on and value changed while on: send
        MKH4Config.GetChanges(disabled, enabled).MotorStrength.Should().BeTrue();
        MKH4Config.GetChanges(enabled, enabledOther).MotorStrength.Should().BeTrue();

        // switched off: nothing is sent, the module keeps its state
        MKH4Config.GetChanges(enabled, disabled).MotorStrength.Should().BeFalse();
    }

    [Fact]
    public void GetChanges_Limits_OnlyEnabledChangedChannels()
    {
        var oldConfig = MKH4Config.FromSettings([Setting(MKH4Config.LimitEnabledName(0), true)], 4);
        var newConfig = MKH4Config.FromSettings(
        [
            Setting(MKH4Config.LimitEnabledName(0), true),
            Setting(MKH4Config.LimitMaxName(0), 20.0), // channel A changed
            Setting(MKH4Config.LimitMaxName(1), 20.0), // channel B changed but not enabled
            Setting(MKH4Config.LimitEnabledName(2), true), // channel C switched on
        ], 4);

        MKH4Config.GetChanges(oldConfig, newConfig).LimitChannels.Should().Equal(0, 2);

        // switching a limit off sends nothing
        MKH4Config.GetChanges(newConfig, MKH4Config.FromSettings([], 4)).LimitChannels.Should().BeEmpty();
    }
}
