using TrueSpec.Core;
using TrueSpec.Core.Collectors;
using TrueSpec.Core.Comparison;

namespace TrueSpec.Tests;

/// <summary>
/// The scan pipeline records the machine fingerprint on every scan
/// (Blueprint 4, U7): a provider-supplied DNA lands on the snapshot, and
/// a missing or failing provider leaves the snapshot honestly without
/// DNA instead of failing the scan.
/// </summary>
public class MachineDnaScanTests
{
    private sealed class FakeDnaProvider : IMachineDnaProvider
    {
        public MachineDna? Result { get; set; }

        public Exception? Failure { get; set; }

        public Task<MachineDna?> CollectAsync(CancellationToken cancellationToken = default) =>
            Failure is null
                ? Task.FromResult(Result)
                : Task.FromException<MachineDna?>(Failure);
    }

    private static MachineDna SampleDna() => MachineDna.Build(new[]
    {
        (MachineDnaPartKind.Board, "Base board", (string?)"ABCD1234EFGH5678"),
        (MachineDnaPartKind.Bios, "BIOS", (string?)"WXYZ9876QRST5432"),
    });

    [Fact]
    public async Task Scan_PopulatesDnaHashAndParts_FromProvider()
    {
        var dna = SampleDna();
        var service = MachineScanService.Create(
            UnavailableCollectorCatalog.CreateAll(),
            new FakeDnaProvider { Result = dna });

        var snapshot = await service.ScanAsync();

        Assert.Equal(dna.Hash, snapshot.MachineDnaHash);
        Assert.Equal(dna.Parts, snapshot.MachineDnaParts);
        Assert.Equal(dna.Hash, snapshot.MachineDnaStatus);
    }

    [Fact]
    public async Task Scan_WithNoProvider_LeavesDnaNotRecorded()
    {
        var snapshot = await MachineScanService.CreateDefault().ScanAsync();

        Assert.Null(snapshot.MachineDnaHash);
        Assert.Null(snapshot.MachineDnaParts);
        Assert.Equal("DNA not recorded", snapshot.MachineDnaStatus);
    }

    [Fact]
    public async Task Scan_WithFailingProvider_StillCompletesScanWithDnaNotRecorded()
    {
        var service = MachineScanService.Create(
            UnavailableCollectorCatalog.CreateAll(),
            new FakeDnaProvider { Failure = new InvalidOperationException("WMI unavailable") });

        var snapshot = await service.ScanAsync();

        Assert.NotNull(snapshot.Components);
        Assert.Null(snapshot.MachineDnaHash);
        Assert.Null(snapshot.MachineDnaParts);
        Assert.Equal("DNA not recorded", snapshot.MachineDnaStatus);
    }

    [Fact]
    public async Task Scan_WithUnavailableDna_ReportsDnaNotAvailable()
    {
        var service = MachineScanService.Create(
            UnavailableCollectorCatalog.CreateAll(),
            new FakeDnaProvider { Result = MachineDna.Empty });

        var snapshot = await service.ScanAsync();

        Assert.Null(snapshot.MachineDnaHash);
        Assert.NotNull(snapshot.MachineDnaParts);
        Assert.Empty(snapshot.MachineDnaParts);
        Assert.Equal("DNA not available", snapshot.MachineDnaStatus);
    }
}
