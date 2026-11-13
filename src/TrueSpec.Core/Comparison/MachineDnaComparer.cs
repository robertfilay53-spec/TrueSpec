namespace TrueSpec.Core.Comparison;

/// <summary>What comparing two machine DNAs concluded.</summary>
public enum MachineDnaVerdict
{
    /// <summary>Same identified parts, same serials: the same machine, unchanged.</summary>
    Identical,

    /// <summary>A part was added, removed or replaced: a different machine, or this one changed.</summary>
    Changed,

    /// <summary>
    /// Identity cannot be proven either way: a part was unidentified in
    /// either scan, or one side has no DNA at all. Never reported as a
    /// match or a mismatch.
    /// </summary>
    Unverifiable,
}

/// <summary>One part whose serial differs between two scans of the same (Kind, Label).</summary>
public sealed record MachineDnaPartChange(MachineDnaPart Before, MachineDnaPart After);

/// <summary>
/// The result of comparing two machine DNAs. Parts are matched by
/// (Kind, Label): a part present only in the new DNA is Added, only in the
/// old DNA is Removed, present in both with a different serial is Changed.
/// <see cref="FingerprintsMatch"/> is true when both scans carry the same
/// SHA-256 hash: the same identified serials and the same unidentified
/// parts, in other words the same canonical part list. A matching
/// fingerprint with an unidentified part still compares
/// <see cref="MachineDnaVerdict.Unverifiable"/> (the verdict never
/// reports a match the hardware did not prove), but the UI may say
/// "fingerprint matches" instead of reading as a failure, and
/// <see cref="UnidentifiedParts"/> names the part(s) it could not prove.
/// </summary>
public sealed record MachineDnaComparison(
    MachineDnaVerdict Verdict,
    IReadOnlyList<MachineDnaPart> Added,
    IReadOnlyList<MachineDnaPart> Removed,
    IReadOnlyList<MachineDnaPartChange> Changed,
    bool FingerprintsMatch,
    IReadOnlyList<MachineDnaPart> UnidentifiedParts)
{
    public string Summary => Verdict switch
    {
        MachineDnaVerdict.Identical => "Same machine, same parts.",
        // The fingerprints-match case reads consistently with the
        // headline: the fingerprint matches and no part changed; the
        // unidentified part is the only reason a full verified identity
        // match cannot be claimed. The old text ("could not be
        // verified") read as a failure on an unchanged machine (the
        // v2.0.1 AppOnFly finding), even though nothing changed.
        MachineDnaVerdict.Unverifiable when FingerprintsMatch && Added.Count == 0 && Removed.Count == 0 && Changed.Count == 0
            => "Same machine — fingerprint matches. No part changed, but a part was not identified in one of the scans, so a full verified identity match cannot be claimed.",
        MachineDnaVerdict.Unverifiable => "Machine identity could not be verified: a part was not identified in one of the scans.",
        MachineDnaVerdict.Changed =>
            $"Machine changed: {Added.Count} part(s) added, {Removed.Count} removed, {Changed.Count} replaced.",
        _ => "Machine identity could not be verified.",
    };
}

/// <summary>
/// Pure comparison of two machine DNAs. No hardware access, no I/O.
/// DNA is identity, not health: this never feeds a score.
/// </summary>
public static class MachineDnaComparer
{
    /// <summary>
    /// Compares two DNAs. Either side null or without a usable serial
    /// makes the verdict Unverifiable: there is nothing to compare
    /// against. A part unidentified in either scan also makes it
    /// Unverifiable, even when other parts match: one unproven part
    /// means the identity is unproven. Added/Removed/Changed are still
    /// filled in when they can be computed, so the UI can show what it
    /// knows alongside the verdict.
    /// </summary>
    public static MachineDnaComparison Compare(MachineDna? old, MachineDna? @new)
    {
        var empty = new MachineDnaComparison(
            MachineDnaVerdict.Unverifiable,
            Array.Empty<MachineDnaPart>(),
            Array.Empty<MachineDnaPart>(),
            Array.Empty<MachineDnaPartChange>(),
            FingerprintsMatch: false,
            UnidentifiedParts: Array.Empty<MachineDnaPart>());

        if (old is null || @new is null || !old.IsAvailable || !@new.IsAvailable)
        {
            return empty;
        }

        var oldByKey = ByKey(old.Parts);
        var newByKey = ByKey(@new.Parts);

        var added = new List<MachineDnaPart>();
        var removed = new List<MachineDnaPart>();
        var changed = new List<MachineDnaPartChange>();

        // Every part either scan could not identify, once per (Kind,
        // Label): the UI names these so the user knows what the
        // comparison could not prove, instead of only hearing "unverified".
        var unidentified = old.Parts.Concat(@new.Parts)
            .Where(p => !p.IsIdentified)
            .GroupBy(Key)
            .Select(g => g.First())
            .OrderBy(p => p.Kind)
            .ThenBy(p => p.Label, StringComparer.Ordinal)
            .ToList();

        foreach (var part in @new.Parts)
        {
            if (!oldByKey.TryGetValue(Key(part), out var before))
            {
                added.Add(part);
                continue;
            }

            if (before.IsIdentified && part.IsIdentified &&
                !string.Equals(before.SerialTruncated, part.SerialTruncated, StringComparison.Ordinal))
            {
                changed.Add(new MachineDnaPartChange(before, part));
            }
        }

        foreach (var part in old.Parts)
        {
            if (!newByKey.ContainsKey(Key(part)))
            {
                removed.Add(part);
            }
        }

        // The verdict stays conservative: one unproven part means the
        // identity is unproven, even when the fingerprints match. The
        // hash is computed over the canonical part list (identified
        // serials plus "not reported" for the rest), so equal hashes
        // mean the same identified serials and the same unidentified
        // parts: the UI may say so, but it must not call it a match.
        var verdict = unidentified.Count > 0
            ? MachineDnaVerdict.Unverifiable
            : added.Count > 0 || removed.Count > 0 || changed.Count > 0
                ? MachineDnaVerdict.Changed
                : MachineDnaVerdict.Identical;

        var fingerprintsMatch = old.Hash is not null && @new.Hash is not null &&
            string.Equals(old.Hash, @new.Hash, StringComparison.Ordinal);

        return new MachineDnaComparison(verdict, added, removed, changed, fingerprintsMatch, unidentified);
    }

    private static (MachineDnaPartKind Kind, string Label) Key(MachineDnaPart part) =>
        (part.Kind, part.Label);

    private static Dictionary<(MachineDnaPartKind Kind, string Label), MachineDnaPart> ByKey(
        IReadOnlyList<MachineDnaPart> parts) =>
        parts
            .GroupBy(Key)
            .ToDictionary(g => g.Key, g => g.First());
}
