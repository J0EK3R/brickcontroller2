// Peripheral definitions and small helpers shared by the GATT tests (device-shaped scenarios of DUPLO,
// MouldKing DIY and WeDo 2.0 plus a generic peripheral).

using System;
using System.Linq;
using System.Text;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;

namespace BrickController2.Tests.PlatformServices.BluetoothLE.UsbStick;

/// <summary>Builders for simulated peripherals and raw advertising data.</summary>
internal static class GattFixtures
{
    public const string GenericAddress = "AA:BB:CC:00:00:01";
    public const string DuploAddress = "AA:BB:CC:00:00:02";
    public const string MkDiyAddress = "AA:BB:CC:00:00:03";
    public const string Wedo2Address = "AA:BB:CC:00:00:04";

    /// <summary>Property bits of the ATT characteristic declaration.</summary>
    public const byte PropRead = 0x02, PropWriteNoRsp = 0x04, PropWrite = 0x08, PropNotify = 0x10;

    /// <summary>Converts a Guid to the 16 byte little-endian ATT wire order the stick uses.</summary>
    public static byte[] Wire(Guid guid)
    {
        byte[] big = new byte[16];
        guid.TryWriteBytes(big, bigEndian: true, out _);
        return big.Reverse().ToArray();
    }

    /// <summary>One AD structure: len, type, data.</summary>
    public static byte[] Ad(byte type, params byte[] data) => [(byte)(data.Length + 1), type, .. data];

    /// <summary>AD structure with a UTF-8 name.</summary>
    public static byte[] AdName(string name, byte type = 0x09) => Ad(type, Encoding.UTF8.GetBytes(name));

    /// <summary>
    /// Generic peripheral: service 0x180A with a readable characteristic 0x2A26 (handle 0x12, value "1.0"), a
    /// service 0xFFF0 with a write characteristic (handle 0x21), a write-no-response one (0x23) and a notify
    /// characteristic (value handle 0x25, CCCD 0x26).
    /// </summary>
    public static FakePeripheral Generic(string address = GenericAddress)
    {
        var p = new FakePeripheral(address)
            .WithService(0x180A, 0x0010, 0x0014)
            .WithChar(0x2A26, 0x0012, 0, PropRead)
            .WithService(0xFFF0, 0x0020, 0x0030)
            .WithChar(0xFFF1, 0x0021, 0, PropWrite)
            .WithChar(0xFFF2, 0x0023, 0, PropWriteNoRsp)
            .WithChar(0xFFF3, 0x0025, 0x0026, PropNotify);
        p.ReadValues[0x0012] = [0x31, 0x2E, 0x30];
        return p;
    }

    /// <summary>LEGO DUPLO train hub: LWP3 service 00001623-..., one characteristic 00001624-... (write + notify).</summary>
    public static FakePeripheral Duplo(string address = DuploAddress)
    {
        var service = new Guid("00001623-1212-efde-1623-785feabcd123");
        var chr = new Guid("00001624-1212-efde-1623-785feabcd123");
        var p = new FakePeripheral(address)
        {
            AdvData = [.. Ad(0x01, 0x06), .. Ad(0xFF, 0x97, 0x03, 0x00, 0x20, 0x00, 0x41, 0x00), .. Ad(0x07, Wire(service))],
            ScanResponseData = AdName("Train Base"),
        };
        p.Services.Add(new FakeService(0, 0x0010, 0x0020, Wire(service)));
        p.Chars.Add(new FakeChar(0, 0x0012, 0x0013, (byte)(PropRead | PropWriteNoRsp | PropWrite | PropNotify), Wire(chr)));
        p.ReadValues[0x0012] = [0x0F, 0x00, 0x01];
        return p;
    }

    /// <summary>MouldKing DIY: manufacturer id 0xAC33, no name needed, write-no-response characteristic.</summary>
    public static FakePeripheral MkDiy(string address = MkDiyAddress)
    {
        var p = new FakePeripheral(address)
        {
            AdvData = Ad(0xFF, 0x33, 0xAC, 0x01, 0x02, 0x03),
        };
        p.WithService(0xFFE0, 0x0010, 0x0020).WithChar(0xFFE1, 0x0012, 0, PropWriteNoRsp);
        return p;
    }

    /// <summary>LEGO WeDo 2.0: service 00001523-..., MTU 23, motor / input / sensor characteristics, firmware 0x2A26.</summary>
    public static FakePeripheral Wedo2(string address = Wedo2Address)
    {
        var service = new Guid("00001523-1212-efde-1523-785feabcd123");
        Guid Chr(string id) => new($"0000{id}-1212-efde-1523-785feabcd123");
        var p = new FakePeripheral(address)
        {
            Mtu = 23,
            AdvData = [.. Ad(0x01, 0x06), .. Ad(0x06, Wire(service))],
            ScanResponseData = AdName("LPF2 Smart Hub 2 I/O"),
        };
        p.Services.Add(new FakeService(0, 0x0010, 0x0020, Wire(service)));
        p.WithService(0x180A, 0x0030, 0x0034).WithChar(0x2A26, 0x0032, 0, PropRead);
        p.Services.Add(new FakeService(0, 0x0040, 0x0060, Wire(new Guid("00004f0e-1212-efde-1523-785feabcd123"))));
        p.Chars.Add(new FakeChar(0, 0x0042, 0x0043, PropNotify, Wire(Chr("1560"))));
        p.Chars.Add(new FakeChar(0, 0x0045, 0, PropWrite, Wire(Chr("1563"))));
        p.Chars.Add(new FakeChar(0, 0x0047, 0, PropWrite | PropWriteNoRsp, Wire(Chr("1565"))));
        p.ReadValues[0x0032] = Encoding.ASCII.GetBytes("1.1.0");
        return p;
    }
}
