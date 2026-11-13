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

    /// <summary>
    /// Snapshots kept per machine. Reads were always clamped, but saves
    /// only INSERTed, so rows accumulated unbounded; a machine scanned
    /// daily would grow the file forever. The cap mirrors the sensor log
    /// next door (<see cref="TrueSpec.Core.Sensors.SensorLogWriter"/>:
    /// 10,000 rows, trim to 8,000). 200 snapshots is years of weekly scans
    /// and far more than any comparison or trend ever reads (50 at most),
    /// so pruning the oldest beyond the cap loses nothing the UI shows.
    /// Per machine: one history file can hold several machines, and a
    /// global cap would evict another machine's whole history.
    /// </summary>
    public const int MaxSnapshots = 200;

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
                payload_json TEXT NOT NULL,
                machine_dna_hash TEXT,
                machine_dna_parts TEXT
            );
            CREATE INDEX IF NOT EXISTS ix_snapshots_captured_at
                ON snapshots (captured_at DESC);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        // Databases created before the DNA columns existed need them
        // added: CREATE TABLE IF NOT EXISTS leaves an old table alone.
        await EnsureDnaColumnsAsync(connection, cancellationToken);
    }

    /// <summary>
    /// Adds the DNA columns to a pre-existing snapshots table that was
    /// created without them. Rows written before DNA existed keep NULL in
    /// both columns and read as "DNA not recorded", never as a mismatch.
    /// </summary>
    private static async Task EnsureDnaColumnsAsync(
        SqliteConnection connection, CancellationToken cancellationToken)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA table_info(snapshots);";
            await using var reader = await pragma.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                existing.Add(reader.GetString(1));
            }
        }

        // Column names are constants, never user input.
        foreach (var (column, type) in new[]
                 {
                     ("machine_dna_hash", "TEXT"),
                     ("machine_dna_parts", "TEXT"),
                 })
        {
            if (existing.Contains(column))
            {
                continue;
            }

            await using var alter = connection.CreateCommand();
            alter.CommandText = $"ALTER TABLE snapshots ADD COLUMN {column} {type};";
            await alter.ExecuteNonQueryAsync(cancellationToken);
        }
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
            INSERT INTO snapshots (id, captured_at, machine_name, overall_score, verdict, payload_json, machine_dna_hash, machine_dna_parts)
            VALUES ($id, $capturedAt, $machineName, $score, $verdict, $payload, $dnaHash, $dnaParts)
            ON CONFLICT(id) DO UPDATE SET
                captured_at = excluded.captured_at,
                machine_name = excluded.machine_name,
                overall_score = excluded.overall_score,
                verdict = excluded.verdict,
                payload_json = excluded.payload_json,
                machine_dna_hash = excluded.machine_dna_hash,
                machine_dna_parts = excluded.machine_dna_parts;
            """;
        command.Parameters.AddWithValue("$id", snapshot.Id.ToString("D"));
        command.Parameters.AddWithValue("$capturedAt", snapshot.CapturedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$machineName", snapshot.MachineName);
        command.Parameters.AddWithValue("$score", snapshot.Score.OverallScore);
        command.Parameters.AddWithValue("$verdict", snapshot.Score.Verdict.ToString());
        command.Parameters.AddWithValue("$payload", payload);
        command.Parameters.AddWithValue("$dnaHash", snapshot.MachineDnaHash ?? (object)DBNull.Value);
        // NULL parts means "DNA not recorded" (a snapshot from before DNA
        // existed); an empty JSON array means a scan recorded DNA but no
        // part reported a usable serial ("DNA not available"). The two
        // states must stay distinguishable, so the column is NULL only
        // when the snapshot carries no parts at all.
        command.Parameters.AddWithValue("$dnaParts", snapshot.MachineDnaParts is null
            ? DBNull.Value
            : JsonSerializer.Serialize(snapshot.MachineDnaParts, JsonOptions));

        await command.ExecuteNonQueryAsync(cancellationToken);

        // Retention: keep only the newest MaxSnapshots rows PER MACHINE.
        // The prune runs on every save so the file can never grow
        // unbounded; the DELETE is a single indexed statement per machine
        // and the newest rows are the ones every reader (history list,
        // comparison, trends) uses. Per-machine, not global: one history
        // file can hold several machines (a buyer-kit USB stick scans many
        // machines into the same database), and a global cap would evict
        // another machine's whole history the moment one machine was
        // scanned 200 times. History still keys on machine name today
        // (a renamable string); this prune does not rework that keying.
        var machineNames = new List<string>();
        await using (var names = connection.CreateCommand())
        {
            names.CommandText = "SELECT DISTINCT machine_name FROM snapshots;";
            await using var reader = await names.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0))
                {
                    machineNames.Add(reader.GetString(0));
                }
            }
        }

        foreach (var machine in machineNames)
        {
            await using var prune = connection.CreateCommand();
            prune.CommandText =
                """
                DELETE FROM snapshots
                WHERE machine_name = $machine
                  AND id NOT IN (
                      SELECT id FROM snapshots
                      WHERE machine_name = $machine
                      ORDER BY captured_at DESC, id DESC
                      LIMIT $keep
                  );
                """;
            prune.Parameters.AddWithValue("$machine", machine);
            prune.Parameters.AddWithValue("$keep", MaxSnapshots);
            await prune.ExecuteNonQueryAsync(cancellationToken);
        }
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

    public async Task<IReadOnlyList<string>> GetMachineNamesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        // Constant SQL, no parameters and no concatenation: machine names
        // are data, never part of the query.
        command.CommandText =
            """
            SELECT DISTINCT machine_name
            FROM snapshots
            ORDER BY machine_name;
            """;

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                var name = reader.GetString(0);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name);
                }
            }
        }

        return names;
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

        // Chronological order by the parsed timestamp, not by the stored
        // string: captured_at is ISO with an offset, and two ISO strings
        // can sort lexicographically out of order across a DST change
        // (different offsets), so the SQL ORDER BY is only a first cut.
        results.Sort(static (a, b) => b.CapturedAt.CompareTo(a.CapturedAt));
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
