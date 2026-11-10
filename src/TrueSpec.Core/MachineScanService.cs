namespace TrueSpec.Core;

using TrueSpec.Core.Collectors;
using TrueSpec.Core.Health;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;

/// <summary>
/// Runs every collector, applies the measured health rules, then feeds the
/// readings to the scoring engine and the buyer check evaluator. A collector
/// that fails is reported as not available; it never produces a fake value.
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

    public static MachineScanService CreateDefault() => Create(UnavailableCollectorCatalog.CreateAll());

    public static MachineScanService Create(IEnumerable<ICollector> collectors) => new(
        collectors,
        new ScoringEngine(),
        new BuyerCheckEvaluator());

    public IReadOnlyList<ICollector> Collectors => _collectors;

    public Task<SystemSnapshot> ScanAsync(CancellationToken cancellationToken = default) =>
        ScanAsync(null, cancellationToken);

    public async Task<SystemSnapshot> ScanAsync(
        BuyerCheckContext? buyerContext,
        CancellationToken cancellationToken = default)
    {
        var components = new List<ComponentSnapshot>();
        foreach (var collector in _collectors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ComponentSnapshot component;
            try
            {
                component = await collector.CollectAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                component = ComponentSnapshot.NotAvailable(
                    collector.Id,
                    collector.Name,
                    collector.Category,
                    collector.Id,
                    $"Collector failed: {exception.Message}");
            }

            components.Add(ComponentHealthRules.Apply(component));
        }

        var score = _scoringEngine.Score(components);
        var buyerChecks = _buyerCheckEvaluator.Evaluate(components, buyerContext);

        var system = components.FirstOrDefault(c =>
            string.Equals(c.Id, "system", StringComparison.OrdinalIgnoreCase));
        var machineName = system?.Headline ?? Environment.MachineName;
        var operatingSystem = system?.Specs
            .FirstOrDefault(s =>
                string.Equals(s.Label, "Operating system", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.Label, "Windows edition", StringComparison.OrdinalIgnoreCase))
            ?.Value ?? "Not reported";

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
