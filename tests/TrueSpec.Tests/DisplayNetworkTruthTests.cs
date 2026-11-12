using TrueSpec.Collectors.Windows.Collectors;
using TrueSpec.Core.Models;

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
    }

    [Fact]
    public async Task NetworkCollector_Live_ListsNegotiatedStandardAndDriverAge()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var snapshot = await new NetworkCollector().CollectAsync(timeout.Token);

        Assert.Equal("network", snapshot.Id);
        Assert.Contains(snapshot.Specs, s => s.Label == SpecLabels.WifiStandardNegotiated);
        Assert.Contains(snapshot.Specs, s => s.Label == SpecLabels.WifiDriverAge);
        Assert.Contains(snapshot.Specs, s => s.Label == SpecLabels.LinkSpeed);
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
