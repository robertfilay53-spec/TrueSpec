using TrueSpec.Collectors.Windows.Collectors;
using TrueSpec.Core.Benchmarks;
using TrueSpec.Core.HardwareDatabase;
using TrueSpec.Core.Models;
using TrueSpec.Core.Sensors;

namespace TrueSpec.Tests;

/// <summary>
/// Windows-leg only: the collector reads the real CPU CIM class and the
/// LibreHardwareMonitor temperature sensor. On Linux this file is excluded
/// from the build (see TrueSpec.Tests.csproj), like StorageCollectorTests.
/// No temperature is asserted: it depends on the machine the test runs on.
/// What is asserted is the coupling: the thermal throttle flag must agree
/// with the measured temperature at the one shared safety limit
/// (<see cref="LoadTestConfig.DefaultAbortTemperatureC"/>), and its source
/// must name that same limit, so a future change to the limit cannot
/// silently leave the flag or the label behind.
/// </summary>
public class CpuCollectorTests
{
    /// <summary>
    /// The throttle flag is judged at the one shared safety limit: below
    /// it "No", at it "Yes", and no reading is honest-empty, never a
    /// guessed "No". The boundary is pinned against the constant, not a
    /// second hardcoded number.
    /// </summary>
    [Theory]
    [InlineData(null, SpecLabels.NotReportedByHardware)]
    [InlineData(0d, "No")]
    [InlineData(94.9, "No")]
    [InlineData(95d, "Yes")]
    [InlineData(120d, "Yes")]
    public void ThrottleFlag_IsJudgedAtTheSharedSafetyLimit(double? temperatureC, string expected)
    {
        Assert.Equal(LoadTestConfig.DefaultAbortTemperatureC, 95d);
        Assert.Equal(expected, CpuCollector.ThrottleFlag(temperatureC));
    }

    [Fact]
    public async Task CpuCollector_ThrottleFlagAgreesWithTemperatureAtTheSharedSafetyLimit()
    {
        var collector = new CpuCollector(new UnavailableSensorMonitor());

        Assert.Equal("cpu", collector.Id);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var collectTask = collector.CollectAsync(timeout.Token);
        var finished = await Task.WhenAny(collectTask, Task.Delay(TimeSpan.FromSeconds(60)));

        Assert.True(
            finished == collectTask,
            "CpuCollector did not finish within 60 seconds.");

        var snapshot = await collectTask;

        var temperature = snapshot.Specs.FirstOrDefault(s =>
            string.Equals(s.Label, SpecLabels.PackageTemperature, StringComparison.Ordinal));
        var flag = snapshot.Specs.FirstOrDefault(s =>
            string.Equals(s.Label, SpecLabels.ThermalThrottleFlag, StringComparison.Ordinal));

        Assert.NotNull(temperature);
        Assert.NotNull(flag);

        // The source names the threshold the flag was judged at, from the
        // same constant, not a second hardcoded number.
        Assert.Contains(
            $"{LoadTestConfig.DefaultAbortTemperatureC:0.#} C",
            flag.Source,
            StringComparison.Ordinal);

        if (temperature.IsNotReported)
        {
            // No temperature reading means no throttle judgement either:
            // the flag is honest-empty, never a guessed "No".
            Assert.True(flag.IsNotReported);
            return;
        }

        Assert.NotNull(temperature.NumericValue);
        Assert.Equal("C", temperature.NumericUnit);
        var expected = temperature.NumericValue.Value >= LoadTestConfig.DefaultAbortTemperatureC
            ? "Yes"
            : "No";
        Assert.Equal(expected, flag.Value);
    }

    /// <summary>
    /// The rated boost clock from the bundled hardware database (Beyond
    /// v2.0, item 5): it is shown under its own label with the dataset
    /// version as its source, it sits after the measured Max clock, and
    /// it never replaces it. The E-core figure rides along for hybrid
    /// chips. The stub database matches every model, so the test is
    /// deterministic whatever CPU the runner has.
    /// </summary>
    [Fact]
    public async Task CpuCollector_RatedBoostClockIsShownAsItsOwnSpec()
    {
        var database = new FixedHardwareDatabase(
            new CpuSpec("TEST", "Intel", "Test CPU", 2.5, 4.7, 3.5, "https://example.invalid/test", "2026-11-14"));
        var snapshot = await CollectWithDatabaseAsync(database);

        var maxClock = Spec(snapshot, SpecLabels.MaxClock);
        var rated = Spec(snapshot, SpecLabels.RatedBoostClock);
        var eCore = Spec(snapshot, SpecLabels.RatedECoreBoostClock);

        Assert.NotNull(maxClock);
        Assert.Equal("CIM Win32_Processor", maxClock.Source);
        Assert.NotNull(rated);
        Assert.Equal("4.70 GHz", rated.Value);
        Assert.Equal(4700, rated.NumericValue);
        Assert.Equal("MHz", rated.NumericUnit);
        Assert.Contains("Bundled hardware database v9.9.9 (2026-11-14)", rated.Source, StringComparison.Ordinal);
        Assert.NotNull(eCore);
        Assert.Equal("3.50 GHz", eCore.Value);

        // The rated spec follows the measured one in the list and never
        // takes its place.
        var labels = snapshot.Specs.Select(s => s.Label).ToList();
        Assert.True(labels.IndexOf(SpecLabels.RatedBoostClock) > labels.IndexOf(SpecLabels.MaxClock));
    }

    /// <summary>
    /// A model the dataset does not list gets no rated spec at all:
    /// the lookup never guesses, and the collector stays silent.
    /// </summary>
    [Fact]
    public async Task CpuCollector_UnknownModelGetsNoRatedSpec()
    {
        var snapshot = await CollectWithDatabaseAsync(new NeverMatchHardwareDatabase());

        Assert.Null(Spec(snapshot, SpecLabels.RatedBoostClock));
        Assert.Null(Spec(snapshot, SpecLabels.RatedECoreBoostClock));
        Assert.NotNull(Spec(snapshot, SpecLabels.MaxClock));
    }

    /// <summary>No database supplied means no rated spec, exactly like a model that is not listed.</summary>
    [Fact]
    public async Task CpuCollector_NoDatabaseGetsNoRatedSpec()
    {
        var snapshot = await CollectWithDatabaseAsync(null);

        Assert.Null(Spec(snapshot, SpecLabels.RatedBoostClock));
    }

    private static SpecValue? Spec(ComponentSnapshot snapshot, string label) =>
        snapshot.Specs.FirstOrDefault(s => string.Equals(s.Label, label, StringComparison.Ordinal));

    private static async Task<ComponentSnapshot> CollectWithDatabaseAsync(IHardwareDatabase? database)
    {
        var collector = new CpuCollector(new UnavailableSensorMonitor(), database);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var collectTask = collector.CollectAsync(timeout.Token);
        var finished = await Task.WhenAny(collectTask, Task.Delay(TimeSpan.FromSeconds(60)));

        Assert.True(finished == collectTask, "CpuCollector did not finish within 60 seconds.");
        return await collectTask;
    }

    /// <summary>A database that matches every model with one fixed spec.</summary>
    private sealed class FixedHardwareDatabase(CpuSpec spec) : IHardwareDatabase
    {
        public string Version => "9.9.9";

        public string Updated => "2026-11-14";

        public int CpuCount => 1;

        public int GpuCount => 0;

        public bool TryGetCpu(string? modelName, out CpuSpec found)
        {
            found = spec;
            return true;
        }

        public bool TryGetGpu(string? modelName, out GpuSpec found)
        {
            found = null!;
            return false;
        }
    }

    /// <summary>A database that matches nothing.</summary>
    private sealed class NeverMatchHardwareDatabase : IHardwareDatabase
    {
        public string Version => "9.9.9";

        public string Updated => "2026-11-14";

        public int CpuCount => 0;

        public int GpuCount => 0;

        public bool TryGetCpu(string? modelName, out CpuSpec spec)
        {
            spec = null!;
            return false;
        }

        public bool TryGetGpu(string? modelName, out GpuSpec spec)
        {
            spec = null!;
            return false;
        }
    }
}
