using BrickController2.PlatformServices.BluetoothLE;
using BrickController2.Protocols;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BrickController2.DeviceManagement.MouldKing;

/// <summary>
/// MouldKing H4
/// </summary>
internal class MKH4 : BluetoothDevice
{
    /// <summary>
    /// max number of transmission attempts
    /// </summary>
    private const int MAX_SEND_ATTEMPTS = 10;

    /// <summary>
    /// delay after successfully data transmission
    /// </summary>
    private static readonly TimeSpan SEND_DELAY = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// After this time period has elapsed since the last transmission, the output values ​​are sent again.
    /// </summary>
    private static readonly TimeSpan RESEND_DELAY = TimeSpan.FromMilliseconds(1000);

    internal static readonly Guid MOULD_KING_HUB_SCAN_SERVICE_UUID = BluetoothLowEnergy.GetGuid(0xaf30);

    private static readonly Guid MOULD_KING_HUB_SERVICE_UUID = BluetoothLowEnergy.GetGuid(0xae3a);
    private static readonly Guid MOULD_KING_HUB_WRITE_NO_RESPONSE_UUID = BluetoothLowEnergy.GetGuid(0xae3b);
    private static readonly Guid MOULD_KING_HUB_NOTIFY_UUID = BluetoothLowEnergy.GetGuid(0xae3c);

    private readonly int[] _lastOutputValues = new int[4];
    private readonly int[] _outputValues = new int[4];
    private readonly object _outputLock = new object();

    private int _sendAttemptsLeft;

    private IGattCharacteristic? _characteristic_AE3B_Write_No_Response;
    private IGattCharacteristic? _characteristic_AE3C_Notify;

    public MKH4(string name, string address, IDeviceRepository deviceRepository, IBluetoothLEService bleService)
        : base(name, address, deviceRepository, bleService)
    {
    }

    public override DeviceType DeviceType => DeviceType.MKH4;

    public override int NumberOfChannels => 4;

    protected override bool AutoConnectOnFirstConnect => false;

    public override void SetOutput(int channelNo, float value)
    {
        CheckChannel(channelNo);
        value = CutOutputValue(value);

        var intValue = (int)(value * 0x80); // scale and cast

        lock (_outputLock)
        {
            if (_outputValues[channelNo] != intValue)
            {
                _outputValues[channelNo] = intValue;
                _sendAttemptsLeft = MAX_SEND_ATTEMPTS;
            }
        }
    }

    protected override Task<bool> ValidateServicesAsync(IEnumerable<IGattService>? services, CancellationToken token)
    {
        var service_AE3A = services?.FirstOrDefault(s => s.Uuid == MOULD_KING_HUB_SERVICE_UUID);
        _characteristic_AE3B_Write_No_Response = service_AE3A?.Characteristics?.FirstOrDefault(c => c.Uuid == MOULD_KING_HUB_WRITE_NO_RESPONSE_UUID);
        _characteristic_AE3C_Notify = service_AE3A?.Characteristics?.FirstOrDefault(c => c.Uuid == MOULD_KING_HUB_NOTIFY_UUID);

        return Task.FromResult(_characteristic_AE3B_Write_No_Response != null);
    }

    protected override void BeforeDisconnectCleanup()
    {
        _characteristic_AE3B_Write_No_Response = null;
        _characteristic_AE3C_Notify = null;
    }

    protected override async Task ProcessOutputsAsync(CancellationToken token)
    {
        try
        {
            lock (_outputLock)
            {
                for (int channelNo = 0; channelNo < NumberOfChannels; channelNo++)
                {
                    _outputValues[channelNo] = 0x00;
                    _lastOutputValues[channelNo] = 0x01;
                }

                _sendAttemptsLeft = MAX_SEND_ATTEMPTS;
            }

            int[] outputValues = new int[NumberOfChannels];
            int sendAttemptsLeft;
            Stopwatch lastSent = Stopwatch.StartNew();

            while (!token.IsCancellationRequested)
            {
                lock (_outputLock)
                {
                    for (int channelNo = 0; channelNo < NumberOfChannels; channelNo++)
                    {
                        outputValues[channelNo] = _outputValues[channelNo];
                    }

                    sendAttemptsLeft = _sendAttemptsLeft;
                    _sendAttemptsLeft = sendAttemptsLeft > 0 ? sendAttemptsLeft - 1 : 0;
                }

                if (outputValues[0] != _lastOutputValues[0] ||
                    outputValues[1] != _lastOutputValues[1] ||
                    outputValues[2] != _lastOutputValues[2] ||
                    outputValues[3] != _lastOutputValues[3] ||
                    sendAttemptsLeft > 0 ||
                    lastSent.Elapsed > RESEND_DELAY)
                {
                    if (await SendOutputValuesAsync(outputValues, token).ConfigureAwait(false))
                    {
                        lastSent.Restart();

                        for (int channelNo = 0; channelNo < NumberOfChannels; channelNo++)
                        {
                            _lastOutputValues[channelNo] = outputValues[channelNo];
                        }

                        // reset attempts due to success
                        lock (_outputLock)
                        {
                            _sendAttemptsLeft = 0;
                        }

                        // delay after success
                        await Task.Delay(SEND_DELAY, token).ConfigureAwait(false);
                    }
                }
                else
                {
                    // delay: no change or no attempts left
                    await Task.Delay(SEND_DELAY, token).ConfigureAwait(false);
                }
            }
        }
        catch
        {
        }
    }

    private async Task<bool> SendOutputValuesAsync(int[] outputValues, CancellationToken token)
    {
        // byte offset to first channel in _sendOutputBuffer
        const int CHANNEL_START_OFFSET = 4;

        // byte offset to first channel in _sendOutputBuffer
        const int CROSS_SUM_OFFSET = 16;

        // precalculated cross sum of the static values of the output buffer
        const int STATIC_CROSS_SUM =
            0x01 +                                                  // startoffset
            0xCC + 0xAA + 0xBB + 0x01 +                             // header
                                                                    //0x80 + 0x80 + 0x80 + 0x80 +                           // dynamic part: Channel 0 .. Channel 3
            0x80 + 0x80 + 0x80 + 0x80 + 0x80 + 0x80 + 0x80 + 0x80 + // ?
                                                                    //0x00                                                  // lower byte of cross sum
            0x33;                                                   // footer

        byte[] sendOutputBuffer = {
            0xCC, 0xAA, 0xBB, 0x01,                           // header
            0x80, 0x80, 0x80, 0x80,                           // channel's values
            0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,   // ?
            0x00,                                             // lower byte of cross sum
            0x33 };                                           // footer

        int crosssum = STATIC_CROSS_SUM;
        for (int channelNo = 0; channelNo < 4; channelNo++)
        {
            int intValue = outputValues[channelNo];

            byte byteValue = intValue switch
            {
                > 0 => (byte)(0x80 + Math.Min(0x7F, intValue)),
                < 0 => (byte)(0x80 - Math.Min(0x80, -intValue)),
                _ => 0x80
            };

            sendOutputBuffer[CHANNEL_START_OFFSET + channelNo] = byteValue;
            crosssum += byteValue;
        }
        sendOutputBuffer[CROSS_SUM_OFFSET] = (byte)crosssum;

        try
        {
            return await _bleDevice!.WriteNoResponseAsync(_characteristic_AE3B_Write_No_Response!, sendOutputBuffer, token);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
