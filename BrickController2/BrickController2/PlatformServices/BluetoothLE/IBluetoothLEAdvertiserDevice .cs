using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BrickController2.PlatformServices.BluetoothLE
{
    public interface IBluetoothLEAdvertiserDevice :
        IDisposable
    {
        Task<bool> StartAdvertiseAsync(int manufacturerId, byte[] rawData);

        Task<bool> StopAdvertiseAsync();

        bool ChangeAdvertiseAsync(int manufacturerId, byte[] rawData);
    }
}
