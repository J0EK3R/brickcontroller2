using BrickController2.CreationManagement;
using BrickController2.DeviceManagement.IO;
using BrickController2.DeviceManagement.Lego;
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

using static BrickController2.CreationManagement.ControllerDefaults;

namespace BrickController2.DeviceManagement.MouldKing;

/// <summary>
/// MouldKing H4 (MKH4.0 module, e.g. from the 2D plotter set).
/// Protocol: ASCII messages 'T' + payload length (2 hex digits) + command (1 hex digit) + payload + 'W',
/// written to characteristic AE3B, answers/notifications are received on characteristic AE3C.
/// A channel is either a normal motor (speed control with T144) or a servo (position control with T303,
/// requires a motor with encoder, e.g. a MK+ motor).
///
/// Position reference of servo channels:
/// The position counter of the module is volatile. After a power cycle of the module it starts at 0 again,
/// wherever the shaft is (verified by test: the same target moves the motor again after a power cycle).
/// So the zero of the counter is the shaft position when the module was switched on (or the position where
/// the counter was reset the last time with T028XX). The center of a servo channel is this zero plus the
/// configured base angle. To get a fixed mechanical center, turn the shaft into that position
/// before switching on the module (or calibrate with "center here" after each power cycle).
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
    /// Number of encoder position units per degree of the motor shaft (594000 units per revolution).
    /// This value was measured with a MK+ motor: set the base angle to 180 degrees, observe the real rotation
    /// and scale the value (new value = old value * 180 / observed angle). Other motor types may differ.
    /// </summary>
    private const double POSITION_UNITS_PER_DEGREE = 1650.0;

    /// <summary>
    /// Speed (0..1) of position moves. The original app used 0x4FFF, which is about 0.625.
    /// </summary>
    private const float SERVO_SPEED = 0.625f;

    /// <summary>
    /// Max time to wait for the "position reached" message (T027300W) during calibration.
    /// </summary>
    private static readonly TimeSpan POSITION_REACHED_TIMEOUT = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Max time to wait for the "position reached" message when moving to the base angle after connecting.
    /// </summary>
    private static readonly TimeSpan CONNECT_POSITION_TIMEOUT = TimeSpan.FromSeconds(3);

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
    /// Configuration (output type, base angle, max angle, ...) of each channel.
    /// Filled in ConnectAsync the same way as WirelessProtocolBasedDevice does it,
    /// because the base class BluetoothDevice does not pass the configurations on.
    /// </summary>
    private readonly StateStore<int, ChannelConfig> _channelConfigs = new();

    /// <summary>
    /// Buffers the requested speed values of all normal motor channels (range -0x7FFF..0x7FFF).
    /// It detects changes, repeats failed transmissions and remembers what was already applied.
    /// </summary>
    private readonly OutputValuesGroup<int> _outputValues = new(CHANNEL_COUNT);

    /// <summary>
    /// Buffers the requested target positions (absolute encoder positions) of all servo channels.
    /// </summary>
    private readonly OutputValuesGroup<int> _servoTargets = new(CHANNEL_COUNT);

    /// <summary>
    /// Copy of the values fetched from <see cref="_outputValues"/>. A span can not be used across an await,
    /// so the values are copied into this array before they are sent. Only used by the output loop.
    /// </summary>
    private readonly int[] _snapshot = new int[CHANNEL_COUNT];

    /// <summary>
    /// Copy of the target positions fetched from <see cref="_servoTargets"/>. Only used by the output loop.
    /// </summary>
    private readonly int[] _servoSnapshot = new int[CHANNEL_COUNT];

    /// <summary>
    /// Values that were sent successfully the last time, used for the periodic resend.
    /// Only used by the output loop.
    /// </summary>
    private readonly int[] _lastSentValues = new int[CHANNEL_COUNT];

    /// <summary>
    /// Position (in counter units) of the center of each servo channel, relative to the current zero of the
    /// volatile module counter (= base angle of the channel configuration converted to position units).
    /// </summary>
    private readonly int[] _centerPositions = new int[CHANNEL_COUNT];

    /// <summary>
    /// True if the center position of a servo channel is known, so that targets can be calculated.
    /// Until then SetOutput is ignored for the channel, otherwise the motor could run to a wrong position.
    /// </summary>
    private readonly bool[] _servoReady = new bool[CHANNEL_COUNT];
    private readonly object _servoLock = new object();

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
        // default configuration (normal motors) until a connection delivers the real channel configuration,
        // ChannelConfiguration is a value type, so "default" is used for "no configuration"
        for (int channel = 0; channel < CHANNEL_COUNT; channel++)
        {
            _channelConfigs.Set(channel, ChannelConfig.From(default));
        }
    }

    public override DeviceType DeviceType => DeviceType.MKH4;

    public override int NumberOfChannels => CHANNEL_COUNT;

    protected override bool AutoConnectOnFirstConnect => false;

    // ------------------------------------------------------------------------------------------
    // channel capabilities (calibration and limits in the BrickController2 UI)
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Every channel can be a normal motor. As servo (position control) it requires a motor with encoder.
    /// Other types (such as stepper) are not supported.
    /// </summary>
    public override bool IsOutputTypeSupported(int channel, ChannelOutputType outputType)
        => outputType switch
        {
            ChannelOutputType.NormalMotor => true,
            ChannelOutputType.ServoMotor => true,
            _ => false,
        };

    /// <summary>
    /// The base angle of a servo channel can be set with the calibration slider (see <see cref="ResetOutputAsync"/>).
    /// The channel type is not known before connecting, so every channel offers it.
    /// </summary>
    public override bool CanResetOutput(int channel) => true;

    /// <summary>
    /// The max servo angle is used as software limit of the position range (center +/- max angle).
    /// The module itself gets no limits (the original app does not set them either).
    /// </summary>
    public override bool CanChangeMaxServoAngle(int channel) => true;

    /// <summary>
    /// "Auto calibration" does not search for mechanical end stops here (the meaning of the T02b command that
    /// the original app sends before such a run is unknown). It defines the current shaft position as center,
    /// see <see cref="AutoCalibrateOutputAsync"/>.
    /// </summary>
    public override bool CanAutoCalibrateOutput(int channel) => true;

    /// <summary>
    /// Takes over the channel configurations of the creation the same way WirelessProtocolBasedDevice does,
    /// because the base class BluetoothDevice does not pass them on.
    /// </summary>
    public override Task<DeviceConnectionResult> ConnectAsync(
        bool reconnect,
        Action<Device> onDeviceDisconnected,
        IEnumerable<ChannelConfiguration> channelConfigurations,
        bool startOutputProcessing,
        bool requestDeviceInformation,
        CancellationToken token)
    {
        // forget the configuration of the previous connection
        _channelConfigs.Clear();

        // build a dictionary, but for supported output types only
        var configs = new Dictionary<int, ChannelConfiguration>();
        foreach (var channelConfiguration in channelConfigurations.Where(c => IsOutputTypeSupported(c.Channel, c.ChannelOutputType)))
        {
            // if multiple configurations target the same channel, keep the last one
            configs[channelConfiguration.Channel] = channelConfiguration;
        }

        // store a configuration for every channel (the default for channels without one)
        for (int channel = 0; channel < CHANNEL_COUNT; channel++)
        {
            configs.TryGetValue(channel, out var config);
            _channelConfigs.Set(channel, ChannelConfig.From(config));
        }

        return base.ConnectAsync(reconnect, onDeviceDisconnected, channelConfigurations, startOutputProcessing, requestDeviceInformation, token);
    }

    /// <summary>
    /// Moves a servo channel to the base angle selected with the calibration slider and makes it the new center.
    /// The value (-1..1) is scaled to degrees the same way as in the Technic Move implementation.
    /// The angle is relative to the current zero of the volatile module counter, see the class description.
    /// </summary>
    public override async Task ResetOutputAsync(int channel, float value, CancellationToken token)
    {
        var channelIndex = CheckChannel(channel);

        // value -1..1 -> base angle -180..180 degrees
        var baseAngle = Convert.ToInt32(value * 180);

        await MoveServoToBaseAngleAsync(channelIndex, baseAngle, POSITION_REACHED_TIMEOUT, token).ConfigureAwait(false);
    }

    /// <summary>
    /// "Center here": defines the current shaft position as zero of the module counter (T028XX)
    /// and as center of the servo channel, so the base angle becomes 0.
    /// Move the shaft to the wanted center position first (e.g. with the calibration slider), then call this.
    /// The module counter is volatile, so this is valid until the module is switched off.
    /// </summary>
    public override async Task<(bool Success, float BaseServoAngle)> AutoCalibrateOutputAsync(int channel, CancellationToken token)
    {
        var channelIndex = CheckChannel(channel);

        // the module counter becomes 0 at the current position
        if (!await ResetPositionAsync(channelIndex, token).ConfigureAwait(false))
        {
            return (false, 0F);
        }

        // the current position is now the center
        _centerPositions[channelIndex] = 0;
        lock (_servoLock)
        {
            _servoReady[channelIndex] = true;
        }

        // keep the output loop in sync, so that it does not move the motor somewhere else afterwards
        _servoTargets.SetOutput(channelIndex, 0);

        return (true, 0F);
    }

    /// <summary>
    /// Stores the requested value of a channel. The value is not sent here,
    /// the output loop (<see cref="ProcessOutputsAsync"/>) picks it up from the buffer.
    /// </summary>
    public override void SetOutput(int channelNo, float value)
    {
        // validate the channel index (CheckChannel returns the index if it is valid)
        var channel = CheckChannel(channelNo);

        // limit the value to -1..1
        value = CutOutputValue(value);

        if (IsServoChannel(channel))
        {
            // position control
            SetServoOutput(channel, value);
            return;
        }

        // speed control: scale to -0x7FFF..0x7FFF, the group detects changes and schedules the transmission
        _outputValues.SetOutput(channel, (int)(value * 0x7FFF));
    }

    /// <summary>
    /// Calculates the target position of a servo channel from the value (-1..1).
    /// The range is limited by the max servo angle: center +/- max angle (software limit).
    /// </summary>
    private void SetServoOutput(int channel, float value)
    {
        // ignore the value as long as the center position is not known
        lock (_servoLock)
        {
            if (!_servoReady[channel])
            {
                return;
            }
        }

        // angle relative to the center, never larger than the configured max servo angle
        var angle = value * GetMaxServoAngle(channel);
        var target = _centerPositions[channel] + AngleToPosition(angle);

        // the group detects whether the target changed and schedules the transmission
        _servoTargets.SetOutput(channel, target);
    }

    /// <summary>
    /// True if the channel is configured as servo (position control with encoder).
    /// </summary>
    private bool IsServoChannel(int channel) => _channelConfigs.Get(channel).OutputType == ChannelOutputType.ServoMotor;

    /// <summary>
    /// Max servo angle of the channel in degrees, falls back to the default if none is configured
    /// (same logic as in WirelessProtocolBasedDevice).
    /// </summary>
    private int GetMaxServoAngle(int channel)
    {
        var maxServoAngle = _channelConfigs.Get(channel).MaxServoAngle;
        return maxServoAngle > 0 ? maxServoAngle : DEFAULT_MAX_SERVO_ANGLE;
    }

    /// <summary>
    /// True if at least one channel is configured as servo.
    /// </summary>
    private bool HasServoChannels()
    {
        for (int channel = 0; channel < CHANNEL_COUNT; channel++)
        {
            if (IsServoChannel(channel))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Converts an angle in degrees to encoder position units.
    /// </summary>
    private static int AngleToPosition(double angle) => (int)Math.Round(angle * POSITION_UNITS_PER_DEGREE);

    /// <summary>
    /// Makes the given base angle the center of a servo channel and moves the motor there.
    /// The base angle is relative to the current zero of the (volatile) module counter.
    /// Returns true if the module confirmed that the position was reached.
    /// </summary>
    private async Task<bool> MoveServoToBaseAngleAsync(int channel, int baseAngle, TimeSpan timeout, CancellationToken token)
    {
        // the center is the base angle relative to the zero of the module counter
        var center = AngleToPosition(baseAngle);

        _centerPositions[channel] = center;
        lock (_servoLock)
        {
            _servoReady[channel] = true;
        }

        // keep the output loop in sync, so that it does not move the motor somewhere else afterwards
        _servoTargets.SetOutput(channel, center);

        // move there directly, the output loop may not be running (e.g. on the calibration page)
        var targets = new int?[CHANNEL_COUNT];
        targets[channel] = center;
        var payload = BuildPositionPayload(targets, SERVO_SPEED);

        return payload is not null && await SendPositionAndWaitAsync(BuildMessage('3', payload), timeout, token).ConfigureAwait(false);
    }

    // ------------------------------------------------------------------------------------------
    // BLE services and notifications
    // ------------------------------------------------------------------------------------------

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
    /// T027300W (position reached), T0A7A1-00336153W (motor position), T027800W (position reset),
    /// T027408W (port is being driven, sent about every 90 ms while a motor runs)
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
    /// Diagnostic logging of the response to T006W. The module keeps this value between connections,
    /// the original app updates it with T155... at the end of a calibration
    /// (new value = old value + position of motor A). Unlike the position counter it is not reset to 0
    /// by the module on its own. The meaning is unknown, so the value is only logged.
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

    // ------------------------------------------------------------------------------------------
    // connect setup and device info
    // ------------------------------------------------------------------------------------------

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

            lock (_servoLock)
            {
                Array.Clear(_servoReady);
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

            // servo channels: take over the configured base angle as center and move there,
            // like the Technic Move implementation does. The module counter starts at 0 at the
            // current shaft position after a power cycle, so the move is relative to that position.
            for (int channel = 0; channel < CHANNEL_COUNT; channel++)
            {
                if (!IsServoChannel(channel))
                {
                    continue;
                }

                var baseAngle = _channelConfigs.Get(channel).ServoBaseAngle;
                await MoveServoToBaseAngleAsync(channel, baseAngle, CONNECT_POSITION_TIMEOUT, token).ConfigureAwait(false);
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

    // ------------------------------------------------------------------------------------------
    // protocol helpers
    // ------------------------------------------------------------------------------------------

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
    /// Builds the payload of the position message (T303): for each channel 4 hex digits speed and 8 hex digits
    /// target position. Channels without a target are idle (speed 0, position 0) as sent by the original app.
    /// Returns null if no channel has a target.
    /// </summary>
    private static string? BuildPositionPayload(int?[] targets, float speed)
    {
        // speed 0..1 scaled to 0..0x7FFF
        var speedValue = (int)(Math.Clamp(Math.Abs(speed), 0f, 1f) * 0x7FFF);

        var payload = new StringBuilder();
        var hasTarget = false;

        for (int channel = 0; channel < CHANNEL_COUNT; channel++)
        {
            if (targets[channel] is int target)
            {
                hasTarget = true;

                // direction bit 0x8000 is set for a positive target position, the position itself is unsigned
                var speedField = speedValue | (target >= 0 ? 0x8000 : 0);
                payload.Append(speedField.ToString("X4"));
                payload.Append(Math.Abs((long)target).ToString("X8"));
            }
            else
            {
                // idle channel
                payload.Append("8000");
                payload.Append("00000000");
            }
        }

        return hasTarget ? payload.ToString() : null;
    }

    /// <summary>
    /// Builds the payload of the simple motor start message (T144):
    /// for each channel one flag digit and 4 hex digits speed.
    /// </summary>
    private static string BuildSpeedPayload(int[] values)
    {
        return string.Concat(Enumerable.Range(0, CHANNEL_COUNT).Select(channel => FormatSpeed(values[channel], CHANNEL_FLAGS[channel])));
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
    /// Sends a position message and waits for the "position reached" notification (T027300W, command 73).
    /// Returns true if it arrived within the timeout.
    /// </summary>
    private async Task<bool> SendPositionAndWaitAsync(string message, TimeSpan timeout, CancellationToken token)
    {
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        // register before sending, so the notification can not be missed
        lock (_responseLock)
        {
            _pendingRequests["73"] = tcs;
        }

        try
        {
            if (!await WriteMessageAsync(message, token).ConfigureAwait(false))
            {
                return false;
            }

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeout, token)).ConfigureAwait(false);
            return completed == tcs.Task;
        }
        finally
        {
            // unregister, but only if it was not replaced by a newer one in the meantime
            lock (_responseLock)
            {
                if (_pendingRequests.TryGetValue("73", out var current) && current == tcs)
                {
                    _pendingRequests.Remove("73");
                }
            }
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

    // ------------------------------------------------------------------------------------------
    // position helpers
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Moves one motor to an absolute position (message T303, confirmed by "T027300W" when reached).
    /// The position is relative to the current zero of the volatile module counter.
    /// Not used by the BrickController2 UI. Note that a T144 message (normal motors) may cancel a running move.
    /// </summary>
    internal Task<bool> MoveToPositionAsync(int channelNo, float speed, int targetPosition, CancellationToken token)
    {
        var channelIndex = CheckChannel(channelNo);

        // only the selected channel gets a target, all others are idle
        var targets = new int?[CHANNEL_COUNT];
        targets[channelIndex] = targetPosition;

        return WriteMessageAsync(BuildMessage('3', BuildPositionPayload(targets, speed)!), token);
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
    /// This defines the zero of the module counter, which is the reference of the servo base angle.
    /// The counter is volatile and starts at 0 again after a power cycle of the module.
    /// </summary>
    internal async Task<bool> ResetPositionAsync(int channelNo, CancellationToken token)
    {
        var channelIndex = CheckChannel(channelNo);

        // the motor is selected with a bitmask (bit 0 = A ... bit 3 = D) in two hex digits
        var response = await RequestAsync('8', (1 << channelIndex).ToString("X2"), token).ConfigureAwait(false);

        // any response means that the module has executed the command
        return response is not null;
    }

    // ------------------------------------------------------------------------------------------
    // disconnect
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Called by the base class while the connection is still up (after the output loop was stopped).
    /// The module keeps the last speed values (the original app does not resend them), so stop all motors.
    /// </summary>
    protected override async ValueTask BeforeDisconnectAsync(CancellationToken token)
    {
        await base.BeforeDisconnectAsync(token);

        await SendStopAsync().ConfigureAwait(false);
    }

    protected override void BeforeDisconnectCleanup()
    {
        // no response can arrive any more
        lock (_responseLock)
        {
            _pendingRequests.Clear();
        }

        // the centers have to be set again after the next connect
        lock (_servoLock)
        {
            Array.Clear(_servoReady);
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

    // ------------------------------------------------------------------------------------------
    // output loop
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Output loop: sends the buffered speed values (T144) and target positions (T303) whenever they changed.
    /// </summary>
    protected override async Task ProcessOutputsAsync(CancellationToken token)
    {
        try
        {
            // reset all values to 0 and force an initial transmission,
            // the original app also starts with an all-zero message
            _outputValues.Initialize();
            Array.Clear(_lastSentValues);

            // servo channels: hold the center position, the targets set during the connect setup were cleared above
            _servoTargets.Initialize();
            for (int channel = 0; channel < CHANNEL_COUNT; channel++)
            {
                if (IsServoReady(channel))
                {
                    _servoTargets.SetOutput(channel, _centerPositions[channel]);
                }
            }

            // time since the last successful transmission, used for the periodic resend
            var lastSent = Stopwatch.StartNew();

            while (!token.IsCancellationRequested)
            {
                // ---- normal motors: speed values ----
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
                    if (ShouldResendOutputValues())
                    {
                        if (await SendOutputValuesAsync(_lastSentValues, token).ConfigureAwait(false))
                        {
                            lastSent.Restart();
                        }
                    }
                    else
                    {
                        // nothing to resend, check again after the delay
                        lastSent.Restart();
                    }
                }

                // ---- servo channels: target positions ----
                if (TryGetServoSnapshot())
                {
                    if (await SendServoTargetsAsync(_servoSnapshot, token).ConfigureAwait(false))
                    {
                        _servoTargets.Commit();
                    }
                }

                // short pause between two iterations
                await Task.Delay(SEND_DELAY, token).ConfigureAwait(false);
            }
        }
        catch
        {
            // cancellation or connection loss ends the loop,
            // the stop message is sent by BeforeDisconnectAsync
        }
    }

    /// <summary>
    /// Decides whether the periodic resend of the last speed values is allowed.
    /// Without servo channels the values (also zeros) are always resent as a safety net for a lost "stop".
    /// With servo channels only running motors are refreshed, because a T144 message could cancel a position move.
    /// </summary>
    private bool ShouldResendOutputValues()
    {
        if (!HasServoChannels())
        {
            return true;
        }

        return _lastSentValues.Any(value => value != 0);
    }

    /// <summary>
    /// True if the center position of the servo channel is known.
    /// </summary>
    private bool IsServoReady(int channel)
    {
        lock (_servoLock)
        {
            return _servoReady[channel];
        }
    }

    /// <summary>
    /// Fetches the speed values from the buffer into <see cref="_snapshot"/>.
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
    /// Fetches the target positions from the buffer into <see cref="_servoSnapshot"/>.
    /// Returns true if there is anything to send.
    /// </summary>
    private bool TryGetServoSnapshot()
    {
        if (!_servoTargets.TryGetValues(out var values))
        {
            return false;
        }

        values.CopyTo(_servoSnapshot);
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
    /// Sends the speed values of all channels with one message (T144, simple motor start).
    /// Servo channels always have the value 0 here.
    /// </summary>
    private Task<bool> SendOutputValuesAsync(int[] values, CancellationToken token)
    {
        return WriteMessageAsync(BuildMessage('4', BuildSpeedPayload(values)), token);
    }

    /// <summary>
    /// Sends the target positions of all servo channels that have a known center with one message (T303).
    /// </summary>
    private Task<bool> SendServoTargetsAsync(int[] targets, CancellationToken token)
    {
        // only servo channels with a known center get a target
        var positions = new int?[CHANNEL_COUNT];
        for (int channel = 0; channel < CHANNEL_COUNT; channel++)
        {
            positions[channel] = IsServoReady(channel) ? targets[channel] : null;
        }

        var payload = BuildPositionPayload(positions, SERVO_SPEED);
        if (payload is null)
        {
            // no servo channel is ready, nothing to move
            return Task.FromResult(true);
        }

        return WriteMessageAsync(BuildMessage('3', payload), token);
    }
}