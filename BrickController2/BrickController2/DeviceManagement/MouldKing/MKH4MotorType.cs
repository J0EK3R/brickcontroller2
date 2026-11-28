// Motor type setting of a port of the MKH4.0 module.
namespace BrickController2.DeviceManagement.MouldKing;

/// <summary>
/// Motor type connected to a port of the MKH4.0 module. The type defines how many encoder position units
/// one revolution of the motor shaft has, which is needed for servo angles and for the limit positions.
/// The numeric values are stored in the settings, so never renumber them.
/// </summary>
public enum MKH4MotorType
{
    /// <summary>
    /// XL motor: 594000 position units per revolution (1650 units per degree, measured).
    /// </summary>
    XL = 0,

    /// <summary>
    /// L and M motors: 395000 position units per revolution (about 1097.2 units per degree).
    /// </summary>
    LM = 1,
}
