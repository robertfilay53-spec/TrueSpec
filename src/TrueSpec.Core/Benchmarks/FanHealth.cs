namespace TrueSpec.Core.Benchmarks;

using System.Globalization;

/// <summary>
/// One fan reading in one load-test sample: the fan's display name (hardware
/// group + sensor name, e.g. "CPU Fan #1") and its speed in RPM. A null RPM
/// means that fan did not report at that second, never zero: zero is a real
/// reading (a stopped fan) and the fault rules depend on telling the two
/// apart.
/// </summary>
public sealed record FanReading(string Name, double? Rpm);

/// <summary>
/// One fan's measured behaviour across a load test: its speed at idle
/// (before the burn started) and under load, and the peak temperature of
/// the component it cools. Any value may be null when the hardware did not
/// report it; a null is shown as "not reported", never guessed.
/// </summary>
public sealed record FanSummary(
    string Name,
    double? IdleRpm,
    double? LoadRpm,
    double? PeakRpm,
    double? LoadTempC)
{
    /// <summary>One line for the Tests page, e.g. "CPU Fan #1: idle 1,200 RPM, under load 2,400 RPM (peak 82 C)".</summary>
    public string DisplayText
    {
        get
        {
            var idle = IdleRpm is { } idleRpm
                ? FormattableString.Invariant($"idle {idleRpm:0} RPM")
                : "idle not reported";
            var load = LoadRpm is { } loadRpm
                ? FormattableString.Invariant($"under load {loadRpm:0} RPM")
                : "under load not reported";
            var temp = LoadTempC is { } tempC
                ? FormattableString.Invariant($" (peak {tempC:0.#} C)")
                : string.Empty;
            return FormattableString.Invariant($"{Name}: {idle}, {load}{temp}");
        }
    }
}

/// <summary>One measured fan fault, in plain English, naming the fan and the numbers behind it.</summary>
public sealed record FanHealthFinding(string FanName, string Message);

/// <summary>
/// The fan-health verdict for one load test. When no fan sensor reported,
/// <see cref="Fans"/> is empty and the summary says so instead of inventing
/// a fan. TrueSpec never controls fans: this result is a report only.
/// </summary>
public sealed record FanHealthResult(
    IReadOnlyList<FanSummary> Fans,
    IReadOnlyList<FanHealthFinding> Findings,
    string SummaryText)
{
    /// <summary>True when at least one fan sensor reported a speed.</summary>
    public bool HasFanSensors => Fans.Count > 0;

    /// <summary>True when at least one fault rule fired.</summary>
    public bool HasFault => Findings.Count > 0;
}

/// <summary>
/// Pure analysis of fan behaviour during the U2 load test (plan U5). No
/// hardware access, no clock, no I/O: the same samples always produce the
/// same verdict, which is what the tests pin down.
///
/// Two measured faults, one line each:
/// <list type="number">
/// <item>A fan reporting 0 RPM while the component it cools is hot.</item>
/// <item>A fan whose RPM does not rise when its component's temperature rises.</item>
/// </list>
/// A 0 RPM reading at idle is NOT a fault: many machines legitimately stop
/// their fans when cool (fan-stop mode), so rule 1 only fires when the
/// component temperature at that same second is at or above
/// <see cref="HotTemperatureC"/>.
/// </summary>
public static class FanHealthAnalyzer
{
    /// <summary>A component at or above this temperature is hot: a fan at 0 RPM here is a fault, not fan-stop mode.</summary>
    public const double HotTemperatureC = 70d;

    /// <summary>The temperature rise, in degrees C, that a fan is expected to answer with a higher RPM.</summary>
    public const double TempRiseC = 20d;

    /// <summary>An RPM rise of this much or less while the temperature climbs counts as "did not rise".</summary>
    public const double FlatRpmTolerance = 50d;

    public static FanHealthResult Analyze(
        IReadOnlyList<LoadTestSample> samples,
        IReadOnlyList<FanReading>? idleFans = null,
        double? idleCpuTempC = null,
        double? idleGpuTempC = null)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var ordered = samples.OrderBy(s => s.Timestamp).ToList();
        var idleByName = (idleFans ?? Array.Empty<FanReading>())
            .Where(f => f.Rpm.HasValue)
            .GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Rpm!.Value, StringComparer.OrdinalIgnoreCase);

        var names = new List<string>();
        foreach (var name in idleByName.Keys)
        {
            names.Add(name);
        }

        foreach (var sample in ordered)
        {
            foreach (var fan in sample.Fans ?? Array.Empty<FanReading>())
            {
                if (fan.Rpm.HasValue && !names.Contains(fan.Name, StringComparer.OrdinalIgnoreCase))
                {
                    names.Add(fan.Name);
                }
            }
        }

        if (names.Count == 0)
        {
            return new FanHealthResult(
                Array.Empty<FanSummary>(),
                Array.Empty<FanHealthFinding>(),
                "No fan sensors were reported by hardware, so fan health could not be checked.");
        }

        var summaries = new List<FanSummary>();
        var findings = new List<FanHealthFinding>();

        foreach (var name in names)
        {
            var isGpu = name.Contains("GPU", StringComparison.OrdinalIgnoreCase);
            var idleTemp = isGpu ? idleGpuTempC : idleCpuTempC;
            double? ComponentTemp(LoadTestSample sample) => isGpu ? sample.GpuTempC : sample.CpuTempC;

            var fanSamples = ordered
                .Select(s => (Sample: s, Reading: (s.Fans ?? Array.Empty<FanReading>())
                    .FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))))
                .Where(pair => pair.Reading?.Rpm.HasValue == true)
                .ToList();

            double? idleRpm = idleByName.TryGetValue(name, out var idle) ? idle : null;
            double? loadRpm = fanSamples.Count > 0
                ? fanSamples.Average(pair => pair.Reading!.Rpm!.Value)
                : null;
            double? peakRpm = fanSamples.Count > 0
                ? fanSamples.Max(pair => pair.Reading!.Rpm!.Value)
                : null;
            double? peakTemp = ordered
                .Where(s => ComponentTemp(s).HasValue)
                .Select(s => ComponentTemp(s)!.Value)
                .DefaultIfEmpty()
                .Max();
            var hasTemp = ordered.Any(s => ComponentTemp(s).HasValue);
            double? peakTempOrNull = hasTemp ? peakTemp : null;

            summaries.Add(new FanSummary(name, idleRpm, loadRpm, peakRpm, peakTempOrNull));

            // Rule 1: 0 RPM while hot. The temperature is the one from the
            // same sample, so a stopped fan at idle (cool) never fires.
            var stoppedHotTemps = ordered
                .Where(s => (s.Fans ?? Array.Empty<FanReading>())
                    .Any(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase) && f.Rpm == 0d))
                .Select(ComponentTemp)
                .Where(t => t is { } temp && temp >= HotTemperatureC)
                .Select(t => t!.Value)
                .ToList();
            if (stoppedHotTemps.Count > 0)
            {
                findings.Add(new FanHealthFinding(
                    name,
                    FormattableString.Invariant($"{name}: 0 RPM at {stoppedHotTemps.Max():0.#} C. This fan is not spinning.")));
                continue;
            }

            // Rule 2: RPM does not rise when temperature rises. The rise is
            // measured from the idle temperature (or the first sample's
            // temperature when no idle reading exists) to the peak, and the
            // RPM change from the idle speed (or the first sample's speed)
            // to the load average. A fan already flagged as stopped is not
            // flagged again here.
            var baselineTemp = idleTemp
                ?? ordered.Select(ComponentTemp).FirstOrDefault(t => t.HasValue);
            double? baselineRpm = idleRpm;
            if (baselineRpm is null && fanSamples.Count > 0)
            {
                baselineRpm = fanSamples[0].Reading!.Rpm!.Value;
            }
            if (baselineTemp is { } startTemp && peakTempOrNull is { } endTemp
                && loadRpm is { } load && baselineRpm is { } startRpm
                && load > 0d
                && endTemp - startTemp >= TempRiseC
                && load - startRpm <= FlatRpmTolerance)
            {
                findings.Add(new FanHealthFinding(
                    name,
                    FormattableString.Invariant($"{name}: RPM stayed near {startRpm:0} RPM while temperature rose from {startTemp:0.#} C to {endTemp:0.#} C. This fan is not speeding up under load.")));
            }
        }

        var summary = findings.Count == 0
            ? FormattableString.Invariant($"All {names.Count} reported fan(s) responded to load.")
            : FormattableString.Invariant($"Fan health: {findings.Count} problem(s) found. TrueSpec reports fan health only; it never controls fans.");
        return new FanHealthResult(summaries, findings, summary);
    }
}
