namespace TrueSpec.Collectors.Windows.Collectors;

using System.Globalization;
using TrueSpec.Collectors.Windows.Diagnostics;
using TrueSpec.Collectors.Windows.Wmi;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;

/// <summary>
/// Wi-Fi, Ethernet and Bluetooth from CIM with live signal, band and channel
/// from netsh. A Wi-Fi card does not expose a manufacture date, so that value is
/// always reported as not available instead of guessed.
/// </summary>
public sealed class NetworkCollector : ICollector
{
    public string Id => "network";

    public string Name => "Network";

    public string Category => "Network";

    public TimeSpan RefreshInterval => TimeSpan.FromSeconds(30);

    public bool RequiresAdministrator => false;

    public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var adapters = Wmi.Query(
            "root\\cimv2",
            "SELECT * FROM Win32_NetworkAdapter WHERE PhysicalAdapter = TRUE");
        var configurations = Wmi.Query("root\\cimv2", "SELECT * FROM Win32_NetworkAdapterConfiguration")
            .ToDictionary(r => r.String("Index") ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        var drivers = Wmi.Query(
                "root\\cimv2",
                "SELECT DeviceID, DriverVersion, DriverDate, Manufacturer FROM Win32_PnPSignedDriver")
            .ToDictionary(r => r.String("DeviceID") ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        var wifi = adapters.FirstOrDefault(IsWifi);
        var ethernet = adapters.FirstOrDefault(IsEthernet);

        if (wifi is null && ethernet is null)
        {
            return Task.FromResult(ComponentSnapshot.NotAvailable(
                Id,
                Name,
                Category,
                Id,
                "No physical Wi-Fi or Ethernet adapter was reported by CIM."));
        }

        var wifiDriver = wifi is null ? null : FindDriver(drivers, wifi);
        var wifiConfig = wifi is null ? null : FindConfiguration(configurations, wifi);
        var interfaces = ParseNetsh(ProcessRunner.Run("netsh", "wlan show interfaces"));
        var driversOutput = ProcessRunner.Run("netsh", "wlan show drivers") ?? string.Empty;
        var standards = ReadStandards(driversOutput);

        var bluetooth = Wmi.Query("root\\cimv2", "SELECT * FROM Win32_PnPEntity WHERE PNPClass = 'Bluetooth'");
        var ethernetDriver = ethernet is null ? null : FindDriver(drivers, ethernet);
        var ethernetConfig = ethernet is null ? null : FindConfiguration(configurations, ethernet);

        var headline = wifi?.String("Name") ?? ethernet?.String("Name") ?? "Network";

        var specs = new List<SpecValue>
        {
            CollectorSupport.Spec("Wi-Fi adapter", wifi?.String("Name"), "CIM Win32_NetworkAdapter"),
            CollectorSupport.Spec("Wi-Fi vendor", wifi?.String("Manufacturer"), "CIM Win32_NetworkAdapter"),
            CollectorSupport.Spec("Standards", standards, "netsh wlan show drivers"),
            CollectorSupport.Spec("Driver version", wifiDriver?.String("DriverVersion"), "CIM Win32_PnPSignedDriver"),
            CollectorSupport.Spec("Driver date", wifiDriver?.DateText("DriverDate"), "CIM Win32_PnPSignedDriver"),
            CollectorSupport.Spec("Link speed", CollectorSupport.FormatMegaBitsPerSecond(wifi?.UInt64("Speed")), "CIM Win32_NetworkAdapter.Speed"),
            CollectorSupport.Spec("Signal", Get(interfaces, "Signal"), "netsh wlan show interfaces"),
            CollectorSupport.Spec("Band", Get(interfaces, "Band"), "netsh wlan show interfaces"),
            CollectorSupport.Spec("Channel", Get(interfaces, "Channel"), "netsh wlan show interfaces"),
            CollectorSupport.Spec("Radio type", Get(interfaces, "Radio type"), "netsh wlan show interfaces"),
            CollectorSupport.Spec("Receive rate", Get(interfaces, "Receive rate (Mbps)"), "netsh wlan show interfaces"),
            CollectorSupport.Spec("Transmit rate", Get(interfaces, "Transmit rate (Mbps)"), "netsh wlan show interfaces"),
            CollectorSupport.Spec("SSID", Get(interfaces, "SSID"), "netsh wlan show interfaces"),
            CollectorSupport.Spec("MAC address", wifi?.String("MACAddress"), "CIM Win32_NetworkAdapter"),
            CollectorSupport.Spec("IP address", Join(wifiConfig?.StringArray("IPAddress")), "CIM Win32_NetworkAdapterConfiguration"),
            CollectorSupport.Spec("Gateway", Join(wifiConfig?.StringArray("DefaultIPGateway")), "CIM Win32_NetworkAdapterConfiguration"),
            CollectorSupport.Spec("DNS servers", Join(wifiConfig?.StringArray("DNSServerSearchOrder")), "CIM Win32_NetworkAdapterConfiguration"),
            CollectorSupport.Spec("Hardware manufacture date", "Not reported by hardware", "No Wi-Fi card or driver exposes a manufacture date"),
            CollectorSupport.Spec("Ethernet adapter", ethernet?.String("Name"), "CIM Win32_NetworkAdapter"),
            CollectorSupport.Spec("Ethernet link speed", CollectorSupport.FormatMegaBitsPerSecond(ethernet?.UInt64("Speed")), "CIM Win32_NetworkAdapter.Speed"),
            CollectorSupport.Spec("Ethernet MAC address", ethernet?.String("MACAddress"), "CIM Win32_NetworkAdapter"),
            CollectorSupport.Spec("Ethernet driver date", ethernetDriver?.DateText("DriverDate"), "CIM Win32_PnPSignedDriver"),
            CollectorSupport.Spec("Ethernet IP address", Join(ethernetConfig?.StringArray("IPAddress")), "CIM Win32_NetworkAdapterConfiguration"),
            CollectorSupport.Spec("Bluetooth devices", bluetooth.Count == 0
                ? null
                : bluetooth.Count.ToString(CultureInfo.InvariantCulture), "CIM Win32_PnPEntity (PNPClass = Bluetooth)"),
            CollectorSupport.Spec("Bluetooth version", "Not reported by hardware", "Windows does not expose the Bluetooth core version through Win32_PnPEntity"),
        };

        return Task.FromResult(ComponentSnapshot.NotMeasurable(
            Id,
            Name,
            Category,
            Id,
            headline,
            specs,
            "Network health is based on the age of the driver and the capability of the adapter. The card itself never reports a manufacture date, so this tool shows the driver date and says the hardware date is not reported instead of guessing."));
    }

    private static bool IsWifi(WmiRow adapter)
    {
        var text = string.Join(" ", new[]
        {
            adapter.String("Name"),
            adapter.String("NetConnectionID"),
            adapter.String("AdapterType"),
            adapter.String("PNPDeviceID"),
        }.Where(v => !string.IsNullOrWhiteSpace(v)));

        return text.Contains("wi-fi", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("wifi", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("wireless", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("wlan", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("802.11", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEthernet(WmiRow adapter)
    {
        var text = string.Join(" ", new[]
        {
            adapter.String("Name"),
            adapter.String("NetConnectionID"),
            adapter.String("AdapterType"),
        }.Where(v => !string.IsNullOrWhiteSpace(v)));

        return !IsWifi(adapter) &&
               (text.Contains("ethernet", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("802.3", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("gbe", StringComparison.OrdinalIgnoreCase));
    }

    private static WmiRow? FindDriver(
        IReadOnlyDictionary<string, WmiRow> drivers,
        WmiRow adapter)
    {
        var deviceId = adapter.String("PNPDeviceID");
        return deviceId is not null && drivers.TryGetValue(deviceId, out var driver) ? driver : null;
    }

    private static WmiRow? FindConfiguration(
        IReadOnlyDictionary<string, WmiRow> configurations,
        WmiRow adapter)
    {
        var index = adapter.String("Index");
        return index is not null && configurations.TryGetValue(index, out var configuration)
            ? configuration
            : null;
    }

    private static Dictionary<string, string> ParseNetsh(string? output)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(output))
        {
            return result;
        }

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();
            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (key.Length > 0 && value.Length > 0 && !result.ContainsKey(key))
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static string? Get(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    private static string ReadStandards(string driverOutput)
    {
        if (string.IsNullOrWhiteSpace(driverOutput))
        {
            return CollectorSupport.NotReported;
        }

        var standards = new List<string>();
        if (driverOutput.Contains("802.11be", StringComparison.OrdinalIgnoreCase))
        {
            standards.Add("802.11be (Wi-Fi 7)");
        }

        if (driverOutput.Contains("802.11ax", StringComparison.OrdinalIgnoreCase))
        {
            standards.Add("802.11ax (Wi-Fi 6/6E)");
        }

        if (driverOutput.Contains("802.11ac", StringComparison.OrdinalIgnoreCase))
        {
            standards.Add("802.11ac (Wi-Fi 5)");
        }

        if (driverOutput.Contains("802.11n", StringComparison.OrdinalIgnoreCase))
        {
            standards.Add("802.11n (Wi-Fi 4)");
        }

        return standards.Count == 0 ? CollectorSupport.NotReported : string.Join(", ", standards);
    }

    private static string? Join(string[]? values) =>
        values is { Length: > 0 } ? string.Join(", ", values) : null;
}
