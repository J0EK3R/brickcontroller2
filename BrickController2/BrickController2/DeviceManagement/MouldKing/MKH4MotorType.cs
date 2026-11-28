// Motor type setting of a port of the MKH4.0 module.
namespace BrickController2.DeviceManagement.MouldKing;

/// <summary>
/// Motor type (size) connected to a port of the MKH4.0 module. The type defines how many encoder position units
/// one revolution of the motor shaft has, which is needed for servo angles and for the limit positions.
/// The numeric values are stored in the settings, so never renumber them.
/// </summary>
public enum MKH4MotorType
{
    /// <summary>
    /// XL motor. The values are defined in <see cref="MKH4MotorParameters"/>.
    /// </summary>
    XL = 0,

    /// <summary>
    /// L motor. The values are defined in <see cref="MKH4MotorParameters"/>.
    /// (Stored value 1 was the combined "L and M" type before, so existing settings keep their behaviour.)
    /// </summary>
    L = 1,

    /// <summary>
    /// M motor. The values are defined in <see cref="MKH4MotorParameters"/>.
    /// </summary>
    M = 2,
}
