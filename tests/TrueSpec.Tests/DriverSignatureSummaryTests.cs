using TrueSpec.Collectors.Windows.Collectors;
using TrueSpec.Collectors.Windows.Wmi;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;

namespace TrueSpec.Tests;

/// <summary>
/// H2 defect-class regression tests, follow-up to PR #12: SystemCollector
/// and SecurityCollector enumerated the same Win32_PnPSignedDriver class
/// with the 2-arg Wmi.Query, so their Installed/Unsigned/Unknown driver
/// counts were silently truncated at the WMI 1000-row cap exactly the
/// way H2 was. Both now share CollectorSupport.ReadDriverSignatureSummary
/// (3-arg Query, error captured); the tests drive the pure summarizer
/// and the collectors' pure spec builders with fake rows, so no live WMI
/// is needed — same style as DriversCollectorTests in PR #12.
/// Windows leg only (the helpers live in the Windows collectors
/// assembly): excluded from the net10.0 leg in the csproj.
/// </summary>
public class DriverSignatureSummaryTests
{
    private static WmiRow SignedRow(bool? signed) =>
        new(new Dictionary<string, object?> { ["IsSigned"] = signed });

    private static List<WmiRow> Rows(int signed, int unsigned, int unknown)
    {
        var rows = new List<WmiRow>(signed + unsigned + unknown);
        for (var i = 0; i < signed; i++)
        {
            rows.Add(SignedRow(true));
        }

        for (var i = 0; i < unsigned; i++)
        {
            rows.Add(SignedRow(false));
        }

        for (var i = 0; i < unknown; i++)
        {
            rows.Add(SignedRow(null));
        }

        return rows;
    }

    private static string? SpecValue(IReadOnlyList<SpecValue> specs, string label) =>
        specs.FirstOrDefault(s => s.Label == label)?.Value;

    [Fact]
    public void Summarize_TruncatedFlag_FollowsTheQueryError()
    {
        var rows = Rows(1000, 0, 0);

        var truncated = CollectorSupport.SummarizeDriverSignatures(rows, Wmi.TruncatedError);
        Assert.True(truncated.Truncated);
        Assert.Equal(1000, truncated.Total);

        var full = CollectorSupport.SummarizeDriverSignatures(rows, null);
        Assert.False(full.Truncated);
        Assert.Equal(1000, full.Total);

        var otherError = CollectorSupport.SummarizeDriverSignatures(rows, "some other failure");
        Assert.False(otherError.Truncated);
    }

    [Fact]
    public void Summarize_CountsSignedUnsignedAndUnknown()
    {
        var summary = CollectorSupport.SummarizeDriverSignatures(Rows(998, 1, 1), null);

        Assert.Equal(1000, summary.Total);
        Assert.Equal(1, summary.Unsigned);
        Assert.Equal(1, summary.Unknown);
        Assert.False(summary.Truncated);
    }

    [Fact]
    public void Summarize_EmptyList_ReturnsNullsNotZeros()
    {
        // No rows means the class was not reported, not "zero drivers".
        var summary = CollectorSupport.SummarizeDriverSignatures(Array.Empty<WmiRow>(), null);

        Assert.Null(summary.Total);
        Assert.Null(summary.Unsigned);
        Assert.Null(summary.Unknown);
        Assert.False(summary.Truncated);
    }

    [Fact]
    public void SystemCollector_TruncatedList_QualifiesTheCountsAndNeverClaimsAllSigned()
    {
        // 1000 signed drivers and nothing else: without the truncation
        // flag this would read "Signed drivers only: Yes". The list is
        // partial, so the claim is unproven and must not be made.
        var summary = CollectorSupport.SummarizeDriverSignatures(Rows(1000, 0, 0), Wmi.TruncatedError);
        var specs = SystemCollector.DriverSignatureSpecs(summary);

        Assert.Equal("1000", SpecValue(specs, SpecLabels.InstalledDrivers));
        Assert.Equal("0", SpecValue(specs, SpecLabels.UnsignedDrivers));
        Assert.Equal(SpecLabels.NotReportedByHardware, SpecValue(specs, SpecLabels.SignedDriversOnly));
        var note = specs.FirstOrDefault(s => s.Label == "Driver list truncated");
        Assert.NotNull(note);
        Assert.Equal(1, specs.IndexOf(note!));
        Assert.Contains("truncated at 1000 rows", note!.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("first 1000", note.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SystemCollector_FullList_ExactCountsAndSignedOnlyYes()
    {
        var summary = CollectorSupport.SummarizeDriverSignatures(Rows(3, 0, 0), null);
        var specs = SystemCollector.DriverSignatureSpecs(summary);

        Assert.Equal("3", SpecValue(specs, SpecLabels.InstalledDrivers));
        Assert.Equal("0", SpecValue(specs, SpecLabels.UnsignedDrivers));
        Assert.Equal("0", SpecValue(specs, SpecLabels.UnknownDriverSignature));
        Assert.Equal("Yes", SpecValue(specs, SpecLabels.SignedDriversOnly));
        Assert.DoesNotContain(specs, s => s.Label == "Driver list truncated");
    }

    [Fact]
    public void SystemCollector_FullList_OneUnsignedDriver_SaysNo()
    {
        var summary = CollectorSupport.SummarizeDriverSignatures(Rows(2, 1, 0), null);
        var specs = SystemCollector.DriverSignatureSpecs(summary);

        Assert.Equal("1", SpecValue(specs, SpecLabels.UnsignedDrivers));
        Assert.Equal("No", SpecValue(specs, SpecLabels.SignedDriversOnly));
        Assert.DoesNotContain(specs, s => s.Label == "Driver list truncated");
    }

    [Fact]
    public void SystemCollector_NoData_AllNotReported()
    {
        var summary = CollectorSupport.SummarizeDriverSignatures(Array.Empty<WmiRow>(), null);
        var specs = SystemCollector.DriverSignatureSpecs(summary);

        Assert.Equal(SpecLabels.NotReportedByHardware, SpecValue(specs, SpecLabels.InstalledDrivers));
        Assert.Equal(SpecLabels.NotReportedByHardware, SpecValue(specs, SpecLabels.UnsignedDrivers));
        Assert.Equal(SpecLabels.NotReportedByHardware, SpecValue(specs, SpecLabels.UnknownDriverSignature));
        Assert.Equal(SpecLabels.NotReportedByHardware, SpecValue(specs, SpecLabels.SignedDriversOnly));
        Assert.DoesNotContain(specs, s => s.Label == "Driver list truncated");
    }

    [Fact]
    public void SecurityCollector_TruncatedList_QualifiesTheUnsignedCount()
    {
        var summary = CollectorSupport.SummarizeDriverSignatures(Rows(999, 1, 0), Wmi.TruncatedError);
        var specs = SecurityCollector.DriverSignatureSpecs(summary);

        // The count is the rows actually read: a lower bound, qualified
        // by the note, never presented as the total.
        Assert.Equal("1", SpecValue(specs, SpecLabels.UnsignedDrivers));
        var note = specs.FirstOrDefault(s => s.Label == "Driver list truncated");
        Assert.NotNull(note);
        Assert.Contains("first 1000", note!.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SecurityCollector_FullList_ExactUnsignedCountAndNoNote()
    {
        var summary = CollectorSupport.SummarizeDriverSignatures(Rows(5, 2, 0), null);
        var specs = SecurityCollector.DriverSignatureSpecs(summary);

        Assert.Equal("2", SpecValue(specs, SpecLabels.UnsignedDrivers));
        Assert.DoesNotContain(specs, s => s.Label == "Driver list truncated");
    }

    [Fact]
    public void SecurityCollector_NoData_UnsignedIsNotReported()
    {
        var summary = CollectorSupport.SummarizeDriverSignatures(Array.Empty<WmiRow>(), null);
        var specs = SecurityCollector.DriverSignatureSpecs(summary);

        Assert.Equal(SpecLabels.NotReportedByHardware, SpecValue(specs, SpecLabels.UnsignedDrivers));
        Assert.DoesNotContain(specs, s => s.Label == "Driver list truncated");
    }

    private static async Task<ComponentSnapshot> CollectAsync(ICollector collector)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var collectTask = collector.CollectAsync(timeout.Token);
        var finished = await Task.WhenAny(collectTask, Task.Delay(TimeSpan.FromSeconds(60)));
        Assert.True(finished == collectTask, $"{collector.Id} did not finish within 60 seconds.");
        return await collectTask;
    }

    [Fact]
    public async Task Live_SystemCollector_ReportsExactDriverCountsWithNoTruncationNote()
    {
        // The CI machine has far fewer than 1000 drivers, so the live
        // list is complete: the counts are exact and no truncation note
        // appears. This guards the wiring (shared 3-arg query feeding
        // the specs), not the truncation branch itself.
        var snapshot = await CollectAsync(new SystemCollector());

        var total = SpecValue(snapshot.Specs, SpecLabels.InstalledDrivers);
        Assert.NotEqual(SpecLabels.NotReportedByHardware, total);
        Assert.True(int.TryParse(total, out var count) && count > 0, $"Expected a positive driver count, got '{total}'.");
        Assert.DoesNotContain(snapshot.Specs, s => s.Label == "Driver list truncated");
    }

    [Fact]
    public async Task Live_SecurityCollector_ReportsUnsignedCountWithNoTruncationNote()
    {
        var snapshot = await CollectAsync(new SecurityCollector());

        var unsigned = SpecValue(snapshot.Specs, SpecLabels.UnsignedDrivers);
        Assert.NotEqual(SpecLabels.NotReportedByHardware, unsigned);
        Assert.True(int.TryParse(unsigned, out var count) && count >= 0, $"Expected an unsigned-driver count, got '{unsigned}'.");
        Assert.DoesNotContain(snapshot.Specs, s => s.Label == "Driver list truncated");
    }
}
