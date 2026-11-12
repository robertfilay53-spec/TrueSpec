using TrueSpec.Collectors.Windows.Collectors;

namespace TrueSpec.Tests;

/// <summary>
/// Windows-leg only: the collector reads the real System event log. On Linux
/// this file is excluded from the build (see TrueSpec.Tests.csproj), like
/// StartupCollectorTests. No counts are asserted: they depend on the machine
/// the test runs on. The CI Windows runner's log may be empty or not; either
/// way the collector must return an honest snapshot (or an honest
/// NotAvailable) and never throw or fabricate an event.
/// </summary>
public class CrashCollectorTests
{
    [Fact]
    public async Task CrashCollector_ReturnsHonestSnapshotOrNotAvailable_AndNeverThrows()
    {
        var collector = new CrashCollector();

        Assert.Equal("crash", collector.Id);
        Assert.Equal("Crash history", collector.Name);
        Assert.Equal("Diagnostics", collector.Category);
        Assert.False(collector.RequiresAdministrator);
        Assert.Equal(TimeSpan.FromHours(1), collector.RefreshInterval);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var collectTask = collector.CollectAsync(timeout.Token);
        var finished = await Task.WhenAny(collectTask, Task.Delay(TimeSpan.FromSeconds(60)));

        Assert.True(
            finished == collectTask,
            "CrashCollector did not finish within 60 seconds.");

        var snapshot = await collectTask;

        Assert.Equal("crash", snapshot.Id);
        Assert.Null(snapshot.HealthPercent);
        Assert.All(snapshot.Specs, spec => Assert.False(string.IsNullOrWhiteSpace(spec.Source)));

        if (snapshot.Headline == "Not available")
        {
            Assert.Contains(snapshot.Specs, spec => spec.Label == "Status");
            return;
        }

        // A real snapshot: the headline is one of the honest forms, and
        // every count spec is an exact non-negative integer from the log.
        Assert.True(
            snapshot.Headline == "No crashes recorded in the last 90 days" ||
            snapshot.Headline.EndsWith(" in the last 90 days", StringComparison.Ordinal),
            $"Unexpected headline: {snapshot.Headline}");

        foreach (var spec in snapshot.Specs.Where(s =>
            s.Label.EndsWith("(last 90 days)", StringComparison.Ordinal)))
        {
            Assert.True(
                int.TryParse(spec.Value, out var count) && count >= 0,
                $"Count spec '{spec.Label}' was not a non-negative integer: '{spec.Value}'");
        }
    }
}
