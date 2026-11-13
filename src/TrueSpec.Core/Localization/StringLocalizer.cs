namespace TrueSpec.Core.Localization;

/// <summary>
/// Resolves a string key for a language. The rules are the whole
/// contract, and they are pinned by tests:
/// <list type="bullet">
/// <item>The translated text when the pack carries the key.</item>
/// <item>The English text when the pack does not carry the key (a
/// partially translated pack never shows a blank).</item>
/// <item>The key itself when no pack carries it (an unknown key is a
/// programming error, and showing the key says so instead of crashing
/// or showing nothing).</item>
/// </list>
/// Resolution never throws and never changes the thread culture:
/// measured values keep the app's existing number and date formatting,
/// so localisation can never change a measurement. Only chrome strings
/// go through this localizer.
/// </summary>
public static class StringLocalizer
{
    /// <summary>Resolves <paramref name="key"/> for a language code.</summary>
    public static string Get(string? key, string? languageCode) =>
        Get(key, LanguagePacks.Get(languageCode));

    /// <summary>Resolves <paramref name="key"/> for an explicit pack.</summary>
    public static string Get(string? key, LanguagePack? pack)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        if (pack is not null && pack.Strings.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (LanguagePacks.English.Strings.TryGetValue(key, out var fallback) && !string.IsNullOrEmpty(fallback))
        {
            return fallback;
        }

        return key;
    }
}
