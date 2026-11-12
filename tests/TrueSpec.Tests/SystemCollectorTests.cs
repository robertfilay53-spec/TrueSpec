using System.Globalization;
using TrueSpec.Collectors.Windows.Collectors;

namespace TrueSpec.Tests;

/// <summary>
/// Windows-leg only: the collector reads the real System event log. On
/// Linux this file is excluded from the build (see TrueSpec.Tests.csproj),
/// like StorageCollectorTests. The date depends on the machine the test
/// runs on, so only its shape is asserted: a parseable yyyy-MM-dd date, or
/// "Not reported by hardware" when the log is empty or unreadable.
/// </summary>
public class SystemCollectorTests
{
    [Fact]
    public async Task SystemCollector_ReportsEarliestEventLogEntry()
    {
        var collector = new SystemCollector();

        Assert.Equal("system", collector.Id);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var collectTask = collector.CollectAsync(timeout.Token);
        var finished = await Task.WhenAny(collectTask, Task.Delay(TimeSpan.FromSeconds(60)));

        Assert.True(
            finished == collectTask,
            "SystemCollector did not finish within 60 seconds.");

        var snapshot = await collectTask;

        Assert.Equal("system", snapshot.Id);
        var spec = Assert.Single(
            snapshot.Specs,
            s => string.Equals(s.Label, "Earliest event log entry", StringComparison.Ordinal));
        Assert.Equal("Windows Event Log (System)", spec.Source);
        Assert.True(
            spec.Value == "Not reported by hardware" ||
            DateTimeOffset.TryParseExact(
                spec.Value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out _),
            $"Earliest event log entry was neither a yyyy-MM-dd date nor Not reported: '{spec.Value}'.");
    }
}
