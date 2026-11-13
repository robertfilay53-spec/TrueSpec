namespace TrueSpec.Core;

using System.Diagnostics;
using System.Globalization;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Comparison;
using TrueSpec.Core.Health;
using TrueSpec.Core.Models;
using TrueSpec.Core.Scoring;

/// <summary>
/// Timeout budget for one scan. Every collector gets its own per-collector
/// budget, and the overall budget bounds the whole scan even when a collector
/// ignores its cancellation token, because each wait is a timeout race rather
/// than a bare await. The overall budget is never smaller than the
/// per-collector budget: with collectors running in parallel, every collector
/// must get its full independent budget before the overall wall can cut the
/// scan short.
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
///
/// Collectors run in parallel, each on the thread pool with its own timeout
/// race. That is structural: run sequentially, one slow collector consumes
/// the shared overall budget and the collectors queued behind it never run
/// at all (on a real standard-user laptop, System, Security and Drivers each
/// hit the 10 s per-collector timeout in turn, so the 30 s overall budget was
/// gone before Updates, Startup and Crash history started). In parallel the
/// scan finishes in about the slowest collector's time, and every collector
/// always ends in a result or a clear "not available" entry, never a cascade
/// of timeouts. Collectors that share a resource (the CPU and GPU collectors
/// share one LibreHardwareMonitor sweep) degrade honestly when they race:
/// the loser reports its live sensors as not reported, never a made-up value.
/// </summary>
public sealed class MachineScanService
{
    private readonly IReadOnlyList<ICollector> _collectors;
    private readonly IScoringEngine _scoringEngine;
    private readonly IBuyerCheckEvaluator _buyerCheckEvaluator;
    private readonly IMachineDnaProvider? _dnaProvider;

    public MachineScanService(
        IEnumerable<ICollector> collectors,
        IScoringEngine scoringEngine,
        IBuyerCheckEvaluator buyerCheckEvaluator,
        IMachineDnaProvider? dnaProvider = null)
    {
        _collectors = collectors.ToList();
        _scoringEngine = scoringEngine;
        _buyerCheckEvaluator = buyerCheckEvaluator;
        _dnaProvider = dnaProvider;
    }

    public static MachineScanService CreateDefault() => Create(UnavailableCollectorCatalog.CreateAll());

    public static MachineScanService Create(IEnumerable<ICollector> collectors) =>
        Create(collectors, null);

    public static MachineScanService Create(
        IEnumerable<ICollector> collectors,
        IMachineDnaProvider? dnaProvider) => new(
        collectors,
        new ScoringEngine(),
        new BuyerCheckEvaluator(),
        dnaProvider);

    public IReadOnlyList<ICollector> Collectors => _collectors;

    public Task<SystemSnapshot> ScanAsync(CancellationToken cancellationToken = default) =>
        ScanAsync(null, cancellationToken);

    /// <summary>
    /// Runs every collector in parallel, each with its own timeout budget.
    /// A collector that exceeds its per-collector budget, or a scan that
    /// exceeds its overall budget, reports the affected components as not
    /// available — never a fake value — while every other collector still
    /// reports its real result. The scan always completes within about the
    /// overall budget, even when a collector ignores its cancellation token,
    /// because each wait is a timeout race. The optional log sink receives
    /// milestone lines; it is never allowed to throw into the scan.
    /// A caller-cancelled scan still throws
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

        // The machine fingerprint runs alongside the collectors, under the
        // same per-collector budget: a slow or failing DNA read never
        // blocks the scan, and a scan without a DNA provider (non-Windows,
        // or a caller that did not supply one) simply records no DNA.
        var dnaTask = _dnaProvider is null
            ? null
            : CollectDnaAsync(_dnaProvider, overallToken, budget.PerCollector, cancellationToken, log);

        // Observe faults on the DNA task too, so a task abandoned by a
        // caller-cancelled scan can never surface as unobserved.
        _ = dnaTask?.ContinueWith(
            task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

        // One task per collector, all started up front: a slow collector
        // waits out its own budget on its own task while the rest finish.
        var collectorTasks = _collectors
            .Select(collector => CollectOneAsync(collector, overallToken, budget.PerCollector, cancellationToken, log))
            .ToArray();

        // Observe faults on the combined task so a task abandoned by the
        // overall wall can never surface as an unobserved exception.
        var whenAll = Task.WhenAll(collectorTasks);
        _ = whenAll.ContinueWith(
            task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

        try
        {
            var wall = Task.Delay(budget.Overall, cancellationToken);
            var finished = await Task.WhenAny(whenAll, wall);
            // A caller cancellation completes the wall task as cancelled:
            // that is a cancelled scan, not an overall timeout.
            cancellationToken.ThrowIfCancellationRequested();
            if (finished == wall)
            {
                Log(log, "Scan timed out: overall scan budget reached");
            }
            else
            {
                await whenAll;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }

        var components = new List<ComponentSnapshot>(_collectors.Count);
        for (var i = 0; i < _collectors.Count; i++)
        {
            var collector = _collectors[i];
            var task = collectorTasks[i];

            // A task that finished holds its real result (or its own
            // NotAvailable for a per-collector timeout or failure). A task
            // still running past the overall wall is abandoned and reported
            // as not available: the scan never waits on it, and the
            // component is never left without an entry.
            if (task.IsCompletedSuccessfully)
            {
                components.Add(ComponentHealthRules.Apply(task.Result));
                continue;
            }

            if (task.IsFaulted || task.IsCanceled)
            {
                // CollectOneAsync only lets a caller cancellation escape, so
                // a faulted or cancelled task here is a cancelled scan.
                // Anything else is observed and reported as a timeout: the
                // component is never left without an entry.
                cancellationToken.ThrowIfCancellationRequested();
                _ = task.Exception;
            }

            Log(log, $"Collector {collector.Id} did not finish within the overall scan budget");
            components.Add(ComponentSnapshot.NotAvailable(
                collector.Id,
                collector.Name,
                collector.Category,
                collector.Id,
                "Scan timed out before this collector finished."));
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

        // The fingerprint, when one was collected: the hash (full, in the
        // snapshot only) and the truncated parts. A null DNA means the
        // provider could not run at all, so the snapshot reads "DNA not
        // recorded"; a DNA with no hash means it ran but no part reported
        // a usable serial, so the snapshot reads "DNA not available".
        // Neither state is a mismatch, and DNA never changes the score.
        MachineDna? dna = null;
        if (dnaTask is not null)
        {
            try
            {
                dna = await dnaTask;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Log(log, $"Machine DNA collection failed: {exception.Message}");
            }
        }

        Log(log, dna?.Hash is null
            ? "Machine DNA not recorded for this scan"
            : string.Create(CultureInfo.InvariantCulture, $"Machine DNA recorded ({dna.Parts.Count} parts)"));

        return new SystemSnapshot(
            Guid.NewGuid(),
            DateTimeOffset.Now,
            machineName,
            operatingSystem,
            components,
            score,
            buyerChecks,
            MachineDnaHash: dna?.Hash,
            MachineDnaParts: dna?.Parts);
    }

    private static ScanTimeouts SanitizeTimeouts(ScanTimeouts timeouts)
    {
        var perCollector = timeouts.PerCollector <= TimeSpan.Zero
            ? ScanTimeouts.Default.PerCollector
            : timeouts.PerCollector;
        // The overall wall must never cut a collector's independent budget
        // short: with collectors in parallel, the wall only matters once
        // every collector has had its own budget.
        var overall = timeouts.Overall <= TimeSpan.Zero
            ? ScanTimeouts.Default.Overall
            : timeouts.Overall;
        overall = overall < perCollector ? perCollector : overall;
        return new ScanTimeouts(perCollector, overall);
    }

    /// <summary>
    /// Runs one collector with its own timeout race and converts every
    /// outcome except caller cancellation into a snapshot: a timeout or a
    /// collector failure is a NotAvailable component with the reason, never
    /// an exception out of the scan and never a fake value.
    /// </summary>
    private static async Task<ComponentSnapshot> CollectOneAsync(
        ICollector collector,
        CancellationToken overallToken,
        TimeSpan perCollectorTimeout,
        CancellationToken callerToken,
        Action<string>? log)
    {
        var collectorStopwatch = Stopwatch.StartNew();
        Log(log, $"Collector {collector.Id} started");

        ComponentSnapshot component;
        try
        {
            component = await CollectWithTimeoutAsync(collector, overallToken, perCollectorTimeout, log);
        }
        catch (OperationCanceledException) when (callerToken.IsCancellationRequested)
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
        return component;
    }

    /// <summary>
    /// Runs the DNA provider with its own timeout race, mirroring the
    /// per-collector race above. A timeout or a provider failure is a
    /// missing fingerprint (null), never an exception out of the scan and
    /// never a fake value. Only a caller cancellation escapes.
    /// </summary>
    private static async Task<MachineDna?> CollectDnaAsync(
        IMachineDnaProvider provider,
        CancellationToken overallToken,
        TimeSpan timeout,
        CancellationToken callerToken,
        Action<string>? log)
    {
        try
        {
            var collectTask = Task.Run(() => provider.CollectAsync(overallToken), overallToken);
            var timeoutTask = Task.Delay(timeout, overallToken);
            var completed = await Task.WhenAny(collectTask, timeoutTask);
            if (completed == collectTask)
            {
                return await collectTask;
            }

            if (overallToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(overallToken);
            }

            _ = collectTask.ContinueWith(
                task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
            Log(log, $"Machine DNA collection timed out after {timeout.TotalSeconds:0.#} s");
            return null;
        }
        catch (OperationCanceledException) when (callerToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception exception)
        {
            Log(log, $"Machine DNA collection failed: {exception.Message}");
            return null;
        }
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
        // is real and the UI stays live. A hung collector is abandoned by
        // the timeout, never awaited. The token is passed to Task.Run so a
        // scan cancelled before this collector starts cancels the queued
        // work instead of running it anyway.
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
