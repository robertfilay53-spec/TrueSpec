namespace TrueSpec.Core.Collectors;

/// <summary>
/// Severity of one authenticity finding. Info is a note, not an accusation:
/// a re-imaged machine fires Info rules by design. Watch is a contradiction
/// worth asking the seller about. Strong is a contradiction that cannot be
/// explained by normal use.
/// </summary>
public enum AuthenticitySeverity
{
    Info,
    Watch,
    Strong,
}

/// <summary>
/// One measured age signal, with its source named so the buyer can see the
/// raw number behind the verdict. <see cref="IsReliable"/> is false only
/// when another rule proved the signal itself impossible (rule R4: a BIOS
/// date after the Windows install date). A missing signal is never invented:
/// it simply is not in the list, and the rule that needed it is skipped.
/// </summary>
public sealed record AuthenticitySignal(
    string Name,
    string Value,
    string Source,
    DateTimeOffset CollectedAt,
    bool IsReliable = true);

/// <summary>
/// One cross-check finding. The evidence line is plain English and names
/// its two sources; it never accuses the seller. The verdict says "signs of
/// prior use", never "refurbished fraud" or "seller lied".
/// </summary>
public sealed record AuthenticityCheck(
    string Title,
    string EvidenceLine,
    AuthenticitySeverity Severity,
    string SourceA,
    string SourceB);

/// <summary>
/// The authenticity verdict for one snapshot. <see cref="Checks"/> holds
/// only rules that fired. <see cref="SkippedChecks"/> names every rule that
/// could not run because a signal was missing, so a missing signal is
/// visible instead of silently guessed. <see cref="Signals"/> is every age
/// signal that was reported, with its source.
/// </summary>
public sealed record AuthenticityResult(
    IReadOnlyList<AuthenticityCheck> Checks,
    IReadOnlyList<string> SkippedChecks,
    IReadOnlyList<AuthenticitySignal> Signals,
    string Verdict,
    string Summary)
{
    /// <summary>At least one Watch or Strong check fired.</summary>
    public const string SignsOfPriorUse = "Signs of prior use";

    /// <summary>No Watch or Strong check fired. An Info note may still be listed.</summary>
    public const string NoSignsOfPriorUse = "No signs of prior use";
}
