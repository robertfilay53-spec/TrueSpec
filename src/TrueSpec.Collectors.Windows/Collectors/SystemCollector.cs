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
///
/// The reads are independent, so they run at the same time instead of one
/// after another: on a real standard-user laptop (v2.1.59, November 15,
/// 2026) the serial version took longer than the 10 second per-collector
/// budget and the whole System page went not available, even though most
/// reads finish in milliseconds. The slow reads are the 1000-row PnP
/// driver enumeration, the 30-day event log walk (up to 10,000 entries)
/// and the BitLocker provider query, each of which can take seconds on
/// its own, plus the first WMI query paying the cold WMI service start.
/// Each read group is also time-boxed, so one slow source only loses its
/// own specs, never the page: a group that does not finish in time
/// contributes nothing and its specs read as not reported, exactly as a
/// read that returned no rows does.
/// </summary>
public sealed class SystemCollector : ICollector
{
    public string Id => "system";

    public string Name => "System";

    public string Category => "System";

    public TimeSpan RefreshInterval => TimeSpan.FromMinutes(10);

    public bool RequiresAdministrator => false;

    // One read group gets at most half the per-collector budget. The
    // groups run at the same time, so the collector finishes in about
    // the slowest group's time, always inside the 10 second budget the
    // scan gives each collector, with room for assembly.
    internal static readonly TimeSpan GroupTimeout = TimeSpan.FromSeconds(5);

    // WMI query strings are hoisted so CollectorWmiSelectionTests can pin
    // the SELECT list to the properties this collector reads (hardening
    // item 7, F9 bug class): WmiRow only carries the properties the query
    // asked for, so a read of an unselected property always returns null.
    internal const string ComputerSystemQuery = "SELECT CSName, Manufacturer, Model, SystemFamily, SystemSKUNumber FROM Win32_ComputerSystem";
    internal const string BiosQuery = "SELECT SerialNumber, ReleaseDate, SMBIOSBIOSVersion, SMBIOSMajorVersion, SMBIOSMinorVersion, Version, Manufacturer FROM Win32_BIOS";
    internal const string OperatingSystemQuery = "SELECT CSName, Caption, BuildNumber, InstallDate, LastBootUpTime, OSArchitecture, Version FROM Win32_OperatingSystem";
    internal const string EnclosureQuery = "SELECT ChassisTypes, SerialNumber FROM Win32_SystemEnclosure";
    internal const string DeviceGuardQuery = "SELECT VirtualizationBasedSecurityStatus FROM Win32_DeviceGuard";
    internal const string TpmQuery = "SELECT SpecVersion, IsEnabled_InitialValue FROM Win32_Tpm";
    internal const string EncryptableVolumeQuery = "SELECT DriveLetter, ProtectionStatus FROM Win32_EncryptableVolume";

    /// <summary>
    /// The four singleton identity queries, read as one group. They are
    /// the gate for the whole snapshot: when none of ComputerSystem, BIOS
    /// or OperatingSystem returns a row, the collector reports not
    /// available, exactly as the serial version did.
    /// </summary>
    internal sealed record CoreIdentity(
        WmiRow? Computer,
        WmiRow? Bios,
        WmiRow? OperatingSystem,
        WmiRow? Enclosure);

    /// <summary>
    /// The TPM reading, or nulls when the class returns no row. A null
    /// reading means the group timed out, which reads the same way.
    /// </summary>
    internal sealed record TpmReading(string? Version, string? Enabled);

    public async Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Every group starts up front on the thread pool, so a slow group
        // waits out its own budget on its own task while the rest finish.
        // SecureBoot is one registry read, fast enough to run inline.
        var coreTask = RunWithTimeoutAsync(ReadCoreIdentity, GroupTimeout, cancellationToken);
        var tpmTask = RunWithTimeoutAsync(ReadTpm, GroupTimeout, cancellationToken);
        var deviceGuardTask = RunWithTimeoutAsync(ReadDeviceGuard, GroupTimeout, cancellationToken);
        var bitLockerTask = RunWithTimeoutAsync(ReadBitLocker, GroupTimeout, cancellationToken);
        var driversTask = RunWithTimeoutAsync(CollectorSupport.ReadDriverSignatureSummary, GroupTimeout, cancellationToken);
        var logTask = RunWithTimeoutAsync(ReadEventLogSummary, GroupTimeout, cancellationToken);

        var secureBoot = ReadSecureBoot();

        return BuildSnapshot(
            await coreTask,
            await tpmTask,
            await deviceGuardTask,
            secureBoot,
            await bitLockerTask,
            await driversTask,
            await logTask);
    }

    /// <summary>
    /// Runs one read group with its own time-box and converts a slow group
    /// into a missing group: the caller gets null and the specs that group
    /// feeds read as not reported. A group that finishes holds its real
    /// result. A fault inside the group escapes to the caller, the same as
    /// a fault in the old serial version. Only a caller cancellation
    /// escapes as a cancellation: a budget timeout is a missing group,
    /// never an exception. A group abandoned by its budget keeps running
    /// in the background; any later fault is observed so it can never
    /// surface as unobserved. This mirrors the per-collector race in
    /// MachineScanService, one level down.
    /// </summary>
    internal static async Task<T?> RunWithTimeoutAsync<T>(
        Func<T?> work,
        TimeSpan budget,
        CancellationToken cancellationToken)
        where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        var workTask = Task.Run(work, cancellationToken);
        var timeoutTask = Task.Delay(budget, cancellationToken);
        var completed = await Task.WhenAny(workTask, timeoutTask);
        if (completed == workTask)
        {
            return await workTask;
        }

        cancellationToken.ThrowIfCancellationRequested();

        _ = workTask.ContinueWith(
            task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
        return null;
    }

    /// <summary>
    /// Pure snapshot assembly from the read groups, so the partial-result
    /// path is unit-testable without live WMI (same pattern as
    /// DriverSignatureSpecs): a missing group leaves its specs as not
    /// reported while every other spec keeps its measured value. Only a
    /// missing core group reports the whole component as not available.
    /// </summary>
    internal static ComponentSnapshot BuildSnapshot(
        CoreIdentity? core,
        TpmReading? tpm,
        WmiRow? deviceGuard,
        string secureBoot,
        string? bitLocker,
        CollectorSupport.DriverSignatureSummary? drivers,
        WindowsEventLog.SystemLogSummary? logSummary)
    {
        const string id = "system";
        const string name = "System";
        const string category = "System";

        var computer = core?.Computer;
        var bios = core?.Bios;
        var os = core?.OperatingSystem;
        var enclosure = core?.Enclosure;

        if (computer is null && bios is null && os is null)
        {
            return ComponentSnapshot.NotAvailable(
                id,
                name,
                category,
                id,
                "Windows CIM did not return Win32_ComputerSystem, Win32_BIOS or Win32_OperatingSystem.");
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

        var criticalErrors = logSummary?.CriticalErrors;
        var memoryErrors = logSummary?.MemoryErrors;
        var earliestLogEntry = logSummary?.EarliestEntry;

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
            CollectorSupport.Spec(SpecLabels.BIOSVendor, bios?.String("Manufacturer"), "CIM Win32_BIOS"), // Manufacturer must stay in BiosQuery (F9 class).
            CollectorSupport.Spec(SpecLabels.BIOSVersion, bios?.String("SMBIOSBIOSVersion") ?? bios?.String("Version"), "CIM Win32_BIOS"),
            CollectorSupport.Spec(SpecLabels.BIOSDate, biosDate, "CIM Win32_BIOS.ReleaseDate"),
            CollectorSupport.Spec(SpecLabels.SMBIOSVersion, smbios, "CIM Win32_BIOS"),
            CollectorSupport.Spec(SpecLabels.TPMVersion, tpm?.Version, "CIM Win32_Tpm"),
            CollectorSupport.Spec(SpecLabels.TPMEnabled, tpm?.Enabled, "CIM Win32_Tpm"),
            CollectorSupport.Spec(SpecLabels.SecureBoot, secureBoot, "Windows registry SecureBoot\\State"),
            CollectorSupport.Spec(SpecLabels.BitLocker, bitLocker, "CIM Win32_EncryptableVolume"),
            CollectorSupport.Spec(SpecLabels.VirtualizationBasedSecurity, DeviceGuardText(deviceGuard), "CIM Win32_DeviceGuard"),
            CollectorSupport.Spec(SpecLabels.CriticalErrorsEventLog, FormatCount(criticalErrors), "Windows Event Log (System)"),
            CollectorSupport.Spec(SpecLabels.MemoryErrorsEventLog, FormatCount(memoryErrors), "Windows Event Log (WHEA-Logger)"),
            CollectorSupport.Spec(SpecLabels.EarliestEventLogEntry, earliestLogEntry?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "Windows Event Log (System)"),
        };

        // The driver-signature specs are built by a pure helper (same
        // pattern as DriversCollector.BuildSnapshot, PR #12) so the
        // truncation handling is unit-testable without live WMI. They
        // are the last specs in the list. A missing driver group reads
        // as no counts at all, never a guessed number.
        var driverSummary = drivers ?? new CollectorSupport.DriverSignatureSummary(null, null, null, Truncated: false);
        specs.AddRange(DriverSignatureSpecs(driverSummary));

        return ComponentSnapshot.NotMeasurable(
            id,
            name,
            category,
            id,
            string.IsNullOrWhiteSpace(headline) ? Environment.MachineName : headline,
            specs,
            "This block is the machine identity. Check the model and serial number against the listing. The BIOS and Windows install dates support the idea that the machine was set up recently. No CPU, RAM or motherboard life percentage is shown because hardware does not expose one.");
    }

    private static CoreIdentity ReadCoreIdentity() => new(
        Wmi.Query("root\\cimv2", ComputerSystemQuery).FirstOrDefault(),
        Wmi.Query("root\\cimv2", BiosQuery).FirstOrDefault(),
        Wmi.Query("root\\cimv2", OperatingSystemQuery).FirstOrDefault(),
        Wmi.Query("root\\cimv2", EnclosureQuery).FirstOrDefault());

    private static WmiRow? ReadDeviceGuard() => Wmi.Query(
        "root\\Microsoft\\Windows\\DeviceGuard",
        DeviceGuardQuery).FirstOrDefault();

    private static WindowsEventLog.SystemLogSummary ReadEventLogSummary() =>
        WindowsEventLog.ReadSummary(TimeSpan.FromDays(30));

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

    /// <summary>
    /// Pure driver-signature specs, so the truncation handling is
    /// unit-testable without live WMI (same pattern as
    /// DriversCollector.BuildSnapshot, PR #12). The counts are the rows
    /// actually read, qualified by the truncation note right after the
    /// installed-drivers count when the list was truncated. "Signed
    /// drivers only" is never "Yes" from a partial list: with unknown
    /// signatures it is already not reported, and with a truncated list
    /// it is not reported too, because unsigned drivers may sit beyond
    /// the first 1000 rows.
    /// </summary>
    internal static List<SpecValue> DriverSignatureSpecs(CollectorSupport.DriverSignatureSummary summary)
    {
        var specs = new List<SpecValue>
        {
            CollectorSupport.Spec(SpecLabels.InstalledDrivers, FormatCount(summary.Total), "CIM Win32_PnPSignedDriver"),
            CollectorSupport.Spec(SpecLabels.UnsignedDrivers, FormatCount(summary.Unsigned), "CIM Win32_PnPSignedDriver.IsSigned"),
            CollectorSupport.Spec(SpecLabels.UnknownDriverSignature, FormatCount(summary.Unknown), "CIM Win32_PnPSignedDriver.IsSigned"),
            CollectorSupport.Spec(SpecLabels.SignedDriversOnly, SignedDriversOnly(summary), "CIM Win32_PnPSignedDriver.IsSigned"),
        };

        if (summary.Truncated)
        {
            specs.Insert(1, CollectorSupport.DriverListTruncationNote());
        }

        return specs;
    }

    private static string SignedDriversOnly(CollectorSupport.DriverSignatureSummary summary)
    {
        if (summary.Truncated)
        {
            return SpecLabels.NotReportedByHardware;
        }

        return summary.Unknown switch
        {
            > 0 => SpecLabels.NotReportedByHardware,
            0 when summary.Unsigned == 0 => "Yes",
            0 => "No",
            _ => SpecLabels.NotReportedByHardware,
        };
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

    private static TpmReading ReadTpm()
    {
        var rows = Wmi.Query("root\\CIMV2\\Security\\MicrosoftTpm", TpmQuery);
        if (rows.Count == 0)
        {
            rows = Wmi.Query("root\\cimv2", TpmQuery);
        }

        var tpm = rows.FirstOrDefault();
        if (tpm is null)
        {
            return new TpmReading(null, null);
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

        return new TpmReading(version, enabled);
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
                EncryptableVolumeQuery);
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

}
