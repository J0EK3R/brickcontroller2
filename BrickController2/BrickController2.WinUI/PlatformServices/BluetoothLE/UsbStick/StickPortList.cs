// Windows-free helpers that turn raw port name lists into the candidate list. Kept out of RegistryStickLocator
// (which needs the registry and is excluded from the Linux test project) so that they are unit tested.

using System;
using System.Collections.Generic;
using System.Linq;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Normalisation of COM port name lists.
/// </summary>
public static class StickPortList
{
    /// <summary>
    /// Sorted, case-insensitively de-duplicated copy of the names. <c>SerialPort.GetPortNames()</c> can return the same
    /// port twice, and a port must never be probed twice in one run.
    /// </summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string> portNames) =>
        portNames
            .Where(p => !string.IsNullOrEmpty(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// The existing ports that the registry lists for the stick identity, normalised.
    /// </summary>
    public static IReadOnlyList<string> IntersectWithRegistry(IEnumerable<string> existingPorts, ISet<string> registryPorts) =>
        Normalize(existingPorts.Where(p => registryPorts.Contains(p)));
}
