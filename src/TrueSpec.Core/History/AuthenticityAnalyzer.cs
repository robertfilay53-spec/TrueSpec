namespace TrueSpec.Core.History;

using System.Globalization;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;

/// <summary>
/// U1 Authenticity engine (Blueprint 1): cross-checks the machine's age
/// signals against each other and reports contradictions as evidence, in
/// plain English. Pure functions over a <see cref="SystemSnapshot"/>: no
/// I/O, no network, no clock except the snapshot's own CapturedAt, so every
/// rule is unit-testable on any OS.
///
/// A fresh Windows install resets the install date but cannot reset the
/// SSD's power-on hours, the battery's cycle count, or the BIOS date. A
/// contradiction is evidence, not proof: the verdict says "signs of prior
/// use", never "this seller lied". A legitimately re-imaged corporate
/// machine fires R1/R5 by design, and the wording says so nowhere near an
/// accusation.
///
/// Missing signal = check skipped, never a false positive. Every rule that
/// cannot run is named in <see cref="AuthenticityResult.SkippedChecks"/>.
///
/// Spec labels consumed (exact labels, as written by the collectors):
/// system "Windows install date" (yyyy-MM-dd), "BIOS date" (yyyy-MM-dd),
/// "Earliest event log entry" (yyyy-MM-dd); storage "Power-on hours"
/// (integer hours) and "Data written" (decimal FormatBytes text such as
/// "650.0 GB", SATA SMART attribute 241 only; NVMe drives report
/// "Not reported by hardware" for total writes, so R2 is skipped for
/// them); battery "Cycle count" (integer); updates "Update - KB..."
/// specs, whose values start with the hotfix date (yyyy-MM-dd).
/// There is no single "earliest hotfix" spec: R5 takes the earliest date
/// across the per-hotfix specs.
/// </summary>
public static class AuthenticityAnalyzer
{
    /// <summary>Rule R1: SSD power-on hours vs Windows install date.</summary>
    public const string R1Title = "SSD power-on hours vs Windows install date";

    /// <summary>Rule R2: SSD total writes vs Windows install date.</summary>
    public const string R2Title = "SSD total writes vs Windows install date";

    /// <summary>Rule R3: battery cycles vs Windows install date.</summary>
    public const string R3Title = "Battery cycles vs Windows install date";

    /// <summary>Rule R4: BIOS date vs Windows install date.</summary>
    public const string R4Title = "BIOS date vs Windows install date";

    /// <summary>Rule R5: earliest hotfix vs Windows install date.</summary>
    public const string R5Title = "Earliest hotfix vs Windows install date";

    /// <summary>Rule R6: earliest event log entry vs Windows install date.</summary>
    public const string R6Title = "Earliest event log entry vs Windows install date";

    /// <summary>
    /// R1 tolerance: a drive may legitimately run a few days before Windows
    /// is installed (imaging, burn-in, the install itself). Hours up to the
    /// Windows age plus this tolerance pass.
    /// </summary>
    public const int SsdHoursToleranceHours = 72;

    /// <summary>
    /// R2/R3 freshness window: an install younger than this is "fresh", so
    /// hundreds of gigabytes written or dozens of battery cycles cannot be
    /// explained by normal use since the install.
    /// </summary>
    public const int FreshInstallDays = 30;

    /// <summary>
    /// R2 threshold: a fresh install writes tens of GB, not hundreds. The
    /// threshold is a constant, documented, and tunable.
    /// </summary>
    public const double FreshInstallWriteThresholdGb = 500;

    /// <summary>R3 threshold: cycles above this on a fresh install.</summary>
    public const int BatteryCycleThreshold = 50;

    /// <summary>
    /// R6 threshold, in days: the earliest event log entry may lag the
    /// install by up to this long (log rotation, first boot delay). A larger
    /// gap means the log was cleared or this is not the first install.
    /// Public so the crash-forensics work (U3) can share the same threshold
    /// after merge instead of owning a second copy.
    /// </summary>
    public const int LogGapDays = 7;

    /// <summary>The R6 threshold as a <see cref="TimeSpan"/>.</summary>
    public static readonly TimeSpan LogGapThreshold = TimeSpan.FromDays(LogGapDays);

    private const string NotReported = "Not reported by hardware";
    private const double BytesPerGb = 1_000_000_000d;

    public static AuthenticityResult Analyze(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var now = snapshot.CapturedAt;
        var byId = snapshot.Components
            .GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var checks = new List<AuthenticityCheck>();
        var skipped = new List<string>();
        var signals = new List<AuthenticitySignal>();

        var install = ReadDate(byId, "system", "Windows install date", signals, now);
        var bios = ReadDate(byId, "system", "BIOS date", signals, now);
        var earliestLog = ReadDate(byId, "system", "Earliest event log entry", signals, now);
        var powerOnHours = ReadULong(byId, "storage", "Power-on hours", signals, now);
        var dataWrittenGb = ReadWrittenGb(byId, "storage", "Data written", signals, now);
        var cycles = ReadULong(byId, "battery", "Cycle count", signals, now);
        var earliestHotfix = ReadEarliestHotfix(byId, signals, now);

        if (install is null)
        {
            foreach (var title in new[] { R1Title, R2Title, R3Title, R4Title, R5Title, R6Title })
            {
                skipped.Add($"{title}: skipped, Windows install date was not reported.");
            }

            return BuildResult(checks, skipped, signals);
        }

        var installAge = now - install.Value;

        // R1: SSD hours vs Windows age.
        if (powerOnHours is null)
        {
            skipped.Add($"{R1Title}: skipped, SSD power-on hours were not reported.");
        }
        else if (powerOnHours.Value > installAge.TotalHours + SsdHoursToleranceHours)
        {
            checks.Add(new AuthenticityCheck(
                R1Title,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SSD has {powerOnHours.Value:N0} power-on hours but Windows was installed {FormatAge(installAge)}. This machine was used before this Windows install."),
                AuthenticitySeverity.Strong,
                SourceOf(byId, "storage", "Power-on hours"),
                SourceOf(byId, "system", "Windows install date")));
        }

        // R2: total host writes vs Windows age.
        if (dataWrittenGb is null)
        {
            skipped.Add($"{R2Title}: skipped, SSD total writes were not reported.");
        }
        else if (installAge < TimeSpan.FromDays(FreshInstallDays) &&
                 dataWrittenGb.Value > FreshInstallWriteThresholdGb)
        {
            checks.Add(new AuthenticityCheck(
                R2Title,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"SSD has {dataWrittenGb.Value:0.#} GB total writes but Windows was installed {FormatAge(installAge)}. A fresh install writes tens of gigabytes, not hundreds. This machine was used before this Windows install."),
                AuthenticitySeverity.Watch,
                SourceOf(byId, "storage", "Data written"),
                SourceOf(byId, "system", "Windows install date")));
        }

        // R3: battery cycles vs Windows age.
        if (cycles is null)
        {
            skipped.Add($"{R3Title}: skipped, battery cycle count was not reported.");
        }
        else if (installAge < TimeSpan.FromDays(FreshInstallDays) &&
                 cycles.Value > BatteryCycleThreshold)
        {
            checks.Add(new AuthenticityCheck(
                R3Title,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Battery has {cycles.Value:N0} charge cycles but Windows was installed {FormatAge(installAge)}. This machine was used before this Windows install."),
                AuthenticitySeverity.Watch,
                SourceOf(byId, "battery", "Cycle count"),
                SourceOf(byId, "system", "Windows install date")));
        }

        // R4: BIOS date after the install date is impossible data.
        if (bios is null)
        {
            skipped.Add($"{R4Title}: skipped, BIOS date was not reported.");
        }
        else if (bios.Value > install.Value)
        {
            MarkUnreliable(signals, "BIOS date");
            checks.Add(new AuthenticityCheck(
                R4Title,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"BIOS date ({FormatDay(bios.Value)}) is after the Windows install date ({FormatDay(install.Value)}). That is impossible data: either the clock was wrong or the firmware date was changed. The BIOS date is not trusted."),
                AuthenticitySeverity.Strong,
                SourceOf(byId, "system", "BIOS date"),
                SourceOf(byId, "system", "Windows install date")));
        }

        // R5: earliest hotfix before the install date.
        if (earliestHotfix is null)
        {
            skipped.Add($"{R5Title}: skipped, no dated installed hotfix was reported.");
        }
        else if (earliestHotfix.Value < install.Value)
        {
            checks.Add(new AuthenticityCheck(
                R5Title,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The earliest installed update ({FormatDay(earliestHotfix.Value)}) is older than the Windows install date ({FormatDay(install.Value)}). This Windows install is not the machine's first install."),
                AuthenticitySeverity.Info,
                "CIM Win32_QuickFixEngineering",
                SourceOf(byId, "system", "Windows install date")));
        }

        // R6: event-log gap.
        if (earliestLog is null)
        {
            skipped.Add($"{R6Title}: skipped, earliest event log entry was not reported.");
        }
        else if (earliestLog.Value - install.Value > LogGapThreshold)
        {
            var gapDays = (int)Math.Floor((earliestLog.Value - install.Value).TotalDays);
            checks.Add(new AuthenticityCheck(
                R6Title,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The earliest event log entry is {FormatDay(earliestLog.Value)}, {gapDays} days after Windows was installed. The log may have been cleared, or this is not the machine's first install."),
                AuthenticitySeverity.Watch,
                SourceOf(byId, "system", "Earliest event log entry"),
                SourceOf(byId, "system", "Windows install date")));
        }

        return BuildResult(checks, skipped, signals);
    }

    private static AuthenticityResult BuildResult(
        List<AuthenticityCheck> checks,
        List<string> skipped,
        List<AuthenticitySignal> signals)
    {
        var hasSigns = checks.Any(c => c.Severity is AuthenticitySeverity.Strong or AuthenticitySeverity.Watch);
        var verdict = hasSigns
            ? AuthenticityResult.SignsOfPriorUse
            : AuthenticityResult.NoSignsOfPriorUse;

        string summary;
        if (hasSigns)
        {
            var strongest = checks
                .OrderByDescending(c => c.Severity)
                .First();
            summary = strongest.EvidenceLine;
        }
        else if (checks.Count > 0)
        {
            summary = "No signs of prior use were found in the age signals that could be checked. " +
                "One informational note was recorded: " + checks[0].EvidenceLine;
        }
        else
        {
            summary = "No signs of prior use were found in the age signals that could be checked.";
        }

        if (skipped.Count > 0)
        {
            summary += string.Create(
                CultureInfo.InvariantCulture,
                $" {skipped.Count} check(s) could not run because a signal was not reported.");
        }

        return new AuthenticityResult(checks, skipped, signals, verdict, summary);
    }

    private static void MarkUnreliable(List<AuthenticitySignal> signals, string name)
    {
        for (var i = 0; i < signals.Count; i++)
        {
            if (string.Equals(signals[i].Name, name, StringComparison.Ordinal))
            {
                signals[i] = signals[i] with { IsReliable = false };
            }
        }
    }

    private static string FormatAge(TimeSpan age)
    {
        var days = (int)Math.Floor(age.TotalDays);
        return days switch
        {
            0 => "today",
            1 => "1 day ago",
            _ => string.Create(CultureInfo.InvariantCulture, $"{days} days ago"),
        };
    }

    private static string FormatDay(DateTimeOffset date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string SourceOf(
        IReadOnlyDictionary<string, ComponentSnapshot> byId,
        string componentId,
        string label) =>
        FindSpec(byId, componentId, label)?.Source ?? "Not reported";

    private static SpecValue? FindSpec(
        IReadOnlyDictionary<string, ComponentSnapshot> byId,
        string componentId,
        string label)
    {
        if (!byId.TryGetValue(componentId, out var component))
        {
            return null;
        }

        // Exact label first (the primary drive is unprefixed); multi-drive
        // snapshots prefix extra drives ("Drive 2 - Power-on hours"), so
        // fall back to the first suffixed match in spec order.
        return component.Specs.FirstOrDefault(s =>
            string.Equals(s.Label, label, StringComparison.OrdinalIgnoreCase))
            ?? component.Specs.FirstOrDefault(s =>
                s.Label.EndsWith(" - " + label, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMissing(string? value) =>
        string.IsNullOrWhiteSpace(value) ||
        string.Equals(value.Trim(), NotReported, StringComparison.OrdinalIgnoreCase);

    private static DateTimeOffset? ReadDate(
        IReadOnlyDictionary<string, ComponentSnapshot> byId,
        string componentId,
        string label,
        List<AuthenticitySignal> signals,
        DateTimeOffset collectedAt)
    {
        var spec = FindSpec(byId, componentId, label);
        if (spec is null || IsMissing(spec.Value))
        {
            return null;
        }

        var parsed = TryParseDate(spec.Value.Trim());
        if (parsed is null)
        {
            return null;
        }

        signals.Add(new AuthenticitySignal(label, spec.Value.Trim(), spec.Source, collectedAt));
        return parsed;
    }

    private static ulong? ReadULong(
        IReadOnlyDictionary<string, ComponentSnapshot> byId,
        string componentId,
        string label,
        List<AuthenticitySignal> signals,
        DateTimeOffset collectedAt)
    {
        var spec = FindSpec(byId, componentId, label);
        if (spec is null || IsMissing(spec.Value))
        {
            return null;
        }

        if (!ulong.TryParse(spec.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return null;
        }

        signals.Add(new AuthenticitySignal(label, spec.Value.Trim(), spec.Source, collectedAt));
        return parsed;
    }

    private static double? ReadWrittenGb(
        IReadOnlyDictionary<string, ComponentSnapshot> byId,
        string componentId,
        string label,
        List<AuthenticitySignal> signals,
        DateTimeOffset collectedAt)
    {
        var spec = FindSpec(byId, componentId, label);
        if (spec is null || IsMissing(spec.Value))
        {
            return null;
        }

        var bytes = TryParseDecimalBytes(spec.Value.Trim());
        if (bytes is null)
        {
            return null;
        }

        signals.Add(new AuthenticitySignal(label, spec.Value.Trim(), spec.Source, collectedAt));
        return bytes.Value / BytesPerGb;
    }

    private static DateTimeOffset? ReadEarliestHotfix(
        IReadOnlyDictionary<string, ComponentSnapshot> byId,
        List<AuthenticitySignal> signals,
        DateTimeOffset collectedAt)
    {
        if (!byId.TryGetValue("updates", out var component))
        {
            return null;
        }

        DateTimeOffset? earliest = null;
        string? source = null;
        foreach (var spec in component.Specs)
        {
            if (!spec.Label.StartsWith("Update - ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsMissing(spec.Value))
            {
                continue;
            }

            // Value is "yyyy-MM-dd / description", a bare date, or a
            // description with no date ("Installed date not reported").
            // Only the leading date counts.
            var text = spec.Value.Trim();
            var dateText = text.Length >= 10 ? text[..10] : text;
            var parsed = TryParseDate(dateText);
            if (parsed is null)
            {
                continue;
            }

            if (earliest is null || parsed.Value < earliest.Value)
            {
                earliest = parsed;
                source = spec.Source;
            }
        }

        if (earliest is null)
        {
            return null;
        }

        signals.Add(new AuthenticitySignal(
            "Earliest hotfix",
            FormatDay(earliest.Value),
            source ?? "CIM Win32_QuickFixEngineering",
            collectedAt));
        return earliest;
    }

    private static DateTimeOffset? TryParseDate(string text)
    {
        if (DateTimeOffset.TryParseExact(
                text,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var exact))
        {
            return exact;
        }

        return DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// Parses the decimal sizes <c>CollectorSupport.FormatBytes</c> writes:
    /// "1.25 TB", "650.0 GB", "40.0 MB", "123 bytes". Decimal (1000-based),
    /// matching the writer.
    /// </summary>
    private static double? TryParseDecimalBytes(string text)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return null;
        }

        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return null;
        }

        var multiplier = parts[1].ToUpperInvariant() switch
        {
            "TB" => 1_000_000_000_000d,
            "GB" => 1_000_000_000d,
            "MB" => 1_000_000d,
            "KB" => 1_000d,
            "BYTES" => 1d,
            _ => (double?)null,
        };

        return multiplier is null ? null : number * multiplier.Value;
    }
}
