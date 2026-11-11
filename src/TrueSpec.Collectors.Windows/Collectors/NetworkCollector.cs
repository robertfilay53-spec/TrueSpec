[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("TrueSpec.NetworkSmoke")]

namespace TrueSpec.Collectors.Windows.Collectors;

using System.Globalization;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using TrueSpec.Collectors.Windows.Diagnostics;
using TrueSpec.Collectors.Windows.Wmi;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;

/// <summary>
/// Wi-Fi, Ethernet and Bluetooth from CIM with live signal, band and channel
/// from netsh. A Wi-Fi card does not expose a manufacture date, so that value is
/// always reported as not available instead of guessed.
///
/// CIM does not always see a wired adapter (for example a Hyper-V synthetic
/// NIC on a virtual machine), so every address/speed field also falls back to
/// <see cref="NetworkInterface"/>: if CIM misses a value, the same field is
/// read from the matching live adapter, and if CIM finds no adapter at all the
/// screen is built from the live adapters instead of reporting nothing.
/// </summary>
public sealed class NetworkCollector : ICollector
{
    /// <summary>Source label used for every value read through NetworkInterface.</summary>
    internal const string NetworkInterfaceSource = "System.Net.NetworkInformation.NetworkInterface";

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
        // A missing key must never crash the scan: two rows with no Index or
        // DeviceID would both map to "" and ToDictionary would throw. Keep
        // the first row for a key and carry on.
        var configurations = Wmi.Query("root\\cimv2", "SELECT * FROM Win32_NetworkAdapterConfiguration")
            .GroupBy(r => r.String("Index") ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var drivers = Wmi.Query(
                "root\\cimv2",
                "SELECT DeviceID, DriverVersion, DriverDate, Manufacturer FROM Win32_PnPSignedDriver")
            .GroupBy(r => r.String("DeviceID") ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var wifi = adapters.FirstOrDefault(IsWifi);
        var ethernet = adapters.FirstOrDefault(IsEthernet);

        // Live adapters from the OS networking stack. This is the fallback
        // when CIM does not see an adapter, and it is also what proves a
        // wired-only machine really has working network.
        var interfaces = ReadInterfaces();

        if (wifi is null && ethernet is null && interfaces.Count == 0)
        {
            return Task.FromResult(ComponentSnapshot.NotAvailable(
                Id,
                Name,
                Category,
                Id,
                "No physical Wi-Fi or Ethernet adapter was reported by CIM, and System.Net.NetworkInformation found no network adapter either."));
        }

        var wifiDriver = wifi is null ? null : FindDriver(drivers, wifi);
        var wifiConfig = wifi is null ? null : FindConfiguration(configurations, wifi);
        var interfacesOutput = ParseNetsh(ProcessRunner.Run("netsh", "wlan show interfaces"));
        var driversOutput = ProcessRunner.Run("netsh", "wlan show drivers") ?? string.Empty;
        var standards = ReadStandards(driversOutput);

        var bluetooth = Wmi.Query("root\\cimv2", "SELECT * FROM Win32_PnPEntity WHERE PNPClass = 'Bluetooth'");
        var ethernetDriver = ethernet is null ? null : FindDriver(drivers, ethernet);
        var ethernetConfig = ethernet is null ? null : FindConfiguration(configurations, ethernet);

        var reading = new NetworkReading(
            WifiName: wifi?.String("Name"),
            WifiVendor: wifi?.String("Manufacturer"),
            WifiDriverVersion: wifiDriver?.String("DriverVersion"),
            WifiDriverDate: wifiDriver?.DateText("DriverDate"),
            WifiSpeedBitsPerSecond: wifi?.UInt64("Speed") is { } wifiSpeed ? (long)wifiSpeed : null,
            WifiMac: wifi?.String("MACAddress"),
            WifiIp: Join(wifiConfig?.StringArray("IPAddress")),
            WifiGateway: Join(wifiConfig?.StringArray("DefaultIPGateway")),
            WifiDns: Join(wifiConfig?.StringArray("DNSServerSearchOrder")),
            Standards: standards,
            Signal: Get(interfacesOutput, "Signal"),
            Band: Get(interfacesOutput, "Band"),
            Channel: Get(interfacesOutput, "Channel"),
            RadioType: Get(interfacesOutput, "Radio type"),
            ReceiveRate: Get(interfacesOutput, "Receive rate (Mbps)"),
            TransmitRate: Get(interfacesOutput, "Transmit rate (Mbps)"),
            Ssid: Get(interfacesOutput, "SSID"),
            EthernetName: ethernet?.String("Name"),
            EthernetSpeedBitsPerSecond: ethernet?.UInt64("Speed") is { } ethernetSpeed ? (long)ethernetSpeed : null,
            EthernetMac: ethernet?.String("MACAddress"),
            EthernetDriverDate: ethernetDriver?.DateText("DriverDate"),
            EthernetIp: Join(ethernetConfig?.StringArray("IPAddress")),
            BluetoothCount: bluetooth.Count);

        var headline = reading.WifiName
            ?? reading.EthernetName
            ?? PickWired(interfaces)?.DisplayName
            ?? PickWireless(interfaces)?.DisplayName
            ?? "Network";

        return Task.FromResult(ComponentSnapshot.NotMeasurable(
            Id,
            Name,
            Category,
            Id,
            headline,
            BuildSpecs(reading, interfaces),
            "Network health is based on the age of the driver and the capability of the adapter. The card itself never reports a manufacture date, so this tool shows the driver date and says the hardware date is not reported instead of guessing. Wired adapters are also read through System.Net.NetworkInformation, so a wired-only machine still shows its real address and speed."));
    }

    /// <summary>
    /// Everything CIM and netsh reported, before the NetworkInterface fallback
    /// is applied. Kept separate from <see cref="CollectAsync"/> so the merge
    /// in <see cref="BuildSpecs"/> can be tested without Windows hardware.
    /// </summary>
    internal sealed record NetworkReading(
        string? WifiName,
        string? WifiVendor,
        string? WifiDriverVersion,
        string? WifiDriverDate,
        long? WifiSpeedBitsPerSecond,
        string? WifiMac,
        string? WifiIp,
        string? WifiGateway,
        string? WifiDns,
        string Standards,
        string? Signal,
        string? Band,
        string? Channel,
        string? RadioType,
        string? ReceiveRate,
        string? TransmitRate,
        string? Ssid,
        string? EthernetName,
        long? EthernetSpeedBitsPerSecond,
        string? EthernetMac,
        string? EthernetDriverDate,
        string? EthernetIp,
        int BluetoothCount);

    /// <summary>
    /// One live adapter as reported by <see cref="NetworkInterface"/>, reduced
    /// to the fields this screen shows. Loopback and tunnel interfaces are
    /// never included.
    /// </summary>
    internal sealed record InterfaceData(
        string Name,
        string Description,
        string? MacAddress,
        string? Ipv4Addresses,
        string? Gateways,
        string? DnsServers,
        long? SpeedBitsPerSecond,
        string Status,
        bool IsWireless,
        bool IsWired,
        bool IsUp)
    {
        public string DisplayName => string.IsNullOrWhiteSpace(Description) ? Name : Description;
    }

    /// <summary>
    /// Builds the detail table. Each address/speed field prefers its CIM value
    /// and falls back to the matching live adapter; anything neither source
    /// reports stays "Not reported by hardware". The Wi-Fi-only fields
    /// (signal, band, channel, SSID, standards) have no NetworkInterface
    /// equivalent and stay CIM/netsh-only.
    /// </summary>
    internal static List<SpecValue> BuildSpecs(NetworkReading reading, IReadOnlyList<InterfaceData> interfaces)
    {
        var wireless = PickWireless(interfaces);
        var wired = PickWired(interfaces);

        var wifiName = Resolve(reading.WifiName, "CIM Win32_NetworkAdapter", wireless?.DisplayName);
        var wifiMac = Resolve(reading.WifiMac, "CIM Win32_NetworkAdapter", wireless?.MacAddress);
        var wifiIp = Resolve(reading.WifiIp, "CIM Win32_NetworkAdapterConfiguration", wireless?.Ipv4Addresses);
        var wifiGateway = Resolve(reading.WifiGateway, "CIM Win32_NetworkAdapterConfiguration", wireless?.Gateways);
        var wifiDns = Resolve(reading.WifiDns, "CIM Win32_NetworkAdapterConfiguration", wireless?.DnsServers);
        var wifiSpeed = reading.WifiSpeedBitsPerSecond ?? wireless?.SpeedBitsPerSecond;

        var ethernetName = Resolve(reading.EthernetName, "CIM Win32_NetworkAdapter", wired?.DisplayName);
        var ethernetMac = Resolve(reading.EthernetMac, "CIM Win32_NetworkAdapter", wired?.MacAddress);
        var ethernetIp = Resolve(reading.EthernetIp, "CIM Win32_NetworkAdapterConfiguration", wired?.Ipv4Addresses);
        var ethernetGateway = Resolve(null, "CIM Win32_NetworkAdapterConfiguration", wired?.Gateways);
        var ethernetDns = Resolve(null, "CIM Win32_NetworkAdapterConfiguration", wired?.DnsServers);
        var ethernetSpeed = reading.EthernetSpeedBitsPerSecond ?? wired?.SpeedBitsPerSecond;

        return new List<SpecValue>
        {
            CollectorSupport.Spec("Wi-Fi adapter", wifiName.Value, wifiName.Source),
            CollectorSupport.Spec("Wi-Fi vendor", reading.WifiVendor, "CIM Win32_NetworkAdapter"),
            CollectorSupport.Spec("Standards", reading.Standards, "netsh wlan show drivers"),
            CollectorSupport.Spec("Driver version", reading.WifiDriverVersion, "CIM Win32_PnPSignedDriver"),
            CollectorSupport.Spec("Driver date", reading.WifiDriverDate, "CIM Win32_PnPSignedDriver"),
            CollectorSupport.Spec("Link speed", FormatBitsPerSecond(wifiSpeed), wifiSpeed == reading.WifiSpeedBitsPerSecond ? "CIM Win32_NetworkAdapter.Speed" : NetworkInterfaceSource),
            CollectorSupport.Spec("Signal", reading.Signal, "netsh wlan show interfaces"),
            CollectorSupport.Spec("Band", reading.Band, "netsh wlan show interfaces"),
            CollectorSupport.Spec("Channel", reading.Channel, "netsh wlan show interfaces"),
            CollectorSupport.Spec("Radio type", reading.RadioType, "netsh wlan show interfaces"),
            CollectorSupport.Spec("Receive rate", reading.ReceiveRate, "netsh wlan show interfaces"),
            CollectorSupport.Spec("Transmit rate", reading.TransmitRate, "netsh wlan show interfaces"),
            CollectorSupport.Spec("SSID", reading.Ssid, "netsh wlan show interfaces"),
            CollectorSupport.Spec("MAC address", wifiMac.Value, wifiMac.Source),
            CollectorSupport.Spec("IP address", wifiIp.Value, wifiIp.Source),
            CollectorSupport.Spec("Gateway", wifiGateway.Value, wifiGateway.Source),
            CollectorSupport.Spec("DNS servers", wifiDns.Value, wifiDns.Source),
            CollectorSupport.Spec("Hardware manufacture date", "Not reported by hardware", "No Wi-Fi card or driver exposes a manufacture date"),
            CollectorSupport.Spec("Ethernet adapter", ethernetName.Value, ethernetName.Source),
            CollectorSupport.Spec("Ethernet link speed", FormatBitsPerSecond(ethernetSpeed), ethernetSpeed == reading.EthernetSpeedBitsPerSecond ? "CIM Win32_NetworkAdapter.Speed" : NetworkInterfaceSource),
            CollectorSupport.Spec("Ethernet status", wired?.Status, NetworkInterfaceSource),
            CollectorSupport.Spec("Ethernet MAC address", ethernetMac.Value, ethernetMac.Source),
            CollectorSupport.Spec("Ethernet driver date", reading.EthernetDriverDate, "CIM Win32_PnPSignedDriver"),
            CollectorSupport.Spec("Ethernet IP address", ethernetIp.Value, ethernetIp.Source),
            CollectorSupport.Spec("Ethernet gateway", ethernetGateway.Value, ethernetGateway.Source),
            CollectorSupport.Spec("Ethernet DNS servers", ethernetDns.Value, ethernetDns.Source),
            CollectorSupport.Spec("Bluetooth devices", reading.BluetoothCount == 0
                ? null
                : reading.BluetoothCount.ToString(CultureInfo.InvariantCulture), "CIM Win32_PnPEntity (PNPClass = Bluetooth)"),
            CollectorSupport.Spec("Bluetooth version", "Not reported by hardware", "Windows does not expose the Bluetooth core version through Win32_PnPEntity"),
        };
    }

    /// <summary>
    /// Prefers the CIM value; falls back to the live adapter when CIM reported
    /// nothing. Returns the value to display (null means "not reported") and
    /// the source label that actually produced it, so the UI never credits the
    /// wrong source.
    /// </summary>
    internal static (string? Value, string Source) Resolve(string? primary, string primarySource, string? fallback) =>
        !string.IsNullOrWhiteSpace(primary)
            ? (primary, primarySource)
            : !string.IsNullOrWhiteSpace(fallback)
                ? (fallback, NetworkInterfaceSource)
                : (null, primarySource);

    /// <summary>
    /// Live adapters from the OS networking stack. Never throws: a failure to
    /// enumerate is reported as an empty list and the CIM values stand alone.
    /// Loopback and tunnel interfaces are excluded.
    /// </summary>
    internal static IReadOnlyList<InterfaceData> ReadInterfaces()
    {
        var result = new List<InterfaceData>();

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                try
                {
                    var data = FromNetworkInterface(nic);
                    if (data is not null)
                    {
                        result.Add(data);
                    }
                }
                catch
                {
                    // One broken adapter must not hide the rest.
                }
            }
        }
        catch
        {
            // Enumeration itself failed; the CIM values stand alone.
        }

        return result;
    }

    /// <summary>Converts one live adapter to the fields this screen shows. Returns null for loopback and tunnel interfaces.</summary>
    internal static InterfaceData? FromNetworkInterface(NetworkInterface nic)
    {
        if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
        {
            return null;
        }

        var properties = nic.GetIPProperties();
        var ipv4 = properties.UnicastAddresses
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(a.Address))
            .Select(a => a.Address.ToString())
            .ToArray();
        var gateways = properties.GatewayAddresses
            .Select(g => g.Address.ToString())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToArray();
        var dns = properties.DnsAddresses
            .Select(a => a.ToString())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToArray();

        var description = string.IsNullOrWhiteSpace(nic.Description) ? nic.Name : nic.Description;
        var isWireless = nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
            description.Contains("wi-fi", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("wifi", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("wireless", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("wlan", StringComparison.OrdinalIgnoreCase) ||
            description.Contains("802.11", StringComparison.OrdinalIgnoreCase);
        var isWired = !isWireless &&
            (nic.NetworkInterfaceType is NetworkInterfaceType.Ethernet
                or NetworkInterfaceType.GigabitEthernet
                or NetworkInterfaceType.FastEthernetFx
                or NetworkInterfaceType.FastEthernetT ||
             description.Contains("ethernet", StringComparison.OrdinalIgnoreCase) ||
             description.Contains("802.3", StringComparison.OrdinalIgnoreCase));

        return new InterfaceData(
            Name: nic.Name,
            Description: nic.Description,
            MacAddress: FormatMac(nic.GetPhysicalAddress()),
            Ipv4Addresses: ipv4.Length == 0 ? null : string.Join(", ", ipv4),
            Gateways: gateways.Length == 0 ? null : string.Join(", ", gateways),
            DnsServers: dns.Length == 0 ? null : string.Join(", ", dns),
            SpeedBitsPerSecond: nic.Speed >= 0 ? nic.Speed : null,
            Status: nic.OperationalStatus.ToString(),
            IsWireless: isWireless,
            IsWired: isWired,
            IsUp: nic.OperationalStatus == OperationalStatus.Up);
    }

    internal static InterfaceData? PickWired(IReadOnlyList<InterfaceData> interfaces) =>
        interfaces.FirstOrDefault(i => i.IsWired && i.IsUp)
        ?? interfaces.FirstOrDefault(i => i.IsWired);

    internal static InterfaceData? PickWireless(IReadOnlyList<InterfaceData> interfaces) =>
        interfaces.FirstOrDefault(i => i.IsWireless && i.IsUp)
        ?? interfaces.FirstOrDefault(i => i.IsWireless);

    private static string? FormatMac(PhysicalAddress address)
    {
        var hex = address.ToString();
        if (string.IsNullOrWhiteSpace(hex) || hex.Length % 2 != 0)
        {
            return null;
        }

        var parts = new string[hex.Length / 2];
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = hex.Substring(i * 2, 2);
        }

        return string.Join(":", parts);
    }

    internal static string FormatBitsPerSecond(long? bitsPerSecond) =>
        bitsPerSecond is null
            ? CollectorSupport.NotReported
            : string.Create(CultureInfo.InvariantCulture, $"{bitsPerSecond.Value / 1_000_000d:0.#} Mbps");

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
