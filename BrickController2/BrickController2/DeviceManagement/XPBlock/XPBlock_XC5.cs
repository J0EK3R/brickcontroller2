using BrickController2.PlatformServices.BluetoothLE;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Linq;
using BrickController2.Helpers;

namespace BrickController2.DeviceManagement
{
  /// <summary>
  /// XP Block XC5 Module
  /// </summary>
  internal class XPBlock_XC5 : BluetoothDevice
  {
    #region Constants
    private const int MAX_SEND_ATTEMPTS = 10;

    private static readonly Guid SERVICE_UUID_1800_GENERIC_ACCESS = new Guid("00001800-0000-1000-8000-00805f9b34fb");
    private static readonly Guid CHARACTERISTIC_UUID_2A00_DEVICE_NAME = new Guid("00002a00-0000-1000-8000-00805f9b34fb");
    private static readonly Guid CHARACTERISTIC_UUID_2A01_APPEARANCE = new Guid("00002a01-0000-1000-8000-00805f9b34fb");
    private static readonly Guid CHARACTERISTIC_UUID_2A04_PERIPHERAL_PREFERRED_CONNECTION_PARAMETERS = new Guid("00002a04-0000-1000-8000-00805f9b34fb");

    private static readonly Guid SERVICE_UUID_1801_GENERIC_ATTRIBUTE = new Guid("00001801-0000-1000-8000-00805f9b34fb");
    private static readonly Guid CHARACTERISTIC_UUID_2A05_SERVICE_CHANGED = new Guid("00002a05-0000-1000-8000-00805f9b34fb");

    private static readonly Guid SERVICE_UUID_180A_DEVICE_INFORMATION = new Guid("0000180a-0000-1000-8000-00805f9b34fb");
    private static readonly Guid CHARACTERISTIC_UUID_2A50_PNP_ID = new Guid("00002a50-0000-1000-8000-00805f9b34fb");

    private static readonly Guid SERVICE_UUID_FFF0_UNKNOWN_SERVICE = new Guid("0000FFF0-0000-1000-8000-00805f9b34fb");
    private static readonly Guid CHARACTERISTIC_UUID_FFF1_UNKNOWN_CHARACTERISTIC = new Guid("0000FFF1-0000-1000-8000-00805f9b34fb");
    private static readonly Guid CHARACTERISTIC_UUID_FFF2_UNKNOWN_CHARACTERISTIC = new Guid("0000FFF2-0000-1000-8000-00805f9b34fb");
    #endregion
    #region Fields
    private readonly int[] _outputValues = new int[6];
    private readonly int[] _lastOutputValues = new int[6];
    private readonly object _outputLock = new object();

    private volatile int _sendAttemptsLeft;

    private IGattCharacteristic _characteristic_FFF1_CYCLIC_STATE;
    private IGattCharacteristic _characteristic_FFF2_CMD;
    private IGattCharacteristic _characteristic_2A50_PNP_ID;
    #endregion
    #region Properties
    public override DeviceType DeviceType => DeviceType.XPBlock_XC5;

    public override int NumberOfChannels => 6;

    protected override bool AutoConnectOnFirstConnect => true;
    #endregion

    #region XPBlock_XC5(string name, string address, byte[] deviceData, IEnumerable<DeviceSetting> settings, IDeviceRepository deviceRepository, IBluetoothLEService bleService)
    /// <summary>
    /// 
    /// </summary>
    /// <param name="name"></param>
    /// <param name="address"></param>
    /// <param name="deviceData"></param>
    /// <param name="settings"></param>
    /// <param name="deviceRepository"></param>
    /// <param name="bleService"></param>
    public XPBlock_XC5(string name, string address, byte[] deviceData, IEnumerable<DeviceSetting> settings, IDeviceRepository deviceRepository, IBluetoothLEService bleService)
        : base(name, address, deviceRepository, bleService)
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

      var intValue = (int)(value * 255);

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
      var service_FFF0 = services?.FirstOrDefault(s => s.Uuid == SERVICE_UUID_FFF0_UNKNOWN_SERVICE);
      _characteristic_FFF1_CYCLIC_STATE = service_FFF0?.Characteristics?.FirstOrDefault(c => c.Uuid == CHARACTERISTIC_UUID_FFF1_UNKNOWN_CHARACTERISTIC);
      _characteristic_FFF2_CMD = service_FFF0?.Characteristics?.FirstOrDefault(c => c.Uuid == CHARACTERISTIC_UUID_FFF2_UNKNOWN_CHARACTERISTIC);

      var service_180A = services?.FirstOrDefault(s => s.Uuid == SERVICE_UUID_180A_DEVICE_INFORMATION);
      _characteristic_2A50_PNP_ID = service_180A?.Characteristics?.FirstOrDefault(c => c.Uuid == CHARACTERISTIC_UUID_2A50_PNP_ID);

      if (_characteristic_FFF1_CYCLIC_STATE != null)
      {
        await _bleDevice?.EnableNotificationAsync(_characteristic_FFF1_CYCLIC_STATE, token);
      }

      return _characteristic_FFF1_CYCLIC_STATE != null && _characteristic_FFF2_CMD != null && _characteristic_2A50_PNP_ID != null;
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
      if (characteristicGuid != _characteristic_FFF1_CYCLIC_STATE.Uuid) // || data.Length < 4 || data[0] != 0x00)
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
          _outputValues[4] = 0;
          _outputValues[5] = 0;
          _lastOutputValues[0] = 1;
          _lastOutputValues[1] = 1;
          _lastOutputValues[2] = 1;
          _lastOutputValues[3] = 1;
          _lastOutputValues[4] = 1;
          _lastOutputValues[5] = 1;
          _sendAttemptsLeft = MAX_SEND_ATTEMPTS;
        }

        while (!token.IsCancellationRequested)
        {
          int v0, v1, v2, v3, v4, v5, sendAttemptsLeft;

          lock (_outputLock)
          {
            v0 = _outputValues[0];
            v1 = _outputValues[1];
            v2 = _outputValues[2];
            v3 = _outputValues[3];
            v4 = _outputValues[4];
            v5 = _outputValues[5];
            sendAttemptsLeft = _sendAttemptsLeft;
            _sendAttemptsLeft = sendAttemptsLeft > 0 ? sendAttemptsLeft - 1 : 0;
          }

          if (v0 != _lastOutputValues[0] ||
            v1 != _lastOutputValues[1] ||
            v2 != _lastOutputValues[2] ||
            v3 != _lastOutputValues[3] ||
            v4 != _lastOutputValues[4] ||
            v5 != _lastOutputValues[5] ||
            sendAttemptsLeft > 0)
          {
            if (await SendOutputValuesAsync(v0, v1, v2, v3, v4, v5, token).ConfigureAwait(false))
            {
              _lastOutputValues[0] = v0;
              _lastOutputValues[1] = v1;
              _lastOutputValues[2] = v2;
              _lastOutputValues[3] = v3;
              _lastOutputValues[4] = v4;
              _lastOutputValues[5] = v5;
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

    #region SendOutputValuesAsync(int v0, int v1, int v2, int v3, int v4, int v5, CancellationToken token)
    /// <summary>
    /// 
    /// </summary>
    /// <param name="v0"></param>
    /// <param name="v1"></param>
    /// <param name="v2"></param>
    /// <param name="v3"></param>
    /// <param name="v4"></param>
    /// <param name="v5"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> SendOutputValuesAsync(int v0, int v1, int v2, int v3, int v4, int v5, CancellationToken token)
    {
      #region sniffed traffic
      // Stop
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0xd0 };

      //// A                                                                XX    XX
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x01, 0xfe, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0xcf };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x01, 0x36, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x07 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x01, 0x4c, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x1d };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x01, 0xae, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x7f };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x01, 0xd4, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0xa5 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x01, 0xec, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0xbd };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x01, 0xf9, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0xca };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x01, 0xfa, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0xcb };

      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x02, 0xd7, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0xa9 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x02, 0xdd, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0xaf };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x02, 0x28, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0xfa };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x02, 0x29, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0xfb };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x02, 0x64, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x36 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x02, 0xdc, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0xae };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x02, 0xff, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0xd1 };
      //// B                                                                XX    XX
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x04, 0xc2, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x96 };

      //// A & B                                                            XX    XX
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x06, 0xff, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0xd5 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x06, 0xe8, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0xbe };

      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x08, 0x3e, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x16 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x08, 0xdd, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0xb5 };

      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x09, 0xfd, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0xd6 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x09, 0xff, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0xd8 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x09, 0xeb, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0xc4 };


      //// C                                                                            XX    XX
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x01, 0xda, 0x01, 0x00, 0x00, 0x00, 0xab };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x01, 0x2c, 0x01, 0x00, 0x00, 0x00, 0xfd };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x01, 0xb7, 0x01, 0x00, 0x00, 0x00, 0x88 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x01, 0xcf, 0x01, 0x00, 0x00, 0x00, 0xa0 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x01, 0xd2, 0x01, 0x00, 0x00, 0x00, 0xa3 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x01, 0xd3, 0x01, 0x00, 0x00, 0x00, 0xa4 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x01, 0xd4, 0x01, 0x00, 0x00, 0x00, 0xa5 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x01, 0xd5, 0x01, 0x00, 0x00, 0x00, 0xa6 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x01, 0xd6, 0x01, 0x00, 0x00, 0x00, 0xa7 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x01, 0xff, 0x01, 0x00, 0x00, 0x00, 0xd0 };

      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x02, 0xe8, 0x01, 0x00, 0x00, 0x00, 0xba };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x02, 0x3b, 0x01, 0x00, 0x00, 0x00, 0x0d };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x02, 0x5a, 0x01, 0x00, 0x00, 0x00, 0x2c };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x02, 0x90, 0x01, 0x00, 0x00, 0x00, 0x62 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x02, 0x96, 0x01, 0x00, 0x00, 0x00, 0x68 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x02, 0x97, 0x01, 0x00, 0x00, 0x00, 0x69 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x02, 0xc0, 0x01, 0x00, 0x00, 0x00, 0x92 };

      //// D                                                                            XX    XX
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x04, 0xbd, 0x01, 0x00, 0x00, 0x00, 0x91 };

      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x08, 0xe2, 0x01, 0x00, 0x00, 0x00, 0xba };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x08, 0x1d, 0x01, 0x00, 0x00, 0x00, 0xf5 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x08, 0x22, 0x01, 0x00, 0x00, 0x00, 0xfa };

      //// C & D                                                                        XX    XX
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x06, 0xe0, 0x01, 0x00, 0x00, 0x00, 0xb6 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x06, 0xa7, 0x01, 0x00, 0x00, 0x00, 0x7d };

      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x09, 0xe7, 0x01, 0x00, 0x00, 0x00, 0xc0 };


      //// E                                                                                              XX    XX
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x01, 0x01, 0xff, 0x00, 0xd0 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x01, 0x01, 0x9a, 0x00, 0x6b };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x01, 0x01, 0xe5, 0x00, 0xb6 };

      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x01, 0x02, 0x81, 0x00, 0x53 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x01, 0x02, 0xff, 0x00, 0xd1 };
      //// F                                                                                              XX          XX
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x01, 0x04, 0x00, 0xff, 0xd3 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x01, 0x04, 0x00, 0xf1, 0xc5 };

      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x01, 0x08, 0x00, 0x2e, 0x06 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x01, 0x08, 0x00, 0x18, 0xf0 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x01, 0x08, 0x00, 0xff, 0xd7 };

      //// E & F                                                                                          XX    XX    XX
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x01, 0x05, 0xff, 0xff, 0xd3 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x01, 0x05, 0xff, 0xa9, 0x7d };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x01, 0x05, 0xff, 0x3f, 0x13 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x01, 0x09, 0xff, 0x10, 0xec };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x01, 0x09, 0xeb, 0x70, 0x36 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x01, 0x09, 0xe8, 0xaf, 0x70 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x01, 0x09, 0xd8, 0xf0, 0xa1 };
      //var sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x01, 0x0a, 0x86, 0xfc, 0x5c };




      #endregion

      try
      {
        // all channels are zero 
        byte[] sendOutputBuffer = new byte[] { 0x5a, 0x6b, 0x02, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0xd0 };

        // Channel A and Channel B have own direction data but are using the same speed data
        // channel A
        if (v0 > 0)
        {
          sendOutputBuffer[5] |= 0x01;
          sendOutputBuffer[6] = Math.Max(sendOutputBuffer[6], (byte)v0);
        }
        else if (v0 < 0)
        {
          sendOutputBuffer[5] |= 0x02;
          sendOutputBuffer[6] = Math.Max(sendOutputBuffer[6], (byte)-v0);
        }

        // channel B
        if (v1 > 0)
        {
          sendOutputBuffer[5] |= 0x04;
          sendOutputBuffer[6] = Math.Max(sendOutputBuffer[6], (byte)v1);
        }
        else if (v1 < 0)
        {
          sendOutputBuffer[5] |= 0x08;
          sendOutputBuffer[6] = Math.Max(sendOutputBuffer[6], (byte)-v1);
        }

        // Channel C and Channel D have own direction data but are using the same speed data
        // channel C
        if (v2 > 0)
        {
          sendOutputBuffer[7] |= 0x01;
          sendOutputBuffer[8] = Math.Max(sendOutputBuffer[8], (byte)v2);
        }
        else if (v2 < 0)
        {
          sendOutputBuffer[7] |= 0x02;
          sendOutputBuffer[8] = Math.Max(sendOutputBuffer[8], (byte)-v2);
        }

        // Channel D
        if (v3 > 0)
        {
          sendOutputBuffer[7] |= 0x04;
          sendOutputBuffer[8] = Math.Max(sendOutputBuffer[8], (byte)v3);
        }
        else if (v3 < 0)
        {
          sendOutputBuffer[7] |= 0x08;
          sendOutputBuffer[8] = Math.Max(sendOutputBuffer[8], (byte)-v3);
        }

        // E
        if (v4 > 0)
        {
          sendOutputBuffer[10] |= 0x01;
          sendOutputBuffer[11] = (byte)(v4);
        }
        else if (v4 < 0)
        {
          sendOutputBuffer[10] |= 0x02;
          sendOutputBuffer[11] = (byte)(-v4);
        }

        // F
        if (v5 > 0)
        {
          sendOutputBuffer[10] |= 0x04;
          sendOutputBuffer[12] = (byte)(v5);
        }
        else if (v5 < 0)
        {
          sendOutputBuffer[10] |= 0x08;
          sendOutputBuffer[12] = (byte)(-v5);
        }

        // array's last byte contains the cross sum of the array's bytes
        int lastCalcIndex = sendOutputBuffer.Length - 2;
        int sum = 0;
        for (int index = 0; index < lastCalcIndex; index++)
        {
          sum += sendOutputBuffer[index];
        }
        sendOutputBuffer[lastCalcIndex + 1] = (byte)sum;

        return await _bleDevice?.WriteAsync(_characteristic_FFF2_CMD, sendOutputBuffer, token);
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
      var firmwareData = await _bleDevice?.ReadAsync(_characteristic_2A50_PNP_ID, token);
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
