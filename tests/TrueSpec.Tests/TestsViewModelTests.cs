using TrueSpec.App.ViewModels;
using TrueSpec.Core.Benchmarks;
using TrueSpec.Core.Sensors;

namespace TrueSpec.Tests;

/// <summary>
/// Tests for the Tests page ViewModel (hardening item 7): the three
/// previously untested ViewModels get coverage here, starting with the
/// benchmark list and its per-test run states.
/// Windows leg only (it drives TrueSpec.App's ViewModels), like
/// LoadTestViewModelTests: excluded from the net10.0 leg in the csproj.
/// </summary>
public class TestsViewModelTests
{
    [Fact]
    public void TestsViewModel_ListsNineTests_WithOnlyTheImplementedOnesRunnable()
    {
        var viewModel = new TestsViewModel(new UnavailableSensorMonitor(), null, false, null);

        Assert.NotNull(viewModel.LoadTest);
        Assert.Equal(9, viewModel.Tests.Count);
        Assert.Equal("CPU single-thread", viewModel.Tests[0].Name);
        Assert.Equal("CPU multi-thread", viewModel.Tests[1].Name);
        Assert.Equal("Disk read speed", viewModel.Tests[2].Name);
        Assert.Equal("Disk random 4K", viewModel.Tests[3].Name);
        Assert.Equal("Memory copy", viewModel.Tests[4].Name);
        Assert.Equal("Disk latency under load", viewModel.Tests[5].Name);
        Assert.Equal("Quick memory test", viewModel.Tests[6].Name);
        Assert.Equal("Battery runtime estimate", viewModel.Tests[7].Name);
        Assert.Equal("Wi-Fi speed test", viewModel.Tests[8].Name);

        Assert.True(viewModel.Tests[0].CanRun);
        Assert.True(viewModel.Tests[1].CanRun);
        Assert.True(viewModel.Tests[2].CanRun);
        Assert.True(viewModel.Tests[3].CanRun);
        Assert.True(viewModel.Tests[4].CanRun);
        Assert.True(viewModel.Tests[5].CanRun);
        Assert.False(viewModel.Tests[6].CanRun);
        Assert.False(viewModel.Tests[7].CanRun);
        Assert.False(viewModel.Tests[8].CanRun);

        Assert.Equal("Run test", viewModel.Tests[0].RunButtonText);
        Assert.Equal("Not available", viewModel.Tests[6].RunButtonText);
        Assert.Equal("Not available in this build", viewModel.Tests[6].Status);
        Assert.Equal("Not run yet.", viewModel.Tests[0].ResultText);
    }

    [Fact]
    public void TestsViewModel_BenchmarkScopeNote_NamesGeekbenchAnd3DMark()
    {
        var viewModel = new TestsViewModel(new UnavailableSensorMonitor(), null, false, null);

        Assert.Contains("Geekbench", viewModel.BenchmarkScopeNote, StringComparison.Ordinal);
        Assert.Contains("3DMark", viewModel.BenchmarkScopeNote, StringComparison.Ordinal);
        Assert.Contains("do not replace", viewModel.BenchmarkScopeNote, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestItem_RunAsync_ReportsTheMeasuredResult()
    {
        var item = new TestItemViewModel(
            "Fake test",
            "A test double.",
            _ => Task.FromResult(new BenchmarkResult(
                "Fake test", true, "1,234 ops/s", "Measured 1,234 operations per second.", "Local benchmark", 1234d, "ops/s")));

        await item.RunCommand.ExecuteAsync(null);

        Assert.False(item.IsRunning);
        Assert.Equal("1,234 ops/s", item.Status);
        Assert.Equal("Measured 1,234 operations per second. Source: Local benchmark.", item.ResultText);
        Assert.Equal("Run test", item.RunButtonText);
    }

    [Fact]
    public async Task TestItem_RunAsync_ReportsAFailureHonestly()
    {
        var item = new TestItemViewModel(
            "Fake test",
            "A test double.",
            _ => throw new InvalidOperationException("disk vanished"));

        await item.RunCommand.ExecuteAsync(null);

        Assert.False(item.IsRunning);
        Assert.Equal("Test failed", item.Status);
        Assert.Contains("disk vanished", item.ResultText, StringComparison.Ordinal);
        Assert.Contains("did not complete", item.ResultText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestItem_RunAsync_DisablesRunWhileRunning()
    {
        var started = new TaskCompletionSource<BenchmarkResult>();
        var item = new TestItemViewModel("Fake test", "A test double.", _ => started.Task);

        var runTask = item.RunCommand.ExecuteAsync(null);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!item.IsRunning && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.True(item.IsRunning, "The test never entered the running state.");
        Assert.False(item.CanRunNow);
        Assert.Equal("Running…", item.RunButtonText);
        Assert.Equal("Running...", item.Status);

        started.SetResult(new BenchmarkResult(
            "Fake test", true, "Done", "Finished.", "Local benchmark", null, string.Empty));
        await runTask;

        Assert.False(item.IsRunning);
        Assert.True(item.CanRunNow);
        Assert.Equal("Done", item.Status);
    }

    [Fact]
    public async Task TestItem_RunAsync_UnavailableTestIsANoOp()
    {
        var viewModel = new TestsViewModel(new UnavailableSensorMonitor(), null, false, null);
        var item = viewModel.Tests[6];

        await item.RunCommand.ExecuteAsync(null);

        Assert.False(item.IsRunning);
        Assert.Equal("Not available in this build", item.Status);
        Assert.Equal("This test is not implemented yet, so no result is shown.", item.ResultText);
    }
}
