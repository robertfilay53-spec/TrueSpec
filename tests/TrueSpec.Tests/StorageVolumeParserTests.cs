using TrueSpec.Core.Collectors;

namespace TrueSpec.Tests;

public class StorageVolumeParserTests
{
    [Theory]
    [InlineData(3u, true)]
    [InlineData(0u, false)]
    [InlineData(1u, false)]
    [InlineData(2u, false)]
    [InlineData(4u, false)]
    [InlineData(5u, false)]
    [InlineData(6u, false)]
    [InlineData(null, false)]
    public void IsLocalDisk_OnlyDriveType3(uint? driveType, bool expected) =>
        Assert.Equal(expected, StorageVolumeParser.IsLocalDisk(driveType));

    [Fact]
    public void FreePercent_HalfFreeIsFifty()
    {
        Assert.Equal(50d, StorageVolumeParser.FreePercent(1_000_000_000UL, 500_000_000UL));
    }

    [Fact]
    public void FreePercent_MissingOrZeroTotalIsNullNeverZero()
    {
        Assert.Null(StorageVolumeParser.FreePercent(null, 500UL));
        Assert.Null(StorageVolumeParser.FreePercent(0UL, 0UL));
        Assert.Null(StorageVolumeParser.FreePercent(1_000UL, null));
    }

    [Fact]
    public void FreePercent_FreeAboveTotalIsImpossibleDataNotOneHundred()
    {
        Assert.Null(StorageVolumeParser.FreePercent(1_000UL, 2_000UL));
    }

    [Fact]
    public void FreePercent_FullDriveIsZero()
    {
        Assert.Equal(0d, StorageVolumeParser.FreePercent(1_000UL, 0UL));
    }

    [Fact]
    public void Build_LocalDiskKeepsMeasuredValues()
    {
        var volume = StorageVolumeParser.Build("C:", 3u, 1_000_000_000_000UL, 250_000_000_000UL, "NTFS");

        Assert.NotNull(volume);
        Assert.Equal("C:", volume.DriveLetter);
        Assert.Equal(1_000_000_000_000UL, volume.TotalBytes);
        Assert.Equal(250_000_000_000UL, volume.FreeBytes);
        Assert.Equal("NTFS", volume.FileSystem);
        Assert.Equal(25d, volume.FreePercent);
    }

    [Fact]
    public void Build_NonLocalDiskIsNull()
    {
        // Network drive, CD-ROM and RAM disk are not buyer storage.
        Assert.Null(StorageVolumeParser.Build("Z:", 4u, 1_000UL, 500UL, "NTFS"));
        Assert.Null(StorageVolumeParser.Build("D:", 5u, 1_000UL, 500UL, "CDFS"));
        Assert.Null(StorageVolumeParser.Build("R:", 6u, 1_000UL, 500UL, "FAT32"));
    }

    [Fact]
    public void Build_BlankLetterIsNull()
    {
        Assert.Null(StorageVolumeParser.Build(null, 3u, 1_000UL, 500UL, "NTFS"));
        Assert.Null(StorageVolumeParser.Build("  ", 3u, 1_000UL, 500UL, "NTFS"));
    }

    [Fact]
    public void Build_SingleLetterGetsAColon()
    {
        var volume = StorageVolumeParser.Build("C", 3u, 1_000UL, 500UL, "NTFS");

        Assert.NotNull(volume);
        Assert.Equal("C:", volume.DriveLetter);
    }

    [Fact]
    public void Build_ZeroSizeIsNotReportedNeverZeroBytes()
    {
        var volume = StorageVolumeParser.Build("C:", 3u, 0UL, 0UL, null);

        Assert.NotNull(volume);
        Assert.Null(volume.TotalBytes);
        Assert.Null(volume.FreeBytes);
        Assert.Null(volume.FileSystem);
        Assert.Null(volume.FreePercent);
    }
}
