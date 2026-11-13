using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using TrueSpec.Core.Comparison;
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
    {        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
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
            // Pooling is off so this connection releases the file on Dispose:
            // a pooled connection would keep snapshots.db locked and the
            // cleanup in the finally block would fail on Windows.
            await using (var connection = new SqliteConnection($"Data Source={databasePath};Pooling=false"))
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

    [Fact]
    public async Task GetMachineNamesAsync_ReturnsDistinctNamesAlphabetically()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");

        try
        {
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();
            await store.SaveAsync(TestSnapshots.Healthy());
            await store.SaveAsync(TestSnapshots.Healthy() with { MachineName = "Alpha Laptop" });
            await store.SaveAsync(TestSnapshots.Healthy() with { MachineName = "Test Laptop" });

            var names = await store.GetMachineNamesAsync();

            Assert.Equal(new[] { "Alpha Laptop", "Test Laptop" }, names);
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
    public async Task GetMachineNamesAsync_EmptyStore_ReturnsEmptyList()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");

        try
        {
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();

            var names = await store.GetMachineNamesAsync();

            Assert.Empty(names);
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
    public async Task SqliteStore_DnaRoundTripsThroughTheStore()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");

        try
        {
            var dna = MachineDna.Build(new[]
            {
                (MachineDnaPartKind.Board, "Board", (string?)"BOARD123456789"),
                (MachineDnaPartKind.Bios, "BIOS", (string?)"BIOS987654321"),
                (MachineDnaPartKind.Disk, "Disk 0", (string?)"DISK-AAAA-1111"),
            });
            var snapshot = TestSnapshots.Healthy() with
            {
                MachineDnaHash = dna.Hash,
                MachineDnaParts = dna.Parts,
            };

            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();
            await store.SaveAsync(snapshot);

            var loaded = await store.GetSnapshotAsync(snapshot.Id);
            Assert.NotNull(loaded);
            Assert.Equal(dna.Hash, loaded!.MachineDnaHash);
            Assert.Equal(dna.Hash, loaded.MachineDnaStatus);
            Assert.NotNull(loaded.MachineDnaParts);
            var parts = loaded.MachineDnaParts!;
            Assert.Equal(3, parts.Count);
            // Only the truncated serial survives the round-trip: the full
            // serial must never be stored or read back.
            Assert.Equal("BOAR...6789", parts[0].SerialTruncated);
            Assert.All(parts, p => Assert.DoesNotContain("BOARD123456789", p.SerialTruncated));

            // The DNA columns carry the same values, so a future query can
            // read identity without parsing the payload.
            await using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=false");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT machine_dna_hash, machine_dna_parts FROM snapshots WHERE id = $id;";
            command.Parameters.AddWithValue("$id", snapshot.Id.ToString("D"));
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(dna.Hash, reader.GetString(0));
            Assert.Contains("BOAR...6789", reader.GetString(1));
            Assert.DoesNotContain("BOARD123456789", reader.GetString(1));
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
    public async Task SqliteStore_OldSnapshotWithoutDna_LoadsAsNotRecorded()
    {
        // A snapshot written before DNA existed: its payload has no DNA
        // fields and its columns are NULL. It must load as "DNA not
        // recorded", never as a mismatch against a later scan.
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");

        try
        {
            var snapshot = TestSnapshots.Healthy();
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();

            var payload = System.Text.Json.JsonSerializer.Serialize(
                snapshot,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            var node = JsonNode.Parse(payload)!.AsObject();
            node.Remove("machineDnaHash");
            node.Remove("machineDnaParts");

            await using (var connection = new SqliteConnection($"Data Source={databasePath};Pooling=false"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    INSERT INTO snapshots (id, captured_at, machine_name, overall_score, verdict, payload_json)
                    VALUES ($id, $capturedAt, $machineName, $score, $verdict, $payload);
                    """;
                command.Parameters.AddWithValue("$id", snapshot.Id.ToString("D"));
                command.Parameters.AddWithValue("$capturedAt", snapshot.CapturedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("$machineName", snapshot.MachineName);
                command.Parameters.AddWithValue("$score", snapshot.Score.OverallScore);
                command.Parameters.AddWithValue("$verdict", snapshot.Score.Verdict.ToString());
                command.Parameters.AddWithValue("$payload", node.ToJsonString());
                await command.ExecuteNonQueryAsync();
            }

            var loaded = await store.GetSnapshotAsync(snapshot.Id);
            Assert.NotNull(loaded);
            Assert.Null(loaded!.MachineDnaHash);
            Assert.Null(loaded.MachineDnaParts);
            Assert.Equal("DNA not recorded", loaded.MachineDnaStatus);
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
    public async Task SqliteStore_PruneKeepsNewest200PerMachine()
    {
        // Hardening 6b, per-machine: one history file can hold several
        // machines (a buyer-kit USB stick), and a global cap would evict
        // another machine's whole history once one machine hit 200 scans.
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");

        try
        {
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();

            var oldestA = Guid.Empty;
            for (var i = 0; i < 201; i++)
            {
                var snapshot = TestSnapshots.Healthy() with
                {
                    Id = Guid.NewGuid(),
                    MachineName = "Machine A",
                    CapturedAt = DateTimeOffset.UtcNow.AddMinutes(-201 + i),
                };
                if (i == 0)
                {
                    oldestA = snapshot.Id;
                }

                await store.SaveAsync(snapshot);
            }

            for (var i = 0; i < 3; i++)
            {
                await store.SaveAsync(TestSnapshots.Healthy() with
                {
                    Id = Guid.NewGuid(),
                    MachineName = "Machine B",
                    CapturedAt = DateTimeOffset.UtcNow.AddMinutes(i),
                });
            }

            var history = await store.GetHistoryAsync(500);
            Assert.Equal(SqliteSnapshotStore.MaxSnapshots + 3, history.Count);
            Assert.Equal(SqliteSnapshotStore.MaxSnapshots, history.Count(h => h.MachineName == "Machine A"));
            Assert.Equal(3, history.Count(h => h.MachineName == "Machine B"));
            Assert.DoesNotContain(history, h => h.Id == oldestA);
            Assert.Null(await store.GetSnapshotAsync(oldestA));
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
    public async Task SqliteStore_PruneKeepsOnlyTheNewest200Snapshots()
    {
        // Hardening 6b: saves only INSERTed, so rows accumulated unbounded.
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");

        try
        {
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();

            var newest = TestSnapshots.Healthy();
            var oldestId = Guid.Empty;
            for (var i = 0; i < 205; i++)
            {
                var snapshot = TestSnapshots.Healthy() with
                {
                    Id = Guid.NewGuid(),
                    CapturedAt = DateTimeOffset.UtcNow.AddMinutes(-205 + i),
                };
                if (i == 0)
                {
                    oldestId = snapshot.Id;
                }

                if (i == 204)
                {
                    newest = snapshot;
                }

                await store.SaveAsync(snapshot);
            }

            var history = await store.GetHistoryAsync(500);

            Assert.Equal(SqliteSnapshotStore.MaxSnapshots, history.Count);
            Assert.Equal(newest.Id, history[0].Id);
            Assert.DoesNotContain(history, h => h.Id == oldestId);
            Assert.Null(await store.GetSnapshotAsync(oldestId));
            Assert.NotNull(await store.GetSnapshotAsync(newest.Id));
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
    public async Task GetLatestPerMachine_ReturnsLatestScanAndCountPerMachine()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");

        try
        {
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();

            var baseTime = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
            var aOldest = TestSnapshots.Healthy() with
            {
                Id = Guid.NewGuid(),
                CapturedAt = baseTime,
                MachineName = "SHOP-PC-A",
            };
            var aMiddle = TestSnapshots.Healthy() with
            {
                Id = Guid.NewGuid(),
                CapturedAt = baseTime.AddHours(1),
                MachineName = "SHOP-PC-A",
            };
            var aLatest = TestSnapshots.Healthy() with
            {
                Id = Guid.NewGuid(),
                CapturedAt = baseTime.AddHours(2),
                MachineName = "SHOP-PC-A",
            };
            var bOlder = TestSnapshots.Healthy() with
            {
                Id = Guid.NewGuid(),
                CapturedAt = baseTime,
                MachineName = "SHOP-PC-B",
            };
            var bLatest = TestSnapshots.Healthy() with
            {
                Id = Guid.NewGuid(),
                CapturedAt = baseTime.AddMinutes(30),
                MachineName = "SHOP-PC-B",
            };

            await store.SaveAsync(aOldest);
            await store.SaveAsync(aMiddle);
            await store.SaveAsync(aLatest);
            await store.SaveAsync(bOlder);
            await store.SaveAsync(bLatest);

            var latest = await store.GetLatestPerMachineAsync();

            Assert.Equal(2, latest.Count);
            Assert.Equal("SHOP-PC-A", latest[0].MachineName);
            Assert.Equal(aLatest.Id, latest[0].Id);
            Assert.Equal(baseTime.AddHours(2), latest[0].CapturedAt);
            Assert.Equal(3, latest[0].ScanCount);
            Assert.Equal("SHOP-PC-B", latest[1].MachineName);
            Assert.Equal(bLatest.Id, latest[1].Id);
            Assert.Equal(baseTime.AddMinutes(30), latest[1].CapturedAt);
            Assert.Equal(2, latest[1].ScanCount);
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
    public async Task GetLatestPerMachine_EmptyStoreGivesEmptyList()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");

        try
        {
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();

            Assert.Empty(await store.GetLatestPerMachineAsync());
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
    public async Task GetLatestPerMachine_SavedOutOfOrderStillPicksNewest()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");

        try
        {
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();

            var baseTime = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
            var newer = TestSnapshots.Healthy() with
            {
                Id = Guid.NewGuid(),
                CapturedAt = baseTime.AddHours(5),
                MachineName = "SHOP-PC-A",
            };
            var older = TestSnapshots.Healthy() with
            {
                Id = Guid.NewGuid(),
                CapturedAt = baseTime,
                MachineName = "SHOP-PC-A",
            };

            // Saved newest first, then the older scan: the query must
            // still return the newest by captured time, not by save order.
            await store.SaveAsync(newer);
            await store.SaveAsync(older);

            var latest = await store.GetLatestPerMachineAsync();

            var only = Assert.Single(latest);
            Assert.Equal(newer.Id, only.Id);
            Assert.Equal(2, only.ScanCount);
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
    public async Task GetLatestPerMachine_DstTransition_ReturnsChronologicallyLatest()
    {
        // A DST fall-back hour writes two scans whose ISO strings sort
        // out of order: 01:30 at -04:00 is 05:30 UTC, 01:15 at -05:00 is
        // 06:15 UTC, but "01:30..." sorts after "01:15..." as a string.
        // The old ROW_NUMBER over the stored string returned the 05:30
        // scan as latest. The pick must be by parsed time, not by string.
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");

        try
        {
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();

            var earlierUtc = TestSnapshots.Healthy() with
            {
                Id = Guid.NewGuid(),
                CapturedAt = new DateTimeOffset(2026, 11, 1, 1, 30, 0, TimeSpan.FromHours(-4)),
                MachineName = "SHOP-PC-A",
            };
            var laterUtc = TestSnapshots.Healthy() with
            {
                Id = Guid.NewGuid(),
                CapturedAt = new DateTimeOffset(2026, 11, 1, 1, 15, 0, TimeSpan.FromHours(-5)),
                MachineName = "SHOP-PC-A",
            };

            // Saved newest first, then the older scan: neither save order
            // nor string order may decide the pick.
            await store.SaveAsync(laterUtc);
            await store.SaveAsync(earlierUtc);

            var latest = await store.GetLatestPerMachineAsync();

            var only = Assert.Single(latest);
            Assert.Equal(laterUtc.Id, only.Id);
            Assert.Equal(laterUtc.CapturedAt, only.CapturedAt);
            Assert.Equal(2, only.ScanCount);
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
    public async Task GetLatestPerMachine_SameTimestampPicksLargestId()
    {
        // Two scans sharing a timestamp: the pick must be deterministic.
        // The old SQL tie-break was id DESC, so the lexicographically
        // largest id wins; the C# pick keeps that rule.
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");

        try
        {
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();

            var at = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
            var lowerId = TestSnapshots.Healthy() with
            {
                Id = new Guid("00000000-0000-0000-0000-000000000001"),
                CapturedAt = at,
                MachineName = "SHOP-PC-A",
            };
            var higherId = TestSnapshots.Healthy() with
            {
                Id = new Guid("00000000-0000-0000-0000-000000000002"),
                CapturedAt = at,
                MachineName = "SHOP-PC-A",
            };

            await store.SaveAsync(lowerId);
            await store.SaveAsync(higherId);

            var latest = await store.GetLatestPerMachineAsync();

            var only = Assert.Single(latest);
            Assert.Equal(higherId.Id, only.Id);
            Assert.Equal(2, only.ScanCount);
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
    public async Task SqliteStore_PruneKeepsChronologicallyNewest_AcrossDstTransition()
    {
        // The same DST string-ordering edge in the retention prune: 200
        // scans at 01:16-01:19 at -04:00 (all before 05:20 UTC) sort
        // lexicographically AFTER one scan at 01:15 at -05:00 (06:15 UTC,
        // the true newest), so the old string-ordered prune evicted the
        // newest scan and kept 200 older ones. The prune must keep by
        // parsed time.
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");

        try
        {
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();

            var oldestId = Guid.Empty;
            var baseTime = new DateTimeOffset(2026, 11, 1, 1, 16, 0, TimeSpan.FromHours(-4));
            for (var i = 0; i < 200; i++)
            {
                var snapshot = TestSnapshots.Healthy() with
                {
                    Id = Guid.NewGuid(),
                    MachineName = "Machine A",
                    CapturedAt = baseTime.AddSeconds(i),
                };
                if (i == 0)
                {
                    oldestId = snapshot.Id;
                }

                await store.SaveAsync(snapshot);
            }

            var newest = TestSnapshots.Healthy() with
            {
                Id = Guid.NewGuid(),
                MachineName = "Machine A",
                CapturedAt = new DateTimeOffset(2026, 11, 1, 1, 15, 0, TimeSpan.FromHours(-5)),
            };
            await store.SaveAsync(newest);

            var history = await store.GetHistoryAsync(500);

            Assert.Equal(SqliteSnapshotStore.MaxSnapshots, history.Count);
            Assert.Equal(newest.Id, history[0].Id);
            Assert.DoesNotContain(history, h => h.Id == oldestId);
            Assert.Null(await store.GetSnapshotAsync(oldestId));
            Assert.NotNull(await store.GetSnapshotAsync(newest.Id));
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
    public async Task SqliteStore_DeleteMachine_RemovesOnlyThatMachine()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");

        try
        {
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();

            var baseTime = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
            var first = TestSnapshots.Healthy() with { Id = Guid.NewGuid(), MachineName = "SHOP-PC", CapturedAt = baseTime };
            var second = TestSnapshots.Healthy() with { Id = Guid.NewGuid(), MachineName = "SHOP-PC", CapturedAt = baseTime.AddHours(1) };
            var prefixed = TestSnapshots.Healthy() with { Id = Guid.NewGuid(), MachineName = "SHOP-PC-2", CapturedAt = baseTime.AddHours(2) };
            var cased = TestSnapshots.Healthy() with { Id = Guid.NewGuid(), MachineName = "shop-pc", CapturedAt = baseTime.AddHours(3) };
            await store.SaveAsync(first);
            await store.SaveAsync(second);
            await store.SaveAsync(prefixed);
            await store.SaveAsync(cased);

            // Exact match only: the prefix machine and the case variant
            // are different machines, and both keep their scans.
            var removed = await store.DeleteMachineAsync("SHOP-PC");

            Assert.Equal(2, removed);
            Assert.Null(await store.GetSnapshotAsync(first.Id));
            Assert.Null(await store.GetSnapshotAsync(second.Id));
            Assert.NotNull(await store.GetSnapshotAsync(prefixed.Id));
            Assert.NotNull(await store.GetSnapshotAsync(cased.Id));
            var names = await store.GetMachineNamesAsync();
            Assert.Equal(new[] { "SHOP-PC-2", "shop-pc" }, names);
            var latest = await store.GetLatestPerMachineAsync();
            Assert.Equal(2, latest.Count);
            Assert.DoesNotContain(latest, m => m.MachineName == "SHOP-PC");
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
    public async Task SqliteStore_DeleteMachine_UnknownMachine_ReturnsZero()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");

        try
        {
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();

            var snapshot = TestSnapshots.Healthy() with { MachineName = "SHOP-PC" };
            await store.SaveAsync(snapshot);

            var removed = await store.DeleteMachineAsync("NO-SUCH-PC");

            Assert.Equal(0, removed);
            Assert.Single(await store.GetHistoryAsync());
            Assert.NotNull(await store.GetSnapshotAsync(snapshot.Id));
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
    public async Task SqliteStore_DeleteMachine_ThenReSave_WorksAgain()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");

        try
        {
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();

            var baseTime = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
            var old = TestSnapshots.Healthy() with { Id = Guid.NewGuid(), MachineName = "SHOP-PC", CapturedAt = baseTime };
            await store.SaveAsync(old);
            Assert.Equal(1, await store.DeleteMachineAsync("SHOP-PC"));

            // A later scan (or a re-import of the same report) brings
            // the machine back: delete is not a ban.
            var fresh = TestSnapshots.Healthy() with { Id = Guid.NewGuid(), MachineName = "SHOP-PC", CapturedAt = baseTime.AddDays(1) };
            await store.SaveAsync(fresh);

            var history = await store.GetHistoryAsync();
            var saved = Assert.Single(history);
            Assert.Equal(fresh.Id, saved.Id);
            Assert.Single(await store.GetLatestPerMachineAsync());
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
    public async Task SqliteStore_DeleteMachine_BlankName_Throws()
    {
        var directory = Path.Combine(Path.GetTempPath(), "truespec-tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "snapshots.db");

        try
        {
            var store = new SqliteSnapshotStore(databasePath);
            await store.InitializeAsync();

            await Assert.ThrowsAsync<ArgumentException>(() => store.DeleteMachineAsync("  "));
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
