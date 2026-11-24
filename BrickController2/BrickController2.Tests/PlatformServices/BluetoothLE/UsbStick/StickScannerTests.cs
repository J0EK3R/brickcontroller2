// Scan merge tests: raw scan reports of the simulated stick must produce ScanResults that BC2's real device
// managers recognise (DUPLO, WeDo 2.0, MouldKing DIY, SBrick, BuWizz 3, CircuitCubes).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BrickController2.DeviceManagement;
using BrickController2.DeviceManagement.BuWizz;
using BrickController2.DeviceManagement.Lego;
using BrickController2.DeviceManagement.MouldKing;
using BrickController2.PlatformServices.BluetoothLE;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using BrickController2.UI.Services.AppIdentifier;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

public class StickScannerTests
{
    /// <summary>Starts a scan on a fresh harness and collects every ScanResult.</summary>
    private static async Task<(StickHarness H, StickScanner Scanner, List<ScanResult> Results, StickAddressBook Book)> StartScanAsync()
    {
        var h = new StickHarness(maxConns: 4);
        await h.ConnectStickAsync();
        var results = new List<ScanResult>();
        var book = new StickAddressBook();
        var scanner = new StickScanner(h.Connection, r => { lock (results) { results.Add(r); } }, book, NullLogger.Instance);
        (await h.RunAsync(scanner.StartAsync())).Should().BeTrue();
        return (h, scanner, results, book);
    }

    private static IBluetoothLEDeviceManager[] Managers()
    {
        var appId = new Mock<IAppIdentifierService>();
        appId.Setup(x => x.GetAppId(2)).Returns(new byte[] { 0x61, 0x62 });
        return
        [
            new LegoDeviceManager(),
            new MouldKingDeviceManager(appId.Object),
            new SBrickDeviceManager(),
            new BuWizzDeviceManager(),
            new CircuitCubeDeviceManager(),
        ];
    }

    /// <summary>Runs every manager over the results (last first) and returns the first match.</summary>
    private static FoundDevice? Recognise(IEnumerable<ScanResult> results)
    {
        foreach (var result in results.Reverse())
        {
            foreach (var manager in Managers())
            {
                if (manager.TryGetDevice(result, out var device))
                {
                    return device;
                }
            }
        }

        return null;
    }

    [Fact]
    public async Task Scan_IsAlwaysActive()
    {
        var (h, scanner, _, _) = await StartScanAsync();
        using var _h = h;

        h.Stick!.LastScanFlags.Should().Be(StickProtocol.ScanFlagActive);
        await h.RunAsync(scanner.StopAsync());
    }

    [Fact]
    public async Task Duplo_NameOnlyInScanResponse_IsRecognisedAfterBothPdus()
    {
        var (h, _, results, _) = await StartScanAsync();
        using var _h = h;
        var duplo = h.Stick!.AddPeripheral(GattFixtures.Duplo());

        h.Stick.SendScanReport(duplo, duplo.AdvData, scanResponse: false);
        Recognise(results).Should().BeNull("without a name the hub is not recognised as DUPLO (like on Windows)");
        h.Stick.SendScanReport(duplo, duplo.ScanResponseData, scanResponse: true);

        var device = Recognise(results);
        device.Should().NotBeNull();
        device!.Value.DeviceType.Should().Be(DeviceType.DuploTrainHub);
        device.Value.DeviceName.Should().Be("Train Base");
        device.Value.DeviceAddress.Should().Be(GattFixtures.DuploAddress);
    }

    [Fact]
    public async Task Duplo_NextAdvertisingPduAfterNameWasSeen_IsRecognisedOnItsOwn()
    {
        var (h, _, results, _) = await StartScanAsync();
        using var _h = h;
        var duplo = h.Stick!.AddPeripheral(GattFixtures.Duplo());
        h.Stick.EmitAdvertising(duplo);
        results.Clear();

        h.Stick.SendScanReport(duplo, duplo.AdvData, scanResponse: false); // periodic refresh of the stick

        Recognise(results)!.Value.DeviceType.Should().Be(DeviceType.DuploTrainHub);
    }

    [Fact]
    public async Task Wedo2_ServiceInAdvertisement_NameInScanResponse()
    {
        var (h, _, results, _) = await StartScanAsync();
        using var _h = h;
        var wedo = h.Stick!.AddPeripheral(GattFixtures.Wedo2());

        h.Stick.EmitAdvertising(wedo);

        var device = Recognise(results);
        device!.Value.DeviceType.Should().Be(DeviceType.WeDo2);
        device.Value.DeviceName.Should().Be("LPF2 Smart Hub 2 I/O");
    }

    [Fact]
    public async Task MkDiy_NeedsNoName()
    {
        var (h, _, results, _) = await StartScanAsync();
        using var _h = h;
        var mk = h.Stick!.AddPeripheral(GattFixtures.MkDiy());

        h.Stick.EmitAdvertising(mk);

        Recognise(results)!.Value.DeviceType.Should().Be(DeviceType.MK_DIY);
        results.Last().DeviceName.Should().BeEmpty();
    }

    [Fact]
    public async Task SBrick_ManufacturerDataWithProductRecord()
    {
        var (h, _, results, _) = await StartScanAsync();
        using var _h = h;
        // 0x0198 Vengit, record: len 2, type 0x00 (product), id 0x00 (SBrick)
        var sbrick = h.Stick!.AddPeripheral(new FakePeripheral("AA:BB:CC:00:00:05") { AdvData = GattFixtures.Ad(0xFF, 0x98, 0x01, 0x02, 0x00, 0x00) });

        h.Stick.EmitAdvertising(sbrick);

        Recognise(results)!.Value.DeviceType.Should().Be(DeviceType.SBrick);
    }

    [Fact]
    public async Task BuWizz3_NameInScanResponse_GetsNameEntryAddedToTheData()
    {
        var (h, _, results, _) = await StartScanAsync();
        using var _h = h;
        var bw = h.Stick!.AddPeripheral(new FakePeripheral("AA:BB:CC:00:00:06")
        {
            AdvData = GattFixtures.Ad(0xFF, 0x4E, 0x05, 0x42, 0x57, 0x03, 0x01),
            ScanResponseData = GattFixtures.AdName("BuWizz3"),
        });

        h.Stick.EmitAdvertising(bw);

        Recognise(results)!.Value.DeviceType.Should().Be(DeviceType.BuWizz3);
        results.Last().TryGetCompleteLocalName(out var name).Should().BeTrue("BuWizz 3 requires the 0x09 entry");
        System.Text.Encoding.ASCII.GetString(name).Should().Be("BuWizz3");
    }

    [Fact]
    public async Task CircuitCubes_ServiceUuidAndName()
    {
        var (h, _, results, _) = await StartScanAsync();
        using var _h = h;
        var cube = h.Stick!.AddPeripheral(new FakePeripheral("AA:BB:CC:00:00:07")
        {
            AdvData = GattFixtures.Ad(0x07, GattFixtures.Wire(new Guid("6e400001-b5a3-f393-e0a9-e50e24dcca9e"))),
            ScanResponseData = GattFixtures.AdName("Tenka"),
        });

        h.Stick.EmitAdvertising(cube);

        Recognise(results)!.Value.DeviceType.Should().Be(DeviceType.CircuitCubes);
    }

    [Fact]
    public async Task OnlyTheFourAdTypes_AreForwarded()
    {
        var (h, _, results, _) = await StartScanAsync();
        using var _h = h;
        var p = h.Stick!.AddPeripheral(new FakePeripheral("AA:BB:CC:00:00:08")
        {
            AdvData = [.. GattFixtures.Ad(0x01, 0x06), .. GattFixtures.Ad(0x0A, 0x04), .. GattFixtures.Ad(0xFF, 1, 2, 3)],
        });

        h.Stick.EmitAdvertising(p);

        results.Last().AdvertismentData.Keys.Should().BeEquivalentTo(new byte[] { 0xFF });
    }

    [Fact]
    public async Task ExistingNameEntry_IsNotDuplicatedOrOverwritten()
    {
        var (h, _, results, _) = await StartScanAsync();
        using var _h = h;
        var p = h.Stick!.AddPeripheral(new FakePeripheral("AA:BB:CC:00:00:09") { AdvData = [.. GattFixtures.AdName("Own"), .. GattFixtures.Ad(0xFF, 1, 2)] });

        h.Stick.EmitAdvertising(p);

        results.Last().DeviceName.Should().Be("Own");
        results.Last().AdvertismentData[0x09].Should().Equal("Own"u8.ToArray());
    }

    [Fact]
    public async Task NothingToReport_PduWithoutDataOrName_IsSkipped()
    {
        var (h, _, results, _) = await StartScanAsync();
        using var _h = h;
        var p = h.Stick!.AddPeripheral(new FakePeripheral("AA:BB:CC:00:00:0A") { AdvData = GattFixtures.Ad(0x01, 0x06) });

        h.Stick.EmitAdvertising(p);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task AddressBook_LearnsAddressTypes()
    {
        var (h, _, _, book) = await StartScanAsync();
        using var _h = h;
        var random = h.Stick!.AddPeripheral(new FakePeripheral("AA:BB:CC:00:00:0B", addressType: 1) { AdvData = GattFixtures.Ad(0xFF, 1, 2) });
        var pub = h.Stick.AddPeripheral(new FakePeripheral("AA:BB:CC:00:00:0C", addressType: 0) { AdvData = GattFixtures.Ad(0xFF, 1, 2) });

        h.Stick.EmitAdvertising(random);
        h.Stick.EmitAdvertising(pub);

        book.GetAddressType("AA:BB:CC:00:00:0B").Should().Be(1);
        book.GetAddressType("aa:bb:cc:00:00:0c").Should().Be(0);
        book.GetAddressType("11:22:33:44:55:66").Should().Be(0xFF, "unknown device: let the stick decide");
        book.WasSeen("AA:BB:CC:00:00:0B").Should().BeTrue();
        book.WasSeen("11:22:33:44:55:66").Should().BeFalse();
    }

    [Fact]
    public async Task Stop_SendsScanStop_AndForgetsNames()
    {
        var (h, scanner, results, _) = await StartScanAsync();
        using var _h = h;
        var duplo = h.Stick!.AddPeripheral(GattFixtures.Duplo());
        h.Stick.EmitAdvertising(duplo);

        await h.RunAsync(scanner.StopAsync());

        h.Stick.ScanStopCount.Should().Be(1);
        h.Stick.Scanning.Should().BeFalse();

        // a second scan starts from scratch: the first ADV is nameless again
        results.Clear();
        (await h.RunAsync(scanner.StartAsync())).Should().BeTrue();
        h.Stick.SendScanReport(duplo, duplo.AdvData, scanResponse: false);
        Recognise(results).Should().BeNull();
    }

    [Fact]
    public async Task ScanTwice_StartsAndStopsTheStickScanTwice()
    {
        var (h, scanner, _, _) = await StartScanAsync();
        using var _h = h;

        await h.RunAsync(scanner.StopAsync());
        (await h.RunAsync(scanner.StartAsync())).Should().BeTrue();
        await h.RunAsync(scanner.StopAsync());

        h.Stick!.ScanStartCount.Should().Be(2);
        h.Stick.ScanStopCount.Should().Be(2);
    }

    [Fact]
    public async Task FailingCallback_DoesNotBreakTheReadLoop()
    {
        using var h = new StickHarness(maxConns: 4);
        await h.ConnectStickAsync();
        int calls = 0;
        var scanner = new StickScanner(h.Connection, _ => { calls++; throw new InvalidOperationException("boom"); }, new StickAddressBook(), NullLogger.Instance);
        await h.RunAsync(scanner.StartAsync());
        var p = h.Stick!.AddPeripheral(GattFixtures.MkDiy());

        h.Stick.EmitAdvertising(p);
        h.Stick.EmitAdvertising(p);

        calls.Should().Be(2);
        h.Connection.Status.State.Should().Be(UsbStickState.Connected);
    }

    [Fact]
    public async Task ScanReportsAreNotAnswered_UnknownEventsIgnored()
    {
        var (h, _, results, _) = await StartScanAsync();
        using var _h = h;

        h.Stick!.SendEvent(0xC9, [1, 2, 3]); // a future event type
        h.Stick.SendEvent(StickProtocol.TypeEvtDropped, [1, 5, 0]);
        h.Stick.SendEvent(StickProtocol.TypeEvtScanReport, [1, 2]); // malformed

        results.Should().BeEmpty();
        h.Connection.Status.State.Should().Be(UsbStickState.Connected);
    }
}
