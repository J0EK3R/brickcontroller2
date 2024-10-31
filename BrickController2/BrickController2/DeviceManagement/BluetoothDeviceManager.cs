using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BrickController2.Helpers;
using BrickController2.PlatformServices.BluetoothLE;

namespace BrickController2.DeviceManagement
{
    internal class BluetoothDeviceManager : IBluetoothDeviceManager
    {
        private readonly IBluetoothLEService _bleService;
        private readonly AsyncLock _asyncLock = new AsyncLock();
        private int mobileSerialChecksum;
        private byte[] mobileSerialChecksumMaskArray;

        public BluetoothDeviceManager(IBluetoothLEService bleService)
        {
            _bleService = bleService;

            this.mobileSerialChecksum = 
                (_bleService.DeviceID[0] << 0 ) +
                (_bleService.DeviceID[1] << 8 ) +
                (_bleService.DeviceID[2] << 16) ;

            this.mobileSerialChecksumMaskArray = ArrayTools.CreateMaskArray(mobileSerialChecksum, 3);
        }

        public bool IsBluetoothLESupported => _bleService.IsBluetoothLESupported;
        public bool IsBluetoothOn => _bleService.IsBluetoothOn;

        public async Task<bool> ScanAsync(Func<DeviceType, string, string, byte[], Task> deviceFoundCallback, CancellationToken token)
        {
            List<Tuple<ushort, byte[]>> advertiseList = new List<Tuple<ushort, byte[]>>();

            // MouldKing
            await deviceFoundCallback(DeviceType.MouldKing_15059, "MouldKing Robot", "15059", BitConverter.GetBytes(MouldKing_15059.ManufacturerID));

            await deviceFoundCallback(DeviceType.MouldKing_4_0_Modul, "MouldKing 4.0 Module", MouldKing_4_0_Modul.Device1_3, BitConverter.GetBytes(MouldKing_4_0_Modul.ManufacturerID));

            await deviceFoundCallback(DeviceType.MouldKing_6_0_Modul, "MouldKing 6.0 Module Device 1", MouldKing_6_0_Modul.Device1, BitConverter.GetBytes(MouldKing_6_0_Modul.ManufacturerID));
            await deviceFoundCallback(DeviceType.MouldKing_6_0_Modul, "MouldKing 6.0 Module Device 2", MouldKing_6_0_Modul.Device2, BitConverter.GetBytes(MouldKing_6_0_Modul.ManufacturerID));
            await deviceFoundCallback(DeviceType.MouldKing_6_0_Modul, "MouldKing 6.0 Module Device 3", MouldKing_6_0_Modul.Device3, BitConverter.GetBytes(MouldKing_6_0_Modul.ManufacturerID));

            await deviceFoundCallback(DeviceType.MouldKing_Mecanum_Modul, "MouldKing Mecanum Module", "Mecanum Module", BitConverter.GetBytes(MouldKing_Mecanum_Modul.ManufacturerID));

            // Hogokids
            await deviceFoundCallback(DeviceType.HOGOKIDS_8051, "HOGOKIDS Robot", "8051", BitConverter.GetBytes(HOGOKIDS_8051.ManufacturerID));

            // CaDA
            //await deviceFoundCallback(DeviceType.CaDA_RaceCar, "CaDA RaceCar", "961008", BitConverter.GetBytes(CaDARaceCar.ManufacturerID));
            CaDARaceCar.AddAdvertisingData(this._bleService, advertiseList);

            // TestModel
            //await deviceFoundCallback(DeviceType.TestModel, "TestModel", "TestModel", BitConverter.GetBytes(TestModel.ManufacturerID));


            using (await _asyncLock.LockAsync())
            {
                if (!IsBluetoothOn)
                {
                    return true;
                }

                try
                {
                    return await _bleService.ScanDevicesAsync(
                        async scanResult =>
                        {
                            var deviceInfo = GetDeviceIfo(scanResult);
                            if (deviceInfo.DeviceType != DeviceType.Unknown)
                            {
                                await deviceFoundCallback(deviceInfo.DeviceType, scanResult.DeviceName, scanResult.DeviceAddress, deviceInfo.ManufacturerData);
                            }
                        },
                        advertiseList,
                        token);
                }
                catch (OperationCanceledException)
                {
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        private (DeviceType DeviceType, byte[] ManufacturerData) GetDeviceIfo(ScanResult scanResult)
        {
            IDictionary<byte, byte[]> advertismentData = scanResult?.AdvertismentData;

            if (advertismentData == null)
            {
                return (DeviceType.Unknown, null);
            }

            if (!advertismentData.TryGetValue(0xFF, out var manufacturerData) || manufacturerData.Length < 2)
            {
                return GetDeviceInfoByService(advertismentData);
            }

            var manufacturerDataString = BitConverter.ToString(manufacturerData).ToLower();
            var manufacturerId = manufacturerDataString.Substring(0, 5);

            switch (manufacturerId)
            {
                case "f0-ff":
                    if (manufacturerData.Length == 18 &&
                        manufacturerData[2] == 0x75 &&
                        (manufacturerData[3] & 0x40) > 0 &&
                        manufacturerData[7] == this.mobileSerialChecksumMaskArray[0] && // response has to have the same mobileSerialChecksum
                        manufacturerData[8] == this.mobileSerialChecksumMaskArray[1] &&
                        manufacturerData[9] == this.mobileSerialChecksumMaskArray[2])
                    {
                        scanResult.DeviceName = "CaDA RaceCar";
                        scanResult.DeviceAddress = $"{manufacturerData[4]:X2}-{manufacturerData[5]:X2}-{manufacturerData[6]:X2}";
                        return (DeviceType.CaDA_RaceCar, manufacturerData);
                    }
                    break;
                case "33-ac": return (DeviceType.MouldKing_DIY, manufacturerData);
                case "98-01": return (DeviceType.SBrick, manufacturerData);
                case "48-4d": return (DeviceType.BuWizz, manufacturerData);
                case "4e-05":
                    if (advertismentData.TryGetValue(0x09, out byte[] completeLocalName))
                    {
                        var completeLocalNameString = BitConverter.ToString(completeLocalName).ToLower();
                        if (completeLocalNameString == "42-75-57-69-7a-7a") // BuWizz
                        {
                            return (DeviceType.BuWizz2, manufacturerData);
                        }
                        else
                        {
                            return (DeviceType.BuWizz3, manufacturerData);
                        }
                    }
                    break;
                case "97-03":
                    if (manufacturerDataString.Length >= 11)
                    {
                        var pupType = manufacturerDataString.Substring(9, 2);
                        switch (pupType)
                        {
                            case "40": return (DeviceType.Boost, manufacturerData);
                            case "41": return (DeviceType.PoweredUp, manufacturerData);
                            case "80": return (DeviceType.TechnicHub, manufacturerData);
                            case "20": return (DeviceType.DuploTrainHub, manufacturerData);
                        }
                    }
                    break;
            }

            return (DeviceType.Unknown, null);
        }

        private (DeviceType DeviceType, byte[] ManufacturerData) GetDeviceInfoByService(IDictionary<byte, byte[]> advertismentData)
        {
            string manufacturerDataString;

            // 0x09: DeviceName
            if (advertismentData.TryGetValue(0x09, out byte[] nameData) &&
              nameData.Length > 0 &&
              (manufacturerDataString = Encoding.ASCII.GetString(nameData)).StartsWith("JG_JMC"))
            {
                return (DeviceType.XPBlock_XC5, null);
            }
            // 0x06: 128 bits Service UUID type
            else if (advertismentData.TryGetValue(0x06, out byte[] serviceData) && serviceData.Length >= 16)
            {
                var serviceGuid = serviceData.GetGuid();

                switch (serviceGuid)
                {
                    case var service when service == CircuitCubeDevice.SERVICE_UUID:
                        return (DeviceType.CircuitCubes, null);

                    case var service when service == Wedo2Device.SERVICE_UUID:
                        return (DeviceType.WeDo2, null);

                    default:
                        return (DeviceType.Unknown, null);
                };
            }
            else
            {
                return (DeviceType.Unknown, null);
            }

        }
    }
}