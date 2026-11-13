[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("TrueSpec.NetworkSmoke")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("TrueSpec.Tests")]

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
/// The CIM read itself does not filter on PhysicalAdapter = TRUE: a virtual
/// wired NIC reports that flag inconsistently across Windows builds, so the
/// filter keeps physical adapters plus virtual wired NICs by name (see
/// <see cref="IsUsableAdapter"/>).
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

    // The adapter query is hoisted so the selection can be pinned and so
    // the PhysicalAdapter flag is available to the filter below: a virtual
    // wired NIC (Hyper-V/VMware/VirtualBox/virtio) is real network
    // hardware presented virtually, and on some Windows builds it reports
    // PhysicalAdapter = FALSE, so a WHERE PhysicalAdapter = TRUE filter
    // silently drops the machine's only network adapter (the v2.0.1
    // AppOnFly case: a Hyper-V VM whose Network screen showed no wired
    // adapter at all). The filter in code keeps physical adapters plus
    // virtual wired NICs by name; a Wi-Fi Direct virtual adapter or WAN
    // Miniport carries no such name and stays excluded.
    internal const string NetworkAdapterQuery = "SELECT Name, Manufacturer, MACAddress, Speed, PNPDeviceID, NetConnectionID, AdapterType, Index, PhysicalAdapter FROM Win32_NetworkAdapter";

    public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var adapters = Wmi.Query("root\\cimv2", NetworkAdapterQuery)
            .Where(IsUsableAdapter)
            .ToList();
        // A missing key must never crash the scan: two rows with no Index or
        // DeviceID would both map to "" and ToDictionary would throw. Keep
        // the first row for a key and carry on.
        var configurations = Wmi.Query("root\\cimv2", "SELECT Index, IPAddress, DefaultIPGateway, DNSServerSearchOrder FROM Win32_NetworkAdapterConfiguration")
            .GroupBy(r => r.String("Index") ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var wifi = adapters.FirstOrDefault(IsWifi);
        var ethernet = adapters.FirstOrDefault(IsEthernet);
        var drivers = ReadAdapterDrivers(wifi, ethernet);

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
        var interfacesOutput = ParseNetsh(ProcessRunner.Run("netsh", "wlan show interfaces"), wifi?.String("NetConnectionID"));
        var driversOutput = ProcessRunner.Run("netsh", "wlan show drivers") ?? string.Empty;
        var standards = ReadStandards(driversOutput);

        var bluetooth = Wmi.Query("root\\cimv2", "SELECT PNPDeviceID FROM Win32_PnPEntity WHERE PNPClass = 'Bluetooth'");
        var ethernetDriver = ethernet is null ? null : FindDriver(drivers, ethernet);
        var ethernetConfig = ethernet is null ? null : FindConfiguration(configurations, ethernet);

        var reading = new NetworkReading(
            WifiName: wifi?.String("Name"),
            WifiVendor: wifi?.String("Manufacturer"),
            WifiDriverVersion: wifiDriver?.String("DriverVersion"),
            WifiDriverDate: wifiDriver?.DateText("DriverDate"),
            WifiSpeedBitsPerSecond: NormalizeCimSpeed(wifi?.UInt64("Speed")),
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
            EthernetSpeedBitsPerSecond: NormalizeCimSpeed(ethernet?.UInt64("Speed")),
            EthernetMac: ethernet?.String("MACAddress"),
            EthernetDriverVersion: ethernetDriver?.String("DriverVersion"),
            EthernetDriverDate: ethernetDriver?.DateText("DriverDate"),
            EthernetIp: Join(ethernetConfig?.StringArray("IPAddress")),
            BluetoothCount: bluetooth.Count);

        var headline = BuildHeadline(reading, interfaces);

        return Task.FromResult(ComponentSnapshot.NotMeasurable(
            Id,
            Name,
            Category,
            Id,
            headline,
            BuildSpecs(reading, interfaces),
            "Network health is based on the age of the driver and the capability of the adapter. The card itself never reports a manufacture date, so this tool shows the driver date and says the hardware date is not reported instead of guessing. Wired adapters are also read through System.Net.NetworkInformation, so a wired-only machine still shows its real address and speed. " +
            BuildBuyerSummary(reading, interfaces)));
    }

    /// <summary>
    /// The screen headline: the Wi-Fi name, else the Ethernet name, else
    /// the wired live adapter, else the wireless live adapter. Kept
    /// separate from <see cref="CollectAsync"/> so the Hyper-V headline
    /// (the wired adapter's name, never "Network" when a wired adapter
    /// was found) is testable without Windows hardware.
    /// </summary>
    internal static string BuildHeadline(NetworkReading reading, IReadOnlyList<InterfaceData> interfaces) =>
        reading.WifiName
        ?? reading.EthernetName
        ?? PickWired(interfaces)?.DisplayName
        ?? PickWireless(interfaces)?.DisplayName
        ?? "Network";

    /// <summary>
    /// U12 one-line buyer summary (plan line ~627): the negotiated standard,
    /// the negotiated link speed, and the driver year, in one sentence a
    /// buyer can use. The link speed is the negotiated rate at scan time,
    /// labelled as such; it is never a throughput measurement. Any part the
    /// hardware did not report says so instead of being guessed.
    /// </summary>
    internal static string BuildBuyerSummary(NetworkReading reading, IReadOnlyList<InterfaceData> interfaces)
    {
        var wireless = PickWireless(interfaces);
        var wired = PickWired(interfaces);

        // A wired-only machine has no Wi-Fi standard, speed or driver to
        // report, and the old text said all three were "not reported" even
        // when the wired adapter was right there: the summary read as if
        // the machine had no network at all. Name the wired adapter
        // instead, with the same negotiated-rate wording as Wi-Fi.
        if (reading.WifiName is null && wireless is null &&
            (reading.EthernetName is not null || wired is not null))
        {
            var wiredName = reading.EthernetName ?? wired?.DisplayName;
            var namePart = wiredName is null
                ? "wired adapter"
                : string.Create(CultureInfo.InvariantCulture, $"wired adapter {wiredName}");
            var wiredSpeed = FormatBitsPerSecond(
                reading.EthernetSpeedBitsPerSecond ?? wired?.SpeedBitsPerSecond);
            var wiredSpeedPart = string.Equals(wiredSpeed, SpecLabels.NotReportedByHardware, StringComparison.Ordinal)
                ? "link speed not reported by hardware"
                : string.Create(CultureInfo.InvariantCulture, $"linked at {wiredSpeed} (negotiated rate at scan time, not a speed test)");
            var wiredDriverPart = DateTimeOffset.TryParse(
                    reading.EthernetDriverDate,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var wiredDriverDate)
                ? string.Create(CultureInfo.InvariantCulture, $"driver from {wiredDriverDate.Year}")
                : "driver date not reported by hardware";

            return string.Create(
                CultureInfo.InvariantCulture,
                $"Buyer check: {namePart}, {wiredSpeedPart}, {wiredDriverPart}.");
        }

        var standard = MapNegotiatedStandard(reading.RadioType);
        var standardPart = string.Equals(standard, SpecLabels.NotReportedByHardware, StringComparison.Ordinal)
            ? "Wi-Fi standard not reported by hardware"
            : string.Create(CultureInfo.InvariantCulture, $"{standard} adapter");

        var wifiSpeed = reading.WifiSpeedBitsPerSecond ?? PickWireless(interfaces)?.SpeedBitsPerSecond;
        var speed = FormatBitsPerSecond(wifiSpeed);
        var speedPart = string.Equals(speed, SpecLabels.NotReportedByHardware, StringComparison.Ordinal)
            ? "link speed not reported by hardware"
            : string.Create(CultureInfo.InvariantCulture, $"linked at {speed} (negotiated rate at scan time, not a speed test)");

        var driverPart = DateTimeOffset.TryParse(
                reading.WifiDriverDate,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var driverDate)
            ? string.Create(CultureInfo.InvariantCulture, $"driver from {driverDate.Year}")
            : "driver date not reported by hardware";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"Buyer check: {standardPart}, {speedPart}, {driverPart}.");
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
        int BluetoothCount,
        // Optional so older construction sites keep compiling; the
        // collector always passes it (see CollectAsync).
        string? EthernetDriverVersion = null);

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
    /// reports stays SpecLabels.NotReportedByHardware. The Wi-Fi-only fields
    /// (signal, band, channel, SSID, standards) have no NetworkInterface
    /// equivalent and stay CIM/netsh-only.
    /// </summary>
    internal static List<SpecValue> BuildSpecs(NetworkReading reading, IReadOnlyList<InterfaceData> interfaces, DateTimeOffset? asOf = null)
    {
        var referenceDate = asOf ?? DateTimeOffset.UtcNow;
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
            CollectorSupport.Spec(SpecLabels.WiFiAdapter, wifiName.Value, wifiName.Source),
            CollectorSupport.Spec(SpecLabels.WiFiVendor, reading.WifiVendor, "CIM Win32_NetworkAdapter"),
            CollectorSupport.Spec(SpecLabels.Standards, reading.Standards, "netsh wlan show drivers"),
            CollectorSupport.Spec(SpecLabels.DriverVersion, reading.WifiDriverVersion, "CIM Win32_PnPSignedDriver"),
            CollectorSupport.Spec(SpecLabels.DriverDate, reading.WifiDriverDate, "CIM Win32_PnPSignedDriver"),
            CollectorSupport.Spec(SpecLabels.WifiDriverAge, CollectorSupport.FormatDriverAge(reading.WifiDriverDate, referenceDate), "CIM Win32_PnPSignedDriver"),
            // U12 honesty rule: this is the rate the adapter negotiated at
            // scan time, not a throughput measurement. The label says so so
            // the UI never has to.
            CollectorSupport.Spec(SpecLabels.LinkSpeed, FormatBitsPerSecond(wifiSpeed), wifiSpeed == reading.WifiSpeedBitsPerSecond ? "CIM Win32_NetworkAdapter.Speed" : NetworkInterfaceSource),
            CollectorSupport.Spec(SpecLabels.Signal, reading.Signal, "netsh wlan show interfaces"),
            CollectorSupport.Spec(SpecLabels.Band, reading.Band, "netsh wlan show interfaces"),
            CollectorSupport.Spec(SpecLabels.Channel, reading.Channel, "netsh wlan show interfaces"),
            CollectorSupport.Spec(SpecLabels.RadioType, reading.RadioType, "netsh wlan show interfaces"),
            CollectorSupport.Spec(SpecLabels.WifiStandardNegotiated, MapNegotiatedStandard(reading.RadioType), "netsh wlan show interfaces"),
            CollectorSupport.Spec(SpecLabels.ReceiveRate, reading.ReceiveRate, "netsh wlan show interfaces"),
            CollectorSupport.Spec(SpecLabels.TransmitRate, reading.TransmitRate, "netsh wlan show interfaces"),
            CollectorSupport.Spec(SpecLabels.SSID, reading.Ssid, "netsh wlan show interfaces"),
            CollectorSupport.Spec(SpecLabels.MACAddress, wifiMac.Value, wifiMac.Source),
            CollectorSupport.Spec(SpecLabels.IPAddress, wifiIp.Value, wifiIp.Source),
            CollectorSupport.Spec(SpecLabels.Gateway, wifiGateway.Value, wifiGateway.Source),
            CollectorSupport.Spec(SpecLabels.DNSServers, wifiDns.Value, wifiDns.Source),
            CollectorSupport.Spec(SpecLabels.HardwareManufactureDate, SpecLabels.NotReportedByHardware, "No Wi-Fi card or driver exposes a manufacture date"),
            CollectorSupport.Spec(SpecLabels.EthernetAdapter, ethernetName.Value, ethernetName.Source),
            CollectorSupport.Spec(SpecLabels.EthernetLinkSpeed, FormatBitsPerSecond(ethernetSpeed), ethernetSpeed == reading.EthernetSpeedBitsPerSecond ? "CIM Win32_NetworkAdapter.Speed" : NetworkInterfaceSource),
            CollectorSupport.Spec(SpecLabels.EthernetStatus, wired?.Status, NetworkInterfaceSource),
            CollectorSupport.Spec(SpecLabels.EthernetMACAddress, ethernetMac.Value, ethernetMac.Source),
            CollectorSupport.Spec(SpecLabels.EthernetDriverVersion, reading.EthernetDriverVersion, "CIM Win32_PnPSignedDriver"),
            CollectorSupport.Spec(SpecLabels.EthernetDriverDate, reading.EthernetDriverDate, "CIM Win32_PnPSignedDriver"),
            CollectorSupport.Spec(SpecLabels.EthernetDriverAge, CollectorSupport.FormatDriverAge(reading.EthernetDriverDate, referenceDate), "CIM Win32_PnPSignedDriver"),
            CollectorSupport.Spec(SpecLabels.EthernetIPAddress, ethernetIp.Value, ethernetIp.Source),
            CollectorSupport.Spec(SpecLabels.EthernetGateway, ethernetGateway.Value, ethernetGateway.Source),
            CollectorSupport.Spec(SpecLabels.EthernetDNSServers, ethernetDns.Value, ethernetDns.Source),
            CollectorSupport.Spec(SpecLabels.BluetoothDevices, reading.BluetoothCount == 0
                ? null
                : reading.BluetoothCount.ToString(CultureInfo.InvariantCulture), "CIM Win32_PnPEntity (PNPClass = Bluetooth)"),
            CollectorSupport.Spec(SpecLabels.BluetoothVersion, SpecLabels.NotReportedByHardware, "Windows does not expose the Bluetooth core version through Win32_PnPEntity"),
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

    /// <summary>
    /// Whether a CIM adapter row is a real network adapter: a physical
    /// adapter, or a virtual wired NIC (Hyper-V/VMware/VirtualBox/virtio)
    /// whose PhysicalAdapter flag is not TRUE on this Windows build. A
    /// Wi-Fi Direct virtual adapter or WAN Miniport is neither: it carries
    /// no virtual-NIC name and stays excluded, because it is not a network
    /// adapter the machine actually uses.
    /// </summary>
    internal static bool IsUsableAdapter(WmiRow adapter) =>
        adapter.Bool("PhysicalAdapter") == true || IsVirtualNicName(adapter.String("Name"));

    /// <summary>
    /// A Wi-Fi Direct / Hosted Network virtual adapter is not a real Wi-Fi
    /// radio: it never carries an SSID, signal or IP of its own, but its
    /// NetworkInterfaceType is Wireless80211, so without this it is picked
    /// as the "wireless" adapter and the wired buyer summary never
    /// appears on a wired-only machine that also has one. Treated like
    /// loopback/tunnel: not a network adapter. A real Wi-Fi adapter
    /// ("Intel(R) Wi-Fi 6 AX200") carries neither marker.
    /// </summary>
    internal static bool IsWifiDirectVirtualAdapter(string? text) =>
        !string.IsNullOrWhiteSpace(text) &&
        (text.Contains("wi-fi direct", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("hosted network", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The wireless/wired classification of one live adapter, as a pure
    /// function of its interface type and description, so the Hyper-V
    /// detection path is testable without Windows hardware. A virtual
    /// wired NIC (Hyper-V/VMware/VirtualBox/virtio) is wired even though
    /// its description carries no "ethernet" marker.
    /// </summary>
    internal static (bool IsWireless, bool IsWired) ClassifyInterface(NetworkInterfaceType type, string? description)
    {
        var isWireless = type == NetworkInterfaceType.Wireless80211 ||
            (!string.IsNullOrWhiteSpace(description) &&
             (description.Contains("wi-fi", StringComparison.OrdinalIgnoreCase) ||
              description.Contains("wifi", StringComparison.OrdinalIgnoreCase) ||
              description.Contains("wireless", StringComparison.OrdinalIgnoreCase) ||
              description.Contains("wlan", StringComparison.OrdinalIgnoreCase) ||
              description.Contains("802.11", StringComparison.OrdinalIgnoreCase)));
        var isWired = !isWireless &&
            (type is NetworkInterfaceType.Ethernet
                or NetworkInterfaceType.GigabitEthernet
                or NetworkInterfaceType.FastEthernetFx
                or NetworkInterfaceType.FastEthernetT ||
             (!string.IsNullOrWhiteSpace(description) &&
              (description.Contains("ethernet", StringComparison.OrdinalIgnoreCase) ||
               description.Contains("802.3", StringComparison.OrdinalIgnoreCase) ||
               IsVirtualNicName(description))));
        return (isWireless, isWired);
    }

    /// <summary>Converts one live adapter to the fields this screen shows. Returns null for loopback, tunnel and Wi-Fi Direct virtual interfaces.</summary>
    internal static InterfaceData? FromNetworkInterface(NetworkInterface nic)
    {
        if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
        {
            return null;
        }

        var description = string.IsNullOrWhiteSpace(nic.Description) ? nic.Name : nic.Description;
        if (IsWifiDirectVirtualAdapter(description))
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

        var (isWireless, isWired) = ClassifyInterface(nic.NetworkInterfaceType, description);

        return new InterfaceData(
            Name: nic.Name,
            Description: nic.Description,
            MacAddress: FormatMac(nic.GetPhysicalAddress()),
            Ipv4Addresses: ipv4.Length == 0 ? null : string.Join(", ", ipv4),
            Gateways: gateways.Length == 0 ? null : string.Join(", ", gateways),
            DnsServers: dns.Length == 0 ? null : string.Join(", ", dns),
            SpeedBitsPerSecond: NormalizeLinkSpeed(nic.Speed),
            Status: nic.OperationalStatus.ToString(),
            IsWireless: isWireless,
            IsWired: isWired,
            IsUp: nic.OperationalStatus == OperationalStatus.Up);
    }

    internal static InterfaceData? PickWired(IReadOnlyList<InterfaceData> interfaces) =>
        interfaces.FirstOrDefault(i => i.IsWired && i.IsUp)
        ?? interfaces.FirstOrDefault(i => i.IsWired);

    /// <summary>
    /// The wireless pick, skipping Wi-Fi Direct virtual adapters by
    /// description: one constructed directly (not through
    /// <see cref="FromNetworkInterface"/>) must not shadow the wired
    /// adapter either, or the wired buyer summary never appears.
    /// </summary>
    internal static InterfaceData? PickWireless(IReadOnlyList<InterfaceData> interfaces) =>
        interfaces.FirstOrDefault(i => i.IsWireless && i.IsUp && !IsWifiDirectVirtualAdapter(i.Description))
        ?? interfaces.FirstOrDefault(i => i.IsWireless && !IsWifiDirectVirtualAdapter(i.Description));

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

    /// <summary>
    /// Normalises a link speed from <see cref="NetworkInterface.Speed"/>:
    /// 0 and negative values mean "no reading", and
    /// <see cref="long.MaxValue"/> is the "unknown speed" sentinel some
    /// drivers report (it used to display as "9223372036854.8 Mbps").
    /// Anything else is a real negotiated rate in bits per second.
    /// </summary>
    internal static long? NormalizeLinkSpeed(long speed) =>
        speed is <= 0 or long.MaxValue ? null : speed;

    /// <summary>
    /// Normalises a link speed from CIM Win32_NetworkAdapter.Speed (a
    /// UInt64): the same sentinels as <see cref="NormalizeLinkSpeed"/>,
    /// plus UInt64.MaxValue, which wraps to -1 on the cast to long.
    /// </summary>
    internal static long? NormalizeCimSpeed(ulong? bitsPerSecond)
    {
        if (bitsPerSecond is null || bitsPerSecond.Value > (ulong)long.MaxValue)
        {
            return null;
        }

        return NormalizeLinkSpeed((long)bitsPerSecond.Value);
    }

    internal static string FormatBitsPerSecond(long? bitsPerSecond) =>
        bitsPerSecond is { } value && NormalizeLinkSpeed(value) is { } speed
            ? string.Create(CultureInfo.InvariantCulture, $"{speed / 1_000_000d:0.#} Mbps")
            : SpecLabels.NotReportedByHardware;

    /// <summary>
    /// Names a virtual wired adapter uses. A Hyper-V, VMware, VirtualBox
    /// or virtio NIC carries no "ethernet" marker in its CIM name or its
    /// live description, so without this it is neither Wi-Fi nor Ethernet
    /// and the wired details never surface — the exact failure on a
    /// virtual machine. Matched case-insensitively; tunnel and loopback
    /// interfaces are excluded before this runs, so a VPN adapter cannot
    /// be mistaken for a wired one here.
    /// </summary>
    internal static bool IsVirtualNicName(string? text) =>
        !string.IsNullOrWhiteSpace(text) &&
        (text.Contains("hyper-v", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("vmware", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("virtualbox", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("virtio", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Internal (not private) so the Hyper-V detection path is testable:
    /// a CIM row named "Microsoft Hyper-V Network Adapter #2" must
    /// classify as Ethernet, never Wi-Fi.
    /// </summary>
    internal static bool IsWifi(WmiRow adapter)
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

    /// <summary>
    /// Internal (not private) so the Hyper-V detection path is testable:
    /// a CIM row named "Microsoft Hyper-V Network Adapter #2" must
    /// classify as Ethernet. A virtual wired NIC carries no "ethernet"
    /// marker in its name, so <see cref="IsVirtualNicName"/> is part of
    /// the check.
    /// </summary>
    internal static bool IsEthernet(WmiRow adapter)
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
                text.Contains("gbe", StringComparison.OrdinalIgnoreCase) ||
                IsVirtualNicName(text));
    }

    /// <summary>
    /// The drivers of the Wi-Fi and Ethernet adapters only. FindDriver
    /// matches on DeviceID, so the old unfiltered query enumerated every
    /// installed driver (hundreds of rows) to look up at most two. The
    /// filter turns it into indexed lookups; with no Wi-Fi or Ethernet
    /// adapter there is nothing to look up and no query runs at all.
    /// </summary>
    private static Dictionary<string, WmiRow> ReadAdapterDrivers(WmiRow? wifi, WmiRow? ethernet)
    {
        var deviceIds = new[] { wifi?.String("PNPDeviceID"), ethernet?.String("PNPDeviceID") }
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (deviceIds.Count == 0)
        {
            return new Dictionary<string, WmiRow>(StringComparer.OrdinalIgnoreCase);
        }

        // PNP device IDs contain backslashes but no quotes; a doubled
        // single quote is the WQL escape if one ever appears.
        var filter = string.Join(" OR ", deviceIds.Select(id =>
            string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"DeviceID = '{id.Replace("'", "''", StringComparison.Ordinal)}'")));
        return Wmi.Query(
                "root\\cimv2",
                $"SELECT DeviceID, DriverVersion, DriverDate, Manufacturer FROM Win32_PnPSignedDriver WHERE {filter}")
            .GroupBy(r => r.String("DeviceID") ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
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

    /// <summary>
    /// Parses `netsh wlan show interfaces` into the fields of one
    /// interface: the one CIM identified as the Wi-Fi adapter (matched by
    /// its NetConnectionID, which is the same name netsh prints), or, when
    /// that name is not given or not connected, the first connected
    /// interface. The old flat parser merged every interface block into a
    /// single first-wins dictionary, so on a machine with two wireless
    /// interfaces (a USB adapter, a Wi-Fi Direct virtual adapter) the
    /// screen could silently show the wrong network's SSID and signal.
    /// Each block starts at its "Name :" line; a disconnected block
    /// carries no SSID, signal, band or channel, so it can never shadow
    /// the connected interface's values. Keys are matched
    /// case-insensitively with internal whitespace collapsed, so column
    /// alignment drift between Windows builds cannot blank the screen.
    /// A missing, empty or unparseable output parses to no fields, never
    /// invented values.
    /// </summary>
    internal static Dictionary<string, string> ParseNetsh(string? output, string? preferredInterfaceName = null)
    {
        var blocks = ParseNetshBlocks(output);
        if (blocks.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        // 1. The interface CIM named, when it is the connected one.
        var named = string.IsNullOrWhiteSpace(preferredInterfaceName)
            ? null
            : blocks.FirstOrDefault(b =>
                string.Equals(b.Name, preferredInterfaceName, StringComparison.OrdinalIgnoreCase));
        if (named is { IsConnected: true })
        {
            return named.Fields;
        }

        // 2. Any connected interface with Wi-Fi fields.
        var connected = blocks.FirstOrDefault(b => b.IsConnected && HasWifiFields(b.Fields));
        if (connected is not null)
        {
            return connected.Fields;
        }

        // 3. Any interface with Wi-Fi fields (covers a connected block
        // whose State line was missing or unreadable).
        var withFields = blocks.FirstOrDefault(b => HasWifiFields(b.Fields));
        if (withFields is not null)
        {
            return withFields.Fields;
        }

        // 4. The named interface even when disconnected, then the first
        // block: both are better than inventing a network, and the caller
        // shows SpecLabels.NotReportedByHardware for whatever is still missing.
        return named?.Fields ?? blocks[0].Fields;
    }

    private static bool HasWifiFields(IReadOnlyDictionary<string, string> fields) =>
        fields.ContainsKey("SSID") || fields.ContainsKey("Signal") || fields.ContainsKey("Radio type");

    /// <summary>One interface block from netsh output, with its parsed fields.</summary>
    internal sealed record NetshInterfaceBlock(string? Name, bool IsConnected, Dictionary<string, string> Fields);

    /// <summary>
    /// Splits netsh output into one block per interface. A block starts at
    /// its "Name :" line; the "There is N interfaces" header and the
    /// "Hosted network status" trailer belong to no interface (the trailer
    /// lands in the last block's fields, where it is never looked up).
    /// Lines without a colon (the "Radio status" continuation line, blank
    /// lines) are ignored, as are keys with empty values.
    /// </summary>
    internal static List<NetshInterfaceBlock> ParseNetshBlocks(string? output)
    {
        var blocks = new List<NetshInterfaceBlock>();
        if (string.IsNullOrWhiteSpace(output))
        {
            return blocks;
        }

        Dictionary<string, string>? current = null;
        string? currentName = null;

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();
            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var key = NormalizeNetshKey(line[..separator]);
            var value = line[(separator + 1)..].Trim();
            if (key.Length == 0 || value.Length == 0)
            {
                continue;
            }

            // A new "Name :" line starts a new interface block. ("Name" is
            // the first line of every interface block in netsh output.)
            if (string.Equals(key, "Name", StringComparison.OrdinalIgnoreCase))
            {
                current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                currentName = value;
                blocks.Add(new NetshInterfaceBlock(currentName, IsConnected: false, current));
            }

            // Lines before the first Name line (the "There is N
            // interfaces" header) belong to no interface and are ignored.
            if (current is null)
            {
                continue;
            }

            if (!current.ContainsKey(key))
            {
                current[key] = value;
            }
        }

        // State is only known once the whole block has been read.
        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            var connected = block.Fields.TryGetValue("State", out var state) &&
                state.Equals("connected", StringComparison.OrdinalIgnoreCase);
            blocks[i] = block with { IsConnected = connected };
        }

        return blocks;
    }

    /// <summary>Collapses runs of whitespace in a netsh key so alignment drift cannot break the match.</summary>
    private static string NormalizeNetshKey(string key)
    {
        var parts = key.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(" ", parts);
    }

    private static string? Get(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    /// <summary>
    /// U12: the Wi-Fi generation the adapter actually negotiated, from the
    /// netsh "Radio type" value (for example "802.11ax"). Highest standard
    /// first: a radio-type string names one standard, but checking in
    /// descending order keeps the mapping correct if a value ever names
    /// more than one. Anything unrecognised is SpecLabels.NotReportedByHardware,
    /// never a guessed generation.
    /// </summary>
    internal static string MapNegotiatedStandard(string? radioType)
    {
        if (string.IsNullOrWhiteSpace(radioType))
        {
            return SpecLabels.NotReportedByHardware;
        }

        if (radioType.Contains("802.11be", StringComparison.OrdinalIgnoreCase))
        {
            return "802.11be (Wi-Fi 7)";
        }

        if (radioType.Contains("802.11ax", StringComparison.OrdinalIgnoreCase))
        {
            return "802.11ax (Wi-Fi 6)";
        }

        if (radioType.Contains("802.11ac", StringComparison.OrdinalIgnoreCase))
        {
            return "802.11ac (Wi-Fi 5)";
        }

        if (radioType.Contains("802.11n", StringComparison.OrdinalIgnoreCase))
        {
            return "802.11n (Wi-Fi 4)";
        }

        return SpecLabels.NotReportedByHardware;
    }

    /// <summary>
    /// U12: plain-text age of the Wi-Fi driver from its driver date.
    /// Delegates to the shared rule in
    /// <see cref="CollectorSupport.FormatDriverAge"/> so Wi-Fi, Ethernet
    /// and GPU drivers are all judged by the same threshold and wording.
    /// </summary>
    internal static string FormatWifiDriverAge(string? driverDate, DateTimeOffset asOf) =>
        CollectorSupport.FormatDriverAge(driverDate, asOf);

    private static string ReadStandards(string driverOutput)
    {
        if (string.IsNullOrWhiteSpace(driverOutput))
        {
            return SpecLabels.NotReportedByHardware;
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

        return standards.Count == 0 ? SpecLabels.NotReportedByHardware : string.Join(", ", standards);
    }

    private static string? Join(string[]? values) =>
        values is { Length: > 0 } ? string.Join(", ", values) : null;
}
