using System;
using System.Collections.Generic;
using System.Linq;
using TrueSpec.Core.Comparison;
using TrueSpec.Core.Models;

namespace TrueSpec.App.Services;

/// <summary>What a machine-DNA check concluded about one scan or a pair of scans.</summary>
public enum DnaVerificationState
{
    /// <summary>No DNA was recorded for one or both scans, so nothing can be verified. This is not a mismatch.</summary>
    NotRecorded,

    /// <summary>DNA exists but a part was unidentified in one of the scans, so the comparison cannot be trusted either way.</summary>
    Unverifiable,

    /// <summary>The DNA matches: same machine, same parts.</summary>
    Identical,

    /// <summary>
    /// The two fingerprints are identical and no part changed, but a part
    /// was unidentified in one of the scans, so the match is not a
    /// verified identity. The UI says "fingerprint matches" with the
    /// unidentified part(s) named, instead of reading as a failure: on a
    /// clean machine this is the normal outcome, not a swapped part.
    /// </summary>
    FingerprintMatches,

    /// <summary>The DNA differs: at least one part changed between the scans.</summary>
    Changed,
}

/// <summary>One part that differs between two scans, in plain words.</summary>
public sealed record DnaPartChange(string Kind, string Label, string Detail);

/// <summary>
/// The result of describing one scan's DNA or comparing two scans' DNA.
/// DNA is identity, not health: a <see cref="DnaVerificationState.Changed"/>
/// result never changes the score or the verdict; it only says the report
/// belongs to a machine whose parts changed since the earlier scan.
/// </summary>
public sealed record MachineDnaVerification(
    DnaVerificationState State,
    string? HashDisplay,
    string SummaryText,
    IReadOnlyList<DnaPartChange> ChangedParts)
{
    /// <summary>
    /// Parts either scan could not identify, as "Kind Label" display
    /// strings ("Memory DIMM 0"). Empty when every part was identified.
    /// The UI lists these next to the verdict so the user knows exactly
    /// what the comparison could not prove.
    /// </summary>
    public IReadOnlyList<string> UnidentifiedParts { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Truncates a full SHA-256 hex hash for display: first 8 and last 8
    /// characters. The full hash is only ever written into the local report
    /// file, never shown in the UI, so a screenshot cannot be used to clone
    /// the machine's identity. Null or blank input stays null: a missing
    /// hash is "not recorded", never a blank.
    /// </summary>
    public static string? TruncateHash(string? hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            return null;
        }

        var trimmed = hash.Trim();
        return trimmed.Length <= 16
            ? trimmed
            : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{trimmed[..8]}…{trimmed[^8..]}");
    }

    /// <summary>Honest empty state: no DNA was recorded for this scan (or pair of scans).</summary>
    public static MachineDnaVerification NotRecorded() => new(
        DnaVerificationState.NotRecorded,
        null,
        "DNA not recorded for this scan. Scans saved before machine DNA existed carry no fingerprint, so this machine cannot be verified against them yet.",
        Array.Empty<DnaPartChange>());

    /// <summary>Honest empty state for a pair of scans where at least one side has no DNA.</summary>
    public static MachineDnaVerification NotRecordedForPair() => new(
        DnaVerificationState.NotRecorded,
        null,
        "DNA not recorded for one or both snapshots, so this is not a mismatch. Re-scan the machine to record its DNA, then compare again.",
        Array.Empty<DnaPartChange>());

    /// <summary>DNA was recorded but no part reported a usable serial: there is nothing to match against.</summary>
    public static MachineDnaVerification NotAvailable() => new(
        DnaVerificationState.Unverifiable,
        null,
        "DNA not available: this scan recorded DNA but no part reported a usable serial, so the machine cannot be verified.",
        Array.Empty<DnaPartChange>());

    /// <summary>The two scans are the same machine with the same parts.</summary>
    public static MachineDnaVerification Identical(string? hashDisplay) => new(
        DnaVerificationState.Identical,
        hashDisplay,
        "Same machine, same parts.",
        Array.Empty<DnaPartChange>());

    /// <summary>
    /// The two fingerprints are identical and no part changed, but a part
    /// was unidentified in one of the scans, so this is not a verified
    /// identity match. The summary says so in one sentence; the names
    /// live in <see cref="UnidentifiedParts"/> for the UI to list.
    /// </summary>
    public static MachineDnaVerification FingerprintMatches(string? hashDisplay, IReadOnlyList<string> unidentifiedParts) => new(
        DnaVerificationState.FingerprintMatches,
        hashDisplay,
        "Same machine — fingerprint matches. A part could not be identified in one of the scans, so this is not a verified identity match; the unidentified part is listed below.",
        Array.Empty<DnaPartChange>())
    {
        UnidentifiedParts = unidentifiedParts,
    };

    /// <summary>The two scans differ: the named parts changed.</summary>
    public static MachineDnaVerification Changed(IReadOnlyList<DnaPartChange> changedParts, string? hashDisplay) => new(
        DnaVerificationState.Changed,
        hashDisplay,
        "Parts changed since the earlier scan. The score still describes this machine as it is now; the earlier report belongs to the machine as it was then.",
        changedParts);

    /// <summary>A part was unidentified in one of the scans, so the comparison cannot be trusted either way.</summary>
    public static MachineDnaVerification Unverifiable(string? hashDisplay) => new(
        DnaVerificationState.Unverifiable,
        hashDisplay,
        "DNA cannot be verified: a part was not identified in one of the scans, so the two fingerprints cannot be compared.",
        Array.Empty<DnaPartChange>());
}

/// <summary>
/// The seam between the UI and machine DNA (v2.0 U7, Blueprint 4).
/// The Report page's "Verify this machine" action and the Compare page's
/// DNA row talk only to this interface, so the UI, its empty states and
/// its tests are written against the verification result, not against the
/// fingerprint itself.
/// </summary>
public interface IMachineDnaVerifier
{
    /// <summary>Describes one snapshot's DNA: its truncated hash, or "not recorded".</summary>
    MachineDnaVerification Describe(SystemSnapshot snapshot);

    /// <summary>Compares two snapshots' DNA: Identical, Changed (with the parts), Unverifiable, or NotRecorded.</summary>
    MachineDnaVerification Compare(SystemSnapshot before, SystemSnapshot after);
}

/// <summary>
/// The real verifier: it rebuilds each snapshot's <see cref="MachineDna"/>
/// from the stored parts + hash and delegates the comparison to
/// <see cref="MachineDnaComparer"/>, translating the verdict into the
/// display record the UI renders. Three honest states, never a guess:
/// a snapshot whose <c>MachineDnaParts</c> is null was saved before DNA
/// existed ("not recorded", never a mismatch); a snapshot with parts but
/// no hash recorded DNA but found no usable serial ("not available");
/// a part unidentified in either scan makes the comparison
/// Unverifiable even when the other parts match. When the fingerprints
/// match and nothing changed, the record says FingerprintMatches
/// instead: the same honest verdict underneath, worded so a clean
/// machine does not read as a failure, with the unidentified part(s)
/// named. The full hash is never
/// exposed: the UI only ever sees <see cref="MachineDnaVerification.TruncateHash"/>.
/// </summary>
public sealed class MachineDnaVerifier : IMachineDnaVerifier
{
    public static readonly MachineDnaVerifier Instance = new();

    public MachineDnaVerification Describe(SystemSnapshot snapshot)
    {
        if (snapshot.MachineDnaParts is null)
        {
            return MachineDnaVerification.NotRecorded();
        }

        if (snapshot.MachineDnaHash is null)
        {
            return MachineDnaVerification.NotAvailable();
        }

        return MachineDnaVerification.Identical(
            MachineDnaVerification.TruncateHash(snapshot.MachineDnaHash));
    }

    public MachineDnaVerification Compare(SystemSnapshot before, SystemSnapshot after)
    {
        if (before.MachineDnaParts is null || after.MachineDnaParts is null)
        {
            return MachineDnaVerification.NotRecordedForPair();
        }

        var oldDna = new MachineDna(before.MachineDnaParts, before.MachineDnaHash);
        var newDna = new MachineDna(after.MachineDnaParts, after.MachineDnaHash);
        var comparison = MachineDnaComparer.Compare(oldDna, newDna);
        var hashDisplay = MachineDnaVerification.TruncateHash(after.MachineDnaHash);
        var unidentified = comparison.UnidentifiedParts
            .Select(p => $"{p.Kind} {p.Label}")
            .ToList();

        // A matching fingerprint with an unidentified part is the normal
        // outcome on a clean machine whose hardware does not report every
        // serial: the verdict stays Unverifiable in the Core (never a
        // match the hardware did not prove), but the UI says "fingerprint
        // matches" with the part named instead of reading as a failure.
        if (comparison.Verdict == MachineDnaVerdict.Unverifiable &&
            comparison.FingerprintsMatch &&
            comparison.Added.Count == 0 &&
            comparison.Removed.Count == 0 &&
            comparison.Changed.Count == 0)
        {
            return MachineDnaVerification.FingerprintMatches(hashDisplay, unidentified);
        }

        return comparison.Verdict switch
        {
            MachineDnaVerdict.Identical => MachineDnaVerification.Identical(hashDisplay),
            MachineDnaVerdict.Changed => new MachineDnaVerification(
                DnaVerificationState.Changed,
                hashDisplay,
                comparison.Summary,
                DescribeChanges(comparison))
            {
                UnidentifiedParts = unidentified,
            },
            _ => new MachineDnaVerification(
                DnaVerificationState.Unverifiable,
                hashDisplay,
                comparison.Summary,
                DescribeChanges(comparison))
            {
                UnidentifiedParts = unidentified,
            },
        };
    }

    private static IReadOnlyList<DnaPartChange> DescribeChanges(MachineDnaComparison comparison)
    {
        var changes = new List<DnaPartChange>();
        changes.AddRange(comparison.Added.Select(part => new DnaPartChange(
            part.Kind.ToString(),
            part.Label,
            $"added (serial {part.SerialTruncated})")));
        changes.AddRange(comparison.Removed.Select(part => new DnaPartChange(
            part.Kind.ToString(),
            part.Label,
            $"removed (was serial {part.SerialTruncated})")));
        changes.AddRange(comparison.Changed.Select(change => new DnaPartChange(
            change.After.Kind.ToString(),
            change.After.Label,
            $"serial changed from {change.Before.SerialTruncated} to {change.After.SerialTruncated}")));
        return changes;
    }
}
