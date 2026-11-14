namespace TrueSpec.Core.History;

using System.Globalization;
using TrueSpec.Core.Health;
using TrueSpec.Core.Localization;
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
/// anything else stays null and the summary says so. <see cref="SnapshotIds"/>
/// names, in the same order as the points, the saved snapshot each point
/// came from, so a row in the History table can be traced back to the exact
/// scan it shows (and deleted without touching any other scan).
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
    string? DriverAgeSummary,
    IReadOnlyList<Guid> SnapshotIds)
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
        null,
        Array.Empty<Guid>());

    /// <summary>
    /// The empty trend with its summary in the given language (English when
    /// the code is blank or unknown). <see cref="Empty"/> is the English
    /// default, kept so callers that never pass a language see no change.
    /// </summary>
    public static HistoryTrend EmptyFor(string? languageCode) => languageCode is null
        ? Empty
        : Empty with
        {
            Summary = StringLocalizer.Get(AppStringKeys.HistoryTrendEmptySummary, languageCode),
        };
}

/// <summary>
/// Pure trend analysis over saved snapshots. No hardware access, no I/O:
/// the numbers come from the spec values the collectors already wrote, so a
/// trend can only ever show what was actually measured.
/// </summary>
public static class HistoryTrendAnalyzer
{
    /// <summary>
    /// Analyses saved snapshots into a trend. <paramref name="languageCode"/>
    /// (optional, English when blank or unknown) localizes the summary, the
    /// battery alert and the driver-age summary; measurements, dates and
    /// counts keep the app's existing formatting. Callers that never pass a
    /// language see the exact English wordings this method always produced.
    /// </summary>
    public static HistoryTrend Analyze(IReadOnlyList<SystemSnapshot> snapshots, string? languageCode = null)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        if (snapshots.Count == 0)
        {
            return HistoryTrend.EmptyFor(languageCode);
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
            BuildSummary(ordered, scorePoints, batteryHealthPoints, storageWearPoints, scoreDelta, batteryDelta, wearDelta, languageCode),
            BatteryHealthAlert(batteryHealthPoints, languageCode),
            driverAgePoints,
            driverAgeDelta,
            BuildDriverAgeSummary(driverAges, driverAgeDelta, languageCode),
            ordered.Select(s => s.Id).ToList());
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
    public static string? BatteryHealthAlert(IReadOnlyList<TrendPoint> batteryHealthPoints, string? languageCode = null)
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
            parts.Add(StringLocalizer.GetFormatted(
                AppStringKeys.HistoryBatteryAlertBandDrop,
                languageCode,
                BandNameFor(firstBand, languageCode),
                FormatValue(first),
                BandNameFor(latestBand, languageCode),
                FormatValue(latest)));
        }

        var fall = previous - latest;
        if (fall > 5d)
        {
            parts.Add(StringLocalizer.GetFormatted(
                AppStringKeys.HistoryBatteryAlertFastFall,
                languageCode,
                FormatValue(fall),
                FormatValue(previous),
                FormatValue(latest)));
        }

        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    /// <summary>
    /// The battery band name in the given language. The band names are used
    /// only inside the History alert sentence, so they live here rather than
    /// in <see cref="HardwareMath"/>: the Battery page keeps showing the
    /// English band text, which is a separate, untranslated surface.
    /// </summary>
    private static string BandNameFor(BatteryHealthBand band, string? languageCode) => band switch
    {
        BatteryHealthBand.Normal => StringLocalizer.Get(AppStringKeys.HistoryBatteryBandNormal, languageCode),
        BatteryHealthBand.Worn => StringLocalizer.Get(AppStringKeys.HistoryBatteryBandWorn, languageCode),
        BatteryHealthBand.Replace => StringLocalizer.Get(AppStringKeys.HistoryBatteryBandReplace, languageCode),
        _ => HardwareMath.BatteryBandName(null),
    };

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
    /// <paramref name="languageCode"/> localizes the trend wordings (see
    /// <see cref="Analyze"/>).
    /// </summary>
    public static async Task<HistoryTrend> LoadAsync(
        ISnapshotStore store,
        string machineName,
        int limit = 50,
        CancellationToken cancellationToken = default,
        string? languageCode = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(machineName);

        var snapshots = await LoadSnapshotsAsync(store, machineName, limit, cancellationToken);
        return Analyze(snapshots, languageCode);
    }

    /// <summary>
    /// Loads the saved snapshots for one machine (same skip rules as
    /// <see cref="LoadAsync"/>), so a caller that re-analyses on a language
    /// switch can keep the snapshots instead of re-reading the store.
    /// </summary>
    public static async Task<IReadOnlyList<SystemSnapshot>> LoadSnapshotsAsync(
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

        return snapshots;
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
        double? wearDelta,
        string? languageCode)
    {
        var firstDate = ordered[0].CapturedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var lastDate = ordered[^1].CapturedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var parts = new List<string>
        {
            ordered.Count == 1
                ? StringLocalizer.GetFormatted(AppStringKeys.HistoryTrendScansOne, languageCode, firstDate, lastDate)
                : StringLocalizer.GetFormatted(AppStringKeys.HistoryTrendScansMany, languageCode, ordered.Count, firstDate, lastDate),
        };
        parts.Add(DescribeDelta(StringLocalizer.Get(AppStringKeys.HistoryHeaderScore, languageCode), scorePoints, scoreDelta, languageCode));
        parts.Add(DescribeDelta(StringLocalizer.Get(AppStringKeys.HistoryBatteryTitle, languageCode), batteryPoints, batteryDelta, languageCode));
        parts.Add(DescribeDelta(StringLocalizer.Get(AppStringKeys.HistoryWearTitle, languageCode), wearPoints, wearDelta, languageCode));
        return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static string DescribeDelta(
        string label,
        IReadOnlyList<TrendPoint> points,
        double? delta,
        string? languageCode)
    {
        var measured = points.Where(p => p.Value is not null).ToList();
        if (measured.Count == 0)
        {
            return StringLocalizer.GetFormatted(AppStringKeys.HistoryTrendDeltaNotReported, languageCode, label);
        }

        if (measured.Count == 1 || delta is null)
        {
            return StringLocalizer.GetFormatted(
                AppStringKeys.HistoryTrendDeltaReportedOnce,
                languageCode,
                label,
                FormatValue(measured[0].Value!.Value));
        }

        var unit = StringLocalizer.Get(AppStringKeys.HistoryTrendPointsUnit, languageCode);
        var first = FormatValue(measured[0].Value!.Value);
        var last = FormatValue(measured[^1].Value!.Value);
        var magnitude = FormatValue(Math.Abs(delta.Value));
        return delta.Value switch
        {
            > 0.05 => StringLocalizer.GetFormatted(AppStringKeys.HistoryTrendDeltaUp, languageCode, label, first, last, magnitude, unit),
            < -0.05 => StringLocalizer.GetFormatted(AppStringKeys.HistoryTrendDeltaDown, languageCode, label, first, last, magnitude, unit),
            _ => StringLocalizer.GetFormatted(AppStringKeys.HistoryTrendDeltaUnchanged, languageCode, label, first, last, magnitude, unit),
        };
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
        double? driverAgeDelta,
        string? languageCode)
    {
        var measured = readings.Where(r => r.AgeDays is not null).ToList();
        if (measured.Count == 0)
        {
            return null;
        }

        var first = measured[0];
        var last = measured[^1];
        var firstDate = first.Date!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var firstAge = FormatValue(first.AgeDays!.Value);
        var firstSentence = measured.Count == 1
            ? StringLocalizer.GetFormatted(AppStringKeys.HistoryDriverAgeOnlyOne, languageCode, firstDate, firstAge)
            : StringLocalizer.GetFormatted(
                AppStringKeys.HistoryDriverAgeFirstLatest,
                languageCode,
                firstDate,
                firstAge,
                last.Date!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                FormatValue(last.AgeDays!.Value));

        if (measured.Count == 1)
        {
            if (first.OlderThan5Years is null)
            {
                return firstSentence;
            }

            return firstSentence + " " + StringLocalizer.GetFormatted(
                AppStringKeys.HistoryDriverAgeCountOnThatScan,
                languageCode,
                first.OlderThan5Years.Value);
        }

        var clauses = new List<string>();
        if (driverAgeDelta is not null)
        {
            clauses.Add(driverAgeDelta.Value switch
            {
                < -0.05 => StringLocalizer.GetFormatted(AppStringKeys.HistoryDriverAgeNewer, languageCode, FormatValue(Math.Abs(driverAgeDelta.Value))),
                > 0.05 => StringLocalizer.GetFormatted(AppStringKeys.HistoryDriverAgeOlder, languageCode, FormatValue(Math.Abs(driverAgeDelta.Value))),
                _ => StringLocalizer.Get(AppStringKeys.HistoryDriverAgeUnchanged, languageCode),
            });
        }

        var countClause = CountClause(first, last, languageCode);
        if (countClause is not null)
        {
            clauses.Add(countClause);
        }

        if (clauses.Count == 0)
        {
            return firstSentence;
        }

        var second = string.Join(StringLocalizer.Get(AppStringKeys.HistoryDriverAgeAndJoin, languageCode), clauses);
        second = char.ToUpperInvariant(second[0]) + second[1..] + ".";
        return firstSentence + " " + second;
    }

    private static string? CountClause(DriverAgeReading first, DriverAgeReading last, string? languageCode)
    {
        if (first.OlderThan5Years is not null && last.OlderThan5Years is not null)
        {
            return first.OlderThan5Years.Value == last.OlderThan5Years.Value
                ? StringLocalizer.GetFormatted(AppStringKeys.HistoryDriverAgeCountStayed, languageCode, first.OlderThan5Years.Value)
                : StringLocalizer.GetFormatted(AppStringKeys.HistoryDriverAgeCountWent, languageCode, first.OlderThan5Years.Value, last.OlderThan5Years.Value);
        }

        if (first.OlderThan5Years is not null)
        {
            return StringLocalizer.GetFormatted(AppStringKeys.HistoryDriverAgeCountFirst, languageCode, first.OlderThan5Years.Value);
        }

        if (last.OlderThan5Years is not null)
        {
            return StringLocalizer.GetFormatted(AppStringKeys.HistoryDriverAgeCountLatest, languageCode, last.OlderThan5Years.Value);
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
