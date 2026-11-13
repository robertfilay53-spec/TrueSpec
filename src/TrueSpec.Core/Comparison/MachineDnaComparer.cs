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
/// </summary>
public sealed record MachineDnaComparison(
    MachineDnaVerdict Verdict,
    IReadOnlyList<MachineDnaPart> Added,
    IReadOnlyList<MachineDnaPart> Removed,
    IReadOnlyList<MachineDnaPartChange> Changed)
{
    public string Summary => Verdict switch
    {
        MachineDnaVerdict.Identical => "Same machine, same parts.",
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
            Array.Empty<MachineDnaPartChange>());

        if (old is null || @new is null || !old.IsAvailable || !@new.IsAvailable)
        {
            return empty;
        }

        var oldByKey = ByKey(old.Parts);
        var newByKey = ByKey(@new.Parts);

        var added = new List<MachineDnaPart>();
        var removed = new List<MachineDnaPart>();
        var changed = new List<MachineDnaPartChange>();
        var unidentified = false;

        foreach (var part in old.Parts)
        {
            if (!part.IsIdentified)
            {
                unidentified = true;
            }
        }

        foreach (var part in @new.Parts)
        {
            if (!part.IsIdentified)
            {
                unidentified = true;
            }
        }

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

        var verdict = unidentified
            ? MachineDnaVerdict.Unverifiable
            : added.Count > 0 || removed.Count > 0 || changed.Count > 0
                ? MachineDnaVerdict.Changed
                : MachineDnaVerdict.Identical;

        return new MachineDnaComparison(verdict, added, removed, changed);
    }

    private static (MachineDnaPartKind Kind, string Label) Key(MachineDnaPart part) =>
        (part.Kind, part.Label);

    private static Dictionary<(MachineDnaPartKind Kind, string Label), MachineDnaPart> ByKey(
        IReadOnlyList<MachineDnaPart> parts) =>
        parts
            .GroupBy(Key)
            .ToDictionary(g => g.Key, g => g.First());
}
