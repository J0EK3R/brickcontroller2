using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BrickController2.PlatformServices.BluetoothLE;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using BrickController2.UI.Services.Preferences;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Selection logic: setting Auto/Off, fallback when no stick is present or all 5 sets are in use.
/// </summary>
public class UsbAdvertiserServiceTests
{
    private static (UsbAdvertiserService Service, StickHarness Harness) Create(bool plugged)
    {
        // Real clock/connection with the fake stick: TryCreateAdvertiserDevice probes synchronously.
        var store = new Dictionary<string, object>();
        var prefs = new Mock<IPreferencesService>();
        prefs.Setup(p => p.Get(It.IsAny<string>(), It.IsAny<UsbAdvertiserMode>(), It.IsAny<string?>()))
            .Returns((string key, UsbAdvertiserMode def, string? _) => store.TryGetValue(key, out var v) ? (UsbAdvertiserMode)v : def);
        prefs.Setup(p => p.Set(It.IsAny<string>(), It.IsAny<UsbAdvertiserMode>(), It.IsAny<string?>()))
            .Callback((string key, UsbAdvertiserMode value, string? _) => store[key] = value);

        var harness = new StickHarness(plugged);
        var service = new UsbAdvertiserService(harness.Connection, prefs.Object, NullLoggerFactory.Instance);
        return (service, harness);
    }

    [Fact]
    public void Mode_DefaultsToAuto_AndIsPersisted()
    {
        var (service, h) = Create(plugged: false);
        using var _ = h;

        service.Mode.Should().Be(UsbAdvertiserMode.Auto);
        service.Mode = UsbAdvertiserMode.Off;
        service.Mode.Should().Be(UsbAdvertiserMode.Off);
    }

    [Fact]
    public void Mode_CorruptedPreference_FallsBackToAuto()
    {
        var prefs = new Mock<IPreferencesService>();
        prefs.Setup(p => p.Get(It.IsAny<string>(), It.IsAny<UsbAdvertiserMode>(), It.IsAny<string?>()))
            .Throws(new System.ArgumentException("Requested value 'garbage' was not found."));
        using var h = new StickHarness(plugged: false);
        var service = new UsbAdvertiserService(h.Connection, prefs.Object, NullLoggerFactory.Instance);

        service.Mode.Should().Be(UsbAdvertiserMode.Auto);
    }

    [Fact]
    public void TryCreate_NoStick_ReturnsNullSoWindowsAdvertiserIsUsed()
    {
        var (service, h) = Create(plugged: false);
        using var _ = h;

        service.TryCreateAdvertiserDevice().Should().BeNull();
    }

    [Fact]
    public void TryCreate_ModeOff_ReturnsNullEvenWithStick()
    {
        var (service, h) = Create(plugged: true);
        using var _ = h;
        service.Mode = UsbAdvertiserMode.Off;

        service.TryCreateAdvertiserDevice().Should().BeNull();
        h.Connection.Status.State.Should().NotBe(UsbStickState.Connected, "Off must not even open the port");
    }

    [Fact]
    public void TryCreate_Stick_ReturnsDeviceAndSixthFallsBack()
    {
        // The probe needs a moving clock only on timeouts; with the plugged fake stick answers are immediate.
        var (service, h) = Create(plugged: true);
        using var _ = h;

        var devices = Enumerable.Range(0, 5).Select(_ => service.TryCreateAdvertiserDevice()).ToList();

        devices.Should().OnlyContain(d => d is StickAdvertiserDevice);
        service.TryCreateAdvertiserDevice().Should().BeNull("the 6th device falls back to the Windows advertiser");
    }

    [Fact]
    public async Task StatusChanged_IsRaisedWhenTheConnectionChanges()
    {
        var (service, h) = Create(plugged: true);
        using var _ = h;
        int raised = 0;
        service.StatusChanged += (_, _) => raised++;

        await h.RunAsync(service.RefreshAsync());

        raised.Should().BeGreaterThan(0);
        service.Status.State.Should().Be(UsbStickState.Connected);
    }

    [Fact]
    public async Task IsStickAvailableAsync_AutoWithStick_IsTrue()
    {
        var (service, h) = Create(plugged: true);
        using var _ = h;
        (await service.IsStickAvailableAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task IsStickAvailableAsync_AutoWithoutStick_IsFalse()
    {
        var (service, h) = Create(plugged: false);
        using var _ = h;
        (await service.IsStickAvailableAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task IsStickAvailableAsync_ModeOff_IsFalseEvenWithStick()
    {
        var (service, h) = Create(plugged: true);
        using var _ = h;
        service.Mode = UsbAdvertiserMode.Off;
        (await service.IsStickAvailableAsync()).Should().BeFalse();
    }
}
