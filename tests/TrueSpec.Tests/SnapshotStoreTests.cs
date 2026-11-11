using Microsoft.Data.Sqlite;
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

    [Fact]
    public async Task SqliteStore_CorruptRowsAreSkippedNotFatal()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");

        try
        {
            var snapshot = TestSnapshots.Healthy();
            var store = new SqliteSnapshotStore(databasePath);

            await store.InitializeAsync();
            await store.SaveAsync(snapshot);

            // A row with an unparseable id and a row with an unparseable
            // date, as a damaged database could contain. History must still
            // return the good snapshot instead of throwing.
            await using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    INSERT INTO snapshots (id, captured_at, machine_name, overall_score, verdict, payload_json)
                    VALUES ('not-a-guid', 'not-a-date', 'Corrupt', 0, 'Worn', '{}'),
                           ($goodId, 'also-not-a-date', 'Corrupt', 0, 'Worn', '{}');
                    """;
                command.Parameters.AddWithValue("$goodId", Guid.NewGuid().ToString("D"));
                await command.ExecuteNonQueryAsync();
            }

            var history = await store.GetHistoryAsync();

            var saved = Assert.Single(history);
            Assert.Equal(snapshot.Id, saved.Id);
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
