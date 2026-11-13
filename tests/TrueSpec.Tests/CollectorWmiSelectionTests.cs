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
/// Every collector with a WMI query is covered: the earlier batch pinned
/// System, Motherboard, CPU, Battery, GPU, RAM, Drivers, Machine DNA and
/// Software; this batch hoisted the remaining inline queries (Network,
/// Display, Storage, Security, Crash, Updates) into named constants and
/// pinned them too. Startup has no WMI query (registry only), so there
/// is nothing to pin for it.
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

    [Fact]
    public void NetworkCollector_SelectsEveryPropertyItReads()
    {
        AssertSelects(NetworkCollector.NetworkAdapterQuery,
            "Name", "Manufacturer", "MACAddress", "Speed", "PNPDeviceID",
            "NetConnectionID", "AdapterType", "Index", "PhysicalAdapter");
        AssertSelects(NetworkCollector.NetworkAdapterConfigurationQuery,
            "Index", "IPAddress", "DefaultIPGateway", "DNSServerSearchOrder");
        // Only the row count is used for Bluetooth; PNPDeviceID is the
        // property the query is about, and pinning it guards the SELECT
        // against a future edit that drops it while keeping the filter.
        AssertSelects(NetworkCollector.BluetoothDeviceQuery, "PNPDeviceID");
        // Manufacturer is selected but never read from driver rows (the
        // vendor comes from the adapter row); only the three properties
        // FindDriver callers actually read are pinned.
        AssertSelects(NetworkCollector.AdapterDriverQueryFormat, "DeviceID", "DriverVersion", "DriverDate");
    }

    [Fact]
    public void DisplayCollector_SelectsEveryPropertyItReads()
    {
        AssertSelects(DisplayCollector.MonitorIdQuery,
            "ManufacturerName", "ProductCodeID", "SerialNumberID", "UserFriendlyName",
            "YearOfManufacture", "WeekOfManufacture");
        AssertSelects(DisplayCollector.VideoControllerQuery,
            "CurrentHorizontalResolution", "CurrentVerticalResolution",
            "CurrentRefreshRate", "CurrentBitsPerPixel");
        AssertSelects(DisplayCollector.MonitorBasicDisplayParamsQuery,
            "MaxHorizontalImageSize", "MaxVerticalImageSize");
    }

    [Fact]
    public void StorageCollector_SelectsEveryPropertyItReads()
    {
        AssertSelects(StorageCollector.PhysicalDiskQuery,
            "DeviceId", "FriendlyName", "SerialNumber", "FirmwareVersion", "Size",
            "BusType", "MediaType", "HealthStatus", "OperationalStatus");
        AssertSelects(StorageCollector.LogicalDiskQuery,
            "DeviceID", "DriveType", "Size", "FreeSpace", "FileSystem");
        AssertSelects(StorageCollector.PartitionQuery, "DriveLetter", "DiskNumber");
        // The same property names are read by FromRow (class-query
        // fallback) and by FromInstance (method-call path).
        AssertSelects(StorageCollector.StorageReliabilityCounterQuery,
            "DeviceId", "Wear", "PowerOnHours", "Temperature", "ReadErrorsTotal",
            "WriteErrorsTotal", "ReadErrorsUncorrected", "WriteErrorsUncorrected",
            "StartStopCycleCount");
    }

    [Fact]
    public void SecurityCollector_SelectsEveryPropertyItReads()
    {
        AssertSelects(SecurityCollector.DeviceGuardQuery, "VirtualizationBasedSecurityStatus");
        AssertSelects(SecurityCollector.AntiVirusProductQuery, "displayName", "productState");
        // productState stays selected for symmetry with the antivirus
        // query but is not read: no per-product firewall state is shown,
        // only the product names. Only displayName is pinned.
        AssertSelects(SecurityCollector.FirewallProductQuery, "displayName");
        AssertSelects(SecurityCollector.TpmQuery,
            "SpecVersion", "IsEnabled_InitialValue", "IsActivated_InitialValue", "IsOwned_InitialValue");
        AssertSelects(SecurityCollector.EncryptableVolumeQuery, "DriveLetter", "ProtectionStatus");
    }

    [Fact]
    public void CrashCollector_SelectsEveryPropertyItReads()
    {
        AssertSelects(CrashCollector.OperatingSystemQuery, "InstallDate");
    }

    [Fact]
    public void UpdatesCollector_SelectsEveryPropertyItReads()
    {
        AssertSelects(UpdatesCollector.HotfixQuery, "HotFixID", "InstalledOn", "Description");
    }

    [Fact]
    public void MachineDnaCollector_SelectsEveryPropertyItReads()
    {
        // The F9 bug class, for the DNA collector: a property read but
        // not selected comes back null, and a real serial would silently
        // become "not reported" (or an unidentified part) on every machine.
        AssertSelects(MachineDnaCollector.BaseBoardQuery, "SerialNumber");
        AssertSelects(MachineDnaCollector.BiosQuery, "SerialNumber");
        AssertSelects(MachineDnaCollector.DiskQuery, "DeviceID", "SerialNumber");
        AssertSelects(MachineDnaCollector.MemoryQuery, "DeviceLocator", "SerialNumber");
        AssertSelects(MachineDnaCollector.ProcessorQuery, "ProcessorId");
    }

    private static WmiRow Row(params (string Name, object? Value)[] values) =>
        new(values.ToDictionary(v => v.Name, v => v.Value, StringComparer.OrdinalIgnoreCase));

    [Fact]
    public void MachineDnaCollector_Build_MapsRowsToPartsWithTruncatedSerials()
    {
        var dna = MachineDnaCollector.Build(
            new[] { Row(("SerialNumber", "ABCD1234EFGH5678")) },
            new[] { Row(("SerialNumber", "To be filled by O.E.M.")) },
            new[] { Row(("DeviceID", @"\\.\PHYSICALDRIVE0"), ("SerialNumber", "DISK000111222333")) },
            new[] { Row(("DeviceLocator", "DIMM 0"), ("SerialNumber", "MEM000111222333")) },
            new[] { Row(("ProcessorId", "BFEBFBFF000906EA")) });

        Assert.True(dna.IsAvailable);
        Assert.NotNull(dna.Hash);
        Assert.Equal(5, dna.Parts.Count);

        var board = Assert.Single(dna.Parts, p => p.Kind == TrueSpec.Core.Comparison.MachineDnaPartKind.Board);
        Assert.Equal("Base board", board.Label);
        Assert.Equal("ABCD...5678", board.SerialTruncated);

        // A placeholder BIOS serial stays in the parts list as an
        // unidentified part: it is "not reported", never an identity.
        var bios = Assert.Single(dna.Parts, p => p.Kind == TrueSpec.Core.Comparison.MachineDnaPartKind.Bios);
        Assert.False(bios.IsIdentified);
        Assert.Equal(SpecLabels.NotReportedByHardware, bios.SerialTruncated);

        var disk = Assert.Single(dna.Parts, p => p.Kind == TrueSpec.Core.Comparison.MachineDnaPartKind.Disk);
        Assert.Equal("Disk 0", disk.Label);
        Assert.Equal("DISK...2333", disk.SerialTruncated);

        var memory = Assert.Single(dna.Parts, p => p.Kind == TrueSpec.Core.Comparison.MachineDnaPartKind.Memory);
        Assert.Equal("DIMM 0", memory.Label);

        var cpu = Assert.Single(dna.Parts, p => p.Kind == TrueSpec.Core.Comparison.MachineDnaPartKind.Cpu);
        Assert.Equal("Processor", cpu.Label);
        Assert.Equal("BFEB...06EA", cpu.SerialTruncated);
    }

    [Fact]
    public void MachineDnaCollector_Build_EmptyRows_GivesDnaNotAvailable()
    {
        var dna = MachineDnaCollector.Build(
            Array.Empty<WmiRow>(),
            Array.Empty<WmiRow>(),
            Array.Empty<WmiRow>(),
            Array.Empty<WmiRow>(),
            Array.Empty<WmiRow>());

        Assert.False(dna.IsAvailable);
        Assert.Null(dna.Hash);
        Assert.Empty(dna.Parts);
    }

    [Fact]
    public void MachineDnaCollector_DiskLabel_FallsBackToTheRowIndex()
    {
        Assert.Equal("Disk 2", MachineDnaCollector.DiskLabel(Row(("SerialNumber", "X")), 2));
        Assert.Equal("Disk 0", MachineDnaCollector.DiskLabel(Row(("DeviceID", @"\\.\PHYSICALDRIVE0"), ("SerialNumber", "X")), 3));
        Assert.Equal("Memory 1", MachineDnaCollector.MemoryLabel(Row(("SerialNumber", "X")), 1));
    }

    [Fact]
    public async Task Live_MachineDnaCollector_ReturnsDnaWithoutThrowing()
    {
        // A live read on the CI machine: the collector never throws for
        // missing hardware, and any hash it reports is a real SHA-256.
        // Whether this machine reports usable serials is hardware truth,
        // not an assertion: a VM that reports only placeholders gives an
        // unavailable DNA, which is the honest answer.
        var dna = await new MachineDnaCollector().CollectAsync(CancellationToken.None);

        Assert.NotNull(dna);
        if (dna!.IsAvailable)
        {
            Assert.NotNull(dna.Hash);
            Assert.Equal(64, dna.Hash!.Length);
            Assert.NotEmpty(dna.Parts);
        }
    }

    [Fact]
    public void SoftwareCollector_SelectsEveryPropertyItReads_AndNeverSelectsAKey()
    {
        AssertSelects(SoftwareCollector.LicensingProductQuery, "ApplicationID", "Description", "LicenseStatus", "Name");
        // A product key is never read, stored or displayed: the query must
        // not select PartialProductKey (or any key-bearing property), and
        // the forbidden Win32_Product class (repair-install trigger) must
        // not appear anywhere in the query.
        Assert.DoesNotContain("PartialProductKey", SoftwareCollector.LicensingProductQuery, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ProductKeyID", SoftwareCollector.LicensingProductQuery, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Win32_Product", SoftwareCollector.LicensingProductQuery, StringComparison.Ordinal);
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
