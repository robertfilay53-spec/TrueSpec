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
    [InlineData("AMD Ryzen AI 9 HX 375", "RYZEN AI 9 HX 375")]
    [InlineData("AMD Ryzen AI 9 365", "RYZEN AI 9 365")]
    [InlineData("AMD Ryzen AI 7 350", "RYZEN AI 7 350")]
    [InlineData("AMD Ryzen AI 7 345", "RYZEN AI 7 345")]
    [InlineData("AMD Ryzen AI 5 340", "RYZEN AI 5 340")]
    [InlineData("AMD Ryzen AI Max+ 395", "RYZEN AI MAX+ 395")]
    [InlineData("AMD Ryzen AI Max+ 392", "RYZEN AI MAX+ 392")]
    [InlineData("AMD Ryzen AI Max 390", "RYZEN AI MAX 390")]
    [InlineData("AMD Ryzen AI Max+ 388", "RYZEN AI MAX+ 388")]
    [InlineData("AMD Ryzen AI Max 385", "RYZEN AI MAX 385")]
    [InlineData("AMD Ryzen AI 9 HX 475", "RYZEN AI 9 HX 475")]
    [InlineData("AMD Ryzen AI 9 HX 470", "RYZEN AI 9 HX 470")]
    [InlineData("AMD Ryzen AI 9 465", "RYZEN AI 9 465")]
    [InlineData("AMD Ryzen AI 7 450", "RYZEN AI 7 450")]
    [InlineData("AMD Ryzen AI 7 445", "RYZEN AI 7 445")]
    [InlineData("AMD Ryzen AI 5 435", "RYZEN AI 5 435")]
    [InlineData("AMD Ryzen AI 5 430", "RYZEN AI 5 430")]
    [InlineData("AMD Ryzen 9 270", "RYZEN 9 270")]
    [InlineData("AMD Ryzen 7 260", "RYZEN 7 260")]
    [InlineData("AMD Ryzen 7 250", "RYZEN 7 250")]
    [InlineData("AMD Ryzen 5 240", "RYZEN 5 240")]
    [InlineData("AMD Ryzen 5 230", "RYZEN 5 230")]
    [InlineData("AMD Ryzen 5 220", "RYZEN 5 220")]
    [InlineData("AMD Ryzen 3 210", "RYZEN 3 210")]
    [InlineData("AMD Ryzen 7 253", "RYZEN 7 253")]
    [InlineData("AMD Ryzen 7 249", "RYZEN 7 249")]
    [InlineData("AMD Ryzen 5 225", "RYZEN 5 225")]
    [InlineData("AMD Ryzen 5 224", "RYZEN 5 224")]
    [InlineData("AMD Ryzen 7 217", "RYZEN 7 217")]
    [InlineData("AMD Ryzen 5 216", "RYZEN 5 216")]
    [InlineData("AMD Ryzen 3 205", "RYZEN 3 205")]
    [InlineData("AMD Ryzen 9 180", "RYZEN 9 180")]
    [InlineData("AMD Ryzen 7 165", "RYZEN 7 165")]
    [InlineData("AMD Ryzen 7 155", "RYZEN 7 155")]
    [InlineData("AMD Ryzen 5 125", "RYZEN 5 125")]
    [InlineData("intel core i5-12400", "I5-12400")]
    [InlineData("Intel(R) Core(TM) i5-12400F Processor", "I5-12400F")]
    [InlineData("Intel(R) Core(TM) i5-14400", "I5-14400")]
    [InlineData("Intel(R) Core(TM) i9-14900HX", "I9-14900HX")]
    [InlineData("Intel(R) Core(TM) Ultra 9 285HX", "ULTRA 9 285HX")]
    [InlineData("AMD Ryzen 7 9800X3D", "RYZEN 7 9800X3D")]
    [InlineData("AMD Ryzen 9 7950X3D", "RYZEN 9 7950X3D")]
    [InlineData("AMD Ryzen 7 5800X3D", "RYZEN 7 5800X3D")]
    [InlineData("AMD Ryzen 9 7900", "RYZEN 9 7900")]
    [InlineData("AMD Ryzen 7 7700", "RYZEN 7 7700")]
    [InlineData("AMD Ryzen 5 7600X", "RYZEN 5 7600X")]
    [InlineData("AMD Ryzen 5 9600", "RYZEN 5 9600")]
    [InlineData("AMD Ryzen 7 9700F", "RYZEN 7 9700F")]
    [InlineData("AMD Ryzen 5 9500F", "RYZEN 5 9500F")]
    [InlineData("AMD Ryzen 7 9850X3D", "RYZEN 7 9850X3D")]
    [InlineData("AMD Ryzen 9 9950X3D2 Dual Edition", "RYZEN 9 9950X3D2 DUAL EDITION")]
    [InlineData("AMD Ryzen 5 5500", "RYZEN 5 5500")]
    [InlineData("AMD Ryzen 7 5700", "RYZEN 7 5700")]
    [InlineData("AMD Ryzen 7 5700X", "RYZEN 7 5700X")]
    [InlineData("AMD Ryzen 7 3700U with Radeon Graphics", "RYZEN 7 3700U")]
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

        Assert.Equal("1.28.0", database.Version);
        Assert.Equal("2026-11-15", database.Updated);
        Assert.Equal(138, database.CpuCount);
        Assert.Equal(95, database.GpuCount);
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
    public void Default_NewDesktopGpusCarryVerifiedFigures()
    {
        // Dataset v1.3.0: five desktop GPUs launched after v1.1.0, each
        // figure verified by hand against a manufacturer page, read
        // 2026-11-14. The GeForce RTX 5050 against NVIDIA's 50-series
        // page; the RX 9060 and RX 9060 XT against AMD's driver/spec
        // pages; the Arc B580 and B570 against Intel's own published
        // B-Series Quick Reference Guide (intel.com returned 403 to the
        // reader, so the guide PDF is the cited source). Intel publishes
        // a single Graphics Clock for Arc cards, recorded as the boost
        // figure. The RX 9060 XT carries no memory or board-power
        // figure: AMD lists 8 GB / 150 W and 16 GB / 160 W under one
        // device name, so the entry guesses neither; only its boost
        // clock (3.13 GHz, the same for both) is recorded.
        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 5050", out var rtx5050));
        Assert.Equal("NVIDIA", rtx5050.Vendor);
        Assert.Equal(2.57, rtx5050.BoostGhz);
        Assert.Equal(8, rtx5050.MemoryGb);
        Assert.Equal(130, rtx5050.TdpW);
        Assert.StartsWith("https://www.nvidia.com/en-us/geforce/graphics-cards/50-series/rtx-5050/", rtx5050.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX 9060", out var rx9060));
        Assert.Equal("AMD", rx9060.Vendor);
        Assert.Equal(2.99, rx9060.BoostGhz);
        Assert.Equal(8, rx9060.MemoryGb);
        Assert.Equal(132, rx9060.TdpW);
        Assert.StartsWith("https://www.amd.com/en/support/downloads/drivers.html/graphics/radeon-rx/radeon-rx-9000-series/amd-radeon-rx-9060.html", rx9060.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX 9060 XT", out var rx9060Xt));
        Assert.Equal("AMD", rx9060Xt.Vendor);
        Assert.Equal(3.13, rx9060Xt.BoostGhz);
        Assert.Null(rx9060Xt.MemoryGb);
        Assert.Null(rx9060Xt.TdpW);
        Assert.Equal("TBP 150 W (8 GB) / 160 W (16 GB); no single figure for the shared device name", rx9060Xt.TdpNote);
        Assert.StartsWith("https://www.amd.com/en/support/downloads/drivers.html/graphics/radeon-rx/radeon-rx-9000-series/amd-radeon-rx-9060-xt.html", rx9060Xt.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetGpu("Intel(R) Arc(TM) B580 Graphics", out var b580));
        Assert.Equal("Intel", b580.Vendor);
        Assert.Equal(2.67, b580.BoostGhz);
        Assert.Equal(12, b580.MemoryGb);
        Assert.Equal(190, b580.TdpW);
        Assert.StartsWith("https://cdrdv2-public.intel.com/839907/", b580.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetGpu("Intel(R) Arc(TM) B570 Graphics", out var b570));
        Assert.Equal("Intel", b570.Vendor);
        Assert.Equal(2.5, b570.BoostGhz);
        Assert.Equal(10, b570.MemoryGb);
        Assert.Equal(150, b570.TdpW);
    }

    [Fact]
    public void Default_ArcASeriesDesktopGpusCarryVerifiedFigures()
    {
        // Dataset v1.4.0: the four Intel Arc A-series desktop GPUs, the
        // last Arc family the dataset was missing. The dataset used to
        // say no manufacturer spec page could be located for the
        // A580/A750/A770; that is no longer true: Intel's ARK pages
        // carry the A770 (16 GB) and the A750, Intel's own launch
        // announcement carries the A580, and Intel's own A-series spec
        // reveal carries the A380, with the figures corroborated by the
        // launch reviews. Intel publishes a single Graphics Clock for
        // Arc cards, recorded as the boost figure. The A770 carries no
        // memory figure: Intel lists 16 GB and 8 GB configurations
        // under one device name, so the entry guesses neither; the
        // clock (2.10 GHz) and board power (225 W) are the same for
        // both. The A580 board power is Intel's published 185 W from
        // its launch spec table; a few secondary databases list 175 W
        // with no Intel document behind them.
        Assert.True(HardwareDatabase.Default.TryGetGpu("Intel(R) Arc(TM) A770 Graphics", out var a770));
        Assert.Equal("Intel", a770.Vendor);
        Assert.Equal(2.1, a770.BoostGhz);
        Assert.Null(a770.MemoryGb);
        Assert.Equal(225, a770.TdpW);
        Assert.StartsWith("https://ark.intel.com/content/www/us/en/ark/products/229151/intel-arc-a770-graphics-16gb.html", a770.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetGpu("Intel(R) Arc(TM) A750 Graphics", out var a750));
        Assert.Equal("Intel", a750.Vendor);
        Assert.Equal(2.05, a750.BoostGhz);
        Assert.Equal(8, a750.MemoryGb);
        Assert.Equal(225, a750.TdpW);
        Assert.StartsWith("https://ark.intel.com/content/www/us/en/ark/products/227954/intel-arc-a750-graphics.html", a750.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetGpu("Intel(R) Arc(TM) A580 Graphics", out var a580));
        Assert.Equal("Intel", a580.Vendor);
        Assert.Equal(1.7, a580.BoostGhz);
        Assert.Equal(8, a580.MemoryGb);
        Assert.Equal(185, a580.TdpW);
        Assert.StartsWith("https://game.intel.com/stories/intel-arc-graphics-a580-release-announcement/", a580.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetGpu("Intel(R) Arc(TM) A380 Graphics", out var a380));
        Assert.Equal("Intel", a380.Vendor);
        Assert.Equal(2.0, a380.BoostGhz);
        Assert.Equal(6, a380.MemoryGb);
        Assert.Equal(75, a380.TdpW);
        Assert.StartsWith("https://game.intel.com/stories/intel-arc-graphics-qa-hardware-specs/", a380.SourceUrl, StringComparison.Ordinal);
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
        // The six 13th-gen HX mobile chips are deliberately absent for
        // the same reason, found in v2.1.47: Intel's own current
        // product pages publish Max Turbo and E-core Max Turbo for
        // them but no Performance-core Base Frequency, so their rows
        // cannot be completed without inference (verified 2026-11-14).
        // The two Ultra 5 200HX chips are deliberately absent for a
        // different reason: their figures could not be verified live
        // on Intel's own page in this cycle, so they stay out even
        // though third-party listings agree on them: 245HX at 3.1,
        // 5.1 and 4.5 GHz, 235HX at 2.9, 5.1 and 4.5 GHz.
        Assert.False(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) i7-12700H", out _));
        Assert.False(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) i3-8100", out _));
        Assert.False(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) i9-13980HX", out _));
        Assert.False(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) i9-13900HX", out _));
        Assert.False(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) i7-13700HX", out _));
        Assert.False(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) i7-13650HX", out _));
        Assert.False(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) i5-13500HX", out _));
        Assert.False(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) i5-13450HX", out _));
        Assert.False(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) Ultra 5 245HX", out _));
        Assert.False(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) Ultra 5 235HX", out _));
    }

    [Theory]
    [InlineData("NVIDIA GeForce RTX 4060 Laptop GPU", "RTX 4060 LAPTOP")]
    [InlineData("NVIDIA GeForce RTX 5090 Laptop GPU", "RTX 5090 LAPTOP")]
    [InlineData("NVIDIA GeForce RTX 4060", "RTX 4060")]
    [InlineData("NVIDIA GeForce RTX 4060 Ti", "RTX 4060 TI")]
    [InlineData("NVIDIA GeForce RTX 3080", "RTX 3080")]
    [InlineData("NVIDIA GeForce GTX 1060 6GB", "GTX 1060 6GB")]
    [InlineData("NVIDIA GeForce GTX 1050 Ti", "GTX 1050 TI")]
    [InlineData("NVIDIA GeForce GTX 1650 SUPER", "GTX 1650 SUPER")]
    [InlineData("NVIDIA GeForce GTX 1630", "GTX 1630")]
    [InlineData("NVIDIA GeForce RTX 2080 Ti", "RTX 2080 TI")]
    [InlineData("NVIDIA GeForce RTX 4070 SUPER", "RTX 4070 SUPER")]
    [InlineData("NVIDIA GeForce RTX 2060 SUPER", "RTX 2060 SUPER")]
    [InlineData("NVIDIA GeForce RTX 3050 6GB Laptop GPU", "RTX 3050 6GB LAPTOP")]
    [InlineData("AMD Radeon RX 7800 XT", "RX 7800 XT")]
    [InlineData("AMD Radeon RX 7600 XT", "RX 7600 XT")]
    [InlineData("AMD Radeon(TM) RX 6600", "RX 6600")]
    [InlineData("AMD Radeon RX 5700 XT", "RX 5700 XT")]
    [InlineData("AMD Radeon RX 5500 XT", "RX 5500 XT")]
    [InlineData("AMD Radeon RX Vega 64", "RX VEGA 64")]
    [InlineData("AMD Radeon VII", "VII")]
    [InlineData("AMD Radeon RX 590", "RX 590")]
    [InlineData("AMD Radeon RX 580", "RX 580")]
    [InlineData("AMD Radeon RX 550", "RX 550")]
    [InlineData("AMD Radeon RX 480", "RX 480")]
    [InlineData("AMD Radeon RX 470", "RX 470")]
    [InlineData("AMD Radeon RX 460", "RX 460")]
    [InlineData("Intel(R) Arc(TM) A750 Graphics", "ARC A750")]
    [InlineData("Intel(R) Arc(TM) A770 Graphics", "ARC A770")]
    [InlineData("Intel(R) Arc(TM) A580 Graphics", "ARC A580")]
    [InlineData("Intel(R) Arc(TM) A380 Graphics", "ARC A380")]
    [InlineData("Intel(R) Arc(TM) B580 Graphics", "ARC B580")]
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
                {"model": "GTX 1650", "vendor": "NVIDIA", "display_name": "GeForce GTX 1650", "boost_ghz": null, "memory_gb": 4, "tdp_w": 75, "tdp_note": null, "source_url": "https://example.invalid/f", "retrieved": "2026-11-14"},
                {"model": "RX 9060 XT", "vendor": "AMD", "display_name": "AMD Radeon RX 9060 XT", "boost_ghz": 3.13, "memory_gb": null, "tdp_w": null, "tdp_note": "TBP 150 W (8 GB) / 160 W (16 GB); no single figure for the shared device name", "source_url": "https://example.invalid/g", "retrieved": "2026-11-14"}
              ]
            }
            """;

        var database = HardwareDatabase.LoadFromJson(json);

        // Only the first entry, the GTX 1650 and the RX 9060 XT survive:
        // a zero boost, a zero board power, a zero memory, an
        // unnormalized key, and a missing source URL are all data bugs,
        // skipped quietly. A null boost is not a bug: the GTX 1650's
        // device name covers two variants with different boost clocks,
        // so it carries no boost figure and still loads. A null memory
        // and board power are not bugs either: the RX 9060 XT's device
        // name covers two variants with different memory and power, so
        // it carries neither figure and still loads.
        Assert.Equal(3, database.GpuCount);
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
        Assert.True(database.TryGetGpu("RX 9060 XT", out var rx9060Xt));
        Assert.Equal(3.13, rx9060Xt.BoostGhz);
        Assert.Null(rx9060Xt.MemoryGb);
        Assert.Null(rx9060Xt.TdpW);
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
    public void Default_Rtx4060TiAndRtx3080CarryBoostOnlyFigures()
    {
        // Dataset v1.5.0: the two desktop GPUs the deliberately-absent
        // list excluded for a split board power. That exclusion was
        // written when board power was a required figure; v2.1.35 made
        // it optional end to end for the RX 9060 XT, which unblocks
        // boost-only entries, and nobody re-examined the absent list
        // after that change. Both figures were verified by hand against
        // NVIDIA's own product pages, read 2026-11-14. RTX 4060 Ti:
        // Boost Clock 2.54 GHz, the single figure on NVIDIA's
        // 4060/4060 Ti page; memory "16 GB or 8 GB" and Total Graphics
        // Power "165 or 160" are both split with no mapping on the
        // page, so the entry carries neither (the 160 W / 8 GB and
        // 165 W / 16 GB mapping is corroborated by the board pages).
        // RTX 3080: Boost Clock 1.71 GHz on NVIDIA's 3080 page (the
        // 10 GB Founders Edition); the 12 GB variant shares the same
        // 1.71 GHz boost with 350 W board power against the 10 GB
        // card's 320 W, so the entry carries the boost only.
        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 4060 Ti", out var rtx4060Ti));
        Assert.Equal("NVIDIA", rtx4060Ti.Vendor);
        Assert.Equal(2.54, rtx4060Ti.BoostGhz);
        Assert.Null(rtx4060Ti.MemoryGb);
        Assert.Null(rtx4060Ti.TdpW);
        Assert.Equal("TGP 160 W (8 GB) / 165 W (16 GB); no single figure for the shared device name", rtx4060Ti.TdpNote);
        Assert.StartsWith("https://www.nvidia.com/en-us/geforce/graphics-cards/40-series/rtx-4060-4060ti/", rtx4060Ti.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 3080", out var rtx3080));
        Assert.Equal("NVIDIA", rtx3080.Vendor);
        Assert.Equal(1.71, rtx3080.BoostGhz);
        Assert.Null(rtx3080.MemoryGb);
        Assert.Null(rtx3080.TdpW);
        Assert.Equal("TGP 320 W (10 GB) / 350 W (12 GB); no single figure for the shared device name", rtx3080.TdpNote);
        Assert.StartsWith("https://www.nvidia.com/en-us/geforce/graphics-cards/30-series/rtx-3080/", rtx3080.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_Gtx1650SuperAndGtx1630CarryCompleteFigures()
    {
        // Dataset v1.6.0: the GTX 16-series desktop family is complete.
        // The 1650 SUPER and the 1630 were the two cards in the family
        // the dataset never listed and the deliberately-absent list never
        // mentioned: every cycle checked the absent list, but neither card
        // was on it, so the gap went unnoticed until this cycle listed the
        // whole family against the dataset. Both figures were verified by
        // hand against NVIDIA's own GeForce compare page (the same page
        // the shipped 1660/1660 SUPER/1660 Ti/1650 entries cite), read
        // 2026-11-14. The page's GTX 16 Series table lists, in column
        // order 1660 Ti / 1660 SUPER / 1660 / 1650 SUPER / 1650 (G5) /
        // 1650 (G6) / 1630: Boost Clock 1725 MHz, 4 GB GDDR6 and
        // Graphics Card Power 100 W for the 1650 SUPER; Boost Clock
        // 1785 MHz, 4 GB GDDR6 and 75 W for the 1630. Both are single
        // variants with one figure each, so both entries are complete —
        // unlike the GTX 1650, whose G5 (1665 MHz) and G6 (1590 MHz)
        // variants differ and which therefore still carries no boost.
        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce GTX 1650 SUPER", out var gtx1650Super));
        Assert.Equal("NVIDIA", gtx1650Super.Vendor);
        Assert.Equal(1.725, gtx1650Super.BoostGhz);
        Assert.Equal(4, gtx1650Super.MemoryGb);
        Assert.Equal(100, gtx1650Super.TdpW);
        Assert.StartsWith("https://www.nvidia.com/en-us/geforce/graphics-cards/compare/?section=compare-20", gtx1650Super.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce GTX 1630", out var gtx1630));
        Assert.Equal("NVIDIA", gtx1630.Vendor);
        Assert.Equal(1.785, gtx1630.BoostGhz);
        Assert.Equal(4, gtx1630.MemoryGb);
        Assert.Equal(75, gtx1630.TdpW);
        Assert.StartsWith("https://www.nvidia.com/en-us/geforce/graphics-cards/compare/?section=compare-20", gtx1630.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_Rtx4070SuperAndRx7600XtCarryCompleteFigures()
    {
        // Dataset v1.23.0: the two desktop GPUs the dataset never listed
        // even though both families were otherwise complete. The RTX 4070
        // SUPER is the missing card of the RTX 40-series desktop family:
        // the dataset had the 4070, the 4070 Ti and the 4070 Ti SUPER but
        // not the SUPER between the 4070 and the 4070 Ti. The RX 7600 XT
        // is the missing card of the RX 7000 desktop family: the dataset
        // had the 7600 but not the XT. Both figures were verified by hand
        // against the vendor's own pages, read 2026-11-15. RTX 4070 SUPER:
        // Boost Clock 2.48 GHz, 12 GB GDDR6X and Total Graphics Power
        // 220 W on NVIDIA's 4070 family page, which carries the spec
        // table for the whole family. RX 7600 XT: Boost Frequency up to
        // 2755 MHz, 16 GB and Typical Board Power 190 W on AMD's own
        // driver page for the card, with the 190 W confirmed by AMD's
        // launch press release. Both are single variants with one figure
        // each, so both entries are complete.
        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 4070 SUPER", out var rtx4070Super));
        Assert.Equal("NVIDIA", rtx4070Super.Vendor);
        Assert.Equal(2.48, rtx4070Super.BoostGhz);
        Assert.Equal(12, rtx4070Super.MemoryGb);
        Assert.Equal(220, rtx4070Super.TdpW);
        Assert.StartsWith("https://www.nvidia.com/en-us/geforce/graphics-cards/40-series/rtx-4070-family/", rtx4070Super.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX 7600 XT", out var rx7600Xt));
        Assert.Equal("AMD", rx7600Xt.Vendor);
        Assert.Equal(2.755, rx7600Xt.BoostGhz);
        Assert.Equal(16, rx7600Xt.MemoryGb);
        Assert.Equal(190, rx7600Xt.TdpW);
        Assert.StartsWith("https://www.amd.com/en/support/downloads/drivers.html/graphics/radeon-rx/radeon-rx-7000-series/amd-radeon-rx-7600-xt.html", rx7600Xt.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_Rtx20SeriesDesktopGpusCarryCompleteFigures()
    {
        // Dataset v1.7.0: the RTX 20-series desktop family, the whole
        // family the dataset never listed and the deliberately-absent
        // list never mentioned: every cycle checked the absent list,
        // but no RTX 20-series desktop card was on it, so the gap went
        // unnoticed until this cycle listed the whole RTX family
        // against the dataset. All figures were verified by hand
        // against NVIDIA's own GeForce compare page (the same page
        // the shipped 1660/1660 SUPER/1660 Ti/1650/1650 SUPER/1630
        // entries cite), read 2026-11-14. The page's RTX 20 Series
        // table lists, in column order 2080 Ti / 2080 SUPER / 2080 /
        // 2070 SUPER / 2070 / 2060 SUPER / 2060: Boost Clock 1.64 /
        // 1.82 / 1.80 / 1.77 / 1.71 / 1.65 GHz, memory 11 / 8 / 8 /
        // 8 / 8 / 8 GB GDDR6, and Graphics Card Power 260 / 250 / 225
        // / 215 / 185 / 175 W. All six are single variants with one
        // figure each, so all six entries are complete. The seventh
        // card, the RTX 2060, stays absent: its boost (1.65/1.68 GHz),
        // memory (12/6 GB) and power (185/160 W) are all split between
        // its 12 GB and 6 GB variants under one device name, with no
        // mapping on the page.
        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 2080 Ti", out var rtx2080Ti));
        Assert.Equal("NVIDIA", rtx2080Ti.Vendor);
        Assert.Equal(1.64, rtx2080Ti.BoostGhz);
        Assert.Equal(11, rtx2080Ti.MemoryGb);
        Assert.Equal(260, rtx2080Ti.TdpW);
        Assert.StartsWith("https://www.nvidia.com/en-us/geforce/graphics-cards/compare/?section=compare-20", rtx2080Ti.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 2080 SUPER", out var rtx2080Super));
        Assert.Equal(1.82, rtx2080Super.BoostGhz);
        Assert.Equal(8, rtx2080Super.MemoryGb);
        Assert.Equal(250, rtx2080Super.TdpW);

        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 2080", out var rtx2080));
        Assert.Equal(1.8, rtx2080.BoostGhz);
        Assert.Equal(8, rtx2080.MemoryGb);
        Assert.Equal(225, rtx2080.TdpW);

        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 2070 SUPER", out var rtx2070Super));
        Assert.Equal(1.77, rtx2070Super.BoostGhz);
        Assert.Equal(8, rtx2070Super.MemoryGb);
        Assert.Equal(215, rtx2070Super.TdpW);

        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 2070", out var rtx2070));
        Assert.Equal(1.71, rtx2070.BoostGhz);
        Assert.Equal(8, rtx2070.MemoryGb);
        Assert.Equal(185, rtx2070.TdpW);

        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 2060 SUPER", out var rtx2060Super));
        Assert.Equal(1.65, rtx2060Super.BoostGhz);
        Assert.Equal(8, rtx2060Super.MemoryGb);
        Assert.Equal(175, rtx2060Super.TdpW);
    }

    [Fact]
    public void Default_I514400I514400FAndRyzen73700UCarryVerifiedFigures()
    {
        // Dataset v1.8.0: the three CPUs the v1.0.0 deliberately-absent
        // list named without a blocking reason. The i5-14400 figures
        // were verified by hand against Intel's own product page (ARK
        // ID 236788, read 2026-11-14): the page title itself says up
        // to 4.70 GHz, and the ARK-sourced listings agree on base
        // 2.5 GHz and E-core boost 3.5 GHz. The i5-14400F carries the
        // same figures (ARK ID 236777); Intel's ARK page returned 403
        // to the reader, so its figures rest on the ARK-sourced
        // listings, which agree exactly with the 14400. The Ryzen 7
        // 3700U figures were verified by hand against AMD's own
        // driver/spec page for the chip, read 2026-11-14: Max. Boost
        // Clock up to 4 GHz, Base Clock 2.3 GHz. The i9-11980HK stays
        // absent: Intel's own current product page lists up to 4.90
        // GHz while ARK-era sources list 5.00 GHz Max Turbo, so no
        // single verifiable figure exists.
        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) i5-14400", out var i514400));
        Assert.Equal("Intel", i514400.Vendor);
        Assert.Equal(2.5, i514400.BaseGhz);
        Assert.Equal(4.7, i514400.BoostGhz);
        Assert.Equal(3.5, i514400.ECoreBoostGhz);
        Assert.StartsWith("https://www.intel.com/content/www/us/en/products/sku/236788/intel-core-i5-processor-14400-20m-cache-up-to-4-70-ghz/specifications.html", i514400.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) i5-14400F", out var i514400F));
        Assert.Equal("Intel", i514400F.Vendor);
        Assert.Equal(2.5, i514400F.BaseGhz);
        Assert.Equal(4.7, i514400F.BoostGhz);
        Assert.Equal(3.5, i514400F.ECoreBoostGhz);
        Assert.StartsWith("https://ark.intel.com/content/www/us/en/ark/products/236777/", i514400F.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 7 3700U with Radeon Graphics", out var r73700U));
        Assert.Equal("AMD", r73700U.Vendor);
        Assert.Equal(2.3, r73700U.BaseGhz);
        Assert.Equal(4.0, r73700U.BoostGhz);
        Assert.Null(r73700U.ECoreBoostGhz);
        Assert.StartsWith("https://www.amd.com/en/support/downloads/drivers.html/processors/ryzen/ryzen-3000-series/amd-ryzen-7-3700u.html", r73700U.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_Rx5000SeriesDesktopGpusCarryVerifiedFigures()
    {
        // Dataset v1.9.0: the AMD Radeon RX 5000-series desktop family,
        // the whole family the dataset never listed and the
        // deliberately-absent list never mentioned: every cycle checked
        // the absent list, but no RX 5000-series desktop card was on
        // it, so the gap went unnoticed until this cycle listed the
        // whole AMD family against the dataset. All figures were
        // verified by hand against AMD's own driver/spec pages (the
        // same pages the shipped RX 6000/7000/9000 entries cite), read
        // 2026-11-14: the RX 5700 XT at Boost Frequency up to 1905
        // MHz, 8 GB GDDR6 and Typical Board Power 225 W; the RX 5700
        // at 1725 MHz, 8 GB and 180 W; the RX 5600 XT at 1560 MHz,
        // 6 GB and 150 W; the RX 5500 XT at 1845 MHz and 130 W; the
        // RX 5500 at 1845 MHz, 4 GB and 150 W. The RX 5500 XT
        // carries no memory figure: AMD lists 8 GB and 4 GB
        // configurations under one device name, so the entry guesses
        // neither, the same precedent as the RTX 5070 Laptop GPU and
        // the RX 9060 XT. The RX 5500 is the OEM card of the family,
        // but AMD's own page lists it as a desktop board with single
        // figures, so it is recorded like the rest. The 5600 XT page
        // lists two memory speeds (up to 12 Gbps / up to 14 Gbps, the
        // vBIOS update), but the memory size is a single 6 GB, so the
        // entry is complete.
        Assert.True(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX 5700 XT", out var rx5700Xt));
        Assert.Equal("AMD", rx5700Xt.Vendor);
        Assert.Equal(1.905, rx5700Xt.BoostGhz);
        Assert.Equal(8, rx5700Xt.MemoryGb);
        Assert.Equal(225, rx5700Xt.TdpW);
        Assert.StartsWith("https://www.amd.com/en/support/downloads/drivers.html/graphics/radeon-rx/radeon-rx-5000-series/amd-radeon-rx-5700-xt.html", rx5700Xt.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX 5700", out var rx5700));
        Assert.Equal(1.725, rx5700.BoostGhz);
        Assert.Equal(8, rx5700.MemoryGb);
        Assert.Equal(180, rx5700.TdpW);

        Assert.True(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX 5600 XT", out var rx5600Xt));
        Assert.Equal(1.56, rx5600Xt.BoostGhz);
        Assert.Equal(6, rx5600Xt.MemoryGb);
        Assert.Equal(150, rx5600Xt.TdpW);

        Assert.True(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX 5500 XT", out var rx5500Xt));
        Assert.Equal(1.845, rx5500Xt.BoostGhz);
        Assert.Null(rx5500Xt.MemoryGb);
        Assert.Equal(130, rx5500Xt.TdpW);
        Assert.StartsWith("https://www.amd.com/en/support/downloads/drivers.html/graphics/radeon-rx/radeon-rx-5000-series/amd-radeon-rx-5500-xt.html", rx5500Xt.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX 5500", out var rx5500));
        Assert.Equal(1.845, rx5500.BoostGhz);
        Assert.Equal(4, rx5500.MemoryGb);
        Assert.Equal(150, rx5500.TdpW);
    }

    [Fact]
    public void Default_RxVegaSeriesDesktopGpusCarryVerifiedFigures()
    {
        // Dataset v1.10.0: the AMD Radeon RX Vega series desktop
        // family, the whole family the dataset never listed and the
        // deliberately-absent list never mentioned: every cycle checked
        // the absent list, but no Vega desktop card was on it, so the
        // gap went unnoticed until this cycle listed the whole AMD
        // family against the dataset. All figures were verified by
        // hand against AMD's own driver/spec pages (the same pages
        // the shipped RX 5000/6000/7000/9000 entries cite), read
        // 2026-11-14: the RX Vega 64 at Boost Frequency up to 1546
        // MHz, 8 GB HBM2 and Typical Board Power 295 W; the RX Vega
        // 56 at 1471 MHz, 8 GB and 210 W; the Radeon VII at 1750
        // MHz, 16 GB HBM2 and 300 W. All three are single variants
        // with one figure each, so all three entries are complete.
        // The RX Vega 64 Liquid Cooled Edition (1677 MHz, 345 W) is
        // a separate device name, not a variant of the air-cooled
        // card, so it is not folded into this entry. AMD lists the
        // Radeon VII under the Radeon RX Vega Series, so it ships
        // with this family even though its name carries no RX.
        Assert.True(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX Vega 64", out var rxVega64));
        Assert.Equal("AMD", rxVega64.Vendor);
        Assert.Equal(1.546, rxVega64.BoostGhz);
        Assert.Equal(8, rxVega64.MemoryGb);
        Assert.Equal(295, rxVega64.TdpW);
        Assert.StartsWith("https://www.amd.com/en/support/downloads/drivers.html/graphics/radeon-rx/radeon-rx-vega-series/radeon-rx-vega-64.html", rxVega64.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX Vega 56", out var rxVega56));
        Assert.Equal(1.471, rxVega56.BoostGhz);
        Assert.Equal(8, rxVega56.MemoryGb);
        Assert.Equal(210, rxVega56.TdpW);
        Assert.StartsWith("https://www.amd.com/en/support/downloads/drivers.html/graphics/radeon-rx/radeon-rx-vega-series/radeon-rx-vega-56.html", rxVega56.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetGpu("AMD Radeon VII", out var radeonVii));
        Assert.Equal(1.75, radeonVii.BoostGhz);
        Assert.Equal(16, radeonVii.MemoryGb);
        Assert.Equal(300, radeonVii.TdpW);
        Assert.StartsWith("https://www.amd.com/en/support/downloads/drivers.html/graphics/radeon-rx/radeon-rx-vega-series/amd-radeon-vii.html", radeonVii.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_Rx500SeriesDesktopGpusCarryVerifiedFigures()
    {
        // Dataset v1.11.0: the AMD Radeon RX 500-series desktop
        // cards that carry single verifiable figures, the part of
        // the family the dataset never listed and the
        // deliberately-absent list never mentioned: every cycle checked
        // the absent list, but no RX 500-series desktop card was on
        // it, so the gap went unnoticed until this cycle listed the
        // whole AMD family against the dataset. The previous cycle
        // passed the family over on a mistaken reason: it claimed the
        // RX 580 4 GB variant carries a different boost (1266 vs
        // 1340 MHz), but AMD's own page lists a single Boost
        // Frequency up to 1340 MHz and a single Typical Board Power
        // 185 W for the RX 580; 1266 MHz is the RX 480's boost, not
        // the RX 580 4 GB card's. The real splits are elsewhere, and
        // they stay absent: the RX 570 page lists two boost figures
        // (Up to 1206 MHz for the OEM card, Up to 1244 MHz for
        // retail) under one device name, and the RX 560 page lists
        // board power as a range (60-80 W), so neither has a single
        // verifiable figure. All figures below were verified by hand
        // against AMD's own driver/spec pages (the same pages the
        // shipped RX 5000/6000/7000/9000 and Vega entries cite),
        // read 2026-11-14: the RX 590 at Boost Frequency up to 1545
        // MHz and 8 GB; the RX 580 at 1340 MHz and 185 W; the RX 550
        // at 1183 MHz and 50 W. The RX 590 carries no board-power
        // figure: AMD publishes no Typical Board Power value for it
        // on its page, the same null treatment as the RX 9060 XT.
        // The RX 580 and RX 550 carry no memory figure: AMD lists
        // 8 GB and 4 GB (RX 580) and 4 GB and 2 GB (RX 550)
        // configurations under one device name, so the entries guess
        // neither, the same precedent as the RX 5500 XT.
        Assert.True(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX 590", out var rx590));
        Assert.Equal("AMD", rx590.Vendor);
        Assert.Equal(1.545, rx590.BoostGhz);
        Assert.Equal(8, rx590.MemoryGb);
        Assert.Null(rx590.TdpW);
        Assert.StartsWith("https://www.amd.com/en/support/downloads/drivers.html/graphics/radeon-600-500-400/radeon-rx-500-series/radeon-rx-590.html", rx590.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX 580", out var rx580));
        Assert.Equal(1.34, rx580.BoostGhz);
        Assert.Null(rx580.MemoryGb);
        Assert.Equal(185, rx580.TdpW);
        Assert.StartsWith("https://www.amd.com/en/support/downloads/drivers.html/graphics/radeon-600-500-400/radeon-rx-500-series/radeon-rx-580.html", rx580.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX 550", out var rx550));
        Assert.Equal(1.183, rx550.BoostGhz);
        Assert.Null(rx550.MemoryGb);
        Assert.Equal(50, rx550.TdpW);
        Assert.StartsWith("https://www.amd.com/en/support/downloads/drivers.html/graphics/radeon-600-500-400/radeon-rx-500-series/radeon-rx-550.html", rx550.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_Rx400SeriesDesktopGpusCarryVerifiedFigures()
    {
        // Dataset v1.12.0: the AMD Radeon RX 400-series desktop
        // family, the whole family the dataset never listed and the
        // deliberately-absent list never mentioned: every cycle checked
        // the absent list, but no RX 400-series desktop card was on
        // it, so the gap went unnoticed until this cycle listed the
        // whole AMD family against the dataset. All figures below
        // were verified by hand against AMD's own driver/spec pages
        // (the same pages the shipped RX 5000/6000/7000/9000, Vega
        // and RX 500 entries cite), read 2026-11-14: the RX 480 at
        // Boost Frequency up to 1266 MHz and Typical Board Power
        // 150 W; the RX 470 at 1206 MHz, 4 GB and 120 W; the RX 460
        // at 1200 MHz and 70 W. The 1266 MHz figure also settles the
        // v2.1.42 confusion for good: it is the RX 480's boost, read
        // from AMD's own RX 480 page, not the RX 580 4 GB card's.
        // The RX 480 and RX 460 carry no memory figure: AMD lists
        // 8 GB and 4 GB (RX 480) and 4 GB and 2 GB (RX 460)
        // configurations under one device name, so the entries guess
        // neither, the same precedent as the RX 580 and RX 550.
        // The RX 470 carries 4 GB: AMD's reference card is 4 GB and
        // the page lists a single 4 GB figure.
        Assert.True(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX 480", out var rx480));
        Assert.Equal("AMD", rx480.Vendor);
        Assert.Equal(1.266, rx480.BoostGhz);
        Assert.Null(rx480.MemoryGb);
        Assert.Equal(150, rx480.TdpW);
        Assert.StartsWith("https://www.amd.com/en/support/downloads/drivers.html/graphics/radeon-600-500-400/radeon-rx-400-series/radeon-rx-480.html", rx480.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX 470", out var rx470));
        Assert.Equal(1.206, rx470.BoostGhz);
        Assert.Equal(4, rx470.MemoryGb);
        Assert.Equal(120, rx470.TdpW);
        Assert.StartsWith("https://www.amd.com/en/support/downloads/drivers.html/graphics/radeon-600-500-400/radeon-rx-400-series/radeon-rx-470.html", rx470.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX 460", out var rx460));
        Assert.Equal(1.2, rx460.BoostGhz);
        Assert.Null(rx460.MemoryGb);
        Assert.Equal(70, rx460.TdpW);
        Assert.StartsWith("https://www.amd.com/en/support/downloads/drivers.html/graphics/radeon-600-500-400/radeon-rx-400-series/radeon-rx-460.html", rx460.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_Gtx1050TiDesktopGpuCarriesVerifiedFigures()
    {
        // Dataset v1.13.0: the NVIDIA GeForce GTX 1050 Ti desktop
        // GPU, the last GTX 10-series desktop card the dataset never
        // listed that a live NVIDIA page still verifies, and the
        // deliberately-absent list never mentioned: every cycle checked
        // the absent list, but no GTX 1050 Ti was on it, so the gap
        // went unnoticed until this cycle listed the whole NVIDIA
        // family against the dataset. Both figures below were verified
        // by hand against NVIDIA's own specs table on the GeForce GTX
        // 1050 launch page (nvidia.com/content/apac/event/ph/
        // overwatch-campaign/geforce-gtx-1050.html), read 2026-11-14
        // from a fresh crawl: Boost Clock 1392 MHz and Frame Buffer
        // 4 GB GDDR5. The entry carries no board-power figure: that
        // live NVIDIA table lists boost clock and memory but no
        // board-power value, the same null treatment as the RX 590.
        // The 75 W figure the card is widely listed at was not taken
        // from any third-party table, and NVIDIA's 10-series family
        // page that lists it could not be confirmed live: its text
        // copy is a 2024 crawl, and a 2026-11-14 check recorded the
        // old 10-series product page as redirecting, so the power
        // stays null rather than resting on a stale copy. The plain
        // GTX 1050 stays absent: its device name covers the 2 GB card
        // (1455 MHz, verified live on the same NVIDIA table) and a
        // later 3 GB variant (1518 MHz, 3 GB), so no single boost or
        // memory figure exists under the bare name, and the live table
        // lists no board-power figure for it either.
        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce GTX 1050 Ti", out var gtx1050Ti));
        Assert.Equal("NVIDIA", gtx1050Ti.Vendor);
        Assert.Equal(1.392, gtx1050Ti.BoostGhz);
        Assert.Equal(4, gtx1050Ti.MemoryGb);
        Assert.Null(gtx1050Ti.TdpW);
        Assert.StartsWith("https://www.nvidia.com/content/apac/event/ph/overwatch-campaign/geforce-gtx-1050.html", gtx1050Ti.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_UnverifiableGpuModelsMatchNothing()
    {
        // RTX 3050 desktop: boost, memory and power all differ between
        // its 8 GB and 6 GB variants under one device name. RTX 2060
        // desktop: the same shape, boost 1.65/1.68 GHz, memory 12/6 GB
        // and power 185/160 W all differ between its 12 GB and 6 GB
        // variants under one device name, with no mapping on NVIDIA's
        // compare page. GTX 1060
        // 3GB: no power figure on nvidia.com. GTX 1070: the old
        // nvidia.com 10-series product page now redirects to the RTX 50
        // Series page, so no boost or power figure exists on nvidia.com
        // (verified 2026-11-14); the retailer tables that list its
        // figures do not satisfy the manufacturer-page source policy.
        // A 2026-11-14 check of the live compare page found no GTX 10
        // per-card table on it either: the page's per-card tables end
        // at the GTX 16 series, so the GTX 10 Series column in the
        // header table carries no boost or power figure.
        // GTX 1050 desktop: the device name covers the 2 GB card
        // (1455 MHz, verified live on NVIDIA's own GTX 1050 launch
        // specs table) and a later 3 GB variant (1518 MHz, 3 GB), so
        // no single boost or memory figure exists under the bare name,
        // and the live table lists no board-power figure for it either.
        // AMD laptop GPUs: AMD publishes Game Frequency, not Boost
        // Frequency, for them. RX 570 desktop: AMD's page lists two
        // boost figures (Up to 1206 MHz for the OEM card, Up to 1244
        // MHz for retail) under one device name, so no single
        // verifiable figure exists. RX 560 desktop: AMD lists board
        // power as a range (60-80 W), so no single figure exists.
        Assert.False(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 3050", out _));
        Assert.False(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 2060", out _));
        Assert.False(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce GTX 1050", out _));
        Assert.False(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX 570", out _));
        Assert.False(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX 560", out _));
        Assert.False(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX 6600M", out _));
        Assert.False(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 2060 Laptop GPU", out _));
        Assert.False(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce GTX 1070", out _));
    }

    [Fact]
    public void Default_14thGenHxMobileCpusCarryVerifiedFigures()
    {
        // Dataset v1.14.0: the five Intel Core 14th-gen HX mobile CPUs,
        // the whole family the dataset never listed and the
        // deliberately-absent list never mentioned: every cycle checked
        // the absent list, but no 14th-gen HX chip was on it, so the gap
        // went unnoticed until this cycle listed the whole Intel mobile
        // family against the dataset. The 12th/13th-gen mobile H/P
        // chips stay absent for a reason that does not apply here:
        // Intel publishes no per-SKU P-core base frequency for them,
        // while its own compare pages publish one for the 14th-gen
        // HX chips. (The 13th-gen HX chips do not publish one either,
        // which v2.1.47 found and recorded in the deliberately-absent
        // assertions.)
        // All figures below were verified by hand against Intel's own
        // product specification comparison pages, read 2026-11-14 from
        // fresh crawls: the i9-14900HX at Performance-core Base
        // Frequency 2.2 GHz, Performance-core Max Turbo Frequency
        // 5.8 GHz and Efficient-core Max Turbo Frequency 4.1 GHz; the
        // i7-14700HX at 2.1, 5.5 and 3.9 GHz; the i7-14650HX at 2.2,
        // 5.2 and 3.7 GHz; the i5-14500HX at 2.6, 4.9 and 3.5 GHz; the
        // i5-14450HX at 2.4, 4.8 and 3.5 GHz. The headline Max Turbo
        // Frequency equals the P-core Max Turbo for all five, so the
        // boost figure is the same either way. No code change was
        // needed: the CPU normalizer already maps "Intel(R) Core(TM)
        // i9-14900HX" to "I9-14900HX", and the Settings card shows the
        // CPU count from the dataset, so it reads 82 with no code
        // change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) i9-14900HX", out var i914900Hx));
        Assert.Equal("Intel", i914900Hx.Vendor);
        Assert.Equal(2.2, i914900Hx.BaseGhz);
        Assert.Equal(5.8, i914900Hx.BoostGhz);
        Assert.Equal(4.1, i914900Hx.ECoreBoostGhz);
        Assert.StartsWith("https://www.intel.com/content/www/us/en/products/compare.html?productIds=235995", i914900Hx.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) i7-14700HX", out var i714700Hx));
        Assert.Equal(2.1, i714700Hx.BaseGhz);
        Assert.Equal(5.5, i714700Hx.BoostGhz);
        Assert.Equal(3.9, i714700Hx.ECoreBoostGhz);
        Assert.StartsWith("https://www.intel.com/content/www/us/en/products/compare.html?productIds=235996,235997,235998,235999", i714700Hx.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) i7-14650HX", out var i714650Hx));
        Assert.Equal(2.2, i714650Hx.BaseGhz);
        Assert.Equal(5.2, i714650Hx.BoostGhz);
        Assert.Equal(3.7, i714650Hx.ECoreBoostGhz);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) i5-14500HX", out var i514500Hx));
        Assert.Equal(2.6, i514500Hx.BaseGhz);
        Assert.Equal(4.9, i514500Hx.BoostGhz);
        Assert.Equal(3.5, i514500Hx.ECoreBoostGhz);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) i5-14450HX", out var i514450Hx));
        Assert.Equal(2.4, i514450Hx.BaseGhz);
        Assert.Equal(4.8, i514450Hx.BoostGhz);
        Assert.Equal(3.5, i514450Hx.ECoreBoostGhz);
    }

    [Fact]
    public void Default_CoreUltra200HxMobileCpusCarryVerifiedFigures()
    {
        // Dataset v1.15.0: the four Intel Core Ultra 200HX mobile CPUs
        // (Arrow Lake HX) the dataset never listed: Ultra 9 285HX and
        // 275HX, Ultra 7 265HX and 255HX. The gap went unnoticed the
        // same way the 14th-gen HX gap did: every cycle re-examined
        // the deliberately-absent list, but no 200HX chip was on it,
        // so the family was never listed against the dataset until
        // this cycle did. All figures below were verified by hand
        // against Intel's own product specification comparison page,
        // read 2026-11-14 from a fresh crawl: the Ultra 9 285HX at
        // Performance-core Base Frequency 2.8 GHz, Performance-core
        // Max Turbo Frequency 5.5 GHz and Efficient-core Max Turbo
        // Frequency 4.6 GHz; the Ultra 9 275HX at 2.7, 5.4 and
        // 4.6 GHz; the Ultra 7 265HX at 2.6, 5.3 and 4.6 GHz; the
        // Ultra 7 255HX at 2.4, 5.2 and 4.5 GHz. The headline Max
        // Turbo Frequency equals the P-core Max Turbo for all four,
        // so the boost figure is the same either way. The Ultra 5
        // 245HX and 235HX stay absent: their figures could not be
        // verified live on Intel's own page in this cycle. No code
        // change was needed: the CPU normalizer already maps
        // "Intel(R) Core(TM) Ultra 9 285HX" to "ULTRA 9 285HX", and
        // the Settings card shows the CPU count from the dataset, so
        // it reads 86 with no code change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) Ultra 9 285HX", out var ultra9285Hx));
        Assert.Equal("Intel", ultra9285Hx.Vendor);
        Assert.Equal(2.8, ultra9285Hx.BaseGhz);
        Assert.Equal(5.5, ultra9285Hx.BoostGhz);
        Assert.Equal(4.6, ultra9285Hx.ECoreBoostGhz);
        Assert.StartsWith("https://www.intel.com/content/www/us/en/products/compare.html?productIds=242292,242293,242294,242297", ultra9285Hx.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) Ultra 9 275HX", out var ultra9275Hx));
        Assert.Equal(2.7, ultra9275Hx.BaseGhz);
        Assert.Equal(5.4, ultra9275Hx.BoostGhz);
        Assert.Equal(4.6, ultra9275Hx.ECoreBoostGhz);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) Ultra 7 265HX", out var ultra7265Hx));
        Assert.Equal(2.6, ultra7265Hx.BaseGhz);
        Assert.Equal(5.3, ultra7265Hx.BoostGhz);
        Assert.Equal(4.6, ultra7265Hx.ECoreBoostGhz);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) Ultra 7 255HX", out var ultra7255Hx));
        Assert.Equal(2.4, ultra7255Hx.BaseGhz);
        Assert.Equal(5.2, ultra7255Hx.BoostGhz);
        Assert.Equal(4.5, ultra7255Hx.ECoreBoostGhz);
    }

    [Fact]
    public void Default_Ryzen9000X3dDesktopCpusCarryVerifiedFigures()
    {
        // Dataset v1.16.0: the three AMD Ryzen 9000 X3D desktop CPUs
        // (Granite Ridge, Zen 5 with 3D V-Cache), the family the dataset
        // never listed: Ryzen 7 9800X3D, Ryzen 9 9900X3D and Ryzen 9
        // 9950X3D. The gap went unnoticed the same way the Intel HX gaps
        // did: every cycle re-examined the deliberately-absent list, but
        // no 9000 X3D chip was on it, so the family was never listed
        // against the dataset until this cycle did. All figures below
        // were verified by hand against AMD's own product pages, read
        // 2026-11-14 from fresh crawls: the Ryzen 7 9800X3D at Base
        // Clock 4.7 GHz and Max Boost Clock up to 5.2 GHz; the Ryzen 9
        // 9900X3D at 4.4 and up to 5.5 GHz; the Ryzen 9 9950X3D at 4.3
        // and up to 5.7 GHz. The dataset's boost_ghz is AMD's Max
        // Boost Clock, per its own definition, and the 7800X3D already
        // in the dataset follows the same rule, so these three are
        // consistent with it. No code change was needed: the CPU
        // normalizer already maps "AMD Ryzen 7 9800X3D" to
        // "RYZEN 7 9800X3D", and the Settings card shows the CPU count
        // from the dataset, so it reads 89 with no code change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 7 9800X3D", out var ryzen79800X3d));
        Assert.Equal("AMD", ryzen79800X3d.Vendor);
        Assert.Equal(4.7, ryzen79800X3d.BaseGhz);
        Assert.Equal(5.2, ryzen79800X3d.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/desktops/ryzen/9000-series/amd-ryzen-7-9800x3d.html", ryzen79800X3d.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 9 9900X3D", out var ryzen99900X3d));
        Assert.Equal(4.4, ryzen99900X3d.BaseGhz);
        Assert.Equal(5.5, ryzen99900X3d.BoostGhz);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 9 9950X3D", out var ryzen99950X3d));
        Assert.Equal(4.3, ryzen99950X3d.BaseGhz);
        Assert.Equal(5.7, ryzen99950X3d.BoostGhz);
    }

    [Fact]
    public void Default_Ryzen7000X3dDesktopCpusCarryVerifiedFigures()
    {
        // Dataset v1.17.0: the two AMD Ryzen 7000 X3D desktop CPUs
        // (Raphael, Zen 4 with 3D V-Cache), the family the dataset
        // never listed: Ryzen 9 7900X3D and Ryzen 9 7950X3D. Only the
        // Ryzen 7 7800X3D was in the dataset, so the two Ryzen 9 X3D
        // chips were the missing half of the family. The gap went
        // unnoticed the same way the 9000 X3D gap did: every cycle
        // re-examined the deliberately-absent list, but no 7000 X3D
        // chip was on it, so the family was never listed against the
        // dataset until this cycle did. All figures below were verified
        // by hand against AMD's own product pages, read 2026-11-14
        // from fresh crawls: the Ryzen 9 7900X3D at Base Clock 4.4 GHz
        // and Max Boost Clock up to 5.6 GHz; the Ryzen 9 7950X3D at
        // 4.2 and up to 5.7 GHz. The dataset's boost_ghz is AMD's Max
        // Boost Clock, per its own definition, and the 7800X3D already
        // in the dataset follows the same rule, so these two are
        // consistent with it. No code change was needed: the CPU
        // normalizer already maps "AMD Ryzen 9 7950X3D" to
        // "RYZEN 9 7950X3D", and the Settings card shows the CPU count
        // from the dataset, so it reads 91 with no code change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 9 7900X3D", out var ryzen97900X3d));
        Assert.Equal("AMD", ryzen97900X3d.Vendor);
        Assert.Equal(4.4, ryzen97900X3d.BaseGhz);
        Assert.Equal(5.6, ryzen97900X3d.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/desktops/ryzen/7000-series/amd-ryzen-9-7900x3d.html", ryzen97900X3d.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 9 7950X3D", out var ryzen97950X3d));
        Assert.Equal(4.2, ryzen97950X3d.BaseGhz);
        Assert.Equal(5.7, ryzen97950X3d.BoostGhz);
    }

    [Fact]
    public void Default_Ryzen5000X3dDesktopCpuCarriesVerifiedFigures()
    {
        // Dataset v1.18.0: the AMD Ryzen 7 5800X3D desktop CPU
        // (Vermeer, Zen 3 with 3D V-Cache), the chip the dataset never
        // listed. The 9000 X3D family shipped in v2.1.48 and the 7000
        // X3D family in v2.1.49, but the original X3D chip, the one
        // that started the family and is still the most asked about
        // AM4 gaming CPU, was missing. The gap went unnoticed the
        // same way the 9000 and 7000 X3D gaps did: every cycle
        // re-examined the deliberately-absent list, but no 5000 X3D
        // chip was on it, so the family was never listed against the
        // dataset until this cycle did. The figure below was verified
        // by hand against AMD's own product page, read 2026-11-14
        // from a fresh crawl: Base Clock 3.4 GHz and Max Boost Clock
        // up to 4.5 GHz. The dataset's boost_ghz is AMD's Max Boost
        // Clock, per its own definition, and the 7800X3D already in
        // the dataset follows the same rule, so this one is consistent
        // with it. The Ryzen 7 5700X3D was examined in the same cycle
        // and stays out: AMD's own product page could not be read
        // live (the product URL returns 404 and the legacy product-ID
        // URL redirects to a generic specifications hub), so its
        // figures stay out even though third-party listings agree on
        // Base Clock 3.0 GHz and Max Boost Clock up to 4.1 GHz, and
        // the dataset records that reason in its coverage text. No
        // code change was needed: the CPU normalizer already maps
        // "AMD Ryzen 7 5800X3D" to "RYZEN 7 5800X3D", and the
        // Settings card shows the CPU count from the dataset, so it
        // reads 92 with no code change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 7 5800X3D", out var ryzen75800X3d));
        Assert.Equal("AMD", ryzen75800X3d.Vendor);
        Assert.Equal(3.4, ryzen75800X3d.BaseGhz);
        Assert.Equal(4.5, ryzen75800X3d.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/desktops/ryzen/5000-series/amd-ryzen-7-5800x3d.html", ryzen75800X3d.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_Ryzen7000NonXDesktopCpusCarryVerifiedFigures()
    {
        // Dataset v1.19.0: the three AMD Ryzen 7000 non-X desktop CPUs
        // (Raphael, Zen 4), the half of the family the dataset never
        // listed. The X chips shipped long ago (7950X, 7900X, 7700X,
        // 7600) and the X3D chips in v2.1.48 and v2.1.49, but the
        // non-X siblings that sit between them on every retail shelf,
        // the 65 W 7900 and 7700 and the 105 W 7600X, were missing.
        // The gap went unnoticed the same way the X3D gaps did: every
        // cycle re-examined the deliberately-absent list, but no 7000
        // non-X chip was on it, so the family was never listed
        // against the dataset until this cycle did. All three figures
        // were verified by hand against AMD's own product pages, read
        // 2026-11-14 from fresh crawls: the Ryzen 9 7900 at Base
        // Clock 3.7 GHz and Max Boost Clock up to 5.4 GHz; the Ryzen
        // 7 7700 at 3.8 and up to 5.3 GHz; the Ryzen 5 7600X at 4.7
        // and up to 5.3 GHz. The dataset's boost_ghz is AMD's Max
        // Boost Clock, per its own definition, and the 7800X3D
        // already in the dataset follows the same rule, so these
        // three are consistent with it. No code change was needed:
        // the CPU normalizer already maps "AMD Ryzen 9 7900" to
        // "RYZEN 9 7900", and the Settings card shows the CPU count
        // from the dataset, so it reads 95 with no code change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 9 7900", out var ryzen97900));
        Assert.Equal("AMD", ryzen97900.Vendor);
        Assert.Equal(3.7, ryzen97900.BaseGhz);
        Assert.Equal(5.4, ryzen97900.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/desktops/ryzen/7000-series/amd-ryzen-9-7900.html", ryzen97900.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 7 7700", out var ryzen77700));
        Assert.Equal("AMD", ryzen77700.Vendor);
        Assert.Equal(3.8, ryzen77700.BaseGhz);
        Assert.Equal(5.3, ryzen77700.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/desktops/ryzen/7000-series/amd-ryzen-7-7700.html", ryzen77700.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 7600X", out var ryzen57600X));
        Assert.Equal("AMD", ryzen57600X.Vendor);
        Assert.Equal(4.7, ryzen57600X.BaseGhz);
        Assert.Equal(5.3, ryzen57600X.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/desktops/ryzen/7000-series/amd-ryzen-5-7600x.html", ryzen57600X.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_Ryzen9000NonXDesktopCpusCarryVerifiedFigures()
    {
        // Dataset v1.20.0: the three AMD Ryzen 9000 non-X desktop CPUs
        // (Granite Ridge, Zen 5), the half of the family the dataset
        // never listed. The X chips shipped long ago (9950X, 9900X,
        // 9700X, 9600X) and the X3D chips in v2.1.48, but the non-X
        // siblings sat missing: the 65 W 9600, and the two F chips
        // (no integrated graphics) 9700F and 9500F, launched in 2025
        // after the X launch. The gap went unnoticed the same way the
        // 7000 non-X gap did: every cycle re-examined the
        // deliberately-absent list, but no 9000 non-X chip was on it,
        // so the family was never listed against the dataset until
        // this cycle did. All three figures were verified by hand
        // against AMD's own product pages, read 2026-11-14 from fresh
        // crawls: the Ryzen 5 9600 at Base Clock 3.8 GHz and Max Boost
        // Clock up to 5.2 GHz; the Ryzen 7 9700F at 3.8 and up to
        // 5.5 GHz; the Ryzen 5 9500F at 3.8 and up to 5.0 GHz. AMD
        // lists the 9700F with Regional Availability NA; it is a
        // desktop part with a full AMD spec page, so it is included
        // and the coverage note records the availability. No plain
        // Ryzen 9 9900 or Ryzen 7 9700 AMD product page could be
        // found after targeted searches (only 9900X, 9900X3D, 9700X
        // and 9700F surfaced), so neither is added. The dataset's
        // boost_ghz is AMD's Max Boost Clock, per its own
        // definition, and the 9800X3D already in the dataset follows
        // the same rule, so these three are consistent with it. No
        // code change was needed: the CPU normalizer already maps
        // "AMD Ryzen 5 9600" to "RYZEN 5 9600", and the Settings
        // card shows the CPU count from the dataset, so it reads 98
        // with no code change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 9600", out var ryzen59600));
        Assert.Equal("AMD", ryzen59600.Vendor);
        Assert.Equal(3.8, ryzen59600.BaseGhz);
        Assert.Equal(5.2, ryzen59600.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/desktops/ryzen/9000-series/amd-ryzen-5-9600.html", ryzen59600.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 7 9700F", out var ryzen79700F));
        Assert.Equal("AMD", ryzen79700F.Vendor);
        Assert.Equal(3.8, ryzen79700F.BaseGhz);
        Assert.Equal(5.5, ryzen79700F.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/desktops/ryzen/9000-series/amd-ryzen-7-9700f.html", ryzen79700F.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 9500F", out var ryzen59500F));
        Assert.Equal("AMD", ryzen59500F.Vendor);
        Assert.Equal(3.8, ryzen59500F.BaseGhz);
        Assert.Equal(5.0, ryzen59500F.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/desktops/ryzen/9000-series/amd-ryzen-5-9500f.html", ryzen59500F.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_Ryzen9000X3dNewDesktopCpusCarryVerifiedFigures()
    {
        // Dataset v1.21.0: the two newer AMD Ryzen 9000 X3D desktop CPUs
        // (Granite Ridge, Zen 5 with 3D V-Cache) that launched after the
        // v2.1.48 X3D slice: the Ryzen 7 9850X3D, the successor to the
        // 9800X3D with a 400 MHz higher boost, and the Ryzen 9 9950X3D2
        // Dual Edition, the first desktop processor with dual 3D
        // V-Cache stacks. Both figures were verified by hand against
        // AMD's own product pages, read 2026-11-15 from fresh crawls:
        // the Ryzen 7 9850X3D at Base Clock 4.7 GHz and Max Boost Clock
        // up to 5.6 GHz, launch date 1/29/2026; the Ryzen 9 9950X3D2
        // Dual Edition at 4.3 and up to 5.6 GHz, launch date 4/22/26.
        // The dataset's boost_ghz is AMD's Max Boost Clock, per its
        // own definition, and the 9800X3D already in the dataset
        // follows the same rule, so these two are consistent with it.
        // No code change was needed: the CPU normalizer already maps
        // "AMD Ryzen 7 9850X3D" to "RYZEN 7 9850X3D" and "AMD Ryzen 9
        // 9950X3D2 Dual Edition" to "RYZEN 9 9950X3D2 DUAL EDITION",
        // and the Settings card shows the CPU count from the dataset,
        // so it reads 100 with no code change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 7 9850X3D", out var ryzen79850X3d));
        Assert.Equal("AMD", ryzen79850X3d.Vendor);
        Assert.Equal(4.7, ryzen79850X3d.BaseGhz);
        Assert.Equal(5.6, ryzen79850X3d.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/desktops/ryzen/9000-series/amd-ryzen-7-9850x3d.html", ryzen79850X3d.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 9 9950X3D2 Dual Edition", out var ryzen99950X3d2));
        Assert.Equal("AMD", ryzen99950X3d2.Vendor);
        Assert.Equal(4.3, ryzen99950X3d2.BaseGhz);
        Assert.Equal(5.6, ryzen99950X3d2.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/desktops/ryzen/9000-series/amd-ryzen-9-9950x3d2-dual-edition.html", ryzen99950X3d2.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_Ryzen5000DesktopCpusCarryVerifiedFigures()
    {
        // Dataset v1.22.0: the three AMD Ryzen 5000 desktop CPUs (Zen 3)
        // the dataset never listed, the remainder of the 5000 desktop
        // family after the 5600, 5600X, 5800X, 5800X3D, 5900X and 5950X
        // already in it: the Ryzen 7 5700X (Vermeer), the Ryzen 7 5700
        // and the Ryzen 5 5500 (both Cezanne). All figures were verified
        // by hand against AMD's own product pages, read 2026-11-15 from
        // fresh crawls: the Ryzen 7 5700X at Base Clock 3.4 GHz and Max
        // Boost Clock up to 4.6 GHz; the Ryzen 7 5700 at 3.7 and up to
        // 4.6 GHz; the Ryzen 5 5500 at 3.6 and up to 4.2 GHz. The
        // dataset's boost_ghz is AMD's Max Boost Clock, per its own
        // definition, and the 5800X already in the dataset follows the
        // same rule, so these three are consistent with it. AMD moved
        // these three pages to the support downloads path, so their
        // source URLs use amd.com/en/support/downloads/drivers.html
        // instead of the products path the 5800X entry uses. No code
        // change was needed: the CPU normalizer already maps
        // "AMD Ryzen 7 5700X" to "RYZEN 7 5700X", "AMD Ryzen 7 5700"
        // to "RYZEN 7 5700" and "AMD Ryzen 5 5500" to "RYZEN 5 5500",
        // and the Settings card shows the CPU count from the dataset,
        // so it reads 103 with no code change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 7 5700X", out var ryzen75700X));
        Assert.Equal("AMD", ryzen75700X.Vendor);
        Assert.Equal(3.4, ryzen75700X.BaseGhz);
        Assert.Equal(4.6, ryzen75700X.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/support/downloads/drivers.html/processors/ryzen/ryzen-5000-series/amd-ryzen-7-5700x.html", ryzen75700X.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 7 5700", out var ryzen75700));
        Assert.Equal("AMD", ryzen75700.Vendor);
        Assert.Equal(3.7, ryzen75700.BaseGhz);
        Assert.Equal(4.6, ryzen75700.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/support/downloads/drivers.html/processors/ryzen/ryzen-5000-series/amd-ryzen-7-5700.html", ryzen75700.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 5500", out var ryzen55500));
        Assert.Equal("AMD", ryzen55500.Vendor);
        Assert.Equal(3.6, ryzen55500.BaseGhz);
        Assert.Equal(4.2, ryzen55500.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/support/downloads/drivers.html/processors/ryzen/ryzen-5000-series/amd-ryzen-5-5500.html", ryzen55500.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_RyzenAi300MobileCpusCarryVerifiedFigures()
    {
        // Dataset v1.24.0: the five AMD Ryzen AI 300 mobile CPUs (Zen 5,
        // Strix Point and Krackan Point) the dataset never listed, the
        // remainder of the consumer AI 300 family after the Ryzen AI 9 HX
        // 370 already in it. The family check found two more than the
        // three the previous cycle flagged: the Ryzen AI 9 HX 375 (a July
        // 2025 addition to the series) and the Ryzen AI 7 345 (a January
        // 2025 Krackan Point part). All figures were verified by hand
        // against AMD's own product pages, read 2026-11-15 from fresh
        // crawls: the Ryzen AI 9 HX 375 at Base Clock 2.0 GHz and Max
        // Boost Clock up to 5.1 GHz; the Ryzen AI 9 365 at 2.0 and up to
        // 5.0 GHz; the Ryzen AI 7 350 at 2.0 and up to 5.0 GHz; the
        // Ryzen AI 7 345 at 2.0 and up to 4.6 GHz; the Ryzen AI 5 340 at
        // 2.0 and up to 4.8 GHz. The dataset's boost_ghz is AMD's Max
        // Boost Clock, per its own definition, and the HX 370 already in
        // the dataset follows the same rule, so these five are consistent
        // with it. AMD also lists a separate Max Zen5c Clock for the
        // compact cores (3.3 GHz on the HX 375 and 365, 3.5 GHz on the
        // 350, 3.4 GHz on the 345 and 340); that is a per-core-type
        // figure, not the headline Max Boost Clock, so it is not the
        // dataset's boost. No code change was needed: the CPU normalizer
        // already maps "AMD Ryzen AI 9 HX 375" to "RYZEN AI 9 HX 375",
        // "AMD Ryzen AI 9 365" to "RYZEN AI 9 365", "AMD Ryzen AI 7
        // 350" to "RYZEN AI 7 350", "AMD Ryzen AI 7 345" to
        // "RYZEN AI 7 345" and "AMD Ryzen AI 5 340" to "RYZEN AI 5 340",
        // and the Settings card shows the CPU count from the dataset,
        // so it reads 108 with no code change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 9 HX 375", out var ryzenAi9Hx375));
        Assert.Equal("AMD", ryzenAi9Hx375.Vendor);
        Assert.Equal(2.0, ryzenAi9Hx375.BaseGhz);
        Assert.Equal(5.1, ryzenAi9Hx375.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/ai-300-series/amd-ryzen-ai-9-hx-375.html", ryzenAi9Hx375.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 9 365", out var ryzenAi9365));
        Assert.Equal("AMD", ryzenAi9365.Vendor);
        Assert.Equal(2.0, ryzenAi9365.BaseGhz);
        Assert.Equal(5.0, ryzenAi9365.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/ai-300-series/amd-ryzen-ai-9-365.html", ryzenAi9365.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 7 350", out var ryzenAi7350));
        Assert.Equal("AMD", ryzenAi7350.Vendor);
        Assert.Equal(2.0, ryzenAi7350.BaseGhz);
        Assert.Equal(5.0, ryzenAi7350.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/ai-300-series/amd-ryzen-ai-7-350.html", ryzenAi7350.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 7 345", out var ryzenAi7345));
        Assert.Equal("AMD", ryzenAi7345.Vendor);
        Assert.Equal(2.0, ryzenAi7345.BaseGhz);
        Assert.Equal(4.6, ryzenAi7345.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/ai-300-series/amd-ryzen-ai-7-345.html", ryzenAi7345.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 5 340", out var ryzenAi5340));
        Assert.Equal("AMD", ryzenAi5340.Vendor);
        Assert.Equal(2.0, ryzenAi5340.BaseGhz);
        Assert.Equal(4.8, ryzenAi5340.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/ai-300-series/amd-ryzen-ai-5-340.html", ryzenAi5340.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_RyzenAiMaxMobileCpusCarryVerifiedFigures()
    {
        // Dataset v1.25.0: the five AMD Ryzen AI Max mobile CPUs (Zen 5,
        // Strix Halo), the whole consumer AI Max family, which the dataset
        // never listed. The Ryzen AI Max+ 395, Max 390 and Max 385 launched
        // at CES 2025, and the Ryzen AI Max+ 392 and Max+ 388 were added at
        // CES 2026, announced January 5, 2026 as the new additions that
        // bring Radeon 8060S graphics to lower core counts. All figures
        // were verified by hand against AMD's own pages, read 2026-11-15
        // from fresh fetches: the Ryzen AI Max+ 395 at Base Clock 3.0 GHz
        // and Max Boost Clock up to 5.1 GHz; the Ryzen AI Max+ 392 at 3.2
        // and up to 5.0 GHz; the Ryzen AI Max 390 at 3.2 and up to 5.0
        // GHz; the Ryzen AI Max+ 388 at 3.6 and up to 5.0 GHz; the Ryzen
        // AI Max 385 at 3.6 and up to 5.0 GHz. The dataset's boost_ghz is
        // AMD's Max Boost Clock, per its own definition, and the AI 300
        // chips already in the dataset follow the same rule, so these five
        // are consistent with them. The 392's figures come from AMD's own
        // driver page for the chip, which carries the same spec table as
        // the product pages; the other four come from their product pages.
        // No code change was needed: the CPU normalizer already maps
        // "AMD Ryzen AI Max+ 395" to "RYZEN AI MAX+ 395", "AMD Ryzen AI
        // Max+ 392" to "RYZEN AI MAX+ 392", "AMD Ryzen AI Max 390" to
        // "RYZEN AI MAX 390", "AMD Ryzen AI Max+ 388" to "RYZEN AI MAX+
        // 388" and "AMD Ryzen AI Max 385" to "RYZEN AI MAX 385", and the
        // Settings card shows the CPU count from the dataset, so it reads
        // 113 with no code change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI Max+ 395", out var ryzenAiMaxPlus395));
        Assert.Equal("AMD", ryzenAiMaxPlus395.Vendor);
        Assert.Equal(3.0, ryzenAiMaxPlus395.BaseGhz);
        Assert.Equal(5.1, ryzenAiMaxPlus395.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/ai-300-series/amd-ryzen-ai-max-plus-395.html", ryzenAiMaxPlus395.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI Max+ 392", out var ryzenAiMaxPlus392));
        Assert.Equal("AMD", ryzenAiMaxPlus392.Vendor);
        Assert.Equal(3.2, ryzenAiMaxPlus392.BaseGhz);
        Assert.Equal(5.0, ryzenAiMaxPlus392.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/support/downloads/drivers.html/processors/ryzen/ryzen-ai-max-series/amd-ryzen-ai-max-plus-392.html", ryzenAiMaxPlus392.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI Max 390", out var ryzenAiMax390));
        Assert.Equal("AMD", ryzenAiMax390.Vendor);
        Assert.Equal(3.2, ryzenAiMax390.BaseGhz);
        Assert.Equal(5.0, ryzenAiMax390.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/ai-300-series/amd-ryzen-ai-max-390.html", ryzenAiMax390.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI Max+ 388", out var ryzenAiMaxPlus388));
        Assert.Equal("AMD", ryzenAiMaxPlus388.Vendor);
        Assert.Equal(3.6, ryzenAiMaxPlus388.BaseGhz);
        Assert.Equal(5.0, ryzenAiMaxPlus388.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/ai-300-series/amd-ryzen-ai-max-plus-388.html", ryzenAiMaxPlus388.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI Max 385", out var ryzenAiMax385));
        Assert.Equal("AMD", ryzenAiMax385.Vendor);
        Assert.Equal(3.6, ryzenAiMax385.BaseGhz);
        Assert.Equal(5.0, ryzenAiMax385.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/ai-300-series/amd-ryzen-ai-max-385.html", ryzenAiMax385.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_RyzenAi400MobileCpusCarryVerifiedFigures()
    {
        // Dataset v1.26.0: the seven AMD Ryzen AI 400 mobile CPUs (Zen 5,
        // Gorgon Point), the whole consumer AI 400 family, which the dataset
        // never listed. All seven launched at CES 2026 on January 5, 2026:
        // the Ryzen AI 9 HX 475 and HX 470 are the 12-core Strix Point
        // refresh flagships, the Ryzen AI 9 465 is the 10-core part, the
        // Ryzen AI 7 450 and 445 and the Ryzen AI 5 435 and 430 round out
        // the stack down to 4 cores. All figures were verified by hand
        // against AMD's own product pages, read 2026-11-15 from fresh
        // fetches: the Ryzen AI 9 HX 475 at Base Clock 2.0 GHz and Max
        // Boost Clock up to 5.2 GHz; the Ryzen AI 9 HX 470 at 2.0 and up
        // to 5.2 GHz; the Ryzen AI 9 465 at 2.0 and up to 5.0 GHz; the
        // Ryzen AI 7 450 at 2.0 and up to 5.1 GHz; the Ryzen AI 7 445 at
        // 2.0 and up to 4.6 GHz; the Ryzen AI 5 435 at 2.0 and up to 4.5
        // GHz; the Ryzen AI 5 430 at 2.0 and up to 4.5 GHz. The dataset's
        // boost_ghz is AMD's Max Boost Clock, per its own definition, and
        // the AI 300 and AI Max chips already in the dataset follow the
        // same rule, so these seven are consistent with them. AMD also
        // lists a separate Max Zen5c Clock for the compact cores (3.3 GHz
        // on the HX 475, HX 470 and 465, 3.6 GHz on the 450, 3.4 GHz on
        // the 445, 435 and 430); that is a per-core-type figure, not the
        // headline Max Boost Clock, so it is not the dataset's boost.
        // Pre-launch leaks had put the HX 470 at 5.25 GHz and the 450 at
        // 5.15 GHz; AMD's final published figures are 5.2 and 5.1 GHz,
        // and those are the figures used here. The Ryzen AI 7 HX 455
        // stays out: AMD has not officially confirmed it, and no clock
        // figures exist for it. The Ryzen 5 439 stays out: it is a
        // non-AI part of the wider Ryzen 400 mobile range. The PRO
        // variants and the AM5 desktop G and GE parts stay out: the
        // dataset covers consumer mobile parts in this slice. No code
        // change was needed: the CPU normalizer already maps "AMD Ryzen
        // AI 9 HX 475" to "RYZEN AI 9 HX 475", "AMD Ryzen AI 9 HX 470"
        // to "RYZEN AI 9 HX 470", "AMD Ryzen AI 9 465" to "RYZEN AI 9
        // 465", "AMD Ryzen AI 7 450" to "RYZEN AI 7 450", "AMD Ryzen AI
        // 7 445" to "RYZEN AI 7 445", "AMD Ryzen AI 5 435" to "RYZEN AI
        // 5 435" and "AMD Ryzen AI 5 430" to "RYZEN AI 5 430", and the
        // Settings card shows the CPU count from the dataset, so it reads
        // 120 with no code change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 9 HX 475", out var ryzenAi9Hx475));
        Assert.Equal("AMD", ryzenAi9Hx475.Vendor);
        Assert.Equal(2.0, ryzenAi9Hx475.BaseGhz);
        Assert.Equal(5.2, ryzenAi9Hx475.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/ai-400-series/amd-ryzen-ai-9-hx-475.html", ryzenAi9Hx475.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 9 HX 470", out var ryzenAi9Hx470));
        Assert.Equal("AMD", ryzenAi9Hx470.Vendor);
        Assert.Equal(2.0, ryzenAi9Hx470.BaseGhz);
        Assert.Equal(5.2, ryzenAi9Hx470.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/ai-400-series/amd-ryzen-ai-9-hx-470.html", ryzenAi9Hx470.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 9 465", out var ryzenAi9465));
        Assert.Equal("AMD", ryzenAi9465.Vendor);
        Assert.Equal(2.0, ryzenAi9465.BaseGhz);
        Assert.Equal(5.0, ryzenAi9465.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/ai-400-series/amd-ryzen-ai-9-465.html", ryzenAi9465.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 7 450", out var ryzenAi7450));
        Assert.Equal("AMD", ryzenAi7450.Vendor);
        Assert.Equal(2.0, ryzenAi7450.BaseGhz);
        Assert.Equal(5.1, ryzenAi7450.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/ai-400-series/amd-ryzen-ai-7-450.html", ryzenAi7450.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 7 445", out var ryzenAi7445));
        Assert.Equal("AMD", ryzenAi7445.Vendor);
        Assert.Equal(2.0, ryzenAi7445.BaseGhz);
        Assert.Equal(4.6, ryzenAi7445.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/ai-400-series/amd-ryzen-ai-7-445.html", ryzenAi7445.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 5 435", out var ryzenAi5435));
        Assert.Equal("AMD", ryzenAi5435.Vendor);
        Assert.Equal(2.0, ryzenAi5435.BaseGhz);
        Assert.Equal(4.5, ryzenAi5435.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/ai-400-series/amd-ryzen-ai-5-435.html", ryzenAi5435.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 5 430", out var ryzenAi5430));
        Assert.Equal("AMD", ryzenAi5430.Vendor);
        Assert.Equal(2.0, ryzenAi5430.BaseGhz);
        Assert.Equal(4.5, ryzenAi5430.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/ai-400-series/amd-ryzen-ai-5-430.html", ryzenAi5430.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_Ryzen200MobileCpusCarryVerifiedFigures()
    {
        // Dataset v1.27.0: the fourteen AMD Ryzen 200 mobile CPUs (Zen 4,
        // Hawk Point), the whole consumer Ryzen 200 family, which the
        // dataset never listed. Seven launched at CES 2025 on January 6,
        // 2025, with systems from Q2 2025: the Ryzen 9 270 is the 8-core
        // flagship, the Ryzen 7 260 and 250 are the 8-core mid stack, the
        // Ryzen 5 240, 230 and 220 are the 6-core parts, and the Ryzen 3
        // 210 is the 4-core entry part. Seven more were added on June 2,
        // 2026: the Ryzen 7 253 and 249, the Ryzen 5 225 and 224, the
        // Ryzen 7 217 and Ryzen 5 216, and the Ryzen 3 205. All figures
        // were verified by hand against AMD's own product pages, read
        // 2026-11-15 from fresh fetches, and cross-checked against AMD's
        // CES 2025 press release for the original seven: the Ryzen 9 270
        // at Base Clock 4.0 GHz and Max Boost Clock up to 5.2 GHz; the
        // Ryzen 7 260 at 3.8 and up to 5.1 GHz; the Ryzen 7 250 at 3.3
        // and up to 5.1 GHz; the Ryzen 5 240 at 4.3 and up to 5.0 GHz;
        // the Ryzen 5 230 at 3.5 and up to 4.9 GHz; the Ryzen 5 220 at
        // 3.2 and up to 4.9 GHz; the Ryzen 3 210 at 3.0 and up to 4.7
        // GHz; the Ryzen 7 253 at 3.6 and up to 4.9 GHz; the Ryzen 7 249
        // at 3.1 and up to 4.9 GHz; the Ryzen 5 225 at 4.1 and up to 4.8
        // GHz; the Ryzen 5 224 at 3.3 and up to 4.8 GHz; the Ryzen 7 217
        // at 3.0 and up to 4.8 GHz; the Ryzen 5 216 at 2.9 and up to 4.7
        // GHz; the Ryzen 3 205 at 2.8 and up to 4.6 GHz. The dataset's
        // base_ghz is AMD's headline Base Clock and boost_ghz is AMD's
        // Max Boost Clock, per its own definition. Five of the chips are
        // hybrid Zen 4 plus Zen 4c parts (the 220, 210, 217, 216 and
        // 205), and AMD also lists per-core-type clocks for them (for
        // example the 220 at Zen4 Base Clock 3.7 GHz, Zen4c Base Clock
        // 3.0 GHz and Max Zen4c Clock up to 3.5 GHz); those are
        // per-core-type figures, not the headline Base Clock and Max
        // Boost Clock, so they are not the dataset's figures, following
        // the same rule as the AI 400 chips' Max Zen5c Clock. Two
        // naming quirks are real, not errors: the Ryzen 3 205 has six
        // cores but only eight threads (two Zen 4 plus four Zen 4c
        // cores), and the Ryzen 7 217 is a six-core part (two Zen 4
        // plus four Zen 4c cores). The PRO variants stay out (Ryzen 7
        // PRO 250, Ryzen 5 PRO 230, Ryzen 5 PRO 220, Ryzen 3 PRO 210):
        // the dataset covers consumer parts, and no PRO chip has been
        // listed in any cycle. No code change was needed: the CPU
        // normalizer already maps "AMD Ryzen 9 270" to "RYZEN 9 270",
        // "AMD Ryzen 7 260" to "RYZEN 7 260", "AMD Ryzen 7 250" to
        // "RYZEN 7 250", "AMD Ryzen 5 240" to "RYZEN 5 240", "AMD Ryzen
        // 5 230" to "RYZEN 5 230", "AMD Ryzen 5 220" to "RYZEN 5 220",
        // "AMD Ryzen 3 210" to "RYZEN 3 210", "AMD Ryzen 7 253" to
        // "RYZEN 7 253", "AMD Ryzen 7 249" to "RYZEN 7 249", "AMD Ryzen
        // 5 225" to "RYZEN 5 225", "AMD Ryzen 5 224" to "RYZEN 5 224",
        // "AMD Ryzen 7 217" to "RYZEN 7 217", "AMD Ryzen 5 216" to
        // "RYZEN 5 216" and "AMD Ryzen 3 205" to "RYZEN 3 205", and the
        // Settings card shows the CPU count from the dataset, so it reads
        // 134 with no code change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 9 270", out var ryzen9270));
        Assert.Equal("AMD", ryzen9270.Vendor);
        Assert.Equal(4.0, ryzen9270.BaseGhz);
        Assert.Equal(5.2, ryzen9270.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/200-series/amd-ryzen-9-270.html", ryzen9270.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 7 260", out var ryzen7260));
        Assert.Equal("AMD", ryzen7260.Vendor);
        Assert.Equal(3.8, ryzen7260.BaseGhz);
        Assert.Equal(5.1, ryzen7260.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/200-series/amd-ryzen-7-260.html", ryzen7260.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 7 250", out var ryzen7250));
        Assert.Equal("AMD", ryzen7250.Vendor);
        Assert.Equal(3.3, ryzen7250.BaseGhz);
        Assert.Equal(5.1, ryzen7250.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/200-series/amd-ryzen-7-250.html", ryzen7250.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 240", out var ryzen5240));
        Assert.Equal("AMD", ryzen5240.Vendor);
        Assert.Equal(4.3, ryzen5240.BaseGhz);
        Assert.Equal(5.0, ryzen5240.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/200-series/amd-ryzen-5-240.html", ryzen5240.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 230", out var ryzen5230));
        Assert.Equal("AMD", ryzen5230.Vendor);
        Assert.Equal(3.5, ryzen5230.BaseGhz);
        Assert.Equal(4.9, ryzen5230.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/200-series/amd-ryzen-5-230.html", ryzen5230.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 220", out var ryzen5220));
        Assert.Equal("AMD", ryzen5220.Vendor);
        Assert.Equal(3.2, ryzen5220.BaseGhz);
        Assert.Equal(4.9, ryzen5220.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/200-series/amd-ryzen-5-220.html", ryzen5220.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 3 210", out var ryzen3210));
        Assert.Equal("AMD", ryzen3210.Vendor);
        Assert.Equal(3.0, ryzen3210.BaseGhz);
        Assert.Equal(4.7, ryzen3210.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/200-series/amd-ryzen-3-210.html", ryzen3210.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 7 253", out var ryzen7253));
        Assert.Equal("AMD", ryzen7253.Vendor);
        Assert.Equal(3.6, ryzen7253.BaseGhz);
        Assert.Equal(4.9, ryzen7253.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/200-series/amd-ryzen-7-253.html", ryzen7253.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 7 249", out var ryzen7249));
        Assert.Equal("AMD", ryzen7249.Vendor);
        Assert.Equal(3.1, ryzen7249.BaseGhz);
        Assert.Equal(4.9, ryzen7249.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/200-series/amd-ryzen-7-249.html", ryzen7249.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 225", out var ryzen5225));
        Assert.Equal("AMD", ryzen5225.Vendor);
        Assert.Equal(4.1, ryzen5225.BaseGhz);
        Assert.Equal(4.8, ryzen5225.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/200-series/amd-ryzen-5-225.html", ryzen5225.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 224", out var ryzen5224));
        Assert.Equal("AMD", ryzen5224.Vendor);
        Assert.Equal(3.3, ryzen5224.BaseGhz);
        Assert.Equal(4.8, ryzen5224.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/200-series/amd-ryzen-5-224.html", ryzen5224.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 7 217", out var ryzen7217));
        Assert.Equal("AMD", ryzen7217.Vendor);
        Assert.Equal(3.0, ryzen7217.BaseGhz);
        Assert.Equal(4.8, ryzen7217.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/200-series/amd-ryzen-7-217.html", ryzen7217.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 216", out var ryzen5216));
        Assert.Equal("AMD", ryzen5216.Vendor);
        Assert.Equal(2.9, ryzen5216.BaseGhz);
        Assert.Equal(4.7, ryzen5216.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/200-series/amd-ryzen-5-216.html", ryzen5216.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 3 205", out var ryzen3205));
        Assert.Equal("AMD", ryzen3205.Vendor);
        Assert.Equal(2.8, ryzen3205.BaseGhz);
        Assert.Equal(4.6, ryzen3205.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/200-series/amd-ryzen-3-205.html", ryzen3205.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_Ryzen100HawkPointMobileCpusCarryVerifiedFigures()
    {
        // Dataset v1.28.0: the four new AMD Ryzen 100 mobile CPUs (Zen 4,
        // Hawk Point, 4 nm, RDNA 3 graphics), the Hawk Point half of the
        // consumer Ryzen 100 family, which the dataset never listed. All
        // four launched on June 2, 2026: the Ryzen 9 180 is the 8-core
        // flagship, the Ryzen 7 165 and 155 are the 6-core parts, and the
        // Ryzen 5 125 is the 4-core entry part. All figures were verified
        // by hand against AMD's own product pages, read 2026-11-15 from
        // fresh fetches: the Ryzen 9 180 at Base Clock 3.6 GHz and Max
        // Boost Clock up to 4.9 GHz; the Ryzen 7 165 at 3.3 and up to 4.7
        // GHz; the Ryzen 7 155 at 3.0 and up to 4.7 GHz; the Ryzen 5 125
        // at 2.8 and up to 4.5 GHz. The dataset's base_ghz is AMD's
        // headline Base Clock and boost_ghz is AMD's Max Boost Clock, per
        // its own definition. The labelling question is settled by the
        // live pages: when these four chips appeared in July 2026,
        // reporting (ubergizmo, and the WhyCry July 11, 2026 note) said
        // AMD's portal listed them as Zen 3+ while their clock speeds
        // and 4 nm process matched Zen 4 silicon. The pages as read
        // 2026-11-15 now say "Processor Architecture, Zen 4" with
        // "Former Codename, Hawk Point", "TSMC 4nm FinFET" and RDNA 3
        // graphics on all four, so the dataset records them as the Zen 4
        // Hawk Point parts AMD's pages now describe. Two AMD quirks are
        // real, not errors: the Ryzen 5 125's own product-page URL slug
        // reads "amd-ryzen-7-125" while the page title and specs are the
        // Ryzen 5 125's, and the Ryzen 5 125 is a 4-core Ryzen 5 (its
        // 4-core, 8-thread layout matches the Ryzen 3 8440U class, not
        // the usual 6-core Ryzen 5). The Ryzen 9 180 and Ryzen 7 165
        // carry an XDNA NPU rated up to 16 TOPS; the 155 and 125 do not
        // list one, so no NPU figure is recorded for them. The older
        // Rembrandt Zen 3+ Ryzen 100 chips (Ryzen 7 170 and 160, Ryzen 5
        // 150 and 130, Ryzen 3 110, launched October 1, 2025) stay out:
        // they are a different sub-family (6 nm, RDNA 2) for a future
        // cycle. The hybrid Ryzen 5 126 (one Zen 4 plus three Zen 4c
        // cores, FP8 socket) also stays out: it is a later addition
        // outside the four named chips. No PRO variant of these chips
        // exists in the consumer range, and no PRO chip has been listed
        // in any cycle. No code change was needed: the CPU normalizer
        // already maps "AMD Ryzen 9 180" to "RYZEN 9 180", "AMD Ryzen 7
        // 165" to "RYZEN 7 165", "AMD Ryzen 7 155" to "RYZEN 7 155" and
        // "AMD Ryzen 5 125" to "RYZEN 5 125", and the Settings card shows
        // the CPU count from the dataset, so it reads 138 with no code
        // change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 9 180", out var ryzen9180));
        Assert.Equal("AMD", ryzen9180.Vendor);
        Assert.Equal(3.6, ryzen9180.BaseGhz);
        Assert.Equal(4.9, ryzen9180.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/100-series/amd-ryzen-9-180.html", ryzen9180.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 7 165", out var ryzen7165));
        Assert.Equal("AMD", ryzen7165.Vendor);
        Assert.Equal(3.3, ryzen7165.BaseGhz);
        Assert.Equal(4.7, ryzen7165.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/100-series/amd-ryzen-7-165.html", ryzen7165.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 7 155", out var ryzen7155));
        Assert.Equal("AMD", ryzen7155.Vendor);
        Assert.Equal(3.0, ryzen7155.BaseGhz);
        Assert.Equal(4.7, ryzen7155.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/100-series/amd-ryzen-7-155.html", ryzen7155.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 125", out var ryzen5125));
        Assert.Equal("AMD", ryzen5125.Vendor);
        Assert.Equal(2.8, ryzen5125.BaseGhz);
        Assert.Equal(4.5, ryzen5125.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/100-series/amd-ryzen-7-125.html", ryzen5125.SourceUrl, StringComparison.Ordinal);
    }
}
