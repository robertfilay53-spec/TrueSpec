namespace TrueSpec.Core.Benchmarks;

using System.Globalization;

/// <summary>
/// Pure analysis of load-test samples (Blueprint 2). No hardware access, no
/// clock, no I/O: the same samples always produce the same verdict, which is
/// what the tests pin down.
/// </summary>
public static class LoadTestAnalyzer
{
    /// <summary>Throttling is judged when the last-30s average clock drops below this share of rated max.</summary>
    public const double ThrottleClockFraction = 0.80d;

    /// <summary>Throttling is judged only when the peak temperature is above this many degrees C.</summary>
    public const double ThrottleTemperatureC = 85d;

    /// <summary>The verdict window: the last 30 seconds of samples.</summary>
    public static readonly TimeSpan VerdictWindow = TimeSpan.FromSeconds(30);

    public static LoadTestResult Analyze(
        IReadOnlyList<LoadTestSample> samples,
        double? ratedMaxMHz,
        string? abortReason,
        int durationSeconds,
        bool memoryPatternVerified = true,
        double? diskReadMBps = null)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var ordered = samples.OrderBy(s => s.Timestamp).ToList();
        var startedAt = ordered.Count > 0 ? ordered[0].Timestamp : DateTimeOffset.Now;
        var completedAt = ordered.Count > 0 ? ordered[^1].Timestamp : DateTimeOffset.Now;

        var peakTemp = ordered
            .Where(s => s.CpuTempC.HasValue)
            .Select(s => s.CpuTempC!.Value)
            .DefaultIfEmpty()
            .Max();
        var hasTemperature = ordered.Any(s => s.CpuTempC.HasValue);
        double? peakTempOrNull = null;
        if (hasTemperature)
        {
            peakTempOrNull = peakTemp;
        }

        // The verdict window is the last 30 seconds. A test shorter than
        // 30 seconds is judged on all of its samples.
        var windowStart = ordered.Count > 0
            ? ordered[^1].Timestamp - VerdictWindow
            : DateTimeOffset.MinValue;
        var windowClocks = ordered
            .Where(s => s.Timestamp >= windowStart && s.AvgClockMHz.HasValue)
            .Select(s => s.AvgClockMHz!.Value)
            .ToList();
        double? avgClock = null;
        if (windowClocks.Count > 0)
        {
            avgClock = windowClocks.Average();
        }

        double? clockHeldPercent = avgClock.HasValue && ratedMaxMHz is { } rated && rated > 0d
            ? avgClock.Value / rated * 100d
            : null;

        var belowRated = avgClock.HasValue && ratedMaxMHz is { } ratedMax && ratedMax > 0d &&
            avgClock.Value < ThrottleClockFraction * ratedMax;
        var hot = peakTempOrNull is { } peak && peak > ThrottleTemperatureC;

        // Throttled needs both halves: a slow clock AND heat. A slow clock
        // with a cool CPU is power-limited or firmware-limited, never
        // thermal throttling, and the verdict says exactly that.
        var throttled = belowRated && hot;
        var powerLimited = belowRated && !hot && hasTemperature;

        var verdict = BuildVerdict(
            ordered.Count,
            avgClock,
            ratedMaxMHz,
            clockHeldPercent,
            peakTempOrNull,
            throttled,
            powerLimited,
            belowRated,
            abortReason,
            memoryPatternVerified);

        return new LoadTestResult(
            ordered,
            peakTempOrNull,
            avgClock,
            ratedMaxMHz,
            clockHeldPercent,
            throttled,
            powerLimited,
            abortReason,
            verdict,
            durationSeconds,
            ordered.Count(s => s.IsGap),
            memoryPatternVerified,
            diskReadMBps,
            startedAt,
            completedAt);
    }

    private static string BuildVerdict(
        int sampleCount,
        double? avgClock,
        double? ratedMaxMHz,
        double? clockHeldPercent,
        double? peakTemp,
        bool throttled,
        bool powerLimited,
        bool belowRated,
        string? abortReason,
        bool memoryPatternVerified)
    {
        var memoryNote = memoryPatternVerified
            ? string.Empty
            : " The memory pattern check did not verify.";

        if (sampleCount == 0)
        {
            return "The load test recorded no samples, so nothing can be said about throttling." + memoryNote;
        }

        if (abortReason == LoadTestResult.AbortCancelled)
        {
            return "The load test was cancelled before it finished, so the result is incomplete and proves nothing about throttling." + memoryNote;
        }

        if (abortReason == LoadTestResult.AbortSensorLost)
        {
            return "The sensors stopped reporting during the load test, so the result is incomplete. No throttle verdict can be given." + memoryNote;
        }

        var peakText = peakTemp is { } peak
            ? FormattableString.Invariant($" at {peak:0.#} C peak")
            : " with no temperature reading";

        if (abortReason == LoadTestResult.AbortTemperature)
        {
            var clockText = avgClock is { } clock && clockHeldPercent is { } percent
                ? FormattableString.Invariant($", averaging {clock:0} MHz ({percent:0}% of rated max)")
                : string.Empty;
            return FormattableString.Invariant($"The load test stopped automatically at {peakTemp:0.#} C, above the 95 C safety limit{clockText}. The cooling could not keep up with a full load.") + memoryNote;
        }

        if (ratedMaxMHz is null || ratedMaxMHz <= 0d)
        {
            return avgClock is { } clock
                ? FormattableString.Invariant($"Rated clock was not reported, so throttling could not be judged. The CPU averaged {clock:0} MHz{peakText}.") + memoryNote
                : "Rated clock was not reported and no clock reading was recorded, so throttling could not be judged." + memoryNote;
        }

        if (avgClock is null || clockHeldPercent is null)
        {
            return peakTemp is { } temp
                ? FormattableString.Invariant($"No clock reading was recorded, so throttling could not be judged. Peak temperature was {temp:0.#} C.") + memoryNote
                : "No clock or temperature reading was recorded, so throttling could not be judged." + memoryNote;
        }

        if (throttled)
        {
            return FormattableString.Invariant($"CPU throttled under load: it averaged {avgClock:0} MHz ({clockHeldPercent:0}% of its rated {ratedMaxMHz:0} MHz max) over the last 30 seconds at {peakTemp:0.#} C peak. Cooling is not coping.") + memoryNote;
        }

        if (powerLimited)
        {
            return FormattableString.Invariant($"CPU averaged {avgClock:0} MHz ({clockHeldPercent:0}% of its rated {ratedMaxMHz:0} MHz max) with a peak temperature of {peakTemp:0.#} C, so it was power-limited or firmware-limited, not thermally throttled.") + memoryNote;
        }

        if (belowRated)
        {
            // Slow clock, no temperature data at all: the slowdown is real
            // but its cause is unknown, so "no throttling detected" would
            // be a claim the data cannot support.
            return FormattableString.Invariant($"CPU averaged {avgClock:0} MHz ({clockHeldPercent:0}% of its rated {ratedMaxMHz:0} MHz max) but no temperature was reported, so it could not be judged whether the slowdown was thermal throttling.") + memoryNote;
        }

        return FormattableString.Invariant($"CPU held {avgClock:0} MHz average ({clockHeldPercent:0}% of its rated {ratedMaxMHz:0} MHz max){peakText}. No throttling detected.") + memoryNote;
    }
}
