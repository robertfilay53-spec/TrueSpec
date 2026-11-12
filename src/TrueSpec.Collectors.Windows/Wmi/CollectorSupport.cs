namespace TrueSpec.Collectors.Windows.Wmi;

using System.Globalization;
using TrueSpec.Core.Models;

internal static class CollectorSupport
{
    public const string NotReported = "Not reported by hardware";

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

    public static SpecValue Spec(string label, string? value, string source, bool estimate = false) => new(
        label,
        string.IsNullOrWhiteSpace(value) ? NotReported : TruncateValue(value.Trim()),
        source,
        estimate);

    public static string FormatBytes(ulong? bytes)
    {
        if (bytes is null)
        {
            return NotReported;
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
            return NotReported;
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

    public static string FormatMegaBitsPerSecond(ulong? bitsPerSecond)
    {
        if (bitsPerSecond is null)
        {
            return NotReported;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{bitsPerSecond.Value / 1_000_000d:0.#} Mbps");
    }

    public static string FormatPercent(double? value) =>
        value is null ? NotReported : string.Create(CultureInfo.InvariantCulture, $"{value.Value:0.#}%");

    public static string FormatNumber(double? value, string unit, int decimals = 0) =>
        value is null
            ? NotReported
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
        _ => NotReported,
    };

    public static string MediaType(uint? mediaType) => mediaType switch
    {
        3 => "HDD",
        4 => "SSD",
        5 => "SCM",
        _ => NotReported,
    };

    public static string DiskHealth(uint? health) => health switch
    {
        0 => "Healthy",
        1 => "Warning",
        2 => "Unhealthy",
        5 => "Unknown",
        _ => NotReported,
    };

    public static string BatteryChemistry(uint? chemistry) => chemistry switch
    {
        1 => "Other",
        2 => "Unknown",
        3 => "Lead acid",
        4 => "Nickel cadmium",
        5 => "Nickel metal hydride",
        6 => "Lithium ion",
        7 => "Lithium polymer",
        8 => "Zinc air",
        9 => "Lithium ion",
        _ => NotReported,
    };

    public static string BatteryChemistry(string? chemistry)
    {
        if (string.IsNullOrWhiteSpace(chemistry))
        {
            return NotReported;
        }

        return chemistry.Trim();
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
        _ => NotReported,
    };

    public static string FormFactor(uint? formFactor) => formFactor switch
    {
        1 => "Other",
        2 => "Unknown",
        3 => "SIMM",
        4 => "SIP",
        5 => "Chip",
        6 => "DIP",
        7 => "ZIP",
        8 => "Proprietary card",
        9 => "DIMM",
        10 => "TSOP",
        11 => "Row of chips",
        12 => "RIMM",
        13 => "SODIMM",
        14 => "SRIMM",
        15 => "FB-DIMM",
        _ => NotReported,
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
        _ => NotReported,
    };

    public static string MemoryType(uint? type) => MemoryType(type is null ? null : (ushort)Math.Min(type.Value, ushort.MaxValue));
}
