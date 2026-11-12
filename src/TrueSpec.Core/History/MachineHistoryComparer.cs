namespace TrueSpec.Core.History;

using System.Globalization;
using TrueSpec.Core.Models;

/// <summary>
/// One machine's side of a two-machine history comparison: facts only.
/// Every "latest" value comes from that machine's snapshot with the latest
/// CapturedAt; a value the latest scan did not report is null, never an
/// older scan's value and never a zero. The stored driver and update dates
/// carry no offset, so they are read as UTC midnight.
/// </summary>
public sealed record MachineSideSummary(
    string MachineName,
    int ScanCount,
    DateTimeOffset? FirstScan,
    DateTimeOffset? LastScan,
    double? LatestScore,
    double? LatestBatteryHealth,
    double? LatestStorageWear,
    DateTimeOffset? LatestOldestDriverDate,
    DateTimeOffset? LastUpdateInstall);

/// <summary>
/// Two already-summarized machines, side by side. No verdict, no "which is
/// better": the History page shows the two fact columns and lets the reader
/// compare.
/// </summary>
public sealed record MachineComparison(
    MachineSideSummary MachineA,
    MachineSideSummary MachineB);

/// <summary>
/// Multi-machine history, pure functions over saved snapshots. No hardware
/// access, no I/O. Each call reads one machine's list at a time and refuses
/// a mixed list, so the two sides of a comparison can never be crossed.
/// </summary>
public static class MachineHistoryComparer
{
    public static MachineSideSummary Summarize(IReadOnlyList<SystemSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        if (snapshots.Count == 0)
        {
            return new MachineSideSummary(string.Empty, 0, null, null, null, null, null, null, null);
        }

        var machineName = snapshots[0].MachineName;
        if (snapshots.Any(s => !string.Equals(s.MachineName, machineName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Snapshots must be from the same machine to summarize.", nameof(snapshots));
        }

        var ordered = snapshots.OrderBy(s => s.CapturedAt).ToList();
        var latest = ordered[^1];

        return new MachineSideSummary(
            machineName,
            ordered.Count,
            ordered[0].CapturedAt,
            ordered[^1].CapturedAt,
            latest.Score.HasMeasurableComponents ? latest.Score.OverallScore : null,
            HistoryTrendAnalyzer.ParseNumber(HistoryTrendAnalyzer.FindSpec(latest, "battery", "Health")),
            HistoryTrendAnalyzer.ParseNumber(HistoryTrendAnalyzer.FindSpec(latest, "storage", "Wear level")),
            ReadDateAsUtc(HistoryTrendAnalyzer.ReadOldestDriverDate(latest)),
            ReadDateAsUtc(ReadLastUpdateInstall(latest)));
    }

    public static MachineComparison Compare(
        IReadOnlyList<SystemSnapshot> machineA,
        IReadOnlyList<SystemSnapshot> machineB)
    {
        ArgumentNullException.ThrowIfNull(machineA);
        ArgumentNullException.ThrowIfNull(machineB);

        // Each side is summarized on its own: Summarize refuses a mixed
        // list, so a snapshot can never leak from one side to the other.
        return new MachineComparison(Summarize(machineA), Summarize(machineB));
    }

    /// <summary>
    /// "updates" / "Last successful update install" ("yyyy-MM-dd HH:mm"),
    /// or null when the component did not report it or the value does not
    /// parse. Only an unparseable value is null here: unlike the driver-age
    /// trend, no future check applies to a reported install time.
    /// </summary>
    private static DateTime? ReadLastUpdateInstall(SystemSnapshot snapshot)
    {
        var text = HistoryTrendAnalyzer.FindSpec(snapshot, "updates", "Last successful update install");
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return DateTime.TryParseExact(
            text.Trim(),
            "yyyy-MM-dd HH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;
    }

    private static DateTimeOffset? ReadDateAsUtc(DateTime? date) =>
        date is null ? null : new DateTimeOffset(date.Value, TimeSpan.Zero);
}
