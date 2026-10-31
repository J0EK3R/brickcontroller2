using BrickController2.PlatformServices.BluetoothLE;
using BrickController2.Protocols;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
    /// After this time period has elapsed since the last transmission, the output values are sent again.
    /// </summary>
    private static readonly TimeSpan RESEND_DELAY = TimeSpan.FromMilliseconds(1000);

    /// <summary>
    /// max number of characters per BLE write (default ATT MTU of 23 minus 3 bytes header)
    /// </summary>
    private const int MAX_PACKET_LENGTH = 20;

    /// <summary>
    /// The original app repeats a request about every 30 ms (9 to 11 times in the captures)
    /// until the response arrives.
    /// </summary>
    private const int REQUEST_MAX_ATTEMPTS = 20;

    private static readonly TimeSpan REQUEST_RETRY_DELAY = TimeSpan.FromMilliseconds(40);

    /// <summary>
    /// Leading hex digit of each channel in the T144 message. The README uses '0' for all channels,
    /// the original app used '1' for the first channel and '0' for the fourth one. Meaning unknown.
    /// If channel A does not turn, try '1' for the first entry.
    /// </summary>
    private static readonly char[] CHANNEL_FLAGS = { '0', '0', '0', '0' };

    /// <summary>
    /// The original app does not enable notifications on the AE30 service, so this is off by default.
    /// </summary>
    private const bool ENABLE_AE30_NOTIFICATIONS = false;

    /// <summary>
    /// the original app acknowledges every notification with a single 0x01 byte
    /// </summary>
    private static readonly byte[] AckByte = { 0x01 };

    internal static readonly Guid MOULD_KING_HUB_SCAN_SERVICE_UUID = BluetoothLowEnergy.GetGuid(0xaf30);

    private static readonly Guid MOULD_KING_HUB_SERVICE_AE30_UUID = BluetoothLowEnergy.GetGuid(0xae30);
    private static readonly Guid MOULD_KING_HUB_AE01_UUID = BluetoothLowEnergy.GetGuid(0xae01);
    private static readonly Guid MOULD_KING_HUB_AE02_UUID = BluetoothLowEnergy.GetGuid(0xae02);
    private static readonly Guid MOULD_KING_HUB_AE03_UUID = BluetoothLowEnergy.GetGuid(0xae03);
    private static readonly Guid MOULD_KING_HUB_AE04_UUID = BluetoothLowEnergy.GetGuid(0xae04);
    private static readonly Guid MOULD_KING_HUB_AE05_UUID = BluetoothLowEnergy.GetGuid(0xae05);
    private static readonly Guid MOULD_KING_HUB_AE10_UUID = BluetoothLowEnergy.GetGuid(0xae10);
    private static readonly Guid MOULD_KING_HUB_SERVICE_AE3A_UUID = BluetoothLowEnergy.GetGuid(0xae3a);
    private static readonly Guid MOULD_KING_HUB_WRITE_NO_RESPONSE_AE3B_UUID = BluetoothLowEnergy.GetGuid(0xae3b);
    private static readonly Guid MOULD_KING_HUB_NOTIFY_AE3C_UUID = BluetoothLowEnergy.GetGuid(0xae3c);

    private readonly int[] _lastOutputValues = new int[4];
    private readonly int[] _outputValues = new int[4];
    private readonly object _outputLock = new object();

    private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);

    // notifications can be split into several packets, so they are collected until the closing 'W'
    private readonly StringBuilder _notificationBuffer = new StringBuilder();
    private readonly object _notificationLock = new object();

    // pending requests and last received responses, both keyed by the response command (e.g. "7E")
    private readonly Dictionary<string, TaskCompletionSource<string>> _pendingRequests = new();
    private readonly Dictionary<string, string> _lastResponses = new();
    private readonly object _responseLock = new object();

    // bit set = motor port not connected (bit 0 = A ... bit 3 = D), from the T00CW response
    private int _motorNotConnectedMask = 0x0F;

    private int _sendAttemptsLeft;

    private IGattCharacteristic? _characteristic_AE01;
    private IGattCharacteristic? _characteristic_AE02;
    private IGattCharacteristic? _characteristic_AE03;
    private IGattCharacteristic? _characteristic_AE04;
    private IGattCharacteristic? _characteristic_AE05;
    private IGattCharacteristic? _characteristic_AE10;
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

        // scale to -0x7FFF..0x7FFF
        var intValue = (int)(value * 0x7FFF);

        lock (_outputLock)
        {
            if (_outputValues[channelNo] != intValue)
            {
                _outputValues[channelNo] = intValue;
                _sendAttemptsLeft = MAX_SEND_ATTEMPTS;
            }
        }
    }

    protected override async Task<bool> ValidateServicesAsync(IEnumerable<IGattService>? services, CancellationToken token)
    {
        var service_AE30 = services?.FirstOrDefault(s => s.Uuid == MOULD_KING_HUB_SERVICE_AE30_UUID);

        // CanNotify = false
        _characteristic_AE01 = service_AE30?.Characteristics?.FirstOrDefault(c => c.Uuid == MOULD_KING_HUB_AE01_UUID);

        // CanNotify = true
        _characteristic_AE02 = service_AE30?.Characteristics?.FirstOrDefault(c => c.Uuid == MOULD_KING_HUB_AE02_UUID);
        if (ENABLE_AE30_NOTIFICATIONS && _characteristic_AE02 is not null)
        {
            await _bleDevice!.EnableNotificationAsync(_characteristic_AE02, token);
        }

        // CanNotify = false
        _characteristic_AE03 = service_AE30?.Characteristics?.FirstOrDefault(c => c.Uuid == MOULD_KING_HUB_AE03_UUID);

        // CanNotify = true
        _characteristic_AE04 = service_AE30?.Characteristics?.FirstOrDefault(c => c.Uuid == MOULD_KING_HUB_AE04_UUID);
        if (ENABLE_AE30_NOTIFICATIONS && _characteristic_AE04 is not null)
        {
            await _bleDevice!.EnableNotificationAsync(_characteristic_AE04, token);
        }

        // CanNotify = false
        _characteristic_AE05 = service_AE30?.Characteristics?.FirstOrDefault(c => c.Uuid == MOULD_KING_HUB_AE05_UUID);

        // CanNotify = false
        _characteristic_AE10 = service_AE30?.Characteristics?.FirstOrDefault(c => c.Uuid == MOULD_KING_HUB_AE10_UUID);

        var service_AE3A = services?.FirstOrDefault(s => s.Uuid == MOULD_KING_HUB_SERVICE_AE3A_UUID);

        // CanNotify = false
        _characteristic_AE3B_Write_No_Response = service_AE3A?.Characteristics?.FirstOrDefault(c => c.Uuid == MOULD_KING_HUB_WRITE_NO_RESPONSE_AE3B_UUID);

        // CanNotify = true
        _characteristic_AE3C_Notify = service_AE3A?.Characteristics?.FirstOrDefault(c => c.Uuid == MOULD_KING_HUB_NOTIFY_AE3C_UUID);
        if (_characteristic_AE3C_Notify is not null)
        {
            await _bleDevice!.EnableNotificationAsync(_characteristic_AE3C_Notify, token);
        }

        return _characteristic_AE3B_Write_No_Response != null && _characteristic_AE3C_Notify != null;
    }

    protected override void OnCharacteristicChanged(Guid characteristicGuid, byte[] data)
    {
        // only the AE3C notifications use the T...W protocol
        if (characteristicGuid != MOULD_KING_HUB_NOTIFY_AE3C_UUID)
        {
            return;
        }

        var completeMessages = new List<string>();

        lock (_notificationLock)
        {
            _notificationBuffer.Append(Encoding.ASCII.GetString(data));

            // extract all complete messages ('T' ... 'W'), payloads only contain hex digits, '+' and '-'
            while (true)
            {
                var text = _notificationBuffer.ToString();
                var start = text.IndexOf('T');
                if (start < 0)
                {
                    _notificationBuffer.Clear();
                    break;
                }

                var end = text.IndexOf('W', start);
                if (end < 0)
                {
                    // incomplete message, wait for the next packet
                    _notificationBuffer.Remove(0, start);
                    break;
                }

                completeMessages.Add(text.Substring(start, end - start + 1));
                _notificationBuffer.Remove(0, end + 1);
            }
        }

        foreach (var message in completeMessages)
        {
            HandleMessage(message);

            // the original app acknowledges every notification with a single 0x01 byte
            _ = AcknowledgeAsync();
        }
    }

    /// <summary>
    /// Response format: 'T' + payload length (2 hex digits) + command (2 hex digits, 0x7X for request X) + payload + 'W'
    /// Examples: T01711W (ready), T067E410012W (version), T027C02W (motor status),
    /// T027300W (position reached), T0A7A1-00336153W (motor position)
    /// </summary>
    private void HandleMessage(string message)
    {
        //Debug.WriteLine($"MKH4 notification: {message}");

        if (message.Length < 6)
        {
            return;
        }

        var command = message.Substring(3, 2);
        var payload = message.Substring(5, message.Length - 6);

        TaskCompletionSource<string>? pending;
        lock (_responseLock)
        {
            _lastResponses[command] = payload;
            _pendingRequests.TryGetValue(command, out pending);
        }

        if (command == "7C" && payload.Length == 2)
        {
            // motor status: bit set = not connected, e.g. 02 = B not connected
            _motorNotConnectedMask = Convert.ToInt32(payload, 16);
        }

        pending?.TrySetResult(payload);
    }

    private async Task AcknowledgeAsync()
    {
        try
        {
            await _writeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await _bleDevice!.WriteNoResponseAsync(_characteristic_AE3B_Write_No_Response!, AckByte, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }
        catch
        {
            // ignore, ack is best effort
        }
    }

    protected override async Task<bool> AfterConnectSetupAsync(bool requestDeviceInformation, CancellationToken token)
    {
        try
        {
            lock (_notificationLock)
            {
                _notificationBuffer.Clear();
            }

            lock (_responseLock)
            {
                _lastResponses.Clear();
            }

            // init sequence observed in the original app, every request is repeated until it is answered
            await RequestAsync('1', "AABB", token).ConfigureAwait(false); // T041AABBW -> T01711W (ready)
            await RequestAsync('E', string.Empty, token).ConfigureAwait(false); // T00EW -> T067E410012W (version info)
            await RequestAsync('C', string.Empty, token).ConfigureAwait(false); // T00CW -> T027C02W (motor status)
            await RequestAsync('6', string.Empty, token).ConfigureAwait(false); // T006W -> T15760B0100030003+02AD4AECW (meaning unknown)
            await RequestAsync('F', "1", token).ConfigureAwait(false);          // T01F1W -> T017F1W

            if (requestDeviceInformation)
            {
                await ReadDeviceInfo(token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            //Debug.WriteLine($"MKH4 setup failed: {ex.Message}");
        }

        return true;
    }

    /// <summary>
    /// The original app never reads the AE30 characteristics, the device info is part of the T...W protocol.
    /// </summary>
    private async Task ReadDeviceInfo(CancellationToken token)
    {
        string? payload;
        lock (_responseLock)
        {
            _lastResponses.TryGetValue("7E", out payload);
        }

        payload ??= await RequestAsync('E', string.Empty, token).ConfigureAwait(false);

        if (payload is not null)
        {
            ApplyVersionInfo(payload);
        }
    }

    /// <summary>
    /// T00EW response payload, e.g. "410012" for a module shown as H4.0 / V1.1.2 in the original app.
    /// The layout is inferred from this single sample: byte 0 = model major (high nibble) and version major (low nibble),
    /// byte 1 = model minor, byte 2 = version minor (high nibble) and version patch (low nibble).
    /// </summary>
    private void ApplyVersionInfo(string payload)
    {
        //Debug.WriteLine($"MKH4 version payload: {payload}");

        if (payload.Length != 6 || !payload.All(Uri.IsHexDigit))
        {
            return;
        }

        var b0 = Convert.ToInt32(payload.Substring(0, 2), 16);
        var b1 = Convert.ToInt32(payload.Substring(2, 2), 16);
        var b2 = Convert.ToInt32(payload.Substring(4, 2), 16);

        HardwareVersion = $"H{b0 >> 4}.{b1}";
        FirmwareVersion = $"V{b0 & 0x0F}.{b2 >> 4}.{b2 & 0x0F}";
    }

    /// <summary>
    /// Message format: 'T' + payload length (2 hex digits) + command (1 hex digit) + payload + 'W'
    /// </summary>
    private static string BuildMessage(char command, string payload = "")
        => $"T{payload.Length:X2}{command}{payload}W";

    private static string FormatSpeed(int value, char flag)
    {
        // value: -0x7FFF..0x7FFF, the direction bit 0x8000 is set for negative values
        int speed = Math.Min(Math.Abs(value), 0x7FFF);
        if (value < 0)
        {
            speed |= 0x8000;
        }

        return flag + speed.ToString("X4");
    }

    private async Task<bool> WriteMessageAsync(string message, CancellationToken token)
    {
        var bytes = Encoding.ASCII.GetBytes(message);

        await _writeLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            for (int offset = 0; offset < bytes.Length; offset += MAX_PACKET_LENGTH)
            {
                var chunk = bytes.AsSpan(offset, Math.Min(MAX_PACKET_LENGTH, bytes.Length - offset)).ToArray();
                if (!await _bleDevice!.WriteNoResponseAsync(_characteristic_AE3B_Write_No_Response!, chunk, token).ConfigureAwait(false))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Sends a request and repeats it until the matching response (0x7X for request X) arrives,
    /// like the original app does. Returns the payload of the response or null after too many attempts.
    /// </summary>
    private async Task<string?> RequestAsync(char command, string payload, CancellationToken token)
    {
        var responseCommand = "7" + command;
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_responseLock)
        {
            _pendingRequests[responseCommand] = tcs;
        }

        try
        {
            var message = BuildMessage(command, payload);

            for (int attempt = 0; attempt < REQUEST_MAX_ATTEMPTS; attempt++)
            {
                token.ThrowIfCancellationRequested();

                await WriteMessageAsync(message, token).ConfigureAwait(false);

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(REQUEST_RETRY_DELAY, token)).ConfigureAwait(false);
                if (completed == tcs.Task)
                {
                    return tcs.Task.Result;
                }
            }

            //Debug.WriteLine($"MKH4 no response for {message}");
            return null;
        }
        finally
        {
            lock (_responseLock)
            {
                if (_pendingRequests.TryGetValue(responseCommand, out var current) && current == tcs)
                {
                    _pendingRequests.Remove(responseCommand);
                }
            }
        }
    }

    /// <summary>
    /// Moves one motor to an absolute position (message T303, confirmed by "T027300W" when reached).
    /// Not used by the BrickController2 UI yet. Note that the periodic resend of ProcessOutputsAsync
    /// (T144) may interfere with a running position move.
    /// </summary>
    internal Task<bool> MoveToPositionAsync(int channelNo, float speed, int targetPosition, CancellationToken token)
    {
        CheckChannel(channelNo);

        var speedValue = (int)(Math.Clamp(Math.Abs(speed), 0f, 1f) * 0x7FFF);
        var payload = new StringBuilder();

        for (int channel = 0; channel < NumberOfChannels; channel++)
        {
            if (channel == channelNo)
            {
                // direction bit 0x8000 is set for a positive target position, the position itself is unsigned
                var speedField = speedValue | (targetPosition >= 0 ? 0x8000 : 0);
                payload.Append(speedField.ToString("X4"));
                payload.Append(Math.Abs((long)targetPosition).ToString("X8"));
            }
            else
            {
                // idle channel as sent by the original app
                payload.Append("8000");
                payload.Append("00000000");
            }
        }

        return WriteMessageAsync(BuildMessage('3', payload.ToString()), token);
    }

    /// <summary>
    /// Reads the current position of one motor (T02AXXW -> T0A7AX+/-PPPPPPPPW).
    /// </summary>
    internal async Task<int?> GetPositionAsync(int channelNo, CancellationToken token)
    {
        CheckChannel(channelNo);

        var response = await RequestAsync('A', (1 << channelNo).ToString("X2"), token).ConfigureAwait(false);

        // payload: motor digit (1..4), sign, 8 hex digits, e.g. "1-00336153"
        if (response is null || response.Length != 10 || response[0] != (char)('1' + channelNo))
        {
            return null;
        }

        var value = (int)Convert.ToInt64(response.Substring(2), 16);
        return response[1] == '-' ? -value : value;
    }

    protected override void BeforeDisconnectCleanup()
    {
        lock (_responseLock)
        {
            _pendingRequests.Clear();
        }

        _characteristic_AE01 = null;
        _characteristic_AE02 = null;
        _characteristic_AE03 = null;
        _characteristic_AE04 = null;
        _characteristic_AE05 = null;
        _characteristic_AE10 = null;
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
        finally
        {
            // the module keeps the last speed values (the original app does not resend them), so stop all motors
            await SendStopAsync().ConfigureAwait(false);
        }
    }

    private async Task SendStopAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            await WriteMessageAsync(BuildMessage('4', BuildSpeedPayload(new int[NumberOfChannels])), cts.Token).ConfigureAwait(false);
        }
        catch
        {
            // best effort, the connection may already be gone
        }
    }

    private static string BuildSpeedPayload(int[] outputValues)
    {
        // simple motor start: 4 x (flag digit + 4 hex digits speed)
        return string.Concat(Enumerable.Range(0, 4).Select(channel => FormatSpeed(outputValues[channel], CHANNEL_FLAGS[channel])));
    }

    private Task<bool> SendOutputValuesAsync(int[] outputValues, CancellationToken token)
    {
        // simple motor start: T144 + 4 x "FXXXX" + W
        return WriteMessageAsync(BuildMessage('4', BuildSpeedPayload(outputValues)), token);
    }
}