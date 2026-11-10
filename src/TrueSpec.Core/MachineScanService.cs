namespace TrueSpec.Core;

using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;

/// <summary>
/// Runs every collector, then feeds the readings to the scoring engine and the
/// buyer check evaluator. The app only talks to this service; in P0 the
/// collectors are stubs.
/// </summary>
public sealed class MachineScanService
{
    private readonly IReadOnlyList<ICollector> _collectors;
    private readonly IScoringEngine _scoringEngine;
    private readonly IBuyerCheckEvaluator _buyerCheckEvaluator;

    public MachineScanService(
        IEnumerable<ICollector> collectors,
        IScoringEngine scoringEngine,
        IBuyerCheckEvaluator buyerCheckEvaluator)
    {
        _collectors = collectors.ToList();
        _scoringEngine = scoringEngine;
        _buyerCheckEvaluator = buyerCheckEvaluator;
    }

    public static MachineScanService CreateDefaultStub() => new(
        StubCollectorCatalog.CreateAll(),
        new ScoringEngine(),
        new BuyerCheckEvaluator());

    public IReadOnlyList<ICollector> Collectors => _collectors;

    public async Task<SystemSnapshot> ScanAsync(CancellationToken cancellationToken = default)
    {
        var components = new List<ComponentSnapshot>();
        foreach (var collector in _collectors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            components.Add(await collector.CollectAsync(cancellationToken));
        }

        var score = _scoringEngine.Score(components);
        var buyerChecks = _buyerCheckEvaluator.Evaluate(components);

        var system = components.FirstOrDefault(c =>
            string.Equals(c.Id, "system", StringComparison.OrdinalIgnoreCase));
        var machineName = system?.Headline ?? Environment.MachineName;
        var operatingSystem = system?.Specs
            .FirstOrDefault(s => string.Equals(s.Label, "Windows edition", StringComparison.OrdinalIgnoreCase))
            ?.Value ?? "Unknown";

        return new SystemSnapshot(
            Guid.NewGuid(),
            DateTimeOffset.Now,
            machineName,
            operatingSystem,
            components,
            score,
            buyerChecks);
    }
}
