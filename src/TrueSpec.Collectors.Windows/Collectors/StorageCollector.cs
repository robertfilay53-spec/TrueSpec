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

    public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var disks = Wmi.QueryRaw(
            "root\\Microsoft\\Windows\\Storage",
            "SELECT * FROM MSFT_PhysicalDisk",
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
                    Join(Wmi.Value(disk, "OperationalStatus")),
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
            void Add(string label, string? value, string source)
            {
                var fullLabel = prefix is null ? label : $"{prefix} - {label}";
                specs.Add(CollectorSupport.Spec(fullLabel, value, source));
            }

            var bus = CollectorSupport.BusType(drive.BusType);
            var media = CollectorSupport.MediaType(drive.MediaType);
            Add("Model", drive.Model, "CIM MSFT_PhysicalDisk");
            Add("Serial number", drive.SerialNumber, "CIM MSFT_PhysicalDisk");
            Add("Firmware", drive.FirmwareVersion, "CIM MSFT_PhysicalDisk");
            Add("Interface", string.Join(" ", new[] { bus, media }.Where(v => v != CollectorSupport.NotReported)), "CIM MSFT_PhysicalDisk");
            Add("Capacity", CollectorSupport.FormatBytes(drive.Size), "CIM MSFT_PhysicalDisk.Size");
            Add("Health word", CollectorSupport.DiskHealth(drive.HealthStatus), "CIM MSFT_PhysicalDisk.HealthStatus");
            Add("Operational status", Join(drive.OperationalStatus), "CIM MSFT_PhysicalDisk.OperationalStatus");
            Add("Wear level", FormatPercent(drive.Wear), drive.WearSource);
            Add("Power-on hours", drive.PowerOnHours?.ToString(CultureInfo.InvariantCulture), drive.PowerOnHoursSource);
            Add("Power cycles", drive.PowerCycles?.ToString(CultureInfo.InvariantCulture), drive.PowerCyclesSource);
            Add("Temperature", drive.Temperature is null
                ? null
                : string.Create(CultureInfo.InvariantCulture, $"{drive.Temperature.Value:0} C"), drive.TemperatureSource);
            Add("Read errors", drive.ReadErrors?.ToString(CultureInfo.InvariantCulture), "Get-StorageReliabilityCounter");
            Add("Write errors", drive.WriteErrors?.ToString(CultureInfo.InvariantCulture), "Get-StorageReliabilityCounter");
            Add("Uncorrected errors", drive.UncorrectedErrors?.ToString(CultureInfo.InvariantCulture), drive.UncorrectedSource);
            Add("Data written", drive.DataWritten is null
                ? null
                : CollectorSupport.FormatBytes(drive.DataWritten), "SATA SMART attribute 241 (total LBAs written)");
            Add("Data read", drive.DataRead is null
                ? null
                : CollectorSupport.FormatBytes(drive.DataRead), "SATA SMART attribute 242 (total LBAs read)");
            Add("SMART warnings", drive.SmartWarnings, "SMART health, reallocated and pending sector counters");
            Add("Rated endurance", "Not reported by hardware", "Drive endurance rating is not exposed by the drive");
            Add("TRIM", "Not reported by hardware", "Windows does not expose TRIM state through MSFT_PhysicalDisk");
        }

        var primaryWear = primary.Wear is null
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"{primary.Wear.Value:0.#}% wear");
        var headline = string.Join(" - ", new[] { primary.Model ?? "Storage", primaryWear }
            .Where(v => !string.IsNullOrWhiteSpace(v)));

        return Task.FromResult(ComponentSnapshot.NotMeasurable(
            Id,
            Name,
            Category,
            Id,
            string.IsNullOrWhiteSpace(headline) ? "Storage" : headline,
            specs,
            "SSD wear is the manufacturer's real endurance counter, not a failure date. A drive can keep working beyond 100% used. A truly new laptop shows 0% wear, no SMART warnings and close to zero power-on hours."));
    }

    private static string? FormatPercent(double? value) =>
        value is null ? null : string.Create(CultureInfo.InvariantCulture, $"{value.Value:0.#}%");

    private static string Join(object? value)
    {
        if (value is Array array)
        {
            var parts = array.Cast<object?>()
                .Select(v => Convert.ToString(v, CultureInfo.InvariantCulture))
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!)
                .ToArray();
            return parts.Length == 0 ? CollectorSupport.NotReported : string.Join(", ", parts);
        }

        var text = value is null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(text) ? CollectorSupport.NotReported : text;
    }

    private static int? ReadSystemDiskNumber()
    {
        var partitions = Wmi.Query("root\\Microsoft\\Windows\\Storage", "SELECT * FROM MSFT_Partition");
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

        var rows = Wmi.Query("root\\Microsoft\\Windows\\Storage", "SELECT * FROM MSFT_StorageReliabilityCounter");
        var match = rows.FirstOrDefault(r =>
            deviceId is not null &&
            string.Equals(r.String("DeviceId"), deviceId, StringComparison.OrdinalIgnoreCase));
        return match is null ? null : FromRow(match);
    }

    private static ReliabilityData FromInstance(ManagementBaseObject counter) => new(
        Wmi.ULong(counter, "Wear"),
        Wmi.ULong(counter, "PowerOnHours"),
        Wmi.UInt(counter, "Temperature"),
        Wmi.ULong(counter, "ReadErrorsTotal"),
        Wmi.ULong(counter, "WriteErrorsTotal"),
        CombineUncorrected(
            Wmi.ULong(counter, "ReadErrorsUncorrected"),
            Wmi.ULong(counter, "WriteErrorsUncorrected")),
        Wmi.UInt(counter, "StartStopCycleCount"));

    private static ReliabilityData FromRow(WmiRow row) => new(
        row.UInt64("Wear"),
        row.UInt64("PowerOnHours"),
        row.UInt32("Temperature"),
        row.UInt64("ReadErrorsTotal"),
        row.UInt64("WriteErrorsTotal"),
        CombineUncorrected(row.UInt64("ReadErrorsUncorrected"), row.UInt64("WriteErrorsUncorrected")),
        row.UInt32("StartStopCycleCount"));

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
                    return CollectorSupport.NotReported;
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
