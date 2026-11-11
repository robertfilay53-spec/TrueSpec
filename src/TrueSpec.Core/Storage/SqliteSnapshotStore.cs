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

        // Pooling is off on purpose: the store opens a short-lived
        // connection per operation, so pooling buys nothing — and a pooled
        // connection keeps the database file locked after Dispose, which
        // blocks deleting/moving the file on Windows (the file lock is held
        // until the pool is cleared or the process exits).
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
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

    public async Task<SystemSnapshot?> GetSnapshotAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT payload_json
            FROM snapshots
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString("D"));

        var payload = (string?)await command.ExecuteScalarAsync(cancellationToken);
        if (payload is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SystemSnapshot>(payload, JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // A snapshot written by an older or corrupt build is skipped
            // instead of crashing the comparison view.
            return null;
        }
    }

    public async Task<IReadOnlyList<SnapshotSummary>> GetHistoryAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        // Clamp the limit: a caller must never be able to ask the store to
        // read an unbounded number of rows.
        limit = Math.Clamp(limit, 1, 500);

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
            // A corrupt row must not hide the whole history: skip rows whose
            // id, date or score cannot be read as a real value instead of
            // throwing. The score column is REAL-affinity, so a text value
            // like "not-a-number" would otherwise read back as a fake 0.0
            // through GetDouble, and an out-of-range score is not a score
            // this app ever writes (the engine clamps to 0-100).
            try
            {
                if (!Guid.TryParse(reader.GetString(0), out var id) ||
                    !DateTimeOffset.TryParse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.None, out var capturedAt))
                {
                    continue;
                }

                var score = ReadScore(reader.GetValue(3));
                if (score is null)
                {
                    continue;
                }

                results.Add(new SnapshotSummary(
                    id,
                    capturedAt,
                    reader.GetString(2),
                    score.Value,
                    reader.GetString(4)));
            }
            catch (Exception ex) when (ex is InvalidCastException or FormatException or IndexOutOfRangeException or InvalidOperationException)
            {
                continue;
            }
        }

        return results;
    }

    private static double? ReadScore(object value) => value switch
    {
        double score => double.IsFinite(score) && score is >= 0d and <= 100d ? score : null,
        float score => double.IsFinite(score) && score is >= 0f and <= 100f ? score : null,
        long score => score is >= 0L and <= 100L ? score : null,
        int score => score is >= 0 and <= 100 ? score : null,
        string text => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && double.IsFinite(parsed) && parsed is >= 0d and <= 100d
                ? parsed
                : null,
        _ => null,
    };
}
