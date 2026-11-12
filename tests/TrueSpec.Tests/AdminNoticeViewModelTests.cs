using System.Windows.Input;
using TrueSpec.App.ViewModels;
using TrueSpec.Core.Health;
using TrueSpec.Core.Models;
using TrueSpec.Core.Sensors;

namespace TrueSpec.Tests;

/// <summary>
/// Windows-leg tests for the F3 Overview banner: a standard-user scan with
/// silent admin-gated readings shows "Run as administrator for full results";
/// an elevated process, or a scan with full results, shows no banner. The
/// timer is never waited on: the tests read the banner straight after
/// construction, the way SensorAlertViewModelTests drives the ViewModel.
/// </summary>
public class AdminNoticeViewModelTests
{
    private sealed class TestCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
        }
    }

    private sealed class QuietSensorMonitor : ISensorMonitor
    {
        public bool IsAvailable => true;

        public string StatusText => "test sensors";

        public Task<SensorSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(SensorSnapshot.Unavailable("test"));
    }

    [Fact]
    public void StandardUser_SilentCpuTemperature_ShowsBanner()
    {
        using var overview = CreateOverview(WithSilentCpuTemp(TestSnapshots.Healthy()), isElevated: false);

        Assert.True(overview.HasAdminNotice);
        Assert.StartsWith(AdminElevationNotice.NoticeText, overview.AdminNoticeText);
        Assert.Contains("CPU temperature", overview.AdminNoticeText);
    }

    [Fact]
    public void StandardUser_FullResults_NoBanner()
    {
        using var overview = CreateOverview(TestSnapshots.Healthy(), isElevated: false);

        Assert.False(overview.HasAdminNotice);
        Assert.Equal(string.Empty, overview.AdminNoticeText);
    }

    [Fact]
    public void ElevatedProcess_SilentCpuTemperature_NoBanner()
    {
        using var overview = CreateOverview(WithSilentCpuTemp(TestSnapshots.Healthy()), isElevated: true);

        Assert.False(overview.HasAdminNotice);
        Assert.Equal(string.Empty, overview.AdminNoticeText);
    }

    [Fact]
    public void RefreshingSnapshot_RecomputesBanner()
    {
        using var overview = CreateOverview(TestSnapshots.Healthy(), isElevated: false);
        Assert.False(overview.HasAdminNotice);

        overview.UpdateSnapshot(WithSilentCpuTemp(TestSnapshots.Healthy()), isScanning: false);

        Assert.True(overview.HasAdminNotice);
        Assert.Contains("CPU temperature", overview.AdminNoticeText);

        overview.UpdateSnapshot(TestSnapshots.Healthy(), isScanning: false);

        Assert.False(overview.HasAdminNotice);
    }

    private static OverviewViewModel CreateOverview(SystemSnapshot snapshot, bool isElevated) => new(
        snapshot,
        _ => { },
        new TestCommand(),
        new TestCommand(),
        new QuietSensorMonitor(),
        isElevated: isElevated);

    private static SystemSnapshot WithSilentCpuTemp(SystemSnapshot snapshot)
    {
        var components = snapshot.Components.Select(component =>
            string.Equals(component.Id, "cpu", StringComparison.OrdinalIgnoreCase)
                ? component with
                {
                    Specs = new[] { new SpecValue("Package temperature", SpecLabels.NotReportedByHardware, "LibreHardwareMonitor") },
                }
                : component).ToList();
        return snapshot with { Components = components };
    }
}
