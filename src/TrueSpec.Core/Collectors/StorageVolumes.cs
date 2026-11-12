namespace TrueSpec.Core.Collectors;

/// <summary>
/// One local fixed-disk volume (a drive letter) from Win32_LogicalDisk.
/// Total and free are the measured byte counts; the free percentage is
/// derived from those two measured numbers, never guessed. A volume whose
/// size Windows does not report keeps nulls and shows as "Not reported by
/// hardware" instead of a zero.
/// </summary>
public sealed record StorageVolume(
    string DriveLetter,
    ulong? TotalBytes,
    ulong? FreeBytes,
    string? FileSystem)
{
    /// <summary>
    /// Free space as a percentage of total, from the two measured byte counts.
    /// </summary>
    public double? FreePercent => StorageVolumeParser.FreePercent(TotalBytes, FreeBytes);
}

/// <summary>
/// Pure parsing for storage volumes, kept in Core so it is testable without
/// WMI. The WMI read itself lives in the Windows Storage collector; this
/// class only interprets what was read.
/// </summary>
public static class StorageVolumeParser
{
    /// <summary>
    /// Win32_LogicalDisk.DriveType for a local fixed disk. Other types are
    /// not buyer storage: 2 removable, 4 network, 5 CD-ROM, 6 RAM disk.
    /// </summary>
    public const uint LocalDiskDriveType = 3;

    public static bool IsLocalDisk(uint? driveType) => driveType == LocalDiskDriveType;

    /// <summary>
    /// Free space as a percentage of total. Returns null when either number
    /// is missing or zero, and when free exceeds total: that is impossible
    /// sensor data, not a full drive.
    /// </summary>
    public static double? FreePercent(ulong? totalBytes, ulong? freeBytes)
    {
        if (totalBytes is null or 0 || freeBytes is null)
        {
            return null;
        }

        if (freeBytes.Value > totalBytes.Value)
        {
            return null;
        }

        return Math.Clamp(freeBytes.Value / (double)totalBytes.Value * 100d, 0d, 100d);
    }

    /// <summary>
    /// Builds one volume from a Win32_LogicalDisk row. Returns null for
    /// anything that is not a local fixed disk, or for a row with no drive
    /// letter: a letter is the identity of a volume. A local disk whose size
    /// Windows does not report still builds, with nulls that render as
    /// SpecLabels.NotReportedByHardware.
    /// </summary>
    public static StorageVolume? Build(
        string? deviceId,
        uint? driveType,
        ulong? size,
        ulong? freeSpace,
        string? fileSystem)
    {
        if (!IsLocalDisk(driveType))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        var letter = deviceId.Trim();
        if (letter.Length == 1)
        {
            letter += ":";
        }

        return new StorageVolume(
            letter,
            NonZero(size),
            NonZero(freeSpace),
            string.IsNullOrWhiteSpace(fileSystem) ? null : fileSystem.Trim());
    }

    // WMI reports 0 for "unknown" on numeric fields. A 0 size is not a
    // measurement: map it to null so the value shows as "Not reported by
    // hardware" instead of "0 bytes".
    private static ulong? NonZero(ulong? value) => value is > 0 ? value : null;
}
