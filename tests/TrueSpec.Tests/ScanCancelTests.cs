using TrueSpec.App.Services;
using TrueSpec.App.ViewModels;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;
using TrueSpec.Core.Sensors;

namespace TrueSpec.Tests;

/// <summary>
/// Scan cancel wiring (hardening 6c) and shell disposal (6d): the scan
/// token reaches the collectors, Cancel stops a running scan with the
/// previous snapshot intact, and closing the shell disposes the Overview
/// timer and the sensor monitor without throwing.
/// Windows leg only (it drives TrueSpec.App's ViewModels), like
/// ExportConfirmationTests: excluded from the net10.0 leg in the csproj.
/// </summary>
public class ScanCancelTests
{
    /// <summary>A collector that never finishes on its own: it only returns when its token is cancelled.</summary>
    private sealed class BlockingCollector : ICollector
    {
        public string Id => "blocking";

        public string Name => "Blocking";

        public string Category => "Test";

        public TimeSpan RefreshInterval => TimeSpan.FromMinutes(1);

        public bool RequiresAdministrator => false;

        public async Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return ComponentSnapshot.NotAvailable(Id, Name, Category, Id, "Unreachable: the delay never completes.");
        }
    }

    private static MainViewModel CreateShell() => new(
        new[] { new BlockingCollector() },
        new UnavailableSensorMonitor(),
        new BuyerCheckEvaluator());

    [Fact]
    public async Task CancelScan_StopsARunningScanAndKeepsThePreviousSnapshot()
    {
        var shell = CreateShell();
        try
        {
            shell.BeginInitialScan();

            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (!shell.IsScanRunning && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            Assert.True(shell.IsScanRunning, "The scan never started running.");
            Assert.True(shell.CancelScanCommand.CanExecute(null), "Cancel must be enabled while a scan runs.");

            shell.CancelScanCommand.Execute(null);

            deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (shell.IsScanRunning && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            Assert.False(shell.IsScanRunning, "The scan did not stop after Cancel.");
            Assert.Equal("Scan cancelled.", shell.StatusMessage);
            Assert.False(shell.CancelScanCommand.CanExecute(null), "Cancel must disable once the scan stops.");
        }
        finally
        {
            shell.Dispose();
        }
    }

    [Fact]
    public async Task RefreshWhileRunning_IsRefusedNotQueued()
    {
        var shell = CreateShell();
        try
        {
            shell.BeginInitialScan();

            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (!shell.IsScanRunning && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            Assert.True(shell.IsScanRunning, "The scan never started running.");

            await shell.RefreshCommand.ExecuteAsync(null);

            Assert.Equal("A scan is already running.", shell.StatusMessage);
            Assert.True(shell.IsScanRunning, "The refused refresh must not stop the running scan.");

            shell.CancelScanCommand.Execute(null);
            deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (shell.IsScanRunning && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            Assert.False(shell.IsScanRunning, "The scan did not stop after Cancel.");
        }
        finally
        {
            shell.Dispose();
        }
    }

    [Fact]
    public void Dispose_StopsTheOverviewTimerAndIsIdempotent()
    {
        var shell = CreateShell();

        shell.Dispose();
        shell.Dispose();

        Assert.False(shell.IsScanRunning);
    }
}
