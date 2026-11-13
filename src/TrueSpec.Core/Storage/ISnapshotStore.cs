namespace TrueSpec.Core.Storage;

using TrueSpec.Core.Models;

public sealed record SnapshotSummary(
    Guid Id,
    DateTimeOffset CapturedAt,
    string MachineName,
    double OverallScore,
    string Verdict);

/// <summary>
/// One machine's latest saved scan, for the technician fleet table:
/// the newest row for the machine plus how many scans the store holds
/// for it. The score and verdict here are the stored columns; the fleet
/// builder reads the score from the loaded snapshot instead, because a
/// stored 0.0 is what a not-measurable scan writes, not a real score.
/// </summary>
public sealed record MachineLatestSummary(
    string MachineName,
    Guid Id,
    DateTimeOffset CapturedAt,
    double OverallScore,
    string Verdict,
    int ScanCount);

/// <summary>
/// Local history storage. Every scan saves a snapshot so later versions can
/// chart degradation and raise alerts. The default implementation uses SQLite.
/// </summary>
public interface ISnapshotStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(SystemSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads one full snapshot by id for the comparison view. Returns null
    /// when the id is not in the store.
    /// </summary>
    Task<SystemSnapshot?> GetSnapshotAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SnapshotSummary>> GetHistoryAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The distinct machine names in the store, alphabetical. Empty when
    /// no snapshots are saved.
    /// </summary>
    Task<IReadOnlyList<string>> GetMachineNamesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The latest saved scan per machine, one row per machine, ordered by
    /// machine name, with the scan count for that machine. A fleet table
    /// needs every machine's latest scan; the capped history list cannot
    /// provide that (a shop with many machines would lose the oldest ones
    /// past the cap). Empty when no snapshots are saved.
    /// </summary>
    Task<IReadOnlyList<MachineLatestSummary>> GetLatestPerMachineAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every saved scan for one machine from the local store and
    /// returns how many rows were removed. The match is the exact stored
    /// machine name, the same key the fleet table groups by, so deleting
    /// "SHOP-PC" never touches "SHOP-PC-2" or a machine whose name only
    /// differs in case. Returns 0 when the store holds no scans for the
    /// name. This only touches TrueSpec's own history file; it never
    /// touches the machine itself, and it never deletes another
    /// machine's scans.
    /// </summary>
    Task<int> DeleteMachineAsync(string machineName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes one saved scan by its id and returns how many rows were
    /// removed: 1 when the scan existed, 0 when it was already gone. A
    /// scan id is never empty in practice (every scan is saved with a
    /// fresh Guid), so an empty id is rejected rather than treated as a
    /// no-op. Deleting one scan never touches any other scan, including
    /// other scans of the same machine. This only touches TrueSpec's own
    /// history file; it never touches the machine itself.
    /// </summary>
    Task<int> DeleteSnapshotAsync(Guid id, CancellationToken cancellationToken = default);
}
