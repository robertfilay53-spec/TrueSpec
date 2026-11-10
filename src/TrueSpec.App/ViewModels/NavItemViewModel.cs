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
        Create("tests", "Tests"),
        Create("report", "Report"),
        Create("settings", "Settings"),
    };

    private static NavItemViewModel Create(string key, string label) =>
        new(key, label, IconLibrary.Get(key));
}
