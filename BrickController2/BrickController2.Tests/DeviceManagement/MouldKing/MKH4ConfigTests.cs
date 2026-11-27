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
    public void PercentToUnits_ClampsAndScales(float percent, int expected)
    {
        MKH4Config.PercentToUnits(percent).Should().Be(expected);
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
