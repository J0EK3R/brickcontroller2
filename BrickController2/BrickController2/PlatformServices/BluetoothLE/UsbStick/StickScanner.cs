// BLE scan through the stick: turns raw scan reports into the ScanResult objects BC2's device managers expect.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BrickController2.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// One scan consumer of the stick. Registers with the connection (which keeps one shared scan running),
/// receives every report on the transport thread, merges it there (cheap) and hands the result to a
/// per-scanner queue: the BC2 scan callback (device manager code) runs on a separate reader task, never on the read loop.
/// <para>
/// Windows reports advertising PDUs and scan responses separately; BC2's matching needs the name (from the
/// scan response for LEGO hubs) AND the manufacturer data / service UUIDs (advertising PDU) together. The
/// scanner therefore merges both PDUs per address and, like <c>BleScanner</c>, adds an AD 0x09 entry from the
/// cached name (BuWizz 3 needs it, DUPLO and WeDo 2.0 are only recognised with a name).
/// </para>
/// The scan itself is stateless on the stick: after a stick loss the connection restarts it.
/// </summary>
internal sealed class StickScanner
{
    // Bounds the per-address merge state during long scans in busy places.
    private const int MaxTrackedAddresses = 1024;

    private readonly UsbStickConnection _connection;
    private readonly Action<ScanResult> _callback;
    private readonly StickAddressBook _addressBook;
    private readonly ILogger _logger;

    // Merge state per address. Reports of one connection arrive on one thread, but Stop/Start may race.
    private readonly object _lock = new();
    private readonly Dictionary<string, PeerState> _peers = new();

    // Results waiting for the BC2 callback; one reader task keeps the order. Started with the first result.
    private readonly Channel<ScanResult> _results = Channel.CreateUnbounded<ScanResult>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false });
    private bool _pumpStarted;

    // Number of results queued but not yet delivered (only used by WaitUntilDelivered).
    private int _undelivered;

    public StickScanner(UsbStickConnection connection, Action<ScanResult> callback, StickAddressBook addressBook, ILogger logger)
    {
        _connection = connection;
        _callback = callback;
        _addressBook = addressBook;
        _logger = logger;
    }

    /// <summary>Starts scanning. Returns false when the stick cannot scan (no session, no scan support, refused).</summary>
    public Task<bool> StartAsync() => _connection.StartScanAsync(this);

    /// <summary>Stops this consumer; the stick's scan stops with the last one. Safe to call twice.</summary>
    public async Task StopAsync()
    {
        await _connection.StopScanAsync(this).ConfigureAwait(false);
        _results.Writer.TryComplete(); // late reports are dropped, the reader task ends after the queue is empty
        lock (_lock)
        {
            // Like BleScanner: the name cache dies with the scan.
            _peers.Clear();
        }
    }

    /// <summary>Handles one scan report (transport thread): merge, then report to BC2.</summary>
    internal void OnReport(StickScanReport report)
    {
        // Remember the address type for CONNECT (also for reports without useful data).
        _addressBook.Remember(report.Address, report.AddressType);

        Dictionary<byte, byte[]> structures = StickAdvertisementParser.Parse(report.Data);
        string? ownName = StickAdvertisementParser.GetLocalName(structures);

        ScanResult? result;
        lock (_lock)
        {
            result = Merge(report, structures, ownName);
        }

        if (result == null)
        {
            return;
        }

        Enqueue(result);
    }

    /// <summary>Queues a result for the callback and starts the reader task on first use.</summary>
    private void Enqueue(ScanResult result)
    {
        lock (_lock)
        {
            if (!_results.Writer.TryWrite(result))
            {
                return; // the scanner was stopped
            }

            _undelivered++;
            if (!_pumpStarted)
            {
                _pumpStarted = true;
                _ = Task.Run(PumpAsync);
            }
        }
    }

    /// <summary>Single reader: calls the BC2 scan callback in arrival order, off the transport thread.</summary>
    private async Task PumpAsync()
    {
        await foreach (ScanResult result in _results.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                _callback(result);
            }
            catch (Exception ex)
            {
                // A failing consumer must not stop the delivery.
                _logger.LogWarning(ex, "USB stick: scan callback failed");
            }
            finally
            {
                lock (_lock)
                {
                    _undelivered--;
                    Monitor.PulseAll(_lock);
                }
            }
        }
    }

    /// <summary>
    /// Blocks until every queued result was handed to the callback (or the timeout expired). For tests of the
    /// simulated stick, which feeds reports synchronously.
    /// </summary>
    internal void WaitUntilDelivered(TimeSpan timeout)
    {
        lock (_lock)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (_undelivered > 0 && DateTime.UtcNow < deadline)
            {
                Monitor.Wait(_lock, TimeSpan.FromMilliseconds(20));
            }
        }
    }

    /// <summary>
    /// Merges the PDU into the per-address state and builds the result. Called with the lock held.
    /// Returns null when there is nothing worth reporting.
    /// </summary>
    private ScanResult? Merge(StickScanReport report, Dictionary<byte, byte[]> structures, string? ownName)
    {
        if (!_peers.TryGetValue(report.Address, out PeerState? peer))
        {
            if (_peers.Count >= MaxTrackedAddresses)
            {
                _peers.Clear();
            }

            peer = _peers[report.Address] = new PeerState();
        }

        // Keep the last advertising PDU and the last scan response separately; the union is what BC2 sees.
        Dictionary<byte, byte[]> reported = structures
            .Where(s => StickAdvertisementParser.ReportedTypes.Contains(s.Key))
            .ToDictionary(s => s.Key, s => s.Value);
        if (report.IsScanResponse)
        {
            peer.ScanResponse = reported;
        }
        else
        {
            peer.Advertisement = reported;
        }

        if (ownName != null)
        {
            peer.Name = ownName;
        }

        var combined = new Dictionary<byte, byte[]>(peer.ScanResponse);
        foreach (var (type, data) in peer.Advertisement)
        {
            combined[type] = data; // the advertising PDU wins on a clash
        }

        if (peer.Name != null)
        {
            // Enrich with the name manually: SBrick does not like a CompleteLocalName in its own data, but
            // BuWizz 3 requires it (exactly what BleScanner does).
            combined.TryAdd(StickAdvertisementParser.AdCompleteLocalName, Encoding.ASCII.GetBytes(peer.Name));
        }
        else if (combined.Count == 0)
        {
            return null;
        }

        return new ScanResult(peer.Name, report.Address, combined);
    }

    /// <summary>Latest data and name of one peripheral.</summary>
    private sealed class PeerState
    {
        public Dictionary<byte, byte[]> Advertisement { get; set; } = new();

        public Dictionary<byte, byte[]> ScanResponse { get; set; } = new();

        public string? Name { get; set; }
    }
}
