using TrueSpec.Collectors.Windows.Collectors;
using TrueSpec.Collectors.Windows.Wmi;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;
using TrueSpec.Core.Sensors;

namespace TrueSpec.Tests;

/// <summary>
/// WMI property-selection tests (hardening item 7, the F9 bug class).
/// <para>
/// A WmiRow only carries the properties the query asked for, so a
/// collector that reads a property it did not SELECT silently gets null and reports "Not reported by
/// hardware" for a value the machine does have. That is exactly what
/// happened to the BIOS vendor on the System page: the Motherboard
/// collector's query selected Manufacturer, the System collector's did
/// not, and the vendor showed as not reported. The pinning tests below
/// fail the moment a query's SELECT list and the properties the collector
/// reads drift apart again; the live tests run four collectors on the CI
/// machine and assert the F9 properties are actually reported.
/// </para>
/// <para>
/// Windows leg only (the collectors are Windows-only), like
/// SystemCollectorTests: excluded from the net10.0 leg in the csproj.
/// Network, Display, Storage, Security, Crash, Updates and Startup
/// collectors are not covered by the pinning test: their queries are
/// still inline, so hoisting them is follow-up work, not this batch.
/// </para>
/// </summary>
public class CollectorWmiSelectionTests
{
    private static HashSet<string> SelectList(string query)
    {
        var from = query.IndexOf(" FROM ", StringComparison.Ordinal);
        Assert.True(from > "SELECT ".Length, $"Not a SELECT query: '{query}'");
        var list = query["SELECT ".Length..from];
        return list.Split(',').Select(p => p.Trim()).ToHashSet(StringComparer.Ordinal);
    }

    private static void AssertSelects(string query, params string[] expected)
    {
        var selected = SelectList(query);
        foreach (var property in expected)
        {
            Assert.Contains(property, selected);
        }
    }

    [Fact]
    public void SystemCollector_SelectsEveryPropertyItReads()
    {
        AssertSelects(SystemCollector.ComputerSystemQuery, "CSName", "Manufacturer", "Model", "SystemFamily", "SystemSKUNumber");
        AssertSelects(SystemCollector.BiosQuery, "SerialNumber", "ReleaseDate", "SMBIOSBIOSVersion", "SMBIOSMajorVersion", "SMBIOSMinorVersion", "Version", "Manufacturer");
        AssertSelects(SystemCollector.OperatingSystemQuery, "CSName", "Caption", "BuildNumber", "InstallDate", "LastBootUpTime", "OSArchitecture", "Version");
        AssertSelects(SystemCollector.EnclosureQuery, "ChassisTypes", "SerialNumber");
        AssertSelects(SystemCollector.DeviceGuardQuery, "VirtualizationBasedSecurityStatus");
        AssertSelects(SystemCollector.TpmQuery, "SpecVersion", "IsEnabled_InitialValue");
        AssertSelects(SystemCollector.EncryptableVolumeQuery, "DriveLetter", "ProtectionStatus");
    }

    [Fact]
    public void DriverSignatureQuery_SelectsEveryPropertyItReads()
    {
        // Shared by SystemCollector and SecurityCollector
        // (CollectorSupport.ReadDriverSignatureSummary): only IsSigned is
        // ever read from these rows.
        AssertSelects(CollectorSupport.DriverSignatureQuery, "IsSigned");
    }

    [Fact]
    public void MotherboardCollector_SelectsEveryPropertyItReads()
    {
        AssertSelects(MotherboardCollector.BaseBoardQuery, "Manufacturer", "Product", "Version", "SerialNumber");
        AssertSelects(MotherboardCollector.BiosQuery, "Manufacturer", "ReleaseDate", "SMBIOSBIOSVersion", "Version", "SerialNumber");
        AssertSelects(MotherboardCollector.EnclosureQuery, "ChassisTypes", "SerialNumber");
        AssertSelects(MotherboardCollector.ComputerSystemQuery, "SystemFamily");
    }

    [Fact]
    public void CpuCollector_SelectsEveryPropertyItReads()
    {
        AssertSelects(CpuCollector.ProcessorQuery,
            "Name", "Manufacturer", "ProcessorId", "SocketDesignation", "Architecture",
            "CurrentClockSpeed", "L2CacheSize", "L3CacheSize", "MaxClockSpeed",
            "NumberOfCores", "NumberOfLogicalProcessors", "VirtualizationFirmwareEnabled");
    }

    [Fact]
    public void BatteryCollector_SelectsEveryPropertyItReads()
    {
        AssertSelects(BatteryCollector.BatteryQuery, "DesignCapacity", "FullChargeCapacity", "EstimatedChargeRemaining", "EstimatedRunTime", "Chemistry");
        AssertSelects(BatteryCollector.StaticDataQuery, "DesignedCapacity", "Chemistry", "ManufactureName", "DeviceName", "SerialNumber");
        AssertSelects(BatteryCollector.FullChargedCapacityQuery, "FullChargedCapacity");
        AssertSelects(BatteryCollector.CycleCountQuery, "CycleCount");
        AssertSelects(BatteryCollector.StatusQuery, "Voltage", "ChargeRate", "DischargeRate");
    }

    [Fact]
    public void GpuCollector_SelectsEveryPropertyItReads()
    {
        AssertSelects(GpuCollector.VideoControllerQuery,
            "Name", "AdapterCompatibility", "AdapterRAM", "DriverVersion", "DriverDate",
            "VideoProcessor", "VideoModeDescription", "CurrentHorizontalResolution",
            "CurrentVerticalResolution", "CurrentBitsPerPixel");
    }

    [Fact]
    public void RamCollector_SelectsEveryPropertyItReads()
    {
        AssertSelects(RamCollector.PhysicalMemoryQuery,
            "Capacity", "Manufacturer", "PartNumber", "SerialNumber", "BankLabel",
            "DeviceLocator", "FormFactor", "SMBIOSMemoryType", "MemoryType",
            "ConfiguredClockSpeed", "Speed");
        AssertSelects(RamCollector.PhysicalMemoryArrayQuery, "MemoryDevices");
        AssertSelects(RamCollector.ComputerSystemQuery, "TotalPhysicalMemory");
    }

    [Fact]
    public void DriversCollector_SelectsEveryPropertyItReads()
    {
        AssertSelects(DriversCollector.SignedDriverQuery, "DeviceName", "DriverDate", "DriverVersion", "IsSigned");
    }

    private static async Task<ComponentSnapshot> CollectAsync(ICollector collector)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var collectTask = collector.CollectAsync(timeout.Token);
        var finished = await Task.WhenAny(collectTask, Task.Delay(TimeSpan.FromSeconds(60)));
        Assert.True(finished == collectTask, $"{collector.Id} did not finish within 60 seconds.");
        return await collectTask;
    }

    private static string SpecValue(ComponentSnapshot snapshot, string label)
    {
        var spec = Assert.Single(
            snapshot.Specs,
            s => string.Equals(s.Label, label, StringComparison.Ordinal));
        return spec.Value;
    }

    [Fact]
    public async Task Live_SystemCollector_ReportsBiosVendorAndWindowsVersion()
    {
        // Regression test for the live F9 defect found by this batch: the
        // System collector read bios.String("Manufacturer") and
        // os.String("Version") without selecting either property, so the
        // BIOS vendor always showed as not reported and the
        // Windows build lost its version number ("build 26100" instead of
        // "10.0.26100 build 26100"). Both properties are now in the SELECT.
        var snapshot = await CollectAsync(new SystemCollector());

        Assert.NotEqual(
            SpecLabels.NotReportedByHardware,
            SpecValue(snapshot, SpecLabels.BIOSVendor));
        Assert.Contains(".", SpecValue(snapshot, SpecLabels.WindowsBuild), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Live_CpuCollector_ReportsTheCpuName()
    {
        var snapshot = await CollectAsync(new CpuCollector(new UnavailableSensorMonitor()));

        Assert.NotEqual(SpecLabels.NotReportedByHardware, SpecValue(snapshot, SpecLabels.Model));
    }

    [Fact]
    public async Task Live_MotherboardCollector_ReportsTheBoardManufacturer()
    {
        var snapshot = await CollectAsync(new MotherboardCollector());

        Assert.NotEqual(SpecLabels.NotReportedByHardware, SpecValue(snapshot, SpecLabels.Manufacturer));
    }

    [Fact]
    public async Task Live_DriversCollector_ReportsInstalledDrivers()
    {
        var snapshot = await CollectAsync(new DriversCollector());

        var spec = Assert.Single(
            snapshot.Specs,
            s => string.Equals(s.Label, SpecLabels.InstalledDrivers, StringComparison.Ordinal));
        Assert.True(spec.NumericValue is > 0, "Expected at least one installed driver on the CI machine.");
    }
}
