namespace TrueSpec.Core.Collectors;

using System.Globalization;

/// <summary>
/// One installed update from Win32_QuickFixEngineering. InstalledOn is the
/// date Windows recorded for the hotfix; it stays null when the recorded
/// date does not parse, and the hotfix still lists.
/// </summary>
public sealed record InstalledHotfix(string Id, DateTimeOffset? InstalledOn, string? Description);

/// <summary>
/// Patch state for one machine: the last successful update install and
/// check from the Windows Update registry results, whether the machine is
/// managed (WSUS / organization-controlled), and the installed-hotfix list.
/// Nothing here is a verdict: on a managed machine the last-success time is
/// the last install the organization allowed, not proof the machine has
/// every patch, and the UI must say so.
/// </summary>
public sealed record WindowsUpdateStatus(
    DateTimeOffset? LastSuccessfulInstall,
    DateTimeOffset? LastSuccessfulCheck,
    bool IsManaged,
    string? ManagementServer,
    IReadOnlyList<InstalledHotfix> Hotfixes)
{
    public int HotfixCount => Hotfixes.Count;

    public double? DaysSinceLastInstall(DateTimeOffset now) =>
        WindowsUpdateStatusParser.DaysSince(LastSuccessfulInstall, now);
}

/// <summary>
/// Pure parsing for Windows Update status, kept in Core so it is testable
/// without a registry or WMI. The registry and WMI reads themselves live in
/// the Windows collector; this class only interprets what was read.
/// </summary>
public static class WindowsUpdateStatusParser
{
    /// <summary>How many hotfixes the detail page lists; the rest are counted, not hidden.</summary>
    public const int MaxHotfixesShown = 10;

    /// <summary>
    /// Parses a Windows Update registry LastSuccessTime. Windows writes it
    /// as "yyyy-MM-dd HH:mm:ss" in local time; anything else that parses as
    /// a date is accepted, anything unparseable is null, never a guess.
    /// </summary>
    public static DateTimeOffset? ParseLastSuccessTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        if (DateTime.TryParseExact(
                text,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var exact))
        {
            return new DateTimeOffset(exact);
        }

        return DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// Parses a Win32_QuickFixEngineering InstalledOn date. It is a
    /// locale-formatted date string ("11/5/2026" on en-US); an unparseable
    /// date stays null and the hotfix still lists, sorted after dated ones.
    /// </summary>
    public static DateTimeOffset? ParseHotfixDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value.Trim(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// A WSUS server configured by policy means the organization controls
    /// updates. The last-success time is then the last install the server
    /// allowed, not proof the machine has every patch.
    /// </summary>
    public static bool IsManaged(string? wsusServer) => !string.IsNullOrWhiteSpace(wsusServer);

    /// <summary>
    /// Newest first, undated after dated, capped at <see cref="MaxHotfixesShown"/>.
    /// The full count is reported separately so the cap never looks like the total.
    /// </summary>
    public static IReadOnlyList<InstalledHotfix> OrderHotfixes(IEnumerable<InstalledHotfix> hotfixes)
    {
        ArgumentNullException.ThrowIfNull(hotfixes);

        return hotfixes
            .OrderByDescending(h => h.InstalledOn.HasValue)
            .ThenByDescending(h => h.InstalledOn)
            .Take(MaxHotfixesShown)
            .ToList();
    }

    /// <summary>
    /// The component headline from what is actually known. Four forms, and
    /// the headline never contradicts a spec on the same page: when the
    /// hotfix count is unknown (failed query) but the install date is known,
    /// the headline names the date instead of saying "not reported".
    /// </summary>
    public static string Headline(int? hotfixCount, DateTimeOffset? lastSuccessfulInstall)
    {
        var date = lastSuccessfulInstall?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return (hotfixCount, date) switch
        {
            (int count, string knownDate) => string.Create(
                CultureInfo.InvariantCulture,
                $"{count} updates installed - last update {knownDate}"),
            (int count, null) => string.Create(
                CultureInfo.InvariantCulture,
                $"{count} updates installed - last update not reported"),
            (null, string knownDate) => string.Create(
                CultureInfo.InvariantCulture,
                $"Last update {knownDate}"),
            _ => "Last update not reported",
        };
    }

    /// <summary>
    /// Days between a moment and now, rounded to one decimal. Returns
    /// null when the moment is missing, and when it is in the future: a
    /// future date is a bad clock, not "updated tomorrow".
    /// </summary>
    public static double? DaysSince(DateTimeOffset? moment, DateTimeOffset now)
    {
        if (moment is null)
        {
            return null;
        }

        var days = (now - moment.Value).TotalDays;
        if (days < -1d)
        {
            return null;
        }

        return Math.Round(Math.Max(0d, days), 1);
    }
}
