using System;
using System.Linq;
using System.Threading.Tasks;
using BrickController2.PlatformServices.BluetoothLE;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// The IBluetoothLEAdvertiserDevice on top of a stick set: mapping of BC2's values to frames.
/// </summary>
public class StickAdvertiserDeviceTests
{
    private static StickAdvertiserDevice CreateDevice(StickHarness h, StickSetLease lease)
        => new(lease, NullLogger.Instance);

    [Fact]
    public async Task Start_WithBc2Defaults_SendsInterval32TxPower127AndAndroidFlags()
    {
        using var h = new StickHarness();
        var device = CreateDevice(h, await h.ConnectAndLeaseAsync());
        byte[] data = Enumerable.Range(1, 24).Select(i => (byte)i).ToArray();

        await device.StartAdvertiseAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 0xFFF0, data);

        var set = h.Stick!.Sets[0];
        set.Interval.Should().Be(32);
        set.Tx.Should().Be(127);
        set.Flags.Should().Be(0x01, "AD flags field like Android, ADV_IND");
        set.Mfr.Should().Be(0xFFF0);
        set.Data.Should().Equal(data);
    }

    [Fact]
    public async Task Start_WithWindowsLayout_StripsThe3DummyBytes()
    {
        using var h = new StickHarness();
        var device = CreateDevice(h, await h.ConnectAndLeaseAsync());
        byte[] android = Enumerable.Range(1, 24).Select(i => (byte)i).ToArray();
        byte[] windows = new byte[3].Concat(android).ToArray();

        await device.StartAdvertiseAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 0xFFF0, windows);

        h.Stick!.Sets[0].Data.Should().Equal(android);
    }

    [Fact]
    public async Task Update_ReplacesDataAndManufacturerId()
    {
        using var h = new StickHarness();
        var device = CreateDevice(h, await h.ConnectAndLeaseAsync());
        await device.StartAdvertiseAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 0xFFF0, [1, 2, 3]);

        await device.UpdateAdvertisedDataAsync(0xC200, [4, 5]);

        h.Stick!.Sets[0].Data.Should().Equal(4, 5);
        h.Stick.Sets[0].Mfr.Should().Be(0xC200);
    }

    [Fact]
    public async Task Update_BeforeStart_SendsNothing()
    {
        using var h = new StickHarness();
        var device = CreateDevice(h, await h.ConnectAndLeaseAsync());

        await device.UpdateAdvertisedDataAsync(0xFFF0, [1]);

        h.Stick!.Requests(StickProtocol.TypeSetUpdate).Should().BeEmpty();
        h.Stick.Requests(StickProtocol.TypeSetStart).Should().BeEmpty();
    }

    [Fact]
    public async Task Update_AfterStop_SendsNothing()
    {
        using var h = new StickHarness();
        var device = CreateDevice(h, await h.ConnectAndLeaseAsync());
        await device.StartAdvertiseAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 0xFFF0, [1]);
        await device.StopAdvertiseAsync();

        await device.UpdateAdvertisedDataAsync(0xFFF0, [2]);

        h.Stick!.Sets.Should().BeEmpty();
        h.Stick.Requests(StickProtocol.TypeSetUpdate).Should().BeEmpty();
    }

    [Fact]
    public async Task Stop_SendsSetStop_AndStartAgainWorks()
    {
        using var h = new StickHarness();
        var device = CreateDevice(h, await h.ConnectAndLeaseAsync());
        await device.StartAdvertiseAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 0xFFF0, [1]);

        await device.StopAdvertiseAsync();
        h.Stick!.Sets.Should().BeEmpty();

        await device.StartAdvertiseAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 0xFFF0, [3]);
        h.Stick.Sets[0].Data.Should().Equal(3);
    }

    [Fact]
    public async Task Start_PayloadLongerThan24AfterNormalising_IsNotSent()
    {
        using var h = new StickHarness();
        var device = CreateDevice(h, await h.ConnectAndLeaseAsync());

        await device.StartAdvertiseAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 0xFFF0, new byte[40]);

        h.Stick!.Requests(StickProtocol.TypeSetStart).Should().BeEmpty();
    }

    [Fact]
    public async Task Start_WhileStickIsAway_IsSentAfterReplug()
    {
        using var h = new StickHarness();
        var device = CreateDevice(h, await h.ConnectAndLeaseAsync());
        h.Locator.Ports.Clear();
        h.Locator.Created.Last().Unplug();
        await h.PumpUntilAsync(() => h.Connection.Status.State == UsbStickState.Lost);

        await device.StartAdvertiseAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 0xFFF0, [7, 7]);

        var newStick = h.Plug("COM7");
        await h.PumpUntilAsync(() => newStick.Sets.ContainsKey(0));
        newStick.Sets[0].Data.Should().Equal(7, 7);
    }

    [Fact]
    public async Task Dispose_StopsTheSetAndFreesTheId()
    {
        using var h = new StickHarness();
        var device = CreateDevice(h, await h.ConnectAndLeaseAsync());
        await device.StartAdvertiseAsync(AdvertisingInterval.Min, TxPowerLevel.Max, 0xFFF0, [1]);

        device.Dispose();
        await h.PumpUntilAsync(() => h.Connection.LeaseCount == 0);

        h.Stick!.Sets.Should().BeEmpty();
        h.Connection.TryAcquireLease()!.SetId.Should().Be(0);
    }

    [Fact]
    public void ParameterMapper_MapsIntervalsAndPower()
    {
        StickParameterMapper.ToInterval(AdvertisingInterval.Min).Should().Be(32);
        StickParameterMapper.ToInterval(AdvertisingInterval.Low).Should().Be(160);
        StickParameterMapper.ToInterval(AdvertisingInterval.Medium).Should().Be(400);
        StickParameterMapper.ToInterval(AdvertisingInterval.High).Should().Be(1600);
        StickParameterMapper.ToInterval(AdvertisingInterval.Max).Should().Be(16384);
        StickParameterMapper.ToTxPower(TxPowerLevel.Max).Should().Be(127);
        StickParameterMapper.ToTxPower(TxPowerLevel.Min).Should().BeLessThan(StickParameterMapper.ToTxPower(TxPowerLevel.High));
    }

    [Fact]
    public void ParameterMapper_ClampsIntervalIntoStickRange()
    {
        var info = new StickInfo(1, 0, "0.1.0", 5, 24, 27, 100, 200, 1000, "id");

        StickParameterMapper.ClampInterval(32, info).Should().Be(100);
        StickParameterMapper.ClampInterval(16384, info).Should().Be(200);
        StickParameterMapper.ClampInterval(32, null).Should().Be(32);
    }
}
