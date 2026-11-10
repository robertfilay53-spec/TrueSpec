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
}
