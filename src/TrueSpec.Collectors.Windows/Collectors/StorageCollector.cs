namespace TrueSpec.Collectors.Windows.Collectors;

using System.Globalization;
using System.Management;
using TrueSpec.Collectors.Windows.Storage;
using TrueSpec.Collectors.Windows.Wmi;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;

/// <summary>
/// Real storage health from MSFT_PhysicalDisk, the Windows reliability counter
/// (NVMe Percentage Used, power-on hours, temperature, error counters) and raw
/// SATA SMART attributes when the drive is SATA.
/// </summary>
public sealed class StorageCollector : ICollector
{
    public string Id => "storage";

    public string Name => "Storage";

    public string Category => "Storage";

    public TimeSpan RefreshInterval => TimeSpan.FromMinutes(5);

    public bool RequiresAdministrator => true;

    // Hoisted so the selection can be pinned (F9 bug class): a property
    // read but not selected comes back null, so a drive value the machine
    // does have would silently show as not reported. The reliability
    // counter query is used both as the fallback read and, through
    // FromInstance, as the property list for the method-call path.
    internal const string PhysicalDiskQuery = "SELECT DeviceId, FriendlyName, SerialNumber, FirmwareVersion, Size, BusType, MediaType, HealthStatus, OperationalStatus FROM MSFT_PhysicalDisk";
    internal const string LogicalDiskQuery = "SELECT DeviceID, DriveType, Size, FreeSpace, FileSystem FROM Win32_LogicalDisk";
    internal const string PartitionQuery = "SELECT DriveLetter, DiskNumber FROM MSFT_Partition";
    internal const string StorageReliabilityCounterQuery = "SELECT DeviceId, Wear, PowerOnHours, Temperature, ReadErrorsTotal, WriteErrorsTotal, ReadErrorsUncorrected, WriteErrorsUncorrected, StartStopCycleCount FROM MSFT_StorageReliabilityCounter";

    public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var disks = Wmi.QueryRaw(
            "root\\Microsoft\\Windows\\Storage",
            PhysicalDiskQuery,
            out var error);

        if (disks.Count == 0)
        {
            return Task.FromResult(ComponentSnapshot.NotAvailable(
                Id,
                Name,
                Category,
                Id,
                error is null
                    ? "Windows Storage CIM did not return any physical disk."
                    : $"Windows Storage CIM did not return a disk: {error}"));
        }

        var systemDiskNumber = ReadSystemDiskNumber();
        var drives = new List<DriveData>();

        try
        {
            foreach (var disk in disks)
            {
                var deviceId = Wmi.Text(disk, "DeviceId");
                var diskNumber = int.TryParse(deviceId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : (int?)null;
                var busType = Wmi.UInt(disk, "BusType");
                var reliability = ReadReliability(disk, deviceId);
                var sata = busType is 3 or 11 && diskNumber is not null
                    ? SataSmartReader.TryRead(diskNumber.Value)
                    : null;

                drives.Add(new DriveData(
                    diskNumber,
                    Wmi.Text(disk, "FriendlyName"),
                    Wmi.Text(disk, "SerialNumber"),
                    Wmi.Text(disk, "FirmwareVersion"),
                    Wmi.ULong(disk, "Size"),
                    busType,
                    Wmi.UInt(disk, "MediaType"),
                    Wmi.UInt(disk, "HealthStatus"),
                    OperationalStatusText(Wmi.Value(disk, "OperationalStatus")),
                    reliability,
                    sata));
            }
        }
        finally
        {
            foreach (var disk in disks)
            {
                disk.Dispose();
            }
        }

        if (drives.Count == 0)
        {
            return Task.FromResult(ComponentSnapshot.NotAvailable(
                Id,
                Name,
                Category,
                Id,
                "Windows Storage CIM returned disks but none could be read."));
        }

        var primary = drives.FirstOrDefault(d => d.DiskNumber == systemDiskNumber) ?? drives[0];
        var ordered = new List<DriveData> { primary };
        ordered.AddRange(drives.Where(d => !ReferenceEquals(d, primary)));

        var specs = new List<SpecValue>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var drive = ordered[i];
            var prefix = i == 0 ? null : string.Create(CultureInfo.InvariantCulture, $"Drive {i + 1}");
            void Add(string label, string? value, string source, double? numericValue = null, string? numericUnit = null)
            {
                var fullLabel = prefix is null ? label : $"{prefix} - {label}";
                specs.Add(CollectorSupport.Spec(fullLabel, value, source, numericValue: numericValue, numericUnit: numericUnit));
            }

            var bus = CollectorSupport.BusType(drive.BusType);
            var media = CollectorSupport.MediaType(drive.MediaType);
            Add(SpecLabels.Model, drive.Model, "CIM MSFT_PhysicalDisk");
            Add(SpecLabels.SerialNumber, drive.SerialNumber, "CIM MSFT_PhysicalDisk");
            Add(SpecLabels.Firmware, drive.FirmwareVersion, "CIM MSFT_PhysicalDisk");
            Add(SpecLabels.Interface, string.Join(" ", new[] { bus, media }.Where(v => v != SpecLabels.NotReportedByHardware)), "CIM MSFT_PhysicalDisk");
            Add(SpecLabels.Capacity, CollectorSupport.FormatBytes(CollectorSupport.NonZeroNonMax(drive.Size)), "CIM MSFT_PhysicalDisk.Size", numericValue: (double?)CollectorSupport.NonZeroNonMax(drive.Size), numericUnit: "bytes");
            Add(SpecLabels.HealthWord, CollectorSupport.DiskHealth(drive.HealthStatus), "CIM MSFT_PhysicalDisk.HealthStatus");
            Add(SpecLabels.OperationalStatus, drive.OperationalStatus, "CIM MSFT_PhysicalDisk.OperationalStatus");
            Add(SpecLabels.WearLevel, FormatPercent(drive.Wear), drive.WearSource, numericValue: drive.Wear, numericUnit: "%");
            Add(SpecLabels.PowerOnHours, drive.PowerOnHours?.ToString(CultureInfo.InvariantCulture), drive.PowerOnHoursSource, numericValue: (double?)drive.PowerOnHours, numericUnit: "hours");
            Add(SpecLabels.PowerCycles, drive.PowerCycles?.ToString(CultureInfo.InvariantCulture), drive.PowerCyclesSource, numericValue: (double?)drive.PowerCycles, numericUnit: "cycles");
            Add(SpecLabels.Temperature, drive.Temperature is null
                ? null
                : string.Create(CultureInfo.InvariantCulture, $"{drive.Temperature.Value:0} C"), drive.TemperatureSource, numericValue: (double?)drive.Temperature, numericUnit: "C");
            Add(SpecLabels.ReadErrors, drive.ReadErrors?.ToString(CultureInfo.InvariantCulture), "Get-StorageReliabilityCounter", numericValue: (double?)drive.ReadErrors, numericUnit: "errors");
            Add(SpecLabels.WriteErrors, drive.WriteErrors?.ToString(CultureInfo.InvariantCulture), "Get-StorageReliabilityCounter", numericValue: (double?)drive.WriteErrors, numericUnit: "errors");
            Add(SpecLabels.UncorrectedErrors, drive.UncorrectedErrors?.ToString(CultureInfo.InvariantCulture), drive.UncorrectedSource, numericValue: (double?)drive.UncorrectedErrors, numericUnit: "errors");
            Add(SpecLabels.DataWritten, drive.DataWritten is null
                ? null
                : CollectorSupport.FormatBytes(drive.DataWritten), "SATA SMART attribute 241 (total LBAs written)", numericValue: (double?)drive.DataWritten, numericUnit: "bytes");
            Add(SpecLabels.DataRead, drive.DataRead is null
                ? null
                : CollectorSupport.FormatBytes(drive.DataRead), "SATA SMART attribute 242 (total LBAs read)", numericValue: (double?)drive.DataRead, numericUnit: "bytes");
            Add(SpecLabels.SMARTWarnings, drive.SmartWarnings, "SMART health, reallocated and pending sector counters");
            Add(SpecLabels.RatedEndurance, SpecLabels.NotReportedByHardware, "Drive endurance rating is not exposed by the drive");
            Add(SpecLabels.TRIM, SpecLabels.NotReportedByHardware, "Windows does not expose TRIM state through MSFT_PhysicalDisk");
        }

        var primaryWear = primary.Wear is null
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"{primary.Wear.Value:0.#}% wear");
        var headline = string.Join(" - ", new[] { primary.Model ?? "Storage", primaryWear }
            .Where(v => !string.IsNullOrWhiteSpace(v)));

        AddVolumes(specs);

        return Task.FromResult(ComponentSnapshot.NotMeasurable(
            Id,
            Name,
            Category,
            Id,
            string.IsNullOrWhiteSpace(headline) ? "Storage" : headline,
            specs,
            "SSD wear is the manufacturer's real endurance counter, not a failure date. A drive can keep working beyond 100% used. A truly new laptop shows 0% wear, no SMART warnings and close to zero power-on hours."));
    }

    /// <summary>
    /// Logical volumes (drive letters) from Win32_LogicalDisk: the logical
    /// view of the same storage the physical-disk specs above report, so
    /// they live on this page rather than in a separate component. A WMI
    /// failure returns no rows and is reported as no volumes, never as a
    /// failed collector: the physical-disk reading above is the headline.
    /// Labels never collide with "Wear level" / "Power-on hours", which the
    /// storage health rule matches by exact label.
    /// </summary>
    private static void AddVolumes(List<SpecValue> specs)
    {
        var rows = Wmi.Query(
            "root\\cimv2",
            LogicalDiskQuery);

        var volumes = rows
            .Select(row => StorageVolumeParser.Build(
                row.String("DeviceID"),
                row.UInt32("DriveType"),
                row.UInt64("Size"),
                row.UInt64("FreeSpace"),
                row.String("FileSystem")))
            .Where(volume => volume is not null)
            .Select(volume => volume!)
            // C: first, then the remaining letters in order.
            .OrderBy(volume => volume.DriveLetter.StartsWith("C", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(volume => volume.DriveLetter, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (volumes.Count == 0)
        {
            specs.Add(CollectorSupport.Spec(
                SpecLabels.Volumes,
                null,
                "CIM Win32_LogicalDisk (no local fixed disks reported)"));
            return;
        }

        foreach (var volume in volumes)
        {
            var letter = volume.DriveLetter;
            specs.Add(CollectorSupport.Spec(
                $"Volume {letter} - {SpecLabels.TotalSpace}",
                CollectorSupport.FormatBytes(volume.TotalBytes),
                "CIM Win32_LogicalDisk.Size",
                numericValue: (double?)volume.TotalBytes,
                numericUnit: "bytes"));
            specs.Add(CollectorSupport.Spec(
                $"Volume {letter} - {SpecLabels.FreeSpace}",
                CollectorSupport.FormatBytes(volume.FreeBytes),
                "CIM Win32_LogicalDisk.FreeSpace",
                numericValue: (double?)volume.FreeBytes,
                numericUnit: "bytes"));
            specs.Add(CollectorSupport.Spec(
                $"Volume {letter} - {SpecLabels.FreeSpacePercent}",
                volume.FreePercent is null
                    ? null
                    : string.Create(CultureInfo.InvariantCulture, $"{volume.FreePercent.Value:0.0}%"),
                "Free space / total space (Win32_LogicalDisk)",
                numericValue: volume.FreePercent,
                numericUnit: "%"));
            specs.Add(CollectorSupport.Spec(
                $"Volume {letter} - {SpecLabels.Filesystem}",
                volume.FileSystem,
                "CIM Win32_LogicalDisk.FileSystem"));
        }
    }

    private static string? FormatPercent(double? value) =>
        value is null ? null : string.Create(CultureInfo.InvariantCulture, $"{value.Value:0.#}%");

    /// <summary>
    /// MSFT_PhysicalDisk.OperationalStatus is an array of raw enum codes
    /// (2 = OK). Each code is decoded to its documented name, duplicates
    /// are collapsed, and an unmapped code is dropped: the result is never
    /// a bare number like "2".
    /// </summary>
    private static string OperationalStatusText(object? value)
    {
        var raw = value switch
        {
            null => Array.Empty<object?>(),
            Array array => array.Cast<object?>().ToArray(),
            _ => new[] { value },
        };

        var names = new List<string>();
        foreach (var item in raw)
        {
            uint? code = null;
            try
            {
                code = item is null ? null : Convert.ToUInt32(item, CultureInfo.InvariantCulture);
            }
            catch
            {
                code = null;
            }

            var name = CollectorSupport.OperationalStatus(code);
            if (name != SpecLabels.NotReportedByHardware && !names.Contains(name, StringComparer.Ordinal))
            {
                names.Add(name);
            }
        }

        return names.Count == 0 ? SpecLabels.NotReportedByHardware : string.Join(", ", names);
    }

    private static int? ReadSystemDiskNumber()
    {
        var partitions = Wmi.Query("root\\Microsoft\\Windows\\Storage", PartitionQuery);
        foreach (var partition in partitions)
        {
            var letter = partition.String("DriveLetter");
            var isSystem = string.Equals(letter, "C", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(letter, "C:", StringComparison.OrdinalIgnoreCase);
            if (isSystem)
            {
                return partition.Int32("DiskNumber");
            }
        }

        return null;
    }

    private static ReliabilityData? ReadReliability(ManagementObject disk, string? deviceId)
    {
        try
        {
            using var inParams = disk.GetMethodParameters("GetStorageReliabilityCounter");
            using var outParams = disk.InvokeMethod("GetStorageReliabilityCounter", inParams, null);
            if (outParams?["StorageReliabilityCounter"] is ManagementBaseObject counter)
            {
                using (counter)
                {
                    return FromInstance(counter);
                }
            }
        }
        catch
        {
            // Older storage stacks do not expose the method; fall through to the class query.
        }

        var rows = Wmi.Query("root\\Microsoft\\Windows\\Storage", StorageReliabilityCounterQuery);
        var match = rows.FirstOrDefault(r =>
            deviceId is not null &&
            string.Equals(r.String("DeviceId"), deviceId, StringComparison.OrdinalIgnoreCase));
        return match is null ? null : FromRow(match);
    }

    private static ReliabilityData FromInstance(ManagementBaseObject counter) => new(
        CollectorSupport.NonMax(Wmi.ULong(counter, "Wear")),
        CollectorSupport.NonMax(Wmi.ULong(counter, "PowerOnHours")),
        CollectorSupport.DriveTemperatureCelsius(Wmi.UInt(counter, "Temperature")),
        CollectorSupport.NonMax(Wmi.ULong(counter, "ReadErrorsTotal")),
        CollectorSupport.NonMax(Wmi.ULong(counter, "WriteErrorsTotal")),
        CombineUncorrected(
            CollectorSupport.NonMax(Wmi.ULong(counter, "ReadErrorsUncorrected")),
            CollectorSupport.NonMax(Wmi.ULong(counter, "WriteErrorsUncorrected"))),
        CollectorSupport.NonMax(Wmi.UInt(counter, "StartStopCycleCount")));

    private static ReliabilityData FromRow(WmiRow row) => new(
        CollectorSupport.NonMax(row.UInt64("Wear")),
        CollectorSupport.NonMax(row.UInt64("PowerOnHours")),
        CollectorSupport.DriveTemperatureCelsius(row.UInt32("Temperature")),
        CollectorSupport.NonMax(row.UInt64("ReadErrorsTotal")),
        CollectorSupport.NonMax(row.UInt64("WriteErrorsTotal")),
        CombineUncorrected(
            CollectorSupport.NonMax(row.UInt64("ReadErrorsUncorrected")),
            CollectorSupport.NonMax(row.UInt64("WriteErrorsUncorrected"))),
        CollectorSupport.NonMax(row.UInt32("StartStopCycleCount")));

    private static ulong? CombineUncorrected(ulong? read, ulong? write)
    {
        if (read is null && write is null)
        {
            return null;
        }

        return (read ?? 0UL) + (write ?? 0UL);
    }

    private sealed record ReliabilityData(
        ulong? Wear,
        ulong? PowerOnHours,
        uint? Temperature,
        ulong? ReadErrors,
        ulong? WriteErrors,
        ulong? UncorrectedErrors,
        uint? PowerCycles);

    private sealed record DriveData(
        int? DiskNumber,
        string? Model,
        string? SerialNumber,
        string? FirmwareVersion,
        ulong? Size,
        uint? BusType,
        uint? MediaType,
        uint? HealthStatus,
        string OperationalStatus,
        ReliabilityData? Reliability,
        SataSmartData? Sata)
    {
        public double? Wear => Reliability?.Wear ?? Sata?.WearPercent;

        public string WearSource => Reliability?.Wear is not null
            ? BusType == 17 ? "NVMe SMART Percentage Used / Get-StorageReliabilityCounter" : "Get-StorageReliabilityCounter.Wear"
            : Sata?.WearPercent is not null
                ? "SATA SMART attribute 231/233"
                : "NVMe/SATA SMART";

        public ulong? PowerOnHours => Reliability?.PowerOnHours ?? Sata?.PowerOnHours;

        public string PowerOnHoursSource => Reliability?.PowerOnHours is not null
            ? "Get-StorageReliabilityCounter.PowerOnHours"
            : Sata?.PowerOnHours is not null
                ? "SATA SMART attribute 9"
                : "SMART power-on hours";

        public uint? PowerCycles => Reliability?.PowerCycles ?? Sata?.PowerCycles;

        public string PowerCyclesSource => Reliability?.PowerCycles is not null
            ? "Get-StorageReliabilityCounter.StartStopCycleCount"
            : Sata?.PowerCycles is not null
                ? "SATA SMART attribute 12"
                : "SMART power cycle count";

        public double? Temperature => Reliability?.Temperature ?? Sata?.TemperatureC;

        public string TemperatureSource => Reliability?.Temperature is not null
            ? "Get-StorageReliabilityCounter.Temperature"
            : Sata?.TemperatureC is not null
                ? "SATA SMART attribute 194/190"
                : "SMART temperature";

        public ulong? ReadErrors => Reliability?.ReadErrors;

        public ulong? WriteErrors => Reliability?.WriteErrors;

        public ulong? UncorrectedErrors => Reliability?.UncorrectedErrors ?? Sata?.UncorrectedErrors;

        public string UncorrectedSource => Reliability?.UncorrectedErrors is not null
            ? "Get-StorageReliabilityCounter uncorrected errors"
            : Sata?.UncorrectedErrors is not null
                ? "SATA SMART attribute 198"
                : "SMART uncorrected errors";

        public ulong? DataWritten => Sata?.DataWrittenBytes;

        public ulong? DataRead => Sata?.DataReadBytes;

        public string SmartWarnings
        {
            get
            {
                var health = CollectorSupport.DiskHealth(HealthStatus);
                var hasData = Wear is not null ||
                              HealthStatus is not null ||
                              UncorrectedErrors is not null ||
                              Sata?.ReallocatedSectors is not null ||
                              Sata?.PendingSectors is not null;
                if (!hasData)
                {
                    return SpecLabels.NotReportedByHardware;
                }

                var warning = string.Equals(health, "Warning", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(health, "Unhealthy", StringComparison.OrdinalIgnoreCase) ||
                              UncorrectedErrors is > 0 ||
                              Sata?.ReallocatedSectors is > 0 ||
                              Sata?.PendingSectors is > 0;

                return warning ? "Yes" : "No";
            }
        }
    }
}
