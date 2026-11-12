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

        var computer = Wmi.Query("root\\cimv2", "SELECT * FROM Win32_ComputerSystem").FirstOrDefault();
        var bios = Wmi.Query("root\\cimv2", "SELECT * FROM Win32_BIOS").FirstOrDefault();
        var os = Wmi.Query("root\\cimv2", "SELECT * FROM Win32_OperatingSystem").FirstOrDefault();
        var enclosure = Wmi.Query("root\\cimv2", "SELECT * FROM Win32_SystemEnclosure").FirstOrDefault();

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
            : CollectorSupport.NotReported;

        var boot = os?.Date("LastBootUpTime");
        var uptime = boot is null
            ? CollectorSupport.NotReported
            : FormatUptime(DateTimeOffset.Now - boot.Value);

        var installDate = os?.DateText("InstallDate") ?? CollectorSupport.NotReported;
        var biosDate = bios?.DateText("ReleaseDate") ?? CollectorSupport.NotReported;

        var smbios = CollectorSupport.NotReported;
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
            "SELECT * FROM Win32_DeviceGuard").FirstOrDefault();
        var secureBoot = ReadSecureBoot();
        var bitLocker = ReadBitLocker();

        var drivers = ReadDriverSignatureSummary();
        var criticalErrors = WindowsEventLog.CountCriticalErrors(TimeSpan.FromDays(30));
        var memoryErrors = WindowsEventLog.CountMemoryErrors(TimeSpan.FromDays(30));
        var earliestLogEntry = WindowsEventLog.EarliestEntry();

        var specs = new List<SpecValue>
        {
            CollectorSupport.Spec("Manufacturer", manufacturer, "CIM Win32_ComputerSystem"),
            CollectorSupport.Spec("Model", model, "CIM Win32_ComputerSystem"),
            CollectorSupport.Spec("System family", computer?.String("SystemFamily"), "CIM Win32_ComputerSystem"),
            CollectorSupport.Spec("SKU", computer?.String("SystemSKUNumber"), "CIM Win32_ComputerSystem"),
            CollectorSupport.Spec("Serial number", bios?.String("SerialNumber"), "CIM Win32_BIOS"),
            CollectorSupport.Spec("Chassis type", chassis, "CIM Win32_SystemEnclosure"),
            CollectorSupport.Spec("Operating system", os?.String("Caption"), "CIM Win32_OperatingSystem"),
            CollectorSupport.Spec("Windows build", FormatBuild(os), "CIM Win32_OperatingSystem"),
            CollectorSupport.Spec("Windows edition", os?.String("Caption"), "CIM Win32_OperatingSystem"),
            CollectorSupport.Spec("Windows install date", installDate, "CIM Win32_OperatingSystem.InstallDate"),
            CollectorSupport.Spec("Uptime", uptime, "CIM Win32_OperatingSystem.LastBootUpTime"),
            CollectorSupport.Spec("Architecture", os?.String("OSArchitecture"), "CIM Win32_OperatingSystem"),
            CollectorSupport.Spec("BIOS vendor", bios?.String("Manufacturer"), "CIM Win32_BIOS"),
            CollectorSupport.Spec("BIOS version", bios?.String("SMBIOSBIOSVersion") ?? bios?.String("Version"), "CIM Win32_BIOS"),
            CollectorSupport.Spec("BIOS date", biosDate, "CIM Win32_BIOS.ReleaseDate"),
            CollectorSupport.Spec("SMBIOS version", smbios, "CIM Win32_BIOS"),
            CollectorSupport.Spec("TPM version", tpm.Version, "CIM Win32_Tpm"),
            CollectorSupport.Spec("TPM enabled", tpm.Enabled, "CIM Win32_Tpm"),
            CollectorSupport.Spec("Secure Boot", secureBoot, "Windows registry SecureBoot\\State"),
            CollectorSupport.Spec("BitLocker", bitLocker, "CIM Win32_EncryptableVolume"),
            CollectorSupport.Spec("Virtualization-based security", DeviceGuardText(deviceGuard), "CIM Win32_DeviceGuard"),
            CollectorSupport.Spec("Critical errors (event log)", FormatCount(criticalErrors), "Windows Event Log (System)"),
            CollectorSupport.Spec("Memory errors (event log)", FormatCount(memoryErrors), "Windows Event Log (WHEA-Logger)"),
            CollectorSupport.Spec("Earliest event log entry", earliestLogEntry?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "Windows Event Log (System)"),
            CollectorSupport.Spec("Installed drivers", FormatCount(drivers.Total), "CIM Win32_PnPSignedDriver"),
            CollectorSupport.Spec("Unsigned drivers", FormatCount(drivers.Unsigned), "CIM Win32_PnPSignedDriver.IsSigned"),
            CollectorSupport.Spec("Unknown driver signature", FormatCount(drivers.Unknown), "CIM Win32_PnPSignedDriver.IsSigned"),
            CollectorSupport.Spec("Signed drivers only", drivers.Unknown switch
            {
                > 0 => CollectorSupport.NotReported,
                0 when drivers.Unsigned == 0 => "Yes",
                0 => "No",
                _ => CollectorSupport.NotReported,
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
            return CollectorSupport.NotReported;
        }

        var version = os.String("Version");
        var build = os.String("BuildNumber");
        if (string.IsNullOrWhiteSpace(version) && string.IsNullOrWhiteSpace(build))
        {
            return CollectorSupport.NotReported;
        }

        return string.Join(" ", new[] { version, build is null ? null : $"build {build}" }
            .Where(v => !string.IsNullOrWhiteSpace(v)));
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime < TimeSpan.Zero)
        {
            return CollectorSupport.NotReported;
        }

        return uptime.Days > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{uptime.Days} days, {uptime.Hours} hours")
            : string.Create(CultureInfo.InvariantCulture, $"{uptime.Hours} hours, {uptime.Minutes} minutes");
    }

    private static string FormatCount(int? value) =>
        value is null ? CollectorSupport.NotReported : value.Value.ToString(CultureInfo.InvariantCulture);

    private static string DeviceGuardText(WmiRow? deviceGuard)
    {
        if (deviceGuard is null)
        {
            return CollectorSupport.NotReported;
        }

        return deviceGuard.UInt32("VirtualizationBasedSecurityStatus") switch
        {
            0 => "Off",
            1 => "Enabled, not running",
            2 => "Running",
            _ => CollectorSupport.NotReported,
        };
    }

    private static (string? Version, string? Enabled) ReadTpm()
    {
        var rows = Wmi.Query("root\\CIMV2\\Security\\MicrosoftTpm", "SELECT * FROM Win32_Tpm");
        if (rows.Count == 0)
        {
            rows = Wmi.Query("root\\cimv2", "SELECT * FROM Win32_Tpm");
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
                null => CollectorSupport.NotReported,
                _ => CollectorSupport.NotReported,
            };
        }
        catch
        {
            return CollectorSupport.NotReported;
        }
    }

    private static string ReadBitLocker()
    {
        try
        {
            var rows = Wmi.Query(
                "root\\CIMV2\\Security\\MicrosoftVolumeEncryption",
                "SELECT * FROM Win32_EncryptableVolume");
            if (rows.Count == 0)
            {
                return CollectorSupport.NotReported;
            }

            var systemVolume = rows.FirstOrDefault(r =>
                string.Equals(r.String("DriveLetter"), "C:", StringComparison.OrdinalIgnoreCase))
                ?? rows[0];

            return systemVolume.UInt32("ProtectionStatus") switch
            {
                0 => "Off",
                1 => "On",
                2 => "Unknown",
                _ => CollectorSupport.NotReported,
            };
        }
        catch
        {
            return CollectorSupport.NotReported;
        }
    }

    private static (int? Total, int? Unsigned, int? Unknown) ReadDriverSignatureSummary()
    {
        var rows = Wmi.Query(
            "root\\cimv2",
            "SELECT DeviceName, IsSigned FROM Win32_PnPSignedDriver");
        if (rows.Count == 0)
        {
            return (null, null, null);
        }

        var unsigned = rows.Count(r => r.Bool("IsSigned") is false);
        var unknown = rows.Count(r => r.Bool("IsSigned") is null);

        return (rows.Count, unsigned, unknown);
    }
}
