using BrickController2.Helpers;
using BrickController2.PlatformServices.BluetoothLE;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BrickController2.DeviceManagement
{
    internal abstract class BluetoothAdvertisingDevice<_TelegramType> : Device
    {
        #region Constants
        protected static readonly Dictionary<_TelegramType, byte[]> Telegrams = new Dictionary<_TelegramType, byte[]>();
        #endregion
        #region Fields
        protected readonly IBluetoothLEService _bleService;
        protected readonly int _manufacturerId;
        protected readonly object _outputLock = new object();

        private Task _outputTask;
        private CancellationTokenSource _outputTaskTokenSource;

        private IBluetoothLEAdvertiserDevice _bleAdvertiserDevice;

        private int counter = 0;

        protected _TelegramType _currentTelegram;
        #endregion
        #region Properties
        public virtual AdvertisingInterval AdvertisingInterval => AdvertisingInterval.Min;
        public virtual TxPowerLevel TxPowerLevel => TxPowerLevel.Max;
        #endregion

        #region Constructor
        protected BluetoothAdvertisingDevice(string name, string address, byte[] deviceData, IDeviceRepository deviceRepository, IBluetoothLEService bleService)
            : base(name, address, deviceRepository)
        {
            this._bleService = bleService;

            try
            {
                this._manufacturerId = BitConverter.ToInt32(deviceData, 0);
            }
            catch
            {
                this._manufacturerId = 0x0666;
            }
        }
        #endregion

        #region InitFirstTelegram()
        protected abstract _TelegramType InitFirstTelegram();
        #endregion

        #region ConnectAsync
        public async override Task<DeviceConnectionResult> ConnectAsync(
            bool reconnect,
            Action<Device> onDeviceDisconnected,
            IEnumerable<ChannelConfiguration> channelConfigurations,
            bool startOutputProcessing,
            bool requestDeviceInformation,
            CancellationToken token)
        {
            using (await _asyncLock.LockAsync())
            {
                if (this._bleAdvertiserDevice != null ||
                    this.DeviceState != DeviceState.Disconnected)
                {
                    HardwareVersion = "DeviceState isn't Disconnected";
                    return DeviceConnectionResult.Error;
                }

                try
                {
                    // get advertiserdevice from BLEService
                    this._bleAdvertiserDevice = this._bleService.GetBluetoothLEAdvertiserDevice();

                    if (this._bleAdvertiserDevice == null)
                    {
                        HardwareVersion = "Can't get BluetoothLEAdvertiserDevice";
                        return DeviceConnectionResult.Error;
                    }

                    HardwareVersion = "Connecting";
                    this.DeviceState = DeviceState.Connecting;

                    //token.ThrowIfCancellationRequested();

                    if (startOutputProcessing)
                    {
                        _currentTelegram = InitFirstTelegram();
                        await StartOutputTaskAsync();
                    }

                    token.ThrowIfCancellationRequested();

                    HardwareVersion = $"Connected";

                    this.DeviceState = DeviceState.Connected;
                    return DeviceConnectionResult.Ok;
                }
                catch (OperationCanceledException)
                {
                    await DisconnectInternalAsync();

                    HardwareVersion = "Connection Canceled";
                    return DeviceConnectionResult.Canceled;
                }
                catch (Exception exception)
                {
                    await DisconnectInternalAsync();

                    HardwareVersion = exception.Message;
                    return DeviceConnectionResult.Error;
                }
            }
        }
        #endregion
        #region DisconnectAsync
        public override async Task DisconnectAsync()
        {
            using (await _asyncLock.LockAsync())
            {
                if (this.DeviceState == DeviceState.Disconnected)
                {
                    return;
                }

                await DisconnectInternalAsync();
            }
        }
        #endregion

        #region DisconnectInternalAsync
        private async Task DisconnectInternalAsync()
        {
            if (this._bleAdvertiserDevice != null)
            {
                HardwareVersion = "Disconnecting";
                this.DeviceState = DeviceState.Disconnecting;

                await StopOutputTaskAsync();

                this._bleAdvertiserDevice.Dispose();
                this._bleAdvertiserDevice = null;
            }

            HardwareVersion = "Disconnected";
            this.DeviceState = DeviceState.Disconnected;
        }
        #endregion

        #region StartOutputTaskAsync
        private async Task StartOutputTaskAsync()
        {
            await StopOutputTaskAsync();

            this._outputTaskTokenSource = new CancellationTokenSource();
            CancellationToken token = _outputTaskTokenSource.Token;

            this._outputTask = Task.Run(async () =>
            {
                try
                {
                    byte[] currentData;
                    if (Telegrams.TryGetValue(_currentTelegram, out currentData))
                    {
                        await this._bleAdvertiserDevice?.StartAdvertiseAsync(this.AdvertisingInterval, this.TxPowerLevel, _manufacturerId, currentData);
                    }

                    await ProcessOutputsAsync(token).ConfigureAwait(false);

                    FirmwareVersion = "Task Started";
                }
                catch (Exception exception)
                {
                    FirmwareVersion = $"StartOutputTaskAsync: {exception.Message}";
                }
            });
        }
        #endregion
        #region StopOutputTaskAsync
        private async Task StopOutputTaskAsync()
        {
            if (this._outputTaskTokenSource != null &&
                this._outputTask != null)
            {
                this._outputTaskTokenSource.Cancel();

                await this._outputTask;
                
                this._outputTaskTokenSource.Dispose();
                this._outputTaskTokenSource = null;
                
                this._outputTask = null;
            }

            await this._bleAdvertiserDevice.StopAdvertiseAsync();
            FirmwareVersion = "Task Stopped";
        }
        #endregion
        #region ProcessOutputsAsync
        protected async Task ProcessOutputsAsync(CancellationToken token)
        {
            try
            {
                bool isInitialized = false;
                _TelegramType _lastSend = default(_TelegramType);

                while (!token.IsCancellationRequested)
                {
                    bool valuesChanged = false;

                    lock (_outputLock)
                    {
                        if (!isInitialized)
                        {
                            valuesChanged = true;
                        }
                        else if (!object.Equals(_lastSend, _currentTelegram))
                        {
                            valuesChanged = true;
                        }
                    }

                    if (valuesChanged &&
                        this._bleAdvertiserDevice != null)
                    {
                        byte[] currentData;
                        if (Telegrams.TryGetValue(_currentTelegram, out currentData))
                        {
                            _lastSend = _currentTelegram;
                            isInitialized = true;

                            bool result = this._bleAdvertiserDevice.ChangeAdvertiseAsync(_manufacturerId, currentData);

                            counter++;
                            FirmwareVersion = $"{counter}";
                        }
                    }

                    await Task.Delay(100, token).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                FirmwareVersion = $"ProcessOutputsAsync: {exception.Message}";
            }
        }
        #endregion
    }
}
