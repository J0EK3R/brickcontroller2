using System.Collections.Generic;

namespace BrickController2.PlatformServices.BluetoothLE
{
    public class ScanResult
    {
        public ScanResult(string deviceName, string deviceAddress, IDictionary<byte, byte[]> advertismentData)
        {
            DeviceName = deviceName;
            DeviceAddress = deviceAddress;
            AdvertismentData = advertismentData;
        }

        public string DeviceName { get; set; }
        public string DeviceAddress { get; set; }
        public IDictionary<byte, byte[]> AdvertismentData { get; }
    }
}
