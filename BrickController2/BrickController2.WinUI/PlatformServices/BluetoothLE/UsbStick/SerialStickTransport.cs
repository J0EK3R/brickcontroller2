using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;

namespace BrickController2.Windows.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// <see cref="IStickTransport"/> on top of a Windows COM port (<see cref="SerialPort"/>).
/// The port is opened with DTR and RTS de-asserted: on the ESP32-S3 USB Serial/JTAG peripheral
/// certain DTR/RTS sequences reset the chip or enter the ROM bootloader (protocol.md section 9).
/// Thin by design: all protocol logic is in the shared project and covered by unit tests.
/// </summary>
internal sealed class SerialStickTransport : IStickTransport
{
    private const int WriteTimeoutMs = 500;
    private const int ReadBufferSize = 256;

    private readonly string _portName;
    private SerialPort? _port;
    private CancellationTokenSource? _readCancellation;
    private int _closing;

    public SerialStickTransport(string portName)
    {
        _portName = portName;
    }

    /// <inheritdoc />
    public string Name => _portName;

    /// <inheritdoc />
    public event Action<byte[]>? DataReceived;

    /// <inheritdoc />
    public event Action? Disconnected;

    /// <inheritdoc />
    public void Open()
    {
        // Baud rate and parity are irrelevant for a virtual USB port; 8N1 is what libraries assume.
        var port = new SerialPort(_portName, 115200, Parity.None, 8, StopBits.One)
        {
            WriteTimeout = WriteTimeoutMs,
        };

        // Must be set before Open(): never touch the control lines (see class comment).
        port.DtrEnable = false;
        port.RtsEnable = false;

        try
        {
            port.Open();

            // The ROM bootloader may print text after a reset; the parser skips garbage, but start clean.
            port.DiscardInBuffer();
        }
        catch
        {
            // Do not leave the half-open SerialPort to the finalizer (it would keep the COM port busy).
            port.Dispose();
            throw;
        }

        _port = port;
        _readCancellation = new CancellationTokenSource();
        _ = Task.Run(() => ReadLoopAsync(port, _readCancellation.Token));
    }

    /// <inheritdoc />
    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        SerialPort port = _port ?? throw new IOException("The serial port is not open.");
        await port.BaseStream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Close()
    {
        // Marks the close as intentional so the read loop does not report a disconnect.
        Interlocked.Exchange(ref _closing, 1);
        _readCancellation?.Cancel();

        try
        {
            _port?.Close();
        }
        catch (Exception)
        {
            // The device may already be gone; closing is best effort.
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Close();
        _port?.Dispose();
        _readCancellation?.Dispose();
        _port = null;
        _readCancellation = null;
    }

    /// <summary>
    /// Reads bytes until the port is closed. Any read error while the port is still supposed to be open
    /// (unplugged stick) is reported once through <see cref="Disconnected"/>.
    /// </summary>
    private async Task ReadLoopAsync(SerialPort port, CancellationToken token)
    {
        var buffer = new byte[ReadBufferSize];

        try
        {
            while (!token.IsCancellationRequested)
            {
                int count = await port.BaseStream.ReadAsync(buffer, token).ConfigureAwait(false);
                if (count == 0)
                {
                    // End of stream: the device is gone. Report it like a read error (no busy loop).
                    ReportDisconnect();
                    return;
                }

                DataReceived?.Invoke(buffer[..count]);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception)
        {
            // Unplugged device: ReadAsync throws IOException / UnauthorizedAccessException.
            ReportDisconnect();
        }
    }

    /// <summary>Raises <see cref="Disconnected"/> once, unless the port is being closed on purpose.</summary>
    private void ReportDisconnect()
    {
        if (Interlocked.Exchange(ref _closing, 1) == 0)
        {
            Disconnected?.Invoke();
        }
    }
}
