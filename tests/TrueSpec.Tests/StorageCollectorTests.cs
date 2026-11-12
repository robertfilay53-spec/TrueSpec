using TrueSpec.Collectors.Windows.Collectors;
using TrueSpec.Collectors.Windows.Storage;

namespace TrueSpec.Tests;

/// <summary>
/// Windows-leg only: the collector reads the real storage CIM classes. On
/// Linux this file is excluded from the build (see TrueSpec.Tests.csproj),
/// like StartupCollectorTests. No sizes are asserted: they depend on the
/// machine the test runs on. The CI Windows runner has a C: drive.
/// </summary>
public class StorageCollectorTests
{
    [Fact]
    public async Task StorageCollector_ListsLogicalVolumes()
    {
        var collector = new StorageCollector();

        Assert.Equal("storage", collector.Id);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var collectTask = collector.CollectAsync(timeout.Token);
        var finished = await Task.WhenAny(collectTask, Task.Delay(TimeSpan.FromSeconds(60)));

        Assert.True(
            finished == collectTask,
            "StorageCollector did not finish within 60 seconds.");

        var snapshot = await collectTask;

        Assert.Equal("storage", snapshot.Id);
        Assert.Contains(
            snapshot.Specs,
            spec => spec.Label.StartsWith("Volume ", StringComparison.Ordinal) &&
                    spec.Source.StartsWith("CIM Win32_LogicalDisk", StringComparison.Ordinal));
        Assert.Contains(
            snapshot.Specs,
            spec => spec.Label.StartsWith("Volume C:", StringComparison.Ordinal));
    }

    /// <summary>
    /// The drive number comes from a WMI DeviceId string and lands in the
    /// "\\.\PhysicalDriveN" path and in the single DriveNumber byte of the
    /// ATA command. A negative value is never a drive, and 256 would
    /// truncate to byte 0 and alias drive 0, so the reader rejects anything
    /// outside 0-255 before touching kernel32. The guard must hold without
    /// a real drive present: these return null, they never throw.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    [InlineData(1000)]
    public void SataSmartReader_RejectsOutOfRangeDriveNumbers(int driveNumber)
    {
        Assert.Null(SataSmartReader.TryRead(driveNumber));
    }
}
