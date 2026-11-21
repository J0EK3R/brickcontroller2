using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using BrickController2.PlatformServices.BluetoothLE.UsbStick;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace BrickController2.Windows.PlatformServices.BluetoothLE.UsbStick;

/// <summary>
/// Finds the COM ports of devices with the USB identity of the ESP32-S3 USB Serial/JTAG peripheral
/// (VID 303A, PID 1001) through the Windows registry. The identity is shared by all ESP32-S3 boards,
/// so the shared connection additionally probes each candidate with HELLO and the magic "BC2ADV".
/// <para>
/// Registry layout: HKLM\SYSTEM\CurrentControlSet\Enum\USB\VID_303A&amp;PID_1001[&amp;MI_xx]\&lt;instance&gt;\Device Parameters,
/// value "PortName" = "COM7". The composite device lists its serial interface under the "&amp;MI_00" key.
/// Windows keeps entries of unplugged devices, so the names are intersected with the ports that exist now.
/// </para>
/// </summary>
internal sealed class RegistryStickLocator : IStickLocator
{
    private const string UsbEnumKey = @"SYSTEM\CurrentControlSet\Enum\USB";
    private const string DeviceKeyPrefix = "VID_303A&PID_1001";

    private readonly ILogger _logger;

    public RegistryStickLocator(ILogger<RegistryStickLocator> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public IEnumerable<IStickTransport> EnumerateCandidates()
    {
        foreach (string portName in FindCandidatePortNames())
        {
            yield return new SerialStickTransport(portName);
        }
    }

    /// <summary>
    /// Candidate port names, sorted. If the registry cannot be read at all, every existing port is a
    /// candidate (the HELLO probe filters them); an empty registry result means "no stick".
    /// </summary>
    private IReadOnlyList<string> FindCandidatePortNames()
    {
        string[] existingPorts = SerialPort.GetPortNames();

        try
        {
            var registryPorts = ReadPortNamesFromRegistry();
            return existingPorts
                .Where(p => registryPorts.Contains(p, StringComparer.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("USB stick: registry query failed ({Message}), probing all COM ports", ex.Message);
            return existingPorts.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    /// <summary>Reads all "PortName" values below the VID_303A&amp;PID_1001 device keys.</summary>
    private static HashSet<string> ReadPortNamesFromRegistry()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using RegistryKey? usbKey = Registry.LocalMachine.OpenSubKey(UsbEnumKey);
        if (usbKey == null)
        {
            return result;
        }

        foreach (string deviceName in usbKey.GetSubKeyNames()
                     .Where(n => n.StartsWith(DeviceKeyPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            using RegistryKey? deviceKey = usbKey.OpenSubKey(deviceName);
            if (deviceKey == null)
            {
                continue;
            }

            // One sub key per device instance (serial number or location).
            foreach (string instanceName in deviceKey.GetSubKeyNames())
            {
                using RegistryKey? parameters = deviceKey.OpenSubKey($@"{instanceName}\Device Parameters");
                if (parameters?.GetValue("PortName") is string portName && portName.Length > 0)
                {
                    result.Add(portName);
                }
            }
        }

        return result;
    }
}
