namespace TrueSpec.Core.History;

using System.Globalization;
using TrueSpec.Core.Health;
using TrueSpec.Core.Models;
using TrueSpec.Core.Storage;

/// <summary>
/// One measured point on a trend line. <see cref="Value"/> is null when the
/// snapshot did not report that measurement, so a gap in the line is a gap in
/// the data, never a zero.
/// </summary>
public sealed record TrendPoint(DateTimeOffset CapturedAt, double? Value);

/// <summary>
/// Score, battery-health and storage-wear trends across saved scans. Deltas
/// are only computed when at least two scans actually reported the value;
/// anything else stays null and the summary says so.
/// </summary>
public sealed record HistoryTrend(
    int SnapshotCount,
    DateTimeOffset? FirstScan,
    DateTimeOffset? LastScan,
    IReadOnlyList<TrendPoint> ScorePoints,
    IReadOnlyList<TrendPoint> BatteryHealthPoints,
    IReadOnlyList<TrendPoint> BatteryCyclePoints,
    IReadOnlyList<TrendPoint> StorageWearPoints,
    IReadOnlyList<TrendPoint> StoragePowerOnHoursPoints,
    double? ScoreDelta,
    double? BatteryHealthDelta,
    double? StorageWearDelta,
    string Summary,
    string? BatteryAlert,
    IReadOnlyList<TrendPoint> DriverAgePoints,
    double? DriverAgeDelta,
    string? DriverAgeSummary)
{
    public static HistoryTrend Empty { get; } = new(
        0,
        null,
        null,
        Array.Empty<TrendPoint>(),
        Array.Empty<TrendPoint>(),
        Array.Empty<TrendPoint>(),
        Array.Empty<TrendPoint>(),
        Array.Empty<TrendPoint>(),
        null,
        null,
        null,
        "No scans saved yet. Run a scan and come back: every scan is saved to local history, and the trend appears once there are two scans to compare.",
        null,
        Array.Empty<TrendPoint>(),
        null,
        null);
}

/// <summary>
/// Pure trend analysis over saved snapshots. No hardware access, no I/O:
/// the numbers come from the spec values the collectors already wrote, so a
/// trend can only ever show what was actually measured.
/// </summary>
public static class HistoryTrendAnalyzer
{
    public static HistoryTrend Analyze(IReadOnlyList<SystemSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        if (snapshots.Count == 0)
        {
            return HistoryTrend.Empty;
        }

        var machineName = snapshots[0].MachineName;
        if (snapshots.Any(s => !string.Equals(s.MachineName, machineName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Snapshots must be from the same machine to trend.", nameof(snapshots));
        }

        var ordered = snapshots.OrderBy(s => s.CapturedAt).ToList();

        var scorePoints = ordered
            .Select(s => new TrendPoint(
                s.CapturedAt,
                s.Score.HasMeasurableComponents ? s.Score.OverallScore : null))
            .ToList();
        var batteryHealthPoints = ordered
            .Select(s => new TrendPoint(s.CapturedAt, ReadBatteryHealth(s)))
            .ToList();
        var batteryCyclePoints = ordered
            .Select(s => new TrendPoint(s.CapturedAt, ReadBatteryCycles(s)))
            .ToList();
        var storageWearPoints = ordered
            .Select(s => new TrendPoint(s.CapturedAt, ReadStorageWear(s)))
            .ToList();
        var storageHoursPoints = ordered
            .Select(s => new TrendPoint(s.CapturedAt, ReadStoragePowerOnHours(s)))
            .ToList();
        var driverAges = ordered.Select(ReadOldestDriverAge).ToList();
        var driverAgePoints = ordered
            .Select((s, i) => new TrendPoint(s.CapturedAt, driverAges[i].AgeDays))
            .ToList();

        var scoreDelta = Delta(scorePoints);
        var batteryDelta = Delta(batteryHealthPoints);
        var wearDelta = Delta(storageWearPoints);
        var driverAgeDelta = Delta(driverAgePoints);

        return new HistoryTrend(
            ordered.Count,
            ordered[0].CapturedAt,
            ordered[^1].CapturedAt,
            scorePoints,
            batteryHealthPoints,
            batteryCyclePoints,
            storageWearPoints,
            storageHoursPoints,
            scoreDelta,
            batteryDelta,
            wearDelta,
            BuildSummary(ordered, scorePoints, batteryHealthPoints, storageWearPoints, scoreDelta, batteryDelta, wearDelta),
            BatteryHealthAlert(batteryHealthPoints),
            driverAgePoints,
            driverAgeDelta,
            BuildDriverAgeSummary(driverAges, driverAgeDelta));
    }

    /// <summary>
    /// Battery-health alerts from the trend data. Two rules, both aligned
    /// with the battery bands in <see cref="HardwareMath"/> (Normal 80-100%,
    /// Worn 60-79%, Replace below 60%):
    /// <list type="bullet">
    /// <item>
    /// <description>Band drop: the latest measured health sits in a worse
    /// band than the first measured health, so the battery crossed a band
    /// boundary downward.</description>
    /// </item>
    /// <item>
    /// <description>Fast fall: the latest measured health is more than 5
    /// points below the previous measured health. Normal scan-to-scan
    /// variation is under a point, so a drop over 5 points in one interval
    /// means the battery fell faster than the previous scans' pace.
    /// </description>
    /// </item>
    /// </list>
    /// Returns null when there is nothing to say: fewer than two measured
    /// values, no band change, and no fast fall. A battery that was already
    /// in the Replace band on the first scan does not alert here; the Battery
    /// page already names that band. Gaps (scans that did not report health)
    /// are skipped, never treated as zero.
    /// </summary>
    public static string? BatteryHealthAlert(IReadOnlyList<TrendPoint> batteryHealthPoints)
    {
        ArgumentNullException.ThrowIfNull(batteryHealthPoints);

        var measured = batteryHealthPoints
            .Where(p => p.Value is not null)
            .Select(p => p.Value!.Value)
            .ToList();
        if (measured.Count < 2)
        {
            return null;
        }

        var first = measured[0];
        var previous = measured[^2];
        var latest = measured[^1];

        var parts = new List<string>();
        var firstBand = HardwareMath.BatteryBand(first);
        var latestBand = HardwareMath.BatteryBand(latest);
        if (BandRank(latestBand) > BandRank(firstBand))
        {
            parts.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"Battery health dropped from {HardwareMath.BatteryBandName(first)} ({first:0.#}%) to {HardwareMath.BatteryBandName(latest)} ({latest:0.#}%) since the first saved scan."));
        }

        var fall = previous - latest;
        if (fall > 5d)
        {
            parts.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"Battery health fell {fall:0.#} points between the last two scans that reported it ({previous:0.#}% to {latest:0.#}%)."));
        }

        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    private static int BandRank(BatteryHealthBand band) => band switch
    {
        BatteryHealthBand.Normal => 0,
        BatteryHealthBand.Worn => 1,
        BatteryHealthBand.Replace => 2,
        _ => -1,
    };

    /// <summary>
    /// Loads saved snapshots for one machine from the history store and
    /// trends them. Snapshots that cannot be read, or that belong to a
    /// different machine, are skipped instead of breaking the trend.
    /// </summary>
    public static async Task<HistoryTrend> LoadAsync(
        ISnapshotStore store,
        string machineName,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(machineName);

        var summaries = await store.GetHistoryAsync(limit, cancellationToken);
        var snapshots = new List<SystemSnapshot>();
        foreach (var summary in summaries)
        {
            var snapshot = await store.GetSnapshotAsync(summary.Id, cancellationToken);
            if (snapshot is null)
            {
                continue;
            }

            if (!string.Equals(snapshot.MachineName, machineName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            snapshots.Add(snapshot);
        }

        return Analyze(snapshots);
    }

    private static double? Delta(IReadOnlyList<TrendPoint> points)
    {
        var measured = points.Where(p => p.Value is not null).ToList();
        if (measured.Count < 2)
        {
            return null;
        }

        return Math.Round(measured[^1].Value!.Value - measured[0].Value!.Value, 1);
    }

    private static string BuildSummary(
        IReadOnlyList<SystemSnapshot> ordered,
        IReadOnlyList<TrendPoint> scorePoints,
        IReadOnlyList<TrendPoint> batteryPoints,
        IReadOnlyList<TrendPoint> wearPoints,
        double? scoreDelta,
        double? batteryDelta,
        double? wearDelta)
    {
        var parts = new List<string>
        {
            $"{ordered.Count} scan{(ordered.Count == 1 ? string.Empty : "s")} from {ordered[0].CapturedAt:yyyy-MM-dd} to {ordered[^1].CapturedAt:yyyy-MM-dd}.",
        };
        parts.Add(DescribeDelta("Score", scorePoints, scoreDelta, "points"));
        parts.Add(DescribeDelta("Battery health", batteryPoints, batteryDelta, "points"));
        parts.Add(DescribeDelta("Storage wear", wearPoints, wearDelta, "points"));
        return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static string DescribeDelta(
        string label,
        IReadOnlyList<TrendPoint> points,
        double? delta,
        string unit)
    {
        var measured = points.Where(p => p.Value is not null).ToList();
        if (measured.Count == 0)
        {
            return $"{label} was not reported in any saved scan.";
        }

        if (measured.Count == 1 || delta is null)
        {
            return $"{label} reported once ({FormatValue(measured[0].Value!.Value)}): one more scan is needed for a trend.";
        }

        var direction = delta.Value switch
        {
            > 0.05 => "up",
            < -0.05 => "down",
            _ => "unchanged",
        };
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{label} {FormatValue(measured[0].Value!.Value)} to {FormatValue(measured[^1].Value!.Value)} ({direction} {Math.Abs(delta.Value):0.#} {unit}).");
    }

    private static string FormatValue(double value) =>
        string.Create(CultureInfo.InvariantCulture, $"{value:0.#}");

    // The trend reads the spec values the collectors wrote, by the exact
    // labels they use. A label that is missing, or a value that does not
    // parse as a number (for example SpecLabels.NotReportedByHardware), is a gap
    // in the trend, never a zero.

    private static double? ReadBatteryHealth(SystemSnapshot snapshot) =>
        ParseNumber(FindSpec(snapshot, "battery", "Health"));

    private static double? ReadBatteryCycles(SystemSnapshot snapshot) =>
        ParseNumber(FindSpec(snapshot, "battery", "Cycle count"));

    private static double? ReadStorageWear(SystemSnapshot snapshot) =>
        ParseNumber(FindSpec(snapshot, "storage", "Wear level"));

    private static double? ReadStoragePowerOnHours(SystemSnapshot snapshot) =>
        ParseNumber(FindSpec(snapshot, "storage", "Power-on hours"));

    /// <summary>
    /// The oldest driver date a scan reported ("drivers" / "Oldest driver
    /// date", yyyy-MM-dd), or null when the component did not report one or
    /// the value does not parse. No future check here: the trend applies it
    /// when it turns the date into an age, while the machine comparer
    /// reports the date as written.
    /// </summary>
    internal static DateTime? ReadOldestDriverDate(SystemSnapshot snapshot)
    {
        var text = FindSpec(snapshot, "drivers", "Oldest driver date");
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return DateTime.TryParseExact(
            text.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? date
            : null;
    }

    /// <summary>
    /// One scan's oldest-driver reading: the date as written, the age in
    /// whole days at scan time, and the older-than-5-years count when the
    /// scan reported it. Age is whole days because the driver date itself
    /// is day-precision. A missing, unparseable or future date is a gap:
    /// the date and the age are both null, never zero.
    /// </summary>
    private sealed record DriverAgeReading(DateTime? Date, double? AgeDays, int? OlderThan5Years);

    private static DriverAgeReading ReadOldestDriverAge(SystemSnapshot snapshot)
    {
        var date = ReadOldestDriverDate(snapshot);
        var count = ReadOldDriversCount(snapshot);

        // A date in the future relative to the scan cannot be an age.
        if (date is null || date.Value.Date > snapshot.CapturedAt.Date)
        {
            return new DriverAgeReading(null, null, count);
        }

        return new DriverAgeReading(
            date,
            (snapshot.CapturedAt.Date - date.Value.Date).TotalDays,
            count);
    }

    private static int? ReadOldDriversCount(SystemSnapshot snapshot)
    {
        var text = FindSpec(snapshot, "drivers", "Drivers older than 5 years");
        return int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count >= 0
            ? count
            : null;
    }

    /// <summary>
    /// One or two plain sentences about the oldest driver, in the same voice
    /// as <see cref="BuildSummary"/>: the oldest-driver date first vs
    /// latest with its age, then whether the oldest driver got newer or
    /// older and how the older-than-5-years count moved when it was
    /// measured. Null when no scan reported an oldest driver date.
    /// </summary>
    private static string? BuildDriverAgeSummary(
        IReadOnlyList<DriverAgeReading> readings,
        double? driverAgeDelta)
    {
        var measured = readings.Where(r => r.AgeDays is not null).ToList();
        if (measured.Count == 0)
        {
            return null;
        }

        var first = measured[0];
        var last = measured[^1];
        var firstSentence = measured.Count == 1
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"Oldest driver dated {first.Date!.Value:yyyy-MM-dd} ({first.AgeDays!.Value:0.#} days old) on the only saved scan that reported it: one more scan is needed for a trend.")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"Oldest driver dated {first.Date!.Value:yyyy-MM-dd} ({first.AgeDays!.Value:0.#} days old) on the first saved scan and {last.Date!.Value:yyyy-MM-dd} ({last.AgeDays!.Value:0.#} days old) on the latest scan.");

        if (measured.Count == 1)
        {
            if (first.OlderThan5Years is null)
            {
                return firstSentence;
            }

            return firstSentence + string.Create(
                CultureInfo.InvariantCulture,
                $" Drivers older than 5 years: {first.OlderThan5Years.Value} on that scan.");
        }

        var clauses = new List<string>();
        if (driverAgeDelta is not null)
        {
            clauses.Add(driverAgeDelta.Value switch
            {
                < -0.05 => string.Create(CultureInfo.InvariantCulture, $"the oldest driver got {Math.Abs(driverAgeDelta.Value):0.#} days newer"),
                > 0.05 => string.Create(CultureInfo.InvariantCulture, $"the oldest driver got {Math.Abs(driverAgeDelta.Value):0.#} days older"),
                _ => "the oldest driver is unchanged since the first saved scan",
            });
        }

        var countClause = CountClause(first, last);
        if (countClause is not null)
        {
            clauses.Add(countClause);
        }

        if (clauses.Count == 0)
        {
            return firstSentence;
        }

        var second = string.Join(", and ", clauses);
        second = char.ToUpperInvariant(second[0]) + second[1..] + ".";
        return firstSentence + " " + second;
    }

    private static string? CountClause(DriverAgeReading first, DriverAgeReading last)
    {
        if (first.OlderThan5Years is not null && last.OlderThan5Years is not null)
        {
            return first.OlderThan5Years.Value == last.OlderThan5Years.Value
                ? string.Create(CultureInfo.InvariantCulture, $"drivers older than 5 years stayed at {first.OlderThan5Years.Value}")
                : string.Create(CultureInfo.InvariantCulture, $"drivers older than 5 years went from {first.OlderThan5Years.Value} to {last.OlderThan5Years.Value}");
        }

        if (first.OlderThan5Years is not null)
        {
            return string.Create(CultureInfo.InvariantCulture, $"drivers older than 5 years were {first.OlderThan5Years.Value} on the first saved scan");
        }

        if (last.OlderThan5Years is not null)
        {
            return string.Create(CultureInfo.InvariantCulture, $"drivers older than 5 years were {last.OlderThan5Years.Value} on the latest scan");
        }

        return null;
    }

    // Shared with MachineHistoryComparer, which reads the same spec labels
    // from one machine's latest scan: the parsing rules stay in one place.
    internal static string? FindSpec(SystemSnapshot snapshot, string componentId, string label) =>
        snapshot.Find(componentId)?.Specs
            .FirstOrDefault(s => string.Equals(s.Label, label, StringComparison.OrdinalIgnoreCase))
            ?.Value;

    internal static double? ParseNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim().TrimEnd('%').Trim();
        // Thousands separators and unit suffixes ("45,000 mWh") are not a
        // trend value: only a plain number parses.
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ||
            !double.IsFinite(parsed) || parsed < 0d)
        {
            return null;
        }

        return parsed;
    }
}
