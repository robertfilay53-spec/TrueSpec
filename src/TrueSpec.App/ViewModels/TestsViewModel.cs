using System.Collections.Generic;

namespace TrueSpec.App.ViewModels;

/// <summary>Tests page placeholder. The launchers arrive in P8.</summary>
public sealed class TestsViewModel : ViewModelBase
{
    public IReadOnlyList<TestItemViewModel> Tests { get; } = new[]
    {
        new TestItemViewModel(
            "Quick memory test",
            "Schedules the Windows Memory Diagnostic and explains the reboot.",
            "Planned for P8"),
        new TestItemViewModel(
            "Disk read speed",
            "Measures sequential read speed and compares it with the drive class.",
            "Planned for P8"),
        new TestItemViewModel(
            "Battery runtime estimate",
            "Estimates real runtime from current wear and charge rate.",
            "Planned for P8"),
        new TestItemViewModel(
            "Wi-Fi speed test",
            "Optional internet test. Off by default because the app is offline first.",
            "Planned for P8"),
        new TestItemViewModel(
            "Stress test",
            "Loads the CPU and GPU while watching temperatures for throttling.",
            "Planned for v2"),
    };
}

public sealed record TestItemViewModel(string Name, string Description, string Status);
