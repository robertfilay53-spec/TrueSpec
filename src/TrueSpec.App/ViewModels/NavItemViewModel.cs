using System.Collections.Generic;

namespace TrueSpec.App.ViewModels;

/// <summary>One entry in the left rail navigation.</summary>
public sealed record NavItemViewModel(string Key, string Label, string IconData)
{
    public static IReadOnlyList<NavItemViewModel> CreateDefault() => new[]
    {
        Create("overview", "Overview"),
        Create("system", "System"),
        Create("cpu", "CPU"),
        Create("ram", "RAM"),
        Create("storage", "Storage"),
        Create("battery", "Battery"),
        Create("gpu", "GPU"),
        Create("display", "Display"),
        Create("network", "Network"),
        Create("motherboard", "Motherboard"),
        Create("security", "Security"),
        Create("drivers", "Drivers"),
        Create("updates", "Updates"),
        Create("startup", "Startup"),
        // Crash history sits with the diagnostics pages (its collector
        // category is Diagnostics) and stays above the ~16-item fold line,
        // so History keeps its visible slot.
        Create("crash", "Crash history"),
        // History sits here, inside the ~16 items the left rail shows at the
        // default 1280x800 window, on purpose: it was second-to-last and the
        // rail overflows past ~16 items, so History rendered below the fold
        // and was effectively unreachable in v1.0.1 even though it was
        // registered. Keep it above that fold line.
        Create("history", "History"),
        // U8 left-rail decision: the rail fits ~16 items and History must
        // stay inside that visible region (pinned by
        // Navigation_HistoryIsWithinVisibleRegion), so Software sits here,
        // directly after History, instead of with the other collectors.
        // Grouping the rail or folding the audit into the System page were
        // both rejected: grouping is a bigger UI change than a MEDIUM item
        // should carry, and the audit is hundreds of rows that would bloat
        // the System detail table. The rail scrolls with an always-visible
        // scrollbar (the v1.4 durable fix), so Software stays reachable.
        Create("software", "Software"),
        Create("tests", "Tests"),
        Create("report", "Report"),
        Create("compare", "Compare"),
        // Technician mode (v2.0.3): the fleet table sits with the other
        // multi-machine pages (History, Compare), after Compare and
        // before Settings, so History keeps its visible slot (index
        // 0-15, pinned by Navigation_HistoryIsWithinVisibleRegion).
        Create("fleet", "Fleet"),
        Create("settings", "Settings"),
    };

    private static NavItemViewModel Create(string key, string label) =>
        new(key, label, IconLibrary.Get(key));
}
