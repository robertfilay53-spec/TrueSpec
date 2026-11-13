using System.Text;
using System.Text.Json;
using TrueSpec.Core.Benchmarks;
using TrueSpec.Core.Comparison;
using TrueSpec.Core.Models;
using TrueSpec.Core.Reports;

namespace TrueSpec.Tests;

/// <summary>
/// Buyer certificate tests (Blueprint 4, U7): the certificate carries the
/// banner, the score, the five key numbers, the DNA hash and the scan date;
/// the JSON export carries the same fields so a re-scan can be verified
/// programmatically; unmeasured values stay honest, and a full serial never
/// appears in either output.
/// </summary>
public class BuyerCertificateTests
{
    private static MachineDna SampleDna() => MachineDna.Build(new[]
    {
        (MachineDnaPartKind.Board, "Base board", (string?)"ABCD1234EFGH5678"),
        (MachineDnaPartKind.Bios, "BIOS", (string?)"WXYZ9876QRST5432"),
        (MachineDnaPartKind.Disk, "Disk 0", (string?)"DISK000111222333"),
    });

    private static SystemSnapshot SnapshotWithDna()
    {
        var dna = SampleDna();
        return TestSnapshots.Healthy() with
        {
            MachineDnaHash = dna.Hash,
            MachineDnaParts = dna.Parts,
        };
    }

    [Fact]
    public void FromSnapshot_ReadsBannerScoreAndTheFiveNumbers()
    {
        var snapshot = SnapshotWithDna();
        var certificate = BuyerCertificate.FromSnapshot(snapshot);

        Assert.Equal(snapshot.BuyerChecks.BannerText, certificate.Banner);
        Assert.StartsWith("PASS", certificate.Banner, StringComparison.Ordinal);
        Assert.Equal($"{snapshot.Score.OverallScore:0.#} / 100", certificate.Score);
        Assert.Equal("0%", certificate.SsdWear);
        Assert.Equal("90%", certificate.BatteryHealth);
        Assert.Equal("2", certificate.CycleCount);
        Assert.Equal("0", certificate.PowerOnHours);
        Assert.Equal("Not tested - no load test has run", certificate.ThrottleResult);
        Assert.Equal(snapshot.MachineDnaHash, certificate.DnaHash);
        Assert.Equal(snapshot.CapturedAt, certificate.ScanDate);
        Assert.Equal(snapshot.MachineName, certificate.MachineName);
        Assert.True(certificate.HasDna);
        Assert.Equal("DNA recorded", certificate.DnaStatus);
    }

    [Fact]
    public void FromSnapshot_UnmeasuredNumbersSayNotReported()
    {
        var healthy = TestSnapshots.Healthy();
        var snapshot = healthy with
        {
            Components = healthy.Components
                .Where(c => c.Id != "storage" && c.Id != "battery")
                .ToList(),
        };

        var certificate = BuyerCertificate.FromSnapshot(snapshot);

        Assert.Equal(SpecLabels.NotReportedByHardware, certificate.SsdWear);
        Assert.Equal(SpecLabels.NotReportedByHardware, certificate.BatteryHealth);
        Assert.Equal(SpecLabels.NotReportedByHardware, certificate.CycleCount);
        Assert.Equal(SpecLabels.NotReportedByHardware, certificate.PowerOnHours);
    }

    [Fact]
    public void FromSnapshot_LoadTestVerdictBecomesTheThrottleResult()
    {
        var now = DateTimeOffset.UtcNow;
        var loadTest = new LoadTestResult(
            Array.Empty<LoadTestSample>(),
            null, null, null, null,
            Throttled: false, PowerLimited: false, AbortReason: null,
            VerdictText: "No throttling detected",
            DurationSeconds: 60, GapCount: 0, MemoryPatternVerified: true,
            DiskReadMBps: null, StartedAt: now, CompletedAt: now);
        var snapshot = TestSnapshots.Healthy() with { LoadTest = loadTest };

        Assert.Equal("No throttling detected", BuyerCertificate.FromSnapshot(snapshot).ThrottleResult);
    }

    [Fact]
    public void FromSnapshot_ThrottledLoadTestWithoutVerdictTextStillReportsThrottling()
    {
        var now = DateTimeOffset.UtcNow;
        var loadTest = new LoadTestResult(
            Array.Empty<LoadTestSample>(),
            null, null, null, null,
            Throttled: true, PowerLimited: false, AbortReason: null,
            VerdictText: string.Empty,
            DurationSeconds: 60, GapCount: 0, MemoryPatternVerified: true,
            DiskReadMBps: null, StartedAt: now, CompletedAt: now);
        var snapshot = TestSnapshots.Healthy() with { LoadTest = loadTest };

        Assert.Equal("Throttled under load", BuyerCertificate.FromSnapshot(snapshot).ThrottleResult);
    }

    [Fact]
    public void DnaStatus_DistinguishesNotRecordedFromNotAvailable()
    {
        var notRecorded = BuyerCertificate.FromSnapshot(TestSnapshots.Healthy());
        Assert.False(notRecorded.HasDna);
        Assert.Equal("DNA not recorded", notRecorded.DnaStatus);

        var unavailableDna = MachineDna.Build(new[]
        {
            (MachineDnaPartKind.Bios, "BIOS", (string?)"To be filled by O.E.M."),
        });
        var notAvailable = BuyerCertificate.FromSnapshot(TestSnapshots.Healthy() with
        {
            MachineDnaHash = unavailableDna.Hash,
            MachineDnaParts = unavailableDna.Parts,
        });
        Assert.False(notAvailable.HasDna);
        Assert.Equal("DNA not available", notAvailable.DnaStatus);
    }

    [Fact]
    public async Task JsonExport_CarriesTheSameCertificateFields()
    {
        var snapshot = SnapshotWithDna();
        var report = ReportModel.FromSnapshot(snapshot);

        using var stream = new MemoryStream();
        await new JsonReportExporter().ExportAsync(report, stream);

        var json = Encoding.UTF8.GetString(stream.ToArray());
        using var document = JsonDocument.Parse(json);
        var certificate = document.RootElement.GetProperty("certificate");

        Assert.Equal(snapshot.BuyerChecks.BannerText, certificate.GetProperty("banner").GetString());
        Assert.Equal("0%", certificate.GetProperty("ssdWear").GetString());
        Assert.Equal("90%", certificate.GetProperty("batteryHealth").GetString());
        Assert.Equal("2", certificate.GetProperty("cycleCount").GetString());
        Assert.Equal("0", certificate.GetProperty("powerOnHours").GetString());
        Assert.Equal("Not tested - no load test has run", certificate.GetProperty("throttleResult").GetString());
        Assert.Equal(snapshot.MachineDnaHash, certificate.GetProperty("dnaHash").GetString());
        Assert.Equal(snapshot.MachineName, certificate.GetProperty("machineName").GetString());
        Assert.True(certificate.GetProperty("hasDna").GetBoolean());
        Assert.Equal("DNA recorded", certificate.GetProperty("dnaStatus").GetString());
        // The scan date round-trips as the snapshot's capture time.
        Assert.Equal(
            snapshot.CapturedAt,
            certificate.GetProperty("scanDate").GetDateTimeOffset());
    }

    [Fact]
    public async Task JsonExport_NeverContainsAFullSerial()
    {
        var snapshot = SnapshotWithDna();
        var report = ReportModel.FromSnapshot(snapshot);

        using var stream = new MemoryStream();
        await new JsonReportExporter().ExportAsync(report, stream);

        var json = Encoding.UTF8.GetString(stream.ToArray());
        Assert.DoesNotContain("ABCD1234EFGH5678", json, StringComparison.Ordinal);
        Assert.DoesNotContain("WXYZ9876QRST5432", json, StringComparison.Ordinal);
        Assert.DoesNotContain("DISK000111222333", json, StringComparison.Ordinal);
        // The truncated forms and the hash are the only DNA traces.
        Assert.Contains("ABCD...5678", json, StringComparison.Ordinal);
        Assert.Contains(snapshot.MachineDnaHash!, json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task JsonExport_HonestStatesWhenDnaWasNotRecorded()
    {
        var snapshot = TestSnapshots.Healthy();
        var report = ReportModel.FromSnapshot(snapshot);

        using var stream = new MemoryStream();
        await new JsonReportExporter().ExportAsync(report, stream);

        using var document = JsonDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));
        var certificate = document.RootElement.GetProperty("certificate");

        Assert.Equal(JsonValueKind.Null, certificate.GetProperty("dnaHash").ValueKind);
        Assert.False(certificate.GetProperty("hasDna").GetBoolean());
        Assert.Equal("DNA not recorded", certificate.GetProperty("dnaStatus").GetString());
    }
}
