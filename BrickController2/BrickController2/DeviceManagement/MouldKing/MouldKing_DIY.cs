using BrickController2.PlatformServices.BluetoothLE;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Linq;
using BrickController2.Helpers;
using System.Diagnostics;

namespace BrickController2.DeviceManagement
{
  /// <summary>
  /// MouldKing DIY
  /// </summary>
  internal class MouldKing_DIY : BluetoothDevice
  {
    #region Constants
    private const int MAX_SEND_ATTEMPTS = 10;

    private static readonly Guid SERVICE_UUID_1800_GENERIC_ACCESS = new Guid("00001800-0000-1000-8000-00805f9b34fb");
    private static readonly Guid CHARACTERISTIC_UUID_2A00_DEVICE_NAME = new Guid("00002a00-0000-1000-8000-00805f9b34fb");

    private static readonly Guid SERVICE_UUID_AE30_UNKNOWN_SERVICE = new Guid("0000ae30-0000-1000-8000-00805f9b34fb");
    private static readonly Guid CHARACTERISTIC_UUID_AE01_UNKNOWN_CHARACTERISTIC = new Guid("0000ae01-0000-1000-8000-00805f9b34fb");
    private static readonly Guid CHARACTERISTIC_UUID_AE02_UNKNOWN_CHARACTERISTIC = new Guid("0000ae02-0000-1000-8000-00805f9b34fb");
    private static readonly Guid CHARACTERISTIC_UUID_AE03_UNKNOWN_CHARACTERISTIC = new Guid("0000ae03-0000-1000-8000-00805f9b34fb");
    private static readonly Guid CHARACTERISTIC_UUID_AE04_UNKNOWN_CHARACTERISTIC = new Guid("0000ae04-0000-1000-8000-00805f9b34fb");
    private static readonly Guid CHARACTERISTIC_UUID_AE05_UNKNOWN_CHARACTERISTIC = new Guid("0000ae05-0000-1000-8000-00805f9b34fb");
    private static readonly Guid CHARACTERISTIC_UUID_AE10_UNKNOWN_CHARACTERISTIC = new Guid("0000ae10-0000-1000-8000-00805f9b34fb");

    private static readonly Guid SERVICE_UUID_AE3A_UNKNOWN_SERVICE = new Guid("0000ae3a-0000-1000-8000-00805f9b34fb");
    private static readonly Guid CHARACTERISTIC_UUID_AE3B_UNKNOWN_CHARACTERISTIC = new Guid("0000ae3b-0000-1000-8000-00805f9b34fb");
    private static readonly Guid CHARACTERISTIC_UUID_AE3C_UNKNOWN_CHARACTERISTIC = new Guid("0000ae3c-0000-1000-8000-00805f9b34fb");
    #endregion
    #region Fields
    private readonly int[] _outputValues = new int[4];
    private readonly int[] _lastOutputValues = new int[4];
    private readonly object _outputLock = new object();

    private volatile int _sendAttemptsLeft;

    private IGattCharacteristic _characteristic_AE01;
    private IGattCharacteristic _characteristic_AE02;
    private IGattCharacteristic _characteristic_AE03;
    private IGattCharacteristic _characteristic_AE04;
    private IGattCharacteristic _characteristic_AE05;
    private IGattCharacteristic _characteristic_AE10;

    private IGattCharacteristic _characteristic_AE3B_CMD;
    private IGattCharacteristic _characteristic_AE3C_CYCLIC_STATE;
    private IGattCharacteristic _characteristic_2A00_DEVICE_NAME;
    #endregion
    #region Properties
    public override DeviceType DeviceType => DeviceType.MouldKing_DIY;

    public override int NumberOfChannels => 4;

    protected override bool AutoConnectOnFirstConnect => true;
    #endregion

    #region MouldKing_DIY(string name, string address, byte[] deviceData, IEnumerable<DeviceSetting> settings, IDeviceRepository deviceRepository, IBluetoothLEService bleService)
    /// <summary>
    /// 
    /// </summary>
    /// <param name="name"></param>
    /// <param name="address"></param>
    /// <param name="deviceData"></param>
    /// <param name="settings"></param>
    /// <param name="deviceRepository"></param>
    /// <param name="bleService"></param>
    public MouldKing_DIY(string name, string address, byte[] deviceData, IEnumerable<DeviceSetting> settings, IDeviceRepository deviceRepository, IBluetoothLEService bleService)
        : base("MouldKing DIY", address, deviceRepository, bleService)
    {
    }
    #endregion

    #region SetOutput(int channel, float value)
    /// <summary>
    /// 
    /// </summary>
    /// <param name="channel"></param>
    /// <param name="value"></param>
    public override void SetOutput(int channel, float value)
    {
      this.CheckChannel(channel);
      value = this.CutOutputValue(value);

      var intValue = (int)(value * 0x7F);

      lock (_outputLock)
      {
        if (_outputValues[channel] != intValue)
        {
          _outputValues[channel] = intValue;
          _sendAttemptsLeft = MAX_SEND_ATTEMPTS;
        }
      }
    }
    #endregion
    #region ValidateServicesAsync(IEnumerable<IGattService> services, CancellationToken token)
    /// <summary>
    /// 
    /// </summary>
    /// <param name="services"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    protected override async Task<bool> ValidateServicesAsync(IEnumerable<IGattService> services, CancellationToken token)
    {
      var service_AE30 = services?.FirstOrDefault(s => s.Uuid == SERVICE_UUID_AE30_UNKNOWN_SERVICE);
      _characteristic_AE01 = service_AE30?.Characteristics?.FirstOrDefault(c => c.Uuid == CHARACTERISTIC_UUID_AE01_UNKNOWN_CHARACTERISTIC);
      _characteristic_AE02 = service_AE30?.Characteristics?.FirstOrDefault(c => c.Uuid == CHARACTERISTIC_UUID_AE02_UNKNOWN_CHARACTERISTIC);
      _characteristic_AE03 = service_AE30?.Characteristics?.FirstOrDefault(c => c.Uuid == CHARACTERISTIC_UUID_AE03_UNKNOWN_CHARACTERISTIC);
      _characteristic_AE04 = service_AE30?.Characteristics?.FirstOrDefault(c => c.Uuid == CHARACTERISTIC_UUID_AE04_UNKNOWN_CHARACTERISTIC);
      _characteristic_AE05 = service_AE30?.Characteristics?.FirstOrDefault(c => c.Uuid == CHARACTERISTIC_UUID_AE05_UNKNOWN_CHARACTERISTIC);
      _characteristic_AE10 = service_AE30?.Characteristics?.FirstOrDefault(c => c.Uuid == CHARACTERISTIC_UUID_AE10_UNKNOWN_CHARACTERISTIC);

      var service_AE3A = services?.FirstOrDefault(s => s.Uuid == SERVICE_UUID_AE3A_UNKNOWN_SERVICE);
      _characteristic_AE3C_CYCLIC_STATE = service_AE3A?.Characteristics?.FirstOrDefault(c => c.Uuid == CHARACTERISTIC_UUID_AE3C_UNKNOWN_CHARACTERISTIC);
      _characteristic_AE3B_CMD = service_AE3A?.Characteristics?.FirstOrDefault(c => c.Uuid == CHARACTERISTIC_UUID_AE3B_UNKNOWN_CHARACTERISTIC);

      var service_1800 = services?.FirstOrDefault(s => s.Uuid == SERVICE_UUID_1800_GENERIC_ACCESS);
      _characteristic_2A00_DEVICE_NAME = service_1800?.Characteristics?.FirstOrDefault(c => c.Uuid == CHARACTERISTIC_UUID_2A00_DEVICE_NAME);

      if (_characteristic_AE3C_CYCLIC_STATE != null)
      {
        await _bleDevice?.EnableNotificationAsync(_characteristic_AE3C_CYCLIC_STATE, token);
      }

      return _characteristic_AE3C_CYCLIC_STATE != null && _characteristic_AE3B_CMD != null;// && _characteristic_2A50_PNP_ID != null;
    }
    #endregion
    #region OnCharacteristicChanged(Guid characteristicGuid, byte[] data)
    /// <summary>
    /// 
    /// </summary>
    /// <param name="characteristicGuid"></param>
    /// <param name="data"></param>
    protected override void OnCharacteristicChanged(Guid characteristicGuid, byte[] data)
    {
      if (characteristicGuid != _characteristic_AE3C_CYCLIC_STATE.Uuid) // || data.Length < 4 || data[0] != 0x00)
      {
        return;
      }

      BatteryVoltage = BitConverter.ToString(data, 0);
    }
    #endregion
    #region AfterConnectSetupAsync(bool requestDeviceInformation, CancellationToken token)
    /// <summary>
    /// 
    /// </summary>
    /// <param name="requestDeviceInformation"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    protected override async Task<bool> AfterConnectSetupAsync(bool requestDeviceInformation, CancellationToken token)
    {
      try
      {
        if (requestDeviceInformation)
        {
          await this.ReadDeviceInfo(token);
        }
      }
      catch { }

      return true;
    }
    #endregion

    #region ProcessOutputsAsync(CancellationToken token)
    /// <summary>
    /// 
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    protected override async Task ProcessOutputsAsync(CancellationToken token)
    {
      try
      {
        lock (_outputLock)
        {
          _outputValues[0] = 0;
          _outputValues[1] = 0;
          _outputValues[2] = 0;
          _outputValues[3] = 0;
          _lastOutputValues[0] = 1;
          _lastOutputValues[1] = 1;
          _lastOutputValues[2] = 1;
          _lastOutputValues[3] = 1;
          _sendAttemptsLeft = MAX_SEND_ATTEMPTS;
        }

        Stopwatch lastSent = Stopwatch.StartNew();
        while (!token.IsCancellationRequested)
        {
          int v0, v1, v2, v3, sendAttemptsLeft;

          lock (_outputLock)
          {
            v0 = _outputValues[0];
            v1 = _outputValues[1];
            v2 = _outputValues[2];
            v3 = _outputValues[3];
            sendAttemptsLeft = _sendAttemptsLeft;
            _sendAttemptsLeft = sendAttemptsLeft > 0 ? sendAttemptsLeft - 1 : 0;
          }

          if (sendAttemptsLeft > 0 ||
            lastSent.Elapsed > TimeSpan.FromMilliseconds(100) ||
            v0 != _lastOutputValues[0] ||
            v1 != _lastOutputValues[1] ||
            v2 != _lastOutputValues[2] ||
            v3 != _lastOutputValues[3]
            )
          {
            if (await SendOutputValuesAsync(v0, v1, v2, v3, token).ConfigureAwait(false))
            {
              _lastOutputValues[0] = v0;
              _lastOutputValues[1] = v1;
              _lastOutputValues[2] = v2;
              _lastOutputValues[3] = v3;
              lastSent.Restart();
            }
          }
          else
          {
            await Task.Delay(10, token).ConfigureAwait(false);
          }
        }
      }
      catch
      {
      }
    }
    #endregion

    #region SendOutputValuesAsync(int v0, int v1, int v2, int v3, CancellationToken token)
    /// <summary>
    /// 
    /// </summary>
    /// <param name="v0"></param>
    /// <param name="v1"></param>
    /// <param name="v2"></param>
    /// <param name="v3"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> SendOutputValuesAsync(int v0, int v1, int v2, int v3, CancellationToken token)
    {
      #region sniffed traffic
      // cc aa bb 01 80 80 80 80 80 80 80 80 80 80 80 80 66 33 // Stop
      // cc aa bb 01 6f 80 80 80 80 80 80 80 80 80 80 80 55 33 // A
      // cc aa bb 01 ff 80 80 80 80 80 80 80 80 80 80 80 e5 33 // A
      // cc aa bb 01 80 73 80 80 80 80 80 80 80 80 80 80 59 33 // B
      // cc aa bb 01 80 80 01 80 80 80 80 80 80 80 80 80 e7 33 // C
      // cc aa bb 01 80 80 80 ff 80 80 80 80 80 80 80 80 e5 33 // D
      // cc aa bb 01 80 80 80 01 80 80 80 80 80 80 80 80 e7 33 // D
      // cc aa bb 01 80 80 80 03 80 80 80 80 80 80 80 80 e9 33 // D
      #endregion

      try
      {

        // all channels are zero 
        byte[] sendOutputBuffer = new byte[] { 0xcc, 0xaa, 0xbb, 0x01, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x00, 0x33 };
        //v0 = 1;

        // channel A
        if (v0 > 0)
        {
          sendOutputBuffer[4] = (byte)(0x80 + Math.Min(v0, 0x7F));
        }
        else if (v0 < 0)
        {
          sendOutputBuffer[4] = (byte)(0x80 - Math.Min(-v0, 0x80));
        }

        // channel B
        if (v1 > 0)
        {
          sendOutputBuffer[5] = (byte)(0x80 + Math.Min(v1, 0x7F));
        }
        else if (v1 < 0)
        {
          sendOutputBuffer[5] = (byte)(0x80 - Math.Min(-v1, 0x80));
        }

        // channel C
        if (v2 > 0)
        {
          sendOutputBuffer[6] = (byte)(0x80 + Math.Min(v2, 0x7F));
        }
        else if (v2 < 0)
        {
          sendOutputBuffer[6] = (byte)(0x80 - Math.Min(-v2, 0x80));
        }

        // Channel D
        if (v3 > 0)
        {
          sendOutputBuffer[7] = (byte)(0x80 + Math.Min(v3, 0x7F));
        }
        else if (v3 < 0)
        {
          sendOutputBuffer[7] = (byte)(0x80 - Math.Min(-v3, 0x80));
        }

        // array's byte at position (lastCalcIndex - 1) contains the cross sum of the array's bytes + 1
        int lastCalcIndex = sendOutputBuffer.Length - 1;
        int sum = 1;
        for (int index = 0; index <= lastCalcIndex; index++)
        {
          sum += sendOutputBuffer[index];
        }
        sendOutputBuffer[lastCalcIndex - 1] = (byte)sum;

        return await _bleDevice?.WriteNoResponseAsync(_characteristic_AE3B_CMD, sendOutputBuffer, token);
      }
      catch (Exception)
      {
        return false;
      }
    }
    #endregion

    #region ReadDeviceInfo(CancellationToken token)
    /// <summary>
    /// 
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task ReadDeviceInfo(CancellationToken token)
    {
      var firmwareData = await _bleDevice?.ReadAsync(_characteristic_2A00_DEVICE_NAME, token);
      var firmwareVersion = $"{firmwareData?.GetInt16(5)}";
      if (!string.IsNullOrEmpty(firmwareVersion))
      {
        FirmwareVersion = firmwareVersion;
      }

      var modelNumber = $"{(ushort)firmwareData?.GetInt16(3)}";
      if (!string.IsNullOrEmpty(modelNumber))
      {
        HardwareVersion = modelNumber;
      }
    }
    #endregion
  }
}
