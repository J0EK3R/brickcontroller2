// Tests of the Windows-free port name list helpers behind RegistryStickLocator.

using System;
using System.Collections.Generic;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using FluentAssertions;
using Xunit;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Verifies de-duplication, ordering and registry intersection of candidate port names.
/// </summary>
public class StickPortListTests
{
    [Fact]
    public void Normalize_RemovesDuplicatesCaseInsensitivelyAndSorts()
    {
        var result = StickPortList.Normalize(["COM9", "com3", "COM9", "COM3", ""]);

        result.Should().Equal(new[] { "com3", "COM9" });
    }

    [Fact]
    public void IntersectWithRegistry_KeepsOnlyRegistryPortsOnceEach()
    {
        var registry = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "com9" };

        var result = StickPortList.IntersectWithRegistry(["COM9", "COM9", "COM4"], registry);

        result.Should().Equal(new[] { "COM9" }, "a port listed twice is probed only once");
    }

    [Fact]
    public void IntersectWithRegistry_EmptyRegistry_GivesNoCandidate()
    {
        StickPortList.IntersectWithRegistry(["COM4"], new HashSet<string>()).Should().BeEmpty();
    }
}
