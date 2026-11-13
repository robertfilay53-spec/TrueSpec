using System.Globalization;

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

    /// <summary>
    /// Resolves a template key for a language and fills its placeholders
    /// ({0}, {1}) with <paramref name="args"/>. Numbers are formatted
    /// with the invariant culture, never the thread culture, so a
    /// translated sentence can never change how a number looks: the
    /// placeholder order is part of the translation, the number format
    /// is not. A template that cannot be formatted (a bad placeholder,
    /// a missing argument) returns unformatted instead of throwing, so
    /// a broken translation shows its template rather than crashing
    /// the page. An unknown key returns the key itself, like
    /// <see cref="Get(string?, string?)"/>.
    /// </summary>
    public static string GetFormatted(string? key, string? languageCode, params object?[] args)
    {
        var template = Get(key, languageCode);
        return FormatTemplate(template, args);
    }

    /// <summary>Resolves a template for an explicit pack and fills its placeholders (see above).</summary>
    public static string GetFormatted(string? key, LanguagePack? pack, params object?[] args)
    {
        var template = Get(key, pack);
        return FormatTemplate(template, args);
    }

    private static string FormatTemplate(string template, object?[] args)
    {
        if (string.IsNullOrEmpty(template) || args.Length == 0)
        {
            return template;
        }

        try
        {
            return string.Format(CultureInfo.InvariantCulture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }
}
