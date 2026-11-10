namespace TrueSpec.Core.Storage;

using TrueSpec.Core.Models;

public sealed record SnapshotSummary(
    Guid Id,
    DateTimeOffset CapturedAt,
    string MachineName,
    double OverallScore,
    string Verdict);

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
}
