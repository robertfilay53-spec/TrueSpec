namespace TrueSpec.Core.Collectors;

/// <summary>
/// The failure classes crash forensics recognises. The class comes only from
/// the event provider + event id; the free-text message is never parsed.
/// </summary>
public enum CrashClass
{
    /// <summary>Windows stopped unexpectedly (blue screen).</summary>
    BugCheck,

    /// <summary>A hardware component reported a fatal error.</summary>
    WheaFatal,

    /// <summary>A hardware error was corrected automatically.</summary>
    WheaCorrected,

    /// <summary>The machine lost power or shut down unexpectedly.</summary>
    UnexpectedShutdown,

    /// <summary>The disk reported an error.</summary>
    DiskError,
}

/// <summary>
/// One crash-class event from the Windows System event log. <see cref="Detail"/>
/// is built only from the provider name and event id, never from the
/// free-text message, because message text varies by Windows version.
/// </summary>
public sealed record CrashEvent(
    DateTimeOffset OccurredAt,
    CrashClass Class,
    string Detail,
    string Source);

/// <summary>
/// One failure class summarised: how many events fell in the 90-day window,
/// and the first / last occurrence over all time (within the per-class cap).
/// <see cref="PlainMeaning"/> is a fixed table entry, not generated text.
/// </summary>
public sealed record CrashClassSummary(
    CrashClass Class,
    int Count90d,
    DateTimeOffset? FirstAt,
    DateTimeOffset? LastAt,
    string PlainMeaning);

/// <summary>
/// The machine's accident history. <see cref="EarliestLogEntry"/> is the
/// oldest entry the reader saw in the System channel; when it is much newer
/// than the Windows install date the log was probably cleared, and a wiped
/// log reads as a gap, never as a clean record.
/// </summary>
public sealed record CrashHistory(
    IReadOnlyList<CrashClassSummary> Summaries,
    DateTimeOffset? EarliestLogEntry,
    bool LogClearedSuspected);
