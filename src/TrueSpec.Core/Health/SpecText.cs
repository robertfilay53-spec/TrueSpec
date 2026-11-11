namespace TrueSpec.Core.Health;

using System.Globalization;
using TrueSpec.Core.Models;

/// <summary>
/// Small helpers for reading the honest, formatted spec values that collectors
/// produce. Values that are not present are never guessed.
/// </summary>
public static class SpecText
{
    public static SpecValue? Find(IReadOnlyList<SpecValue> specs, params string[] labels)
    {
        foreach (var label in labels)
        {
            var match = specs.FirstOrDefault(s =>
                string.Equals(s.Label, label, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    public static string? Value(IReadOnlyList<SpecValue> specs, params string[] labels) =>
        Find(specs, labels)?.Value;

    public static double? Number(IReadOnlyList<SpecValue> specs, params string[] labels)
    {
        var value = Value(specs, labels);
        return value is not null && TryParseLeadingNumber(value, out var number) ? number : null;
    }

    public static DateTimeOffset? Date(IReadOnlyList<SpecValue> specs, params string[] labels)
    {
        var value = Value(specs, labels);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return parsed;
        }

        return null;
    }

    public static bool TryParseLeadingNumber(string? text, out double value)
    {
        value = 0d;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var token = text.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0];
        token = token.TrimEnd('%').Replace(",", string.Empty, StringComparison.Ordinal);

        return double.TryParse(
            token,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }

    /// <summary>
    /// Parses a memory size such as "16 GB", "16.0 GiB", "16384 MB" or "16GB"
    /// into bytes. Units are binary (1 GB = 1 GiB = 1024^3 bytes) because RAM
    /// is sold and displayed that way; a bare number is read as GB. Returns
    /// false when the text cannot be understood, so the caller can say so
    /// instead of guessing.
    /// </summary>
    public static bool TryParseMemorySizeBytes(string? text, out ulong bytes)
    {
        bytes = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var clean = text.Trim().Replace(",", string.Empty, StringComparison.Ordinal);

        var split = 0;
        while (split < clean.Length && (char.IsDigit(clean[split]) || clean[split] == '.'))
        {
            split++;
        }

        if (split == 0 ||
            !double.TryParse(clean[..split], NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ||
            number < 0d)
        {
            return false;
        }

        var unit = clean[split..].Trim().ToUpperInvariant();
        var multiplier = unit switch
        {
            "" or "G" or "GB" or "GIB" => 1UL << 30,
            "T" or "TB" or "TIB" => 1UL << 40,
            "M" or "MB" or "MIB" => 1UL << 20,
            "K" or "KB" or "KIB" => 1UL << 10,
            "B" or "BYTE" or "BYTES" => 1UL,
            _ => 0UL,
        };

        if (multiplier == 0UL || number > (double)ulong.MaxValue / multiplier)
        {
            return false;
        }

        bytes = (ulong)(number * multiplier);
        return true;
    }
}
