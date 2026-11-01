using BrickController2.DeviceManagement.IO;
using BrickController2.Helpers;
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
/// MouldKing H4 (MKH4.0 module, e.g. from the 2D plotter set).
/// Protocol: ASCII messages 'T' + payload length (2 hex digits) + command (1 hex digit) + payload + 'W',
/// written to characteristic AE3B, answers/notifications are received on characteristic AE3C.
/// </summary>
internal class MKH4 : BluetoothDevice
{
    /// <summary>
    /// number of motor ports (A, B, C, D)
    /// </summary>
    private const int CHANNEL_COUNT = 4;

    /// <summary>
    /// delay between two iterations of the output loop
    /// </summary>
    private static readonly TimeSpan SEND_DELAY = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// If nothing was sent for this time period, the last values are sent again as a safety net
    /// (e.g. if a "stop" message got lost, the module would keep the old speed values otherwise).
    /// </summary>
    private static readonly TimeSpan RESEND_DELAY = TimeSpan.FromMilliseconds(1000);

    /// <summary>
    /// max number of characters per BLE write (default ATT MTU of 23 minus 3 bytes header)
    /// </summary>
    private const int MAX_PACKET_LENGTH = 20;

    /// <summary>
    /// The original app repeats a request about every 30 ms (9 to 11 times in the captures)
    /// until the response arrives. Requests are repeated the same way here.
    /// </summary>
    private const int REQUEST_MAX_ATTEMPTS = 20;
    private static readonly TimeSpan REQUEST_RETRY_DELAY = TimeSpan.FromMilliseconds(40);

    /// <summary>
    /// Leading hex digit of each channel in the T144 message. The README uses '0' for all channels,
    /// the original app used '1' for the first channel and '0' for the third and fourth one. Meaning unknown.
    /// If channel A does not turn, try '1' for the first entry.
    /// </summary>
    private static readonly char[] CHANNEL_FLAGS = { '0', '0', '0', '0' };

    /// <summary>
    /// The original app does not enable notifications on the AE30 service, so this is off by default.
    /// </summary>
    private const bool ENABLE_AE30_NOTIFICATIONS = false;

    /// <summary>
    /// The original app acknowledges every notification with a single 0x01 byte.
    /// </summary>
    private static readonly byte[] AckByte = { 0x01 };

    internal static readonly Guid MOULD_KING_HUB_SCAN_SERVICE_UUID = BluetoothLowEnergy.GetGuid(0xaf30);

    // service AE30: not used by the original app during the captured sessions, kept for reference
    private static readonly Guid MOULD_KING_HUB_SERVICE_AE30_UUID = BluetoothLowEnergy.GetGuid(0xae30);
    private static readonly Guid MOULD_KING_HUB_AE01_UUID = BluetoothLowEnergy.GetGuid(0xae01);
    private static readonly Guid MOULD_KING_HUB_AE02_UUID = BluetoothLowEnergy.GetGuid(0xae02);
    private static readonly Guid MOULD_KING_HUB_AE03_UUID = BluetoothLowEnergy.GetGuid(0xae03);
    private static readonly Guid MOULD_KING_HUB_AE04_UUID = BluetoothLowEnergy.GetGuid(0xae04);
    private static readonly Guid MOULD_KING_HUB_AE05_UUID = BluetoothLowEnergy.GetGuid(0xae05);
    private static readonly Guid MOULD_KING_HUB_AE10_UUID = BluetoothLowEnergy.GetGuid(0xae10);

    // service AE3A: the actual control service
    private static readonly Guid MOULD_KING_HUB_SERVICE_AE3A_UUID = BluetoothLowEnergy.GetGuid(0xae3a);
    private static readonly Guid MOULD_KING_HUB_WRITE_NO_RESPONSE_AE3B_UUID = BluetoothLowEnergy.GetGuid(0xae3b);
    private static readonly Guid MOULD_KING_HUB_NOTIFY_AE3C_UUID = BluetoothLowEnergy.GetGuid(0xae3c);

    /// <summary>
    /// Buffers the requested speed values of all channels (range -0x7FFF..0x7FFF).
    /// It detects changes, repeats failed transmissions and remembers what was already applied.
    /// </summary>
    private readonly OutputValuesGroup<int> _outputValues = new(CHANNEL_COUNT);

    /// <summary>
    /// Copy of the values fetched from <see cref="_outputValues"/>. A span can not be used across an await,
    /// so the values are copied into this array before they are sent. Only used by the output loop.
    /// </summary>
    private readonly int[] _snapshot = new int[CHANNEL_COUNT];

    /// <summary>
    /// Values that were sent successfully the last time, used for the periodic resend.
    /// Only used by the output loop.
    /// </summary>
    private readonly int[] _lastSentValues = new int[CHANNEL_COUNT];

    /// <summary>
    /// Serializes all writes to AE3B, because the ACK bytes are written from the notification callback
    /// while the output loop and the requests write at the same time.
    /// </summary>
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

    public override int NumberOfChannels => CHANNEL_COUNT;

    protected override bool AutoConnectOnFirstConnect => false;

    /// <summary>
    /// Stores the requested value of a channel. The value is not sent here,
    /// the output loop (<see cref="ProcessOutputsAsync"/>) picks it up from the buffer.
    /// </summary>
    public override void SetOutput(int channelNo, float value)
    {
        // validate the channel index (CheckChannel returns the index if it is valid)
        var channel = CheckChannel(channelNo);

        // limit the value to -1..1 and scale it to -0x7FFF..0x7FFF
        var intValue = (int)(CutOutputValue(value) * 0x7FFF);

        // the group detects whether the value changed and schedules the transmission
        _outputValues.SetOutput(channel, intValue);
    }

    protected override async Task<bool> ValidateServicesAsync(IEnumerable<IGattService>? services, CancellationToken token)
    {
        // ---- service AE30: looked up for completeness, the original app does not use it ----
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

        // ---- service AE3A: write characteristic (commands) and notify characteristic (responses) ----
        var service_AE3A = services?.FirstOrDefault(s => s.Uuid == MOULD_KING_HUB_SERVICE_AE3A_UUID);

        // CanNotify = false
        _characteristic_AE3B_Write_No_Response = service_AE3A?.Characteristics?.FirstOrDefault(c => c.Uuid == MOULD_KING_HUB_WRITE_NO_RESPONSE_AE3B_UUID);

        // CanNotify = true
        _characteristic_AE3C_Notify = service_AE3A?.Characteristics?.FirstOrDefault(c => c.Uuid == MOULD_KING_HUB_NOTIFY_AE3C_UUID);
        if (_characteristic_AE3C_Notify is not null)
        {
            // the original app writes the CCCD of this characteristic right after the service discovery
            await _bleDevice!.EnableNotificationAsync(_characteristic_AE3C_Notify, token);
        }

        // both characteristics are required for the protocol
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
            // append the new packet to what is left over from earlier packets
            _notificationBuffer.Append(Encoding.ASCII.GetString(data));

            // extract all complete messages ('T' ... 'W'), payloads only contain hex digits, '+' and '-'
            // (the module may send several messages in one notification, e.g. "T017b1WT027300W")
            while (true)
            {
                var text = _notificationBuffer.ToString();

                // find the start of the next message, anything before it is garbage
                var start = text.IndexOf('T');
                if (start < 0)
                {
                    _notificationBuffer.Clear();
                    break;
                }

                // find the end of the message
                var end = text.IndexOf('W', start);
                if (end < 0)
                {
                    // incomplete message, drop the garbage in front of it and wait for the next packet
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
    /// T027300W (position reached), T0A7A1-00336153W (motor position), T027800W (position reset)
    /// </summary>
    private void HandleMessage(string message)
    {
        Debug.WriteLine($"MKH4 notification: {message}");

        // shortest possible message is "T" + 2 digits length + 2 digits command + "W"
        if (message.Length < 6)
        {
            return;
        }

        var command = message.Substring(3, 2);
        var payload = message.Substring(5, message.Length - 6);

        // remember the response and look for a request that waits for it
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
        else if (command == "76")
        {
            // response to T006W, diagnostic only
            LogStoredValue(payload);
        }

        // complete the waiting request (if any)
        pending?.TrySetResult(payload);
    }

    /// <summary>
    /// Diagnostic logging of the response to T006W. The module keeps this value permanently,
    /// the original app updates it with T155... at the end of a calibration
    /// (new value = old value + position of motor A). The meaning is unknown, so the value is only logged.
    /// Payload: fixed prefix (12 hex digits) + sign + 8 hex digits, e.g. "0B0100030003+0279E999"
    /// </summary>
    private static void LogStoredValue(string payload)
    {
        try
        {
            // expected: 12 digit prefix, sign, 8 hex digits
            if (payload.Length != 21 || (payload[12] != '+' && payload[12] != '-'))
            {
                Debug.WriteLine($"MKH4 stored value (unexpected format): {payload}");
                return;
            }

            var prefix = payload.Substring(0, 12);
            var value = Convert.ToInt64(payload.Substring(13), 16);
            if (payload[12] == '-')
            {
                value = -value;
            }

            Debug.WriteLine($"MKH4 stored value (T006): prefix={prefix}, value={value} (0x{Math.Abs(value):X8})");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"MKH4 stored value could not be parsed: {payload} ({ex.Message})");
        }
    }

    /// <summary>
    /// Sends the single 0x01 byte that the original app sends after each notification.
    /// </summary>
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
            // ignore, the ack is best effort
        }
    }

    protected override async Task<bool> AfterConnectSetupAsync(bool requestDeviceInformation, CancellationToken token)
    {
        try
        {
            // forget everything left over from an earlier connection
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
            await RequestAsync('6', string.Empty, token).ConfigureAwait(false); // T006W -> T15760B0100030003+0279E999W (stored value, logged only)
            await RequestAsync('F', "1", token).ConfigureAwait(false);          // T01F1W -> T017F1W

            if (requestDeviceInformation)
            {
                await ReadDeviceInfo(token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // the connection attempt was cancelled, let the caller handle it
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"MKH4 setup failed: {ex.Message}");
        }

        return true;
    }

    /// <summary>
    /// The original app never reads the AE30 characteristics, the device info is part of the T...W protocol.
    /// </summary>
    private async Task ReadDeviceInfo(CancellationToken token)
    {
        // the version info was already requested during the init sequence
        string? payload;
        lock (_responseLock)
        {
            _lastResponses.TryGetValue("7E", out payload);
        }

        // ask again if there is no answer yet
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
        Debug.WriteLine($"MKH4 version payload: {payload}");

        // three bytes = six hex digits are expected
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

    /// <summary>
    /// Formats the speed of one channel as one flag digit followed by 4 hex digits.
    /// </summary>
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

    /// <summary>
    /// Writes a complete message, split into packets of at most 20 characters.
    /// </summary>
    private async Task<bool> WriteMessageAsync(string message, CancellationToken token)
    {
        var bytes = Encoding.ASCII.GetBytes(message);

        // no other write may be interleaved between the packets of one message
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
        // a request X is answered with a message with the command 7X
        var responseCommand = "7" + command;
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        // register the request before sending, so the response can not be missed
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

                // (re)send the request, a failed write is simply tried again in the next round
                await WriteMessageAsync(message, token).ConfigureAwait(false);

                // wait a short time for the response before repeating the request
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(REQUEST_RETRY_DELAY, token)).ConfigureAwait(false);
                if (completed == tcs.Task)
                {
                    return tcs.Task.Result;
                }
            }

            Debug.WriteLine($"MKH4 no response for {message}");
            return null;
        }
        finally
        {
            // unregister the request, but only if it was not replaced by a newer one in the meantime
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
    /// Not used by the BrickController2 UI yet. Note that the periodic resend of the output loop
    /// (T144) may interfere with a running position move.
    /// </summary>
    internal Task<bool> MoveToPositionAsync(int channelNo, float speed, int targetPosition, CancellationToken token)
    {
        var channelIndex = CheckChannel(channelNo);

        // speed 0..1 scaled to 0..0x7FFF
        var speedValue = (int)(Math.Clamp(Math.Abs(speed), 0f, 1f) * 0x7FFF);
        var payload = new StringBuilder();

        for (int channel = 0; channel < CHANNEL_COUNT; channel++)
        {
            if (channel == channelIndex)
            {
                // direction bit 0x8000 is set for a positive target position, the position itself is unsigned
                var speedField = speedValue | (targetPosition >= 0 ? 0x8000 : 0);
                payload.Append(speedField.ToString("X4"));
                payload.Append(Math.Abs((long)targetPosition).ToString("X8"));
            }
            else
            {
                // idle channel as sent by the original app: speed 0, target position 0
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
        var channelIndex = CheckChannel(channelNo);

        // the motor is selected with a bitmask (bit 0 = A ... bit 3 = D) in two hex digits
        var response = await RequestAsync('A', (1 << channelIndex).ToString("X2"), token).ConfigureAwait(false);

        // payload: motor digit (1..4), sign, 8 hex digits, e.g. "1-00336153"
        if (response is null || response.Length != 10 || response[0] != (char)('1' + channelIndex))
        {
            return null;
        }

        var value = (int)Convert.ToInt64(response.Substring(2), 16);
        return response[1] == '-' ? -value : value;
    }

    /// <summary>
    /// Sets the current position of one motor to 0 (T028XXW -> T027800W, XX = motor bitmask,
    /// e.g. T02804W for motor C as seen in the calibration capture of the original app).
    /// Not used by the BrickController2 UI yet.
    /// </summary>
    internal async Task<bool> ResetPositionAsync(int channelNo, CancellationToken token)
    {
        var channelIndex = CheckChannel(channelNo);

        // the motor is selected with a bitmask (bit 0 = A ... bit 3 = D) in two hex digits
        var response = await RequestAsync('8', (1 << channelIndex).ToString("X2"), token).ConfigureAwait(false);

        // any response means that the module has executed the command
        return response is not null;
    }

    protected override void BeforeDisconnectCleanup()
    {
        // no response can arrive any more
        lock (_responseLock)
        {
            _pendingRequests.Clear();
        }

        // the characteristics are invalid after the disconnect
        _characteristic_AE01 = null;
        _characteristic_AE02 = null;
        _characteristic_AE03 = null;
        _characteristic_AE04 = null;
        _characteristic_AE05 = null;
        _characteristic_AE10 = null;
        _characteristic_AE3B_Write_No_Response = null;
        _characteristic_AE3C_Notify = null;
    }

    /// <summary>
    /// Output loop: sends the buffered speed values whenever they changed.
    /// </summary>
    protected override async Task ProcessOutputsAsync(CancellationToken token)
    {
        try
        {
            // reset all values to 0 and force an initial transmission,
            // the original app also starts with an all-zero message
            _outputValues.Initialize();
            Array.Clear(_lastSentValues);

            // time since the last successful transmission, used for the periodic resend
            var lastSent = Stopwatch.StartNew();

            while (!token.IsCancellationRequested)
            {
                if (TryGetOutputSnapshot())
                {
                    // the values changed (or a previous attempt failed), so send them
                    if (await SendOutputValuesAsync(_snapshot, token).ConfigureAwait(false))
                    {
                        // tell the group that these values were applied, so they are not sent again
                        _outputValues.Commit();

                        // remember the sent values for the periodic resend
                        _snapshot.CopyTo(_lastSentValues, 0);
                        lastSent.Restart();
                    }
                }
                else if (lastSent.Elapsed > RESEND_DELAY)
                {
                    // nothing changed for a while: send the last values again as a safety net,
                    // the group does not offer a periodic resend, so the own copy is used
                    if (await SendOutputValuesAsync(_lastSentValues, token).ConfigureAwait(false))
                    {
                        lastSent.Restart();
                    }
                }

                // short pause between two iterations
                await Task.Delay(SEND_DELAY, token).ConfigureAwait(false);
            }
        }
        catch
        {
            // cancellation or connection loss ends the loop
        }
        finally
        {
            // the module keeps the last speed values (the original app does not resend them), so stop all motors
            await SendStopAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Fetches the values from the buffer into <see cref="_snapshot"/>.
    /// Returns true if there is anything to send (changed values or a failed previous attempt).
    /// This is a separate synchronous method, because the span returned by the group can not be used in an async method.
    /// </summary>
    private bool TryGetOutputSnapshot()
    {
        if (!_outputValues.TryGetValues(out var values))
        {
            return false;
        }

        values.CopyTo(_snapshot);
        return true;
    }

    /// <summary>
    /// Sends a stop message for all channels, best effort with a short timeout.
    /// </summary>
    private async Task SendStopAsync()
    {
        try
        {
            // the connection may be dying, so do not wait longer than half a second
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            await WriteMessageAsync(BuildMessage('4', BuildSpeedPayload(new int[CHANNEL_COUNT])), cts.Token).ConfigureAwait(false);
        }
        catch
        {
            // best effort, the connection may already be gone
        }
    }

    /// <summary>
    /// Builds the payload of the simple motor start message: for each channel one flag digit and 4 hex digits speed.
    /// </summary>
    private static string BuildSpeedPayload(int[] values)
    {
        return string.Concat(Enumerable.Range(0, CHANNEL_COUNT).Select(channel => FormatSpeed(values[channel], CHANNEL_FLAGS[channel])));
    }

    /// <summary>
    /// Sends the speed values of all channels with one message.
    /// </summary>
    private Task<bool> SendOutputValuesAsync(int[] values, CancellationToken token)
    {
        // simple motor start: T144 + 4 x "FXXXX" + W
        return WriteMessageAsync(BuildMessage('4', BuildSpeedPayload(values)), token);
    }
}