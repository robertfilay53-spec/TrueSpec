using System.Collections.Generic;
using TrueSpec.Core.Localization;

namespace TrueSpec.App.ViewModels;

/// <summary>One entry in the left rail navigation.</summary>
public sealed record NavItemViewModel(string Key, string Label, string IconData)
{
    public static IReadOnlyList<NavItemViewModel> CreateDefault() => CreateForLanguage(LanguagePacks.EnglishCode);

    /// <summary>
    /// The rail for a language: the same 22 items in the same order and
    /// with the same keys, only the labels are translated. An unknown
    /// code falls back to English (see <see cref="StringLocalizer"/>),
    /// so a bad stored language can never leave the rail blank. Keys
    /// never change with the language: routing is by key, and a label
    /// change must never re-route a page.
    /// </summary>
    public static IReadOnlyList<NavItemViewModel> CreateForLanguage(string? languageCode)
    {
        string Label(string key) => StringLocalizer.Get(key, languageCode);
        return new[]
        {
            Create("overview", Label(AppStringKeys.NavOverview)),
            Create("system", Label(AppStringKeys.NavSystem)),
            Create("cpu", Label(AppStringKeys.NavCpu)),
            Create("ram", Label(AppStringKeys.NavRam)),
            Create("storage", Label(AppStringKeys.NavStorage)),
            Create("battery", Label(AppStringKeys.NavBattery)),
            Create("gpu", Label(AppStringKeys.NavGpu)),
            Create("display", Label(AppStringKeys.NavDisplay)),
            Create("network", Label(AppStringKeys.NavNetwork)),
            Create("motherboard", Label(AppStringKeys.NavMotherboard)),
            Create("security", Label(AppStringKeys.NavSecurity)),
            Create("drivers", Label(AppStringKeys.NavDrivers)),
            Create("updates", Label(AppStringKeys.NavUpdates)),
            Create("startup", Label(AppStringKeys.NavStartup)),
            // Crash history sits with the diagnostics pages (its collector
            // category is Diagnostics) and stays above the ~16-item fold line,
            // so History keeps its visible slot.
            Create("crash", Label(AppStringKeys.NavCrash)),
            // History sits here, inside the ~16 items the left rail shows at the
            // default 1280x800 window, on purpose: it was second-to-last and the
            // rail overflows past ~16 items, so History rendered below the fold
            // and was effectively unreachable in v1.0.1 even though it was
            // registered. Keep it above that fold line.
            Create("history", Label(AppStringKeys.NavHistory)),
            // U8 left-rail decision: the rail fits ~16 items and History must
            // stay inside that visible region (pinned by
            // Navigation_HistoryIsWithinVisibleRegion), so Software sits here,
            // directly after History, instead of with the other collectors.
            // Grouping the rail or folding the audit into the System page were
            // both rejected: grouping is a bigger UI change than a MEDIUM item
            // should carry, and the audit is hundreds of rows that would bloat
            // the System detail table. The rail scrolls with an always-visible
            // scrollbar (the v1.4 durable fix), so Software stays reachable.
            Create("software", Label(AppStringKeys.NavSoftware)),
            Create("tests", Label(AppStringKeys.NavTests)),
            Create("report", Label(AppStringKeys.NavReport)),
            Create("compare", Label(AppStringKeys.NavCompare)),
            // Technician mode (v2.0.3): the fleet table sits with the other
            // multi-machine pages (History, Compare), after Compare and
            // before Settings, so History keeps its visible slot (index
            // 0-15, pinned by Navigation_HistoryIsWithinVisibleRegion).
            Create("fleet", Label(AppStringKeys.NavFleet)),
            Create("settings", Label(AppStringKeys.NavSettings)),
        };
    }

    private static NavItemViewModel Create(string key, string label) =>
        new(key, label, IconLibrary.Get(key));
}
