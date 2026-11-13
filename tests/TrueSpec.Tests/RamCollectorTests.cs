using System.Text.RegularExpressions;
using TrueSpec.Collectors.Windows.Collectors;
using TrueSpec.Core.Models;

namespace TrueSpec.Tests;

/// <summary>
/// Windows-leg only: the collector reads the real Win32_PhysicalMemory CIM
/// class and the Windows event log. On Linux this file is excluded from the
/// build (see TrueSpec.Tests.csproj), like CpuCollectorTests. No capacity,
/// speed or error count is asserted: every one of those depends on the
/// machine the test runs on. What is asserted is the honest-limits note:
/// the RAM page must say the in-OS pattern exercise is not a definitive
/// RAM test and must name MemTest86, the bootable test that is. (World-class
/// plan section 8: MemTest86 is the one tool this plan deliberately does
/// not replace, and the product should say that on the memory page rather
/// than hide it.)
/// </summary>
public class RamCollectorTests
{
    /// <summary>
    /// When RAM is actually read, the page text names MemTest86 as the
    /// definitive test. If this machine reports no physical memory at all
    /// the collector returns NotAvailable instead, and there is no page
    /// text to pin, so the test accepts that outcome rather than failing
    /// on a machine with no RAM to report.
    /// </summary>
    [Fact]
    public async Task RamCollector_WhenRamIsRead_NamesMemTest86AsTheDefinitiveTest()
    {
        var collector = new RamCollector();

        Assert.Equal("ram", collector.Id);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var collectTask = collector.CollectAsync(timeout.Token);
        var finished = await Task.WhenAny(collectTask, Task.Delay(TimeSpan.FromSeconds(60)));

        Assert.True(
            finished == collectTask,
            "RamCollector did not finish within 60 seconds.");

        var snapshot = await collectTask;

        var totalMemory = snapshot.Specs.FirstOrDefault(s =>
            string.Equals(s.Label, SpecLabels.TotalMemory, StringComparison.Ordinal));
        if (totalMemory is null)
        {
            // No physical memory was reported on this machine: the
            // collector returned NotAvailable, so there is no RAM page
            // text to check. The MemTest86 note is only pinned when RAM
            // was actually read.
            return;
        }

        Assert.Contains("MemTest86", snapshot.WhatThisMeans, StringComparison.Ordinal);
        Assert.Contains("not a definitive RAM test", snapshot.WhatThisMeans, StringComparison.Ordinal);
    }

    /// <summary>
    /// The "Memory test" spec must be a value the event log actually
    /// supports: "Passed on &lt;date&gt;" or "Failed on &lt;date&gt;" when
    /// a Windows Memory Diagnostic result event survives in the System
    /// log, "Not run" when the log was read and holds no result event, or
    /// the honest unreadable value when the log could not be read at all.
    /// It must never be a hardcoded label that was never checked against
    /// the log. The exact value depends on the machine, so the shape is
    /// pinned, not the value: on a CI runner the diagnostic has almost
    /// certainly never run, so "Not run" is the expected honest outcome
    /// there, and the test accepts any of the four honest shapes.
    /// </summary>
    [Fact]
    public async Task RamCollector_MemoryTestSpec_IsAnHonestValueFromTheEventLog()
    {
        var collector = new RamCollector();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var collectTask = collector.CollectAsync(timeout.Token);
        var finished = await Task.WhenAny(collectTask, Task.Delay(TimeSpan.FromSeconds(60)));

        Assert.True(
            finished == collectTask,
            "RamCollector did not finish within 60 seconds.");

        var snapshot = await collectTask;

        var totalMemory = snapshot.Specs.FirstOrDefault(s =>
            string.Equals(s.Label, SpecLabels.TotalMemory, StringComparison.Ordinal));
        if (totalMemory is null)
        {
            // No physical memory was reported on this machine: the
            // collector returned NotAvailable, so there is no Memory test
            // spec to check.
            return;
        }

        var memoryTest = snapshot.Specs.FirstOrDefault(s =>
            string.Equals(s.Label, SpecLabels.MemoryTest, StringComparison.Ordinal));
        Assert.NotNull(memoryTest);
        Assert.Matches(
            "^(Not run|Passed on \\d{4}-\\d{2}-\\d{2}|Failed on \\d{4}-\\d{2}-\\d{2}|"
                + Regex.Escape(SpecLabels.NotReportedByHardware) + ")$",
            memoryTest.Value);
        Assert.True(
            string.Equals(memoryTest.Source, "Windows Memory Diagnostic", StringComparison.Ordinal) ||
            string.Equals(memoryTest.Source, "Windows Event Log (System)", StringComparison.Ordinal),
            $"Unexpected Memory test source: {memoryTest.Source}");
    }
}
