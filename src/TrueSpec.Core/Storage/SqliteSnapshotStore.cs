namespace TrueSpec.Core.Storage;

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using TrueSpec.Core.Models;

/// <summary>
/// SQLite backed snapshot store. The full snapshot is stored as JSON next to a
/// few indexed summary columns so the history list is cheap to read.
/// </summary>
public sealed class SqliteSnapshotStore : ISnapshotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _connectionString;

    public SqliteSnapshotStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var fullPath = Path.GetFullPath(databasePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS snapshots (
                id TEXT NOT NULL PRIMARY KEY,
                captured_at TEXT NOT NULL,
                machine_name TEXT NOT NULL,
                overall_score REAL NOT NULL,
                verdict TEXT NOT NULL,
                payload_json TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_snapshots_captured_at
                ON snapshots (captured_at DESC);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveAsync(SystemSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var payload = JsonSerializer.Serialize(snapshot, JsonOptions);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO snapshots (id, captured_at, machine_name, overall_score, verdict, payload_json)
            VALUES ($id, $capturedAt, $machineName, $score, $verdict, $payload)
            ON CONFLICT(id) DO UPDATE SET
                captured_at = excluded.captured_at,
                machine_name = excluded.machine_name,
                overall_score = excluded.overall_score,
                verdict = excluded.verdict,
                payload_json = excluded.payload_json;
            """;
        command.Parameters.AddWithValue("$id", snapshot.Id.ToString("D"));
        command.Parameters.AddWithValue("$capturedAt", snapshot.CapturedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$machineName", snapshot.MachineName);
        command.Parameters.AddWithValue("$score", snapshot.Score.OverallScore);
        command.Parameters.AddWithValue("$verdict", snapshot.Score.Verdict.ToString());
        command.Parameters.AddWithValue("$payload", payload);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SnapshotSummary>> GetHistoryAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            return Array.Empty<SnapshotSummary>();
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, captured_at, machine_name, overall_score, verdict
            FROM snapshots
            ORDER BY captured_at DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var results = new List<SnapshotSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new SnapshotSummary(
                Guid.Parse(reader.GetString(0)),
                DateTimeOffset.Parse(reader.GetString(1), CultureInfo.InvariantCulture),
                reader.GetString(2),
                reader.GetDouble(3),
                reader.GetString(4)));
        }

        return results;
    }
}
