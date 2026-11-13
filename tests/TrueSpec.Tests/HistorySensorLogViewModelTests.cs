using TrueSpec.App.ViewModels;
using TrueSpec.Core.Sensors;

namespace TrueSpec.Tests;

/// <summary>
/// F6 end to end: a real sensor-log.csv on disk, and the History page
/// ViewModel's sensor-log card. Windows leg only (it drives TrueSpec.App's
/// ViewModel), like HistoryForecastViewModelTests: excluded from the
/// net10.0 leg in the csproj.
/// </summary>
public class HistorySensorLogViewModelTests
{
    /// <summary>Fixed UTC+5 zone, standing in for PKT in the laptop test.</summary>
    private static TimeZoneInfo PktZone() =>
        TimeZoneInfo.CreateCustomTimeZone("PKT-test", TimeSpan.FromHours(5), "PKT-test", "PKT-test");

    private static SensorReading Temp(double? value) =>
        new("CPU", "Package", SensorKind.Temperature, "C", value, "Test");

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

    /// <summary>
    /// Writes <paramref name="values"/> as CPU Package rows starting at
    /// <paramref name="firstAtUtc"/> (UTC, as the writer stores them),
    /// two seconds apart.
    /// </summary>
    private static void WriteLog(string path, DateTimeOffset firstAtUtc, params double?[] values)
    {
        var writer = new SensorLogWriter(path);
        for (var i = 0; i < values.Length; i++)
        {
            writer.Append(new SensorSnapshot(
                true,
                "test",
                firstAtUtc.AddSeconds(2 * i),
                new[] { Temp(values[i]) }));
        }
    }

    private static HistoryViewModel ViewModelFor(string path) =>
        new(null, TestSnapshots.Healthy(), sensorLogPath: path, displayTimeZone: PktZone());

    [Fact]
    public void AllGaps_ShowsEmptyStateNotABlankChart()
    {
        // The laptop-test case: 89 samples, every one a gap, and the page
        // showed a blank chart. Now it must say plainly that no readings
        // were recorded and why, and the chart stays hidden.
        var path = TempFilePath();
        try
        {
            WriteLog(path, new DateTimeOffset(2026, 11, 12, 8, 45, 0, TimeSpan.Zero), null, null, null);

            var viewModel = ViewModelFor(path);
            viewModel.LoadSensorLog();

            Assert.False(viewModel.HasSensorLog);
            Assert.Null(viewModel.CpuTemperatureLogSegments);
            Assert.Equal(
                "No temperature readings were recorded in 3 samples: this machine's hardware does not report a CPU temperature sensor. This is expected on machines whose sensor is not exposed, so there is no CPU temperature chart to show.",
                viewModel.SensorLogStatusText);
            Assert.Equal(string.Empty, viewModel.SensorLogSummaryText);
        }
        finally
        {
            DeleteDirectory(path);
        }
    }

    [Fact]
    public void ZeroSamples_ShowsEmptyState()
    {
        var path = TempFilePath();
        try
        {
            File.WriteAllText(path, "Timestamp,CPU_Package_C\n");

            var viewModel = ViewModelFor(path);
            viewModel.LoadSensorLog();

            Assert.False(viewModel.HasSensorLog);
            Assert.Null(viewModel.CpuTemperatureLogSegments);
            Assert.Equal(
                "No sensor samples recorded yet. Leave TrueSpec running and the CPU temperature chart will appear here.",
                viewModel.SensorLogStatusText);
        }
        finally
        {
            DeleteDirectory(path);
        }
    }

    [Fact]
    public void OneMeasuredReading_ShowsEmptyState()
    {
        // One point cannot draw a line (SensorLogChart renders nothing
        // with fewer than two measured points), so it is an empty state
        // too, not a blank chart.
        var path = TempFilePath();
        try
        {
            WriteLog(path, new DateTimeOffset(2026, 11, 12, 8, 45, 0, TimeSpan.Zero), 70, null, null);

            var viewModel = ViewModelFor(path);
            viewModel.LoadSensorLog();

            Assert.False(viewModel.HasSensorLog);
            Assert.Equal(
                "Only one temperature reading was recorded in 3 samples, so there is not enough data to draw a chart.",
                viewModel.SensorLogStatusText);
        }
        finally
        {
            DeleteDirectory(path);
        }
    }

    [Fact]
    public void PartialGaps_StillChartsAndRangeUsesLocalTime()
    {
        // Same log shape, but one gap among measured values: the chart
        // stays, the gap stays a gap, and the range is local time:
        // 08:45-08:46 UTC is 13:45-13:46 in the UTC+5 zone. The appended
        // row lands 56 seconds after the previous one, inside the 60
        // second logging gap, so the run after the sensor gap draws as
        // one segment positioned by time (4 of 60 seconds, then the end).
        var path = TempFilePath();
        try
        {
            WriteLog(
                path,
                new DateTimeOffset(2026, 11, 12, 8, 45, 0, TimeSpan.Zero),
                70, null, 72);
            // Stretch the last sample to 08:46 so the range spans a minute.
            File.AppendAllText(path, "2026-11-12T08:46:00.0000000Z,73\n");

            var viewModel = ViewModelFor(path);
            viewModel.LoadSensorLog();

            Assert.True(viewModel.HasSensorLog);
            var onlySegment = Assert.Single(viewModel.CpuTemperatureLogSegments!);
            Assert.Equal(2, onlySegment.Count);
            Assert.Equal(4d / 60d, onlySegment[0].XFraction, precision: 10);
            Assert.Equal(72d, onlySegment[0].Value);
            Assert.Equal(1d, onlySegment[1].XFraction);
            Assert.Equal(73d, onlySegment[1].Value);
            Assert.Equal(
                "CPU temperature, 4 samples from 13:45 to 13:46.",
                viewModel.SensorLogStatusText);
            Assert.Equal(
                "4 samples, 1 gap where the sensor did not report.",
                viewModel.SensorLogSummaryText);
        }
        finally
        {
            DeleteDirectory(path);
        }
    }

    [Fact]
    public void ReadingsTooFarApart_ShowsEmptyState()
    {
        // Every measured reading stands alone: the last sample lands 176
        // seconds after the previous one, past the 60 second logging gap,
        // and the first stands alone across the sensor gap. No line can
        // be drawn, so it is an empty state with the reason, not a blank
        // chart under a status line that claims one exists.
        var path = TempFilePath();
        try
        {
            WriteLog(
                path,
                new DateTimeOffset(2026, 11, 12, 8, 45, 0, TimeSpan.Zero),
                70, null, 72);
            File.AppendAllText(path, "2026-11-12T08:48:00.0000000Z,73\n");

            var viewModel = ViewModelFor(path);
            viewModel.LoadSensorLog();

            Assert.False(viewModel.HasSensorLog);
            Assert.Null(viewModel.CpuTemperatureLogSegments);
            Assert.Equal(
                "The temperature readings are too far apart in time to draw a line: each reading stands alone because logging was not running between them.",
                viewModel.SensorLogStatusText);
        }
        finally
        {
            DeleteDirectory(path);
        }
    }
}
