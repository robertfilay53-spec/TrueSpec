using TrueSpec.Core.Storage;

namespace TrueSpec.Tests;

public class SnapshotStoreTests
{
    [Fact]
    public async Task SqliteStore_SavesAndReadsSnapshotHistory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");

        try
        {
            var snapshot = TestSnapshots.Healthy();
            var store = new SqliteSnapshotStore(databasePath);

            await store.InitializeAsync();
            await store.SaveAsync(snapshot);
            var history = await store.GetHistoryAsync();

            var saved = Assert.Single(history);
            Assert.Equal(snapshot.Id, saved.Id);
            Assert.Equal(snapshot.MachineName, saved.MachineName);
            Assert.Equal(snapshot.Score.OverallScore, saved.OverallScore, 3);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
