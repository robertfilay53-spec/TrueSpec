using TrueSpec.Core.Sensors;

namespace TrueSpec.Tests;

/// <summary>
/// Tests for the U6 sensor log writer: one CSV row per sample, gaps recorded
/// as empty values (never zero, never interpolated), and a hard cap on the
/// file size so the log can never grow unbounded.
/// </summary>
public class SensorLogWriterTests
{
    private static SensorReading Temp(string hardware, string name, double? value) =>
        new(hardware, name, SensorKind.Temperature, "C", value, "Test");

    private static SensorReading Load(string hardware, string name, double? value) =>
        new(hardware, name, SensorKind.Load, "%", value, "Test");

    private static SensorSnapshot Snapshot(DateTimeOffset at, params SensorReading[] readings) =>
        new(true, "test", at, readings);

    private static string TempFilePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "sensor-log.csv");
    }

    private static void DeleteDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void WritesHeaderAndOneRowPerSample()
    {
        var path = TempFilePath();
        try
        {
            var writer = new SensorLogWriter(path);
            var at = new DateTimeOffset(2026, 11, 12, 14, 30, 0, TimeSpan.Zero);
            Assert.True(writer.Append(Snapshot(at, Temp("CPU", "Package", 71.5), Temp("GPU", "GPU Core", 55))));
            Assert.True(writer.Append(Snapshot(at.AddSeconds(2), Temp("CPU", "Package", 72), Temp("GPU", "GPU Core", 56))));

            var lines = File.ReadAllLines(path);
            Assert.Equal(3, lines.Length);
            Assert.Equal("Timestamp,CPU_Package_C,GPU_GPU_Core_C", lines[0]);
            Assert.Equal("2026-11-12T14:30:00.0000000Z,71.5,55", lines[1]);
            Assert.Equal("2026-11-12T14:30:02.0000000Z,72,56", lines[2]);
        }
        finally
        {
            DeleteDirectory(path);
        }
    }

    [Fact]
    public void GapIsRecordedAsEmpty_NeverZeroOrInterpolated()
    {
        var path = TempFilePath();
        try
        {
            var writer = new SensorLogWriter(path);
            var at = new DateTimeOffset(2026, 11, 12, 14, 30, 0, TimeSpan.Zero);
            writer.Append(Snapshot(at, Temp("CPU", "Package", 71.5), Temp("GPU", "GPU Core", 55)));
            // The GPU sensor stopped reporting: the row is still written,
            // with the missing value empty.
            writer.Append(Snapshot(at.AddSeconds(2), Temp("CPU", "Package", 72)));
            // A reading whose value is null is a gap too, not a zero.
            writer.Append(Snapshot(at.AddSeconds(4), Temp("CPU", "Package", null), Temp("GPU", "GPU Core", 56)));

            var lines = File.ReadAllLines(path);
            Assert.Equal(4, lines.Length);
            Assert.Equal("2026-11-12T14:30:02.0000000Z,72,", lines[2]);
            Assert.Equal("2026-11-12T14:30:04.0000000Z,,56", lines[3]);
        }
        finally
        {
            DeleteDirectory(path);
        }
    }

    [Fact]
    public void UnavailableSnapshotStillWritesATimestampRow()
    {
        var path = TempFilePath();
        try
        {
            var writer = new SensorLogWriter(path);
            var at = new DateTimeOffset(2026, 11, 12, 14, 30, 0, TimeSpan.Zero);
            writer.Append(Snapshot(at, Temp("CPU", "Package", 71.5)));
            writer.Append(SensorSnapshot.Unavailable("sweep failed"));

            var lines = File.ReadAllLines(path);
            Assert.Equal(3, lines.Length);
            Assert.StartsWith("Timestamp,", lines[0]);
            Assert.EndsWith(",", lines[2]);
        }
        finally
        {
            DeleteDirectory(path);
        }
    }

    [Fact]
    public void CapKeepsOnlyTheNewestRows()
    {
        var path = TempFilePath();
        try
        {
            var writer = new SensorLogWriter(path, maxRows: 5, trimToRows: 3);
            var at = new DateTimeOffset(2026, 11, 12, 14, 30, 0, TimeSpan.Zero);
            for (var i = 0; i < 7; i++)
            {
                writer.Append(Snapshot(at.AddSeconds(i * 2), Temp("CPU", "Package", 70 + i)));
            }

            var lines = File.ReadAllLines(path);
            // Header plus the newest 3 rows (samples 4, 5, 6): the trim
            // fires when the 6th row lands, keeping 3, then rows 6 and 7
            // append, so 5 rows remain and the cap is respected.
            Assert.True(lines.Length <= 6, $"Expected at most header + 5 rows, got {lines.Length}");
            Assert.Equal("Timestamp,CPU_Package_C", lines[0]);
            Assert.EndsWith(",76", lines[^1]);
            Assert.DoesNotContain(lines, line => line.Contains(",70,") || line.EndsWith(",70"));
        }
        finally
        {
            DeleteDirectory(path);
        }
    }

    [Fact]
    public void SensorAppearingAfterTheHeaderIsIgnored()
    {
        var path = TempFilePath();
        try
        {
            var writer = new SensorLogWriter(path);
            var at = new DateTimeOffset(2026, 11, 12, 14, 30, 0, TimeSpan.Zero);
            writer.Append(Snapshot(at, Temp("CPU", "Package", 71.5)));
            writer.Append(Snapshot(at.AddSeconds(2), Temp("CPU", "Package", 72), Load("CPU", "CPU Total", 44)));

            var lines = File.ReadAllLines(path);
            Assert.Equal(3, lines.Length);
            Assert.Equal("Timestamp,CPU_Package_C", lines[0]);
            Assert.Equal("2026-11-12T14:30:02.0000000Z,72", lines[2]);
        }
        finally
        {
            DeleteDirectory(path);
        }
    }

    [Fact]
    public void SecondWriterReusesTheExistingHeader()
    {
        var path = TempFilePath();
        try
        {
            var at = new DateTimeOffset(2026, 11, 12, 14, 30, 0, TimeSpan.Zero);
            new SensorLogWriter(path).Append(Snapshot(at, Temp("CPU", "Package", 71.5)));
            // A new session (new writer, same file) appends to the same columns.
            new SensorLogWriter(path).Append(Snapshot(at.AddSeconds(2), Temp("CPU", "Package", 72)));

            var lines = File.ReadAllLines(path);
            Assert.Equal(3, lines.Length);
            Assert.Equal("Timestamp,CPU_Package_C", lines[0]);
            Assert.Equal("2026-11-12T14:30:02.0000000Z,72", lines[2]);
        }
        finally
        {
            DeleteDirectory(path);
        }
    }

    [Fact]
    public void DisabledWriterWritesNothing()
    {
        var path = TempFilePath();
        try
        {
            var writer = new SensorLogWriter(path) { Enabled = false };
            var at = new DateTimeOffset(2026, 11, 12, 14, 30, 0, TimeSpan.Zero);
            Assert.True(writer.Append(Snapshot(at, Temp("CPU", "Package", 71.5))));
            Assert.False(File.Exists(path));
        }
        finally
        {
            DeleteDirectory(path);
        }
    }

    [Fact]
    public void ConstructorRejectsAnEmptyPath()
    {
        Assert.Throws<ArgumentException>(() => new SensorLogWriter(string.Empty));
        Assert.Throws<ArgumentException>(() => new SensorLogWriter("   "));
    }

    [Fact]
    public void ConstructorRejectsAnInvalidCap()
    {
        var path = TempFilePath();
        try
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SensorLogWriter(path, maxRows: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SensorLogWriter(path, maxRows: 10, trimToRows: 10));
        }
        finally
        {
            DeleteDirectory(path);
        }
    }

    [Fact]
    public void ValuesUseInvariantCulture()
    {
        var path = TempFilePath();
        try
        {
            var writer = new SensorLogWriter(path);
            var at = new DateTimeOffset(2026, 11, 12, 14, 30, 0, TimeSpan.Zero);
            writer.Append(Snapshot(at, Temp("CPU", "Package", 71.5)));

            var lines = File.ReadAllLines(path);
            Assert.Equal("2026-11-12T14:30:00.0000000Z,71.5", lines[1]);
            Assert.DoesNotContain(",", lines[1].Split(',', 2)[1].Replace("71.5", string.Empty));
        }
        finally
        {
            DeleteDirectory(path);
        }
    }
}
