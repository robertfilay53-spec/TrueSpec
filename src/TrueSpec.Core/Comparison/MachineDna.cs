namespace TrueSpec.Core.Comparison;

using System.Security.Cryptography;
using System.Text;
using TrueSpec.Core.Models;

/// <summary>
/// Machine DNA (v2.0 U7): a fingerprint that binds a report to one physical
/// machine, built from the serials the hardware reports (board, BIOS, disks,
/// memory modules, CPU). The hash input uses the FULL serial; the stored
/// snapshot and the UI only ever carry the truncated form, so a full serial
/// never appears in a screenshot-friendly view, a log or an export.
/// DNA is identity, not health: it never changes the score or the banner.
/// </summary>
public enum MachineDnaPartKind
{
    Board,
    Bios,
    Disk,
    Memory,
    Cpu,
}

/// <summary>
/// One part of a machine's DNA: what it is, which one it is, and the
/// truncated serial the UI and the stored snapshot may show. A part whose
/// serial the hardware did not report (or reported as a known placeholder)
/// carries <see cref="SpecLabels.NotReportedByHardware"/> as its truncated
/// serial and <see cref="IsIdentified"/> false: an unidentified part can
/// never prove two machines are the same, and the comparer reports
/// Unverifiable instead of guessing.
/// </summary>
public sealed record MachineDnaPart(
    MachineDnaPartKind Kind,
    string Label,
    string SerialTruncated)
{
    /// <summary>
    /// Builds a part from a full serial. A missing serial, a blank serial
    /// and every known placeholder serial (see <see cref="MachineDna.PlaceholderSerials"/>)
    /// become "not reported": OEM placeholders such as
    /// "To be filled by O.E.M." appear on thousands of unrelated machines,
    /// so treating one as an identity would let two different machines
    /// share a DNA by accident.
    /// </summary>
    public static MachineDnaPart FromSerial(MachineDnaPartKind kind, string label, string? serial)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        var clean = serial?.Trim();
        if (string.IsNullOrEmpty(clean) || MachineDna.IsPlaceholderSerial(clean))
        {
            return new MachineDnaPart(kind, label, SpecLabels.NotReportedByHardware);
        }

        return new MachineDnaPart(kind, label, MachineDna.TruncateSerial(clean));
    }

    /// <summary>False when the hardware did not report a usable serial for this part.</summary>
    public bool IsIdentified => !string.Equals(
        SerialTruncated, SpecLabels.NotReportedByHardware, StringComparison.Ordinal);

    /// <summary>True when this part is the same identified part as <paramref name="other"/>.</summary>
    public bool Matches(MachineDnaPart other) =>
        other is not null &&
        Kind == other.Kind &&
        string.Equals(Label, other.Label, StringComparison.Ordinal) &&
        IsIdentified && other.IsIdentified &&
        string.Equals(SerialTruncated, other.SerialTruncated, StringComparison.Ordinal);
}

/// <summary>
/// A machine's DNA: the canonically sorted parts and the SHA-256 hash of
/// the canonical string (parts sorted by (Kind, Label), joined with "|"
/// as "Kind=Label:Serial", full serials for identified parts). The hash is
/// null when no part reported a usable serial: a machine that reports no
/// serials at all has "DNA not available", never a hash shared with every
/// other serial-less machine.
/// </summary>
public sealed record MachineDna(
    IReadOnlyList<MachineDnaPart> Parts,
    string? Hash)
{
    /// <summary>
    /// The serials hardware vendors use as placeholders instead of a real
    /// serial. Compared case-insensitively after trimming, so
    /// "TO BE FILLED BY O.E.M." and "To be filled by O.E.M" match too.
    /// A serial on this list is "not reported", never an identity.
    /// </summary>
    public static readonly IReadOnlyList<string> PlaceholderSerials = new[]
    {
        "To be filled by O.E.M.",
        "To be filled by O.E.M",
        "Default string",
        "None",
        "Not Applicable",
        "Not Specified",
        "Not Available",
        "Unknown",
        "N/A",
        "NA",
        "0",
        "00000000",
    };

    public static bool IsPlaceholderSerial(string serial) =>
        PlaceholderSerials.Contains(serial.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The truncated form shown in the UI and the stored snapshot: the
    /// first 4 and last 4 characters with "..." between them. A serial of
    /// 8 characters or fewer is shown whole: truncating it would hide
    /// characters without hiding anything, and the overlap would mislead.
    /// </summary>
    public static string TruncateSerial(string serial)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);

        var clean = serial.Trim();
        if (clean.Length <= 8)
        {
            return clean;
        }

        return clean[..4] + "..." + clean[^4..];
    }

    /// <summary>
    /// Builds a DNA from full serials: the hash is computed over the full
    /// serials, the stored parts only keep the truncated form. Parts are
    /// sorted by (Kind, Label) so the same machine scanned in any order
    /// produces the same hash; a duplicated (Kind, Label) keeps the first
    /// serial in that order, so the hash is deterministic.
    /// </summary>
    public static MachineDna Build(IEnumerable<(MachineDnaPartKind Kind, string Label, string? Serial)> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        var normalized = parts
            .Select(p => (Part: MachineDnaPart.FromSerial(p.Kind, p.Label, p.Serial), Serial: p.Serial?.Trim()))
            .OrderBy(p => p.Part.Kind)
            .ThenBy(p => p.Part.Label, StringComparer.Ordinal)
            .ThenBy(p => p.Serial, StringComparer.Ordinal)
            .GroupBy(p => (p.Part.Kind, p.Part.Label))
            .Select(g => g.First())
            .ToList();

        var sorted = normalized
            .OrderBy(p => p.Part.Kind)
            .ThenBy(p => p.Part.Label, StringComparer.Ordinal)
            .ToList();

        string? hash = null;
        if (sorted.Any(p => p.Part.IsIdentified))
        {
            var canonical = string.Join("|", sorted.Select(p =>
                p.Part.Kind + "=" + p.Part.Label + ":" +
                (p.Part.IsIdentified ? p.Serial : SpecLabels.NotReportedByHardware)));
            hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        }

        return new MachineDna(
            sorted.Select(p => p.Part).ToList(),
            hash);
    }

    /// <summary>A DNA with no parts at all: no hash, not available.</summary>
    public static MachineDna Empty { get; } = new(Array.Empty<MachineDnaPart>(), null);

    /// <summary>True when at least one part reported a usable serial.</summary>
    public bool IsAvailable => Hash is not null;

    /// <summary>
    /// The hash for display, or the honest status when there is none:
    /// "DNA not available" for a scan that recorded DNA but found no
    /// usable serial. (A snapshot from before DNA existed says
    /// "DNA not recorded" instead; see SystemSnapshot.MachineDnaStatus.)
    /// </summary>
    public string HashDisplay => Hash ?? "DNA not available";
}
