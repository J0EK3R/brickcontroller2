// Diagnostic trace of the USB stick GATT path (stick connection, link, device and the BC2 connect flow).
// Output goes to System.Diagnostics.Debug, i.e. to the "Output" window of Visual Studio (Debug builds only).

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;

namespace BrickController2.Diagnostics;

/// <summary>
/// Central place for the "[USBSTICK]" diagnostic lines. Every call site uses <see cref="Write"/>, which is marked
/// <c>[Conditional("DEBUG")]</c>: in Release builds the call (including the evaluation of its arguments) is removed
/// by the compiler, so the trace is completely silent and costs nothing. In Debug builds a line looks like
/// <c>[USBSTICK] 14:03:21.457 t12 conn 0: TX CONNECT seq=5 ...</c> (time with milliseconds, managed thread id).
/// The helpers below only format values (frame names, HCI reasons) and are safe to call anywhere.
/// </summary>
internal static class UsbStickTrace
{
    /// <summary>
    /// Prefix of every line, easy to filter for in the Visual Studio Output window.
    /// </summary>
    public const string Prefix = "[USBSTICK]";

    /// <summary>
    /// Maximum number of lines waiting for the writer thread; older lines are dropped beyond that.
    /// </summary>
    internal const int QueueCapacity = 4096;

    // Bounded queue between the callers (any thread) and the single writer thread. DropOldest keeps the
    // newest lines and never blocks a producer, so tracing cannot starve the code it observes.
    private static readonly Channel<string> Queue = Channel.CreateBounded<string>(
        new BoundedChannelOptions(QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        },
        OnDropped);

    // Number of lines dropped since the writer last reported it.
    private static long _dropped;

    // Started lazily on the first line so Release builds (where Write is compiled out) never create the thread.
    private static readonly Lazy<Thread> Writer = new(StartWriter);

    /// <summary>
    /// Queues one trace line for the debugger output (Debug builds only). The time stamp and thread id are taken
    /// here, at call time; the (slow, synchronous under a debugger) <see cref="Debug.WriteLine(string)"/> runs on
    /// a background thread. Never blocks.
    /// </summary>
    /// <param name="message">Free text; callers include the conn_id where one exists.</param>
    [Conditional("DEBUG")]
    public static void Write(string message)
    {
        _ = Writer.Value;
        Enqueue($"{Prefix} {DateTime.Now:HH:mm:ss.fff} t{Environment.CurrentManagedThreadId} {message}");
    }

    /// <summary>
    /// Queues an already formatted line (also used by tests via <see cref="Sink"/>).
    /// </summary>
    private static void Enqueue(string line) => Queue.Writer.TryWrite(line);

    /// <summary>
    /// Output of the writer thread; replaced by tests, <see cref="Debug.WriteLine(string)"/> otherwise.
    /// </summary>
    internal static Action<string> Sink { get; set; } = line => Debug.WriteLine(line);

    /// <summary>
    /// Called by the channel for every line it had to drop because the queue was full.
    /// </summary>
    private static void OnDropped(string _) => Interlocked.Increment(ref _dropped);

    /// <summary>
    /// Creates the background writer thread (low priority, does not keep the process alive).
    /// </summary>
    private static Thread StartWriter()
    {
        var thread = new Thread(WriterLoop)
        {
            IsBackground = true,
            Name = "UsbStickTrace",
            Priority = ThreadPriority.BelowNormal,
        };
        thread.Start();
        return thread;
    }

    /// <summary>
    /// Writer thread: drains the queue and reports how many lines were dropped in between.
    /// </summary>
    private static void WriterLoop()
    {
        var reader = Queue.Reader;
        while (reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
        {
            while (reader.TryRead(out var line))
            {
                ReportDropped();     // before the line, so the gap shows up where it happened
                Sink(line);
            }

            ReportDropped();
        }
    }

    /// <summary>
    /// Emits a "dropped" line when the queue overflowed since the last report.
    /// </summary>
    private static void ReportDropped()
    {
        long dropped = Interlocked.Exchange(ref _dropped, 0);
        if (dropped > 0)
        {
            Sink($"{Prefix} {DateTime.Now:HH:mm:ss.fff} trace queue overflow: {dropped} older line(s) dropped");
        }
    }

    /// <summary>
    /// Test support: waits until the writer thread has emptied the queue.
    /// </summary>
    internal static void Flush()
    {
        // Poll: the queue is tiny in tests and this is never used in production.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (Queue.Reader.Count > 0 && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(5);
        }

        Thread.Sleep(20); // let the writer finish the line it is currently emitting
    }

    /// <summary>
    /// Name of a stick frame type byte (protocol.md section 3), "0x.." when unknown.
    /// </summary>
    public static string FrameName(byte type) => type switch
    {
        StickProtocol.TypeHello => "HELLO",
        StickProtocol.TypePing => "PING",
        StickProtocol.TypeSetStart => "SET_START",
        StickProtocol.TypeSetUpdate => "SET_UPDATE",
        StickProtocol.TypeSetStop => "SET_STOP",
        StickProtocol.TypeStopAll => "STOP_ALL",
        StickProtocol.TypeGetStatus => "GET_STATUS",
        StickProtocol.TypeScanStart => "SCAN_START",
        StickProtocol.TypeScanStop => "SCAN_STOP",
        StickProtocol.TypeConnect => "CONNECT",
        StickProtocol.TypeDisconnect => "DISCONNECT",
        StickProtocol.TypeDiscover => "DISCOVER",
        StickProtocol.TypeRead => "READ",
        StickProtocol.TypeWrite => "WRITE",
        StickProtocol.TypeWriteCmd => "WRITE_CMD",
        StickProtocol.TypeSubscribe => "SUBSCRIBE",
        StickProtocol.TypeGetGattStatus => "GET_GATT_STATUS",
        StickProtocol.TypeSetFailsafe => "SET_FAILSAFE",
        StickProtocol.TypeReadRsp => "READ_RSP",
        StickProtocol.TypeGattStatusRsp => "GATT_STATUS_RSP",
        StickProtocol.TypeEvtNotify => "EVT_NOTIFY",
        StickProtocol.TypeEvtConnected => "EVT_CONNECTED",
        StickProtocol.TypeEvtDisconnected => "EVT_DISCONNECTED",
        StickProtocol.TypeEvtScanReport => "EVT_SCAN_REPORT",
        StickProtocol.TypeEvtGattService => "EVT_GATT_SERVICE",
        StickProtocol.TypeEvtGattChar => "EVT_GATT_CHAR",
        StickProtocol.TypeEvtDiscoverDone => "EVT_DISCOVER_DONE",
        StickProtocol.TypeEvtScanStopped => "EVT_SCAN_STOPPED",
        StickProtocol.TypeEvtDropped => "EVT_DROPPED",
        StickProtocol.TypeAck => "ACK",
        StickProtocol.TypeHelloRsp => "HELLO_RSP",
        StickProtocol.TypePong => "PONG",
        StickProtocol.TypeSetStatusRsp => "SET_STATUS_RSP",
        StickProtocol.TypeEvtSetStopped => "EVT_SET_STOPPED",
        StickProtocol.TypeEvtButton => "EVT_BUTTON",
        StickProtocol.TypeEvtTimeout => "EVT_TIMEOUT",
        _ => $"0x{type:X2}",
    };

    /// <summary>
    /// Decoded disconnect reason: the stick's own 0xF0.. reasons or the HCI error code (Bluetooth Core spec,
    /// Vol 1 Part F). Format: "0x08 (connection timeout / supervision timeout)".
    /// </summary>
    public static string ReasonName(byte reason)
    {
        string name = reason switch
        {
            StickProtocol.ReasonConnectTimeout => "stick: connect timeout, peer not reachable",
            StickProtocol.ReasonCancelled => "stick: cancelled by host",
            StickProtocol.ReasonSafetyStop => "stick: safety stop / session lost",
            StickProtocol.ReasonStickError => "stick: internal error",
            0x00 => "success",
            0x05 => "authentication failure",
            0x06 => "PIN or key missing",
            0x08 => "connection timeout (supervision timeout)",
            0x0C => "command disallowed",
            0x13 => "remote user terminated connection",
            0x14 => "remote device terminated: low resources",
            0x15 => "remote device terminated: power off",
            0x16 => "connection terminated by local host",
            0x1A => "unsupported remote feature",
            0x1F => "unspecified error",
            0x22 => "LL response timeout",
            0x28 => "instant passed",
            0x2A => "different transaction collision",
            0x3A => "controller busy",
            0x3B => "unacceptable connection parameters",
            0x3C => "advertising timeout",
            0x3D => "connection terminated due to MIC failure",
            0x3E => "connection failed to be established",
            _ => "unknown",
        };

        return $"0x{reason:X2} ({name})";
    }
}
