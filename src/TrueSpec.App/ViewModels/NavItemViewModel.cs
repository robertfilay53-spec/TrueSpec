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
        // History sits here, inside the ~16 items the left rail shows at the
        // default 1280x800 window, on purpose: it was second-to-last and the
        // rail overflows past ~16 items, so History rendered below the fold
        // and was effectively unreachable in v1.0.1 even though it was
        // registered. Keep it above that fold line.
        Create("history", "History"),
        Create("tests", "Tests"),
        Create("report", "Report"),
        Create("compare", "Compare"),
        Create("settings", "Settings"),
    };

    private static NavItemViewModel Create(string key, string label) =>
        new(key, label, IconLibrary.Get(key));
}
