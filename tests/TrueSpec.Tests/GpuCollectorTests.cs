using TrueSpec.Collectors.Windows.Collectors;
using TrueSpec.Core.HardwareDatabase;
using TrueSpec.Core.Models;
using TrueSpec.Core.Sensors;

namespace TrueSpec.Tests;

/// <summary>
/// Windows-leg only: the collector reads the real CIM video-controller
/// class. On Linux this file is excluded from the build (see
/// TrueSpec.Tests.csproj), like CpuCollectorTests. No temperature or
/// clock is asserted: they depend on the machine the test runs on. What
/// is asserted is the coupling: the rated specs from the bundled
/// hardware database (Beyond v2.0, item 5) are shown under their own
/// labels with the dataset version as their source, they sit after the
/// measured dedicated memory, and they never replace it. The stub
/// database matches every model, so the test is deterministic whatever
/// GPU the runner has.
/// </summary>
public class GpuCollectorTests
{
    /// <summary>
    /// The rated boost clock, memory and board power from the bundled
    /// hardware database are shown under their own labels with the
    /// dataset version as their source, after the measured dedicated
    /// memory, and never in its place.
    /// </summary>
    [Fact]
    public async Task GpuCollector_RatedSpecsAreShownAsTheirOwnSpecs()
    {
        var database = new FixedHardwareDatabase(
            new GpuSpec("TEST", "NVIDIA", "Test GPU", 2.37, 8, 115, "TGP range 35-115 W; figure is the maximum", "https://example.invalid/test", "2026-11-14"));
        var snapshot = await CollectWithDatabaseAsync(database);

        var memory = Spec(snapshot, SpecLabels.DedicatedMemory);
        var ratedBoost = Spec(snapshot, SpecLabels.RatedBoostClock);
        var ratedMemory = Spec(snapshot, SpecLabels.RatedMemory);
        var ratedPower = Spec(snapshot, SpecLabels.RatedBoardPower);

        Assert.NotNull(memory);
        Assert.Equal("CIM Win32_VideoController.AdapterRAM", memory.Source);
        Assert.NotNull(ratedBoost);
        Assert.Equal("2.37 GHz", ratedBoost.Value);
        Assert.Equal(2370, ratedBoost.NumericValue);
        Assert.Equal("MHz", ratedBoost.NumericUnit);
        Assert.Contains("Bundled hardware database v9.9.9 (2026-11-14)", ratedBoost.Source, StringComparison.Ordinal);
        Assert.NotNull(ratedMemory);
        Assert.Equal("8 GB", ratedMemory.Value);
        Assert.Equal(8, ratedMemory.NumericValue);
        Assert.Equal("GB", ratedMemory.NumericUnit);
        Assert.Contains("Bundled hardware database v9.9.9 (2026-11-14)", ratedMemory.Source, StringComparison.Ordinal);
        Assert.NotNull(ratedPower);
        Assert.Equal("115 W", ratedPower.Value);
        Assert.Equal(115, ratedPower.NumericValue);
        Assert.Equal("W", ratedPower.NumericUnit);
        Assert.Contains("Bundled hardware database v9.9.9 (2026-11-14)", ratedPower.Source, StringComparison.Ordinal);

        // The rated specs follow the measured one in the list and never
        // take its place.
        var labels = snapshot.Specs.Select(s => s.Label).ToList();
        var memoryIndex = labels.IndexOf(SpecLabels.DedicatedMemory);
        Assert.True(labels.IndexOf(SpecLabels.RatedBoostClock) > memoryIndex);
        Assert.True(labels.IndexOf(SpecLabels.RatedMemory) > memoryIndex);
        Assert.True(labels.IndexOf(SpecLabels.RatedBoardPower) > memoryIndex);
    }

    /// <summary>
    /// A model the dataset does not list gets no rated specs at all:
    /// the lookup never guesses, and the collector stays silent.
    /// </summary>
    [Fact]
    public async Task GpuCollector_UnknownModelGetsNoRatedSpec()
    {
        var snapshot = await CollectWithDatabaseAsync(new NeverMatchHardwareDatabase());

        Assert.Null(Spec(snapshot, SpecLabels.RatedBoostClock));
        Assert.Null(Spec(snapshot, SpecLabels.RatedMemory));
        Assert.Null(Spec(snapshot, SpecLabels.RatedBoardPower));
        Assert.NotNull(Spec(snapshot, SpecLabels.DedicatedMemory));
    }

    /// <summary>No database supplied means no rated specs, exactly like a model that is not listed.</summary>
    [Fact]
    public async Task GpuCollector_NoDatabaseGetsNoRatedSpec()
    {
        var snapshot = await CollectWithDatabaseAsync(null);

        Assert.Null(Spec(snapshot, SpecLabels.RatedBoostClock));
        Assert.Null(Spec(snapshot, SpecLabels.RatedMemory));
        Assert.Null(Spec(snapshot, SpecLabels.RatedBoardPower));
    }

    /// <summary>
    /// A model with no single boost figure (GTX 1650 G5/G6) shows its
    /// rated memory and board power and no rated boost clock.
    /// </summary>
    [Fact]
    public async Task GpuCollector_NullBoostShowsMemoryAndPowerOnly()
    {
        var database = new FixedHardwareDatabase(
            new GpuSpec("TEST", "NVIDIA", "Test GPU", null, 4, 75, null, "https://example.invalid/test", "2026-11-14"));
        var snapshot = await CollectWithDatabaseAsync(database);

        Assert.Null(Spec(snapshot, SpecLabels.RatedBoostClock));
        Assert.NotNull(Spec(snapshot, SpecLabels.RatedMemory));
        Assert.NotNull(Spec(snapshot, SpecLabels.RatedBoardPower));
    }

    /// <summary>
    /// A model with no single memory figure (RTX 3060 12 GB / 8 GB)
    /// shows its rated boost clock and board power and no rated memory.
    /// </summary>
    [Fact]
    public async Task GpuCollector_NullMemoryShowsBoostAndPowerOnly()
    {
        var database = new FixedHardwareDatabase(
            new GpuSpec("TEST", "NVIDIA", "Test GPU", 1.78, null, 170, null, "https://example.invalid/test", "2026-11-14"));
        var snapshot = await CollectWithDatabaseAsync(database);

        Assert.NotNull(Spec(snapshot, SpecLabels.RatedBoostClock));
        Assert.Null(Spec(snapshot, SpecLabels.RatedMemory));
        Assert.NotNull(Spec(snapshot, SpecLabels.RatedBoardPower));
    }

    /// <summary>
    /// A model with no single memory or board-power figure (RX 9060 XT
    /// 8 GB / 150 W and 16 GB / 160 W) shows its rated boost clock and
    /// nothing else: the collector must not crash on the missing power.
    /// </summary>
    [Fact]
    public async Task GpuCollector_NullMemoryAndPowerShowsBoostOnly()
    {
        var database = new FixedHardwareDatabase(
            new GpuSpec("TEST", "AMD", "Test GPU", 3.13, null, null, "TBP 150 W (8 GB) / 160 W (16 GB); no single figure for the shared device name", "https://example.invalid/test", "2026-11-14"));
        var snapshot = await CollectWithDatabaseAsync(database);

        Assert.NotNull(Spec(snapshot, SpecLabels.RatedBoostClock));
        Assert.Equal("3.13 GHz", Spec(snapshot, SpecLabels.RatedBoostClock)!.Value);
        Assert.Null(Spec(snapshot, SpecLabels.RatedMemory));
        Assert.Null(Spec(snapshot, SpecLabels.RatedBoardPower));
    }

    private static SpecValue? Spec(ComponentSnapshot snapshot, string label) =>
        snapshot.Specs.FirstOrDefault(s => string.Equals(s.Label, label, StringComparison.Ordinal));

    private static async Task<ComponentSnapshot> CollectWithDatabaseAsync(IHardwareDatabase? database)
    {
        var collector = new GpuCollector(new UnavailableSensorMonitor(), database);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var collectTask = collector.CollectAsync(timeout.Token);
        var finished = await Task.WhenAny(collectTask, Task.Delay(TimeSpan.FromSeconds(60)));

        Assert.True(finished == collectTask, "GpuCollector did not finish within 60 seconds.");
        return await collectTask;
    }

    /// <summary>A database that matches every model with one fixed spec.</summary>
    private sealed class FixedHardwareDatabase(GpuSpec spec) : IHardwareDatabase
    {
        public string Version => "9.9.9";

        public string Updated => "2026-11-14";

        public int CpuCount => 0;

        public int GpuCount => 1;

        public bool TryGetCpu(string? modelName, out CpuSpec found)
        {
            found = null!;
            return false;
        }

        public bool TryGetGpu(string? modelName, out GpuSpec found)
        {
            found = spec;
            return true;
        }
    }

    /// <summary>A database that matches nothing.</summary>
    private sealed class NeverMatchHardwareDatabase : IHardwareDatabase
    {
        public string Version => "9.9.9";

        public string Updated => "2026-11-14";

        public int CpuCount => 0;

        public int GpuCount => 0;

        public bool TryGetCpu(string? modelName, out CpuSpec spec)
        {
            spec = null!;
            return false;
        }

        public bool TryGetGpu(string? modelName, out GpuSpec spec)
        {
            spec = null!;
            return false;
        }
    }
}
