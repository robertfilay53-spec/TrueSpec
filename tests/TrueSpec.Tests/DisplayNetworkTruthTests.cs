using TrueSpec.Collectors.Windows.Collectors;
using TrueSpec.Collectors.Windows.Wmi;
using TrueSpec.Core.Models;
using TrueSpec.Core.Sensors;

namespace TrueSpec.Tests;

/// <summary>
/// U11 (display panel truth) + U12 (network truth), v1.3 Buyer Truth.
/// Windows-leg only: the collectors live in TrueSpec.Collectors.Windows
/// (net10.0-windows), so this file is excluded from the net10.0 leg in
/// TrueSpec.Tests.csproj, like StartupCollectorTests. The mapping and
/// formatting helpers are internal static pure functions, tested here
/// without hardware; the live tests only assert the new spec labels are
/// present on a real Windows machine (values may be "Not reported").
/// </summary>
public class DisplayNetworkTruthTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    // ---- U12: negotiated Wi-Fi standard ---------------------------------

    [Theory]
    [InlineData("802.11be", "Wi-Fi 7")]
    [InlineData("802.11ax", "Wi-Fi 6")]
    [InlineData("802.11ac", "Wi-Fi 5")]
    [InlineData("802.11n", "Wi-Fi 4")]
    public void NegotiatedStandard_MapsKnownRadioType(string radioType, string expected)
    {
        var mapped = NetworkCollector.MapNegotiatedStandard(radioType);

        Assert.Contains(expected, mapped, StringComparison.Ordinal);
        Assert.Contains(radioType, mapped, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("802.11g")]
    [InlineData("802.11b")]
    public void NegotiatedStandard_UnknownRadioType_IsNotReported(string? radioType)
    {
        Assert.Equal("Not reported by hardware", NetworkCollector.MapNegotiatedStandard(radioType));
    }

    [Fact]
    public void NegotiatedStandard_IsCaseInsensitive()
    {
        Assert.Contains("Wi-Fi 6", NetworkCollector.MapNegotiatedStandard("802.11AX"), StringComparison.Ordinal);
    }

    // ---- U12: Wi-Fi driver age ------------------------------------------

    [Fact]
    public void WifiDriverAge_TwoYearOldDriver_IsNotFlagged()
    {
        var age = NetworkCollector.FormatWifiDriverAge("2024-06-01", AsOf);

        Assert.Equal("Driver from 2024 (2 years old)", age);
    }

    [Fact]
    public void WifiDriverAge_OlderThanTwoYears_IsFlagged()
    {
        var age = NetworkCollector.FormatWifiDriverAge("2020-01-15", AsOf);

        Assert.Equal("Driver from 2020 (6 years old, older than 2 years)", age);
    }

    [Fact]
    public void WifiDriverAge_OneYearOldDriver_UsesSingular()
    {
        Assert.Equal("Driver from 2025 (1 year old)", NetworkCollector.FormatWifiDriverAge("2025-06-01", AsOf));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-date")]
    public void WifiDriverAge_MissingOrUnparseableDate_IsNotReported(string? driverDate)
    {
        Assert.Equal("Not reported by hardware", NetworkCollector.FormatWifiDriverAge(driverDate, AsOf));
    }

    [Fact]
    public void WifiDriverAge_FutureDriverDate_NeverGoesNegative()
    {
        Assert.Equal("Driver from 2027 (0 years old)", NetworkCollector.FormatWifiDriverAge("2027-01-01", AsOf));
    }

    // ---- v1.4.1 F1: link-speed sentinels --------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(long.MaxValue)]
    public void LinkSpeed_SentinelOrZero_IsNotReported(long? bitsPerSecond)
    {
        Assert.Equal("Not reported by hardware", NetworkCollector.FormatBitsPerSecond(bitsPerSecond));
    }

    [Theory]
    [InlineData(72_200_000L, "72.2 Mbps")]
    [InlineData(1_000_000_000L, "1000 Mbps")]
    [InlineData(100_000_000L, "100 Mbps")]
    public void LinkSpeed_RealSpeed_FormatsMegabits(long bitsPerSecond, string expected)
    {
        Assert.Equal(expected, NetworkCollector.FormatBitsPerSecond(bitsPerSecond));
    }

    [Fact]
    public void NormalizeCimSpeed_MaxValueSentinels_AreNull()
    {
        Assert.Null(NetworkCollector.NormalizeCimSpeed(null));
        Assert.Null(NetworkCollector.NormalizeCimSpeed(0));
        Assert.Null(NetworkCollector.NormalizeCimSpeed((ulong)long.MaxValue));
        Assert.Null(NetworkCollector.NormalizeCimSpeed(ulong.MaxValue));
        Assert.Equal(72_200_000L, NetworkCollector.NormalizeCimSpeed(72_200_000));
    }

    [Fact]
    public void NormalizeLinkSpeed_MaxValueSentinel_IsNull()
    {
        Assert.Null(NetworkCollector.NormalizeLinkSpeed(long.MaxValue));
        Assert.Null(NetworkCollector.NormalizeLinkSpeed(0));
        Assert.Null(NetworkCollector.NormalizeLinkSpeed(-1));
        Assert.Equal(72_200_000L, NetworkCollector.NormalizeLinkSpeed(72_200_000));
    }

    [Fact]
    public void BuildSpecs_EthernetSentinelSpeed_IsNotReported()
    {
        var reading = new NetworkCollector.NetworkReading(
            WifiName: null,
            WifiVendor: null,
            WifiDriverVersion: null,
            WifiDriverDate: null,
            WifiSpeedBitsPerSecond: null,
            WifiMac: null,
            WifiIp: null,
            WifiGateway: null,
            WifiDns: null,
            Standards: "Not reported by hardware",
            Signal: null,
            Band: null,
            Channel: null,
            RadioType: null,
            ReceiveRate: null,
            TransmitRate: null,
            Ssid: null,
            EthernetName: "Test Ethernet",
            EthernetSpeedBitsPerSecond: long.MaxValue,
            EthernetMac: null,
            EthernetDriverDate: null,
            EthernetIp: null,
            BluetoothCount: 0);

        var specs = NetworkCollector.BuildSpecs(reading, Array.Empty<NetworkCollector.InterfaceData>(), AsOf);

        Assert.Equal("Not reported by hardware", Assert.Single(specs, s => s.Label == SpecLabels.EthernetLinkSpeed).Value);
    }

    // ---- v1.4.1 F1: GPU current mode -----------------------------------

    [Fact]
    public void CurrentMode_StructuredFields_UsesBitDepthNotColourCount()
    {
        Assert.Equal(
            "1920 x 1080, 32-bit color",
            GpuCollector.FormatCurrentMode(1920, 1080, 32, "1920 x 1080 x 4294967296 colors"));
    }

    [Theory]
    [InlineData("1920 x 1080 x 4294967296 colors", "1920 x 1080, 32-bit color")]
    [InlineData("1920 x 1080 x 16777216 colors", "1920 x 1080, 24-bit color")]
    [InlineData("1024 x 768 x 65536 colors", "1024 x 768, 16-bit color")]
    [InlineData("800 x 600 x 256 colors", "800 x 600, 8-bit color")]
    public void SanitizeVideoModeDescription_ColourCount_ConvertsToBitDepth(string description, string expected)
    {
        Assert.Equal(expected, GpuCollector.SanitizeVideoModeDescription(description));
    }

    [Theory]
    [InlineData("1920 x 1080 x 1000000 colors", "1920 x 1080")]
    [InlineData("1920 x 1080 x True Color", "1920 x 1080")]
    [InlineData("1920 x 1080", "1920 x 1080")]
    public void SanitizeVideoModeDescription_NonColourCount_KeepsResolutionOnly(string description, string expected)
    {
        Assert.Equal(expected, GpuCollector.SanitizeVideoModeDescription(description));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a mode")]
    [InlineData("0 x 1080 x 4294967296 colors")]
    public void SanitizeVideoModeDescription_Unparseable_IsNotReported(string? description)
    {
        Assert.Equal("Not reported by hardware", GpuCollector.SanitizeVideoModeDescription(description));
    }

    [Fact]
    public void CurrentMode_MissingParts_AreLeftOutNotGuessed()
    {
        Assert.Equal("1920 x 1080", GpuCollector.FormatCurrentMode(1920, 1080, null, null));
        Assert.Equal("32-bit color", GpuCollector.FormatCurrentMode(null, null, 32, null));
        Assert.Equal("Not reported by hardware", GpuCollector.FormatCurrentMode(null, null, null, null));
        Assert.Equal("1920 x 1080", GpuCollector.FormatCurrentMode(1920, 1080, 0, "1920 x 1080 x 4294967296 colors"));
        Assert.Equal("1920 x 1080", GpuCollector.FormatCurrentMode(1920, 1080, uint.MaxValue, null));
        Assert.Equal("1920 x 1080, 32-bit color", GpuCollector.FormatCurrentMode(1920, 1080, 32, null));
    }

    [Fact]
    public void NormalizeDedicatedMemory_Sentinels_AreNull()
    {
        Assert.Null(GpuCollector.NormalizeDedicatedMemory(null));
        Assert.Null(GpuCollector.NormalizeDedicatedMemory(0));
        Assert.Null(GpuCollector.NormalizeDedicatedMemory(0xFFFFFFFF));
        Assert.Null(GpuCollector.NormalizeDedicatedMemory(ulong.MaxValue));
        Assert.Equal(8_589_934_592UL, GpuCollector.NormalizeDedicatedMemory(8_589_934_592));
    }

    // ---- v1.4.1 F1: display sentinels ----------------------------------

    [Theory]
    [InlineData(65u)]
    [InlineData(uint.MaxValue)]
    public void ColourDepth_ImpossibleValue_IsNotReported(uint bitsPerPixel)
    {
        Assert.Equal("Not reported by hardware", DisplayCollector.FormatColourDepth(bitsPerPixel));
    }

    [Theory]
    [InlineData(null, 1080u)]
    [InlineData(0u, 1080u)]
    [InlineData(uint.MaxValue, 1080u)]
    [InlineData(1920u, null)]
    [InlineData(1920u, 0u)]
    [InlineData(1920u, uint.MaxValue)]
    public void Resolution_Sentinel_IsNotReported(uint? width, uint? height)
    {
        Assert.Equal("Not reported by hardware", DisplayCollector.FormatResolution(width, height));
    }

    [Fact]
    public void Resolution_RealValues_Format()
    {
        Assert.Equal("1920 x 1080", DisplayCollector.FormatResolution(1920, 1080));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0u)]
    [InlineData(uint.MaxValue)]
    public void RefreshRate_Sentinel_IsNotReported(uint? hertz)
    {
        Assert.Equal("Not reported by hardware", DisplayCollector.FormatRefreshRate(hertz));
    }

    [Fact]
    public void RefreshRate_RealValue_Formats()
    {
        Assert.Equal("60 Hz", DisplayCollector.FormatRefreshRate(60));
    }

    // ---- v1.4.1 F4: display headline -----------------------------------

    [Fact]
    public void Headline_MissingPanelName_ShowsRealValuesFirst()
    {
        Assert.Equal(
            "1920 x 1080, 60 Hz",
            DisplayCollector.BuildHeadline("Not reported by hardware", "Not reported by hardware", "1920 x 1080", "60 Hz"));
    }

    [Fact]
    public void Headline_WithPanelName_KeepsNameFirst()
    {
        Assert.Equal(
            "ThinkPad Display 1920x1080 1920 x 1080, 60 Hz",
            DisplayCollector.BuildHeadline("ThinkPad Display 1920x1080", "Not reported by hardware", "1920 x 1080", "60 Hz"));
    }

    [Fact]
    public void Headline_ModelFallsBackWhenFriendlyNameMissing()
    {
        Assert.Equal(
            "LP140WFH 1920 x 1080",
            DisplayCollector.BuildHeadline("Not reported by hardware", "LP140WFH", "1920 x 1080", "Not reported by hardware"));
    }

    [Fact]
    public void Headline_NothingReported_IsEmpty()
    {
        Assert.Equal(
            string.Empty,
            DisplayCollector.BuildHeadline("Not reported by hardware", "Not reported by hardware", "Not reported by hardware", "Not reported by hardware"));
    }

    // ---- v1.4.1 F9: panel serial ---------------------------------------

    [Theory]
    [InlineData("0")]
    [InlineData("00000000")]
    [InlineData("Not reported by hardware")]
    public void PanelSerial_Placeholder_IsNotReported(string serial)
    {
        Assert.Equal("Not reported by hardware", DisplayCollector.FormatSerial(serial));
    }

    [Theory]
    [InlineData("ABC123")]
    [InlineData("000123")]
    [InlineData("SN987654321")]
    public void PanelSerial_RealValue_IsKept(string serial)
    {
        Assert.Equal(serial, DisplayCollector.FormatSerial(serial));
    }

    // ---- v1.4.1 F7: driver age everywhere ------------------------------

    [Fact]
    public void DriverAge_GpuDriverDate_IsFlaggedLikeWifi()
    {
        Assert.Equal(
            "Driver from 2021 (4 years old, older than 2 years)",
            CollectorSupport.FormatDriverAge("2021-11-15", AsOf));
    }

    [Fact]
    public void DriverAge_EthernetDriverDate_IsFlaggedLikeWifi()
    {
        Assert.Equal(
            "Driver from 2019 (6 years old, older than 2 years)",
            CollectorSupport.FormatDriverAge("2019-06-06", AsOf));
    }

    [Fact]
    public void BuildSpecs_EthernetDriverAge_IsFlagged()
    {
        var reading = new NetworkCollector.NetworkReading(
            WifiName: "Test Wi-Fi",
            WifiVendor: null,
            WifiDriverVersion: null,
            WifiDriverDate: "2022-03-01",
            WifiSpeedBitsPerSecond: null,
            WifiMac: null,
            WifiIp: null,
            WifiGateway: null,
            WifiDns: null,
            Standards: "Not reported by hardware",
            Signal: null,
            Band: null,
            Channel: null,
            RadioType: null,
            ReceiveRate: null,
            TransmitRate: null,
            Ssid: null,
            EthernetName: "Test Ethernet",
            EthernetSpeedBitsPerSecond: null,
            EthernetMac: null,
            EthernetDriverDate: "2019-06-06",
            EthernetIp: null,
            BluetoothCount: 0);

        var specs = NetworkCollector.BuildSpecs(reading, Array.Empty<NetworkCollector.InterfaceData>(), AsOf);

        Assert.Equal("Driver from 2019 (6 years old, older than 2 years)", Assert.Single(specs, s => s.Label == SpecLabels.EthernetDriverAge).Value);
        Assert.Equal("Driver from 2022 (4 years old, older than 2 years)", Assert.Single(specs, s => s.Label == SpecLabels.WifiDriverAge).Value);
    }

    // ---- U11: colour depth ----------------------------------------------

    [Theory]
    [InlineData(32u, "32-bit")]
    [InlineData(24u, "24-bit")]
    [InlineData(16u, "16-bit")]
    public void ColourDepth_FormatsBitsPerPixel(uint bitsPerPixel, string expected)
    {
        Assert.Equal(expected, DisplayCollector.FormatColourDepth(bitsPerPixel));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0u)]
    public void ColourDepth_MissingOrZero_IsNotReported(uint? bitsPerPixel)
    {
        Assert.Equal("Not reported by hardware", DisplayCollector.FormatColourDepth(bitsPerPixel));
    }

    // ---- U12: BuildSpecs wires the new specs in --------------------------

    [Fact]
    public void BuildSpecs_IncludesNegotiatedStandard_DriverAge_AndLabelledLinkSpeed()
    {
        var reading = new NetworkCollector.NetworkReading(
            WifiName: "Test Wi-Fi",
            WifiVendor: "Test Vendor",
            WifiDriverVersion: "1.2.3.4",
            WifiDriverDate: "2020-01-15",
            WifiSpeedBitsPerSecond: 1_201_000_000,
            WifiMac: null,
            WifiIp: null,
            WifiGateway: null,
            WifiDns: null,
            Standards: "802.11ax (Wi-Fi 6/6E)",
            Signal: "80%",
            Band: "5 GHz",
            Channel: "36",
            RadioType: "802.11ax",
            ReceiveRate: "1201",
            TransmitRate: "1201",
            Ssid: "TestNet",
            EthernetName: null,
            EthernetSpeedBitsPerSecond: null,
            EthernetMac: null,
            EthernetDriverDate: null,
            EthernetIp: null,
            BluetoothCount: 0);

        var specs = NetworkCollector.BuildSpecs(reading, Array.Empty<NetworkCollector.InterfaceData>(), AsOf);

        var standard = Assert.Single(specs, s => s.Label == SpecLabels.WifiStandardNegotiated);
        Assert.Contains("Wi-Fi 6", standard.Value, StringComparison.Ordinal);
        Assert.Equal("netsh wlan show interfaces", standard.Source);

        var driverAge = Assert.Single(specs, s => s.Label == SpecLabels.WifiDriverAge);
        Assert.Equal("Driver from 2020 (6 years old, older than 2 years)", driverAge.Value);
        Assert.Equal("CIM Win32_PnPSignedDriver", driverAge.Source);

        var linkSpeed = Assert.Single(specs, s => s.Label == SpecLabels.LinkSpeed);
        Assert.Contains("negotiated rate at scan time", linkSpeed.Label, StringComparison.Ordinal);
        Assert.DoesNotContain("throughput", linkSpeed.Label, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSpecs_NoWifiRadioOrDriverDate_ReportsBothAsNotReported()
    {
        var reading = new NetworkCollector.NetworkReading(
            WifiName: "Test Wi-Fi",
            WifiVendor: null,
            WifiDriverVersion: null,
            WifiDriverDate: null,
            WifiSpeedBitsPerSecond: null,
            WifiMac: null,
            WifiIp: null,
            WifiGateway: null,
            WifiDns: null,
            Standards: "Not reported by hardware",
            Signal: null,
            Band: null,
            Channel: null,
            RadioType: null,
            ReceiveRate: null,
            TransmitRate: null,
            Ssid: null,
            EthernetName: null,
            EthernetSpeedBitsPerSecond: null,
            EthernetMac: null,
            EthernetDriverDate: null,
            EthernetIp: null,
            BluetoothCount: 0);

        var specs = NetworkCollector.BuildSpecs(reading, Array.Empty<NetworkCollector.InterfaceData>(), AsOf);

        Assert.Equal("Not reported by hardware", Assert.Single(specs, s => s.Label == SpecLabels.WifiStandardNegotiated).Value);
        Assert.Equal("Not reported by hardware", Assert.Single(specs, s => s.Label == SpecLabels.WifiDriverAge).Value);
    }

    // ---- v1.4.1 F5: netsh parsing --------------------------------------
    //
    // Samples follow the real `netsh wlan show interfaces` format. The
    // connected sample models Jawad's laptop hardware (Intel Dual Band
    // Wireless-AC 8265, 802.11ac, 5 GHz). The two-interface sample models
    // a real multi-adapter machine: a USB adapter ("Wi-Fi 2") listed
    // first, the Intel adapter ("Wi-Fi") second, both connected.

    private const string ConnectedAc8265Sample = """
        There is 1 interface on the system:

            Name                   : Wi-Fi
            Description            : Intel(R) Dual Band Wireless-AC 8265
            GUID                   : 9e0f3a2b-1c4d-4e5f-8a6b-7c8d9e0f1a2b
            Physical address       : 00:28:f8:12:34:56
            Interface type         : Primary
            State                  : connected
            SSID                   : HomeWiFi
            BSSID                  : 00:11:22:33:44:55
            Network type           : Infrastructure
            Radio type             : 802.11ac
            Authentication         : WPA2-Personal
            Cipher                 : CCMP
            Connection mode        : Auto Connect
            Band                   : 5 GHz
            Channel                : 44
            Receive rate (Mbps)    : 866.7
            Transmit rate (Mbps)   : 866.7
            Signal                 : 82%
            Profile                : HomeWiFi

            Hosted network status  : Not available
        """;

    private const string TwoInterfaceSample = """
        There are 2 interfaces on the system:

            Name                   : Wi-Fi 2
            Description            : TP-Link Wireless USB Adapter
            GUID                   : 41e960f6-3ef5-49fa-9372-0d229f7e8284
            Physical address       : a8:29:48:b0:19:0b
            Interface type         : Primary
            State                  : connected
            SSID                   : polaris_b83c06
            BSSID                  : 94:bb:43:c9:e1:f1
            Network type           : Infrastructure
            Radio type             : 802.11n
            Authentication         : WPA2-Personal
            Cipher                 : CCMP
            Connection mode        : Auto Connect
            Band                   : 2.4 GHz
            Channel                : 6
            Receive rate (Mbps)    : 72.2
            Transmit rate (Mbps)   : 72.2
            Signal                 : 100%
            Profile                : polaris_b83c06

            Name                   : Wi-Fi
            Description            : Intel(R) Dual Band Wireless-AC 8265
            GUID                   : 9e0f3a2b-1c4d-4e5f-8a6b-7c8d9e0f1a2b
            Physical address       : 00:28:f8:12:34:56
            Interface type         : Primary
            State                  : connected
            SSID                   : HomeWiFi
            BSSID                  : 00:11:22:33:44:55
            Network type           : Infrastructure
            Radio type             : 802.11ac
            Authentication         : WPA2-Personal
            Cipher                 : CCMP
            Connection mode        : Auto Connect
            Band                   : 5 GHz
            Channel                : 44
            Receive rate (Mbps)    : 866.7
            Transmit rate (Mbps)   : 866.7
            Signal                 : 82%
            Profile                : HomeWiFi

            Hosted network status  : Not available
        """;

    private const string DisconnectedSample = """
        There is 1 interface on the system:

            Name                   : Wi-Fi
            Description            : Intel(R) Dual Band Wireless-AC 8265
            GUID                   : 9e0f3a2b-1c4d-4e5f-8a6b-7c8d9e0f1a2b
            Physical address       : 00:28:f8:12:34:56
            Interface type         : Primary
            State                  : disconnected
            Radio status           : Hardware On
                                     Software On
        """;

    private static string? GetField(IReadOnlyDictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) ? value : null;

    [Fact]
    public void ParseNetsh_ConnectedInterface_ReportsSsidSignalBandChannel()
    {
        var fields = NetworkCollector.ParseNetsh(ConnectedAc8265Sample);

        Assert.Equal("HomeWiFi", GetField(fields, "SSID"));
        Assert.Equal("82%", GetField(fields, "Signal"));
        Assert.Equal("5 GHz", GetField(fields, "Band"));
        Assert.Equal("44", GetField(fields, "Channel"));
        Assert.Equal("802.11ac", GetField(fields, "Radio type"));
        Assert.Equal("866.7", GetField(fields, "Receive rate (Mbps)"));
        Assert.Equal("866.7", GetField(fields, "Transmit rate (Mbps)"));
    }

    [Fact]
    public void ParseNetsh_NamedInterface_WinsOverFirstConnectedInterface()
    {
        var fields = NetworkCollector.ParseNetsh(TwoInterfaceSample, "Wi-Fi");

        Assert.Equal("HomeWiFi", GetField(fields, "SSID"));
        Assert.Equal("82%", GetField(fields, "Signal"));
        Assert.Equal("5 GHz", GetField(fields, "Band"));
        Assert.Equal("44", GetField(fields, "Channel"));
    }

    [Fact]
    public void ParseNetsh_WithoutPreferredName_FirstConnectedInterfaceWins()
    {
        var fields = NetworkCollector.ParseNetsh(TwoInterfaceSample);

        Assert.Equal("polaris_b83c06", GetField(fields, "SSID"));
        Assert.Equal("100%", GetField(fields, "Signal"));
    }

    [Fact]
    public void ParseNetsh_DisconnectedInterface_ReportsNoWifiFields()
    {
        var fields = NetworkCollector.ParseNetsh(DisconnectedSample);

        Assert.False(fields.ContainsKey("SSID"));
        Assert.False(fields.ContainsKey("Signal"));
        Assert.False(fields.ContainsKey("Band"));
        Assert.False(fields.ContainsKey("Channel"));
        Assert.False(fields.ContainsKey("Radio type"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("There is no wireless interface on the system.")]
    public void ParseNetsh_NoOutput_IsEmpty(string? output)
    {
        Assert.Empty(NetworkCollector.ParseNetsh(output));
    }

    [Fact]
    public void ParseNetsh_ConnectedSample_FlowsThroughToBuildSpecs()
    {
        var fields = NetworkCollector.ParseNetsh(ConnectedAc8265Sample);
        var reading = new NetworkCollector.NetworkReading(
            WifiName: "Intel(R) Dual Band Wireless-AC 8265",
            WifiVendor: "Intel",
            WifiDriverVersion: "20.70.0.0",
            WifiDriverDate: "2022-03-01",
            WifiSpeedBitsPerSecond: 72_200_000,
            WifiMac: "00:28:f8:12:34:56",
            WifiIp: null,
            WifiGateway: null,
            WifiDns: null,
            Standards: "802.11ac (Wi-Fi 5)",
            Signal: GetField(fields, "Signal"),
            Band: GetField(fields, "Band"),
            Channel: GetField(fields, "Channel"),
            RadioType: GetField(fields, "Radio type"),
            ReceiveRate: GetField(fields, "Receive rate (Mbps)"),
            TransmitRate: GetField(fields, "Transmit rate (Mbps)"),
            Ssid: GetField(fields, "SSID"),
            EthernetName: null,
            EthernetSpeedBitsPerSecond: null,
            EthernetMac: null,
            EthernetDriverDate: null,
            EthernetIp: null,
            BluetoothCount: 0);

        var specs = NetworkCollector.BuildSpecs(reading, Array.Empty<NetworkCollector.InterfaceData>(), AsOf);

        Assert.Equal("HomeWiFi", Assert.Single(specs, s => s.Label == SpecLabels.SSID).Value);
        Assert.Equal("82%", Assert.Single(specs, s => s.Label == SpecLabels.Signal).Value);
        Assert.Equal("5 GHz", Assert.Single(specs, s => s.Label == SpecLabels.Band).Value);
        Assert.Equal("44", Assert.Single(specs, s => s.Label == SpecLabels.Channel).Value);
        Assert.Equal("802.11ac", Assert.Single(specs, s => s.Label == SpecLabels.RadioType).Value);
        Assert.Equal("866.7", Assert.Single(specs, s => s.Label == SpecLabels.ReceiveRate).Value);
        Assert.Equal("866.7", Assert.Single(specs, s => s.Label == SpecLabels.TransmitRate).Value);
        Assert.Contains("Wi-Fi 5", Assert.Single(specs, s => s.Label == SpecLabels.WifiStandardNegotiated).Value, StringComparison.Ordinal);
    }

    // ---- Live (Windows hardware): labels present, values may vary -------

    [Fact]
    public async Task DisplayCollector_Live_ListsColourDepthAndActiveResolution()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var snapshot = await new DisplayCollector().CollectAsync(timeout.Token);

        Assert.Equal("display", snapshot.Id);
        Assert.Contains(snapshot.Specs, s => s.Label == SpecLabels.ColourDepth);
        Assert.Contains(snapshot.Specs, s => s.Label == SpecLabels.ActiveResolution);
        Assert.Contains(snapshot.Specs, s => s.Label == SpecLabels.NativeResolution);
        // v1.4.1 F4: the headline never leads with "Not reported by
        // hardware" when real values were reported.
        Assert.DoesNotContain("Not reported by hardware", snapshot.Headline, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NetworkCollector_Live_ListsNegotiatedStandardAndDriverAge()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var snapshot = await new NetworkCollector().CollectAsync(timeout.Token);

        Assert.Equal("network", snapshot.Id);
        Assert.Contains(snapshot.Specs, s => s.Label == SpecLabels.WifiStandardNegotiated);
        Assert.Contains(snapshot.Specs, s => s.Label == SpecLabels.WifiDriverAge);
        Assert.Contains(snapshot.Specs, s => s.Label == SpecLabels.EthernetDriverAge);
        Assert.Contains(snapshot.Specs, s => s.Label == SpecLabels.LinkSpeed);
    }

    [Fact]
    public async Task GpuCollector_Live_ListsDriverAgeAndCurrentMode()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var snapshot = await new GpuCollector(new EmptySensorMonitor()).CollectAsync(timeout.Token);

        Assert.Equal("gpu", snapshot.Id);
        Assert.Contains(snapshot.Specs, s => s.Label == SpecLabels.GPUDriverAge);
        Assert.Contains(snapshot.Specs, s => s.Label == SpecLabels.CurrentMode);
        // v1.4.1 F1: the mode never shows the 2^32 colour count.
        Assert.DoesNotContain("4294967296", Assert.Single(snapshot.Specs, s => s.Label == SpecLabels.CurrentMode).Value, StringComparison.Ordinal);
    }

    /// <summary>Sensor source with no readings, for live collector tests that do not need hardware sensors.</summary>
    private sealed class EmptySensorMonitor : ISensorMonitor
    {
        public bool IsAvailable => false;

        public string StatusText => "No sensors";

        public Task<SensorSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(SensorSnapshot.Unavailable("No sensors"));
    }

    // ---- v1.3 integration: one-line buyer summaries ----------------------

    private static NetworkCollector.NetworkReading Reading(
        string? radioType,
        long? wifiSpeedBitsPerSecond,
        string? wifiDriverDate) => new(
            WifiName: "Test Wi-Fi",
            WifiVendor: "Test Vendor",
            WifiDriverVersion: "1.2.3.4",
            WifiDriverDate: wifiDriverDate,
            WifiSpeedBitsPerSecond: wifiSpeedBitsPerSecond,
            WifiMac: null,
            WifiIp: null,
            WifiGateway: null,
            WifiDns: null,
            Standards: "802.11ax (Wi-Fi 6/6E)",
            Signal: "80%",
            Band: "5 GHz",
            Channel: "36",
            RadioType: radioType,
            ReceiveRate: "1201",
            TransmitRate: "1201",
            Ssid: "TestNet",
            EthernetName: null,
            EthernetSpeedBitsPerSecond: null,
            EthernetMac: null,
            EthernetDriverDate: null,
            EthernetIp: null,
            BluetoothCount: 0);

    [Fact]
    public void NetworkBuyerSummary_NamesNegotiatedStandard_LinkSpeed_AndDriverYear()
    {
        var summary = NetworkCollector.BuildBuyerSummary(
            Reading("802.11ax", 1_201_000_000, "2020-01-15"),
            Array.Empty<NetworkCollector.InterfaceData>());

        Assert.Equal(
            "Buyer check: 802.11ax (Wi-Fi 6) adapter, linked at 1201 Mbps (negotiated rate at scan time, not a speed test), driver from 2020.",
            summary);
        Assert.DoesNotContain("throughput", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NetworkBuyerSummary_MissingReadings_SaysNotReportedInsteadOfGuessing()
    {
        var summary = NetworkCollector.BuildBuyerSummary(
            Reading(null, null, null),
            Array.Empty<NetworkCollector.InterfaceData>());

        Assert.Equal(
            "Buyer check: Wi-Fi standard not reported by hardware, link speed not reported by hardware, driver date not reported by hardware.",
            summary);
    }

    [Fact]
    public void DisplayBuyerSummary_NamesPanelIdentity_NextToActiveResolution()
    {
        var summary = DisplayCollector.BuildBuyerSummary(
            "ACME", "XYZ123", "2023 week 4", "32-bit", "1920 x 1080");

        Assert.Equal(
            "Buyer check: panel ACME XYZ123, made 2023 week 4, colour depth 32-bit; Windows is using 1920 x 1080.",
            summary);
    }

    [Fact]
    public void DisplayBuyerSummary_MissingReadings_SaysNotReportedInsteadOfGuessing()
    {
        var summary = DisplayCollector.BuildBuyerSummary(
            "Not reported by hardware",
            "Not reported by hardware",
            "Not reported by hardware",
            "Not reported by hardware",
            "Not reported by hardware");

        Assert.Equal(
            "Buyer check: panel manufacturer and model not reported by hardware, manufacture date not reported by hardware, colour depth not reported by hardware; Windows is not reporting an active resolution.",
            summary);
    }
}
