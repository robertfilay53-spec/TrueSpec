namespace TrueSpec.Core.Collectors;

using System.Globalization;

/// <summary>
/// Pure parsing of Windows Memory Diagnostic results: raw event rows in,
/// the newest result out. The diagnostic writes its result to the System
/// event log under the provider Microsoft-Windows-MemoryDiagnostics-Results:
/// event 1101 / 1201 mean the test ran and found no errors, event 1102 /
/// 1202 mean it found hardware errors. Each run writes a companion pair at
/// the same second, so the newest row decides. The parser relies on event
/// id + timestamp ONLY and never reads the free-text message (wording
/// varies by Windows version). No result in the log means the diagnostic
/// has not run: a measured "Not run", not the hardcoded label the RAM page
/// used to carry.
/// </summary>
public static class MemoryDiagnosticParser
{
    /// <summary>One raw System-channel row for the MemoryDiagnostics-Results provider.</summary>
    public sealed record RawMemoryDiagnosticEvent(
        int EventId,
        DateTimeOffset OccurredAt);

    /// <summary>How many newest rows the caller needs to read; only the newest ever decides.</summary>
    public const int MaxRowsRead = 5;

    /// <summary>
    /// The newest recognised result in the rows, or null when no row is a
    /// result event (empty log, or only unrelated events).
    /// </summary>
    public static MemoryDiagnosticResult? Parse(IReadOnlyList<RawMemoryDiagnosticEvent> rows)
    {
        return rows
            .Select(Classify)
            .Where(e => e is not null)
            .Select(e => e!)
            .OrderByDescending(e => e.RanAt)
            .FirstOrDefault();
    }

    private static MemoryDiagnosticResult? Classify(RawMemoryDiagnosticEvent row) => row.EventId switch
    {
        1101 or 1201 => new MemoryDiagnosticResult(MemoryDiagnosticOutcome.Passed, row.OccurredAt),
        1102 or 1202 => new MemoryDiagnosticResult(MemoryDiagnosticOutcome.Failed, row.OccurredAt),
        _ => null,
    };

    /// <summary>
    /// The RAM page value: "Passed on 2026-11-01", "Failed on 2026-11-01",
    /// or "Not run" when no result event exists in the log.
    /// </summary>
    public static string DisplayText(MemoryDiagnosticResult? result) => result switch
    {
        { Outcome: MemoryDiagnosticOutcome.Passed, RanAt: var ranAt } =>
            string.Create(CultureInfo.InvariantCulture, $"Passed on {ranAt:yyyy-MM-dd}"),
        { Outcome: MemoryDiagnosticOutcome.Failed, RanAt: var ranAt } =>
            string.Create(CultureInfo.InvariantCulture, $"Failed on {ranAt:yyyy-MM-dd}"),
        _ => "Not run",
    };
}

/// <summary>The outcome of one Windows Memory Diagnostic run.</summary>
public enum MemoryDiagnosticOutcome
{
    Passed,
    Failed,
}

/// <summary>
/// The newest Windows Memory Diagnostic result in the event log. The
/// diagnostic itself is a boot-time test, not a live sensor: the result is
/// a dated fact from the log, and a failed result is a reason to run the
/// definitive bootable MemTest86, never a diagnosis of which stick failed.
/// </summary>
public sealed record MemoryDiagnosticResult(
    MemoryDiagnosticOutcome Outcome,
    DateTimeOffset RanAt);
