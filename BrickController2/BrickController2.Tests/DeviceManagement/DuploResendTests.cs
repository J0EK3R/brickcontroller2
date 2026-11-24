// Test of the DUPLO train hub motor resend with a slow write-with-response (slow BLE link: 100+ ms per write).
// The hub needs the motor command at least every ~500 ms; the resend deadline therefore has to count from the START
// of the previous send, not from its completion (otherwise 500 ms + write latency passes and the motor stutters).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BrickController2.DeviceManagement;
using BrickController2.PlatformServices.BluetoothLE;
using BrickController2.Protocols;
using FluentAssertions;
using Moq;
using Xunit;

namespace BrickController2.Tests.DeviceManagement;

/// <summary>Effective resend interval of <see cref="DuploTrainHubDevice"/> with artificial write latency.</summary>
public class DuploResendTests
{
    private const int WriteLatencyMs = 150;

    /// <summary>
    /// With 150 ms per write the identical motor command must still be repeated every ~500 ms (old behaviour:
    /// 500 ms + 150 ms + loop delay = 660 ms, which made the DUPLO motor stutter).
    /// </summary>
    [Fact]
    public async Task MotorCommand_IsResentWithin520ms_EvenWhenWriteTakes150ms()
    {
        var starts = new List<(long Ticks, byte[] Data)>();
        var device = CreateConnectedDevice(starts, out var bleDevice);
        var result = await device.ConnectAsync(false, _ => { }, Array.Empty<ChannelConfiguration>(), startOutputProcessing: true, requestDeviceInformation: false, CancellationToken.None);
        result.Should().Be(DeviceConnectionResult.Ok);

        device.SetOutput(0, 0.5f);
        await Task.Delay(TimeSpan.FromSeconds(3.5));
        await device.DisconnectAsync();

        // Motor commands only (output command 0x81 on the port); property/other writes are not part of this.
        List<long> motor;
        lock (starts)
        {
            motor = starts.Where(s => s.Data.Length > 2 && s.Data[2] == 0x81).Select(s => s.Ticks).ToList();
        }

        motor.Count.Should().BeGreaterThan(4);
        // After the initial burst of MAX_SEND_ATTEMPTS sends, only resends remain: look at the gaps of the tail.
        var gaps = motor.Zip(motor.Skip(1), (a, b) => Stopwatch.GetElapsedTime(a, b).TotalMilliseconds).TakeLast(3).ToList();
        gaps.Should().OnlyContain(g => g <= 540, "the resend interval is measured from the start of the previous send (<= ~520 ms)");
        gaps.Should().OnlyContain(g => g >= 450, "no flood: the command is resent about every 500 ms");
    }

    /// <summary>Creates a DUPLO device on top of a fake BLE device whose writes record their start and take 150 ms.</summary>
    private static DuploTrainHubDevice CreateConnectedDevice(List<(long Ticks, byte[] Data)> starts, out Mock<IBluetoothLEDevice> bleDevice)
    {
        var characteristic = Mock.Of<IGattCharacteristic>(c => c.Uuid == LegoWirelessProtocol.CharacteristicUuid);
        var service = Mock.Of<IGattService>(s => s.Uuid == LegoWirelessProtocol.ServiceUuid && s.Characteristics == new[] { characteristic });

        bleDevice = new Mock<IBluetoothLEDevice>();
        bleDevice.Setup(d => d.ConnectAndDiscoverServicesAsync(It.IsAny<bool>(), It.IsAny<Action<Guid, byte[]?>>(), It.IsAny<Action<IBluetoothLEDevice>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { service });
        bleDevice.Setup(d => d.EnableNotificationAsync(It.IsAny<IGattCharacteristic>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        bleDevice.Setup(d => d.DisconnectAsync()).Returns(Task.CompletedTask);
        bleDevice.Setup(d => d.WriteAsync(It.IsAny<IGattCharacteristic>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(async (IGattCharacteristic _, byte[] data, CancellationToken token) =>
            {
                lock (starts) { starts.Add((Stopwatch.GetTimestamp(), data)); }
                await Task.Delay(WriteLatencyMs, token);   // ACK latency of a slow link
                return true;
            });

        var ble = new Mock<IBluetoothLEService>();
        ble.Setup(s => s.GetKnownDeviceAsync(It.IsAny<string>())).ReturnsAsync(bleDevice.Object);

        return new DuploTrainHubDevice("Train", "D0:2E:AB:D3:E1:E7", [], Mock.Of<IDeviceRepository>(), ble.Object);
    }
}
