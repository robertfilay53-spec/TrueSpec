using TrueSpec.App.ViewModels;
using TrueSpec.Core.History;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;
using TrueSpec.Core.Storage;

namespace TrueSpec.Tests;

/// <summary>
/// U4 end to end: four synthetic snapshots in a real SQLite store, and the
/// History page ViewModel surfaces the forecast text for both metrics.
/// Windows leg only (it drives TrueSpec.App's ViewModel), like
/// ScoreAgreementTests: excluded from the net10.0 leg in the csproj.
/// </summary>
public class HistoryForecastViewModelTests
{
    [Fact]
    public async Task FourSyntheticSnapshots_ViewModelSurfacesBothForecastRanges()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");

        try
        {
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();

            // Battery health 90 to 87 and SSD wear 2% to 8%, one scan
            // every 30 days over 90 days: the same steady decline the
            // Core tests fit, so the ranges must agree with them.
            var start = DateTimeOffset.UtcNow.AddDays(-90);
            var current = SnapshotWith(90d, "90.0%", "2%", start);
            await store.SaveAsync(current);
            await store.SaveAsync(SnapshotWith(89d, "89.0%", "4%", start.AddDays(30)));
            await store.SaveAsync(SnapshotWith(88d, "88.0%", "6%", start.AddDays(60)));
            await store.SaveAsync(SnapshotWith(87d, "87.0%", "8%", start.AddDays(90)));

            var viewModel = new HistoryViewModel(store, current);

            // The ViewModel loads history on a fire-and-forget task from
            // its constructor: wait for it instead of sleeping a fixed time.
            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (!viewModel.HasHistory && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            Assert.True(viewModel.HasHistory, "History never loaded from the store.");
            Assert.True(viewModel.HasBatteryForecast, "Battery forecast card is hidden with 4 scans of history.");
            Assert.True(viewModel.HasStorageWearForecast, "Storage-wear forecast card is hidden with 4 scans of history.");
            Assert.Contains("23 to 30 months", viewModel.BatteryForecastText, StringComparison.Ordinal);
            Assert.Contains("14 to 18 months", viewModel.StorageWearForecastText, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task OneScan_ViewModelRefusesToForecast()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");

        try
        {
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();

            var current = SnapshotWith(90d, "90.0%", "2%", DateTimeOffset.UtcNow);
            await store.SaveAsync(current);

            var viewModel = new HistoryViewModel(store, current);

            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (viewModel.StatusMessage == "Loading snapshot history..." && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            // One scan is history, but the forecast card is shown, not
            // hidden: it says how many more scans are needed.
            Assert.True(viewModel.HasBatteryForecast, "Battery forecast card should say more scans are needed.");
            Assert.True(viewModel.HasStorageWearForecast, "Storage-wear forecast card should say more scans are needed.");
            Assert.Contains("2 more scans needed", viewModel.BatteryForecastText, StringComparison.Ordinal);
            Assert.DoesNotContain("months", viewModel.BatteryForecastText, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    /// <summary>
    /// A snapshot shaped like the real collectors write it, after
    /// HistoryTrendTests.SnapshotWith: battery health and storage wear live
    /// in spec values, not in HealthPercent.
    /// </summary>
    private static SystemSnapshot SnapshotWith(
        double score, string batteryHealth, string wear, DateTimeOffset capturedAt)
    {
        var wearValue = double.Parse(wear.TrimEnd('%'), System.Globalization.CultureInfo.InvariantCulture);
        var components = new[]
        {
            new ComponentSnapshot(
                "battery", "Battery", "Battery", "battery", "Test battery", null,
                new[]
                {
                    new SpecValue("Health", batteryHealth, "Full charge capacity / design capacity"),
                    new SpecValue("Cycle count", "10", "CIM root\\WMI BatteryCycleCount"),
                },
                "Battery health compares full charge capacity with design capacity.",
                capturedAt),
            new ComponentSnapshot(
                "storage", "Storage", "Storage", "storage", "Test drive", 100d - wearValue,
                new[]
                {
                    new SpecValue("Wear level", wear, "Get-StorageReliabilityCounter.Wear"),
                    new SpecValue("Power-on hours", "100", "Get-StorageReliabilityCounter.PowerOnHours"),
                },
                "Drive health comes from the manufacturer wear counter.",
                capturedAt),
        };
        var scoreResult = new ScoreResult(
            score,
            Verdict.UsedHealthy,
            new[] { new ComponentScore("storage", 100d - wearValue, 10d, 10d, 10d) },
            10d);
        return new SystemSnapshot(
            Guid.NewGuid(),
            capturedAt,
            "Test Laptop",
            "Windows 11 Pro",
            components,
            scoreResult,
            new BuyerCheckEvaluator().Evaluate(components, null));
    }
}
