using BrickController2.Helpers;
using BrickController2.UI.Services.MainThread;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BrickController2.DeviceManagement
{
    internal class DeviceManager : NotifyPropertyChangedSource, IDeviceManager
    {
        private readonly IBluetoothDeviceManager _bluetoothDeviceManager;
        private readonly IInfraredDeviceManager _infraredDeviceManager;
        private readonly IDeviceRepository _deviceRepository;
        private readonly DeviceFactory _deviceFactory;
        private readonly IMainThreadService _uiThreadService;

        private readonly AsyncLock _asyncLock = new AsyncLock();
        private readonly AsyncLock _foundDeviceLock = new AsyncLock();

        private bool _isScanning;

        public DeviceManager(
            IBluetoothDeviceManager bluetoothDeviceManager,
            IInfraredDeviceManager infraredDeviceManager,
            IDeviceRepository deviceRepository,
            DeviceFactory deviceFactory,
            IMainThreadService uiThreadService)
        {
            _bluetoothDeviceManager = bluetoothDeviceManager;
            _infraredDeviceManager = infraredDeviceManager;
            _deviceRepository = deviceRepository;
            _deviceFactory = deviceFactory;
            _uiThreadService = uiThreadService;
        }

        public ObservableCollection<Device> Devices { get; } = new ObservableCollection<Device>();

        public bool IsScanning
        {
            get { return _isScanning; }
            set { _isScanning = value; RaisePropertyChanged(); }
        }

        public bool IsBluetoothOn => _bluetoothDeviceManager.IsBluetoothOn;

        public async Task LoadDevicesAsync()
        {
            using (await _asyncLock.LockAsync())
            {
                Devices.Clear();

                var deviceDTOs = await _deviceRepository.GetDevicesAsync();
                foreach (var deviceDTO in deviceDTOs)
                {
                    var device = _deviceFactory(deviceDTO.DeviceType, deviceDTO.Name, deviceDTO.Address, deviceDTO.DeviceData);
                    if (device != null)
                    {
                        Devices.Add(device);
                    }
                }
            }
        }

        public async Task<bool> ScanAsync(CancellationToken token)
        {
            using (await _asyncLock.LockAsync())
            {
                IsScanning = true;

                try
                {
                    // MouldKing
                    await FoundDevice(DeviceType.MouldKing_15059, "MouldKing Robot", "15059", BitConverter.GetBytes(MouldKing_15059.ManufacturerID));

                    await FoundDevice(DeviceType.MouldKing_4_0_Modul, "MouldKing 4.0 Module", MouldKing_4_0_Modul.Device1_3, BitConverter.GetBytes(MouldKing_4_0_Modul.ManufacturerID));

                    await FoundDevice(DeviceType.MouldKing_6_0_Modul, "MouldKing 6.0 Module Device 1", MouldKing_6_0_Modul.Device1, BitConverter.GetBytes(MouldKing_6_0_Modul.ManufacturerID));
                    await FoundDevice(DeviceType.MouldKing_6_0_Modul, "MouldKing 6.0 Module Device 2", MouldKing_6_0_Modul.Device2, BitConverter.GetBytes(MouldKing_6_0_Modul.ManufacturerID));
                    await FoundDevice(DeviceType.MouldKing_6_0_Modul, "MouldKing 6.0 Module Device 3", MouldKing_6_0_Modul.Device3, BitConverter.GetBytes(MouldKing_6_0_Modul.ManufacturerID));

                    await FoundDevice(DeviceType.MouldKing_Mecanum_Modul, "MouldKing Mecanum Module", "Mecanum Module", BitConverter.GetBytes(MouldKing_Mecanum_Modul.ManufacturerID));

                    // Hogokids
                    await FoundDevice(DeviceType.HOGOKIDS_8051, "HOGOKIDS Robot", "8051", BitConverter.GetBytes(HOGOKIDS_8051.ManufacturerID));

                    // Cada
                    await FoundDevice(DeviceType.Cada_C51072W, "Cada C51072W RaceCar", "C51072W", BitConverter.GetBytes(Cada_C51072W.ManufacturerID));

                    // TestModel
                    await FoundDevice(DeviceType.TestModel, "TestModel", "TestModel", BitConverter.GetBytes(TestModel.ManufacturerID));

                    var infraScan = _infraredDeviceManager.ScanAsync(FoundDevice, token);
                    var bluetoothScan = _bluetoothDeviceManager.ScanAsync(FoundDevice, token);

                    await Task.WhenAll(infraScan, bluetoothScan);

                    return infraScan.Result && bluetoothScan.Result;
                }
                catch (Exception)
                {
                    return false;
                }
                finally
                {
                    IsScanning = false;
                }
            }

            async Task FoundDevice(DeviceType deviceType, string deviceName, string deviceAddress, byte[] deviceData)
            {
                using (await _foundDeviceLock.LockAsync())
                {
                    if (Devices.Any(d => d.DeviceType == deviceType && d.Address == deviceAddress))
                    {
                        return;
                    }

                    var device = _deviceFactory(deviceType, deviceName, deviceAddress, deviceData);
                    if (device != null)
                    {
                        await _deviceRepository.InsertDeviceAsync(device.DeviceType, device.Name, device.Address, deviceData);

                        await _uiThreadService.RunOnMainThread(() => Devices.Add(device));
                    }
                }
            }
        }

        public Device GetDeviceById(string Id)
        {
            try
            {
                if (string.IsNullOrEmpty(Id))
                {
                    return null;
                }

                var deviceTypeAndAddress = Id.Split('#');
                var deviceType = (DeviceType)Enum.Parse(typeof(DeviceType), deviceTypeAndAddress[0]);
                var deviceAddress = deviceTypeAndAddress[1];
                return Devices.FirstOrDefault(d => d.DeviceType == deviceType && d.Address == deviceAddress);
            }
            catch
            {
                return null;
            }
        }

        public async Task DeleteDeviceAsync(Device device)
        {
            using (await _asyncLock.LockAsync())
            {
                await _deviceRepository.DeleteDeviceAsync(device.DeviceType, device.Address);
                Devices.Remove(device);
            }
        }

        public async Task DeleteDevicesAsync()
        {
            using (await _asyncLock.LockAsync())
            {
                await _deviceRepository.DeleteDevicesAsync();
                Devices.Clear();
            }
        }
    }
}
