using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BrickController2.PlatformServices.BluetoothLE;
using Windows.Devices.Bluetooth;
using Windows.Foundation.Metadata;
using Windows.System.Profile;

namespace BrickController2.Windows.PlatformServices.BluetoothLE
{
    public class BleService :
        IBluetoothLEService
    {
        [Flags]
        private enum BluetoothStatus
        {
            None = 0x00,
            ClassicSupported = 0x01,
            LowEnergySupported = 0x02,

            AllFeatures = ClassicSupported | LowEnergySupported
        }

        private readonly string _DeviceId;
        private bool _isScanning;

        public BleService()
        {
            this._DeviceId = BleService.GetDeviceId();
        }

        public bool IsBluetoothLESupported => CurrentBluetoothStatus.HasFlag(BluetoothStatus.LowEnergySupported);
        public bool IsBluetoothOn => CurrentBluetoothStatus.HasFlag(BluetoothStatus.ClassicSupported);

        public string DeviceID => _DeviceId;

        private BluetoothStatus CurrentBluetoothStatus
        {
            get
            {
                // synchroniously wait
                var adapterTask = GetBluetoothAdapter();
                adapterTask.Wait();

                BluetoothStatus status = (adapterTask.Result?.IsClassicSupported ?? false) ? BluetoothStatus.ClassicSupported : BluetoothStatus.None;
                status |= (adapterTask.Result?.IsLowEnergySupported ?? false) ? BluetoothStatus.LowEnergySupported : BluetoothStatus.None;

                return status;
            }
        }

        private static async Task<BluetoothAdapter> GetBluetoothAdapter() => await BluetoothAdapter.GetDefaultAsync()
            .AsTask()
            .ConfigureAwait(false);

        public async Task<bool> ScanDevicesAsync(Action<ScanResult> scanCallback, IEnumerable<Tuple<ushort, byte[]>> advertiseList, CancellationToken token)
        {
            if (_isScanning || CurrentBluetoothStatus != BluetoothStatus.AllFeatures)
            {
                return false;
            }

            try
            {
                _isScanning = true;
                return await NewScanAsync(scanCallback, advertiseList, token);
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                _isScanning = false;
            }
        }

        public IBluetoothLEDevice GetKnownDevice(string address)
        {
            if (!IsBluetoothLESupported)
            {
                return null;
            }

            return new BleDevice(address);
        }

        private async Task<bool> NewScanAsync(Action<ScanResult> scanCallback, IEnumerable<Tuple<ushort, byte[]>> advertiseList, CancellationToken token)
        {
            try
            {
                IBluetoothLEAdvertiserDevice advertiserDevice = null;
                if (advertiseList != null)
                {
                    advertiserDevice = this.GetBluetoothLEAdvertiserDevice();

                    foreach (Tuple<ushort, byte[]> currentEntry in advertiseList)
                    {
                        await advertiserDevice.StartAdvertiseAsync(AdvertisingInterval.Min, TxPowerLevel.Max, currentEntry.Item1, currentEntry.Item2);
                    }
                }

                var leScanner = new BleScanner(scanCallback);

                leScanner.Start();

                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                token.Register(() =>
                {
                    leScanner.Stop();
                    advertiserDevice.StopAdvertiseAsync();
                    tcs.SetResult(true);
                });

                return await tcs.Task;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public IBluetoothLEAdvertiserDevice GetBluetoothLEAdvertiserDevice()
        {
            return new BluetoothLEAdvertiserDevice();
        }

        private static string GetDeviceId()
        {

            string id = null;

            try
            {
                if (ApiInformation.IsTypePresent("Windows.System.Profile.SystemIdentification"))
                {
                    var systemId = SystemIdentification.GetSystemIdForPublisher();

                    // Make sure this device can generate the IDs
                    if (systemId.Source != SystemIdentificationSource.None)
                    {
                        // The Id property has a buffer with the unique ID
                        var hardwareId = systemId.Id;
                        var bytes = new byte[hardwareId.Length];
                        //var dataReader = Windows.Storage.Streams.DataReader.FromBuffer(hardwareId);
                        //dataReader.ReadBytes(bytes);

                        id = Convert.ToBase64String(bytes);
                    }
                }

                if (id == null && ApiInformation.IsTypePresent("Windows.System.Profile.HardwareIdentification"))
                {
                    var token = HardwareIdentification.GetPackageSpecificToken(null);
                    var hardwareId = token.Id;
                    var bytes = new byte[hardwareId.Length];

                    //var dataReader = Windows.Storage.Streams.DataReader.FromBuffer(hardwareId);
                    //dataReader.ReadBytes(bytes);

                    id = Convert.ToBase64String(bytes);
                }

                if (id == null)
                {
                    id = "unsupported";
                }

            }
            catch (Exception)
            {
                id = "unsupported";
            }

            return id;
        }
    }
}