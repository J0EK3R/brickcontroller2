using BrickController2.PlatformServices.BluetoothLE;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace BrickController2.DeviceManagement
{
  /// <summary>
  /// Baseclass for devices wich communicate over BluetoothAdvertising
  /// </summary>
  internal abstract class BluetoothAdvertisingDevice : Device
  {
    #region Constants
    #endregion
    #region Fields
    /// <summary>
    /// reference to bleService object
    /// </summary>
    protected readonly IBluetoothLEService _bleService;

    /// <summary>
    /// manufacturerid wich ist sent by the BluetoothAdvertising
    /// </summary>
    protected readonly ushort _manufacturerId;

    /// <summary>
    /// object to lock the output data
    /// </summary>
    protected readonly object _outputLock = new object();

    /// <summary>
    /// output task wich runs the cyclic output loop
    /// </summary>
    private Task _outputTask;

    /// <summary>
    /// CancellationToken to stop the output task
    /// </summary>
    private CancellationTokenSource _outputTaskTokenSource;

    /// <summary>
    /// BluetoothLEAdvertiserDevice wich is created in ConnectAsync
    /// </summary>
    private IBluetoothLEAdvertiserDevice _bleAdvertiserDevice;

    /// <summary>
    /// counter wich is incremented on new data
    /// </summary>
    private int counter = 0;

    /// <summary>
    /// this counter is incremented in method "SetOutput" if data has changed
    /// </summary>
    private int dataVersion = 0;

    /// <summary>
    /// after this timespan the method TryGetTelegram is called in the output loop to refresh data
    /// </summary>
    private TimeSpan cyclicDataRefreshTimeSpan = TimeSpan.FromSeconds(2);

    /// <summary>
    /// timespan to wait after each output loop
    /// </summary>
    private TimeSpan cyclicLoopWaitTimeSpan = TimeSpan.FromMilliseconds(100);
    #endregion
    #region Properties
    public virtual AdvertisingInterval AdvertisingInterval => AdvertisingInterval.Min;
    public virtual TxPowerLevel TxPowerLevel => TxPowerLevel.Max;
    #endregion

    #region Constructor
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="name"></param>
    /// <param name="address"></param>
    /// <param name="deviceData"></param>
    /// <param name="deviceRepository"></param>
    /// <param name="bleService"></param>
    protected BluetoothAdvertisingDevice(string name, string address, byte[] deviceData, IDeviceRepository deviceRepository, IBluetoothLEService bleService)
        : base(name, address, deviceRepository)
    {
      this._bleService = bleService;

      try
      {
        this._manufacturerId = BitConverter.ToUInt16(deviceData, 0);
      }
      catch
      {
        this._manufacturerId = 0x0666;
      }
    }
    #endregion

    #region ConnectAsync
    /// <summary>
    /// This method creates the advertising device and starts the output loop
    /// </summary>
    /// <param name="reconnect"></param>
    /// <param name="onDeviceDisconnected"></param>
    /// <param name="channelConfigurations"></param>
    /// <param name="startOutputProcessing"></param>
    /// <param name="requestDeviceInformation"></param>
    /// <param name="token"></param>
    /// <returns></returns>
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
          this.SetStateText("DeviceState isn't Disconnected");
          return DeviceConnectionResult.Error;
        }

        try
        {
          // get advertiserdevice from BLEService
          this._bleAdvertiserDevice = this._bleService.GetBluetoothLEAdvertiserDevice();

          if (this._bleAdvertiserDevice == null)
          {
            this.SetStateText("Can't get BluetoothLEAdvertiserDevice");
            return DeviceConnectionResult.Error;
          }

          this.SetStateText("Connecting");
          this.DeviceState = DeviceState.Connecting;

          token.ThrowIfCancellationRequested();

          if (startOutputProcessing)
          {
            this.InitOutputTask();
            await StartOutputTaskAsync();
          }

          token.ThrowIfCancellationRequested();

          this.SetStateText($"Connected");

          this.DeviceState = DeviceState.Connected;
          return DeviceConnectionResult.Ok;
        }
        catch (OperationCanceledException)
        {
          await DisconnectInternalAsync();

          this.SetStateText("Connection Canceled");
          return DeviceConnectionResult.Canceled;
        }
        catch (Exception exception)
        {
          await DisconnectInternalAsync();

          this.SetStateText($"Connection exception: {exception.Message}");
          return DeviceConnectionResult.Error;
        }
      }
    }
    #endregion
    #region DisconnectAsync
    /// <summary>
    /// This method stops the output loop and disposes the advertising device
    /// </summary>
    /// <returns>Task</returns>
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
    /// <summary>
    /// This method stops the output loop and disposes the advertising device
    /// </summary>
    /// <returns>Task</returns>
    private async Task DisconnectInternalAsync()
    {
      if (this._bleAdvertiserDevice != null)
      {
        this.SetStateText("Disconnecting");
        this.DeviceState = DeviceState.Disconnecting;

        await this.StopOutputTaskAsync();

        this._bleAdvertiserDevice.Dispose();
        this._bleAdvertiserDevice = null;
      }

      this.SetStateText("Disconnected");
      this.DeviceState = DeviceState.Disconnected;
    }
    #endregion

    #region StartOutputTaskAsync
    /// <summary>
    /// This method creates a new task wich starts bluetooth advertising und runs the output loop
    /// </summary>
    /// <returns>Task</returns>
    private async Task StartOutputTaskAsync()
    {
      await this.StopOutputTaskAsync();

      this._outputTaskTokenSource = new CancellationTokenSource();
      CancellationToken token = this._outputTaskTokenSource.Token;

      this._outputTask = Task.Run(async () =>
      {
        try
        {
          byte[] currentData;
          if (this.TryGetTelegram(out currentData))
          {
            await this._bleAdvertiserDevice?.StartAdvertiseAsync(this.AdvertisingInterval, this.TxPowerLevel, this._manufacturerId, currentData);

            this.SetDebugOutputText("Outputloop started");

            await ProcessOutputsAsync(token).ConfigureAwait(false);
          }
          else
          {
            this.SetDebugOutputText("Outputloop not started - can't get telegram data");
          }
        }
        catch (Exception exception)
        {
          this.SetDebugOutputText($"StartOutputTaskAsync: {exception.Message}");
        }
      });
    }
    #endregion
    #region StopOutputTaskAsync
    /// <summary>
    /// This method stops the output loop
    /// </summary>
    /// <returns></returns>
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
      this.SetDebugOutputText("Task Stopped");
    }
    #endregion
    #region ProcessOutputsAsync
    /// <summary>
    /// This method runs the output loop to check for new data
    /// </summary>
    /// <param name="token">CancellationToken</param>
    /// <returns>Task</returns>
    protected async Task ProcessOutputsAsync(CancellationToken token)
    {
      try
      {
        int lastChangeDataVersion = this.dataVersion - 1;
        int currentDataVersion;
        Stopwatch stopwatch = Stopwatch.StartNew();

        while (!token.IsCancellationRequested)
        {
          currentDataVersion = this.dataVersion;
          bool valuesChanged = lastChangeDataVersion != currentDataVersion;

          if (valuesChanged ||
              stopwatch.Elapsed > this.cyclicDataRefreshTimeSpan)
          {
            byte[] currentData;
            if (this.TryGetTelegram(out currentData))
            {
              lastChangeDataVersion = currentDataVersion;
              stopwatch.Restart();

              bool result = this._bleAdvertiserDevice.ChangeAdvertiseAsync(this._manufacturerId, currentData);

              this.counter++;
              this.SetDebugOutputText($"{this.counter}");
            }
          }

          await Task.Delay(this.cyclicLoopWaitTimeSpan, token).ConfigureAwait(false);
        }
      }
      catch (Exception exception)
      {
        this.SetDebugOutputText($"ProcessOutputsAsync: {exception.Message}");
      }
    }
    #endregion

    #region InitOutputTask()
    /// <summary>
    /// This method sets the device to initial state before output loop starts
    /// </summary>
    protected abstract void InitOutputTask();
    #endregion

    #region SetDebugOutputText(string text)
    /// <summary>
    /// Sets output text somewere in the UI - currently as "FimrwareVersion"
    /// </summary>
    /// <param name="text">output text</param>
    protected void SetDebugOutputText(string text)
    {
      this.FirmwareVersion = text;
    }
    #endregion
    #region SetStateText(string text)
    /// <summary>
    /// Sets state text somewere in the UI - currently as "HardwareVersion"
    /// </summary>
    /// <param name="text">state text</param>
    protected void SetStateText(string text)
    {
      this.HardwareVersion = text;
    }
    #endregion

    #region SetOutput(int channel, float value)
    /// <summary>
    /// This method is called on new controller events
    /// </summary>
    /// <param name="channel">number of channel</param>
    /// <param name="value">currrent channels value</param>
    public sealed override void SetOutput(int channel, float value)
    {
      // call mthod SetChannel und increment dataVersion-Counter
      if (this.SetChannel(channel, value))
      {
        this.dataVersion++;
      }
    }
    #endregion
    #region SetChannel(int channel, float value)
    /// <summary>
    /// This method is called on new controller events
    /// </summary>
    /// <param name="channel">number of channel</param>
    /// <param name="value">currrent channels value</param>
    /// <returns>True: values have changed. False: else</returns>
    protected abstract bool SetChannel(int channel, float value);
    #endregion

    #region TryGetTelegram(out byte[] currentData)
    /// <summary>
    /// This method is called by the output loop in ProcessOutputsAsync if 
    /// * dataVersion has changed
    /// * cyclic after a timespan
    /// </summary>
    /// <param name="currentData"></param>
    /// <returns></returns>
    public abstract bool TryGetTelegram(out byte[] currentData);
    #endregion
  }
}
