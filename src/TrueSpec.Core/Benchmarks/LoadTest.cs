namespace TrueSpec.Core.Benchmarks;

/// <summary>
/// Configuration for the safe load test (Blueprint 2). The test never runs
/// longer than 120 seconds and never changes clocks, voltages or fan speeds:
/// it only loads the CPU and watches the sensors. The abort temperature is
/// a hard limit, not a target.
/// </summary>
public sealed record LoadTestConfig(
    int DurationSeconds = LoadTestConfig.DefaultDurationSeconds,
    double AbortTemperatureC = LoadTestConfig.DefaultAbortTemperatureC,
    int SampleIntervalMs = LoadTestConfig.DefaultSampleIntervalMs,
    double? RatedMaxMHz = null)
{
    public const int DefaultDurationSeconds = 60;

    public const int MaxDurationSeconds = 120;

    public const double DefaultAbortTemperatureC = 95d;

    public const int DefaultSampleIntervalMs = 1000;

    /// <summary>
    /// Clamps the duration to the 1-120 second safety envelope and the sample
    /// interval to a sane range. A non-positive or non-finite abort
    /// temperature falls back to the 95 C default instead of disabling the
    /// safety abort; a test threshold of 1 C (used by the forced-abort test)
    /// is a legitimate positive value and is kept.
    /// </summary>
    public LoadTestConfig Sanitized() => new(
        Math.Clamp(DurationSeconds, 1, MaxDurationSeconds),
        double.IsFinite(AbortTemperatureC) && AbortTemperatureC > 0d
            ? AbortTemperatureC
            : DefaultAbortTemperatureC,
        Math.Clamp(SampleIntervalMs, 100, 5000),
        RatedMaxMHz is { } rated && double.IsFinite(rated) && rated > 0d ? rated : null);
}

/// <summary>
/// One 1 Hz sensor sample taken during the load test. A null reading means
/// that sensor did not report at that second, never zero. A sample with no
/// reading at all is a gap (<see cref="IsGap"/>) and is recorded as such,
/// never interpolated. <see cref="Fans"/> carries every fan sensor that
/// reported that second (U5); a fan with no reading that second is absent
/// from the list, never recorded as 0 RPM.
/// </summary>
public sealed record LoadTestSample(
    DateTimeOffset Timestamp,
    double? CpuTempC,
    double? AvgClockMHz,
    double? MaxClockMHz,
    double? FanRpm,
    double? CpuPowerW,
    double? GpuTempC,
    bool IsGap,
    IReadOnlyList<FanReading>? Fans = null);

/// <summary>One live progress report during a load test run.</summary>
public sealed record LoadTestProgress(
    int ElapsedSeconds,
    double? CpuTempC,
    double? AvgClockMHz,
    int SampleCount);

/// <summary>
/// The completed load test. <see cref="AvgClockMHz"/> and
/// <see cref="ClockHeldPercent"/> are averages over the last 30 seconds of
/// the test (the window the throttle verdict judges), or over all samples
/// when the test ran shorter than 30 seconds. <see cref="AbortReason"/> is
/// null for a test that ran to its full duration. <see cref="FanHealth"/>
/// is the U5 fan-health verdict measured from the same samples; it is null
/// only for results built before U5, and it travels with the result into
/// the snapshot store, so a saved load test keeps its fan readings.
/// </summary>
public sealed record LoadTestResult(
    IReadOnlyList<LoadTestSample> Samples,
    double? PeakTempC,
    double? AvgClockMHz,
    double? RatedMaxMHz,
    double? ClockHeldPercent,
    bool Throttled,
    bool PowerLimited,
    string? AbortReason,
    string VerdictText,
    int DurationSeconds,
    int GapCount,
    bool MemoryPatternVerified,
    double? DiskReadMBps,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    FanHealthResult? FanHealth = null)
{
    /// <summary>Abort reason when the temperature limit stopped the test.</summary>
    public const string AbortTemperature = "temperature";

    /// <summary>Abort reason when the sensors stopped reporting mid-test.</summary>
    public const string AbortSensorLost = "sensor-lost";

    /// <summary>Abort reason when the user cancelled the test.</summary>
    public const string AbortCancelled = "cancelled";
}
