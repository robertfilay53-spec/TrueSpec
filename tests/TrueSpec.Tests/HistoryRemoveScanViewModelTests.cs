using TrueSpec.App.ViewModels;
using TrueSpec.Core.Storage;

namespace TrueSpec.Tests;

/// <summary>
/// History-page individual-scan deletion (v2.1.3): two saved scans for
/// the current machine in a real SQLite store, and the History ViewModel's
/// Remove button on a row deletes exactly that one scan.
/// Windows leg only (it drives TrueSpec.App's ViewModel), like
/// HistorySensorLogViewModelTests: excluded from the net10.0 leg in the
/// csproj.
/// </summary>
public class HistoryRemoveScanViewModelTests
{
    private static async Task<SqliteSnapshotStore> StoreWithAsync(
        string directory,
        params (string Machine, double Score, string Wear, DateTimeOffset At)[] scans)
    {
        var databasePath = Path.Combine(directory, "snapshots.db");
        var store = new SqliteSnapshotStore(databasePath);
        await store.InitializeAsync();
        foreach (var scan in scans)
        {
            await store.SaveAsync(TestSnapshots.HistorySnapshot(
                scan.Score,
                "90.0%",
                "10",
                scan.Wear,
                "100",
                scan.At,
                machineName: scan.Machine));
        }

        return store;
    }

    /// <summary>
    /// The ViewModel loads its trend on a fire-and-forget task from its
    /// constructor: wait for it instead of sleeping a fixed time. The
    /// status starts as "Loading snapshot history..." and changes when
    /// the load finishes (or fails).
    /// </summary>
    private static async Task WaitForLoadAsync(HistoryViewModel viewModel)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (viewModel.StatusMessage == "Loading snapshot history..." && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(50);
        }
    }

    [Fact]
    public async Task RemoveScanCommand_FirstClickArms_SecondClickDeletesOnlyThatScan()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));

        try
        {
            var baseTime = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
            var store = await StoreWithAsync(
                directory,
                ("Test Laptop", 90d, "2%", baseTime),
                ("Test Laptop", 85d, "5%", baseTime.AddHours(1)),
                ("OTHER-PC", 70d, "9%", baseTime));

            var viewModel = new HistoryViewModel(store, TestSnapshots.Healthy());
            await WaitForLoadAsync(viewModel);

            Assert.True(viewModel.HasHistory, "History never loaded from the store.");
            Assert.Equal(2, viewModel.Rows.Count);
            var firstRow = viewModel.Rows[0];
            Assert.Equal("2026-11-10 09:00", firstRow.CapturedAt.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture));
            var deletedId = firstRow.Id;

            // First click only arms: nothing is deleted, and the button
            // now reads "Confirm remove".
            await viewModel.RemoveScanCommand.ExecuteAsync(firstRow);

            Assert.True(firstRow.IsRemoveArmed);
            Assert.Equal("Confirm remove", firstRow.RemoveButtonText);
            Assert.Equal(2, viewModel.Rows.Count);
            Assert.Contains("2026-11-10 09:00", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("nothing is deleted until then", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Equal(3, (await store.GetHistoryAsync()).Count);

            // Second click deletes exactly that one scan and the trend
            // reloads: only the 10:00 scan of Test Laptop remains, and
            // the other machine's scan is untouched.
            await viewModel.RemoveScanCommand.ExecuteAsync(firstRow);

            Assert.Single(viewModel.Rows);
            Assert.Equal("2026-11-10 10:00", viewModel.Rows[0].CapturedAt.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture));
            Assert.Contains("Removed the scan from 2026-11-10 09:00", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("1 scan remains", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Null(await store.GetSnapshotAsync(deletedId));
            Assert.Equal(2, (await store.GetHistoryAsync()).Count);
            Assert.Equal(new[] { "OTHER-PC", "Test Laptop" }, await store.GetMachineNamesAsync());
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
    public async Task RemoveScanCommand_AlreadyGone_ReportsNothingToRemove()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));

        try
        {
            var baseTime = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
            var store = await StoreWithAsync(
                directory,
                ("Test Laptop", 90d, "2%", baseTime));

            var viewModel = new HistoryViewModel(store, TestSnapshots.Healthy());
            await WaitForLoadAsync(viewModel);

            Assert.Single(viewModel.Rows);
            var row = viewModel.Rows[0];
            await viewModel.RemoveScanCommand.ExecuteAsync(row);
            Assert.True(row.IsRemoveArmed);

            // The scan vanishes between the arm and the confirm (another
            // copy of the app, or a re-import cycle): this direct delete
            // removes the one saved scan, so the confirm that follows
            // must say so honestly instead of claiming a deletion.
            Assert.Equal(1, await store.DeleteSnapshotAsync(row.Id));
            await viewModel.RemoveScanCommand.ExecuteAsync(row);

            Assert.False(viewModel.HasHistory);
            Assert.Empty(viewModel.Rows);
            Assert.Contains("Nothing to remove", viewModel.StatusMessage, StringComparison.Ordinal);
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
    public async Task RemoveScanCommand_LastScan_LeavesEmptyState()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));

        try
        {
            var baseTime = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
            var store = await StoreWithAsync(
                directory,
                ("Test Laptop", 90d, "2%", baseTime));

            var viewModel = new HistoryViewModel(store, TestSnapshots.Healthy());
            await WaitForLoadAsync(viewModel);

            var row = viewModel.Rows[0];
            await viewModel.RemoveScanCommand.ExecuteAsync(row);
            await viewModel.RemoveScanCommand.ExecuteAsync(row);

            Assert.False(viewModel.HasHistory);
            Assert.Empty(viewModel.Rows);
            Assert.Contains("Removed the scan from 2026-11-10 09:00", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("No saved scans remain", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Empty(await store.GetHistoryAsync());
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
    public async Task RemoveScanCommand_NullStore_ReportsUnavailable()
    {
        var viewModel = new HistoryViewModel(null, TestSnapshots.Healthy());
        await WaitForLoadAsync(viewModel);

        // No store, so no rows exist: drive the command with a row built
        // by hand, the way the view would pass one.
        var row = new HistoryRowViewModel(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            80d,
            null,
            null,
            null,
            null,
            removeScanCommand: viewModel.RemoveScanCommand);

        await viewModel.RemoveScanCommand.ExecuteAsync(row);

        Assert.Equal("Remove is unavailable: snapshot history is unavailable on this machine.", viewModel.StatusMessage);
    }

    [Fact]
    public void HistoryRowViewModel_RemoveButtonText_FollowsArmedState()
    {
        var id = Guid.NewGuid();
        var row = new HistoryRowViewModel(
            id,
            new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero),
            90d,
            90d,
            10d,
            2d,
            100d);

        Assert.Equal(id, row.Id);
        Assert.Equal("Remove", row.RemoveButtonText);
        Assert.False(row.IsRemoveArmed);

        row.IsRemoveArmed = true;

        Assert.Equal("Confirm remove", row.RemoveButtonText);
    }
}
