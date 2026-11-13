namespace TrueSpec.Core.Localization;

/// <summary>
/// One language pack: a BCP 47 code, the language's own display name,
/// and the translated strings keyed by <see cref="AppStringKeys"/>.
/// Packs are data, never code paths: a bad pack is a validation error,
/// never an exception and never a half-translated screen.
/// </summary>
public sealed record LanguagePack(string Code, string DisplayName, IReadOnlyDictionary<string, string> Strings)
{
    /// <summary>
    /// Validates this pack against the key catalogue. Returns every
    /// problem found (empty means the pack is valid): a missing key, a
    /// key that is not in the catalogue, an empty translation, or a
    /// malformed language code. The caller decides what to do with the
    /// errors; validation itself never throws.
    /// </summary>
    public IReadOnlyList<string> Validate(IEnumerable<string> keys)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Code) || !IsValidCode(Code))
        {
            errors.Add($"Language code '{Code}' is not a two-letter language code (for example 'en' or 'de').");
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            errors.Add("Language display name is empty.");
        }

        var wanted = new HashSet<string>(keys, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (key, value) in Strings)
        {
            seen.Add(key);
            if (!wanted.Contains(key))
            {
                errors.Add($"Unknown string key '{key}' is not in the catalogue.");
            }
            else if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"String key '{key}' has an empty translation.");
            }
        }

        foreach (var key in wanted)
        {
            if (!seen.Contains(key))
            {
                errors.Add($"String key '{key}' is missing from the '{Code}' pack.");
            }
        }

        return errors;
    }

    /// <summary>
    /// A supported code is two lowercase ASCII letters (for example "en",
    /// "de"), optionally followed by a dash and a two-letter region
    /// (for example "de-DE"). Region variants fall back to their base
    /// language when no pack carries the region.
    /// </summary>
    public static bool IsValidCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var parts = code.Trim().Split('-');
        if (parts.Length is < 1 or > 2)
        {
            return false;
        }

        if (parts[0].Length != 2 || parts[0].Any(c => c is < 'a' or > 'z'))
        {
            return false;
        }

        if (parts.Length == 2 && (parts[1].Length != 2 || parts[1].Any(c => c is < 'A' or > 'Z')))
        {
            return false;
        }

        return true;
    }
}
