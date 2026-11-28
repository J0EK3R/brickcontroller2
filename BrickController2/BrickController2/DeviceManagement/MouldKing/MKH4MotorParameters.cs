// Per motor type parameters of the MKH4.0 module.
namespace BrickController2.DeviceManagement.MouldKing;

/// <summary>
/// Parameters of one motor type. Each type has its own instance, so the values of one motor size can be
/// changed without touching the others, even if some of them are equal today.
/// </summary>
/// <param name="UnitsPerTurn">Encoder position units of one revolution of the motor shaft.</param>
internal readonly record struct MKH4MotorParameters(int UnitsPerTurn)
{
    /// <summary>
    /// Parameters of the XL motor (594000 units per turn, 1650 units per degree, measured).
    /// </summary>
    internal static readonly MKH4MotorParameters XL = new(594000);

    /// <summary>
    /// Parameters of the L motor.
    /// </summary>
    internal static readonly MKH4MotorParameters L = new(395000);

    /// <summary>
    /// Parameters of the M motor.
    /// </summary>
    internal static readonly MKH4MotorParameters M = new(395000);

    /// <summary>
    /// Encoder units per degree (XL: 1650).
    /// </summary>
    public double UnitsPerDegree => UnitsPerTurn / 360.0;

    /// <summary>
    /// Encoder units of one percent of a revolution (XL: 5940).
    /// </summary>
    public double UnitsPerPercent => UnitsPerTurn / 100.0;

    /// <summary>
    /// Returns the parameters of a motor type. Undefined values fall back to the XL motor.
    /// </summary>
    internal static MKH4MotorParameters For(MKH4MotorType type) => type switch
    {
        MKH4MotorType.L => L,
        MKH4MotorType.M => M,
        _ => XL,
    };
}
