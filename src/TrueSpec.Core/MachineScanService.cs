namespace TrueSpec.Core;

using System.Diagnostics;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Health;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;

/// <summary>
/// Timeout budget for one scan. A collector that exceeds its per-collector
/// budget is reported as not available; the overall budget bounds the whole
/// scan even when a collector ignores its cancellation token, because the
/// wait is a timeout race rather than a bare await.
/// </summary>
public sealed record ScanTimeouts(TimeSpan PerCollector, TimeSpan Overall)
{
    public static ScanTimeouts Default { get; } = new(
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30));
}

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

    /// <summary>
    /// Runs every collector with a timeout budget. A collector that exceeds its
    /// per-collector budget, or a scan that exceeds its overall budget, reports
    /// the affected components as not available — never a fake value. The scan
    /// always completes within about the overall budget, even when a collector
    /// ignores its cancellation token, because each wait is a timeout race.
    /// The optional log sink receives milestone lines; it is never allowed to
    /// throw into the scan. A caller-cancelled scan still throws
    /// <see cref="OperationCanceledException"/>; a budget timeout does not.
    /// </summary>
    public async Task<SystemSnapshot> ScanAsync(
        BuyerCheckContext? buyerContext,
        CancellationToken cancellationToken = default,
        ScanTimeouts? timeouts = null,
        Action<string>? log = null)
    {
        var budget = SanitizeTimeouts(timeouts ?? ScanTimeouts.Default);
        var stopwatch = Stopwatch.StartNew();
        Log(log, "Scan started");

        using var overallCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        overallCts.CancelAfter(budget.Overall);
        var overallToken = overallCts.Token;

        var components = new List<ComponentSnapshot>();
        foreach (var collector in _collectors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (overallToken.IsCancellationRequested)
            {
                Log(log, $"Collector {collector.Id} skipped: overall scan timeout reached");
                components.Add(ComponentSnapshot.NotAvailable(
                    collector.Id,
                    collector.Name,
                    collector.Category,
                    collector.Id,
                    "Scan timed out before this collector ran."));
                continue;
            }

            var collectorStopwatch = Stopwatch.StartNew();
            Log(log, $"Collector {collector.Id} started");

            ComponentSnapshot component;
            try
            {
                component = await CollectWithTimeoutAsync(collector, overallToken, budget.PerCollector, log);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                component = ComponentSnapshot.NotAvailable(
                    collector.Id,
                    collector.Name,
                    collector.Category,
                    collector.Id,
                    "Scan timed out while collecting this component.");
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

            collectorStopwatch.Stop();
            Log(log, $"Collector {collector.Id} finished in {collectorStopwatch.ElapsedMilliseconds} ms");
            components.Add(ComponentHealthRules.Apply(component));
        }

        var score = _scoringEngine.Score(components);
        var buyerChecks = _buyerCheckEvaluator.Evaluate(components, buyerContext);

        // One verdict for the whole machine: a score alone only says how
        // healthy the measured parts are, so the buyer checks cap it. A
        // machine with 100% healthy parts but failed buyer checks (wear,
        // power-on hours, old BIOS) is "Used - healthy", never "Like new".
        score = score with { Verdict = ScoringEngine.CombineVerdict(score, buyerChecks) };

        var system = components.FirstOrDefault(c =>
            string.Equals(c.Id, "system", StringComparison.OrdinalIgnoreCase));
        // A failed system collector reports the headline "Not available":
        // that is a missing reading, never the machine's name. Fall back
        // to the OS machine name instead of stamping "Not available" on
        // the Overview hero, the report and the history list.
        var machineName = system?.Headline;
        if (string.IsNullOrWhiteSpace(machineName) ||
            string.Equals(machineName, "Not available", StringComparison.OrdinalIgnoreCase))
        {
            machineName = Environment.MachineName;
        }
        var operatingSystem = system?.Specs
            .FirstOrDefault(s =>
                string.Equals(s.Label, SpecLabels.OperatingSystem, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.Label, SpecLabels.WindowsEdition, StringComparison.OrdinalIgnoreCase))
            ?.Value ?? "Not reported";

        stopwatch.Stop();
        Log(log, $"Scan complete in {stopwatch.ElapsedMilliseconds} ms ({components.Count} components)");

        return new SystemSnapshot(
            Guid.NewGuid(),
            DateTimeOffset.Now,
            machineName,
            operatingSystem,
            components,
            score,
            buyerChecks);
    }

    private static ScanTimeouts SanitizeTimeouts(ScanTimeouts timeouts)
    {
        var perCollector = timeouts.PerCollector <= TimeSpan.Zero
            ? ScanTimeouts.Default.PerCollector
            : timeouts.PerCollector;
        var overall = timeouts.Overall <= TimeSpan.Zero
            ? ScanTimeouts.Default.Overall
            : timeouts.Overall;
        return new ScanTimeouts(perCollector, overall);
    }

    private static async Task<ComponentSnapshot> CollectWithTimeoutAsync(
        ICollector collector,
        CancellationToken overallToken,
        TimeSpan perCollectorTimeout,
        Action<string>? log)
    {
        // The WMI/registry/process work inside a collector is synchronous:
        // calling CollectAsync directly would run it on the caller's thread
        // (the UI thread during a scan) and a collector that blocks before
        // returning its task would defeat the timeout race below entirely.
        // Task.Run moves the whole call onto the thread pool, so the race
        // is real and the UI stays live. Collectors still run one at a time,
        // in order: WMI contention makes parallel collection slower, not
        // faster, and a hung collector is abandoned by the timeout, never
        // awaited. The token is passed to Task.Run so a scan cancelled
        // before this collector starts cancels the queued work instead of
        // running it anyway.
        var collectTask = Task.Run(() => collector.CollectAsync(overallToken), overallToken);
        var timeoutTask = Task.Delay(perCollectorTimeout, overallToken);
        var completed = await Task.WhenAny(collectTask, timeoutTask);
        if (completed == collectTask)
        {
            return await collectTask;
        }

        if (overallToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(overallToken);
        }

        // Per-collector timeout: the collector keeps running in the background.
        // Observe any later fault so it can never surface as unobserved.
        _ = collectTask.ContinueWith(
            task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
        Log(log, $"Collector {collector.Id} timed out after {perCollectorTimeout.TotalSeconds:0.#} s");
        return ComponentSnapshot.NotAvailable(
            collector.Id,
            collector.Name,
            collector.Category,
            collector.Id,
            $"Collector timed out after {perCollectorTimeout.TotalSeconds:0.#} seconds. TrueSpec does not invent a value when the hardware does not report one.");
    }

    private static void Log(Action<string>? log, string message)
    {
        try
        {
            log?.Invoke(message);
        }
        catch
        {
            // Logging must never break a scan.
        }
    }
}
