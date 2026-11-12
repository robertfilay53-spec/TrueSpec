using TrueSpec.Core.Sensors;

namespace TrueSpec.Tests;

/// <summary>
/// Tests for the U6 sensor log reader: the log round-trips through the
/// writer, gaps stay gaps, and a damaged file degrades to the readable rows
/// instead of failing the whole read.
/// </summary>
public class SensorLogReaderTests
{
    private static SensorReading Temp(string hardware, string name, double? value) =>
        new(hardware, name, SensorKind.Temperature, "C", value, "Test");

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

    private static string WriteLog(string path)
    {
        var writer = new SensorLogWriter(path);
        var at = new DateTimeOffset(2026, 11, 12, 14, 30, 0, TimeSpan.Zero);
        writer.Append(new SensorSnapshot(true, "test", at, new[]
        {
            Temp("CPU", "Package", 71.5),
            Temp("GPU", "GPU Core", 55),
        }));
        writer.Append(new SensorSnapshot(true, "test", at.AddSeconds(2), new[]
        {
            Temp("CPU", "Package", 72),
        }));
        writer.Append(new SensorSnapshot(true, "test", at.AddSeconds(4), new[]
        {
            Temp("CPU", "Package", 73),
            Temp("GPU", "GPU Core", 57),
        }));
        return path;
    }

    [Fact]
    public void RoundTripPreservesValuesAndGaps()
    {
        var path = TempFilePath();
        try
        {
            WriteLog(path);

            var series = SensorLogReader.Read(path, "CPU_Package_C");

            Assert.Equal(3, series.RowCount);
            Assert.Equal(3, series.Points.Count);
            Assert.Equal(71.5, series.Points[0].Value);
            Assert.Equal(72, series.Points[1].Value);
            Assert.Equal(73, series.Points[2].Value);
            Assert.Equal(0, series.GapCount);
            Assert.Equal(new DateTimeOffset(2026, 11, 12, 14, 30, 0, TimeSpan.Zero), series.FirstAt);
            Assert.Equal(new DateTimeOffset(2026, 11, 12, 14, 30, 4, TimeSpan.Zero), series.LastAt);
        }
        finally
        {
            DeleteDirectory(path);
        }
    }

    [Fact]
    public void GapInTheColumnCountsAsAGapNotAZero()
    {
        var path = TempFilePath();
        try
        {
            WriteLog(path);

            var series = SensorLogReader.Read(path, "GPU_GPU_Core_C");

            Assert.Equal(3, series.Points.Count);
            Assert.Equal(55, series.Points[0].Value);
            Assert.Null(series.Points[1].Value);
            Assert.Equal(57, series.Points[2].Value);
            Assert.Equal(1, series.GapCount);
        }
        finally
        {
            DeleteDirectory(path);
        }
    }

    [Fact]
    public void MissingFileReturnsAnEmptySeries()
    {
        var path = TempFilePath();
        try
        {
            var series = SensorLogReader.Read(path, "CPU_Package_C");

            Assert.Equal(0, series.RowCount);
            Assert.Empty(series.Points);
            Assert.Null(series.FirstAt);
            Assert.Null(series.LastAt);
        }
        finally
        {
            DeleteDirectory(path);
        }
    }

    [Fact]
    public void UnknownColumnReturnsEmptyPointsForEveryRow()
    {
        var path = TempFilePath();
        try
        {
            WriteLog(path);

            var series = SensorLogReader.Read(path, "CPU_No_Such_Sensor_C");

            Assert.Equal(3, series.RowCount);
            Assert.Equal(3, series.Points.Count);
            Assert.All(series.Points, point => Assert.Null(point.Value));
            Assert.Equal(3, series.GapCount);
        }
        finally
        {
            DeleteDirectory(path);
        }
    }

    [Fact]
    public void MalformedLinesAreSkippedNotFatal()
    {
        var path = TempFilePath();
        try
        {
            WriteLog(path);
            File.AppendAllText(path, "this is not a row\n");
            File.AppendAllText(path, "2026-11-12T14:30:06.0000000Z,not-a-number,58\n");

            var series = SensorLogReader.Read(path, "CPU_Package_C");

            Assert.Equal(4, series.RowCount);
            Assert.Equal(4, series.Points.Count);
            Assert.Null(series.Points[3].Value);
            Assert.Equal(1, series.GapCount);
        }
        finally
        {
            DeleteDirectory(path);
        }
    }

    [Theory]
    [InlineData(new[] { "Timestamp", "CPU_Package_C", "GPU_GPU_Core_C" }, "CPU_Package_C")]
    [InlineData(new[] { "Timestamp", "CPU_Core_Max_C", "CPU_Package_C" }, "CPU_Core_Max_C")]
    [InlineData(new[] { "Timestamp", "GPU_GPU_Core_C" }, null)]
    [InlineData(new[] { "Timestamp", "CPU_CPU_Total_pct" }, null)]
    public void FindCpuTemperatureColumnPrefersTheFirstCpuTemperature(
        string[] columns,
        string? expected)
    {
        Assert.Equal(expected, SensorLogReader.FindCpuTemperatureColumn(columns));
    }

    [Fact]
    public void ReadColumnsReturnsTheHeader()
    {
        var path = TempFilePath();
        try
        {
            WriteLog(path);

            var columns = SensorLogReader.ReadColumns(path);

            Assert.Equal(new[] { "CPU_Package_C", "GPU_GPU_Core_C" }, columns);
            Assert.Empty(SensorLogReader.ReadColumns(Path.Combine(Path.GetDirectoryName(path)!, "missing.csv")));
        }
        finally
        {
            DeleteDirectory(path);
        }
    }

    [Fact]
    public void Read_CapsAtTheNewestMaxRows()
    {
        // Hardening 6g: a hand-edited file with millions of rows must not
        // balloon memory. The read keeps the newest rows; RowCount still
        // counts every row in the file so "newest N of M" stays truthful.
        var path = TempFilePath();
        try
        {
            var at = new DateTimeOffset(2026, 11, 12, 14, 30, 0, TimeSpan.Zero);
            var lines = new List<string> { "Timestamp,CPU_Package_C" };
            for (var i = 0; i < SensorLogReader.MaxRows + 1; i++)
            {
                lines.Add($"{at.AddSeconds(i):o},71.5");
            }

            File.WriteAllLines(path, lines);

            var series = SensorLogReader.Read(path, "CPU_Package_C");

            Assert.Equal(SensorLogReader.MaxRows + 1, series.RowCount);
            Assert.Equal(SensorLogReader.MaxRows, series.Points.Count);
            Assert.Equal(at.AddSeconds(1), series.FirstAt);
            Assert.Equal(at.AddSeconds(SensorLogReader.MaxRows), series.LastAt);
        }
        finally
        {
            DeleteDirectory(path);
        }
    }
}
