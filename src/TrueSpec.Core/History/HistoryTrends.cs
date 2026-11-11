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
    string? BatteryAlert)
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

        var scoreDelta = Delta(scorePoints);
        var batteryDelta = Delta(batteryHealthPoints);
        var wearDelta = Delta(storageWearPoints);

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
            BatteryHealthAlert(batteryHealthPoints));
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
    // parse as a number (for example "Not reported by hardware"), is a gap
    // in the trend, never a zero.

    private static double? ReadBatteryHealth(SystemSnapshot snapshot) =>
        ParseNumber(FindSpec(snapshot, "battery", "Health"));

    private static double? ReadBatteryCycles(SystemSnapshot snapshot) =>
        ParseNumber(FindSpec(snapshot, "battery", "Cycle count"));

    private static double? ReadStorageWear(SystemSnapshot snapshot) =>
        ParseNumber(FindSpec(snapshot, "storage", "Wear level"));

    private static double? ReadStoragePowerOnHours(SystemSnapshot snapshot) =>
        ParseNumber(FindSpec(snapshot, "storage", "Power-on hours"));

    private static string? FindSpec(SystemSnapshot snapshot, string componentId, string label) =>
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
