using System.Windows.Input;
using TrueSpec.App.ViewModels;
using TrueSpec.Core;
using TrueSpec.Core.Models;
using TrueSpec.Core.Sensors;
using TrueSpec.Core.Settings;

namespace TrueSpec.Tests;

/// <summary>
/// Windows-leg tests for the U6 Overview wiring: live snapshots reach the
/// sensor log and the threshold evaluator, alerts surface on the Overview,
/// and the History page charts the log. The timer is never waited on: the
/// tests drive <see cref="OverviewViewModel.ProcessSensorSnapshot"/>
/// directly with explicit timestamps.
/// </summary>
public class SensorAlertViewModelTests
{
    private sealed class TestCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
        }
    }

    /// <summary>Returns one cool snapshot forever: the constructor refresh and the timer ticks stay quiet.</summary>
    private sealed class CoolSensorMonitor : ISensorMonitor
    {
        public bool IsAvailable => true;

        public string StatusText => "test sensors";

        public Task<SensorSnapshot> ReadAsync(CancellationToken cancellationToken = default)
        {
            var snapshot = new SensorSnapshot(
                true,
                "test",
                new DateTimeOffset(2026, 11, 12, 14, 30, 0, TimeSpan.FromHours(2)),
                new[] { new SensorReading("CPU", "Package", SensorKind.Temperature, "C", 70, "Test") });
            return Task.FromResult(snapshot);
        }
    }

    private static SensorSnapshot HotSnapshot(DateTimeOffset at, double cpuTemp) =>
        new(true, "test", at, new[]
        {
            new SensorReading("CPU", "Package", SensorKind.Temperature, "C", cpuTemp, "Test"),
            new SensorReading("GPU", "GPU Core", SensorKind.Temperature, "C", 55, "Test"),
        });

    private static string TempLogPath()
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

    private static DateTimeOffset At(int second) =>
        new(2026, 11, 12, 14, 32, second, TimeSpan.FromHours(2));

    private static (OverviewViewModel Overview, SensorLogWriter Writer) CreateOverview(string logPath)
    {
        var snapshot = TestSnapshots.Healthy();
        var writer = new SensorLogWriter(logPath);
        var evaluator = new ThresholdAlertEvaluator(ThresholdAlertEvaluator.CpuTemperatureLabel, "C");
        var settings = new AppSettings
        {
            SensorLoggingEnabled = true,
            CpuTemperatureThresholdC = 90,
            CpuTemperatureThresholdSeconds = 5,
        };
        var overview = new OverviewViewModel(
            snapshot,
            _ => { },
            new TestCommand(),
            new TestCommand(),
            new CoolSensorMonitor(),
            refreshInterval: TimeSpan.FromSeconds(30),
            sensorLogWriter: writer,
            alertEvaluator: evaluator,
            settings: settings);
        return (overview, writer);
    }

    [Fact]
    public void SustainedHotSnapshotsAlertOnTheOverviewAndLogToCsv()
    {
        var logPath = TempLogPath();
        try
        {
            using var overview = CreateOverview(logPath).Overview;

            Assert.False(overview.HasSensorAlert);
            Assert.Empty(overview.SensorAlerts);

            overview.ProcessSensorSnapshot(HotSnapshot(At(0), 95));
            Assert.False(overview.HasSensorAlert);

            overview.ProcessSensorSnapshot(HotSnapshot(At(5), 96));

            Assert.True(overview.HasSensorAlert);
            Assert.Single(overview.SensorAlerts);
            Assert.Equal("CPU temperature exceeded 90 C for 5 seconds at 14:32.", overview.LatestSensorAlertText);
            Assert.Equal(overview.LatestSensorAlertText, overview.SensorAlerts[0]);

            // The log has the samples: header plus the hot rows (and the
            // cool constructor refresh row, which is harmless here).
            var series = SensorLogReader.Read(logPath, "CPU_Package_C");
            Assert.True(series.RowCount >= 2);
            Assert.Contains(series.Points, p => p.Value == 95);
            Assert.Contains(series.Points, p => p.Value == 96);
        }
        finally
        {
            DeleteDirectory(logPath);
        }
    }

    [Fact]
    public void SingleHotSpikeAlertsNothingButStillLogs()
    {
        var logPath = TempLogPath();
        try
        {
            using var overview = CreateOverview(logPath).Overview;

            overview.ProcessSensorSnapshot(HotSnapshot(At(0), 99));
            overview.ProcessSensorSnapshot(HotSnapshot(At(2), 70));

            Assert.False(overview.HasSensorAlert);
            Assert.Empty(overview.SensorAlerts);

            var series = SensorLogReader.Read(logPath, "CPU_Package_C");
            Assert.Contains(series.Points, p => p.Value == 99);
        }
        finally
        {
            DeleteDirectory(logPath);
        }
    }

    [Fact]
    public void ApplySensorSettingsSwitchesLoggingOff()
    {
        var logPath = TempLogPath();
        try
        {
            var (overview, _) = CreateOverview(logPath);
            using var _ = overview;

            overview.ProcessSensorSnapshot(HotSnapshot(At(0), 71));
            var rowsAfterFirst = SensorLogReader.Read(logPath, "CPU_Package_C").RowCount;
            Assert.True(rowsAfterFirst >= 1);

            overview.ApplySensorSettings(new AppSettings
            {
                SensorLoggingEnabled = false,
                CpuTemperatureThresholdC = null,
            });

            overview.ProcessSensorSnapshot(HotSnapshot(At(5), 72));
            overview.ProcessSensorSnapshot(HotSnapshot(At(10), 73));

            Assert.Equal(rowsAfterFirst, SensorLogReader.Read(logPath, "CPU_Package_C").RowCount);
            Assert.Contains("logging is off", overview.SensorLogText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("No temperature alert", overview.AlertSettingsText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(logPath);
        }
    }

    [Fact]
    public void HistoryChartsTheLogWithTheThreshold()
    {
        var logPath = TempLogPath();
        try
        {
            using var overview = CreateOverview(logPath).Overview;
            overview.ProcessSensorSnapshot(HotSnapshot(At(0), 71));
            overview.ProcessSensorSnapshot(HotSnapshot(At(2), 72));
            overview.ProcessSensorSnapshot(HotSnapshot(At(4), 73));

            var history = new HistoryViewModel(
                null,
                TestSnapshots.Healthy(),
                sensorLogPath: logPath,
                sensorLoggingEnabled: true,
                cpuTemperatureThresholdC: 90);
            history.LoadSensorLog();

            Assert.True(history.HasSensorLog);
            Assert.Equal(90, history.SensorLogThresholdC);
            Assert.NotNull(history.CpuTemperatureLogSegments);
            var onlySegment = Assert.Single(history.CpuTemperatureLogSegments!);
            Assert.Equal(3, onlySegment.Count);
            Assert.Equal(0d, onlySegment[0].XFraction);
            Assert.Equal(0.5d, onlySegment[1].XFraction);
            Assert.Equal(1d, onlySegment[2].XFraction);
            Assert.Equal(71d, onlySegment[0].Value);
            Assert.Equal(73d, onlySegment[2].Value);
            Assert.Contains("samples", history.SensorLogStatusText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(logPath);
        }
    }

    [Fact]
    public void HistoryShowsEmptyStateWhenLoggingIsOff()
    {
        var history = new HistoryViewModel(
            null,
            TestSnapshots.Healthy(),
            sensorLogPath: null,
            sensorLoggingEnabled: false,
            cpuTemperatureThresholdC: null);
        history.LoadSensorLog();

        Assert.False(history.HasSensorLog);
        Assert.Null(history.CpuTemperatureLogSegments);
        Assert.Contains("logging is off", history.SensorLogStatusText, StringComparison.OrdinalIgnoreCase);
    }
}
