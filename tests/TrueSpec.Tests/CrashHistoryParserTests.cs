using TrueSpec.Core.Collectors;

namespace TrueSpec.Tests;

/// <summary>
/// Core parser tests (run everywhere, including Linux CI): the parser is pure
/// and works only from provider + event id + timestamp. The free-text message
/// is never parsed for facts, so a misleading message cannot invent a crash.
/// </summary>
public class CrashHistoryParserTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    private static CrashHistoryParser.RawCrashEvent Row(
        string provider,
        int eventId,
        DateTimeOffset at,
        string? message = null) =>
        new(provider, eventId, at, message);

    [Fact]
    public void BugCheckRow_MapsToBugCheckClassWithFixedMeaning()
    {
        var rows = new[]
        {
            Row("Microsoft-Windows-WER-SystemErrorReporting", 1001, Now.AddDays(-5)),
        };

        var history = CrashHistoryParser.Parse(rows, Now, installDate: null);

        var summary = Assert.Single(history.Summaries);
        Assert.Equal(CrashClass.BugCheck, summary.Class);
        Assert.Equal(1, summary.Count90d);
        Assert.Equal(Now.AddDays(-5), summary.FirstAt);
        Assert.Equal(Now.AddDays(-5), summary.LastAt);
        Assert.Equal("Windows stopped unexpectedly (blue screen)", summary.PlainMeaning);
    }

    [Fact]
    public void WheaFatalRow_MapsToWheaFatalClassWithFixedMeaning()
    {
        var rows = new[]
        {
            Row("Microsoft-Windows-WHEA-Logger", 18, Now.AddDays(-2)),
        };

        var history = CrashHistoryParser.Parse(rows, Now, installDate: null);

        var summary = Assert.Single(history.Summaries);
        Assert.Equal(CrashClass.WheaFatal, summary.Class);
        Assert.Equal(1, summary.Count90d);
        Assert.Equal("a hardware component reported a fatal error", summary.PlainMeaning);
    }

    [Fact]
    public void WheaCorrectedRow_MapsToWheaCorrectedClassWithFixedMeaning()
    {
        var rows = new[]
        {
            Row("Microsoft-Windows-WHEA-Logger", 19, Now.AddDays(-1)),
            Row("Microsoft-Windows-WHEA-Logger", 20, Now.AddDays(-1)),
        };

        var history = CrashHistoryParser.Parse(rows, Now, installDate: null);

        var summary = Assert.Single(history.Summaries);
        Assert.Equal(CrashClass.WheaCorrected, summary.Class);
        Assert.Equal(2, summary.Count90d);
        Assert.Equal("a hardware error was corrected automatically", summary.PlainMeaning);
    }

    [Fact]
    public void KernelPower41Row_MapsToUnexpectedShutdownClassWithFixedMeaning()
    {
        var rows = new[]
        {
            Row("Microsoft-Windows-Kernel-Power", 41, Now.AddDays(-3)),
        };

        var history = CrashHistoryParser.Parse(rows, Now, installDate: null);

        var summary = Assert.Single(history.Summaries);
        Assert.Equal(CrashClass.UnexpectedShutdown, summary.Class);
        Assert.Equal(1, summary.Count90d);
        Assert.Equal("the machine lost power or shut down unexpectedly", summary.PlainMeaning);
    }

    [Fact]
    public void DiskAndNtfsRows_MapToDiskErrorClassWithFixedMeaning()
    {
        var rows = new[]
        {
            Row("Disk", 7, Now.AddDays(-10)),
            Row("Disk", 11, Now.AddDays(-9)),
            Row("Disk", 51, Now.AddDays(-8)),
            Row("Ntfs", 55, Now.AddDays(-7)),
            Row("Ntfs", 98, Now.AddDays(-6)),
            Row("Microsoft-Windows-Ntfs", 140, Now.AddDays(-5)),
        };

        var history = CrashHistoryParser.Parse(rows, Now, installDate: null);

        var summary = Assert.Single(history.Summaries);
        Assert.Equal(CrashClass.DiskError, summary.Class);
        Assert.Equal(6, summary.Count90d);
        Assert.Equal(Now.AddDays(-10), summary.FirstAt);
        Assert.Equal(Now.AddDays(-5), summary.LastAt);
        Assert.Equal("the disk reported an error", summary.PlainMeaning);
    }

    [Fact]
    public void WindowBoundary_EventExactly90DaysOldCounts_OlderDoesNotCountButSetsFirst()
    {
        var rows = new[]
        {
            Row("Microsoft-Windows-WER-SystemErrorReporting", 1001, Now.AddDays(-90)),
            Row("Microsoft-Windows-WER-SystemErrorReporting", 1001, Now.AddDays(-90).AddSeconds(-1)),
        };

        var history = CrashHistoryParser.Parse(rows, Now, installDate: null);

        var summary = Assert.Single(history.Summaries);
        Assert.Equal(1, summary.Count90d);
        Assert.Equal(Now.AddDays(-90).AddSeconds(-1), summary.FirstAt);
        Assert.Equal(Now.AddDays(-90), summary.LastAt);
    }

    [Fact]
    public void EmptyLog_YieldsNoSummariesAndNoClearedSuspicion()
    {
        var history = CrashHistoryParser.Parse(
            Array.Empty<CrashHistoryParser.RawCrashEvent>(),
            Now,
            installDate: Now.AddYears(-1));

        Assert.Empty(history.Summaries);
        Assert.Null(history.EarliestLogEntry);
        Assert.False(history.LogClearedSuspected);
    }

    [Fact]
    public void MoreThan500EventsPerClass_CapsAtNewest500()
    {
        var rows = Enumerable.Range(0, 600)
            .Select(i => Row(
                "Microsoft-Windows-WER-SystemErrorReporting",
                1001,
                Now.AddMinutes(-i)))
            .ToArray();

        var history = CrashHistoryParser.Parse(rows, Now, installDate: null);

        var summary = Assert.Single(history.Summaries);
        Assert.Equal(500, summary.Count90d);
        // The cap keeps the newest 500, so the earliest kept event is the
        // 500th newest (599 minutes ago would be the oldest of all 600).
        Assert.Equal(Now.AddMinutes(-499), summary.FirstAt);
        Assert.Equal(Now, summary.LastAt);
    }

    [Fact]
    public void CapAppliesPerClass_NotAcrossAllClasses()
    {
        var bugChecks = Enumerable.Range(0, 600)
            .Select(i => Row(
                "Microsoft-Windows-WER-SystemErrorReporting",
                1001,
                Now.AddMinutes(-i)));
        var shutdowns = new[]
        {
            Row("Microsoft-Windows-Kernel-Power", 41, Now.AddDays(-1)),
        };

        var history = CrashHistoryParser.Parse(
            bugChecks.Concat(shutdowns).ToArray(),
            Now,
            installDate: null);

        Assert.Equal(2, history.Summaries.Count);
        Assert.Equal(500, history.Summaries.Single(s => s.Class == CrashClass.BugCheck).Count90d);
        Assert.Equal(1, history.Summaries.Single(s => s.Class == CrashClass.UnexpectedShutdown).Count90d);
    }

    [Fact]
    public void ClearedLogRule_EarliestEntryAfterInstallPlusSevenDays_IsSuspected()
    {
        var installDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var rows = new[]
        {
            Row("Microsoft-Windows-WER-SystemErrorReporting", 1001, installDate.AddDays(30)),
        };

        var history = CrashHistoryParser.Parse(rows, Now, installDate);

        Assert.Equal(installDate.AddDays(30), history.EarliestLogEntry);
        Assert.True(history.LogClearedSuspected);
    }

    [Fact]
    public void ClearedLogRule_EarliestEntryWithinSevenDaysOfInstall_IsNotSuspected()
    {
        var installDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var rows = new[]
        {
            Row("Microsoft-Windows-WER-SystemErrorReporting", 1001, installDate.AddDays(7)),
        };

        var history = CrashHistoryParser.Parse(rows, Now, installDate);

        Assert.False(history.LogClearedSuspected);
    }

    [Fact]
    public void ClearedLogRule_WithoutInstallDate_IsNeverSuspected()
    {
        var rows = new[]
        {
            Row("Microsoft-Windows-WER-SystemErrorReporting", 1001, Now.AddDays(-1)),
        };

        var history = CrashHistoryParser.Parse(rows, Now, installDate: null);

        Assert.False(history.LogClearedSuspected);
    }

    [Fact]
    public void ClearedLogRule_UsesSuppliedEarliestEntry_NotJustCrashRows()
    {
        // The collector passes the oldest entry of the whole System channel;
        // the rule must use that, not the oldest crash row.
        var installDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var rows = new[]
        {
            Row("Microsoft-Windows-WER-SystemErrorReporting", 1001, installDate.AddDays(2)),
        };

        var history = CrashHistoryParser.Parse(
            rows,
            Now,
            installDate,
            earliestLogEntry: installDate.AddDays(30));

        Assert.True(history.LogClearedSuspected);
    }

    [Fact]
    public void UnknownProviderOrEvent_IsIgnored()
    {
        var rows = new[]
        {
            Row("SomeOtherProvider", 1001, Now.AddDays(-1)),
            Row("Microsoft-Windows-WER-SystemErrorReporting", 9999, Now.AddDays(-1)),
            Row("Disk", 999, Now.AddDays(-1)),
        };

        var history = CrashHistoryParser.Parse(rows, Now, installDate: null);

        Assert.Empty(history.Summaries);
    }

    [Fact]
    public void MessageText_IsNeverParsedForFacts()
    {
        // A Disk event whose message claims a blue screen is still a disk
        // error, and an unknown provider whose message claims a blue screen
        // is still ignored. Only provider + event id + timestamp count.
        var rows = new[]
        {
            Row("Disk", 7, Now.AddDays(-1), "The computer has rebooted from a bugcheck. Blue screen!"),
            Row("UnknownProvider", 1234, Now.AddDays(-1), "Windows stopped unexpectedly (blue screen)"),
        };

        var history = CrashHistoryParser.Parse(rows, Now, installDate: null);

        var summary = Assert.Single(history.Summaries);
        Assert.Equal(CrashClass.DiskError, summary.Class);
        Assert.Equal(1, summary.Count90d);
    }

    [Fact]
    public void PlainMeaningTable_CoversEveryClass()
    {
        foreach (var crashClass in Enum.GetValues<CrashClass>())
        {
            var meaning = CrashHistoryParser.PlainMeaning(crashClass);

            Assert.False(string.IsNullOrWhiteSpace(meaning));
        }
    }
}
