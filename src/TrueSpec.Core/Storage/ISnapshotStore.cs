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
/// chart degradation and raise alerts. P0 is a working SQLite skeleton.
/// </summary>
public interface ISnapshotStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(SystemSnapshot snapshot, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SnapshotSummary>> GetHistoryAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);
}
