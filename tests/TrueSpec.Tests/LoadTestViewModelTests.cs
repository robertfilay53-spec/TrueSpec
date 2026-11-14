using TrueSpec.App.ViewModels;
using TrueSpec.Core.Benchmarks;
using TrueSpec.Core.Sensors;

namespace TrueSpec.Tests;

/// <summary>
/// v1.4.1 (F13): the rated max and the held % are separate lines on the
/// load-test result card. A known rated max (4100 MHz from WMI) must still
/// show when no clock was measured, instead of the whole line falling
/// back to "Rated clock: not reported by hardware".
/// </summary>
public class LoadTestViewModelTests
{
    [Fact]
    public void RatedMaxKnownButNoClockMeasured_ShowsRatedMaxAndReportsHeldPercentMissing()
    {
        var result = Result(ratedMaxMHz: 4100d, clockHeldPercent: null);

        Assert.Equal("Rated max clock: 4100 MHz", LoadTestViewModel.RatedMaxText(result));
        Assert.Equal("Held % of rated clock: not reported by hardware", LoadTestViewModel.HeldPercentText(result));
    }

    [Fact]
    public void RatedMaxAndHeldPercentBothKnown_ShowsBothLines()
    {
        var result = Result(ratedMaxMHz: 4100d, clockHeldPercent: 97d);

        Assert.Equal("Rated max clock: 4100 MHz", LoadTestViewModel.RatedMaxText(result));
        Assert.Equal("Held 97% of rated clock", LoadTestViewModel.HeldPercentText(result));
    }

    [Fact]
    public void NeitherKnown_ReportsBothLinesAsNotReported()
    {
        var result = Result(ratedMaxMHz: null, clockHeldPercent: null);

        Assert.Equal("Rated max clock: not reported by hardware", LoadTestViewModel.RatedMaxText(result));
        Assert.Equal("Held % of rated clock: not reported by hardware", LoadTestViewModel.HeldPercentText(result));
    }

    /// <summary>
    /// The pre-start warning names the safety limit from the one shared
    /// constant, not a second hardcoded number: if the default ever
    /// changes, the warning still agrees with the temperature at which
    /// the test aborts.
    /// </summary>
    [Fact]
    public void WarningText_NamesTheSharedSafetyLimit()
    {
        var viewModel = new LoadTestViewModel(new UnavailableSensorMonitor(), null, false, null);

        Assert.Contains(
            $"{LoadTestConfig.DefaultAbortTemperatureC:0.#} C",
            viewModel.WarningText,
            StringComparison.Ordinal);
        Assert.Contains("60 seconds", viewModel.WarningText, StringComparison.Ordinal);
    }

    private static LoadTestResult Result(double? ratedMaxMHz, double? clockHeldPercent) => new(
        Array.Empty<LoadTestSample>(),
        PeakTempC: 70d,
        AvgClockMHz: null,
        RatedMaxMHz: ratedMaxMHz,
        ClockHeldPercent: clockHeldPercent,
        Throttled: false,
        PowerLimited: false,
        AbortReason: null,
        VerdictText: "Test verdict.",
        DurationSeconds: 60,
        GapCount: 0,
        MemoryPatternVerified: true,
        DiskReadMBps: null,
        StartedAt: DateTimeOffset.UtcNow,
        CompletedAt: DateTimeOffset.UtcNow);
}

/// <summary>
/// Localisation polish (v2.1.18): the measured live lines translate
/// their labels like their fixed-state siblings, and the elapsed line
/// translates too; only the measured values stay data. A real run
/// burns CPU, so this class carries the burn collection and nothing
/// else lives in it.
/// </summary>
[Collection("LoadTestBurn")]
public sealed class LoadTestLiveLinesTests
{
    [Fact]
    public async Task LiveMeasuredLines_TranslateLabel_KeepInvariantValue()
    {
        var viewModel = new LoadTestViewModel(new FakeSensorMonitor(50d, 4200d), null, false, null, "de");
        viewModel.SelectedDurationSeconds = 5;

        var run = viewModel.StartCommand.ExecuteAsync(null);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (viewModel.LiveTemperatureText != "Temperatur: 50 C" && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        Assert.Equal("Temperatur: 50 C", viewModel.LiveTemperatureText);
        Assert.Equal("Takt: 4200 MHz", viewModel.LiveClockText);

        // A language switch mid-run re-resolves the live lines.
        viewModel.ApplyLanguage("en");
        Assert.Equal("Temperature: 50 C", viewModel.LiveTemperatureText);
        Assert.Equal("Clock: 4200 MHz", viewModel.LiveClockText);
        viewModel.ApplyLanguage("de");
        Assert.Equal("Temperatur: 50 C", viewModel.LiveTemperatureText);

        viewModel.CancelCommand.Execute(null);
        await run;

        Assert.StartsWith("Abgeschlossen in ", viewModel.ElapsedText, StringComparison.Ordinal);
        Assert.EndsWith(" s.", viewModel.ElapsedText, StringComparison.Ordinal);
    }

    /// <summary>A scripted monitor that always reports the same temperature and clock, so a run's live lines are deterministic.</summary>
    private sealed class FakeSensorMonitor : ISensorMonitor
    {
        private readonly double _tempC;
        private readonly double _clockMHz;

        public FakeSensorMonitor(double tempC, double clockMHz)
        {
            _tempC = tempC;
            _clockMHz = clockMHz;
        }

        public bool IsAvailable => true;

        public string StatusText => "Fake sensor monitor (test only)";

        public Task<SensorSnapshot> ReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var readings = new List<SensorReading>
            {
                new("CPU", "Package", SensorKind.Temperature, "C", _tempC, "Test"),
                new("CPU", "Core #1", SensorKind.Clock, "MHz", _clockMHz, "Test"),
                new("CPU", "Core #2", SensorKind.Clock, "MHz", _clockMHz, "Test"),
            };
            return Task.FromResult(new SensorSnapshot(true, "Test", DateTimeOffset.Now, readings));
        }
    }
}
