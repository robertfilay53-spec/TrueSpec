using TrueSpec.Core.Sensors;

namespace TrueSpec.Tests;

/// <summary>
/// Regression tests for the v1.4 release-blocker honesty defect: a sensor
/// library that does not report a value returns 0 (notably
/// LibreHardwareMonitor on virtual machines), and a 0 clock or 0 C
/// temperature was passed through as a real reading — so the load test
/// concluded a CPU under load was "power-limited" at 0 MHz / 0 C. A running
/// CPU cannot be at 0 MHz or 0 C, so a zero clock or temperature is "not
/// reporting", never data. A 0 RPM fan IS a real reading (a stopped fan)
/// and a 0% load IS a real reading (an idle CPU), so those keep zero.
/// </summary>
public class SensorSnapshotTests
{
    private static SensorSnapshot Snapshot(params SensorReading[] readings) =>
        new(true, "test", DateTimeOffset.Now, readings);

    private static SensorReading Clock(string name, double? value) =>
        new("CPU", name, SensorKind.Clock, "MHz", value, "Test");

    private static SensorReading Temp(string hardware, string name, double? value) =>
        new(hardware, name, SensorKind.Temperature, "C", value, "Test");

    private static SensorReading Fan(string name, double? value) =>
        new("CPU", name, SensorKind.Fan, "RPM", value, "Test");

    private static SensorReading Load(string name, double? value) =>
        new("CPU", name, SensorKind.Load, "%", value, "Test");

    [Fact]
    public void ZeroClock_IsNotAReading()
    {
        var reading = Clock("Core #1", 0d);

        Assert.False(reading.HasValue);
        Assert.Equal("Not available", reading.DisplayText);
    }

    [Fact]
    public void ZeroTemperature_IsNotAReading()
    {
        var reading = Temp("CPU", "Package", 0d);

        Assert.False(reading.HasValue);
        Assert.Equal("Not available", reading.DisplayText);
    }

    [Fact]
    public void NegativeClockAndTemperature_AreNotReadings()
    {
        Assert.False(Clock("Core #1", -100d).HasValue);
        Assert.False(Temp("CPU", "Package", -5d).HasValue);
    }

    [Fact]
    public void ZeroFanRpm_IsARealReading_AStoppedFan()
    {
        var reading = Fan("Fan #1", 0d);

        Assert.True(reading.HasValue);
        Assert.Equal("0 RPM", reading.DisplayText);
    }

    [Fact]
    public void ZeroLoad_IsARealReading_AnIdleCpu()
    {
        var reading = Load("CPU Total", 0d);

        Assert.True(reading.HasValue);
        Assert.Equal("0 %", reading.DisplayText);
    }

    [Fact]
    public void ZeroClock_IsExcludedFromCpuClocks()
    {
        var snapshot = Snapshot(Clock("Core #1", 0d), Clock("Core #2", 0d));

        Assert.Empty(snapshot.CpuClocks());
        Assert.Null(snapshot.CpuClockMHz());
    }

    [Fact]
    public void ZeroTemperature_IsNotFoundByCpuTemperature()
    {
        var snapshot = Snapshot(Temp("CPU", "Package", 0d));

        Assert.Null(snapshot.CpuTemperature());
    }

    [Fact]
    public void ZeroGpuTemperature_IsNotFoundByGpuTemperature()
    {
        var snapshot = Snapshot(Temp("GPU", "GPU Core", 0d));

        Assert.Null(snapshot.GpuTemperature());
    }

    [Fact]
    public void MixedZeroAndRealClock_OnlyTheRealClockCounts()
    {
        var snapshot = Snapshot(Clock("Core #1", 0d), Clock("Core #2", 3000d));

        Assert.Single(snapshot.CpuClocks());
        Assert.Equal(3000d, snapshot.CpuClockMHz());
    }

    [Fact]
    public void RealClockAndTemperature_AreStillReported()
    {
        var snapshot = Snapshot(Clock("Core #1", 3000d), Temp("CPU", "Package", 55d));

        Assert.Equal(3000d, snapshot.CpuClockMHz());
        Assert.Equal(55d, snapshot.CpuTemperature()?.Value);
    }

    [Fact]
    public void NullValuedReading_IsNotFound()
    {
        var snapshot = Snapshot(Temp("CPU", "Package", null));

        Assert.Null(snapshot.CpuTemperature());
    }
}
