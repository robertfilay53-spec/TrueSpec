namespace TrueSpec.Tests;

using TrueSpec.Core.HardwareDatabase;

/// <summary>
/// The bundled hardware database (Beyond v2.0, item 5): the lookup must
/// never guess. A model name normalizes to one key, a model the dataset
/// does not list matches nothing, and a bad file or a bad row degrades
/// instead of failing. The bundled dataset itself is pinned: its version
/// and its entry count, so an accidental loss of entries fails CI.
/// </summary>
public sealed class HardwareDatabaseTests
{
    private const string TwoEntryJson = """
        {
          "dataset_version": "9.9.9",
          "updated": "2026-11-14",
          "license": "CC0-1.0",
          "cpus": [
            {"model": "I7-12700H", "vendor": "Intel", "display_name": "Intel Core i7-12700H", "base_ghz": 2.3, "boost_ghz": 4.7, "e_core_boost_ghz": 3.5, "source_url": "https://example.invalid/ark/12700h", "retrieved": "2026-11-14"},
            {"model": "RYZEN 7 5800H", "vendor": "AMD", "display_name": "AMD Ryzen 7 5800H", "base_ghz": 3.2, "boost_ghz": 4.4, "e_core_boost_ghz": null, "source_url": "https://example.invalid/amd/5800h", "retrieved": "2026-11-14"}
          ]
        }
        """;

    private const string TwoGpuEntryJson = """
        {
          "dataset_version": "9.9.9",
          "updated": "2026-11-14",
          "license": "CC0-1.0",
          "gpus": [
            {"model": "RTX 4060 LAPTOP", "vendor": "NVIDIA", "display_name": "GeForce RTX 4060 Laptop GPU", "boost_ghz": 2.37, "memory_gb": 8, "tdp_w": 115, "tdp_note": "TGP range 35-115 W; figure is the maximum", "source_url": "https://example.invalid/nvidia/4060-laptop", "retrieved": "2026-11-14"},
            {"model": "RX 7900 XTX", "vendor": "AMD", "display_name": "AMD Radeon RX 7900 XTX", "boost_ghz": 2.5, "memory_gb": 24, "tdp_w": 355, "tdp_note": null, "source_url": "https://example.invalid/amd/7900-xtx", "retrieved": "2026-11-14"}
          ]
        }
        """;

    [Theory]
    [InlineData("Intel(R) Core(TM) i7-12700H", "I7-12700H")]
    [InlineData("i7-12700H", "I7-12700H")]
    [InlineData("Intel(R) Core(TM) Ultra 7 155H", "ULTRA 7 155H")]
    [InlineData("AMD Ryzen 7 5800H with Radeon Graphics", "RYZEN 7 5800H")]
    [InlineData("AMD Ryzen 9 7945HX with Radeon Graphics", "RYZEN 9 7945HX")]
    [InlineData("AMD Ryzen AI 9 HX 370", "RYZEN AI 9 HX 370")]
    [InlineData("intel core i5-12400", "I5-12400")]
    [InlineData("Intel(R) Core(TM) i5-12400F Processor", "I5-12400F")]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void NormalizeModel_StripsBrandAndTrademarkMarkers(string? input, string expected)
    {
        Assert.Equal(expected, HardwareDatabase.NormalizeModel(input));
    }

    [Fact]
    public void LoadFromJson_LooksUpBothVendorsByWin32Name()
    {
        var database = HardwareDatabase.LoadFromJson(TwoEntryJson);

        Assert.Equal("9.9.9", database.Version);
        Assert.Equal("2026-11-14", database.Updated);
        Assert.Equal(2, database.CpuCount);

        Assert.True(database.TryGetCpu("Intel(R) Core(TM) i7-12700H", out var intel));
        Assert.Equal("Intel", intel.Vendor);
        Assert.Equal(2.3, intel.BaseGhz);
        Assert.Equal(4.7, intel.BoostGhz);
        Assert.Equal(3.5, intel.ECoreBoostGhz);

        Assert.True(database.TryGetCpu("AMD Ryzen 7 5800H with Radeon Graphics", out var amd));
        Assert.Equal("AMD", amd.Vendor);
        Assert.Equal(3.2, amd.BaseGhz);
        Assert.Equal(4.4, amd.BoostGhz);
        Assert.Null(amd.ECoreBoostGhz);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Intel(R) Core(TM) i9-99999X")]
    [InlineData("AMD Ryzen 9 9999X")]
    public void TryGetCpu_UnknownModelMatchesNothing(string? modelName)
    {
        var database = HardwareDatabase.LoadFromJson(TwoEntryJson);

        Assert.False(database.TryGetCpu(modelName, out _));
    }

    [Fact]
    public void LoadFromJson_MalformedJsonDegradesToEmpty()
    {
        var database = HardwareDatabase.LoadFromJson("{ not json");

        Assert.Equal("0.0.0", database.Version);
        Assert.Equal(0, database.CpuCount);
        Assert.Equal(0, database.GpuCount);
        Assert.False(database.TryGetCpu("I7-12700H", out _));
        Assert.False(database.TryGetGpu("RTX 4060 LAPTOP", out _));
    }

    [Fact]
    public void LoadFromJson_MissingVersionOrDateDegradesToEmpty()
    {
        var noVersion = HardwareDatabase.LoadFromJson("""{"updated": "2026-11-14", "cpus": []}""");
        var noDate = HardwareDatabase.LoadFromJson("""{"dataset_version": "1.0.0", "cpus": []}""");

        Assert.Equal(0, noVersion.CpuCount);
        Assert.Equal(0, noDate.CpuCount);
    }

    [Fact]
    public void LoadFromJson_InvalidEntriesAreSkippedNotFatal()
    {
        const string json = """
            {
              "dataset_version": "1.0.0",
              "updated": "2026-11-14",
              "cpus": [
                {"model": "I5-12400", "vendor": "Intel", "display_name": "Intel Core i5-12400", "base_ghz": 2.5, "boost_ghz": 4.4, "e_core_boost_ghz": null, "source_url": "https://example.invalid/a", "retrieved": "2026-11-14"},
                {"model": "I5-12500", "vendor": "Intel", "display_name": "Intel Core i5-12500", "base_ghz": 3.0, "boost_ghz": 2.9, "e_core_boost_ghz": null, "source_url": "https://example.invalid/b", "retrieved": "2026-11-14"},
                {"model": "i7-12700h", "vendor": "Intel", "display_name": "Intel Core i7-12700H", "base_ghz": 2.3, "boost_ghz": 4.7, "e_core_boost_ghz": 3.5, "source_url": "https://example.invalid/c", "retrieved": "2026-11-14"},
                {"model": "I7-12800H", "vendor": "Intel", "display_name": "Intel Core i7-12800H", "base_ghz": 2.4, "boost_ghz": 4.8, "e_core_boost_ghz": 5.0, "source_url": "https://example.invalid/d", "retrieved": "2026-11-14"},
                {"model": "I9-12900K", "vendor": "Intel", "display_name": "Intel Core i9-12900K", "base_ghz": 3.2, "boost_ghz": 5.2, "e_core_boost_ghz": null, "source_url": "", "retrieved": "2026-11-14"}
              ]
            }
            """;

        var database = HardwareDatabase.LoadFromJson(json);

        // Only the first entry survives: boost below base, an
        // unnormalized key, an E-core boost above the P-core boost, and
        // a missing source URL are all data bugs, skipped quietly.
        Assert.Equal(1, database.CpuCount);
        Assert.True(database.TryGetCpu("I5-12400", out _));
        Assert.False(database.TryGetCpu("I5-12500", out _));
        Assert.False(database.TryGetCpu("I7-12700H", out _));
        Assert.False(database.TryGetCpu("I7-12800H", out _));
        Assert.False(database.TryGetCpu("I9-12900K", out _));
    }

    [Fact]
    public void LoadFromJson_FirstEntryWinsOnDuplicateKeys()
    {
        const string json = """
            {
              "dataset_version": "1.0.0",
              "updated": "2026-11-14",
              "cpus": [
                {"model": "I5-12400", "vendor": "Intel", "display_name": "Intel Core i5-12400", "base_ghz": 2.5, "boost_ghz": 4.4, "e_core_boost_ghz": null, "source_url": "https://example.invalid/first", "retrieved": "2026-11-14"},
                {"model": "I5-12400", "vendor": "Intel", "display_name": "Intel Core i5-12400", "base_ghz": 2.5, "boost_ghz": 9.9, "e_core_boost_ghz": null, "source_url": "https://example.invalid/second", "retrieved": "2026-11-14"}
              ]
            }
            """;

        var database = HardwareDatabase.LoadFromJson(json);

        Assert.Equal(1, database.CpuCount);
        Assert.True(database.TryGetCpu("I5-12400", out var spec));
        Assert.Equal(4.4, spec.BoostGhz);
    }

    [Fact]
    public void Default_BundledDatasetLoadsWithVersionAndEntries()
    {
        var database = HardwareDatabase.Default;

        Assert.Equal("1.2.0", database.Version);
        Assert.Equal("2026-11-14", database.Updated);
        Assert.Equal(74, database.CpuCount);
        Assert.Equal(59, database.GpuCount);
    }

    [Fact]
    public void Default_Rtx50SeriesLaptopGpusCarryVerifiedFigures()
    {
        // Dataset v1.2.0: the six RTX 50-series laptop GPUs, verified by
        // hand against NVIDIA's laptop compare page
        // (nvidia.com/en-us/geforce/laptops/compare/, read 2026-11-14).
        // Boost is the upper end of NVIDIA's published Boost Clock range
        // and board power is the maximum of the published TGP range, per
        // the dataset's own definitions. The RTX 5070 Laptop GPU carries
        // no memory figure: NVIDIA lists 12 GB and 8 GB configurations
        // under one device name, so the entry guesses neither.
        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 5090 Laptop GPU", out var rtx5090));
        Assert.Equal("NVIDIA", rtx5090.Vendor);
        Assert.Equal(2.16, rtx5090.BoostGhz);
        Assert.Equal(24, rtx5090.MemoryGb);
        Assert.Equal(150, rtx5090.TdpW);
        Assert.Equal("TGP range 95-150 W; figure is the maximum", rtx5090.TdpNote);
        Assert.StartsWith("https://www.nvidia.com/en-us/geforce/laptops/compare/", rtx5090.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 5080 Laptop GPU", out var rtx5080));
        Assert.Equal(2.287, rtx5080.BoostGhz);
        Assert.Equal(16, rtx5080.MemoryGb);
        Assert.Equal(150, rtx5080.TdpW);

        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 5070 Ti Laptop GPU", out var rtx5070Ti));
        Assert.Equal(2.22, rtx5070Ti.BoostGhz);
        Assert.Equal(12, rtx5070Ti.MemoryGb);
        Assert.Equal(115, rtx5070Ti.TdpW);

        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 5070 Laptop GPU", out var rtx5070));
        Assert.Equal(2.347, rtx5070.BoostGhz);
        Assert.Null(rtx5070.MemoryGb);
        Assert.Equal(100, rtx5070.TdpW);

        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 5060 Laptop GPU", out var rtx5060));
        Assert.Equal(2.497, rtx5060.BoostGhz);
        Assert.Equal(8, rtx5060.MemoryGb);
        Assert.Equal(100, rtx5060.TdpW);

        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 5050 Laptop GPU", out var rtx5050));
        Assert.Equal(2.662, rtx5050.BoostGhz);
        Assert.Equal(8, rtx5050.MemoryGb);
        Assert.Equal(100, rtx5050.TdpW);
    }

    [Fact]
    public void Default_KnownEntryCarriesTheVerifiedBoost()
    {
        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) i7-13700K", out var spec));
        Assert.Equal(5.4, spec.BoostGhz);
        Assert.Equal(4.2, spec.ECoreBoostGhz);
        Assert.StartsWith("https://", spec.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_UnverifiableModelsMatchNothing()
    {
        // i7-12700H is deliberately absent: Intel publishes no per-SKU
        // P-core base frequency for 12th-gen mobile hybrid chips, so the
        // row cannot be completed without inference. i3-8100 has no
        // Turbo Boost, so no boost figure exists at all.
        Assert.False(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) i7-12700H", out _));
        Assert.False(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) i3-8100", out _));
    }

    [Theory]
    [InlineData("NVIDIA GeForce RTX 4060 Laptop GPU", "RTX 4060 LAPTOP")]
    [InlineData("NVIDIA GeForce RTX 5090 Laptop GPU", "RTX 5090 LAPTOP")]
    [InlineData("NVIDIA GeForce RTX 4060", "RTX 4060")]
    [InlineData("NVIDIA GeForce GTX 1060 6GB", "GTX 1060 6GB")]
    [InlineData("NVIDIA GeForce RTX 3050 6GB Laptop GPU", "RTX 3050 6GB LAPTOP")]
    [InlineData("AMD Radeon RX 7800 XT", "RX 7800 XT")]
    [InlineData("AMD Radeon(TM) RX 6600", "RX 6600")]
    [InlineData("Intel(R) Arc(TM) A750 Graphics", "ARC A750")]
    [InlineData("nvidia geforce rtx 4060 laptop gpu", "RTX 4060 LAPTOP")]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void NormalizeGpuModel_StripsBrandAndTrademarkMarkers(string? input, string expected)
    {
        Assert.Equal(expected, HardwareDatabase.NormalizeGpuModel(input));
    }

    [Fact]
    public void NormalizeGpuModel_KeepsLaptopDesktopAndMemoryVariantsApart()
    {
        // The family and the variant are part of the key: a laptop card
        // and its desktop namesake, and two memory variants of one card,
        // must never collide.
        Assert.NotEqual(
            HardwareDatabase.NormalizeGpuModel("NVIDIA GeForce RTX 4060 Laptop GPU"),
            HardwareDatabase.NormalizeGpuModel("NVIDIA GeForce RTX 4060"));
        Assert.NotEqual(
            HardwareDatabase.NormalizeGpuModel("NVIDIA GeForce GTX 1060 6GB"),
            HardwareDatabase.NormalizeGpuModel("NVIDIA GeForce GTX 1060 3GB"));
    }

    [Fact]
    public void LoadFromJson_LooksUpGpusByWin32Name()
    {
        var database = HardwareDatabase.LoadFromJson(TwoGpuEntryJson);

        Assert.Equal("9.9.9", database.Version);
        Assert.Equal(2, database.GpuCount);

        Assert.True(database.TryGetGpu("NVIDIA GeForce RTX 4060 Laptop GPU", out var laptop));
        Assert.Equal("NVIDIA", laptop.Vendor);
        Assert.Equal(2.37, laptop.BoostGhz);
        Assert.Equal(8, laptop.MemoryGb);
        Assert.Equal(115, laptop.TdpW);
        Assert.Equal("TGP range 35-115 W; figure is the maximum", laptop.TdpNote);

        Assert.True(database.TryGetGpu("AMD Radeon RX 7900 XTX", out var desktop));
        Assert.Equal("AMD", desktop.Vendor);
        Assert.Equal(2.5, desktop.BoostGhz);
        Assert.Equal(24, desktop.MemoryGb);
        Assert.Equal(355, desktop.TdpW);
        Assert.Null(desktop.TdpNote);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("NVIDIA GeForce RTX 4060 Ti")]
    [InlineData("AMD Radeon RX 6600M")]
    [InlineData("Intel(R) Arc(TM) A750 Graphics")]
    public void TryGetGpu_UnknownModelMatchesNothing(string? modelName)
    {
        var database = HardwareDatabase.LoadFromJson(TwoGpuEntryJson);

        Assert.False(database.TryGetGpu(modelName, out _));
    }

    [Fact]
    public void LoadFromJson_InvalidGpuEntriesAreSkippedNotFatal()
    {
        const string json = """
            {
              "dataset_version": "1.0.0",
              "updated": "2026-11-14",
              "gpus": [
                {"model": "RTX 4060", "vendor": "NVIDIA", "display_name": "GeForce RTX 4060", "boost_ghz": 2.46, "memory_gb": 8, "tdp_w": 115, "tdp_note": null, "source_url": "https://example.invalid/a", "retrieved": "2026-11-14"},
                {"model": "RTX 4070", "vendor": "NVIDIA", "display_name": "GeForce RTX 4070", "boost_ghz": 0, "memory_gb": 12, "tdp_w": 200, "tdp_note": null, "source_url": "https://example.invalid/b", "retrieved": "2026-11-14"},
                {"model": "RTX 4080", "vendor": "NVIDIA", "display_name": "GeForce RTX 4080", "boost_ghz": 2.51, "memory_gb": 16, "tdp_w": 0, "tdp_note": null, "source_url": "https://example.invalid/c", "retrieved": "2026-11-14"},
                {"model": "RTX 4090", "vendor": "NVIDIA", "display_name": "GeForce RTX 4090", "boost_ghz": 2.52, "memory_gb": 0, "tdp_w": 450, "tdp_note": null, "source_url": "https://example.invalid/d", "retrieved": "2026-11-14"},
                {"model": "rtx 3060", "vendor": "NVIDIA", "display_name": "GeForce RTX 3060", "boost_ghz": 1.78, "memory_gb": 12, "tdp_w": 170, "tdp_note": null, "source_url": "https://example.invalid/e", "retrieved": "2026-11-14"},
                {"model": "RTX 3070", "vendor": "NVIDIA", "display_name": "GeForce RTX 3070", "boost_ghz": 1.73, "memory_gb": 8, "tdp_w": 220, "tdp_note": null, "source_url": "", "retrieved": "2026-11-14"},
                {"model": "GTX 1650", "vendor": "NVIDIA", "display_name": "GeForce GTX 1650", "boost_ghz": null, "memory_gb": 4, "tdp_w": 75, "tdp_note": null, "source_url": "https://example.invalid/f", "retrieved": "2026-11-14"}
              ]
            }
            """;

        var database = HardwareDatabase.LoadFromJson(json);

        // Only the first entry and the last survive: a zero boost, a
        // zero board power, a zero memory, an unnormalized key, and a
        // missing source URL are all data bugs, skipped quietly. A null
        // boost is not a bug: the GTX 1650's device name covers two
        // variants with different boost clocks, so it carries no boost
        // figure and still loads.
        Assert.Equal(2, database.GpuCount);
        Assert.True(database.TryGetGpu("RTX 4060", out _));
        Assert.False(database.TryGetGpu("RTX 4070", out _));
        Assert.False(database.TryGetGpu("RTX 4080", out _));
        Assert.False(database.TryGetGpu("RTX 4090", out _));
        Assert.False(database.TryGetGpu("RTX 3060", out _));
        Assert.False(database.TryGetGpu("RTX 3070", out _));
        Assert.True(database.TryGetGpu("GTX 1650", out var gtx));
        Assert.Null(gtx.BoostGhz);
        Assert.Equal(4, gtx.MemoryGb);
        Assert.Equal(75, gtx.TdpW);
    }

    [Fact]
    public void LoadFromJson_FirstGpuEntryWinsOnDuplicateKeys()
    {
        const string json = """
            {
              "dataset_version": "1.0.0",
              "updated": "2026-11-14",
              "gpus": [
                {"model": "RTX 4060", "vendor": "NVIDIA", "display_name": "GeForce RTX 4060", "boost_ghz": 2.46, "memory_gb": 8, "tdp_w": 115, "tdp_note": null, "source_url": "https://example.invalid/first", "retrieved": "2026-11-14"},
                {"model": "RTX 4060", "vendor": "NVIDIA", "display_name": "GeForce RTX 4060", "boost_ghz": 9.9, "memory_gb": 8, "tdp_w": 115, "tdp_note": null, "source_url": "https://example.invalid/second", "retrieved": "2026-11-14"}
              ]
            }
            """;

        var database = HardwareDatabase.LoadFromJson(json);

        Assert.Equal(1, database.GpuCount);
        Assert.True(database.TryGetGpu("RTX 4060", out var spec));
        Assert.Equal(2.46, spec.BoostGhz);
    }

    [Fact]
    public void Default_KnownGpuEntriesCarryTheVerifiedFigures()
    {
        // Laptop card: the boost is the upper end of NVIDIA's published
        // range, the power is the maximum of the TGP range, and the range
        // is recorded in the note.
        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 4060 Laptop GPU", out var laptop));
        Assert.Equal(2.37, laptop.BoostGhz);
        Assert.Equal(8, laptop.MemoryGb);
        Assert.Equal(115, laptop.TdpW);
        Assert.Equal("TGP range 35-115 W; figure is the maximum", laptop.TdpNote);
        Assert.StartsWith("https://", laptop.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX 7900 XTX", out var desktop));
        Assert.Equal(2.5, desktop.BoostGhz);
        Assert.Equal(24, desktop.MemoryGb);
        Assert.Equal(355, desktop.TdpW);

        // GTX 1650: no single boost figure exists for the shared device
        // name (G5 1665 MHz, G6 1590 MHz), so the entry carries no boost
        // and still shows its memory and power.
        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce GTX 1650", out var gtx));
        Assert.Null(gtx.BoostGhz);
        Assert.Equal(4, gtx.MemoryGb);
        Assert.Equal(75, gtx.TdpW);

        // RTX 3060: no single memory figure exists for the shared device
        // name (12 GB / 8 GB), so the entry carries no memory figure.
        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 3060", out var rtx3060));
        Assert.Equal(1.78, rtx3060.BoostGhz);
        Assert.Null(rtx3060.MemoryGb);
        Assert.Equal(170, rtx3060.TdpW);
    }

    [Fact]
    public void Default_UnverifiableGpuModelsMatchNothing()
    {
        // RTX 4060 Ti and RTX 3080: the board power is split between
        // variants with no mapping on the NVIDIA page. RTX 3050 desktop:
        // boost, memory and power all differ between its 8 GB and 6 GB
        // variants under one device name. AMD laptop GPUs: AMD publishes
        // Game Frequency, not Boost Frequency, for them. Intel Arc: no
        // manufacturer spec page could be located.
        Assert.False(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 4060 Ti", out _));
        Assert.False(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 3080", out _));
        Assert.False(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 3050", out _));
        Assert.False(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX 6600M", out _));
        Assert.False(HardwareDatabase.Default.TryGetGpu("Intel(R) Arc(TM) A750 Graphics", out _));
        Assert.False(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 2060 Laptop GPU", out _));
        Assert.False(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce GTX 1070", out _));
    }
}
