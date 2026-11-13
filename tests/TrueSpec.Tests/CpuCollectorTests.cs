using TrueSpec.Collectors.Windows.Collectors;
using TrueSpec.Core.Benchmarks;
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
    [InlineData(null, "Not reported by hardware")]
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
}
