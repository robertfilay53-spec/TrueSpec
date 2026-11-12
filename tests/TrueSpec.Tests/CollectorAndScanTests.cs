using TrueSpec.Core;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;

namespace TrueSpec.Tests;

public class CollectorAndScanTests
{
    [Fact]
    public void UnavailableCatalog_ExposesEveryDashboardComponent()
    {
        var collectors = UnavailableCollectorCatalog.CreateAll();

        Assert.Equal(14, collectors.Count);
        Assert.Equal(collectors.Count, collectors.Select(c => c.Id).Distinct().Count());
        Assert.Contains(collectors, c => c.Id == "system");
        Assert.Contains(collectors, c => c.Id == "crash");
        Assert.Contains(collectors, c => c.Id == "drivers");
        Assert.All(collectors, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Name));
            Assert.False(string.IsNullOrWhiteSpace(c.Category));
            Assert.True(c.RefreshInterval > TimeSpan.Zero);
        });
    }

    [Fact]
    public async Task EveryUnavailableCollector_ReturnsAReasonAndNoHealthNumber()
    {
        foreach (var collector in UnavailableCollectorCatalog.CreateAll())
        {
            var snapshot = await collector.CollectAsync();

            Assert.Equal(collector.Id, snapshot.Id);
            Assert.Equal("Not available", snapshot.Headline);
            Assert.Null(snapshot.HealthPercent);
            Assert.Contains(snapshot.Specs, spec => spec.Label == "Status");
            Assert.All(snapshot.Specs, spec =>
                Assert.False(string.IsNullOrWhiteSpace(spec.Source)));
            Assert.Contains("does not invent", snapshot.WhatThisMeans, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task DefaultScan_UsesHonestFallbackAndReportsNoScore()
    {
        var snapshot = await MachineScanService.CreateDefault().ScanAsync();

        // The default scan runs the whole honest-fallback catalogue: one
        // "Not available" component per dashboard screen (14 since the
        // crash-history collector joined in v1.3).
        // An earlier version of this test expected 4, which never matched
        // the catalogue and never ran before CI.
        Assert.Equal(UnavailableCollectorCatalog.CreateAll().Count, snapshot.Components.Count);
        Assert.All(snapshot.Components, component => Assert.Null(component.HealthPercent));
        Assert.False(snapshot.Score.HasMeasurableComponents);
        Assert.Equal(0d, snapshot.Score.OverallScore);
        Assert.NotEmpty(snapshot.BuyerChecks.Checks);
        Assert.All(snapshot.BuyerChecks.Checks, check =>
            Assert.Equal(BuyerCheckState.NotApplicable, check.State));
    }

    [Fact]
    public async Task ScanService_HealthyMachineWithAllBuyerChecksPassed_IsNew()
    {
        var now = DateTimeOffset.UtcNow;
        var components = new[]
        {
            TestSnapshots.System(now),
            TestSnapshots.Cpu(),
            TestSnapshots.Ram("16 GB"),
            TestSnapshots.Storage("0", "0"),
            TestSnapshots.Battery(2),
            TestSnapshots.Gpu(),
            TestSnapshots.Network(now.AddDays(-20).ToString("yyyy-MM-dd")),
        };
        var service = MachineScanService.Create(components.Select(c => new FixedCollector(c)));

        var snapshot = await service.ScanAsync(new BuyerCheckContext("16 GB"));

        Assert.True(snapshot.Score.OverallScore >= 90d);
        Assert.Equal(BuyerCheckState.Pass, snapshot.BuyerChecks.OverallState);
        Assert.Equal(Verdict.New, snapshot.Score.Verdict);
    }

    [Fact]
    public async Task ScanService_HighScoreButFailedBuyerCheck_IsUsedHealthyNotLikeNew()
    {
        // The AppOnFly case: 100% healthy measured parts, but the drive has
        // wear, so the machine has clearly been used. The verdict must say
        // "Used - healthy", never "Like new", on the same screen as the
        // FAIL buyer banner.
        var now = DateTimeOffset.UtcNow;
        var components = new[]
        {
            TestSnapshots.System(now),
            TestSnapshots.Cpu(),
            TestSnapshots.Ram("16 GB"),
            TestSnapshots.Storage("10", "0", health: 100d),
            TestSnapshots.Battery(2),
            TestSnapshots.Gpu(),
            TestSnapshots.Network(now.AddDays(-20).ToString("yyyy-MM-dd")),
        };
        var service = MachineScanService.Create(components.Select(c => new FixedCollector(c)));

        var snapshot = await service.ScanAsync(new BuyerCheckContext("16 GB"));

        Assert.True(snapshot.Score.OverallScore >= 90d);
        Assert.Equal(BuyerCheckState.Fail, snapshot.BuyerChecks.OverallState);
        Assert.Equal(Verdict.UsedHealthy, snapshot.Score.Verdict);
    }

    [Fact]
    public async Task ScanService_AppliesMeasuredHealthRulesAndKeepsSources()
    {
        var now = DateTimeOffset.UtcNow;
        var components = new[]
        {
            TestSnapshots.System(now),
            TestSnapshots.Storage("10", "0", health: null),
            TestSnapshots.Battery(2, health: null),
            TestSnapshots.Ram("16 GB"),
        };
        var service = MachineScanService.Create(components.Select(c => new FixedCollector(c)));

        var snapshot = await service.ScanAsync(new BuyerCheckContext("16 GB"));

        Assert.Equal(4, snapshot.Components.Count);
        Assert.Equal(90d, snapshot.Find("storage")!.HealthPercent!.Value, 3);
        Assert.Equal(90d, snapshot.Find("battery")!.HealthPercent!.Value, 3);
        Assert.True(snapshot.Score.HasMeasurableComponents);
        Assert.NotEmpty(snapshot.BuyerChecks.Checks);
        Assert.All(snapshot.Components.SelectMany(c => c.Specs), spec =>
            Assert.False(string.IsNullOrWhiteSpace(spec.Source)));
    }

    [Fact]
    public async Task HungCollector_DoesNotBlockScanPastOverallTimeout()
    {
        var now = DateTimeOffset.UtcNow;
        var service = MachineScanService.Create(new ICollector[]
        {
            new HangingCollector(),
            new FixedCollector(TestSnapshots.System(now)),
        });
        var timeouts = new ScanTimeouts(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500));

        var started = DateTimeOffset.UtcNow;
        var snapshot = await service.ScanAsync(null, CancellationToken.None, timeouts, null);
        var elapsed = DateTimeOffset.UtcNow - started;

        Assert.True(elapsed < TimeSpan.FromSeconds(10), $"Scan took {elapsed}, expected it to finish near the overall timeout");
        Assert.Equal(2, snapshot.Components.Count);
        var hung = snapshot.Find("hanging")!;
        Assert.Equal("Not available", hung.Headline);
        Assert.Null(hung.HealthPercent);
        Assert.NotNull(snapshot.Find("system"));
    }

    [Fact]
    public async Task SlowCollector_TimesOutAndYieldsNotAvailable()
    {
        var service = MachineScanService.Create(new ICollector[] { new SlowCollector() });
        var timeouts = new ScanTimeouts(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(30));

        var snapshot = await service.ScanAsync(null, CancellationToken.None, timeouts, null);

        var slow = snapshot.Find("slow")!;
        Assert.Equal("Not available", slow.Headline);
        Assert.Null(slow.HealthPercent);
        Assert.Contains(slow.Specs, spec => spec.Label == "Status");
    }

    [Fact]
    public async Task SlowCollectors_DoNotBlockOtherCollectors()
    {
        // The v1.4.0 laptop failure, in miniature: run sequentially, three
        // 5-second collectors would eat the whole overall budget and the
        // collectors behind them would never run. In parallel the scan
        // finishes in about one per-collector budget and every collector
        // still reports — the slow ones as NotAvailable, the fast ones with
        // their real results.
        var now = DateTimeOffset.UtcNow;
        var service = MachineScanService.Create(new ICollector[]
        {
            new SlowCollector(),
            new SlowCollector(),
            new SlowCollector(),
            new FixedCollector(TestSnapshots.System(now)),
            new FixedCollector(TestSnapshots.Ram("16 GB")),
        });
        var timeouts = new ScanTimeouts(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(30));

        var started = DateTimeOffset.UtcNow;
        var snapshot = await service.ScanAsync(null, CancellationToken.None, timeouts, null);
        var elapsed = DateTimeOffset.UtcNow - started;

        Assert.True(elapsed < TimeSpan.FromSeconds(3), $"Scan took {elapsed}, expected about one per-collector budget, not the sum of the slow collectors");
        Assert.Equal(5, snapshot.Components.Count);
        Assert.All(snapshot.Components.Where(c => c.Id == "slow"), slow =>
        {
            Assert.Equal("Not available", slow.Headline);
            Assert.Null(slow.HealthPercent);
        });
        Assert.Equal("Test Laptop", snapshot.Find("system")!.Headline);
        Assert.Equal("16 GB", snapshot.Find("ram")!.Headline);
    }

    [Fact]
    public async Task HungCollectors_DoNotBlockOthersAndEveryCollectorReports()
    {
        var now = DateTimeOffset.UtcNow;
        var service = MachineScanService.Create(new ICollector[]
        {
            new HangingCollector(),
            new HangingCollector(),
            new FixedCollector(TestSnapshots.System(now)),
        });
        var timeouts = new ScanTimeouts(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(30));

        var started = DateTimeOffset.UtcNow;
        var snapshot = await service.ScanAsync(null, CancellationToken.None, timeouts, null);
        var elapsed = DateTimeOffset.UtcNow - started;

        Assert.True(elapsed < TimeSpan.FromSeconds(3), $"Scan took {elapsed}");
        Assert.Equal(3, snapshot.Components.Count);
        Assert.All(snapshot.Components.Where(c => c.Id == "hanging"), hung =>
        {
            Assert.Equal("Not available", hung.Headline);
            Assert.Null(hung.HealthPercent);
        });
        Assert.Equal("Test Laptop", snapshot.Find("system")!.Headline);
    }

    [Fact]
    public async Task CallerCancelledScan_ThrowsOperationCanceledException()
    {
        var now = DateTimeOffset.UtcNow;
        var service = MachineScanService.Create(new ICollector[]
        {
            new FixedCollector(TestSnapshots.System(now)),
        });
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.ScanAsync(null, cancelled.Token, ScanTimeouts.Default, null));
    }

    private sealed class FixedCollector : ICollector
    {
        private readonly ComponentSnapshot _component;

        public FixedCollector(ComponentSnapshot component)
        {
            _component = component;
        }

        // A real collector reports the Id (and Name/Category/IconKey) of the
        // component it produces, so report the wrapped component's values.
        // This matters because MachineScanService synthesizes the
        // NotAvailable fallback for a skipped collector keyed by
        // collector.Id: if the Id here did not match the component's Id,
        // a collector skipped by the overall timeout would appear under a
        // different Id than the component it would have produced, and a
        // slow machine could make Find("system") fail even though the scan
        // behaved correctly (see HungCollector test).
        public string Id => _component.Id;

        public string Name => _component.Name;

        public string Category => _component.Category;

        public string IconKey => _component.IconKey;

        public TimeSpan RefreshInterval => TimeSpan.FromSeconds(2);

        public bool RequiresAdministrator => false;

        public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_component);
        }
    }

    /// <summary>
    /// Never completes and ignores cancellation, like a hung WMI query.
    /// </summary>
    private sealed class HangingCollector : ICollector
    {
        public string Id => "hanging";

        public string Name => "Hanging";

        public string Category => "Test";

        public string IconKey => "system";

        public TimeSpan RefreshInterval => TimeSpan.FromSeconds(2);

        public bool RequiresAdministrator => false;

        public Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default) =>
            new TaskCompletionSource<ComponentSnapshot>().Task;
    }

    /// <summary>
    /// Takes 5 seconds and ignores cancellation, slower than the test budget.
    /// </summary>
    private sealed class SlowCollector : ICollector
    {
        public string Id => "slow";

        public string Name => "Slow";

        public string Category => "Test";

        public string IconKey => "system";

        public TimeSpan RefreshInterval => TimeSpan.FromSeconds(2);

        public bool RequiresAdministrator => false;

        public async Task<ComponentSnapshot> CollectAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            return TestSnapshots.System(DateTimeOffset.UtcNow);
        }
    }
}
