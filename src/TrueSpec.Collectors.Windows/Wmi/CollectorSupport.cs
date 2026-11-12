namespace TrueSpec.Collectors.Windows.Wmi;

using System.Globalization;
using TrueSpec.Core.Models;

internal static class CollectorSupport
{
    // A WMI string can be arbitrarily long (a hostile or corrupt driver
    // description, a pasted blob). Values are capped at the boundary so a
    // single runaway string cannot bloat the snapshot, the history database
    // or the report. Truncation is visible, never silent: the value ends
    // with "..." so the reader knows more was there.
    public const int MaxSpecValueLength = 512;

    public static string TruncateValue(string value) =>
        value.Length <= MaxSpecValueLength
            ? value
            : value[..(MaxSpecValueLength - 3)] + "...";

    /// <summary>
    /// One spec row. <paramref name="numericValue"/> is the typed measurement
    /// behind the display string (with <paramref name="numericUnit"/> naming
    /// its unit), when the value is a number: analyzers read it through
    /// <see cref="TrueSpec.Core.Health.SpecText.Number"/> instead of
    /// re-parsing the display text. A value the hardware did not report
    /// never carries a typed number, even if the caller passes one.
    /// </summary>
    public static SpecValue Spec(string label, string? value, string source, bool estimate = false, double? numericValue = null, string? numericUnit = null)
    {
        var text = string.IsNullOrWhiteSpace(value)
            ? SpecLabels.NotReportedByHardware
            : TruncateValue(value.Trim());
        if (string.Equals(text, SpecLabels.NotReportedByHardware, StringComparison.Ordinal))
        {
            numericValue = null;
            numericUnit = null;
        }

        return new(label, text, source, estimate, numericValue, numericUnit);
    }

    public static string FormatBytes(ulong? bytes)
    {
        if (bytes is null)
        {
            return SpecLabels.NotReportedByHardware;
        }

        var value = bytes.Value;
        if (value >= 1_000_000_000_000UL)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{value / 1_000_000_000_000d:0.00} TB");
        }

        if (value >= 1_000_000_000UL)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{value / 1_000_000_000d:0.0} GB");
        }

        if (value >= 1_000_000UL)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{value / 1_000_000d:0.0} MB");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{value} bytes");
    }

    /// <summary>
    /// Binary (1024-based) sizes for RAM and video memory: those are sold
    /// and reported in GiB, so a 16 GiB stick shows "16.0 GB", not "17.2 GB".
    /// Disk sizes stay decimal (see <see cref="FormatBytes"/>) because drives
    /// are sold in decimal GB.
    /// </summary>
    public static string FormatBytesBinary(ulong? bytes)
    {
        if (bytes is null)
        {
            return SpecLabels.NotReportedByHardware;
        }

        var value = bytes.Value;
        if (value >= 1UL << 40)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{value / 1_099_511_627_776d:0.00} TB");
        }

        if (value >= 1UL << 30)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{value / 1_073_741_824d:0.0} GB");
        }

        if (value >= 1UL << 20)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{value / 1_048_576d:0.0} MB");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{value} bytes");
    }

    /// <summary>
    /// WMI reports 0 for "unknown" on many numeric fields (clock speed,
    /// cache size, drive size, link speed). A 0 in those places is not a
    /// measurement: map it to null so the value shows as "Not reported
    /// by hardware" instead of "0 MHz" / "0 bytes" / "0 Mbps".
    /// </summary>
    public static uint? NonZero(uint? value) => value is > 0 ? value : null;

    public static ulong? NonZero(ulong? value) => value is > 0 ? value : null;

    /// <summary>
    /// Unsigned WMI fields also use all-bits-set (0xFFFFFFFF /
    /// 0xFFFFFFFFFFFFFFFF) for "no reading". That value is not a
    /// measurement either: map it to null so it shows as "Not reported
    /// by hardware" instead of "4294967295 mWh" / "4294967295 MHz".
    /// </summary>
    public static uint? NonMax(uint? value) => value is uint.MaxValue ? null : value;

    public static ulong? NonMax(ulong? value) => value is ulong.MaxValue ? null : value;

    /// <summary>0 or all-bits-set: neither is a measurement.</summary>
    public static uint? NonZeroNonMax(uint? value) => value is > 0 and < uint.MaxValue ? value : null;

    public static ulong? NonZeroNonMax(ulong? value) => value is > 0 and < ulong.MaxValue ? value : null;

    /// <summary>
    /// Win32_Battery capacities: 0 and 0xFFFFFFFF both mean the firmware
    /// did not report a capacity.
    /// </summary>
    public static uint? BatteryCapacity(uint? value) => NonZeroNonMax(value);

    /// <summary>
    /// Win32_Battery.EstimatedChargeRemaining is a percentage (0-100).
    /// Anything above 100, including the 0xFFFF "unknown" marker, is not
    /// a reading.
    /// </summary>
    public static ushort? BatteryChargePercent(ushort? value) => value is <= 100 ? value : null;

    /// <summary>
    /// Win32_Battery.EstimatedRunTime: 0xFFFFFFFF means unknown, and
    /// 0x04444444 (71582788) is the value firmware reports when the
    /// runtime is unknown.
    /// </summary>
    public static uint? BatteryRuntimeMinutes(uint? value) => value switch
    {
        null or uint.MaxValue or 71582788 => null,
        _ => value,
    };

    /// <summary>
    /// BatteryCycleCount: 0 is a real count (a new battery); 0xFFFFFFFF
    /// is the "no reading" marker.
    /// </summary>
    public static uint? BatteryCycleCount(uint? value) => NonMax(value);

    /// <summary>
    /// A drive temperature in Celsius from the reliability counter: 0
    /// means the drive exposes no sensor, 0xFFFFFFFF is the "no reading"
    /// marker, and anything above 150 C is impossible sensor data.
    /// </summary>
    public static uint? DriveTemperatureCelsius(uint? value) => value is > 0 and <= 150 ? value : null;

    public static string FormatMegaBitsPerSecond(ulong? bitsPerSecond)
    {
        if (bitsPerSecond is null)
        {
            return SpecLabels.NotReportedByHardware;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{bitsPerSecond.Value / 1_000_000d:0.#} Mbps");
    }

    /// <summary>
    /// The one driver-age rule in the whole app (U12): a driver older
    /// than this many years is flagged as stale, in its age value on the
    /// Wi-Fi, Ethernet and GPU pages and in the Drivers page count
    /// ("Drivers older than 5 years"). One rule, one number: the pages
    /// used to disagree (2 years on the component pages, 5 on the Drivers
    /// page), so a 4-year-old driver was flagged stale in one place and
    /// not counted as old in another. 5 years is the line because a
    /// 2-year-old driver is normal: vendors ship stable drivers for
    /// years, and only a driver older than 5 years is very old.
    /// </summary>
    public const int DriverStaleAgeYears = 5;

    /// <summary>
    /// The one driver-signature query in the app: SystemCollector and
    /// SecurityCollector both enumerate Win32_PnPSignedDriver for its
    /// IsSigned column only (DriversCollector has its own wider query:
    /// it also reads DeviceName, DriverDate and DriverVersion, and
    /// already handles truncation since PR #12). Only IsSigned is ever
    /// read from these rows: DeviceName was fetched and thrown away.
    /// Win32_PnPSignedDriver has hundreds of rows and every returned
    /// property is materialised per row, so asking for one column is
    /// measurably cheaper.
    /// </summary>
    internal const string DriverSignatureQuery = "SELECT IsSigned FROM Win32_PnPSignedDriver";

    /// <summary>
    /// What the driver-signature enumeration actually knows. The counts
    /// are over the rows WMI returned; <see cref="Truncated"/> says
    /// whether that was the whole list. The counts are all null together
    /// when the class returned no rows at all (not "zero drivers": the
    /// data was not reported), and <see cref="Truncated"/> is false then:
    /// the row cap can only trigger at <see cref="Wmi.MaxRows"/> rows.
    /// </summary>
    public sealed record DriverSignatureSummary(int? Total, int? Unsigned, int? Unknown, bool Truncated);

    /// <summary>
    /// Pure summary of driver-signature rows, so the truncation handling
    /// is unit-testable without live WMI (same pattern as
    /// DriversCollector.BuildSnapshot, PR #12). When the WMI row cap
    /// truncated the list (<paramref name="queryError"/> is
    /// <see cref="Wmi.TruncatedError"/>), the counts cover the first
    /// <see cref="Wmi.MaxRows"/> drivers only: callers must qualify them
    /// with <see cref="DriverListTruncationNote"/> and must never derive
    /// an "all signed" claim from a partial list.
    /// </summary>
    internal static DriverSignatureSummary SummarizeDriverSignatures(IReadOnlyList<WmiRow> rows, string? queryError)
    {
        var truncated = string.Equals(queryError, Wmi.TruncatedError, StringComparison.Ordinal);
        if (rows.Count == 0)
        {
            return new(null, null, null, false);
        }

        return new(
            rows.Count,
            rows.Count(r => r.Bool("IsSigned") is false),
            rows.Count(r => r.Bool("IsSigned") is null),
            truncated);
    }

    /// <summary>
    /// Live driver-signature enumeration shared by SystemCollector and
    /// SecurityCollector: the 3-arg Query keeps the error, so a list
    /// truncated at the WMI row cap is reported, never silently counted.
    /// </summary>
    internal static DriverSignatureSummary ReadDriverSignatureSummary()
    {
        var rows = Wmi.Query("root\\cimv2", DriverSignatureQuery, out var queryError);
        return SummarizeDriverSignatures(rows, queryError);
    }

    /// <summary>
    /// The truncation qualifier for a driver count, in the same words
    /// DriversCollector uses (PR #12): a plain literal label, no new
    /// SpecLabels constant, so the three collectors say the same thing.
    /// DriversCollector keeps its own identical inline copy because
    /// that file is owned by PR #12.
    /// </summary>
    internal static SpecValue DriverListTruncationNote() => Spec(
        "Driver list truncated",
        "Truncated at 1000 rows; counts cover the first 1000 drivers only.",
        "CIM Win32_PnPSignedDriver");

    /// <summary>
    /// Plain-text age of a driver from its driver date ("yyyy-MM-dd" as
    /// written by WmiRow.DateText), for example "Driver from 2024
    /// (2 years old)". A driver older than <see cref="DriverStaleAgeYears"/>
    /// years is flagged in the same value. A missing, unparseable or future
    /// date never produces a negative age: a future date reads as 0 years
    /// old. Shared by the Wi-Fi, Ethernet and GPU collectors so the same
    /// rule (and the same wording) applies to every driver.
    /// </summary>
    public static string FormatDriverAge(string? driverDate, DateTimeOffset asOf)
    {
        if (string.IsNullOrWhiteSpace(driverDate) ||
            !DateTimeOffset.TryParse(
                driverDate,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var date))
        {
            return SpecLabels.NotReportedByHardware;
        }

        var years = asOf.Year - date.Year;
        if (asOf.Month < date.Month || (asOf.Month == date.Month && asOf.Day < date.Day))
        {
            years--;
        }

        if (years < 0)
        {
            years = 0;
        }

        // The stale flag sits inside the same parentheses as the age, so
        // the value reads as one sentence: "Driver from 2020 (6 years old,
        // older than 5 years)".
        var stale = years > DriverStaleAgeYears
            ? string.Create(CultureInfo.InvariantCulture, $", older than {DriverStaleAgeYears} years")
            : string.Empty;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Driver from {date.Year} ({years} year{(years == 1 ? string.Empty : "s")} old{stale})");
    }

    public static string FormatPercent(double? value) =>
        value is null ? SpecLabels.NotReportedByHardware : string.Create(CultureInfo.InvariantCulture, $"{value.Value:0.#}%");

    public static string FormatNumber(double? value, string unit, int decimals = 0) =>
        value is null
            ? SpecLabels.NotReportedByHardware
            : string.Create(CultureInfo.InvariantCulture, $"{value.Value.ToString($"F{decimals}", CultureInfo.InvariantCulture)} {unit}");

    public static string BusType(uint? busType) => busType switch
    {
        1 => "SCSI",
        2 => "ATAPI",
        3 => "ATA",
        4 => "IEEE 1394",
        5 => "SSA",
        6 => "Fibre Channel",
        7 => "USB",
        8 => "RAID",
        9 => "iSCSI",
        10 => "SAS",
        11 => "SATA",
        12 => "SD",
        13 => "MMC",
        14 => "Virtual",
        15 => "File-backed virtual",
        16 => "Storage Spaces",
        17 => "NVMe",
        18 => "SCM",
        19 => "UFS",
        _ => SpecLabels.NotReportedByHardware,
    };

    public static string MediaType(uint? mediaType) => mediaType switch
    {
        3 => "HDD",
        4 => "SSD",
        5 => "SCM",
        _ => SpecLabels.NotReportedByHardware,
    };

    public static string DiskHealth(uint? health) => health switch
    {
        0 => "Healthy",
        1 => "Warning",
        2 => "Unhealthy",
        5 => "Unknown",
        _ => SpecLabels.NotReportedByHardware,
    };

    /// <summary>
    /// MSFT_PhysicalDisk.OperationalStatus, the documented CIM codes
    /// (2 = OK). Unknown codes show as SpecLabels.NotReportedByHardware, never
    /// as a bare number.
    /// </summary>
    public static string OperationalStatus(uint? status) => status switch
    {
        0 => "Unknown",
        1 => "Other",
        2 => "OK",
        3 => "Degraded",
        4 => "Stressed",
        5 => "Predictive Failure",
        6 => "Error",
        7 => "Non-Recoverable Error",
        8 => "Starting",
        9 => "Stopping",
        10 => "Stopped",
        11 => "In Service",
        12 => "No Contact",
        13 => "Lost Communication",
        14 => "Aborted",
        15 => "Dormant",
        16 => "Supporting Entity in Error",
        17 => "Completed",
        18 => "Power Mode",
        _ => SpecLabels.NotReportedByHardware,
    };

    /// <summary>
    /// Win32_Battery Chemistry codes. Note 7 is Zinc air and 8 is Lithium
    /// polymer: an earlier table had them swapped and carried a bogus 9,
    /// which is not a documented code.
    /// </summary>
    public static string BatteryChemistry(uint? chemistry) => chemistry switch
    {
        1 => "Other",
        2 => "Unknown",
        3 => "Lead acid",
        4 => "Nickel cadmium",
        5 => "Nickel metal hydride",
        6 => "Lithium ion",
        7 => "Zinc air",
        8 => "Lithium polymer",
        _ => SpecLabels.NotReportedByHardware,
    };

    /// <summary>
    /// BatteryStaticData reports Chemistry as a number, so a numeric
    /// string here is a raw enum code, not a name: decode it, never print
    /// the number (a ThinkPad reported "5269836", an unmapped vendor
    /// value). A real name ("Li-ion", "NiMH") is normalised to the
    /// canonical name; an unrecognised name is shown as reported.
    /// </summary>
    public static string BatteryChemistry(string? chemistry)
    {
        if (string.IsNullOrWhiteSpace(chemistry))
        {
            return SpecLabels.NotReportedByHardware;
        }

        var text = chemistry.Trim();
        if (uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
        {
            return BatteryChemistry(code);
        }

        var compact = text
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
        return compact switch
        {
            "other" => "Other",
            "unknown" => "Unknown",
            "leadacid" => "Lead acid",
            "nickelcadmium" or "nicd" => "Nickel cadmium",
            "nickelmetalhydride" or "nimh" => "Nickel metal hydride",
            "lithiumion" or "liion" => "Lithium ion",
            "zincair" => "Zinc air",
            "lithiumpolymer" or "lipolymer" or "lipoly" => "Lithium polymer",
            _ => text,
        };
    }

    public static string ChassisType(ushort? chassis) => chassis switch
    {
        3 => "Desktop",
        4 => "Low profile desktop",
        5 => "Pizza box",
        6 => "Mini tower",
        7 => "Tower",
        8 => "Portable",
        9 => "Laptop",
        10 => "Notebook",
        11 => "Handheld",
        12 => "Docking station",
        13 => "All in one",
        14 => "Sub notebook",
        15 => "Space-saving",
        16 => "Lunch box",
        17 => "Main system chassis",
        18 => "Expansion chassis",
        19 => "Sub chassis",
        20 => "Bus expansion chassis",
        21 => "Peripheral chassis",
        22 => "Storage chassis",
        23 => "Sealed-case PC",
        30 => "Tablet",
        31 => "Convertible",
        32 => "Detachable",
        _ => SpecLabels.NotReportedByHardware,
    };

    /// <summary>
    /// Win32_PhysicalMemory FormFactor, the SMBIOS memory form-factor
    /// codes. An earlier table was off by one (12 read "RIMM", 13 read
    /// "SODIMM"); the correct codes are 11 = RIMM and 12 = SODIMM, which
    /// is what a ThinkPad T480 reports.
    /// </summary>
    public static string FormFactor(uint? formFactor) => formFactor switch
    {
        0 => "Unknown",
        1 => "Other",
        2 => "SIP",
        3 => "DIP",
        4 => "ZIP",
        5 => "SOJ",
        6 => "Proprietary",
        7 => "SIMM",
        8 => "DIMM",
        9 => "TSOP",
        10 => "PGA",
        11 => "RIMM",
        12 => "SODIMM",
        13 => "SRIMM",
        14 => "SMD",
        15 => "SSMP",
        16 => "QFP",
        17 => "TQFP",
        18 => "SOIC",
        19 => "LCC",
        20 => "PLCC",
        21 => "BGA",
        22 => "FPBGA",
        23 => "LGA",
        _ => SpecLabels.NotReportedByHardware,
    };

    public static string MemoryType(ushort? type) => type switch
    {
        20 => "DDR",
        21 => "DDR2",
        22 => "DDR2 FB-DIMM",
        24 => "DDR3",
        26 => "DDR4",
        27 => "LPDDR",
        28 => "LPDDR2",
        29 => "LPDDR3",
        30 => "LPDDR4",
        34 => "DDR5",
        35 => "LPDDR5",
        _ => SpecLabels.NotReportedByHardware,
    };

    public static string MemoryType(uint? type) => MemoryType(type is null ? null : (ushort)Math.Min(type.Value, ushort.MaxValue));
}
