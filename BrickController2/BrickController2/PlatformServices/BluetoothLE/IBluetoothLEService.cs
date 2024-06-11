using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BrickController2.PlatformServices.BluetoothLE
{
    public interface IBluetoothLEService
    {
        string DeviceID { get; }

        bool IsBluetoothLESupported { get; }
        bool IsBluetoothOn { get; }

        Task<bool> ScanDevicesAsync(Action<ScanResult> scanCallback, IEnumerable<Tuple<ushort, byte[]>> advertiseList, CancellationToken token);

        IBluetoothLEDevice? GetKnownDevice(string address);

        IBluetoothLEAdvertiserDevice GetBluetoothLEAdvertiserDevice();
    }
}
