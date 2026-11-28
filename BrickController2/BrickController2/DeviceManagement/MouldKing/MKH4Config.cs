using BrickController2.Settings;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace BrickController2.DeviceManagement.MouldKing;

/// <summary>
/// Limit settings of one channel, in raw encoder units of the module.
/// </summary>
/// <param name="Enabled">True if the limits are used for the channel.</param>
/// <param name="MinUnits">Negative limit (zero or negative).</param>
/// <param name="MaxUnits">Positive limit (zero or positive).</param>
internal readonly record struct MKH4ChannelLimit(bool Enabled, int MinUnits, int MaxUnits);

/// <summary>
/// Which parts of the configuration have to be sent to the module after a settings change.
/// </summary>
/// <param name="StallProtection">The stall protection (F) has to be sent.</param>
/// <param name="MotorStrength">The motor strength (b) has to be sent.</param>
/// <param name="LimitChannels">Channels whose limits (9) have to be sent.</param>
internal readonly record struct MKH4ConfigChanges(bool StallProtection, bool MotorStrength, IReadOnlyList<int> LimitChannels)
{
    /// <summary>
    /// True if nothing has to be sent.
    /// </summary>
    public bool IsEmpty => !StallProtection && !MotorStrength && LimitChannels.Count == 0;
}

/// <summary>
/// Immutable snapshot of the device settings of the MKH4.0 module. It is built from the stored settings and
/// replaced as a whole when a setting changes, so the output loop and the notification thread never see
/// half-updated values. The defaults reproduce the behaviour of the module without any settings.
/// </summary>
internal sealed class MKH4Config
{
    /// <summary>
    /// Leading digit of a channel in the speed message (T144) if the limits of the channel are enabled.
    /// The meaning of the digit in the module is unverified. If the opposite assignment turns out to be
    /// right, swap this and <see cref="LimitDisabledFlag"/> (one line each).
    /// </summary>
    internal const char LimitEnabledFlag = '1';

    /// <summary>
    /// Leading digit of a channel in the speed message (T144) if the limits of the channel are not enabled
    /// (normal operation).
    /// </summary>
    internal const char LimitDisabledFlag = '0';

    /// <summary>
    /// Limits with a smaller absolute value are treated as 0 by the module.
    /// </summary>
    internal const int MinLimitUnits = 1000;

    // ---- setting names, they are also the keys of the translations ----
    internal const string StallProtectionName = "MKH4StallProtection";
    internal const string MoveSpeedName = "MKH4MoveSpeed";
    internal const string MotorStrengthEnabledName = "MKH4MotorStrengthEnabled";
    internal const string MotorStrengthName = "MKH4MotorStrength";
    internal const string MotorStrengthGroupName = "MKH4GroupMotorStrength";

    // ---- defaults, identical to the behaviour without settings ----
    internal const bool DefaultStallProtection = true;
    internal const float DefaultMoveSpeedPercent = 62.5f;
    internal const bool DefaultMotorStrengthEnabled = false;
    internal const float DefaultMotorStrengthPercent = 100f;
    internal const bool DefaultLimitEnabled = false;
    internal const float DefaultLimitPercent = 17f;
    internal const MKH4MotorType DefaultMotorType = MKH4MotorType.XL;

    /// <summary>
    /// Name of the "limits enabled" setting of a channel (e.g. MKH4LimitEnabledA).
    /// </summary>
    internal static string LimitEnabledName(int channel) => $"MKH4LimitEnabled{ChannelLetter(channel)}";

    /// <summary>
    /// Name of the positive limit setting of a channel (e.g. MKH4LimitMaxA).
    /// </summary>
    internal static string LimitMaxName(int channel) => $"MKH4LimitMax{ChannelLetter(channel)}";

    /// <summary>
    /// Name of the negative limit setting of a channel (e.g. MKH4LimitMinA).
    /// </summary>
    internal static string LimitMinName(int channel) => $"MKH4LimitMin{ChannelLetter(channel)}";

    /// <summary>
    /// Name of the motor type setting of a channel (e.g. MKH4MotorTypeA).
    /// </summary>
    internal static string MotorTypeName(int channel) => $"MKH4MotorType{ChannelLetter(channel)}";

    /// <summary>
    /// Name of the settings group of a channel (e.g. MKH4GroupLimitA).
    /// </summary>
    internal static string LimitGroupName(int channel) => $"MKH4GroupLimit{ChannelLetter(channel)}";

    /// <summary>
    /// Port letter of a channel index (0 = A, 1 = B, ...).
    /// </summary>
    private static char ChannelLetter(int channel) => (char)('A' + channel);

    private MKH4Config(
        bool stallProtection,
        float moveSpeed,
        bool motorStrengthEnabled,
        int motorStrength,
        ImmutableArray<MKH4ChannelLimit> limits,
        ImmutableArray<MKH4MotorType> motorTypes)
    {
        StallProtection = stallProtection;
        MoveSpeed = moveSpeed;
        MotorStrengthEnabled = motorStrengthEnabled;
        MotorStrength = motorStrength;
        Limits = limits;
        MotorTypes = motorTypes;
        ChannelFlags = limits.Select(limit => limit.Enabled ? LimitEnabledFlag : LimitDisabledFlag).ToImmutableArray();
    }

    /// <summary>
    /// True if the stall protection is switched on (F1), otherwise it is off (F0).
    /// </summary>
    public bool StallProtection { get; }

    /// <summary>
    /// Speed (0..1) of position moves.
    /// </summary>
    public float MoveSpeed { get; }

    /// <summary>
    /// True if the motor strength is sent to the module. Otherwise the module keeps what it has.
    /// </summary>
    public bool MotorStrengthEnabled { get; }

    /// <summary>
    /// Motor strength in percent (0..100), only used if <see cref="MotorStrengthEnabled"/> is true.
    /// </summary>
    public int MotorStrength { get; }

    /// <summary>
    /// Limit settings of each channel.
    /// </summary>
    public ImmutableArray<MKH4ChannelLimit> Limits { get; }

    /// <summary>
    /// Motor type of each channel.
    /// </summary>
    public ImmutableArray<MKH4MotorType> MotorTypes { get; }

    /// <summary>
    /// Encoder units per degree of the motor connected to a channel.
    /// </summary>
    public double UnitsPerDegree(int channel) => UnitsPerDegree(MotorTypes[channel]);

    /// <summary>
    /// Leading digit of each channel in the speed message (T144), derived from the limit settings.
    /// </summary>
    public ImmutableArray<char> ChannelFlags { get; }

    /// <summary>
    /// Builds a snapshot from stored settings. Missing settings get their default value,
    /// stored values of another representation (e.g. double or long from JSON) are converted.
    /// </summary>
    internal static MKH4Config FromSettings(IEnumerable<NamedSetting> settings)
    {
        // evaluate once, the lookups below walk the collection several times
        var list = settings?.ToList() ?? [];

        T Read<T>(string name, T defaultValue) => list.FirstOrDefault(s => s.Name == name).GetValue(defaultValue);

        // the motor type is read first, because the limit units depend on it
        var motorTypes = Enumerable.Range(0, MKH4.CHANNEL_COUNT)
            .Select(channel => Read(MotorTypeName(channel), DefaultMotorType))
            .ToImmutableArray();

        var limits = Enumerable.Range(0, MKH4.CHANNEL_COUNT)
            .Select(channel => new MKH4ChannelLimit(
                Read(LimitEnabledName(channel), DefaultLimitEnabled),
                -PercentToUnits(Read<Percent>(LimitMinName(channel), DefaultLimitPercent), motorTypes[channel]),
                PercentToUnits(Read<Percent>(LimitMaxName(channel), DefaultLimitPercent), motorTypes[channel])))
            .ToImmutableArray();

        return new MKH4Config(
            Read(StallProtectionName, DefaultStallProtection),
            PercentToMoveSpeed(Read<Percent>(MoveSpeedName, DefaultMoveSpeedPercent)),
            Read(MotorStrengthEnabledName, DefaultMotorStrengthEnabled),
            (int)Math.Round(Read<Percent>(MotorStrengthName, DefaultMotorStrengthPercent).Value),
            limits,
            motorTypes);
    }

    /// <summary>
    /// Encoder units per degree of a motor type (see <see cref="MKH4MotorParameters"/>).
    /// </summary>
    internal static double UnitsPerDegree(MKH4MotorType type) => MKH4MotorParameters.For(type).UnitsPerDegree;

    /// <summary>
    /// Encoder units of one percent of a revolution of a motor type (XL: 5940, L/M: 3950, both exact).
    /// </summary>
    internal static double UnitsPerPercent(MKH4MotorType type) => MKH4MotorParameters.For(type).UnitsPerPercent;

    /// <summary>
    /// Converts a percentage of a revolution to encoder units (XL: 0..594000, L/M: 0..395000),
    /// values outside 0..100 are clamped.
    /// </summary>
    internal static int PercentToUnits(float percent, MKH4MotorType type)
        => (int)Math.Round(Math.Clamp(percent, 0f, 100f) * UnitsPerPercent(type));

    /// <summary>
    /// Converts the move speed setting (percent) to the range 0..1, the minimum is 1 %.
    /// </summary>
    internal static float PercentToMoveSpeed(float percent)
        => Math.Clamp(percent, 1f, 100f) / 100f;

    /// <summary>
    /// Determines what has to be sent to the module to get from the old to the new configuration.
    /// Disabling the motor strength or a limit sends nothing, because the module keeps its state anyway.
    /// </summary>
    internal static MKH4ConfigChanges GetChanges(MKH4Config oldConfig, MKH4Config newConfig)
    {
        var stallProtection = oldConfig.StallProtection != newConfig.StallProtection;

        var motorStrength = newConfig.MotorStrengthEnabled
            && (!oldConfig.MotorStrengthEnabled || oldConfig.MotorStrength != newConfig.MotorStrength);

        var limitChannels = new List<int>();
        for (int channel = 0; channel < newConfig.Limits.Length; channel++)
        {
            var oldLimit = channel < oldConfig.Limits.Length ? oldConfig.Limits[channel] : default;
            var newLimit = newConfig.Limits[channel];

            // a changed motor type changes the units of the limits, so it is detected here as well
            if (newLimit.Enabled && oldLimit != newLimit)
            {
                limitChannels.Add(channel);
            }
        }

        return new MKH4ConfigChanges(stallProtection, motorStrength, limitChannels);
    }
}
