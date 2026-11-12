namespace TrueSpec.Collectors.Windows.Collectors;

using System.Globalization;
using Microsoft.Win32;
using TrueSpec.Collectors.Windows.Diagnostics;
using TrueSpec.Collectors.Windows.Wmi;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;

/// <summary>
/// Machine identity, BIOS, Windows install, TPM and security basics from real
/// CIM classes. There is no CPU, RAM or motherboard life sensor here, because
/// no such sensor exists.
/// </summary>
public sealed class SystemCollector : ICollector
{
    public string Id => "system";

    public string Name => "System";

    public string Category => "System";

    public TimeSpan RefreshInterval => TimeSpan.FromMinutes(10);

    public bool RequiresAdministrator => false;

    public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var computer = Wmi.Query("root\\cimv2", "SELECT CSName, Manufacturer, Model, SystemFamily, SystemSKUNumber FROM Win32_ComputerSystem").FirstOrDefault();
        var bios = Wmi.Query("root\\cimv2", "SELECT SerialNumber, ReleaseDate, SMBIOSBIOSVersion, SMBIOSMajorVersion, SMBIOSMinorVersion, Version FROM Win32_BIOS").FirstOrDefault();
        var os = Wmi.Query("root\\cimv2", "SELECT CSName, Caption, BuildNumber, InstallDate, LastBootUpTime, OSArchitecture FROM Win32_OperatingSystem").FirstOrDefault();
        var enclosure = Wmi.Query("root\\cimv2", "SELECT ChassisTypes, SerialNumber FROM Win32_SystemEnclosure").FirstOrDefault();

        if (computer is null && bios is null && os is null)
        {
            return Task.FromResult(ComponentSnapshot.NotAvailable(
                Id,
                Name,
                Category,
                Id,
                "Windows CIM did not return Win32_ComputerSystem, Win32_BIOS or Win32_OperatingSystem."));
        }

        var manufacturer = computer?.String("Manufacturer");
        var model = computer?.String("Model");
        var headline = string.Join(" ", new[] { manufacturer, model }.Where(v => !string.IsNullOrWhiteSpace(v)));
        if (string.IsNullOrWhiteSpace(headline))
        {
            headline = os?.String("CSName") ?? Environment.MachineName;
        }

        var chassisTypes = enclosure?.UInt16Array("ChassisTypes");
        var chassis = chassisTypes is { Length: > 0 }
            ? CollectorSupport.ChassisType(chassisTypes[0])
            : SpecLabels.NotReportedByHardware;

        var boot = os?.Date("LastBootUpTime");
        var uptime = boot is null
            ? SpecLabels.NotReportedByHardware
            : FormatUptime(DateTimeOffset.Now - boot.Value);

        var installDate = os?.DateText("InstallDate") ?? SpecLabels.NotReportedByHardware;
        var biosDate = bios?.DateText("ReleaseDate") ?? SpecLabels.NotReportedByHardware;

        var smbios = SpecLabels.NotReportedByHardware;
        if (bios?.UInt32("SMBIOSMajorVersion") is { } major)
        {
            var minor = bios?.UInt32("SMBIOSMinorVersion");
            smbios = minor is null
                ? major.ToString(CultureInfo.InvariantCulture)
                : string.Create(CultureInfo.InvariantCulture, $"{major}.{minor}");
        }

        var tpm = ReadTpm();
        var deviceGuard = Wmi.Query(
            "root\\Microsoft\\Windows\\DeviceGuard",
            "SELECT VirtualizationBasedSecurityStatus FROM Win32_DeviceGuard").FirstOrDefault();
        var secureBoot = ReadSecureBoot();
        var bitLocker = ReadBitLocker();

        var drivers = ReadDriverSignatureSummary();
        // One open of the System event log for all three readings: three
        // separate walks (critical count, memory count, earliest entry)
        // were a large share of this collector's cost on a busy log.
        var logSummary = WindowsEventLog.ReadSummary(TimeSpan.FromDays(30));
        var criticalErrors = logSummary.CriticalErrors;
        var memoryErrors = logSummary.MemoryErrors;
        var earliestLogEntry = logSummary.EarliestEntry;

        var specs = new List<SpecValue>
        {
            CollectorSupport.Spec(SpecLabels.Manufacturer, manufacturer, "CIM Win32_ComputerSystem"),
            CollectorSupport.Spec(SpecLabels.Model, model, "CIM Win32_ComputerSystem"),
            CollectorSupport.Spec(SpecLabels.SystemFamily, computer?.String("SystemFamily"), "CIM Win32_ComputerSystem"),
            CollectorSupport.Spec(SpecLabels.SKU, computer?.String("SystemSKUNumber"), "CIM Win32_ComputerSystem"),
            CollectorSupport.Spec(SpecLabels.SerialNumber, bios?.String("SerialNumber"), "CIM Win32_BIOS"),
            CollectorSupport.Spec(SpecLabels.ChassisType, chassis, "CIM Win32_SystemEnclosure"),
            CollectorSupport.Spec(SpecLabels.OperatingSystem, os?.String("Caption"), "CIM Win32_OperatingSystem"),
            CollectorSupport.Spec(SpecLabels.WindowsBuild, FormatBuild(os), "CIM Win32_OperatingSystem"),
            CollectorSupport.Spec(SpecLabels.WindowsEdition, os?.String("Caption"), "CIM Win32_OperatingSystem"),
            CollectorSupport.Spec(SpecLabels.WindowsInstallDate, installDate, "CIM Win32_OperatingSystem.InstallDate"),
            CollectorSupport.Spec(SpecLabels.Uptime, uptime, "CIM Win32_OperatingSystem.LastBootUpTime"),
            CollectorSupport.Spec(SpecLabels.Architecture, os?.String("OSArchitecture"), "CIM Win32_OperatingSystem"),
            CollectorSupport.Spec(SpecLabels.BIOSVendor, bios?.String("Manufacturer"), "CIM Win32_BIOS"),
            CollectorSupport.Spec(SpecLabels.BIOSVersion, bios?.String("SMBIOSBIOSVersion") ?? bios?.String("Version"), "CIM Win32_BIOS"),
            CollectorSupport.Spec(SpecLabels.BIOSDate, biosDate, "CIM Win32_BIOS.ReleaseDate"),
            CollectorSupport.Spec(SpecLabels.SMBIOSVersion, smbios, "CIM Win32_BIOS"),
            CollectorSupport.Spec(SpecLabels.TPMVersion, tpm.Version, "CIM Win32_Tpm"),
            CollectorSupport.Spec(SpecLabels.TPMEnabled, tpm.Enabled, "CIM Win32_Tpm"),
            CollectorSupport.Spec(SpecLabels.SecureBoot, secureBoot, "Windows registry SecureBoot\\State"),
            CollectorSupport.Spec(SpecLabels.BitLocker, bitLocker, "CIM Win32_EncryptableVolume"),
            CollectorSupport.Spec(SpecLabels.VirtualizationBasedSecurity, DeviceGuardText(deviceGuard), "CIM Win32_DeviceGuard"),
            CollectorSupport.Spec(SpecLabels.CriticalErrorsEventLog, FormatCount(criticalErrors), "Windows Event Log (System)"),
            CollectorSupport.Spec(SpecLabels.MemoryErrorsEventLog, FormatCount(memoryErrors), "Windows Event Log (WHEA-Logger)"),
            CollectorSupport.Spec(SpecLabels.EarliestEventLogEntry, earliestLogEntry?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "Windows Event Log (System)"),
            CollectorSupport.Spec(SpecLabels.InstalledDrivers, FormatCount(drivers.Total), "CIM Win32_PnPSignedDriver"),
            CollectorSupport.Spec(SpecLabels.UnsignedDrivers, FormatCount(drivers.Unsigned), "CIM Win32_PnPSignedDriver.IsSigned"),
            CollectorSupport.Spec(SpecLabels.UnknownDriverSignature, FormatCount(drivers.Unknown), "CIM Win32_PnPSignedDriver.IsSigned"),
            CollectorSupport.Spec(SpecLabels.SignedDriversOnly, drivers.Unknown switch
            {
                > 0 => SpecLabels.NotReportedByHardware,
                0 when drivers.Unsigned == 0 => "Yes",
                0 => "No",
                _ => SpecLabels.NotReportedByHardware,
            }, "CIM Win32_PnPSignedDriver.IsSigned"),
        };

        return Task.FromResult(ComponentSnapshot.NotMeasurable(
            Id,
            Name,
            Category,
            Id,
            string.IsNullOrWhiteSpace(headline) ? Environment.MachineName : headline,
            specs,
            "This block is the machine identity. Check the model and serial number against the listing. The BIOS and Windows install dates support the idea that the machine was set up recently. No CPU, RAM or motherboard life percentage is shown because hardware does not expose one."));
    }

    private static string FormatBuild(WmiRow? os)
    {
        if (os is null)
        {
            return SpecLabels.NotReportedByHardware;
        }

        var version = os.String("Version");
        var build = os.String("BuildNumber");
        if (string.IsNullOrWhiteSpace(version) && string.IsNullOrWhiteSpace(build))
        {
            return SpecLabels.NotReportedByHardware;
        }

        return string.Join(" ", new[] { version, build is null ? null : $"build {build}" }
            .Where(v => !string.IsNullOrWhiteSpace(v)));
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime < TimeSpan.Zero)
        {
            return SpecLabels.NotReportedByHardware;
        }

        return uptime.Days > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{uptime.Days} days, {uptime.Hours} hours")
            : string.Create(CultureInfo.InvariantCulture, $"{uptime.Hours} hours, {uptime.Minutes} minutes");
    }

    private static string FormatCount(int? value) =>
        value is null ? SpecLabels.NotReportedByHardware : value.Value.ToString(CultureInfo.InvariantCulture);

    private static string DeviceGuardText(WmiRow? deviceGuard)
    {
        if (deviceGuard is null)
        {
            return SpecLabels.NotReportedByHardware;
        }

        return deviceGuard.UInt32("VirtualizationBasedSecurityStatus") switch
        {
            0 => "Off",
            1 => "Enabled, not running",
            2 => "Running",
            _ => SpecLabels.NotReportedByHardware,
        };
    }

    private static (string? Version, string? Enabled) ReadTpm()
    {
        var rows = Wmi.Query("root\\CIMV2\\Security\\MicrosoftTpm", "SELECT SpecVersion, IsEnabled_InitialValue FROM Win32_Tpm");
        if (rows.Count == 0)
        {
            rows = Wmi.Query("root\\cimv2", "SELECT SpecVersion, IsEnabled_InitialValue FROM Win32_Tpm");
        }

        var tpm = rows.FirstOrDefault();
        if (tpm is null)
        {
            return (null, null);
        }

        var version = tpm.String("SpecVersion");
        if (version is not null)
        {
            version = version.Split(',')[0].Trim();
        }

        var enabled = tpm.Bool("IsEnabled_InitialValue") switch
        {
            true => "Yes",
            false => "No",
            _ => null,
        };

        return (version, enabled);
    }

    private static string ReadSecureBoot()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
            var value = key?.GetValue("UEFISecureBootEnabled");
            return value switch
            {
                int number => number == 1 ? "On" : "Off",
                null => SpecLabels.NotReportedByHardware,
                _ => SpecLabels.NotReportedByHardware,
            };
        }
        catch
        {
            return SpecLabels.NotReportedByHardware;
        }
    }

    private static string ReadBitLocker()
    {
        try
        {
            var rows = Wmi.Query(
                "root\\CIMV2\\Security\\MicrosoftVolumeEncryption",
                "SELECT DriveLetter, ProtectionStatus FROM Win32_EncryptableVolume");
            if (rows.Count == 0)
            {
                return SpecLabels.NotReportedByHardware;
            }

            var systemVolume = rows.FirstOrDefault(r =>
                string.Equals(r.String("DriveLetter"), "C:", StringComparison.OrdinalIgnoreCase))
                ?? rows[0];

            return systemVolume.UInt32("ProtectionStatus") switch
            {
                0 => "Off",
                1 => "On",
                2 => "Unknown",
                _ => SpecLabels.NotReportedByHardware,
            };
        }
        catch
        {
            return SpecLabels.NotReportedByHardware;
        }
    }

    private static (int? Total, int? Unsigned, int? Unknown) ReadDriverSignatureSummary()
    {
        // Only IsSigned is ever read from these rows: DeviceName was
        // fetched and thrown away. Win32_PnPSignedDriver has hundreds of
        // rows and every returned property is materialised per row, so
        // asking for one column is measurably cheaper — and this query runs
        // in three collectors (System, Security, Drivers).
        var rows = Wmi.Query(
            "root\\cimv2",
            "SELECT IsSigned FROM Win32_PnPSignedDriver");
        if (rows.Count == 0)
        {
            return (null, null, null);
        }

        var unsigned = rows.Count(r => r.Bool("IsSigned") is false);
        var unknown = rows.Count(r => r.Bool("IsSigned") is null);

        return (rows.Count, unsigned, unknown);
    }
}
