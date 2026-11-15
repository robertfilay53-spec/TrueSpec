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
    [InlineData("Intel Core Ultra 7 165H", "ULTRA 7 165H")]
    [InlineData("Intel Core Ultra 5 135H", "ULTRA 5 135H")]
    [InlineData("Intel Core Ultra 7 265U", "ULTRA 7 265U")]
    [InlineData("Intel Core Ultra 7 255U", "ULTRA 7 255U")]
    [InlineData("Intel Core Ultra 5 235U", "ULTRA 5 235U")]
    [InlineData("Intel Core Ultra 5 225U", "ULTRA 5 225U")]
    [InlineData("Intel Core Ultra 7 165U", "ULTRA 7 165U")]
    [InlineData("Intel Core Ultra 7 155U", "ULTRA 7 155U")]
    [InlineData("Intel Core Ultra 5 135U", "ULTRA 5 135U")]
    [InlineData("Intel Core Ultra 5 125U", "ULTRA 5 125U")]
    [InlineData("Intel Core Ultra 7 165UL", "ULTRA 7 165UL")]
    [InlineData("Intel Core Ultra 7 155UL", "ULTRA 7 155UL")]
    [InlineData("Intel Core Ultra 5 135UL", "ULTRA 5 135UL")]
    [InlineData("Intel Core Ultra 5 125UL", "ULTRA 5 125UL")]
    [InlineData("Intel Core Ultra 3 105UL", "ULTRA 3 105UL")]
    [InlineData("Intel Core Ultra 7 165HL", "ULTRA 7 165HL")]
    [InlineData("Intel Core Ultra 7 155HL", "ULTRA 7 155HL")]
    [InlineData("Intel Core Ultra 5 135HL", "ULTRA 5 135HL")]
    [InlineData("Intel Core Ultra 5 125HL", "ULTRA 5 125HL")]
    [InlineData("Intel Core Ultra 7 164U", "ULTRA 7 164U")]
    [InlineData("Intel Core Ultra 5 134U", "ULTRA 5 134U")]
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
    [InlineData("AMD Ryzen 7 PRO 250", "RYZEN 7 PRO 250")]
    [InlineData("AMD Ryzen 5 PRO 230", "RYZEN 5 PRO 230")]
    [InlineData("AMD Ryzen 5 PRO 220", "RYZEN 5 PRO 220")]
    [InlineData("AMD Ryzen 3 PRO 210", "RYZEN 3 PRO 210")]
    [InlineData("AMD Ryzen 5 PRO 215", "RYZEN 5 PRO 215")]
    [InlineData("AMD Ryzen 5 PRO 216", "RYZEN 5 PRO 216")]
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
    [InlineData("AMD Ryzen AI 7 450G", "RYZEN AI 7 450G")]
    [InlineData("AMD Ryzen AI 7 450GE", "RYZEN AI 7 450GE")]
    [InlineData("AMD Ryzen AI 5 440G", "RYZEN AI 5 440G")]
    [InlineData("AMD Ryzen AI 5 440GE", "RYZEN AI 5 440GE")]
    [InlineData("AMD Ryzen AI 5 435G", "RYZEN AI 5 435G")]
    [InlineData("AMD Ryzen AI 5 435GE", "RYZEN AI 5 435GE")]
    [InlineData("AMD Ryzen AI 7 PRO 450G", "RYZEN AI 7 PRO 450G")]
    [InlineData("AMD Ryzen AI 7 PRO 450GE", "RYZEN AI 7 PRO 450GE")]
    [InlineData("AMD Ryzen AI 5 PRO 440G", "RYZEN AI 5 PRO 440G")]
    [InlineData("AMD Ryzen AI 5 PRO 440GE", "RYZEN AI 5 PRO 440GE")]
    [InlineData("AMD Ryzen AI 5 PRO 435G", "RYZEN AI 5 PRO 435G")]
    [InlineData("AMD Ryzen AI 5 PRO 435GE", "RYZEN AI 5 PRO 435GE")]
    [InlineData("AMD Ryzen 7 8700G", "RYZEN 7 8700G")]
    [InlineData("AMD Ryzen 5 8600G", "RYZEN 5 8600G")]
    [InlineData("AMD Ryzen 5 8500G", "RYZEN 5 8500G")]
    [InlineData("AMD Ryzen 3 8300G", "RYZEN 3 8300G")]
    [InlineData("AMD Ryzen 7 8700F", "RYZEN 7 8700F")]
    [InlineData("AMD Ryzen 5 8400F", "RYZEN 5 8400F")]
    [InlineData("AMD Ryzen 5 7500F", "RYZEN 5 7500F")]
    [InlineData("AMD Ryzen 5 7400F", "RYZEN 5 7400F")]
    [InlineData("AMD Ryzen 5 5600GT", "RYZEN 5 5600GT")]
    [InlineData("AMD Ryzen 5 5500GT", "RYZEN 5 5500GT")]
    [InlineData("AMD Ryzen 5 5600T", "RYZEN 5 5600T")]
    [InlineData("AMD Ryzen 5 5600XT", "RYZEN 5 5600XT")]
    [InlineData("AMD Ryzen 5 5600F", "RYZEN 5 5600F")]
    [InlineData("AMD Ryzen AI 9 HX PRO 475", "RYZEN AI 9 HX PRO 475")]
    [InlineData("AMD Ryzen AI 9 HX PRO 470", "RYZEN AI 9 HX PRO 470")]
    [InlineData("AMD Ryzen AI 9 PRO 465", "RYZEN AI 9 PRO 465")]
    [InlineData("AMD Ryzen AI 7 PRO 450", "RYZEN AI 7 PRO 450")]
    [InlineData("AMD Ryzen AI 5 PRO 440", "RYZEN AI 5 PRO 440")]
    [InlineData("AMD Ryzen AI 5 PRO 435", "RYZEN AI 5 PRO 435")]
    [InlineData("AMD Ryzen AI 9 HX PRO 375", "RYZEN AI 9 HX PRO 375")]
    [InlineData("AMD Ryzen AI 9 HX PRO 370", "RYZEN AI 9 HX PRO 370")]
    [InlineData("AMD Ryzen AI 7 PRO 360", "RYZEN AI 7 PRO 360")]
    [InlineData("AMD Ryzen AI 7 PRO 350", "RYZEN AI 7 PRO 350")]
    [InlineData("AMD Ryzen AI 5 PRO 340", "RYZEN AI 5 PRO 340")]
    [InlineData("AMD Ryzen 7 170", "RYZEN 7 170")]
    [InlineData("AMD Ryzen 7 160", "RYZEN 7 160")]
    [InlineData("AMD Ryzen 5 150", "RYZEN 5 150")]
    [InlineData("AMD Ryzen 5 130", "RYZEN 5 130")]
    [InlineData("AMD Ryzen 3 110", "RYZEN 3 110")]
    [InlineData("AMD Ryzen 5 126", "RYZEN 5 126")]
    [InlineData("Intel Core Ultra X7 368H", "ULTRA X7 368H")]
    [InlineData("Intel Core Ultra X9 388H", "ULTRA X9 388H")]
    [InlineData("Intel Core Ultra X9 378H", "ULTRA X9 378H")]
    [InlineData("Intel Core Ultra X7 358H", "ULTRA X7 358H")]
    [InlineData("Intel Core Ultra 9 386H", "ULTRA 9 386H")]
    [InlineData("Intel Core Ultra 7 366H", "ULTRA 7 366H")]
    [InlineData("Intel Core Ultra 5 338H", "ULTRA 5 338H")]
    [InlineData("Intel Core Ultra 7 356H", "ULTRA 7 356H")]
    [InlineData("Intel Core Ultra 5 336H", "ULTRA 5 336H")]
    [InlineData("Intel Core Ultra 7 365", "ULTRA 7 365")]
    [InlineData("Intel Core Ultra 5 325", "ULTRA 5 325")]
    [InlineData("Intel Core Ultra 7 355", "ULTRA 7 355")]
    [InlineData("Intel Core Ultra 5 335", "ULTRA 5 335")]
    [InlineData("Intel Core Ultra 5 332", "ULTRA 5 332")]
    [InlineData("Intel Core Ultra 5 322", "ULTRA 5 322")]
    [InlineData("intel core i5-12400", "I5-12400")]
    [InlineData("Intel(R) Core(TM) i5-12400F Processor", "I5-12400F")]
    [InlineData("Intel(R) Core(TM) i5-14400", "I5-14400")]
    [InlineData("Intel(R) Core(TM) i9-14900HX", "I9-14900HX")]
    [InlineData("Intel(R) Core(TM) Ultra 9 285HX", "ULTRA 9 285HX")]
    [InlineData("Intel(R) Core(TM) Ultra 9 290HX Plus", "ULTRA 9 290HX PLUS")]
    [InlineData("Intel(R) Core(TM) Ultra 7 270HX Plus", "ULTRA 7 270HX PLUS")]
    [InlineData("Intel(R) Core(TM) Ultra 7 251HX", "ULTRA 7 251HX")]
    [InlineData("Intel(R) Core(TM) Ultra 5 245HX", "ULTRA 5 245HX")]
    [InlineData("Intel(R) Core(TM) Ultra 5 235HX", "ULTRA 5 235HX")]
    [InlineData("Intel(R) Core(TM) Ultra 9 288V", "ULTRA 9 288V")]
    [InlineData("Intel(R) Core(TM) Ultra 7 268V", "ULTRA 7 268V")]
    [InlineData("Intel(R) Core(TM) Ultra 7 266V", "ULTRA 7 266V")]
    [InlineData("Intel(R) Core(TM) Ultra 7 256V", "ULTRA 7 256V")]
    [InlineData("Intel(R) Core(TM) Ultra 5 238V", "ULTRA 5 238V")]
    [InlineData("Intel(R) Core(TM) Ultra 5 236V", "ULTRA 5 236V")]
    [InlineData("Intel(R) Core(TM) Ultra 5 228V", "ULTRA 5 228V")]
    [InlineData("Intel(R) Core(TM) Ultra 7 265KF", "ULTRA 7 265KF")]
    [InlineData("Intel(R) Core(TM) Ultra 5 245KF", "ULTRA 5 245KF")]
    [InlineData("Intel(R) Core(TM) Ultra 9 285KF", "ULTRA 9 285KF")]
    [InlineData("Intel(R) Core(TM) Ultra 9 285T", "ULTRA 9 285T")]
    [InlineData("Intel(R) Core(TM) Ultra 7 265T", "ULTRA 7 265T")]
    [InlineData("Intel(R) Core(TM) Ultra 5 245T", "ULTRA 5 245T")]
    [InlineData("Intel(R) Core(TM) Ultra 5 235T", "ULTRA 5 235T")]
    [InlineData("Intel(R) Core(TM) Ultra 5 225T", "ULTRA 5 225T")]
    [InlineData("Intel(R) Core(TM) Ultra 9 285", "ULTRA 9 285")]
    [InlineData("Intel(R) Core(TM) Ultra 7 265", "ULTRA 7 265")]
    [InlineData("Intel(R) Core(TM) Ultra 7 265F", "ULTRA 7 265F")]
    [InlineData("Intel(R) Core(TM) Ultra 5 245", "ULTRA 5 245")]
    [InlineData("Intel(R) Core(TM) Ultra 5 235", "ULTRA 5 235")]
    [InlineData("Intel(R) Core(TM) Ultra 5 225", "ULTRA 5 225")]
    [InlineData("Intel(R) Core(TM) Ultra 5 225F", "ULTRA 5 225F")]
    [InlineData("Intel Core 9 270H", "9 270H")]
    [InlineData("Intel Core 7 250H", "7 250H")]
    [InlineData("Intel Core 7 240H", "7 240H")]
    [InlineData("Intel Core 5 220H", "5 220H")]
    [InlineData("Intel Core 5 210H", "5 210H")]
    [InlineData("Intel Core 7 230H", "7 230H")]
    [InlineData("Intel Core 5 205H", "5 205H")]
    [InlineData("Intel Core 7 250U", "7 250U")]
    [InlineData("Intel Core 5 220U", "5 220U")]
    [InlineData("AMD Ryzen 7 9800X3D", "RYZEN 7 9800X3D")]
    [InlineData("AMD Ryzen 9 7950X3D", "RYZEN 9 7950X3D")]
    [InlineData("AMD Ryzen 7 5800X3D", "RYZEN 7 5800X3D")]
    [InlineData("AMD Ryzen 7 5700X3D", "RYZEN 7 5700X3D")]
    [InlineData("AMD Ryzen 5 5600X3D", "RYZEN 5 5600X3D")]
    [InlineData("AMD Ryzen 5 5500X3D", "RYZEN 5 5500X3D")]
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
    [InlineData("AMD Ryzen 7 5700G", "RYZEN 7 5700G")]
    [InlineData("AMD Ryzen 5 5600G", "RYZEN 5 5600G")]
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

        Assert.Equal("1.74.0", database.Version);
        Assert.Equal("2026-11-16", database.Updated);
        Assert.Equal(304, database.CpuCount);
        Assert.Equal(109, database.GpuCount);
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
        // Dataset v1.4.0: the five Intel Arc A-series desktop GPUs, the
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

        // Dataset v1.53.0: the Arc A310, the last A-series desktop GPU
        // the dataset was missing, completing the set at five SKUs.
        // Intel never published a rated clock for the A310 (its launch
        // spec table listed the clock as TBD and Intel's own A-series
        // page lists no clock), so the 2.0 GHz figure is a judgement
        // call: every shipping board partner card runs at 2000 MHz
        // (ASRock, Sparkle and Gigabyte, per TechPowerUp's own
        // retail-board table), Intel's own launch announcement said
        // the six Xe-cores run at around 2000 MHz, and VideoCardz and
        // Notebookcheck both list 2000 MHz. TechPowerUp's
        // reference-design entry lists 1750 MHz, but no A310 was ever
        // sold at that clock, so the entry records 2.0 GHz. Board
        // power is Intel's typical 75 W, the same rule as the A380;
        // the single-slot reference design draws 30 W and retail
        // boards 50 W to 75 W. The source is Intel's own A-series
        // desktop page, which lists the A310 at 6 Xe-cores, 6 ray
        // tracing units, 4 GB, 124 GB/s, 52 TOPS INT8 and 4 displays.
        Assert.True(HardwareDatabase.Default.TryGetGpu("Intel(R) Arc(TM) A310 Graphics", out var a310));
        Assert.Equal("Intel", a310.Vendor);
        Assert.Equal(2.0, a310.BoostGhz);
        Assert.Equal(4, a310.MemoryGb);
        Assert.Equal(75, a310.TdpW);
        Assert.StartsWith("https://www.intel.com/content/www/us/en/products/details/discrete-gpus/arc/desktop/a-series.html", a310.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_Rx9070GreDesktopGpuCarriesVerifiedFigures()
    {
        // Dataset v1.59.0: the AMD Radeon RX 9070 GRE, the last
        // announced RX 9000 desktop part the dataset was missing, and
        // the fifth RX 9000 desktop SKU in the dataset (with the 9070
        // XT, 9070, 9060 XT and 9060). Every figure was verified by
        // hand against AMD's own product page, read 2026-11-15: RDNA 4
        // (Navi 48), 48 compute units, Boost Frequency up to 2790 MHz,
        // Game Frequency 2220 MHz, 12 GB GDDR6 on a 192-bit bus at up
        // to 432 GB/s, 48 MB Infinity Cache, 220 W typical board
        // power, launched in China May 8, 2025, global June 2, 2026
        // at $549. The figures are cross-checked against agreeing
        // third parties (Notebookcheck, cputronic and guru3d, which
        // all agree on 48 compute units, 2790 MHz boost, 12 GB,
        // 192-bit, 432 GB/s and 220 W). No judgement call was needed:
        // unlike the Arc A310 in v2.1.89, AMD publishes a rated boost
        // clock for the GRE, so the entry records AMD's own 2.79 GHz.
        // The dataset carries no compute-unit, bus-width or bandwidth
        // field, so those figures live here in the comment and in the
        // README and ROADMAP entries, not in the entry itself.
        Assert.True(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX 9070 GRE", out var rx9070Gre));
        Assert.Equal("AMD", rx9070Gre.Vendor);
        Assert.Equal(2.79, rx9070Gre.BoostGhz);
        Assert.Equal(12, rx9070Gre.MemoryGb);
        Assert.Equal(220, rx9070Gre.TdpW);
        Assert.StartsWith("https://www.amd.com/en/products/graphics/desktops/radeon/9000-series/amd-radeon-rx-9070-gre.html", rx9070Gre.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_Rx6000DesktopFamilyCompletesAt11()
    {
        // Dataset v1.61.0: the three AMD Radeon RX 6000 desktop GPUs
        // the dataset was missing, completing the RX 6000 desktop
        // family at eleven SKUs (the 6900 XT, 6800 XT, 6800, 6750 XT,
        // 6700 XT, 6700, 6650 XT, 6600 XT, 6600, 6500 XT and 6400).
        // Every figure was verified by hand against AMD's own product
        // pages, read 2026-11-15: the RX 6700 at Boost Frequency up
        // to 2450 MHz (Game Frequency 2174 MHz, Base Frequency
        // 1941 MHz), 36 compute units, 2304 stream processors, 10 GB
        // GDDR6 on a 160-bit bus at up to 320 GB/s, 80 MB Infinity
        // Cache, 175 W typical board power, RDNA 2 (Navi 22), launched
        // June 9, 2022; the RX 6500 XT at Boost Frequency up to
        // 2815 MHz (Game Frequency 2650 MHz on the 8 GB model,
        // 2610 MHz on the 4 GB launch model), 16 compute units, 1024
        // stream processors, GDDR6 on a 64-bit bus at up to 144 GB/s,
        // 16 MB Infinity Cache, RDNA 2 (Navi 24, TSMC 6nm), launched
        // January 19, 2022 at $199; the RX 6400 at Boost Frequency up
        // to 2321 MHz (Game Frequency 2039 MHz), 12 compute units,
        // 768 stream processors, 4 GB GDDR6 on a 64-bit bus at up to
        // 128 GB/s, 16 MB Infinity Cache, 53 W typical board power,
        // PCIe powered with no extra connector, RDNA 2 (Navi 24),
        // launched January 19, 2022. Cross-checked against agreeing
        // third parties (TechPowerUp, KitGuru, VideoCardz and the
        // launch coverage, which all agree per SKU on the boost clock,
        // the memory size and the board power, except for the 6500 XT
        // board-power split recorded below). Two reading traps are
        // recorded: AMD's own RX 6500 XT page now lists Typical Board
        // Power 113 W and Max Memory Size 8 GB, the later 8 GB model,
        // while the 4 GB launch model is 107 W and 4 GB, so with two
        // memory sizes and two board-power figures sharing the device
        // name the entry records no single memory or board-power
        // figure, the same null treatment as the RX 9060 XT in
        // v1.59.0; and AMD's own RX 6700 page prints a launch date of
        // 6/9/2021, while the card launched June 9, 2022, so the page
        // date is a typo and the dataset records no launch date for
        // it. The Sapphire custom RX 6700 cards are factory-overclocked
        // (2495 MHz boost, over 220 W board power), so the entry
        // records AMD's own reference figures (2450 MHz, 175 W). The
        // dataset carries no compute-unit, bus-width or bandwidth
        // field, so those figures live here in the comment and in the
        // README and ROADMAP entries, not in the entries themselves.
        Assert.True(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX 6700", out var rx6700));
        Assert.Equal("AMD", rx6700.Vendor);
        Assert.Equal(2.45, rx6700.BoostGhz);
        Assert.Equal(10, rx6700.MemoryGb);
        Assert.Equal(175, rx6700.TdpW);
        Assert.StartsWith("https://www.amd.com/en/products/graphics/desktops/radeon/6000-series/amd-radeon-rx-6700.html", rx6700.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX 6500 XT", out var rx6500Xt));
        Assert.Equal("AMD", rx6500Xt.Vendor);
        Assert.Equal(2.815, rx6500Xt.BoostGhz);
        Assert.Null(rx6500Xt.MemoryGb);
        Assert.Null(rx6500Xt.TdpW);
        Assert.Equal("TBP 107 W (4 GB launch model) / 113 W (8 GB model); no single figure for the shared device name", rx6500Xt.TdpNote);
        Assert.StartsWith("https://www.amd.com/en/products/graphics/desktops/radeon/6000-series/amd-radeon-rx-6500-xt.html", rx6500Xt.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX 6400", out var rx6400));
        Assert.Equal("AMD", rx6400.Vendor);
        Assert.Equal(2.321, rx6400.BoostGhz);
        Assert.Equal(4, rx6400.MemoryGb);
        Assert.Equal(53, rx6400.TdpW);
        Assert.StartsWith("https://www.amd.com/en/products/graphics/desktops/radeon/6000-series/amd-radeon-rx-6400.html", rx6400.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_Rtx5090DDesktopGpuCarriesVerifiedFigures()
    {
        // Dataset v1.62.0: the NVIDIA GeForce RTX 5090 D desktop GPU,
        // the eighth RTX 50 desktop SKU in the dataset (with the 5090,
        // 5080, 5070 Ti, 5070, 5060 Ti, 5060 and 5050) and the
        // China-only variant of the 5090, named as a future candidate
        // by the v2.1.97 audit. Blackwell (GB202, GB202-250-A1), 21,760
        // CUDA cores, 680 tensor cores, 170 ray tracing cores, base
        // clock 2017 MHz, Boost Clock up to 2407 MHz, 32 GB GDDR7 on a
        // 512-bit bus at up to 1.79 TB/s in the original model, 575 W
        // total graphics power, launched January 30, 2025 at CNY
        // 16,499. The V2, launched August 12, 2025, ships 24 GB GDDR7
        // on a 384-bit bus at the same 2407 MHz boost and 575 W, so
        // with two memory sizes sharing the device name the entry
        // records no single memory figure, the same null treatment as
        // the RX 6500 XT in v1.61.0 and the RX 9060 XT in v1.59.0, and
        // the split is recorded in the entry note. The entry records
        // the boost and the board power because both are the same on
        // the two variants. Every figure was verified by hand, read
        // 2026-11-15: NVIDIA's own 5090 D page is the China site and
        // its exact URL did not surface in the search index, so the
        // entry records the TechPowerUp GPU database page, fetched
        // directly as page text (21,760 shading units, 32 GB GDDR7,
        // 512-bit, base 2017 MHz, boost up to 2407 MHz, TDP 575 W,
        // launched January 30, 2025), cross-checked against agreeing
        // third parties (VideoCardz, technical.city, cputronic and
        // gpuspecs, which all agree on 2407 MHz boost, 32 GB and
        // 575 W for the original), the same disclosed third-party
        // exception as the 285T, 245T, 235T and 225T in v2.1.96,
        // because NVIDIA's own compare pages for the D variant are
        // China-only. Two reading traps are recorded: the V2 is a
        // separate product, not a revision of the original, so its
        // 24 GB and 384-bit figures must not be read as the 5090 D
        // figures; and board-partner cards are factory-overclocked
        // (up to 2655 MHz boost on the GALAX HOF OC LAB XOC and the
        // GIGABYTE AORUS MASTER), so the entry records NVIDIA's
        // reference boost of 2407 MHz. The stored key is "RTX 5090 D"
        // with a space, because that is what Windows reports ("NVIDIA
        // GeForce RTX 5090 D"), which the normalizer reduces to the
        // same key. The dataset carries no CUDA-core, bus-width or
        // bandwidth field, so those figures live here in the comment
        // and in the README and ROADMAP entries, not in the entry
        // itself.
        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 5090 D", out var rtx5090D));
        Assert.Equal("NVIDIA", rtx5090D.Vendor);
        Assert.Equal(2.407, rtx5090D.BoostGhz);
        Assert.Null(rtx5090D.MemoryGb);
        Assert.Equal(575, rtx5090D.TdpW);
        Assert.Equal("Memory 32 GB (original, Jan 2025) / 24 GB (V2, Aug 2025); no single figure for the shared device name", rtx5090D.TdpNote);
        Assert.StartsWith("http://techpowerup.com/gpu-specs/geforce-rtx-5090-d.c4253", rtx5090D.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_Gtx10DesktopGpusCarryVerifiedFigures()
    {
        // Dataset v1.64.0: the four NVIDIA GeForce GTX 10 desktop GPUs
        // the dataset was missing, bringing the GTX 10 desktop family
        // to seven of eight SKUs (with the 1050 Ti, 1060 6GB and 1080).
        // The GeForce GTX 1080 Ti at Boost Clock up to 1582 MHz
        // (Base Clock 1481 MHz per TechPowerUp, 1480 MHz per NVIDIA's
        // own launch material), 3584 CUDA cores, 11 GB GDDR5X on a
        // 352-bit bus at up to 484 GB/s, 250 W total graphics power,
        // Pascal (GP102), launched March 10, 2017 at $699. The GeForce
        // GTX 1070 Ti at 1683 MHz, 2432 CUDA cores, 8 GB GDDR5 on a
        // 256-bit bus at up to 256 GB/s, 180 W, Pascal (GP104),
        // launched November 2, 2017 at $449. The GeForce GTX 1070 at
        // 1683 MHz, 1920 CUDA cores, 8 GB GDDR5 on a 256-bit bus at
        // up to 256 GB/s, 150 W, Pascal (GP104), launched June 10,
        // 2016 at $379. The GeForce GTX 1060 3GB at 1708 MHz, 1152
        // CUDA cores, 3 GB GDDR5 on a 192-bit bus at up to 192 GB/s,
        // 120 W, Pascal (GP106; a GP104 variant of the 3GB card also
        // shipped), launched August 18, 2016 at $199. Every figure was
        // verified by hand, read 2026-11-15: NVIDIA's own GTX 10
        // product pages are gone (the 1080 Ti page now redirects to
        // the generic RTX 50 Series page), so the entries record the
        // TechPowerUp GPU database pages, the 1080 Ti page fetched
        // directly as page text, cross-checked against agreeing third
        // parties (Notebookcheck, technical.city, nanoreview, pcbench
        // and siliconcat, which all agree per SKU on the boost clock,
        // the memory size and the board power), the same disclosed
        // third-party exception as the RTX 5090 D in v2.1.98. Four
        // reading traps are recorded: TechPowerUp lists the 1080 Ti
        // base clock as 1481 MHz while NVIDIA's own launch material
        // lists 1480 MHz, and the dataset records no GPU base clock,
        // so it does not affect the entries; the 1070 Ti and the 1070
        // share the same 1683 MHz boost, and differ in CUDA cores and
        // board power, so the boost must be read per SKU and never
        // inferred from the tier; TechPowerUp lists the 1070 Ti launch
        // price as $399, while NVIDIA's own price history and the
        // launch coverage give $449 for the Founders Edition, so the
        // $399 figure is the GTX 1070 Founders Edition price, not the
        // 1070 Ti; and board-partner cards are factory-overclocked,
        // so the entries record NVIDIA's reference boost figures. The
        // dataset carries no CUDA-core, bus-width or bandwidth field,
        // so those figures live here in the comment and in the README
        // and ROADMAP entries, not in the entries themselves.
        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce GTX 1080 Ti", out var gtx1080Ti));
        Assert.Equal("NVIDIA", gtx1080Ti.Vendor);
        Assert.Equal(1.582, gtx1080Ti.BoostGhz);
        Assert.Equal(11, gtx1080Ti.MemoryGb);
        Assert.Equal(250, gtx1080Ti.TdpW);
        Assert.Null(gtx1080Ti.TdpNote);
        Assert.StartsWith("https://www.techpowerup.com/gpu-specs/geforce-gtx-1080-ti.c2877", gtx1080Ti.SourceUrl, StringComparison.Ordinal);
        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce GTX 1070 Ti", out var gtx1070Ti));
        Assert.Equal("NVIDIA", gtx1070Ti.Vendor);
        Assert.Equal(1.683, gtx1070Ti.BoostGhz);
        Assert.Equal(8, gtx1070Ti.MemoryGb);
        Assert.Equal(180, gtx1070Ti.TdpW);
        Assert.Null(gtx1070Ti.TdpNote);
        Assert.StartsWith("https://www.techpowerup.com/gpu-specs/geforce-gtx-1070-ti.c3010", gtx1070Ti.SourceUrl, StringComparison.Ordinal);
        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce GTX 1070", out var gtx1070));
        Assert.Equal("NVIDIA", gtx1070.Vendor);
        Assert.Equal(1.683, gtx1070.BoostGhz);
        Assert.Equal(8, gtx1070.MemoryGb);
        Assert.Equal(150, gtx1070.TdpW);
        Assert.Null(gtx1070.TdpNote);
        Assert.StartsWith("https://www.techpowerup.com/gpu-specs/geforce-gtx-1070.c2840", gtx1070.SourceUrl, StringComparison.Ordinal);
        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce GTX 1060 3GB", out var gtx1060_3gb));
        Assert.Equal("NVIDIA", gtx1060_3gb.Vendor);
        Assert.Equal(1.708, gtx1060_3gb.BoostGhz);
        Assert.Equal(3, gtx1060_3gb.MemoryGb);
        Assert.Equal(120, gtx1060_3gb.TdpW);
        Assert.Null(gtx1060_3gb.TdpNote);
        Assert.StartsWith("https://www.techpowerup.com/gpudb/2867/geforce-gtx-1060-3-gb", gtx1060_3gb.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_Rx6950XtDesktopGpuCarriesVerifiedFigures()
    {
        // Dataset v1.65.0: the one AMD Radeon RX 6950 XT desktop GPU
        // the dataset was missing, completing the RX 6000 desktop
        // family at twelve SKUs (the 6400, 6500 XT, 6600, 6600 XT,
        // 6650 XT, 6700, 6700 XT, 6750 XT, 6800, 6800 XT, 6900 XT and
        // 6950 XT; the RX 6750 GRE stays out, declined in v2.1.98
        // because its two variants differ in boost as well as memory
        // and power). The Radeon RX 6950 XT at Boost Frequency up to
        // 2310 MHz (Base Frequency 1890 MHz, Game Frequency 2100 MHz),
        // 80 compute units, 5120 stream processors, 16 GB GDDR6 on a
        // 256-bit bus at up to 576 GB/s, 128 MB Infinity Cache, 335 W
        // typical board power, RDNA 2 (Navi 21 KXTX), launched May 10,
        // 2022 at $1099 per the launch coverage. Every figure was
        // verified by hand against AMD's own product page, fetched
        // directly as page text and read 2026-11-15, cross-checked
        // against agreeing third parties (TechPowerUp, siliconcat,
        // pcbench and hardwaredb, which all agree on 2310 MHz boost,
        // 16 GB and 335 W). Two reading traps are recorded:
        // TechPowerUp lists the base clock as 1860 MHz while AMD's own
        // page lists 1890 MHz, and the dataset records no GPU base
        // clock, so it does not affect the entry; and AMD's page lists
        // a minimum PSU recommendation of 850 W while TechPowerUp
        // suggests 700 W, and the dataset records no PSU figure, so
        // that disagreement does not affect the entry either.
        // Board-partner cards are factory-overclocked, so the entry
        // records AMD's reference boost figure. The dataset carries no
        // compute-unit, bus-width or bandwidth field, so those figures
        // live here in the comment and in the README and ROADMAP
        // entries, not in the entry itself.
        Assert.True(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX 6950 XT", out var rx6950Xt));
        Assert.Equal("AMD", rx6950Xt.Vendor);
        Assert.Equal(2.31, rx6950Xt.BoostGhz);
        Assert.Equal(16, rx6950Xt.MemoryGb);
        Assert.Equal(335, rx6950Xt.TdpW);
        Assert.Null(rx6950Xt.TdpNote);
        Assert.StartsWith("https://www.amd.com/en/products/graphics/desktops/radeon/6000-series/amd-radeon-rx-6950-xt.html", rx6950Xt.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_Rx7650GreDesktopGpuCarriesVerifiedFigures()
    {
        // Dataset v1.66.0: the one AMD Radeon RX 7650 GRE desktop GPU
        // the dataset was missing, completing the RX 7000 desktop
        // family at eight SKUs (the 7600, 7600 XT, 7650 GRE, 7700 XT,
        // 7800 XT, 7900 GRE, 7900 XT and 7900 XTX). The Radeon RX 7650
        // GRE at Boost Frequency up to 2695 MHz (Base Frequency 1720
        // MHz, Game Frequency 2350 MHz), 32 compute units, 2048 stream
        // processors, 8 GB GDDR6 on a 128-bit bus at up to 288 GB/s,
        // 32 MB Infinity Cache, 165 W typical board power, RDNA 3
        // (Navi 33 XL), launched February 2025 in China at $279 per
        // TechPowerUp. Every figure was verified by hand, read
        // 2026-11-15: AMD's own product page is the China site and its
        // exact URL did not surface, so the entry records the
        // TechPowerUp GPU database page, fetched directly as page text,
        // cross-checked against agreeing third parties (technical.city,
        // Tom's Hardware and TweakTown, which all agree on 2695 MHz
        // boost, 8 GB and 165 W), the same disclosed third-party
        // exception as the RTX 5090 D in v2.1.98 and the GTX 10 set in
        // v2.2.0. Two reading traps are recorded: siliconcat alone
        // lists the boost as 2690 MHz while TechPowerUp, technical.city,
        // Tom's Hardware and TweakTown all list 2695 MHz, so the four
        // agreeing sources decide it; and board-partner cards are
        // factory-overclocked (up to 2810 MHz boost on the ASRock
        // Challenger OC+ and the Sapphire Black Edition), so the entry
        // records AMD's reference boost figure. The dataset carries no
        // compute-unit, bus-width or bandwidth field, so those figures
        // live here in the comment and in the README and ROADMAP
        // entries, not in the entry itself.
        Assert.True(HardwareDatabase.Default.TryGetGpu("AMD Radeon RX 7650 GRE", out var rx7650Gre));
        Assert.Equal("AMD", rx7650Gre.Vendor);
        Assert.Equal(2.695, rx7650Gre.BoostGhz);
        Assert.Equal(8, rx7650Gre.MemoryGb);
        Assert.Equal(165, rx7650Gre.TdpW);
        Assert.Null(rx7650Gre.TdpNote);
        Assert.StartsWith("https://www.techpowerup.com/gpu-specs/radeon-rx-7650-gre.c4258", rx7650Gre.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_Rtx30SeriesLaptopGpusCompleteAtEight()
    {
        // Dataset v1.67.0: the two NVIDIA GeForce RTX 30-series laptop
        // GPUs the dataset was missing, completing the RTX 30-series
        // laptop family at eight SKUs (the 3080 Ti, 3080, 3070 Ti,
        // 3070, 3060, 3050 Ti, 3050 and 3050 6GB Laptop GPUs). The RTX
        // 3080 Ti Laptop GPU at Boost Clock up to 1590 MHz, 7424 CUDA
        // cores, 16 GB GDDR6 on a 256-bit bus, 150 W maximum TGP,
        // Ampere (GA103S), launched January 2022. The RTX 3050 Ti
        // Laptop GPU at Boost Clock up to 1695 MHz, 2560 CUDA cores,
        // 4 GB GDDR6 on a 128-bit bus, 80 W maximum TGP, Ampere
        // (GA107), launched May 2021. Every figure was verified by
        // hand, read 2026-11-15: NVIDIA's own GeForce RTX 30 Series
        // laptop compare page, fetched directly as page text (3080 Ti
        // at Boost Clock 1125-1590 MHz, GPU Subsystem Power 80-150 W,
        // Standard Memory Config 16 GB GDDR6; 3050 Ti at Boost Clock
        // 1035-1695 MHz, GPU Subsystem Power 35-80 W, Standard Memory
        // Config 4 GB GDDR6), cross-checked against agreeing third
        // parties (Notebookcheck, nanoreview, Guru3D, WCCFTech and
        // KitGuru, which all agree per SKU on the boost clock, the
        // memory size and the TGP range). Boost is the upper end of
        // NVIDIA's published Boost Clock range and board power is the
        // maximum of the published TGP range, per the dataset's own
        // definitions, exactly like the RTX 50/40-series laptop
        // entries. One reading trap is recorded: some third-party
        // tables list the 3080 Ti Laptop GPU TGP up to 175 W with
        // Dynamic Boost, while NVIDIA's own compare page lists 80-150
        // W, and the entry records the NVIDIA figure per the dataset's
        // convention, the same as the RTX 3080 Laptop GPU entry. The
        // dataset carries no CUDA-core, bus-width or launch-date field,
        // so those figures live here in the comment and in the README
        // and ROADMAP entries, not in the entries themselves.
        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 3080 Ti Laptop GPU", out var rtx3080TiLaptop));
        Assert.Equal("NVIDIA", rtx3080TiLaptop.Vendor);
        Assert.Equal(1.59, rtx3080TiLaptop.BoostGhz);
        Assert.Equal(16, rtx3080TiLaptop.MemoryGb);
        Assert.Equal(150, rtx3080TiLaptop.TdpW);
        Assert.Equal("TGP range 80-150 W; figure is the maximum", rtx3080TiLaptop.TdpNote);
        Assert.StartsWith("https://www.nvidia.com/en-in/geforce/laptops/compare/30-series/", rtx3080TiLaptop.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetGpu("NVIDIA GeForce RTX 3050 Ti Laptop GPU", out var rtx3050TiLaptop));
        Assert.Equal("NVIDIA", rtx3050TiLaptop.Vendor);
        Assert.Equal(1.695, rtx3050TiLaptop.BoostGhz);
        Assert.Equal(4, rtx3050TiLaptop.MemoryGb);
        Assert.Equal(80, rtx3050TiLaptop.TdpW);
        Assert.Equal("TGP range 35-80 W; figure is the maximum", rtx3050TiLaptop.TdpNote);
        Assert.StartsWith("https://www.nvidia.com/en-in/geforce/laptops/compare/30-series/", rtx3050TiLaptop.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_Core14thGenUnlockedDesktopFamilyCompletesAt7()
    {
        // Dataset v1.63.0: the five Intel Core 14th-gen unlocked desktop
        // CPUs the dataset was missing, completing the unlocked set at
        // seven SKUs (the 14900KS, 14900K, 14900KF, 14700K, 14700KF,
        // 14600K and 14600KF; the 14700K and 14900K were already listed).
        // All five are Raptor Lake Refresh on Intel 7, LGA 1700. The
        // 14900KS at Performance-core Base Frequency 3.2 GHz and Max
        // Turbo Frequency up to 6.2 GHz with Efficient-core Max Turbo
        // Frequency 4.5 GHz, 24 cores (8 P-cores and 16 E-cores,
        // 32 threads, 36 MB Intel Smart Cache), 150 W base power,
        // launched March 2024. The 14900KF at 3.2 and up to 6.0 GHz
        // with E-core turbo 4.4 GHz, 24 cores, 36 MB cache, 125 W base
        // power, no integrated graphics, launched October 2023. The
        // 14700KF at 3.4 and up to 5.6 GHz with E-core turbo 4.3 GHz,
        // 20 cores (8 P-cores and 12 E-cores, 28 threads, 33 MB cache),
        // 125 W base power, no integrated graphics, launched October
        // 2023. The 14600K at 3.5 and up to 5.3 GHz with E-core turbo
        // 4.0 GHz, 14 cores (6 P-cores and 8 E-cores, 20 threads,
        // 24 MB cache), 125 W base power, launched October 2023. The
        // 14600KF at the same clocks, cores, cache and power as the
        // 14600K, no integrated graphics, launched October 2023. The
        // boost figures are ARK Max Turbo Frequency, the headline up-to
        // figure, per the dataset boost definition. Every figure was
        // verified by hand, read 2026-11-15: the 14900KS against
        // Intel's own ARK spec table as republished by agreeing
        // retailers (ARK ID 237504; Max Turbo 6.2 GHz, Thermal Velocity
        // Boost 6.2 GHz, Turbo Boost Max 3.0 5.9 GHz, P-core Max Turbo
        // 5.7 GHz, E-core Max Turbo 4.5 GHz, P-core base 3.2 GHz, 36 MB,
        // 150 W) and the 14900KF, 14700KF, 14600K and 14600KF against
        // Intel's own 14th-gen comparison table as republished by
        // Newegg (Max Turbo 6.0, 5.6 and 5.3 GHz; P-core base 3.2, 3.4
        // and 3.5 GHz; E-core turbo 4.4, 4.3 and 4.0 GHz), cross-checked
        // against agreeing third parties (nanoreview, digit, wccftech
        // and the AnandTech table, which all agree per SKU on the base,
        // the boost and the E-core turbo). Intel's own compare pages
        // return HTTP 403 in this sandbox, so the figures rest on
        // agreeing ARK mirrors, the same disclosed third-party
        // exception as the 285T, 245T, 235T and 225T in v2.1.96, and
        // each entry records Intel's own compare URL with the five
        // verified ARK IDs (237504, 236787, 236789, 236799, 236778).
        // Three reading traps are recorded: the 14900KS 6.2 GHz is
        // Thermal Velocity Boost, not the P-core Max Turbo (5.7 GHz),
        // and the dataset records the ARK Max Turbo headline, the same
        // convention as the 14900K 6.0 GHz; some retailers list the
        // 14900KF 5.8 GHz Turbo Boost Max 3.0 figure as its maximum
        // turbo, while the ARK Max Turbo is 6.0 GHz; and Newegg's
        // comparison table lists the i7-14700K/KF cache as 30 MB, while
        // the L3 cache is 33 MB (28 MB L2) per the ARK spec tables, so
        // the comment records 33 MB. The dataset carries no core-count,
        // cache or power field, so those figures live here in the
        // comment and in the README and ROADMAP entries, not in the
        // entries themselves.
        var compareUrl = "https://www.intel.com/content/www/us/en/products/compare.html?productIds=237504,236787,236789,236799,236778";
        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i9-14900KS", out var i9_14900Ks));
        Assert.Equal("Intel", i9_14900Ks.Vendor);
        Assert.Equal(3.2, i9_14900Ks.BaseGhz);
        Assert.Equal(6.2, i9_14900Ks.BoostGhz);
        Assert.Equal(4.5, i9_14900Ks.ECoreBoostGhz);
        Assert.Equal(compareUrl, i9_14900Ks.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i9-14900KF", out var i9_14900Kf));
        Assert.Equal("Intel", i9_14900Kf.Vendor);
        Assert.Equal(3.2, i9_14900Kf.BaseGhz);
        Assert.Equal(6.0, i9_14900Kf.BoostGhz);
        Assert.Equal(4.4, i9_14900Kf.ECoreBoostGhz);
        Assert.Equal(compareUrl, i9_14900Kf.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i7-14700KF", out var i7_14700Kf));
        Assert.Equal("Intel", i7_14700Kf.Vendor);
        Assert.Equal(3.4, i7_14700Kf.BaseGhz);
        Assert.Equal(5.6, i7_14700Kf.BoostGhz);
        Assert.Equal(4.3, i7_14700Kf.ECoreBoostGhz);
        Assert.Equal(compareUrl, i7_14700Kf.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i5-14600K", out var i5_14600K));
        Assert.Equal("Intel", i5_14600K.Vendor);
        Assert.Equal(3.5, i5_14600K.BaseGhz);
        Assert.Equal(5.3, i5_14600K.BoostGhz);
        Assert.Equal(4.0, i5_14600K.ECoreBoostGhz);
        Assert.Equal(compareUrl, i5_14600K.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i5-14600KF", out var i5_14600Kf));
        Assert.Equal("Intel", i5_14600Kf.Vendor);
        Assert.Equal(3.5, i5_14600Kf.BaseGhz);
        Assert.Equal(5.3, i5_14600Kf.BoostGhz);
        Assert.Equal(4.0, i5_14600Kf.ECoreBoostGhz);
        Assert.Equal(compareUrl, i5_14600Kf.SourceUrl);
    }

    [Fact]
    public void Default_Core13thGenUnlockedDesktopFamilyCompletesAt7()
    {
        // Dataset v1.68.0: the five Intel Core 13th-gen unlocked desktop
        // CPUs the dataset was missing, completing the unlocked set at
        // seven SKUs (the 13900KS, 13900K, 13900KF, 13700K, 13700KF,
        // 13600K and 13600KF; the 13900K and 13700K were already listed).
        // All five are Raptor Lake on Intel 7, LGA 1700. The 13900KS at
        // Performance-core Base Frequency 3.2 GHz and Max Turbo
        // Frequency up to 6.0 GHz with Efficient-core Max Turbo
        // Frequency 4.3 GHz, 24 cores (8 P-cores and 16 E-cores,
        // 32 threads, 36 MB Intel Smart Cache), 150 W base power,
        // launched January 2023. The 13900KF at 3.0 and up to 5.8 GHz
        // with E-core turbo 4.3 GHz, 24 cores, 36 MB cache, 125 W base
        // power, no integrated graphics, launched October 2022. The
        // 13700KF at 3.4 and up to 5.4 GHz with E-core turbo 4.2 GHz,
        // 16 cores (8 P-cores and 8 E-cores, 24 threads, 30 MB cache),
        // 125 W base power, no integrated graphics, launched October
        // 2022. The 13600K at 3.5 and up to 5.1 GHz with E-core turbo
        // 3.9 GHz, 14 cores (6 P-cores and 8 E-cores, 20 threads,
        // 24 MB cache), 125 W base power, launched October 2022. The
        // 13600KF at the same clocks, cores, cache and power as the
        // 13600K, no integrated graphics, launched October 2022. The
        // boost figures are ARK Max Turbo Frequency, the headline up-to
        // figure, per the dataset boost definition. Every figure was
        // verified by hand, read 2026-11-15: Intel's own ARK spec
        // tables as republished by agreeing retailers and comparison
        // tables (ARK IDs 232167, 230497, 230489, 230493, 230494;
        // 13900KS Max Turbo 6.0 GHz, Thermal Velocity Boost 6.0 GHz,
        // Turbo Boost Max 3.0 5.8 GHz, P-core Max Turbo 5.4 GHz,
        // E-core Max Turbo 4.3 GHz, P-core base 3.2 GHz, 36 MB,
        // 150 W; 13900KF Max Turbo 5.8 GHz, Thermal Velocity Boost
        // 5.8 GHz, Turbo Boost Max 3.0 5.7 GHz, P-core Max Turbo
        // 5.4 GHz, E-core Max Turbo 4.3 GHz, P-core base 3.0 GHz,
        // 36 MB, 125 W; 13700KF Max Turbo 5.4 GHz, Turbo Boost Max
        // 3.0 5.4 GHz, P-core Max Turbo 5.3 GHz, E-core Max Turbo
        // 4.2 GHz, P-core base 3.4 GHz, 30 MB, 125 W; 13600K Max
        // Turbo 5.1 GHz, P-core Max Turbo 5.1 GHz, E-core Max Turbo
        // 3.9 GHz, P-core base 3.5 GHz, 24 MB, 125 W; 13600KF the
        // same clocks as the 13600K, 24 MB, 125 W), cross-checked
        // against agreeing third parties (cpu-monkey, cpu-world,
        // Notebookcheck, TechPowerUp, Overclockers and WikiChip,
        // which all agree per SKU on the base, the boost and the
        // E-core turbo). Intel's own compare pages return HTTP 403
        // in this sandbox, so the figures rest on agreeing ARK
        // mirrors, the same disclosed third-party exception as the
        // 14th-gen unlocked set in v2.1.99, and each entry records
        // Intel's own compare URL with the five verified ARK IDs
        // (232167, 230497, 230489, 230493, 230494). Three reading
        // traps are recorded: the 13900KS 6.0 GHz is Thermal Velocity
        // Boost, not the P-core Max Turbo (5.4 GHz), and the dataset
        // records the ARK Max Turbo headline, the same convention as
        // the 14900KS 6.2 GHz; the 13900KF 5.8 GHz is likewise
        // Thermal Velocity Boost, its P-core Max Turbo is 5.4 GHz
        // and some retailers list its 5.7 GHz Turbo Boost Max 3.0
        // figure as its maximum turbo, while the ARK Max Turbo is
        // 5.8 GHz; and one retailer lists the 13600K boost as
        // 5.3 GHz, which is the 14600K's figure, while the agreeing
        // ARK mirrors give 5.1 GHz. The dataset carries no
        // core-count, cache or power field, so those figures live
        // here in the comment and in the README and ROADMAP entries,
        // not in the entries themselves.
        var compareUrl = "https://www.intel.com/content/www/us/en/products/compare.html?productIds=232167,230497,230489,230493,230494";
        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i9-13900KS", out var i9_13900Ks));
        Assert.Equal("Intel", i9_13900Ks.Vendor);
        Assert.Equal(3.2, i9_13900Ks.BaseGhz);
        Assert.Equal(6.0, i9_13900Ks.BoostGhz);
        Assert.Equal(4.3, i9_13900Ks.ECoreBoostGhz);
        Assert.Equal(compareUrl, i9_13900Ks.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i9-13900KF", out var i9_13900Kf));
        Assert.Equal("Intel", i9_13900Kf.Vendor);
        Assert.Equal(3.0, i9_13900Kf.BaseGhz);
        Assert.Equal(5.8, i9_13900Kf.BoostGhz);
        Assert.Equal(4.3, i9_13900Kf.ECoreBoostGhz);
        Assert.Equal(compareUrl, i9_13900Kf.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i7-13700KF", out var i7_13700Kf));
        Assert.Equal("Intel", i7_13700Kf.Vendor);
        Assert.Equal(3.4, i7_13700Kf.BaseGhz);
        Assert.Equal(5.4, i7_13700Kf.BoostGhz);
        Assert.Equal(4.2, i7_13700Kf.ECoreBoostGhz);
        Assert.Equal(compareUrl, i7_13700Kf.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i5-13600K", out var i5_13600K));
        Assert.Equal("Intel", i5_13600K.Vendor);
        Assert.Equal(3.5, i5_13600K.BaseGhz);
        Assert.Equal(5.1, i5_13600K.BoostGhz);
        Assert.Equal(3.9, i5_13600K.ECoreBoostGhz);
        Assert.Equal(compareUrl, i5_13600K.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i5-13600KF", out var i5_13600Kf));
        Assert.Equal("Intel", i5_13600Kf.Vendor);
        Assert.Equal(3.5, i5_13600Kf.BaseGhz);
        Assert.Equal(5.1, i5_13600Kf.BoostGhz);
        Assert.Equal(3.9, i5_13600Kf.ECoreBoostGhz);
        Assert.Equal(compareUrl, i5_13600Kf.SourceUrl);
    }

    [Fact]
    public void Default_Core12thGenUnlockedDesktopFamilyCompletesAt6()
    {
        // Dataset v1.69.0: the three Intel Core 12th-gen unlocked desktop
        // KF CPUs the dataset was missing, completing the unlocked set at
        // six SKUs (the 12900K, 12700K, 12600K, 12900KF, 12700KF and
        // 12600KF; the three K parts were already listed). All three are
        // Alder Lake on Intel 7, LGA 1700, and each KF part shares its K
        // twin's clocks exactly and differs only by the missing integrated
        // graphics, so a discrete card is required. The 12900KF at
        // Performance-core Base Frequency 3.2 GHz and Max Turbo
        // Frequency up to 5.2 GHz with Efficient-core Max Turbo
        // Frequency 3.9 GHz, 16 cores (8 P-cores and 8 E-cores,
        // 24 threads, 30 MB Intel Smart Cache), 125 W base power,
        // launched November 2021. The 12700KF at 3.6 and up to 5.0 GHz
        // with E-core turbo 3.8 GHz, 12 cores (8 P-cores and 4 E-cores,
        // 20 threads, 25 MB cache), 125 W base power, no integrated
        // graphics, launched November 2021. The 12600KF at 3.7 and up
        // to 4.9 GHz with E-core turbo 3.6 GHz, 10 cores (6 P-cores
        // and 4 E-cores, 16 threads, 20 MB cache), 125 W base power,
        // no integrated graphics, launched November 2021. The boost
        // figures are ARK Max Turbo Frequency, the headline up-to
        // figure, per the dataset boost definition. Every figure was
        // verified by hand, read 2026-11-15: Intel's own 12th-gen
        // comparison table as republished by ASRock (12900KF 3.2 GHz,
        // Max Turbo 5.2 GHz, E-core turbo 3.9 GHz, 16 cores, 24 threads,
        // 30 MB, 125 W; 12700KF 3.6 GHz, Max Turbo 5.0 GHz, E-core turbo
        // 3.8 GHz, 12 cores, 20 threads, 25 MB, 125 W; 12600KF 3.7 GHz,
        // Max Turbo 4.9 GHz, E-core turbo 3.6 GHz, 10 cores, 16 threads,
        // 20 MB, 125 W), cross-checked against agreeing third parties
        // (cpu-world, TechPowerUp, Newegg, B and H, Overclockers and
        // siliconcat, which all agree per SKU on the base, the boost and
        // the E-core turbo). Intel's own compare pages return HTTP 403
        // in this sandbox, so the figures rest on agreeing ARK mirrors,
        // the same disclosed third-party exception as the 14th-gen
        // unlocked set in v2.1.99 and the 13th-gen unlocked set in
        // v2.2.4, and each entry records Intel's own compare URL with
        // the three verified ARK IDs (134600, 134595, 134590). Three
        // reading traps are recorded: pre-launch leak tables listed the
        // 12900K boost as 5.3 GHz, while the final ARK Max Turbo is
        // 5.2 GHz, the same figure the KF twin carries; some retailers
        // list the 12700KF turbo as 4.9 GHz, which is its Single P-core
        // Turbo, while the ARK Max Turbo is 5.0 GHz via Turbo Boost Max
        // 3.0, the same trap class as the 13900KF's 5.7 GHz listing; and
        // one retailer lists the 12600KF cache as 25 MB, which is the
        // 12700KF's cache, while the agreeing sources give 20 MB. The
        // dataset carries no core-count, cache or power field, so those
        // figures live here in the comment and in the README and ROADMAP
        // entries, not in the entries themselves.
        var compareUrl = "https://www.intel.com/content/www/us/en/products/compare.html?productIds=134600,134595,134590";
        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i9-12900KF", out var i9_12900Kf));
        Assert.Equal("Intel", i9_12900Kf.Vendor);
        Assert.Equal(3.2, i9_12900Kf.BaseGhz);
        Assert.Equal(5.2, i9_12900Kf.BoostGhz);
        Assert.Equal(3.9, i9_12900Kf.ECoreBoostGhz);
        Assert.Equal(compareUrl, i9_12900Kf.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i7-12700KF", out var i7_12700Kf));
        Assert.Equal("Intel", i7_12700Kf.Vendor);
        Assert.Equal(3.6, i7_12700Kf.BaseGhz);
        Assert.Equal(5.0, i7_12700Kf.BoostGhz);
        Assert.Equal(3.8, i7_12700Kf.ECoreBoostGhz);
        Assert.Equal(compareUrl, i7_12700Kf.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i5-12600KF", out var i5_12600Kf));
        Assert.Equal("Intel", i5_12600Kf.Vendor);
        Assert.Equal(3.7, i5_12600Kf.BaseGhz);
        Assert.Equal(4.9, i5_12600Kf.BoostGhz);
        Assert.Equal(3.6, i5_12600Kf.ECoreBoostGhz);
        Assert.Equal(compareUrl, i5_12600Kf.SourceUrl);
    }

    [Fact]
    public void Default_Core11thGenUnlockedDesktopFamilyCompletesAt6()
    {
        // Dataset v1.70.0: the five Intel Core 11th-gen unlocked desktop
        // CPUs the dataset was missing, completing the unlocked set at
        // six SKUs (the 11900K, 11900KF, 11700K, 11700KF, 11600K and
        // 11600KF; only the 11900K was already listed). All five are
        // Rocket Lake on 14 nm, LGA 1200, and each KF part shares its K
        // twin's clocks exactly and differs only by the missing integrated
        // graphics, so a discrete card is required. There are no E-cores
        // in this generation, so ECoreBoostGhz is null on all five. The
        // 11900KF at Processor Base Frequency 3.5 GHz and Max Turbo
        // Frequency up to 5.3 GHz (Thermal Velocity Boost 5.3 GHz, Turbo
        // Boost Max 3.0 5.2 GHz, Turbo Boost 2.0 5.1 GHz), 8 cores,
        // 16 threads, 16 MB Intel Smart Cache, 125 W base power, no
        // integrated graphics, launched Q1 2021. The 11700K at 3.6 and up
        // to 5.0 GHz (Turbo Boost Max 3.0 5.0 GHz, Turbo Boost 2.0
        // 4.9 GHz), 8 cores, 16 threads, 16 MB cache, 125 W, launched
        // Q1 2021. The 11700KF at the same 3.6 and 5.0 GHz, 8 cores,
        // 16 threads, 16 MB cache, 125 W, no integrated graphics,
        // launched Q1 2021. The 11600K at 3.9 and up to 4.9 GHz (Turbo
        // Boost 2.0 4.9 GHz, no Turbo Boost Max 3.0), 6 cores, 12 threads,
        // 12 MB cache, 125 W, launched Q1 2021. The 11600KF at the same
        // 3.9 and 4.9 GHz, 6 cores, 12 threads, 12 MB cache, 125 W, no
        // integrated graphics, launched Q1 2021. The boost figures are
        // ARK Max Turbo Frequency, the headline up-to figure, per the
        // dataset's boost definition. Intel's own compare pages return
        // HTTP 403 in this sandbox, so the figures rest on agreeing ARK
        // mirrors, the same disclosed third-party exception as the
        // 14th-gen unlocked set in v2.1.99 and the 13th-gen unlocked set
        // in v2.2.4; Intel's own compare page did surface in the search
        // index for the 11900KF and the 11700K and agrees exactly
        // (11900KF Max Turbo 5.30, TVB 5.30, TBMT 3.0 5.20, TBT 2.0 5.10,
        // Base 3.50, 16 MB, 125 W; 11700K Max Turbo 5.00, TBMT 3.0 5.00,
        // TBT 2.0 4.90, Base 3.60, 16 MB, 125 W). Three reading traps
        // are recorded: cpu-monkey lists the 11700K base as 3.4 GHz and
        // the 11600K at 3.8 GHz with a 4.5 GHz boost, which are all-core
        // figures, while Intel's own compare page and the agreeing
        // sources give 3.6 GHz base and 5.0 GHz Max Turbo for the 11700K
        // and 3.9 GHz base and 4.9 GHz Max Turbo for the 11600K;
        // technical.city lists the 11900KF boost as 5.2 GHz, which is
        // its Turbo Boost Max 3.0 frequency, while the ARK Max Turbo is
        // 5.3 GHz via Thermal Velocity Boost, the same trap class as the
        // 13900KF's 5.7 GHz listing; and some retailers list the 12700KF
        // turbo as 4.9 GHz, which is its Single P-core Turbo, while the
        // ARK Max Turbo is 5.0 GHz via Turbo Boost Max 3.0. The dataset
        // carries no core-count, cache or TDP field, so those figures
        // live here in the comment and in the README and ROADMAP
        // entries, not in the entries themselves.
        const string compareUrl = "https://www.intel.com/content/www/us/en/products/compare.html?productIds=212326,212327,212325,212275,212276";

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i9-11900KF", out var i9_11900Kf));
        Assert.Equal("Intel", i9_11900Kf.Vendor);
        Assert.Equal(3.5, i9_11900Kf.BaseGhz);
        Assert.Equal(5.3, i9_11900Kf.BoostGhz);
        Assert.Null(i9_11900Kf.ECoreBoostGhz);
        Assert.Equal(compareUrl, i9_11900Kf.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i7-11700K", out var i7_11700K));
        Assert.Equal("Intel", i7_11700K.Vendor);
        Assert.Equal(3.6, i7_11700K.BaseGhz);
        Assert.Equal(5.0, i7_11700K.BoostGhz);
        Assert.Null(i7_11700K.ECoreBoostGhz);
        Assert.Equal(compareUrl, i7_11700K.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i7-11700KF", out var i7_11700Kf));
        Assert.Equal("Intel", i7_11700Kf.Vendor);
        Assert.Equal(3.6, i7_11700Kf.BaseGhz);
        Assert.Equal(5.0, i7_11700Kf.BoostGhz);
        Assert.Null(i7_11700Kf.ECoreBoostGhz);
        Assert.Equal(compareUrl, i7_11700Kf.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i5-11600K", out var i5_11600K));
        Assert.Equal("Intel", i5_11600K.Vendor);
        Assert.Equal(3.9, i5_11600K.BaseGhz);
        Assert.Equal(4.9, i5_11600K.BoostGhz);
        Assert.Null(i5_11600K.ECoreBoostGhz);
        Assert.Equal(compareUrl, i5_11600K.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i5-11600KF", out var i5_11600Kf));
        Assert.Equal("Intel", i5_11600Kf.Vendor);
        Assert.Equal(3.9, i5_11600Kf.BaseGhz);
        Assert.Equal(4.9, i5_11600Kf.BoostGhz);
        Assert.Null(i5_11600Kf.ECoreBoostGhz);
        Assert.Equal(compareUrl, i5_11600Kf.SourceUrl);
    }

    [Fact]
    public void Default_Core10thGenUnlockedDesktopFamilyCompletesAt6()
    {
        // Dataset v1.71.0: the four Intel Core 10th-gen unlocked desktop
        // CPUs the dataset was missing, completing the unlocked set at
        // six SKUs (the 10900K, 10900KF, 10700K, 10700KF, 10600K and
        // 10600KF; only the 10900K and 10700K were already listed). All
        // four are Comet Lake on 14 nm, LGA 1200, and each KF part shares
        // its K twin's clocks exactly and differs only by the missing
        // integrated graphics, so a discrete card is required. There are
        // no E-cores in this generation, so ECoreBoostGhz is null on all
        // four. The 10900KF at Processor Base Frequency 3.7 GHz and Max
        // Turbo Frequency up to 5.3 GHz (Thermal Velocity Boost 5.3 GHz,
        // Turbo Boost Max 3.0 5.2 GHz, Turbo Boost 2.0 5.1 GHz), 10 cores,
        // 20 threads, 20 MB Intel Smart Cache, 125 W base power, no
        // integrated graphics, launched Q2 2020. The 10700KF at 3.8 and
        // up to 5.1 GHz (Turbo Boost Max 3.0 5.1 GHz, Turbo Boost 2.0
        // 5.0 GHz), 8 cores, 16 threads, 16 MB cache, 125 W, no
        // integrated graphics, launched Q2 2020. The 10600K at 4.1 and up
        // to 4.8 GHz (Turbo Boost 2.0 4.8 GHz, no Turbo Boost Max 3.0),
        // 6 cores, 12 threads, 12 MB cache, 125 W, launched Q2 2020. The
        // 10600KF at the same 4.1 and 4.8 GHz, 6 cores, 12 threads,
        // 12 MB cache, 125 W, no integrated graphics, launched Q2 2020.
        // The boost figures are ARK Max Turbo Frequency, the headline
        // up-to figure, per the dataset's boost definition. Intel's own
        // compare pages return HTTP 403 in this sandbox, so the figures
        // rest on agreeing ARK mirrors, the same disclosed third-party
        // exception as the 14th-gen unlocked set in v2.1.99, the 13th-gen
        // unlocked set in v2.2.4 and the 11th-gen unlocked set in v2.2.6;
        // Intel's own compare page surfaced in the search index for all
        // four and agrees exactly (10900KF Max Turbo 5.30, TVB 5.30,
        // TBMT 3.0 5.20, TBT 2.0 5.10, Base 3.70, 20 MB, 125 W; 10700KF
        // Max Turbo 5.10, TBMT 3.0 5.10, TBT 2.0 5.00, Base 3.80, 16 MB,
        // 125 W; 10600K and 10600KF Max Turbo 4.80, TBT 2.0 4.80, Base
        // 4.10, 12 MB, 125 W). Three reading traps are recorded: the
        // 10900KF 5.3 GHz is Thermal Velocity Boost, not the P-core Max
        // Turbo, and some sources list its 5.2 GHz Turbo Boost Max 3.0
        // figure as its maximum turbo, while the ARK Max Turbo is
        // 5.3 GHz, the same trap class as the 13900KF's 5.7 GHz listing;
        // the 10700KF's Turbo Boost 2.0 figure is 5.0 GHz while the ARK
        // Max Turbo is 5.1 GHz via Turbo Boost Max 3.0, and pre-launch
        // leak tables quoted the two figures the other way round; and
        // cpu-world's CPUID dump on its 10600KF page lists a 4900 MHz
        // maximum frequency from a single sampled chip, which is not the
        // rated figure: the agreeing ARK mirrors give 4.8 GHz. The
        // dataset carries no core-count, cache or TDP field, so those
        // figures live here in the comment and in the README and ROADMAP
        // entries, not in the entries themselves.
        const string compareUrl = "https://www.intel.com/content/www/us/en/products/compare.html?productIds=199331,199325,199311,199315";

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i9-10900KF", out var i9_10900Kf));
        Assert.Equal("Intel", i9_10900Kf.Vendor);
        Assert.Equal(3.7, i9_10900Kf.BaseGhz);
        Assert.Equal(5.3, i9_10900Kf.BoostGhz);
        Assert.Null(i9_10900Kf.ECoreBoostGhz);
        Assert.Equal(compareUrl, i9_10900Kf.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i7-10700KF", out var i7_10700Kf));
        Assert.Equal("Intel", i7_10700Kf.Vendor);
        Assert.Equal(3.8, i7_10700Kf.BaseGhz);
        Assert.Equal(5.1, i7_10700Kf.BoostGhz);
        Assert.Null(i7_10700Kf.ECoreBoostGhz);
        Assert.Equal(compareUrl, i7_10700Kf.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i5-10600K", out var i5_10600K));
        Assert.Equal("Intel", i5_10600K.Vendor);
        Assert.Equal(4.1, i5_10600K.BaseGhz);
        Assert.Equal(4.8, i5_10600K.BoostGhz);
        Assert.Null(i5_10600K.ECoreBoostGhz);
        Assert.Equal(compareUrl, i5_10600K.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i5-10600KF", out var i5_10600Kf));
        Assert.Equal("Intel", i5_10600Kf.Vendor);
        Assert.Equal(4.1, i5_10600Kf.BaseGhz);
        Assert.Equal(4.8, i5_10600Kf.BoostGhz);
        Assert.Null(i5_10600Kf.ECoreBoostGhz);
        Assert.Equal(compareUrl, i5_10600Kf.SourceUrl);
    }

    [Fact]
    public void Default_Core9thGenUnlockedDesktopFamilyCompletesAt6()
    {
        // Dataset v1.73.0: the four Intel Core 9th-gen unlocked desktop
        // CPUs the dataset was missing, completing the unlocked set at
        // six SKUs (the 9900K, 9900KF, 9700K, 9700KF, 9600K and 9600KF;
        // only the 9900K and 9700K were already listed). All four are
        // Coffee Lake Refresh on 14 nm, socket LGA 1151, and each KF part
        // shares its K twin's clocks exactly and differs only by the
        // missing integrated graphics, so a discrete card is required.
        // There are no E-cores in this generation, so ECoreBoostGhz is
        // null on all four. The 9900KF at Processor Base Frequency
        // 3.6 GHz and Max Turbo Frequency up to 5.0 GHz (Turbo Boost 2.0
        // 5.0 GHz, no Turbo Boost Max 3.0, no Thermal Velocity Boost),
        // 8 cores, 16 threads, 16 MB Intel Smart Cache, 95 W base power,
        // no integrated graphics, launched Q1 2019. The 9700KF at 3.6 and
        // up to 4.9 GHz (Turbo Boost 2.0 4.9 GHz), 8 cores, 8 threads,
        // 12 MB cache, 95 W, no integrated graphics, launched Q1 2019.
        // The 9600K at 3.7 and up to 4.6 GHz (Turbo Boost 2.0 4.6 GHz),
        // 6 cores, 6 threads, 9 MB cache, 95 W, launched Q4 2018. The
        // 9600KF at the same 3.7 and 4.6 GHz, 6 cores, 6 threads,
        // 9 MB cache, 95 W, no integrated graphics, launched Q1 2019.
        // The boost figures are ARK Max Turbo Frequency, the headline
        // up-to figure, per the dataset's boost definition. Intel's own
        // compare pages return HTTP 403 in this sandbox, so the figures
        // rest on agreeing ARK mirrors, the same disclosed third-party
        // exception as the 14th-gen unlocked set in v2.1.99, the 13th-gen
        // unlocked set in v2.2.4 and the 11th-gen unlocked set in v2.2.6;
        // Intel's own compare page surfaced in the search index for the
        // 9700KF and the 9600KF and agrees exactly (9700KF Max Turbo
        // 4.90, TBT 2.0 4.90, Base 3.60, 12 MB, 95 W; 9600KF Max Turbo
        // 4.60, TBT 2.0 4.60, Base 3.70, 9 MB, 95 W), and the 9900KF
        // figures agree across Newegg, technical.city, Scan, WikiChip,
        // PCCaseGear and ShopCGX, which republish the ARK table verbatim
        // (Base 3.60, Max Turbo 5.00, TBT 2.0 5.00, 16 MB, 95 W).
        // Three reading traps are recorded: the KF parts launched in
        // Q1 2019 while their K twins launched in Q4 2018, and some
        // sources date the KF parts to late 2018, which is the
        // announcement, not the retail launch; technical.city lists the
        // 9600KF recommended price as $262, which is the 9600K's price,
        // while cpu-world lists $237 for the 9600KF; and the 9700KF and
        // 9700K are 8 cores and 8 threads, with no Hyper-Threading on the
        // 9th-gen i7, while the i9 twins carry 16 threads. The dataset
        // carries no core-count, cache or TDP field, so those figures
        // live here in the comment and in the README and ROADMAP entries,
        // not in the entries themselves. The six-SKU family follows the
        // v2.2.8 audit's definition (the i9, i7 and i5 K and KF parts);
        // two further unlocked 9th-gen desktop SKUs stay outside it and
        // are not counted here: the Core i9-9900KS special edition
        // (4.0/5.0 GHz, 127 W, October 2019) and the Core i3-9350K and
        // 9350KF (4.0/4.6 GHz, 4 cores, 4 threads, 91 W, January 2019).
        const string compareUrl = "https://www.intel.com/content/www/us/en/products/compare.html?productIds=190887,190885,134896,190884";

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i9-9900KF", out var i9_9900Kf));
        Assert.Equal("Intel", i9_9900Kf.Vendor);
        Assert.Equal(3.6, i9_9900Kf.BaseGhz);
        Assert.Equal(5.0, i9_9900Kf.BoostGhz);
        Assert.Null(i9_9900Kf.ECoreBoostGhz);
        Assert.Equal(compareUrl, i9_9900Kf.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i7-9700KF", out var i7_9700Kf));
        Assert.Equal("Intel", i7_9700Kf.Vendor);
        Assert.Equal(3.6, i7_9700Kf.BaseGhz);
        Assert.Equal(4.9, i7_9700Kf.BoostGhz);
        Assert.Null(i7_9700Kf.ECoreBoostGhz);
        Assert.Equal(compareUrl, i7_9700Kf.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i5-9600K", out var i5_9600K));
        Assert.Equal("Intel", i5_9600K.Vendor);
        Assert.Equal(3.7, i5_9600K.BaseGhz);
        Assert.Equal(4.6, i5_9600K.BoostGhz);
        Assert.Null(i5_9600K.ECoreBoostGhz);
        Assert.Equal(compareUrl, i5_9600K.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i5-9600KF", out var i5_9600Kf));
        Assert.Equal("Intel", i5_9600Kf.Vendor);
        Assert.Equal(3.7, i5_9600Kf.BaseGhz);
        Assert.Equal(4.6, i5_9600Kf.BoostGhz);
        Assert.Null(i5_9600Kf.ECoreBoostGhz);
        Assert.Equal(compareUrl, i5_9600Kf.SourceUrl);
    }

    [Fact]
    public void Default_Core9thGenUnlockedDesktopRemainingSkusCompleteAt9()
    {
        // Dataset v1.74.0: the three remaining Intel Core 9th-gen unlocked
        // desktop CPUs the dataset was missing, completing the unlocked
        // 9th-gen desktop set at nine SKUs (with the six from v1.73.0: the
        // 9900K, 9900KF, 9700K, 9700KF, 9600K and 9600KF). All three are
        // Coffee Lake Refresh on 14 nm, socket LGA 1151. There are no
        // E-cores in this generation, so ECoreBoostGhz is null on all
        // three. The 9900KS special edition at Processor Base Frequency
        // 4.0 GHz and Max Turbo Frequency up to 5.0 GHz, 8 cores,
        // 16 threads, 16 MB Intel Smart Cache, 127 W base power, launched
        // October 2019. The 9350K at 4.0 and up to 4.6 GHz (Turbo Boost
        // 2.0 4.6 GHz), 4 cores, 4 threads, 8 MB cache, 91 W, launched
        // Q2 2019. The 9350KF at the same 4.0 and 4.6 GHz, 4 cores,
        // 4 threads, 8 MB cache, 91 W, no integrated graphics, launched
        // Q1 2019. The boost figures are ARK Max Turbo Frequency, the
        // headline up-to figure, per the dataset's boost definition.
        // Intel's own compare pages return HTTP 403 in this sandbox, so
        // the figures rest on agreeing ARK mirrors, the same disclosed
        // third-party exception as the 14th-gen unlocked set in v2.1.99,
        // the 13th-gen unlocked set in v2.2.4 and the 11th-gen unlocked
        // set in v2.2.6; Intel's own compare page surfaced in the search
        // index for the 9350KF and agrees exactly (9350KF Max Turbo 4.60,
        // TBT 2.0 4.60, Base 4.00, 8 MB, 91 W; launch Q1'19), and the
        // 9900KS figures agree across TechPowerUp, notebookcheck and
        // PCCaseGear, which republishes the ARK table verbatim (Base 4.00,
        // Max Turbo 5.00, Cache 16 MB, TDP 127 W), with the 9900KS ARK ID
        // 192943 confirmed on the Intel datasheet, and the 9350K figures
        // agree across cpu-world, multitronic (ARK ID 186606, launch
        // Q2'19), cputronic and siliconcat. Three reading traps are
        // recorded: the 9900KS 5.0 GHz is an all-core turbo, while the
        // 9900K's 5.0 GHz is a single-core maximum, so the two parts share
        // the headline figure but not its meaning; cpu-monkey lists the
        // 9350K as 4 cores and 8 threads in its header while its own
        // specification table says 4 cores and 4 threads, and every
        // agreeing source gives 4 threads; and the 9350K launched in Q2
        // 2019 (April 23), after the 9350KF in Q1 2019 (January 8), so the
        // K part is the later launch, the reverse of the K/KF pattern in
        // the i9, i7 and i5 pairs. The dataset carries no core-count,
        // cache or TDP field, so those figures live here in the comment
        // and in the README and ROADMAP entries, not in the entries
        // themselves. Family-definition decision: the six-SKU family of
        // v2.2.8/v2.2.9 covered the i9, i7 and i5 K and KF parts, and the
        // three SKUs added here, the KS special edition and the i3 K and
        // KF pair, complete the unlocked 9th-gen desktop set at nine
        // SKUs; counting the KS part follows the 13th-gen and 14th-gen
        // sets, which counted their KS parts.
        const string compareUrl = "https://www.intel.com/content/www/us/en/products/compare.html?productIds=192943,186606,191126";

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i9-9900KS", out var i9_9900Ks));
        Assert.Equal("Intel", i9_9900Ks.Vendor);
        Assert.Equal(4.0, i9_9900Ks.BaseGhz);
        Assert.Equal(5.0, i9_9900Ks.BoostGhz);
        Assert.Null(i9_9900Ks.ECoreBoostGhz);
        Assert.Equal(compareUrl, i9_9900Ks.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i3-9350K", out var i3_9350K));
        Assert.Equal("Intel", i3_9350K.Vendor);
        Assert.Equal(4.0, i3_9350K.BaseGhz);
        Assert.Equal(4.6, i3_9350K.BoostGhz);
        Assert.Null(i3_9350K.ECoreBoostGhz);
        Assert.Equal(compareUrl, i3_9350K.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core i3-9350KF", out var i3_9350Kf));
        Assert.Equal("Intel", i3_9350Kf.Vendor);
        Assert.Equal(4.0, i3_9350Kf.BaseGhz);
        Assert.Equal(4.6, i3_9350Kf.BoostGhz);
        Assert.Null(i3_9350Kf.ECoreBoostGhz);
        Assert.Equal(compareUrl, i3_9350Kf.SourceUrl);
    }

    [Fact]
    public void Default_Ryzen7000DragonRangeHxMobileFamilyCompletesAt4()
    {
        // Dataset v1.72.0: the three AMD Ryzen 7000 Dragon Range HX mobile
        // CPUs the dataset was missing, completing the Dragon Range HX
        // mobile family at four SKUs (the 7945HX, 7845HX, 7745HX and
        // 7645HX; only the 7945HX was already listed). All three are Zen 4
        // Dragon Range on 5 nm, socket FL1, with Radeon 610M integrated
        // graphics, and all three are unlocked. The Ryzen 9 7845HX at Base
        // Clock 3.0 GHz and Max Boost Clock up to 5.2 GHz, 12 cores,
        // 24 threads, 64 MB L3 cache, 55 W default TDP (45-75 W
        // configurable), launched February 28, 2023. The Ryzen 7 7745HX at
        // 3.6 and up to 5.1 GHz, 8 cores, 16 threads, 32 MB L3 cache,
        // 55 W default TDP (45-75 W configurable), launched February 28,
        // 2023. The Ryzen 5 7645HX at 4.0 and up to 5.0 GHz, 6 cores,
        // 12 threads, 32 MB L3 cache, 55 W default TDP (45-75 W
        // configurable), launched February 28, 2023. The boost figures are
        // AMD Max Boost Clock, the headline up-to figure, per the
        // dataset's boost definition. AMD's own product pages surfaced in
        // the search index for all three and agree exactly (7845HX Max
        // Boost Clock up to 5.2 GHz, Base Clock 3.0 GHz, 12 cores,
        // 24 threads, 64 MB L3, Default TDP 55 W, cTDP 45-75 W; 7745HX up
        // to 5.1 GHz, Base 3.6 GHz, 8 cores, 16 threads, 32 MB L3,
        // 55 W; 7645HX up to 5.0 GHz, Base 4.0 GHz, 6 cores, 12 threads,
        // 32 MB L3, 55 W), cross-checked against agreeing third parties
        // (notebookcheck, nanoreview, technical.city and wccftech, which
        // all agree per SKU on the base and the boost). Two reading traps
        // are recorded: technical.city lists the 7645HX TDP as 45 W in
        // some views, which is the configurable TDP floor, while AMD's own
        // page gives Default TDP 55 W with cTDP 45-75 W; and some laptop
        // reviews report the 7845HX drawing 110 W or more under load,
        // which is an OEM power limit, not the rated 55 W default TDP.
        // The dataset carries no core-count, cache or TDP field, so those
        // figures live here in the comment and in the README and ROADMAP
        // entries, not in the entries themselves.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 9 7845HX", out var r9_7845Hx));
        Assert.Equal("AMD", r9_7845Hx.Vendor);
        Assert.Equal(3.0, r9_7845Hx.BaseGhz);
        Assert.Equal(5.2, r9_7845Hx.BoostGhz);
        Assert.Null(r9_7845Hx.ECoreBoostGhz);
        Assert.Equal("https://www.amd.com/en/products/processors/laptop/ryzen/7000-series/amd-ryzen-9-7845hx.html", r9_7845Hx.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 7 7745HX", out var r7_7745Hx));
        Assert.Equal("AMD", r7_7745Hx.Vendor);
        Assert.Equal(3.6, r7_7745Hx.BaseGhz);
        Assert.Equal(5.1, r7_7745Hx.BoostGhz);
        Assert.Null(r7_7745Hx.ECoreBoostGhz);
        Assert.Equal("https://www.amd.com/en/products/processors/laptop/ryzen/7000-series/amd-ryzen-7-7745hx.html", r7_7745Hx.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 7645HX", out var r5_7645Hx));
        Assert.Equal("AMD", r5_7645Hx.Vendor);
        Assert.Equal(4.0, r5_7645Hx.BaseGhz);
        Assert.Equal(5.0, r5_7645Hx.BoostGhz);
        Assert.Null(r5_7645Hx.ECoreBoostGhz);
        Assert.Equal("https://www.amd.com/en/products/processors/laptop/ryzen/7000-series/amd-ryzen-5-7645hx.html", r5_7645Hx.SourceUrl);
    }

    [Fact]
    public void Default_CoreUltra200SDesktopFamilyCompletesAt18()
    {
        // Dataset v1.60.0: the six Intel Core Ultra 200S desktop CPUs the
        // dataset was missing, completing the Core Ultra 200S desktop
        // family at 18 SKUs (the three K parts 285K, 265K and 245K, the
        // three KF parts 285KF, 265KF and 245KF, the seven 65 W parts
        // 285, 265, 265F, 245, 235, 225 and 225F, and the five 35 W T
        // parts 285T, 265T, 245T, 235T and 225T). All six are Arrow Lake
        // on TSMC 3nm FinFET, LGA 1851. The 285KF at Performance-core
        // Base Frequency 3.7 GHz and Max Turbo Frequency up to 5.7 GHz
        // with Efficient-core Max Turbo Frequency 4.6 GHz, 24 cores
        // (8 P-cores and 16 E-cores, 24 threads, 36 MB Intel Smart
        // Cache), 125 W base power, unlocked, no integrated graphics,
        // launched October 24, 2024. The 285T at 1.4 and up to 5.4 GHz
        // with E-core turbo 4.6 GHz, 24 cores, 36 MB cache, 35 W,
        // launched January 7, 2025. The 265T at 1.5 and up to 5.3 GHz
        // with E-core turbo 4.6 GHz, 20 cores (8 P-cores and 12
        // E-cores, 20 threads, 30 MB cache), 35 W, launched January 7,
        // 2025. The 245T at 2.2 and up to 5.1 GHz with E-core turbo
        // 4.5 GHz, 14 cores (6 P-cores and 8 E-cores, 14 threads,
        // 24 MB cache), 35 W, launched January 7, 2025. The 235T at
        // 2.2 and up to 5.0 GHz with E-core turbo 4.4 GHz, 14 cores,
        // 24 MB cache, 35 W, launched January 7, 2025. The 225T at
        // 2.5 and up to 4.9 GHz with E-core turbo 4.4 GHz, 10 cores
        // (6 P-cores and 4 E-cores, 10 threads, 20 MB cache), 35 W,
        // launched January 7, 2025. The boost figures are ARK Max Turbo
        // Frequency, the headline up-to figure, per the dataset's boost
        // definition. The 265T figures rest on Intel's own Core Ultra
        // 200S series processors for the edge product brief (December
        // 2024), which lists the 265T at 20 cores, 30 MB cache, 35 W,
        // P-core turbo 5.3 GHz, E-core turbo 4.6 GHz, P-core base
        // 1.5 GHz and E-core base 1.2 GHz, cross-checked against
        // agreeing third parties. The 285T, 245T, 235T and 225T
        // figures rest on agreeing third-party ARK mirrors, the same
        // disclosed mirror exception as the 251HX in v2.1.76, because
        // Intel's own compare pages return HTTP 403 in this sandbox.
        // The 285KF figures rest on the K twin: the KF is the same
        // silicon as the 285K with the integrated graphics disabled,
        // and the dataset already records the 265KF and 245KF at their
        // K twins' clocks. Two reading traps are recorded: topcpu and
        // siliconcat give the 265T a 2.4 GHz P-core base, while
        // Intel's own product brief and the agreeing sources give
        // 1.5 GHz, so Intel's own document decides it; and topcpu and
        // siliconcat give the 265T a 4.5 GHz E-core turbo, while
        // Intel's own product brief and the agreeing sources give
        // 4.6 GHz, so Intel's own document decides it. The dataset
        // carries no core-count, cache or TDP field, so those figures
        // live here in the comment and in the README and ROADMAP
        // entries, not in the entries themselves.
        const string compareUrl = "https://www.intel.com/content/www/us/en/products/compare.html?productIds=240961,241060,240958,240956,241062,241063,240957,240954,241066,241067,240951,240959,240955,240960";

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 9 285KF", out var ultra9_285Kf));
        Assert.Equal(3.7, ultra9_285Kf.BaseGhz);
        Assert.Equal(5.7, ultra9_285Kf.BoostGhz);
        Assert.Equal(4.6, ultra9_285Kf.ECoreBoostGhz);
        Assert.Equal(compareUrl, ultra9_285Kf.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 9 285T", out var ultra9_285T));
        Assert.Equal(1.4, ultra9_285T.BaseGhz);
        Assert.Equal(5.4, ultra9_285T.BoostGhz);
        Assert.Equal(4.6, ultra9_285T.ECoreBoostGhz);
        Assert.Equal(compareUrl, ultra9_285T.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 7 265T", out var ultra7_265T));
        Assert.Equal(1.5, ultra7_265T.BaseGhz);
        Assert.Equal(5.3, ultra7_265T.BoostGhz);
        Assert.Equal(4.6, ultra7_265T.ECoreBoostGhz);
        Assert.Equal(compareUrl, ultra7_265T.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 5 245T", out var ultra5_245T));
        Assert.Equal(2.2, ultra5_245T.BaseGhz);
        Assert.Equal(5.1, ultra5_245T.BoostGhz);
        Assert.Equal(4.5, ultra5_245T.ECoreBoostGhz);
        Assert.Equal(compareUrl, ultra5_245T.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 5 235T", out var ultra5_235T));
        Assert.Equal(2.2, ultra5_235T.BaseGhz);
        Assert.Equal(5.0, ultra5_235T.BoostGhz);
        Assert.Equal(4.4, ultra5_235T.ECoreBoostGhz);
        Assert.Equal(compareUrl, ultra5_235T.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 5 225T", out var ultra5_225T));
        Assert.Equal(2.5, ultra5_225T.BaseGhz);
        Assert.Equal(4.9, ultra5_225T.BoostGhz);
        Assert.Equal(4.4, ultra5_225T.ECoreBoostGhz);
        Assert.Equal(compareUrl, ultra5_225T.SourceUrl);
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
        // The two Ultra 5 200HX chips were deliberately absent until
        // v2.1.74 for a different reason: their figures could not be
        // verified live on Intel's own page in the v2.1.47 cycle, so
        // they stayed out even though third-party listings agreed on
        // them. That exclusion is overturned in this cycle: Intel's
        // own Quick Reference Guide for the 200HX series, v1.1, read
        // 2026-11-15, publishes both rows, so both chips are listed
        // now and the assertions below are removed.
        Assert.False(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) i7-12700H", out _));
        Assert.False(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) i3-8100", out _));
        Assert.False(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) i9-13980HX", out _));
        Assert.False(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) i9-13900HX", out _));
        Assert.False(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) i7-13700HX", out _));
        Assert.False(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) i7-13650HX", out _));
        Assert.False(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) i5-13500HX", out _));
        Assert.False(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) i5-13450HX", out _));
    }

    [Theory]
    [InlineData("NVIDIA GeForce RTX 4060 Laptop GPU", "RTX 4060 LAPTOP")]
    [InlineData("NVIDIA GeForce RTX 5090 Laptop GPU", "RTX 5090 LAPTOP")]
    [InlineData("NVIDIA GeForce RTX 5090 D", "RTX 5090 D")]
    [InlineData("NVIDIA GeForce GTX 1080 Ti", "GTX 1080 TI")]
    [InlineData("NVIDIA GeForce GTX 1070 Ti", "GTX 1070 TI")]
    [InlineData("NVIDIA GeForce GTX 1070", "GTX 1070")]
    [InlineData("NVIDIA GeForce GTX 1060 3GB", "GTX 1060 3GB")]
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
    [InlineData("AMD Radeon RX 9070 GRE", "RX 9070 GRE")]
    [InlineData("AMD Radeon RX 6700", "RX 6700")]
    [InlineData("AMD Radeon RX 6950 XT", "RX 6950 XT")]
    [InlineData("AMD Radeon RX 7650 GRE", "RX 7650 GRE")]
    [InlineData("NVIDIA GeForce RTX 3080 Ti Laptop GPU", "RTX 3080 TI LAPTOP")]
    [InlineData("NVIDIA GeForce RTX 3050 Ti Laptop GPU", "RTX 3050 TI LAPTOP")]
    [InlineData("AMD Radeon RX 6500 XT", "RX 6500 XT")]
    [InlineData("AMD Radeon RX 6400", "RX 6400")]
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
    [InlineData("Intel(R) Arc(TM) A310 Graphics", "ARC A310")]
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
        // compare page. The GTX 1060 3GB and GTX 1070 exclusion is
        // overturned in v2.2.0: both cards are listed now, in dataset
        // v1.64.0, and the GTX 1070 assertion below is removed.
        // NVIDIA's own GTX 10 product pages are gone (the old
        // 10-series page now redirects to the RTX 50 Series page, and
        // the live compare page carries no GTX 10 per-card table), so
        // the figures rest on the TechPowerUp GPU database pages, the
        // same disclosed third-party exception as the RTX 5090 D in
        // v2.1.98.
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
        // 245HX and 235HX stayed absent until v2.1.74: their figures
        // could not be verified live on Intel's own page in the
        // v2.1.47 cycle. That exclusion is overturned in this cycle:
        // Intel's own Quick Reference Guide for the 200HX series,
        // v1.1, read 2026-11-15 from a fresh fetch, publishes both
        // rows in its HX table, so both chips are listed now and the
        // family is complete at six SKUs. The 245HX at Performance-
        // core Base Frequency 3.1 GHz and Max Turbo Frequency up to
        // 5.1 GHz; the 235HX at 2.9 and up to 5.1 GHz. Both are 14
        // cores (6 P-cores and 8 E-cores, 14 threads, 24 MB cache).
        // One judgement call is recorded: the Quick Reference Guide
        // table publishes the P-core base and the Max Turbo per SKU
        // but no E-core turbo, so the E-core Max Turbo Frequency of
        // 4.5 GHz for both chips comes from five agreeing ARK
        // mirrors (nanoreview, siliconcat, techpowerup, cputronic,
        // eatyourbytes), the same mirror rule used for the 200H and
        // 200U families. The trailing numbers in the Quick Reference
        // Guide HX table (2.0, 1.9, 1.9, 1.85, 1.9 and 1.8 GHz) are
        // the graphics Max Frequency, not E-core base figures, so
        // they are not recorded as base figures anywhere. No code
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

        // Dataset v1.38.0: the two Ultra 5 200HX chips complete the
        // family. Both figures were verified by hand against Intel's
        // own Quick Reference Guide for the 200HX series, v1.1, read
        // 2026-11-15, which publishes the Performance-core Base
        // Frequency and the Max Turbo Frequency per SKU in its HX
        // table. Each entry records its source URL and retrieval
        // date; the source URL is Intel's own document page for the
        // guide, because the guide PDF itself was read through the
        // browser fetch path and no direct cdrdv2-public link for
        // this document surfaced in search.
        const string qrgHx = "https://www.intel.com/content/www/us/en/content-details/842532/intel-core-ultra-processors-200hx-series-processors-quick-reference-guide-pdf.html";

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) Ultra 5 245HX", out var ultra5245Hx));
        Assert.Equal("Intel", ultra5245Hx.Vendor);
        Assert.Equal(3.1, ultra5245Hx.BaseGhz);
        Assert.Equal(5.1, ultra5245Hx.BoostGhz);
        Assert.Equal(4.5, ultra5245Hx.ECoreBoostGhz);
        Assert.StartsWith(qrgHx, ultra5245Hx.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) Ultra 5 235HX", out var ultra5235Hx));
        Assert.Equal("Intel", ultra5235Hx.Vendor);
        Assert.Equal(2.9, ultra5235Hx.BaseGhz);
        Assert.Equal(5.1, ultra5235Hx.BoostGhz);
        Assert.Equal(4.5, ultra5235Hx.ECoreBoostGhz);
        Assert.StartsWith(qrgHx, ultra5235Hx.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_CoreUltra200HxPlusMobileCpusCarryVerifiedFigures()
    {
        // Dataset v1.39.0: the two Core Ultra 200HX Plus mobile CPUs,
        // the Arrow Lake-HX Refresh family Intel announced March 17,
        // 2026: the Core Ultra 9 290HX Plus and the Core Ultra 7 270HX
        // Plus. The P-core base figures come from Intel's own Core
        // Ultra 200HX Plus datasheet (832586 Rev 007, March 2026),
        // Table 61, which publishes frequencies per configuration and
        // the HX-Plus line has exactly two configurations, so the
        // mapping to the two SKUs is one to one. The Max Turbo figures
        // come from Intel's own press deck for the series and the ARK
        // spec page titles, which publish the cache and the up-to Max
        // Turbo per SKU. The E-core Max Turbo Frequency of 4.7 GHz for
        // both comes from agreeing ARK mirrors (nanoreview, topcpu,
        // cputronic and rottenwifi, plus Clove and Ryans spec sheets),
        // the same mirror rule used for the 200H, 200U and 200HX
        // families. The ARK
        // spec table values could not be extracted in this cycle
        // (Intel returns HTTP 403 in this sandbox and the fetched page
        // text carries only boilerplate), so the ARK page titles are
        // what corroborate the model, cache and Max Turbo. This cycle
        // also corrects the coverage note from v2.1.73 and v2.1.74,
        // which named the family as 290HX Plus plus 251HX with the
        // launch dates swapped: the family is 290HX Plus plus 270HX
        // Plus, launched March 17, 2026, and the 251HX is a separate
        // non-Plus SKU announced April 1, 2026, which stays out until
        // an Intel document corroborates its figures. No code change
        // was needed: the CPU normalizer already maps the names, and
        // the Settings card shows the CPU count from the dataset, so
        // it reads 196 with no code change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) Ultra 9 290HX Plus", out var ultra9290HxPlus));
        Assert.Equal("Intel", ultra9290HxPlus.Vendor);
        Assert.Equal(2.7, ultra9290HxPlus.BaseGhz);
        Assert.Equal(5.5, ultra9290HxPlus.BoostGhz);
        Assert.Equal(4.7, ultra9290HxPlus.ECoreBoostGhz);
        Assert.StartsWith("https://www.intel.com/content/www/us/en/products/sku/245960/intel-core-ultra-9-processor-290hx-plus-36m-cache-up-to-5-50-ghz/specifications.html", ultra9290HxPlus.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) Ultra 7 270HX Plus", out var ultra7270HxPlus));
        Assert.Equal("Intel", ultra7270HxPlus.Vendor);
        Assert.Equal(2.4, ultra7270HxPlus.BaseGhz);
        Assert.Equal(5.3, ultra7270HxPlus.BoostGhz);
        Assert.Equal(4.7, ultra7270HxPlus.ECoreBoostGhz);
        Assert.StartsWith("https://www.intel.com/content/www/us/en/products/sku/245961/intel-core-ultra-7-processor-270hx-plus-30m-cache-up-to-5-30-ghz/specifications.html", ultra7270HxPlus.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_CoreUltra7251HxMobileCarriesVerifiedFigures()
    {
        // Dataset v1.40.0: the Intel Core Ultra 7 251HX mobile CPU, the
        // separate non-Plus Arrow Lake-HX SKU Intel announced April 1,
        // 2026, which slots between the Core Ultra 5 245HX and the Core
        // Ultra 7 255HX. This overturns the v2.1.75 exclusion, which kept
        // it out because no Intel document corroborated its figures:
        // Intel's own APP Metrics for Intel Microprocessors, Revision 2,
        // dated June 15, 2026, read directly in this cycle, lists
        // "Intel Core Ultra 7 processor 251HX (30M Cache, up to 5.10
        // GHz)", which corroborates the model, the cache and the Max
        // Turbo from Intel itself, and Intel's own Material Declaration
        // Data Sheet for the 270HX Plus, 290HX Plus and 251HX names the
        // SKU as an Intel product. The 200HX Quick Reference Guide v1.1
        // was fetched directly in this cycle and contains no 251HX row,
        // and datasheet 832586 Rev 007 (March 2026) predates the April 1
        // announcement, so no newer datasheet revision surfaced. One
        // judgement call is recorded: the Performance-core Base
        // Frequency of 2.9 GHz and the E-core Max Turbo Frequency of
        // 4.5 GHz rest on agreeing ARK mirrors (nanoreview, topcpu,
        // siliconcat, cputronic, eatyourbytes, rottenwifi, notebookcheck
        // and cpubenchmark) and on outlet reads of Intel's own ARK
        // specifications page (VideoCardz, TweakTown, WCCFTech, Lowyat,
        // HWBusters and ThinkComputers all report the same figures from
        // it), not on a direct Intel read, because Intel's own compare
        // pages return HTTP 403 in this sandbox and the ARK spec table
        // values could not be extracted here. The E-core turbo from
        // mirrors is the same rule used for the 200H, 200U, 200HX and
        // 200HX Plus families; the P-core base from mirrors is a
        // deliberate, disclosed exception for this one SKU, justified by
        // the Intel APP Metrics corroboration of the SKU and its Max
        // Turbo plus the unanimous mirrors. The only mirror disagreement
        // is the technical.city listing that calls its base clock
        // 2.5 GHz, which is the E-core base mislabeled. The chip is 18
        // cores (6 P-cores, 12 E-cores, 18 threads, 30 MB cache), 55 W
        // base power and 160 W maximum turbo power. No code change was
        // needed: the CPU normalizer already maps the name, and the
        // Settings card shows the CPU count from the dataset, so it
        // reads 197 with no code change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) Ultra 7 251HX", out var ultra7251Hx));
        Assert.Equal("Intel", ultra7251Hx.Vendor);
        Assert.Equal(2.9, ultra7251Hx.BaseGhz);
        Assert.Equal(5.1, ultra7251Hx.BoostGhz);
        Assert.Equal(4.5, ultra7251Hx.ECoreBoostGhz);
        Assert.StartsWith("https://www.intel.com/content/www/us/en/content-details/916308/intel-core-ultra-7-processor-270hx-plus-ultra-9-processor-290hx-plus-ultra-7-processor-251hx-105143-mdds.html", ultra7251Hx.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_CoreUltraSeries3RemainingCpusCarryVerifiedFigures()
    {
        // Dataset v1.41.0: the three remaining Intel Core Ultra Series 3
        // (Panther Lake) mobile CPUs, which complete the family at fifteen
        // SKUs: the Core Ultra 5 335, 332 and 322, launched January 5,
        // 2026 on Intel 18A. This overturns the v2.1.65 exclusion, which
        // kept them out because their Performance-core Base Frequency
        // figures could not be verified live on Intel's own page: Intel's
        // own Series 3 Quick Reference Guide v1 (cdrdv2-public.intel.com
        // 871380), fetched directly as a PDF in this cycle and
        // text-extracted, lists all three SKUs with P-core Max Turbo
        // Frequency 4.6, 4.4 and 4.4 GHz, 8, 6 and 6 cores and threads,
        // 12 MB Intel Smart Cache, 25 W processor base power and 55 W
        // maximum turbo power, which corroborates the SKUs and their Max
        // Turbo from Intel itself. The guide publishes no P-core base
        // frequency, so one judgement call is recorded, the same disclosed
        // exception as the 251HX in v2.1.76: the Performance-core Base
        // Frequencies 2.2, 2.5 and 2.5 GHz and the Low Power
        // Efficient-core Max Turbo Frequencies 3.4, 3.3 and 3.3 GHz rest
        // on agreeing ARK mirrors (nanoreview, topcpu, siliconcat,
        // cputronic, cpu-monkey, eatyourbytes and notebookcheck), not on a
        // direct Intel read, because Intel's own compare pages return HTTP
        // 403 in this sandbox and no Intel compare page containing these
        // three SKUs surfaced. The old 322 disagreement is resolved: the
        // 1.9 GHz figure is the Low Power Efficient-core base, not the
        // P-core base (technical.city mislabels it as the base clock, and
        // notebookcheck's "1.9 - 4.4 GHz" is a min-max range from the
        // LP-E base to the P-core turbo); every source that reports the
        // P-core base distinctly agrees on 2.5 GHz. The three have no
        // standard E-cores, so their e_core_boost_ghz carries the Low
        // Power Efficient-core Max Turbo Frequency, the same rule the
        // dataset already uses for the 365, 325 and 355. The 332 and 322
        // are identical on clocks, cores, cache and graphics and differ
        // only in vPro eligibility (the guide lists the 332 as eligible
        // and the 322 as not), so both are listed, the same as the 378H
        // and 368H. No code change was needed: the CPU normalizer already
        // maps the names, and the Settings card shows the CPU count from
        // the dataset, so it reads 200 with no code change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) Ultra 5 335", out var ultra5335));
        Assert.Equal("Intel", ultra5335.Vendor);
        Assert.Equal(2.2, ultra5335.BaseGhz);
        Assert.Equal(4.6, ultra5335.BoostGhz);
        Assert.Equal(3.4, ultra5335.ECoreBoostGhz);
        Assert.StartsWith("https://cdrdv2-public.intel.com/871380/Intel%20Core%20Ultra%20Series%203%20Processors%20-%20Quick%20Reference%20Guide%20v1.pdf", ultra5335.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) Ultra 5 332", out var ultra5332));
        Assert.Equal("Intel", ultra5332.Vendor);
        Assert.Equal(2.5, ultra5332.BaseGhz);
        Assert.Equal(4.4, ultra5332.BoostGhz);
        Assert.Equal(3.3, ultra5332.ECoreBoostGhz);
        Assert.StartsWith("https://cdrdv2-public.intel.com/871380/Intel%20Core%20Ultra%20Series%203%20Processors%20-%20Quick%20Reference%20Guide%20v1.pdf", ultra5332.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) Ultra 5 322", out var ultra5322));
        Assert.Equal("Intel", ultra5322.Vendor);
        Assert.Equal(2.5, ultra5322.BaseGhz);
        Assert.Equal(4.4, ultra5322.BoostGhz);
        Assert.Equal(3.3, ultra5322.ECoreBoostGhz);
        Assert.StartsWith("https://cdrdv2-public.intel.com/871380/Intel%20Core%20Ultra%20Series%203%20Processors%20-%20Quick%20Reference%20Guide%20v1.pdf", ultra5322.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_Ryzen5000X3dDesktopCpusCarryVerifiedFigures()
    {
        // Dataset v1.42.0: the three remaining AMD Ryzen 5000 X3D desktop
        // CPUs (Vermeer, Zen 3 with 3D V-Cache), which complete the AM4
        // X3D set alongside the 5800X3D already in the dataset. This
        // overturns the stays-out note for the 5700X3D, which kept it out
        // because AMD's own product page could not be read live: AMD has
        // removed the per-product page, the product URL now redirects to
        // the specifications hub and the new-style URL redirects to the
        // AMD homepage, so no live AMD read was possible in this cycle.
        // The 5700X3D figures rest on AMD's own documentation as mirrored
        // verbatim, AMD's own spec sheet (Product ID Boxed
        // 100-100001503WOF, launch 1/8/2024) and AMD's own user manual
        // (Base Clock Speed 3.0 GHz, Max Boost Clock Up to 4.1 GHz, 96 MB
        // L3 cache, 105 W), plus a dated PDF print of AMD's own product
        // page amd.com/en/product/14101 and unanimous third-party
        // agreement (nanoreview, siliconcat, cputronic, cpu-monkey and
        // technical.city); the only disagreement is a pre-launch
        // speculation piece that guessed 3.4 and 4.5 GHz before AMD
        // announced the part, superseded by AMD's own announcement at 3.0
        // and 4.1 GHz. The other two verify against AMD's own live pages:
        // the 5600X3D against AMD's support page (Base Clock 3.3 GHz,
        // Max. Boost Clock Up to 4.4 GHz, 96 MB L3 cache, 105 W, launch
        // 07/07/2023) and the 5500X3D against AMD's product page (Base
        // Clock 3 GHz, Max. Boost Clock Up to 4 GHz, 96 MB L3 cache,
        // 105 W, launch 6/5/2025, Latin America). All three are Zen 3
        // Vermeer on AM4 with 96 MB of L3 cache and a 105 W default TDP,
        // and all three launched after the 5800X3D: the 5600X3D on
        // July 7, 2023 (Micro Center exclusive in the US), the 5700X3D on
        // January 8, 2024, and the 5500X3D on June 5, 2025. No code change
        // was needed: the CPU normalizer already maps the names, and the
        // Settings card shows the CPU count from the dataset, so it reads
        // 203 with no code change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 7 5700X3D", out var ryzen75700X3d));
        Assert.Equal("AMD", ryzen75700X3d.Vendor);
        Assert.Equal(3.0, ryzen75700X3d.BaseGhz);
        Assert.Equal(4.1, ryzen75700X3d.BoostGhz);
        Assert.Equal("https://www.amd.com/en/product/14101", ryzen75700X3d.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 5600X3D", out var ryzen55600X3d));
        Assert.Equal("AMD", ryzen55600X3d.Vendor);
        Assert.Equal(3.3, ryzen55600X3d.BaseGhz);
        Assert.Equal(4.4, ryzen55600X3d.BoostGhz);
        Assert.Equal("https://www.amd.com/en/support/downloads/drivers.html/processors/ryzen/ryzen-5000-series/amd-ryzen-5-5600x3d.html", ryzen55600X3d.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 5500X3D", out var ryzen55500X3d));
        Assert.Equal("AMD", ryzen55500X3d.Vendor);
        Assert.Equal(3.0, ryzen55500X3d.BaseGhz);
        Assert.Equal(4.0, ryzen55500X3d.BoostGhz);
        Assert.Equal("https://www.amd.com/en/products/processors/desktops/ryzen/5000-series/amd-ryzen-5-5500x3d.html", ryzen55500X3d.SourceUrl);
    }

    [Fact]
    public void Default_Core200HAnd200UMobileCpusCarryVerifiedFigures()
    {
        // Dataset v1.43.0: the seven Intel Core 200H and 200U mobile CPUs
        // (Raptor Lake-H/U Refresh, non-Ultra, Series 2), which the dataset
        // never listed. This overturns the v2.1.78 exclusion, which kept
        // them out because no Intel document published their figures, only
        // the CES 2025 keynote deck, whose table text is garbled, and
        // pre-launch leaks. Intel itself published the full Core 200H
        // table on its own website in December 2024, and Notebookcheck
        // reproduces that table verbatim (Core 9 270H, Core 7 250H, Core 7
        // 240H, Core 5 220H, Core 5 210H, with P-core base, P-core boost,
        // E-core base, E-core boost, TDP and cache per SKU); the 210H
        // figures are independently confirmed by Intel's own product
        // comparison page as read through the search index (8 cores,
        // 12 threads, Max Turbo 4.8 GHz, P-core base 2.2 GHz, E-core base
        // 1.6 GHz, 12 MB cache, 45 W, launch Q4'24). The five H parts
        // launched December 18, 2024, all on Intel 7 at 45 W. The two U
        // parts launched January 6, 2025, both 10 cores (2 P-cores, 8
        // E-cores, 12 threads, 12 MB cache) at 15 W. One judgement call
        // is recorded, the same disclosed mirror exception as the 251HX
        // in v2.1.76 and the 5700X3D in v2.1.78: no Intel document with a
        // per-SKU table for the U parts surfaced, so the 250U at 1.9 and
        // up to 5.4 GHz with E-core turbo 4.1 GHz and the 220U at 1.5 and
        // up to 5.0 GHz with E-core turbo 3.9 GHz rest on agreeing ARK
        // mirrors (siliconcat, cputronic, topcpu, technical.city, and
        // techpowerup for the 220U), not on a direct Intel read, because
        // Intel's own compare pages return HTTP 403 in this sandbox and
        // no live compare-page spot-check was possible in this cycle. Two
        // mirror disagreements are resolved: the 250U P-core base is 1.9
        // GHz per the agreeing ARK mirrors, while notebookcheck's
        // per-SKU page gives 1.8 GHz because it copies the renamed
        // predecessor 150U's clocks (and pc-builds also lists 1.8 GHz);
        // the 220U E-core turbo is 3.9 GHz per the agreeing ARK mirrors
        // including techpowerup, while notebookcheck's per-SKU page gives
        // 3.8 GHz, the predecessor 120U's figure. Staying out: the Core 7
        // 230H, a real SKU shipping since June 12, 2026, because no
        // source found publishes its P-core base distinctly (notebookcheck
        // gives only the 1.8 to 5.2 GHz min-max range), and it is not
        // inferred from the 240H; and the Core 3 100U, a Series 1 part
        // launched January 2024 and a rebrand of the i3-1315U, not part
        // of the Core 200U family. No code change was needed: the CPU
        // normalizer already maps the names, and the Settings card shows
        // the CPU count from the dataset, so it reads 210 with no code
        // change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) 9 270H", out var core9270H));
        Assert.Equal("Intel", core9270H.Vendor);
        Assert.Equal(2.7, core9270H.BaseGhz);
        Assert.Equal(5.8, core9270H.BoostGhz);
        Assert.Equal(4.1, core9270H.ECoreBoostGhz);
        Assert.Equal("https://www.notebookcheck.net/Intel-confirms-specs-of-Core-9-Processor-270H-Core-7-Processor-250H-Core-5-Processor-220H-and-other-rebadged-Raptor-Lake-CPUs-on-its-website.934559.0.html", core9270H.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) 7 250H", out var core7250H));
        Assert.Equal("Intel", core7250H.Vendor);
        Assert.Equal(2.5, core7250H.BaseGhz);
        Assert.Equal(5.4, core7250H.BoostGhz);
        Assert.Equal(4.0, core7250H.ECoreBoostGhz);
        Assert.Equal("https://www.notebookcheck.net/Intel-confirms-specs-of-Core-9-Processor-270H-Core-7-Processor-250H-Core-5-Processor-220H-and-other-rebadged-Raptor-Lake-CPUs-on-its-website.934559.0.html", core7250H.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) 7 240H", out var core7240H));
        Assert.Equal("Intel", core7240H.Vendor);
        Assert.Equal(2.5, core7240H.BaseGhz);
        Assert.Equal(5.2, core7240H.BoostGhz);
        Assert.Equal(4.0, core7240H.ECoreBoostGhz);
        Assert.Equal("https://www.notebookcheck.net/Intel-confirms-specs-of-Core-9-Processor-270H-Core-7-Processor-250H-Core-5-Processor-220H-and-other-rebadged-Raptor-Lake-CPUs-on-its-website.934559.0.html", core7240H.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) 5 220H", out var core5220H));
        Assert.Equal("Intel", core5220H.Vendor);
        Assert.Equal(2.7, core5220H.BaseGhz);
        Assert.Equal(4.9, core5220H.BoostGhz);
        Assert.Equal(3.7, core5220H.ECoreBoostGhz);
        Assert.Equal("https://www.notebookcheck.net/Intel-confirms-specs-of-Core-9-Processor-270H-Core-7-Processor-250H-Core-5-Processor-220H-and-other-rebadged-Raptor-Lake-CPUs-on-its-website.934559.0.html", core5220H.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) 5 210H", out var core5210H));
        Assert.Equal("Intel", core5210H.Vendor);
        Assert.Equal(2.2, core5210H.BaseGhz);
        Assert.Equal(4.8, core5210H.BoostGhz);
        Assert.Equal(3.6, core5210H.ECoreBoostGhz);
        Assert.Equal("https://www.notebookcheck.net/Intel-confirms-specs-of-Core-9-Processor-270H-Core-7-Processor-250H-Core-5-Processor-220H-and-other-rebadged-Raptor-Lake-CPUs-on-its-website.934559.0.html", core5210H.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) 7 250U", out var core7250U));
        Assert.Equal("Intel", core7250U.Vendor);
        Assert.Equal(1.9, core7250U.BaseGhz);
        Assert.Equal(5.4, core7250U.BoostGhz);
        Assert.Equal(4.1, core7250U.ECoreBoostGhz);
        Assert.Equal("https://siliconcat.net/cpu/intel-core-7-250u", core7250U.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) 5 220U", out var core5220U));
        Assert.Equal("Intel", core5220U.Vendor);
        Assert.Equal(1.5, core5220U.BaseGhz);
        Assert.Equal(5.0, core5220U.BoostGhz);
        Assert.Equal(3.9, core5220U.ECoreBoostGhz);
        Assert.Equal("https://siliconcat.net/cpu/intel-core-5-220u", core5220U.SourceUrl);
    }

    [Fact]
    public void Default_Core200HDisabledGraphicsCpusCarryVerifiedFigures()
    {
        // Dataset v1.44.0: the two remaining Intel Core 200H mobile CPUs
        // with disabled graphics (Raptor Lake-H Refresh, non-Ultra,
        // Series 2), completing the family at nine parts. This overturns
        // the v2.1.79 exclusion, which kept the 230H out because no
        // source found published its P-core base distinctly, notebookcheck
        // giving only the 1.8 to 5.2 GHz min-max range from the E-core
        // base to the P-core turbo, and it was not inferred from the
        // 240H. The 205H was not considered at all in that cycle. The
        // per-SKU pages, fetched directly in this cycle, state the
        // P-cluster range distinctly: the 230H P-cores run 2.5 to 5.2
        // GHz with E-cores at 1.8 to 4.0 GHz, and the 205H P-cores run
        // 2.2 to 4.8 GHz with E-cores at 1.6 to 3.6 GHz. VideoCardz's
        // read of Intel's own product pages confirms the 230H has the
        // same CPU configuration, clocks, cache and power limits as the
        // Core 7 240H and the 205H the same specifications as the Core 5
        // 210H, and topcpu's spec listing for the 205H (8 cores, 12
        // threads, 12 MB cache, 45 W, boost up to 4.8 GHz, E-core boost
        // 3.6 GHz) is stated to follow specifications published by
        // Intel. The 230H launched June 12, 2026 and the 205H on June 7,
        // 2026, both on Intel 7 at 45 W. The one difference from the
        // 240H and the 210H is the graphics: the 230H's Xe iGPU is
        // disabled, and the 205H has no Xe graphics unit enabled or
        // installed. One judgement call is recorded, the same disclosed
        // mirror exception as the 251HX in v2.1.76, the 5700X3D in
        // v2.1.78 and the 250U and 220U in v2.1.79: Intel's own ARK page
        // for the 230H exists (SKU 246028, its title confirms 24M cache
        // and up to 5.20 GHz), but the spec table values could not be
        // extracted here, the fetched page text carries only boilerplate,
        // and Intel's own compare pages return HTTP 403 in this sandbox,
        // so no live compare-page spot-check was possible in this cycle
        // and the P-core base figures rest on notebookcheck's per-SKU
        // pages stating the P-cluster range distinctly, corroborated by
        // the Intel-sourced same-clocks reports. Two blocks were
        // re-checked in this cycle and stay out. Intel 13th-gen HX
        // P-core base: Intel's own compare page for the i9-13900K,
        // i9-13900T and i9-13980HX, fetched directly, still shows the
        // Performance-core Base Frequency row with an empty cell for the
        // 13980HX (3.00 GHz for the 13900K, 1.10 GHz for the 13900T) and
        // the Efficient-core Base Frequency cell empty too, while Max
        // Turbo 5.60 GHz and E-core Max Turbo 4.00 GHz are published;
        // the mirrors still agree on 2.2 GHz, but the vendor-page rule
        // keeps the family out. The Core 7 245HX non-Ultra: its figures
        // now verify distinctly on mirrors (GadReview gives P-core base
        // 2.9 GHz, E-core base 2.6 GHz, turbo 5.1 GHz; notebookcheck
        // gives a slower P-core base than the Ultra 5 245HX's 3.1 GHz
        // with a 2.6 to 5.1 GHz range; VideoCardz and TweakTown, from
        // Intel ARK, give the same clocks as the Ultra 5 235HX), but it
        // stays out because the number 245HX already names the Ultra 5
        // part and a second 245HX would invite the misidentification the
        // dataset exists to prevent. The Core 3 100U stays out too: it
        // is a Series 1 part launched January 2024, a rebrand of the
        // i3-1315U, not part of the Core 200U family. No code change was
        // needed: the CPU normalizer already maps the names, and the
        // Settings card shows the CPU count from the dataset, so it
        // reads 212 with no code change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) 7 230H", out var core7230H));
        Assert.Equal("Intel", core7230H.Vendor);
        Assert.Equal(2.5, core7230H.BaseGhz);
        Assert.Equal(5.2, core7230H.BoostGhz);
        Assert.Equal(4.0, core7230H.ECoreBoostGhz);
        Assert.Equal("https://www.notebookcheck.net/Intel-Core-7-230H-Processor-Benchmarks-and-Specs.1363555.0.html", core7230H.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel(R) Core(TM) 5 205H", out var core5205H));
        Assert.Equal("Intel", core5205H.Vendor);
        Assert.Equal(2.2, core5205H.BaseGhz);
        Assert.Equal(4.8, core5205H.BoostGhz);
        Assert.Equal(3.6, core5205H.ECoreBoostGhz);
        Assert.Equal("https://www.notebookcheck.net/Intel-Core-5-205H-Processor-Benchmarks-and-Specs.1364948.0.html", core5205H.SourceUrl);
    }

    [Fact]
    public void Default_Ryzen200ProMobileCpusCarryVerifiedFigures()
    {
        // Dataset v1.45.0: the four AMD Ryzen 200 PRO mobile CPUs (Hawk
        // Point, Zen 4 on TSMC 4nm FinFET, RDNA 3 graphics, 28 W default
        // TDP), the PRO family the dataset never listed. This overturns
        // the v2.1.59 exclusion, which kept the PRO variants out on the
        // ground that the dataset covered consumer parts only, a ground
        // later cycles superseded by listing the AI 400, AI 300 and AI
        // Max PRO families. All figures below were verified by hand
        // against AMD's own product page per model, read 2026-11-15 from
        // fresh fetches, and cross-checked against AMD's own January 2026
        // mobile-for-business Quick Reference Guide, which publishes the
        // same boost and base figures for all four, and against the CES
        // 2025 press-release table as reproduced by Tom's Hardware: the
        // Ryzen 7 PRO 250 at Base Clock 3.3 GHz and Max Boost Clock up to
        // 5.1 GHz, 8 cores and 16 threads, 24 MB total cache (8 MB L2,
        // 16 MB L3), Radeon 780M; the Ryzen 5 PRO 230 at 3.5 and up to
        // 4.9 GHz, 6 cores and 12 threads, 22 MB total cache (6 MB L2,
        // 16 MB L3), Radeon 760M; the Ryzen 5 PRO 220 at 3.2 and up to
        // 4.9 GHz, 6 cores and 12 threads, 22 MB total cache (6 MB L2,
        // 16 MB L3), Radeon 740M; the Ryzen 3 PRO 210 at 3.0 and up to
        // 4.7 GHz, 4 cores and 8 threads, 12 MB total cache (4 MB L2,
        // 8 MB L3), Radeon 740M. All four were announced at CES 2025 on
        // January 6, 2025 and launched March 27, 2025. The two hybrid
        // parts (the PRO 220 with two Zen 4 plus four Zen 4c cores, the
        // PRO 210 with one Zen 4 plus three Zen 4c cores) use AMD's
        // headline Base Clock and Max Boost Clock, not the per-core-type
        // figures (the PRO 220 lists Max Zen4c Clock up to 3.5 GHz, the
        // PRO 210 up to 3.3 GHz), the same rule as the consumer Ryzen 200
        // hybrid parts and the AI 400 chips' Max Zen5c Clock. The dataset's
        // base_ghz is AMD's headline Base Clock and boost_ghz is AMD's
        // Max Boost Clock, per its own definition. Two later PRO 200
        // additions stayed out in this cycle: the Ryzen 5 PRO 215 (6 cores,
        // Base Clock 3.2 GHz and Max Boost Clock up to 4.7 GHz, AMD's own
        // product page, launch March 27, 2025) and the Ryzen 5 PRO 216
        // (6 cores, 2.9 and up to 4.7 GHz, Lenovo PSREF), later PRO 200
        // additions outside the four CES 2025 parts; both were added in
        // v2.1.82, which verified each against AMD's own product page.
        // No code change was needed: the
        // CPU normalizer already maps "AMD Ryzen 7 PRO 250" to
        // "RYZEN 7 PRO 250", "AMD Ryzen 5 PRO 230" to "RYZEN 5 PRO 230",
        // "AMD Ryzen 5 PRO 220" to "RYZEN 5 PRO 220" and "AMD Ryzen 3
        // PRO 210" to "RYZEN 3 PRO 210", and the Settings card shows the
        // CPU count from the dataset, so it reads 216 with no code change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 7 PRO 250", out var ryzen7Pro250));
        Assert.Equal("AMD", ryzen7Pro250.Vendor);
        Assert.Equal(3.3, ryzen7Pro250.BaseGhz);
        Assert.Equal(5.1, ryzen7Pro250.BoostGhz);
        Assert.Equal("https://www.amd.com/en/products/processors/laptop/ryzen-pro/200-series/amd-ryzen-7-pro-250.html", ryzen7Pro250.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 PRO 230", out var ryzen5Pro230));
        Assert.Equal("AMD", ryzen5Pro230.Vendor);
        Assert.Equal(3.5, ryzen5Pro230.BaseGhz);
        Assert.Equal(4.9, ryzen5Pro230.BoostGhz);
        Assert.Equal("https://www.amd.com/en/products/processors/laptop/ryzen-pro/200-series/amd-ryzen-5-pro-230.html", ryzen5Pro230.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 PRO 220", out var ryzen5Pro220));
        Assert.Equal("AMD", ryzen5Pro220.Vendor);
        Assert.Equal(3.2, ryzen5Pro220.BaseGhz);
        Assert.Equal(4.9, ryzen5Pro220.BoostGhz);
        Assert.Equal("https://www.amd.com/en/products/processors/laptop/ryzen-pro/200-series/amd-ryzen-5-pro-220.html", ryzen5Pro220.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 3 PRO 210", out var ryzen3Pro210));
        Assert.Equal("AMD", ryzen3Pro210.Vendor);
        Assert.Equal(3.0, ryzen3Pro210.BaseGhz);
        Assert.Equal(4.7, ryzen3Pro210.BoostGhz);
        Assert.Equal("https://www.amd.com/en/products/processors/laptop/ryzen-pro/200-series/amd-ryzen-3-pro-210.html", ryzen3Pro210.SourceUrl);
    }

    [Fact]
    public void Default_Ryzen200Pro215And216CarryVerifiedFigures()
    {
        // Dataset v1.46.0: the two remaining AMD Ryzen 200 PRO mobile
        // CPUs, completing the Ryzen 200 PRO family at six parts. Both are
        // Hawk Point Zen 4 on TSMC 4nm FinFET, RDNA 3 graphics, 28 W
        // default TDP, two Zen 4 plus four Zen 4c cores, 6 cores and 12
        // threads, 22 MB total cache (6 MB L2, 16 MB L3), Radeon 740M.
        // The Ryzen 5 PRO 215 at Base Clock 3.2 GHz and Max Boost Clock up
        // to 4.7 GHz, launched March 27, 2025; the Ryzen 5 PRO 216 at 2.9
        // and up to 4.7 GHz, launched June 2, 2026. All figures below were
        // verified by hand against AMD's own product page per model, read
        // 2026-11-15 from fresh fetches of the live pages: the PRO 215
        // page lists 2x Zen 4 plus 4x Zen 4c, 6 cores, 12 threads, Max
        // Boost Clock up to 4.7 GHz, Max Zen4c Clock up to 3.55 GHz, Base
        // Clock 3.2 GHz, L2 6 MB, L3 16 MB, 28 W, Radeon 740M with 4
        // compute units at 2.7 GHz; the PRO 216 page lists the same core
        // mix, 6 cores, 12 threads, Max Boost up to 4.7 GHz, Base Clock
        // 2.9 GHz, L2 6 MB, L3 16 MB, 28 W, Radeon 740M with 4 compute
        // units at 2.5 GHz. The PRO 216 page publishes no Max Zen4c Clock
        // figure, so none is recorded for it. Both parts use AMD's
        // headline Base Clock and Max Boost Clock, not the per-core-type
        // figures, the same rule as the consumer Ryzen 200 hybrid parts
        // and the other four PRO 200 parts. This completes the family the
        // v2.1.81 cycle left open: the two parts stayed out there because
        // the brief named only the four CES 2025 parts, and the PRO 216
        // note then rested on Lenovo PSREF; AMD's own product page now
        // verifies it directly, which also corrects that note. The
        // dataset's base_ghz is AMD's headline Base Clock and boost_ghz
        // is AMD's Max Boost Clock, per its own definition. No code change
        // was needed: the CPU normalizer already maps "AMD Ryzen 5 PRO
        // 215" to "RYZEN 5 PRO 215" and "AMD Ryzen 5 PRO 216" to
        // "RYZEN 5 PRO 216", and the Settings card shows the CPU count
        // from the dataset, so it reads 218 with no code change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 PRO 215", out var ryzen5Pro215));
        Assert.Equal("AMD", ryzen5Pro215.Vendor);
        Assert.Equal(3.2, ryzen5Pro215.BaseGhz);
        Assert.Equal(4.7, ryzen5Pro215.BoostGhz);
        Assert.Equal("https://www.amd.com/en/products/processors/laptop/ryzen-pro/200-series/amd-ryzen-5-pro-215.html", ryzen5Pro215.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 PRO 216", out var ryzen5Pro216));
        Assert.Equal("AMD", ryzen5Pro216.Vendor);
        Assert.Equal(2.9, ryzen5Pro216.BaseGhz);
        Assert.Equal(4.7, ryzen5Pro216.BoostGhz);
        Assert.Equal("https://www.amd.com/en/products/processors/laptop/ryzen-pro/200-series/amd-ryzen-5-pro-216.html", ryzen5Pro216.SourceUrl);
    }

    [Fact]
    public void Default_CoreUltra200VMobileCpusCarryVerifiedFigures()
    {
        // Dataset v1.47.0: the seven remaining Intel Core Ultra 200V
        // mobile CPUs (Lunar Lake, TSMC N3B), completing the 200V family
        // at nine parts. The 226V and 258V were in the dataset since
        // v2.1.14; the other seven were never listed, the same unnoticed
        // gap as the 9000 X3D family in v2.1.80, because no cycle had
        // listed the family against the dataset. All seven are 8 cores
        // (4 P-cores, 4 LP E-cores, 8 threads), launched Q3 2024, 17 W
        // base power and 37 W maximum turbo power except the 288V at
        // 30 W base power; the 288V, 268V, 266V, 258V and 256V carry
        // 12 MB Intel Smart Cache and the 238V, 236V, 228V and 226V
        // carry 8 MB. The Core Ultra 9 288V at Performance-core Base
        // Frequency 3.3 GHz and Max Turbo Frequency up to 5.1 GHz with
        // Low Power Efficient-core Max Turbo Frequency 3.7 GHz; the Core
        // Ultra 7 268V and 266V at 2.2 and up to 5.0 GHz with LP E-core
        // turbo 3.7 GHz; the Core Ultra 7 256V at 2.2 and up to 4.8 GHz
        // with LP E-core turbo 3.7 GHz; the Core Ultra 5 238V and 236V
        // at 2.1 and up to 4.7 GHz with LP E-core turbo 3.5 GHz; and
        // the Core Ultra 5 228V at 2.1 and up to 4.5 GHz with LP E-core
        // turbo 3.5 GHz. All figures below were verified by hand against
        // Intel's own product specification comparison page, read
        // 2026-11-15 from the fourteen-product compare page that lists
        // all nine V SKUs alongside the Core Ultra 200S desktop parts:
        // the page publishes Performance-core Base Frequency, Max Turbo
        // Frequency and Low Power Efficient-core Max Turbo Frequency
        // per SKU, and the two V parts already in the dataset match it
        // exactly (the 226V at 2.1, 4.5 and 3.5 GHz; the 258V at 2.2,
        // 4.8 and 3.7 GHz), which validates the column mapping. The
        // dataset's e_core_boost_ghz is the Low Power Efficient-core
        // Max Turbo Frequency, the same rule the dataset already uses
        // for the 200V parts and the Core Ultra Series 3 non-H models.
        // The pairs differ only in on-package memory (the 268V ships
        // 32 GB against the 266V 16 GB, the 258V 32 GB against the 256V
        // 16 GB, the 238V 32 GB against the 236V 16 GB, the 228V 32 GB
        // against the 226V 16 GB), so all four pairs are listed, the
        // same rule as the 378H and 368H in v2.1.65. Cross-checked
        // against Notebookcheck's per-SKU pages, which state the same
        // P-cluster ranges for all seven, and against TechPowerUp for
        // the 268V (2.2 GHz base, up to 5.0 GHz boost, E-cores 2.2 to
        // 3.7 GHz). One mirror disagreement is resolved: cputronic
        // lists the 256V Performance-core Base Frequency at 1.6 GHz,
        // which Intel's own page contradicts at 2.2 GHz, so Intel's
        // page decides it. The dataset's base_ghz is Intel's
        // Performance-core Base Frequency and boost_ghz is Intel's Max
        // Turbo Frequency, per its own definition. No code change was
        // needed: the CPU normalizer already maps "Intel(R) Core(TM)
        // Ultra 9 288V" to "ULTRA 9 288V", and the Settings card shows
        // the CPU count from the dataset, so it reads 225 with no code
        // change.
        const string compareUrl = "https://www.intel.com/content/www/us/en/products/compare.html?productIds=240961,241060,240958,240956,241062,241063,240957,240954,241066,241067,240951,240959,240955,240960";

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 9 288V", out var ultra9_288V));
        Assert.Equal("Intel", ultra9_288V.Vendor);
        Assert.Equal(3.3, ultra9_288V.BaseGhz);
        Assert.Equal(5.1, ultra9_288V.BoostGhz);
        Assert.Equal(3.7, ultra9_288V.ECoreBoostGhz);
        Assert.Equal(compareUrl, ultra9_288V.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 7 268V", out var ultra7_268V));
        Assert.Equal(2.2, ultra7_268V.BaseGhz);
        Assert.Equal(5.0, ultra7_268V.BoostGhz);
        Assert.Equal(3.7, ultra7_268V.ECoreBoostGhz);
        Assert.Equal(compareUrl, ultra7_268V.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 7 266V", out var ultra7_266V));
        Assert.Equal(2.2, ultra7_266V.BaseGhz);
        Assert.Equal(5.0, ultra7_266V.BoostGhz);
        Assert.Equal(3.7, ultra7_266V.ECoreBoostGhz);
        Assert.Equal(compareUrl, ultra7_266V.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 7 256V", out var ultra7_256V));
        Assert.Equal(2.2, ultra7_256V.BaseGhz);
        Assert.Equal(4.8, ultra7_256V.BoostGhz);
        Assert.Equal(3.7, ultra7_256V.ECoreBoostGhz);
        Assert.Equal(compareUrl, ultra7_256V.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 5 238V", out var ultra5_238V));
        Assert.Equal(2.1, ultra5_238V.BaseGhz);
        Assert.Equal(4.7, ultra5_238V.BoostGhz);
        Assert.Equal(3.5, ultra5_238V.ECoreBoostGhz);
        Assert.Equal(compareUrl, ultra5_238V.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 5 236V", out var ultra5_236V));
        Assert.Equal(2.1, ultra5_236V.BaseGhz);
        Assert.Equal(4.7, ultra5_236V.BoostGhz);
        Assert.Equal(3.5, ultra5_236V.ECoreBoostGhz);
        Assert.Equal(compareUrl, ultra5_236V.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 5 228V", out var ultra5_228V));
        Assert.Equal(2.1, ultra5_228V.BaseGhz);
        Assert.Equal(4.5, ultra5_228V.BoostGhz);
        Assert.Equal(3.5, ultra5_228V.ECoreBoostGhz);
        Assert.Equal(compareUrl, ultra5_228V.SourceUrl);
    }

    [Fact]
    public void Default_CoreUltra200SDesktopCpusCarryVerifiedFigures()
    {
        // Dataset v1.48.0: the nine remaining Intel Core Ultra 200S desktop
        // CPUs (Arrow Lake-S, TSMC N3B), completing the family at twelve
        // parts. The 285K, 265K and 245K were in the dataset since v2.1.14;
        // the nine remaining parts were never listed, the same unnoticed
        // gap as the 200V family in v2.1.83, because no cycle had listed the
        // family against the dataset. The two K/KF figures were verified
        // directly against Intel's own product specification comparison
        // page, read 2026-11-15, whose fourteen-product table also lists
        // the 285K at 3.7, 5.7 and 4.6 GHz, the 265K at 3.9, 5.5 and 4.6 GHz
        // and the 245K at 4.2, 5.2 and 4.6 GHz, matching the dataset
        // exactly, which validates the column mapping. The page also
        // resolves one pre-launch disagreement: the early retailer listing
        // gave the 245KF Max Turbo as 5.0 GHz, Intel's own page lists 5.2
        // GHz, so Intel's page decides it. The seven 65 W figures rest on
        // Intel's own ARK specification tables as reproduced by agreeing
        // retailers (B and H Photo, Newegg, TechPowerUp, Overclockers UK,
        // ComX Computers, VideoCardz), not on a direct Intel read, because
        // Intel's own compare pages return HTTP 403 in this sandbox and no
        // compare page containing the seven 65 W SKUs surfaced; the same
        // disclosed mirror exception as the 251HX in v2.1.76, the 5700X3D
        // in v2.1.78, the 250U and 220U in v2.1.79 and the 230H and 205H in
        // v2.1.80. The 245's launched P-core base is 3.5 GHz per the
        // agreeing ARK tables, not the 3.4 GHz the pre-launch leak tables
        // guessed; the launched figure decides it, the same rule as the
        // 265U P-core base in v2.1.70. The KF and F parts carry the same CPU
        // clocks as their K and non-K twins and differ only in the disabled
        // integrated graphics, so all of them are listed, the same rule as
        // the 378H and 368H in v2.1.65. Staying out, recorded in the
        // coverage text: the three 35 W T-series parts (the 285T, 265T and
        // 245T), whose figures rest only on pre-launch leak tables that
        // disagree with each other; the Core Ultra 5 250K PLUS, a 2026
        // refresh SKU on one retailer's table that needs Intel's own
        // documentation; the Core Ultra 3 205, leak only; the Intel
        // 13th-gen HX P-core base rows, not re-checked because no new Intel
        // document publishing per-SKU P-core base surfaced; the Core 7
        // 245HX non-Ultra, whose number still names the Ultra 5 part; the
        // Core 3 100U, a Series 1 rebrand of the i3-1315U; and the Ryzen AI
        // 7 HX 455, still an unconfirmed leak.
        const string compareUrl = "https://www.intel.com/content/www/us/en/products/compare.html?productIds=240961,241060,240958,240956,241062,241063,240957,240954,241066,241067,240951,240959,240955,240960";

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 7 265KF", out var ultra7_265KF));
        Assert.Equal(3.9, ultra7_265KF.BaseGhz);
        Assert.Equal(5.5, ultra7_265KF.BoostGhz);
        Assert.Equal(4.6, ultra7_265KF.ECoreBoostGhz);
        Assert.Equal(compareUrl, ultra7_265KF.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 5 245KF", out var ultra5_245KF));
        Assert.Equal(4.2, ultra5_245KF.BaseGhz);
        Assert.Equal(5.2, ultra5_245KF.BoostGhz);
        Assert.Equal(4.6, ultra5_245KF.ECoreBoostGhz);
        Assert.Equal(compareUrl, ultra5_245KF.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 9 285", out var ultra9_285));
        Assert.Equal(2.5, ultra9_285.BaseGhz);
        Assert.Equal(5.6, ultra9_285.BoostGhz);
        Assert.Equal(4.6, ultra9_285.ECoreBoostGhz);
        Assert.Equal("https://www.bhphotovideo.com/c/product/1860691-REG/intel_bx80768285_core_ultra_9_285.html/overview", ultra9_285.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 7 265", out var ultra7_265));
        Assert.Equal(2.4, ultra7_265.BaseGhz);
        Assert.Equal(5.3, ultra7_265.BoostGhz);
        Assert.Equal(4.6, ultra7_265.ECoreBoostGhz);
        Assert.Equal("https://www.newegg.com/intel-core-ultra-7-265-arrow-lake-lga-1851-desktop-cpu-processor/p/N82E16819118575", ultra7_265.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 7 265F", out var ultra7_265F));
        Assert.Equal(2.4, ultra7_265F.BaseGhz);
        Assert.Equal(5.3, ultra7_265F.BoostGhz);
        Assert.Equal(4.6, ultra7_265F.ECoreBoostGhz);
        Assert.Equal("https://www.bhphotovideo.com/c/product/1860690-REG/intel_bx80768265f_core_ultra_7_265f.html", ultra7_265F.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 5 245", out var ultra5_245));
        Assert.Equal(3.5, ultra5_245.BaseGhz);
        Assert.Equal(5.1, ultra5_245.BoostGhz);
        Assert.Equal(4.5, ultra5_245.ECoreBoostGhz);
        Assert.Equal("https://www.techpowerup.com/cpu-specs/core-ultra-5-245.c4020", ultra5_245.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 5 235", out var ultra5_235));
        Assert.Equal(3.4, ultra5_235.BaseGhz);
        Assert.Equal(5.0, ultra5_235.BoostGhz);
        Assert.Equal(4.4, ultra5_235.ECoreBoostGhz);
        Assert.Equal("https://www.overclockers.co.uk/intel-core-ultra-5-235-arrow-lake-socket-lga-1851-processor-retail/PRO-INT-04177.html", ultra5_235.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 5 225", out var ultra5_225));
        Assert.Equal(3.3, ultra5_225.BaseGhz);
        Assert.Equal(4.9, ultra5_225.BoostGhz);
        Assert.Equal(4.4, ultra5_225.ECoreBoostGhz);
        Assert.Equal("https://www.comx-computers.co.za/BX80768225-Intel-Arrow-Lake-Socket-LGA-1851-Core-Ultra-5-Series-2-10-Core-245-up-p-331020.php", ultra5_225.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 5 225F", out var ultra5_225F));
        Assert.Equal(3.3, ultra5_225F.BaseGhz);
        Assert.Equal(4.9, ultra5_225F.BoostGhz);
        Assert.Equal(4.4, ultra5_225F.ECoreBoostGhz);
        Assert.Equal("https://videocardz.com/newz/intel-core-ultra-5-225f-leaks-out-10-core-arrow-lake-desktop-cpu-with-4-9-ghz-boost", ultra5_225F.SourceUrl);
    }

    [Fact]
    public void Default_CoreUltra100UMobileCpusCarryVerifiedFigures()
    {
        // Dataset v1.49.0: the six Intel Core Ultra 100U mobile CPUs
        // (Meteor Lake-U, Intel 4, launched December 14, 2023), completing
        // the 100U family, which was wholly absent before. Only the 100H
        // parts (185H, 155H, 125H) were in the dataset, all since v2.1.14;
        // the 100U family was never listed, the same unnoticed gap as the
        // 200V family in v2.1.83 and the 200S family in v2.1.84, because no
        // cycle had listed the family against the dataset. The Max Turbo
        // figures (P-core and E-core) come from Intel's own Core Ultra
        // Processors Quick Reference Guide Phase 2, read 2026-11-15, whose
        // U-series table lists all six parts with the same turbos, cores,
        // threads and cache; the same table lists the three H parts already
        // in the dataset at the same figures (the 185H at 5.1 and 3.8 GHz,
        // the 155H at 4.8 and 3.8 GHz, the 125H at 4.5 and 3.6 GHz), which
        // validates the column mapping. The QRG publishes no P-core base
        // frequency, so the P-core base rests on agreeing ARK mirrors
        // (nanoreview, topcpu, cpu-monkey, notebookcheck, TechPowerUp,
        // technical.city and the CNX Software table taken from Intel ARK),
        // the same disclosed mirror exception as the Series 3 335, 332 and
        // 322 in v2.1.77 and the 251HX in v2.1.76. The 164U and 134U are
        // the 9 W vPro Type 4 parts (FCBGA2551 package, fewer PCIe lanes,
        // LPDDR5-6400 maximum, 9 W base power and 30 W maximum turbo power)
        // with distinct P-core base frequencies, so they are listed
        // separately from their 15 W twins, the same rule as the 332 and
        // 322 in v2.1.77. Two reading traps are recorded in the coverage
        // text: Notebookcheck's per-SKU pages give only the E-core turbo
        // to P-core turbo min-max range (the 165U reads 3.8 to 4.9 GHz),
        // not the P-core base; and the pre-launch WCCFTech leak table lists
        // the 135U and 125U base at 3.6 GHz, which is the E-core turbo
        // mislabeled, superseded by the launched ARK figures. Staying out,
        // recorded in the coverage text: the Core Ultra 7 165H and Core
        // Ultra 5 135H, the next candidate; the Ryzen 8000G desktop parts,
        // the Ryzen 5000G parts (5600G, 5700G) and the Arc A310, all
        // recorded as future candidates by the v2.1.84 audit; the Core
        // Ultra UL family (165UL, 155UL, 135UL, 125UL, 105UL), named in
        // Intel's own APP Metrics Revision 1; and the standing exclusions
        // (13th-gen HX P-core base, Core 7 245HX non-Ultra, Core 3 100U,
        // Ryzen AI 7 HX 455, Ryzen AI 400 mobile H variants), unchanged in
        // this cycle.
        const string qrgUrl = "https://cdrdv2-public.intel.com/791727/Intel%20Core%20Ultra%20Processors%20Quick%20Reference%20Guide%20Phase%202.pdf";

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 7 165U", out var ultra7_165U));
        Assert.Equal(1.7, ultra7_165U.BaseGhz);
        Assert.Equal(4.9, ultra7_165U.BoostGhz);
        Assert.Equal(3.8, ultra7_165U.ECoreBoostGhz);
        Assert.Equal(qrgUrl, ultra7_165U.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 7 155U", out var ultra7_155U));
        Assert.Equal(1.7, ultra7_155U.BaseGhz);
        Assert.Equal(4.8, ultra7_155U.BoostGhz);
        Assert.Equal(3.8, ultra7_155U.ECoreBoostGhz);
        Assert.Equal(qrgUrl, ultra7_155U.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 5 135U", out var ultra5_135U));
        Assert.Equal(1.6, ultra5_135U.BaseGhz);
        Assert.Equal(4.4, ultra5_135U.BoostGhz);
        Assert.Equal(3.6, ultra5_135U.ECoreBoostGhz);
        Assert.Equal(qrgUrl, ultra5_135U.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 5 125U", out var ultra5_125U));
        Assert.Equal(1.3, ultra5_125U.BaseGhz);
        Assert.Equal(4.3, ultra5_125U.BoostGhz);
        Assert.Equal(3.6, ultra5_125U.ECoreBoostGhz);
        Assert.Equal(qrgUrl, ultra5_125U.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 7 164U", out var ultra7_164U));
        Assert.Equal(1.1, ultra7_164U.BaseGhz);
        Assert.Equal(4.8, ultra7_164U.BoostGhz);
        Assert.Equal(3.8, ultra7_164U.ECoreBoostGhz);
        Assert.Equal(qrgUrl, ultra7_164U.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 5 134U", out var ultra5_134U));
        Assert.Equal(0.7, ultra5_134U.BaseGhz);
        Assert.Equal(4.4, ultra5_134U.BoostGhz);
        Assert.Equal(3.6, ultra5_134U.ECoreBoostGhz);
        Assert.Equal(qrgUrl, ultra5_134U.SourceUrl);
    }

    [Fact]
    public void Default_CoreUltra100ULDesktopCpusCarryVerifiedFigures()
    {
        // Dataset v1.54.0: the five Intel Core Ultra 100UL desktop CPUs
        // (Meteor Lake-PS, Intel 4, Socket 1851, launched April 8, 2024),
        // completing the UL family, which was wholly absent before. Only
        // the U and H parts were in the dataset; the socketed UL family
        // was never listed, the same unnoticed gap as the 100U family in
        // v2.1.85, the 200V family in v2.1.83 and the 200S family in
        // v2.1.84, because no cycle had listed the family against the
        // dataset. The Max Turbo figures and cache sizes come from
        // Intel's own APP Metrics for Intel Microprocessors Revision 1
        // (January 6, 2026), read 2026-11-15, which lists all five UL
        // parts with the same turbos and cache (165UL at 12 MB up to
        // 4.90 GHz, 155UL at 12 MB up to 4.80 GHz, 135UL at 12 MB up to
        // 4.40 GHz, 125UL at 12 MB up to 4.30 GHz, 105UL at 10 MB up to
        // 4.20 GHz), and lists the HL parts alongside them, which is
        // recorded in the coverage text as the next candidate. Intel's
        // own document publishes no P-core base or E-core turbo
        // frequency, so the P-core base and E-core turbo rest on
        // agreeing third-party databases (TechPowerUp, cputronic,
        // topcpu, technical.city, cpu-monkey and siliconcat), the same
        // disclosed mirror exception as the 100U parts in v2.1.85 and
        // the 100H parts in v2.1.86; and the 105UL page on TechPowerUp
        // was fetched directly as page text (8 cores, 10 threads, 1500
        // MHz by default, boost up to 4.2 GHz, E-core 1000 MHz up to
        // 3.5 GHz, LP E-core up to 2.1 GHz, 10 MB L3, Socket 1851,
        // April 8, 2024). The UL parts are the socketed Meteor Lake-PS
        // counterparts of the 100U mobile parts, with the same clocks as
        // their U twins (the 165UL matches the 165U at 1.7, 4.9 and 3.8
        // GHz, the 155UL the 155U at 1.7, 4.8 and 3.8 GHz, the 135UL
        // the 135U at 1.6, 4.4 and 3.6 GHz, the 125UL the 125U at 1.3,
        // 4.3 and 3.6 GHz), which cross-validates the figures. Two
        // reading traps are recorded in the coverage text:
        // notebookcheck's per-SKU pages give only the E-core turbo to
        // P-core turbo min-max range (the 165UL reads 3.8 to 4.9 GHz),
        // not the P-core base; and the 105UL is the only UL part with a
        // different core count (8 cores, 10 threads, 2 P-cores, 4
        // E-cores, 2 LP E-cores), so its figures are not inferred from
        // the 12-core parts. Staying out, recorded in the coverage
        // text: the Core Ultra HL family (165HL, 155HL, 135HL, 125HL),
        // the next candidate, higher-power H variants named in Intel's
        // own APP Metrics Revision 1 and not yet researched; and the
        // standing exclusions (13th-gen HX P-core base, Core 7 245HX
        // non-Ultra, Core 3 100U, Ryzen AI 7 HX 455, Ryzen AI 400
        // mobile H variants), unchanged in this cycle.
        const string appUrl = "https://cdrdv2-public.intel.com/874290/APP-for-Intel-Core-Ultra-Processors.pdf";

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 7 165UL", out var ultra7_165UL));
        Assert.Equal(1.7, ultra7_165UL.BaseGhz);
        Assert.Equal(4.9, ultra7_165UL.BoostGhz);
        Assert.Equal(3.8, ultra7_165UL.ECoreBoostGhz);
        Assert.Equal(appUrl, ultra7_165UL.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 7 155UL", out var ultra7_155UL));
        Assert.Equal(1.7, ultra7_155UL.BaseGhz);
        Assert.Equal(4.8, ultra7_155UL.BoostGhz);
        Assert.Equal(3.8, ultra7_155UL.ECoreBoostGhz);
        Assert.Equal(appUrl, ultra7_155UL.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 5 135UL", out var ultra5_135UL));
        Assert.Equal(1.6, ultra5_135UL.BaseGhz);
        Assert.Equal(4.4, ultra5_135UL.BoostGhz);
        Assert.Equal(3.6, ultra5_135UL.ECoreBoostGhz);
        Assert.Equal(appUrl, ultra5_135UL.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 5 125UL", out var ultra5_125UL));
        Assert.Equal(1.3, ultra5_125UL.BaseGhz);
        Assert.Equal(4.3, ultra5_125UL.BoostGhz);
        Assert.Equal(3.6, ultra5_125UL.ECoreBoostGhz);
        Assert.Equal(appUrl, ultra5_125UL.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 3 105UL", out var ultra3_105UL));
        Assert.Equal(1.5, ultra3_105UL.BaseGhz);
        Assert.Equal(4.2, ultra3_105UL.BoostGhz);
        Assert.Equal(3.5, ultra3_105UL.ECoreBoostGhz);
        Assert.Equal(appUrl, ultra3_105UL.SourceUrl);
    }

    [Fact]
    public void Default_CoreUltra100HLDesktopCpusCarryVerifiedFigures()
    {
        // Dataset v1.55.0: the four Intel Core Ultra 100HL desktop CPUs
        // (Meteor Lake-PS, Intel 4, Socket 1851, launched April 8, 2024),
        // completing the HL family, which was wholly absent before. Only
        // the U, H and UL parts were in the dataset; the higher-power HL
        // family was never listed, the same unnoticed gap as the 100UL
        // family in v2.1.90, the 100U family in v2.1.85, the 200V family
        // in v2.1.83 and the 200S family in v2.1.84, because no cycle had
        // listed the family against the dataset. The Max Turbo figures
        // and cache sizes come from Intel's own APP Metrics for Intel
        // Microprocessors Revision 1 (January 6, 2026), read 2026-11-15,
        // which lists all four HL parts with the same turbos and cache
        // (165HL at 24 MB up to 5.00 GHz, 155HL at 24 MB up to 4.80 GHz,
        // 135HL at 18 MB up to 4.60 GHz, 125HL at 18 MB up to 4.50 GHz),
        // and Intel's own product page confirms the headline figures
        // (the 165HL page lists 24M Cache, up to 5.00 GHz). Intel's own
        // document publishes no P-core base or E-core turbo frequency,
        // so the P-core base and E-core turbo rest on agreeing
        // third-party databases (TechPowerUp, cputronic, topcpu,
        // technical.city, cpu-monkey and siliconcat), the same disclosed
        // mirror exception as the 100U parts in v2.1.85, the 100H parts
        // in v2.1.86 and the 100UL parts in v2.1.90. The HL parts are
        // the socketed Meteor Lake-PS counterparts of the 100H mobile
        // parts, with the same clocks as their H twins (the 165HL
        // matches the 165H at 1.4, 5.0 and 3.8 GHz, the 155HL the 155H
        // at 1.4, 4.8 and 3.8 GHz, the 135HL the 135H at 1.7, 4.6 and
        // 3.6 GHz, the 125HL the 125H at 1.2, 4.5 and 3.6 GHz), which
        // cross-validates the figures. Two reading traps are recorded
        // in the coverage text: notebookcheck's per-SKU pages give only
        // the E-core turbo to P-core turbo min-max range (the 165HL
        // reads 3.8 to 5.0 GHz), not the P-core base; and the 155HL
        // shares the 165HL's 1.4 GHz P-core base despite its lower
        // 4.8 GHz turbo, so the base does not scale with the tier and
        // must be read per SKU. Staying out, recorded in the coverage
        // text: no further hardware family candidate is named; the
        // v2.1.84 audit's list (the Ryzen 8000G desktop parts, the
        // Ryzen 5000G parts, the Arc A310 GPU, the Core Ultra UL family
        // and the Core Ultra HL family) is now complete, so the next
        // cycle runs a fresh audit of the dataset against the vendor
        // catalogues before picking a slice; and the standing exclusions
        // (13th-gen HX P-core base, Core 7 245HX non-Ultra, Core 3 100U,
        // Ryzen AI 7 HX 455, Ryzen AI 400 mobile H variants), unchanged
        // in this cycle.
        const string appUrl = "https://cdrdv2-public.intel.com/874290/APP-for-Intel-Core-Ultra-Processors.pdf";

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 7 165HL", out var ultra7_165HL));
        Assert.Equal(1.4, ultra7_165HL.BaseGhz);
        Assert.Equal(5.0, ultra7_165HL.BoostGhz);
        Assert.Equal(3.8, ultra7_165HL.ECoreBoostGhz);
        Assert.Equal(appUrl, ultra7_165HL.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 7 155HL", out var ultra7_155HL));
        Assert.Equal(1.4, ultra7_155HL.BaseGhz);
        Assert.Equal(4.8, ultra7_155HL.BoostGhz);
        Assert.Equal(3.8, ultra7_155HL.ECoreBoostGhz);
        Assert.Equal(appUrl, ultra7_155HL.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 5 135HL", out var ultra5_135HL));
        Assert.Equal(1.7, ultra5_135HL.BaseGhz);
        Assert.Equal(4.6, ultra5_135HL.BoostGhz);
        Assert.Equal(3.6, ultra5_135HL.ECoreBoostGhz);
        Assert.Equal(appUrl, ultra5_135HL.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 5 125HL", out var ultra5_125HL));
        Assert.Equal(1.2, ultra5_125HL.BaseGhz);
        Assert.Equal(4.5, ultra5_125HL.BoostGhz);
        Assert.Equal(3.6, ultra5_125HL.ECoreBoostGhz);
        Assert.Equal(appUrl, ultra5_125HL.SourceUrl);
    }

    [Fact]
    public void Default_CoreUltra100HMobileCpusCarryVerifiedFigures()
    {
        // Dataset v1.50.0: the two missing Intel Core Ultra 100H mobile
        // CPUs (Meteor Lake-H, Intel 4, launched December 14, 2023),
        // completing the 100H family at five SKUs. Only the 185H, 155H
        // and 125H were in the dataset, all since v2.1.14; the other two
        // were never listed, the same unnoticed gap as the 100U family
        // in v2.1.85, the 200V family in v2.1.83 and the 200S family in
        // v2.1.84, because no cycle had listed the family against the
        // dataset. The Max Turbo figures (P-core and E-core) come from
        // Intel's own Core Ultra Processors Quick Reference Guide Phase
        // 2, read 2026-11-15, whose H-series table lists both parts with
        // the same turbos, cores, threads and cache; the same table lists
        // the three H parts already in the dataset at the same figures
        // (the 185H at 5.1 and 3.8 GHz, the 155H at 4.8 and 3.8 GHz, the
        // 125H at 4.5 and 3.6 GHz), which validates the column mapping.
        // The QRG publishes no P-core base frequency, so the P-core base
        // rests on agreeing ARK mirrors (nanoreview, topcpu, cpu-monkey,
        // eatyourbytes and the Clove spec sheets taken from Intel ARK),
        // the same disclosed mirror exception as the 100U parts in
        // v2.1.85, the Series 3 335, 332 and 322 in v2.1.77 and the 251HX
        // in v2.1.76. Two reading traps are recorded in the coverage
        // text: TechPowerUp's per-SKU page for the 165H says the chip
        // operates at 3.8 GHz by default, which is the E-core turbo, not
        // the P-core base, and cputronic repeats the same 3.8 GHz as the
        // P-core base, superseded by the agreeing ARK figures at 1.4
        // GHz; and the pre-launch WCCFTech leak table lists the 135H and
        // 125H base at 3.6 GHz, which is the E-core turbo mislabeled,
        // superseded by the launched ARK figures. Staying out, recorded
        // in the coverage text: the AMD Ryzen 8000G desktop parts, the
        // next candidate; the Ryzen 5000G parts (5600G, 5700G) and the
        // Arc A310, all recorded as future candidates by the v2.1.84
        // audit and not researched in this cycle; the Core Ultra UL
        // family (165UL, 155UL, 135UL, 125UL, 105UL), named in Intel's
        // own APP Metrics Revision 1; the Core Ultra HL family (165HL,
        // 155HL, 135HL, 125HL), higher-power H variants named in Intel's
        // own product list and not yet researched; and the standing
        // exclusions (13th-gen HX P-core base, Core 7 245HX non-Ultra,
        // Core 3 100U, Ryzen AI 7 HX 455, Ryzen AI 400 mobile H
        // variants), unchanged in this cycle.
        const string qrgUrl = "https://cdrdv2-public.intel.com/791727/Intel%20Core%20Ultra%20Processors%20Quick%20Reference%20Guide%20Phase%202.pdf";

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 7 165H", out var ultra7_165H));
        Assert.Equal(1.4, ultra7_165H.BaseGhz);
        Assert.Equal(5.0, ultra7_165H.BoostGhz);
        Assert.Equal(3.8, ultra7_165H.ECoreBoostGhz);
        Assert.Equal(qrgUrl, ultra7_165H.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 5 135H", out var ultra5_135H));
        Assert.Equal(1.7, ultra5_135H.BaseGhz);
        Assert.Equal(4.6, ultra5_135H.BoostGhz);
        Assert.Equal(3.6, ultra5_135H.ECoreBoostGhz);
        Assert.Equal(qrgUrl, ultra5_135H.SourceUrl);
    }

    [Fact]
    public void Default_Ryzen8000GDesktopCpusCarryVerifiedFigures()
    {
        // Dataset v1.51.0: the four AMD Ryzen 8000G desktop APUs (Hawk
        // Point, Zen 4 with RDNA 3 integrated graphics, TSMC 4nm FinFET,
        // AM5, 65 W default TDP), completing the 8000G family, which was
        // wholly absent before. Only the Ryzen 200 PRO and AI 400 G parts
        // were in the dataset; no 8000G part had ever been listed, the
        // same unnoticed gap as the 100H family in v2.1.86, the 100U
        // family in v2.1.85, the 200V family in v2.1.83 and the 200S
        // family in v2.1.84, because no cycle had listed the family
        // against the dataset. Every figure was verified by hand against
        // AMD's own product page per model, read 2026-11-15: the 8700G
        // page was fetched directly as page text (8 cores, 16 threads,
        // Max Boost Clock up to 5.1 GHz, Base Clock 4.2 GHz, L2 8 MB, L3
        // 16 MB, 65 W, launch Q1 2024), and the 8600G, 8500G and 8300G
        // pages were read through the search index of AMD's own product
        // pages, which publish the same fields per SKU. Two judgement
        // calls are recorded in the coverage text: the two hybrid parts
        // use AMD's headline Base Clock and Max Boost Clock, not the
        // per-core-type figures (the 8500G lists Zen4 Base Clock 4.1 GHz
        // and Zen4c Base Clock 3.3 GHz with Max Zen4c Clock up to 3.7
        // GHz; the 8300G lists Zen4 Base Clock 4.0 GHz and Zen4c Base
        // Clock 3.2 GHz with Max Zen4c Clock up to 3.6 GHz), the same
        // rule as the Ryzen 200 PRO hybrid parts in v2.1.81 and v2.1.82;
        // and the pre-launch leak tables list the base clocks at 4.35 GHz
        // for the 8600G, 3.55 GHz for the 8500G and 3.45 GHz for the 8300G,
        // while AMD's own pages list 4.3, 3.5 and 3.4 GHz, so AMD's pages
        // decide it. The 8300G is OEM only, sold through system
        // integrators, not retail. Staying out, recorded in the coverage
        // text: the Ryzen 5000G parts (5600G, 5700G), the next candidate;
        // the Arc A310 GPU; the Core Ultra UL family (165UL, 155UL,
        // 135UL, 125UL, 105UL); the Core Ultra HL family (165HL, 155HL,
        // 135HL, 125HL); and the standing exclusions (13th-gen HX P-core
        // base, Core 7 245HX non-Ultra, Core 3 100U, Ryzen AI 7 HX 455,
        // Ryzen AI 400 mobile H variants), unchanged in this cycle.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 7 8700G", out var ryzen7_8700G));
        Assert.Equal(4.2, ryzen7_8700G.BaseGhz);
        Assert.Equal(5.1, ryzen7_8700G.BoostGhz);
        Assert.Null(ryzen7_8700G.ECoreBoostGhz);
        Assert.Equal("https://www.amd.com/en/products/processors/desktops/ryzen/8000-series/amd-ryzen-7-8700g.html", ryzen7_8700G.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 8600G", out var ryzen5_8600G));
        Assert.Equal(4.3, ryzen5_8600G.BaseGhz);
        Assert.Equal(5.0, ryzen5_8600G.BoostGhz);
        Assert.Null(ryzen5_8600G.ECoreBoostGhz);
        Assert.Equal("https://www.amd.com/en/products/processors/desktops/ryzen/8000-series/amd-ryzen-5-8600g.html", ryzen5_8600G.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 8500G", out var ryzen5_8500G));
        Assert.Equal(3.5, ryzen5_8500G.BaseGhz);
        Assert.Equal(5.0, ryzen5_8500G.BoostGhz);
        Assert.Null(ryzen5_8500G.ECoreBoostGhz);
        Assert.Equal("https://www.amd.com/en/products/processors/desktops/ryzen/8000-series/amd-ryzen-5-8500g.html", ryzen5_8500G.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 3 8300G", out var ryzen3_8300G));
        Assert.Equal(3.4, ryzen3_8300G.BaseGhz);
        Assert.Equal(4.9, ryzen3_8300G.BoostGhz);
        Assert.Null(ryzen3_8300G.ECoreBoostGhz);
        Assert.Equal("https://www.amd.com/en/products/processors/desktops/ryzen/8000-series/amd-ryzen-3-8300g.html", ryzen3_8300G.SourceUrl);
    }

    [Fact]
    public void Default_Ryzen5000GDesktopCpusCarryVerifiedFigures()
    {
        // Dataset v1.52.0: the two AMD Ryzen 5000G desktop APUs (Cezanne,
        // Zen 3 with Radeon Vega integrated graphics, TSMC 7nm FinFET,
        // AM4, 65 W default TDP), completing the 5000G family, which was
        // wholly absent before. Only the 5000 X3D parts and the non-G 5000
        // parts were in the dataset; no 5000G part had ever been listed,
        // the same unnoticed gap as the 8000G family in v2.1.87, the 100H
        // family in v2.1.86, the 100U family in v2.1.85, the 200V family
        // in v2.1.83 and the 200S family in v2.1.84, because no cycle had
        // listed the family against the dataset. Every figure was verified
        // by hand against AMD's own product page per model, read
        // 2026-11-15, and both pages were fetched directly as page text:
        // the 5700G page (8 cores, 16 threads, Max Boost Clock up to 4.6
        // GHz, Base Clock 3.8 GHz, L2 4 MB, L3 16 MB, 65 W, launch April
        // 13, 2021, Radeon Graphics 8 compute units at 2000 MHz) and the
        // 5600G page (6 cores, 12 threads, Max Boost Clock up to 4.4 GHz,
        // Base Clock 3.9 GHz, L2 3 MB, L3 16 MB, 65 W, launch April 13,
        // 2021, Radeon Graphics 7 compute units at 1900 MHz). Staying
        // out, recorded in the coverage text: the Arc A310 GPU, the next
        // candidate; the Core Ultra UL family (165UL, 155UL, 135UL,
        // 125UL, 105UL); the Core Ultra HL family (165HL, 155HL, 135HL,
        // 125HL); and the standing exclusions (13th-gen HX P-core base,
        // Core 7 245HX non-Ultra, Core 3 100U, Ryzen AI 7 HX 455, Ryzen
        // AI 400 mobile H variants), unchanged in this cycle.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 7 5700G", out var ryzen7_5700G));
        Assert.Equal(3.8, ryzen7_5700G.BaseGhz);
        Assert.Equal(4.6, ryzen7_5700G.BoostGhz);
        Assert.Null(ryzen7_5700G.ECoreBoostGhz);
        Assert.Equal("https://www.amd.com/en/support/downloads/drivers.html/processors/ryzen/ryzen-5000-series/amd-ryzen-7-5700g.html", ryzen7_5700G.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 5600G", out var ryzen5_5600G));
        Assert.Equal(3.9, ryzen5_5600G.BaseGhz);
        Assert.Equal(4.4, ryzen5_5600G.BoostGhz);
        Assert.Null(ryzen5_5600G.ECoreBoostGhz);
        Assert.Equal("https://www.amd.com/en/support/downloads/drivers.html/processors/ryzen/ryzen-5000-series/amd-ryzen-5-5600g.html", ryzen5_5600G.SourceUrl);
    }

    [Fact]
    public void Default_Ryzen8000FDesktopCpusCarryVerifiedFigures()
    {
        // Dataset v1.56.0: the two AMD Ryzen 8000F desktop CPUs (Phoenix,
        // Zen 4, TSMC 4nm FinFET, AM5, 65 W default TDP), completing the
        // 8000F family, which was wholly absent before. Only the 8000G
        // parts were in the dataset; no 8000F part had ever been listed,
        // the same unnoticed gap as the 5000G family in v2.1.88, the 8000G
        // family in v2.1.87, the 100H family in v2.1.86, the 100U family
        // in v2.1.85, the 200V family in v2.1.83 and the 200S family in
        // v2.1.84, because no cycle had listed the family against the
        // dataset. The slice was picked by the fresh audit this cycle,
        // which checked the dataset against the vendor catalogues after
        // the v2.1.84 audit's list was completed in v2.1.91; the audit
        // also checked the Core Ultra 200U family (complete at four SKUs,
        // 265U, 255U, 235U, 225U, per Intel's own Quick Reference Guide),
        // the Core 200U family (complete at two SKUs, 250U, 220U), the
        // Core Ultra 200H family (complete at five SKUs, 285H, 265H,
        // 255H, 235H, 225H), the Ryzen 7000F desktop CPUs (7500F, 7400F),
        // the Ryzen 5 5600GT desktop APU and the Ryzen 5 5600T, 5600XT,
        // 5600F and 5500GT AM4 refresh parts, and the AMD Radeon RX 9070
        // GRE GPU (12 GB GDDR6, 192-bit, 432 GB/s, launched in China May
        // 8, 2025, global June 2, 2026). Every figure was verified by
        // hand against AMD's own product page per model, read 2026-11-15:
        // the 8700F page was fetched directly as page text (8 cores, 16
        // threads, Max Boost Clock up to 5 GHz, Base Clock 4.1 GHz, L2 8
        // MB, L3 16 MB, 65 W, launch 04/01/2024), and the 8400F page was
        // read through the search index of AMD's own product page, which
        // publishes the same fields per SKU (6 cores, 12 threads, Max
        // Boost Clock up to 4.7 GHz, Base Clock 4.2 GHz, L2 6 MB, L3 16
        // MB, 65 W, launch 04/01/2024). The 8400F is a hybrid Phoenix 2
        // part (2 Zen 4 plus 4 Zen 4c cores) and uses AMD's headline Base
        // Clock and Max Boost Clock, not the per-core-type figures, the
        // same rule as the Ryzen 200 PRO hybrid parts in v2.1.81 and
        // v2.1.82 and the 8000G hybrid parts in v2.1.87. One reading trap
        // is recorded: the pre-launch leak tables list the boost clocks
        // at 5.05 GHz for the 8700F and 4.75 GHz for the 8400F, while
        // AMD's own pages list 5.0 and 4.7 GHz, so AMD's pages decide it.
        // Both parts carry no integrated graphics and require a discrete
        // graphics card; the 8700F carries the Ryzen AI XDNA NPU at 16
        // TOPS and the 8400F carries no NPU. Staying out, recorded in the
        // coverage text: the Ryzen 7000F desktop CPUs (7500F, 7400F), the
        // next candidate; the Ryzen 5 5600GT desktop APU and the Ryzen 5
        // 5600T, 5600XT, 5600F and 5500GT AM4 refresh parts; the AMD
        // Radeon RX 9070 GRE GPU; and the standing exclusions (13th-gen
        // HX P-core base, Core 7 245HX non-Ultra, Core 3 100U, Ryzen AI 7
        // HX 455, Ryzen AI 400 mobile H variants), unchanged in this
        // cycle.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 7 8700F", out var ryzen7_8700F));
        Assert.Equal(4.1, ryzen7_8700F.BaseGhz);
        Assert.Equal(5.0, ryzen7_8700F.BoostGhz);
        Assert.Null(ryzen7_8700F.ECoreBoostGhz);
        Assert.Equal("https://www.amd.com/en/products/processors/desktops/ryzen/8000-series/amd-ryzen-7-8700f.html", ryzen7_8700F.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 8400F", out var ryzen5_8400F));
        Assert.Equal(4.2, ryzen5_8400F.BaseGhz);
        Assert.Equal(4.7, ryzen5_8400F.BoostGhz);
        Assert.Null(ryzen5_8400F.ECoreBoostGhz);
        Assert.Equal("https://www.amd.com/en/products/processors/desktops/ryzen/8000-series/amd-ryzen-5-8400f.html", ryzen5_8400F.SourceUrl);
    }

    [Fact]
    public void Default_Ryzen7000FDesktopCpusCarryVerifiedFigures()
    {
        // Dataset v1.57.0: the two AMD Ryzen 7000F desktop CPUs (Raphael,
        // Zen 4, TSMC 5nm FinFET, AM5, 65 W default TDP), completing the
        // 7000F family, which was wholly absent before. Only the 7000
        // parts without an F suffix were in the dataset; no 7000F part
        // had ever been listed, the same unnoticed gap as the 8000F
        // family in v2.1.92, the 5000G family in v2.1.88, the 8000G
        // family in v2.1.87, the 100H family in v2.1.86, the 100U family
        // in v2.1.85, the 200V family in v2.1.83 and the 200S family in
        // v2.1.84, because no cycle had listed the family against the
        // dataset. The slice was picked by the brief, which named the
        // Ryzen 7000F pair first among the v2.1.92 audit's three future
        // candidates; both parts verified against AMD's own product
        // pages, so the dependency-audit fallback was not needed and the
        // other two candidates (the Ryzen 5 5600GT desktop APU and the
        // Ryzen 5 5600T, 5600XT, 5600F and 5500GT AM4 refresh parts, and
        // the AMD Radeon RX 9070 GRE GPU) were not researched in this
        // cycle and are recorded in the coverage text as future
        // candidates. Every figure was verified by hand against AMD's
        // own product page per model, read 2026-11-15, and both pages
        // were fetched directly as page text: the 7500F page (6 cores,
        // 12 threads, Max Boost Clock up to 5 GHz, Base Clock 3.7 GHz,
        // L1 384 KB, L2 6 MB, L3 32 MB, 65 W, launch 07/22/2023, Global)
        // and the 7400F page (6 cores, 12 threads, Max Boost Clock up
        // to 4.7 GHz, Base Clock 3.7 GHz, L1 384 KB, L2 6 MB, L3 32 MB,
        // 65 W, launch 1/9/2025, China and APJ only). Both parts carry
        // no integrated graphics and require a discrete graphics card.
        // Staying out, recorded in the coverage text: the Ryzen 5
        // 5600GT desktop APU and the Ryzen 5 5600T, 5600XT, 5600F and
        // 5500GT AM4 refresh parts, the next candidate; the AMD Radeon
        // RX 9070 GRE GPU; and the standing exclusions (13th-gen HX
        // P-core base, Core 7 245HX non-Ultra, Core 3 100U, Ryzen AI 7
        // HX 455, Ryzen AI 400 mobile H variants), unchanged in this
        // cycle.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 7500F", out var ryzen5_7500F));
        Assert.Equal(3.7, ryzen5_7500F.BaseGhz);
        Assert.Equal(5.0, ryzen5_7500F.BoostGhz);
        Assert.Null(ryzen5_7500F.ECoreBoostGhz);
        Assert.Equal("https://www.amd.com/en/products/processors/desktops/ryzen/7000-series/amd-ryzen-5-7500f.html", ryzen5_7500F.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 7400F", out var ryzen5_7400F));
        Assert.Equal(3.7, ryzen5_7400F.BaseGhz);
        Assert.Equal(4.7, ryzen5_7400F.BoostGhz);
        Assert.Null(ryzen5_7400F.ECoreBoostGhz);
        Assert.Equal("https://www.amd.com/en/products/processors/desktops/ryzen/7000-series/amd-ryzen-5-7400f.html", ryzen5_7400F.SourceUrl);
    }

    [Fact]
    public void Default_Ryzen5000Am4RefreshCpusCarryVerifiedFigures()
    {
        // Dataset v1.58.0: the five AMD Ryzen 5000 AM4 refresh parts
        // (Zen 3, TSMC 7nm FinFET, AM4, 65 W default TDP), completing
        // the AM4 refresh set, which was wholly absent before. Only the
        // 5000 parts without a GT, T, XT or F suffix were in the dataset;
        // no refresh part had ever been listed, the same unnoticed gap
        // as the 7000F family in v2.1.93, the 8000F family in v2.1.92,
        // the 5000G family in v2.1.88, the 8000G family in v2.1.87, the
        // 100H family in v2.1.86, the 100U family in v2.1.85, the 200V
        // family in v2.1.83 and the 200S family in v2.1.84, because no
        // cycle had listed the family against the dataset. The slice was
        // picked by the brief, which named the 5600GT desktop APU plus
        // the 5600T, 5600XT, 5600F and 5500GT refresh singles first
        // among the v2.1.93 audit's two future candidates; all five
        // parts verified, so the dependency-audit fallback was not
        // needed and the other candidate (the AMD Radeon RX 9070 GRE
        // GPU) was not researched in this cycle and is recorded in the
        // coverage text as the next candidate. Every figure was verified
        // by hand against AMD's own pages, read 2026-11-15. The 5600T
        // page was fetched directly as page text (6 cores, 12 threads,
        // Max Boost Clock up to 4.5 GHz, Base Clock 3.5 GHz, L1 384 KB,
        // L2 3 MB, L3 32 MB, 65 W, launch 10/31/2024, Global) and the
        // 5600F page was fetched directly as page text (6 cores, 12
        // threads, Max Boost Clock up to 4.0 GHz, Base Clock 3.0 GHz,
        // L1 384 KB, L2 3 MB, L3 32 MB, 65 W, launch 9/16/25, APJ
        // only). The 5600XT figures were read from AMD's own processor
        // comparison page, fetched directly as page text, which lists
        // the 5600XT at Base Clock 3.7 GHz and Max Boost Clock up to
        // 4.7 GHz with L2 3 MB, L3 32 MB, 65 W and launch 10/31/2024;
        // no standalone AMD product page for the 5600XT was found, so
        // the comparison page URL is recorded as its source. The two GT
        // parts have no standalone AMD product page either: their
        // figures rest on AMD's own January 8, 2024 press release,
        // which lists the 5600GT at up to 4.6 GHz with Base Clock 3.6
        // GHz and the 5500GT at up to 4.4 GHz with Base Clock 3.6 GHz,
        // both 6 cores, 12 threads, 19 MB cache, 65 W, cross-checked
        // against agreeing third-party databases (cputronic, cpu-monkey,
        // pcbench, Gigabyte's CPU support list and the ASUS QVL), which
        // all agree on 3.6 and up to 4.6 GHz for the 5600GT and 3.6 and
        // up to 4.4 GHz for the 5500GT, with Radeon Vega 7 at up to
        // 1900 MHz. Three reading traps are recorded: the WCCFTech leak
        // table lists the 5600XT base clock at 3.8 GHz with a 105 W
        // TDP, while AMD's own comparison page lists 3.7 GHz and 65 W,
        // so AMD's page decides it; WCCFTech's prose lists the 5600T
        // base clock at 3.7 GHz, while AMD's own product page lists
        // 3.5 GHz, so AMD's page decides it; and one Flipkart listing
        // gives the 5600GT a 4.4 GHz boost, which is the 5500GT's
        // figure, not the 5600GT's. The two GT parts carry Radeon Vega
        // 7 integrated graphics; the three Vermeer parts (5600T,
        // 5600XT, 5600F) carry no integrated graphics and require a
        // discrete graphics card. Staying out, recorded in the coverage
        // text: the AMD Radeon RX 9070 GRE GPU, the next candidate; and
        // the standing exclusions (13th-gen HX P-core base, Core 7
        // 245HX non-Ultra, Core 3 100U, Ryzen AI 7 HX 455, Ryzen AI 400
        // mobile H variants), unchanged in this cycle.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 5600GT", out var ryzen5_5600GT));
        Assert.Equal(3.6, ryzen5_5600GT.BaseGhz);
        Assert.Equal(4.6, ryzen5_5600GT.BoostGhz);
        Assert.Null(ryzen5_5600GT.ECoreBoostGhz);
        Assert.Equal("https://www.nasdaq.com/press-release/amd-reveals-next-gen-desktop-processors-for-extreme-pc-gaming-and-creator-performance", ryzen5_5600GT.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 5500GT", out var ryzen5_5500GT));
        Assert.Equal(3.6, ryzen5_5500GT.BaseGhz);
        Assert.Equal(4.4, ryzen5_5500GT.BoostGhz);
        Assert.Null(ryzen5_5500GT.ECoreBoostGhz);
        Assert.Equal("https://www.nasdaq.com/press-release/amd-reveals-next-gen-desktop-processors-for-extreme-pc-gaming-and-creator-performance", ryzen5_5500GT.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 5600T", out var ryzen5_5600T));
        Assert.Equal(3.5, ryzen5_5600T.BaseGhz);
        Assert.Equal(4.5, ryzen5_5600T.BoostGhz);
        Assert.Null(ryzen5_5600T.ECoreBoostGhz);
        Assert.Equal("https://www.amd.com/en/products/processors/desktops/ryzen/5000-series/amd-ryzen-5-5600t.html", ryzen5_5600T.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 5600XT", out var ryzen5_5600XT));
        Assert.Equal(3.7, ryzen5_5600XT.BaseGhz);
        Assert.Equal(4.7, ryzen5_5600XT.BoostGhz);
        Assert.Null(ryzen5_5600XT.ECoreBoostGhz);
        Assert.Equal("https://www.amd.com/en/products/specifications/processors.html?product-0=AMD Ryzen\u2122 5 5600XT&product-1=AMD Ryzen\u2122 5 5600", ryzen5_5600XT.SourceUrl);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 5600F", out var ryzen5_5600F));
        Assert.Equal(3.0, ryzen5_5600F.BaseGhz);
        Assert.Equal(4.0, ryzen5_5600F.BoostGhz);
        Assert.Null(ryzen5_5600F.ECoreBoostGhz);
        Assert.Equal("https://www.amd.com/en/products/processors/desktops/ryzen/5000-series/amd-ryzen-5-5600f.html", ryzen5_5600F.SourceUrl);
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
    [Fact]
    public void Default_RyzenAi400DesktopGGeCpusCarryVerifiedFigures()
    {
        // Dataset v1.29.0: the six consumer AMD Ryzen AI 400 desktop G and
        // GE CPUs (Gorgon Point AM5, Zen 5, TSMC 4nm FinFET), the complete
        // consumer desktop half of the AI 400 family, which the dataset
        // never listed. All six launched on March 2, 2026. The G parts run
        // at 65W and the GE parts at 35W; the Ryzen AI 7 parts are 8-core
        // with Radeon 860M graphics and the Ryzen AI 5 parts are 6-core
        // with Radeon 840M graphics. All figures were verified by hand
        // against AMD's own product pages, read 2026-11-15 from fresh
        // fetches: the Ryzen AI 7 450G and 450GE at Base Clock 2.0 GHz and
        // Max Boost Clock up to 5.1 GHz; the Ryzen AI 5 440G and 440GE at
        // 2.0 and up to 4.8 GHz; the Ryzen AI 5 435G and 435GE at 2.0 and
        // up to 4.5 GHz. The family list comes from AMD's own March 2,
        // 2026 press release table (450G, 440G, 435G) plus the GE product
        // pages, which also exist on AMD's site: the 440G and 440GE middle
        // parts were not in the original brief, which named only 450G/GE
        // and 435G/GE, but they are real consumer parts, so all six are
        // added and the consumer desktop family is complete. The PRO
        // mirrors (Ryzen AI 7 PRO 450G/450GE, Ryzen AI 5 PRO 440G/440GE,
        // Ryzen AI 5 PRO 435G/435GE) stay out: consumer parts only. The
        // older Rembrandt Zen 3+ Ryzen 100 chips and the hybrid Ryzen 5
        // 126 stay out for a future cycle. No code change was needed: the
        // CPU normalizer already maps "AMD Ryzen AI 7 450G" to "RYZEN AI 7
        // 450G" and the other five names the same way, and the Settings
        // card shows the CPU count from the dataset, so it reads 144 with
        // no code change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 7 450G", out var ryzenAi7450G));
        Assert.Equal("AMD", ryzenAi7450G.Vendor);
        Assert.Equal(2.0, ryzenAi7450G.BaseGhz);
        Assert.Equal(5.1, ryzenAi7450G.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/desktops/ryzen/ai-400-series/amd-ryzen-ai-7-450g.html", ryzenAi7450G.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 7 450GE", out var ryzenAi7450GE));
        Assert.Equal("AMD", ryzenAi7450GE.Vendor);
        Assert.Equal(2.0, ryzenAi7450GE.BaseGhz);
        Assert.Equal(5.1, ryzenAi7450GE.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/desktops/ryzen/ai-400-series/amd-ryzen-ai-7-450ge.html", ryzenAi7450GE.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 5 440G", out var ryzenAi5440G));
        Assert.Equal("AMD", ryzenAi5440G.Vendor);
        Assert.Equal(2.0, ryzenAi5440G.BaseGhz);
        Assert.Equal(4.8, ryzenAi5440G.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/desktops/ryzen/ai-400-series/amd-ryzen-ai-5-440g.html", ryzenAi5440G.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 5 440GE", out var ryzenAi5440GE));
        Assert.Equal("AMD", ryzenAi5440GE.Vendor);
        Assert.Equal(2.0, ryzenAi5440GE.BaseGhz);
        Assert.Equal(4.8, ryzenAi5440GE.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/desktops/ryzen/ai-400-series/amd-ryzen-ai-5-440ge.html", ryzenAi5440GE.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 5 435G", out var ryzenAi5435G));
        Assert.Equal("AMD", ryzenAi5435G.Vendor);
        Assert.Equal(2.0, ryzenAi5435G.BaseGhz);
        Assert.Equal(4.5, ryzenAi5435G.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/desktops/ryzen/ai-400-series/amd-ryzen-ai-5-435g.html", ryzenAi5435G.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 5 435GE", out var ryzenAi5435GE));
        Assert.Equal("AMD", ryzenAi5435GE.Vendor);
        Assert.Equal(2.0, ryzenAi5435GE.BaseGhz);
        Assert.Equal(4.5, ryzenAi5435GE.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/desktops/ryzen/ai-400-series/amd-ryzen-ai-5-435ge.html", ryzenAi5435GE.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_Ryzen100RembrandtAndRyzen5126CpusCarryVerifiedFigures()
    {
        // Dataset v1.30.0: the five consumer AMD Ryzen 100 Rembrandt Zen 3+
        // mobile CPUs and the hybrid Ryzen 5 126, the half of the consumer
        // Ryzen 100 family the dataset never listed. With them the consumer
        // Ryzen 100 family is complete: the four Hawk Point CPUs (Ryzen 9
        // 180, Ryzen 7 165, Ryzen 7 155, Ryzen 5 125) shipped in v2.1.60.
        // The Rembrandt five are rebadged Ryzen 7035 (Rembrandt-R) parts,
        // TSMC 6nm FinFET, launched October 1, 2025: the Ryzen 7 170 and
        // 160 are 8-core with Radeon 680M graphics, the Ryzen 5 150 and
        // 130 are 6-core with Radeon 660M graphics, and the Ryzen 3 110
        // is 4-core with Radeon 660M graphics. The Ryzen 5 126 is the
        // hybrid Hawk Point part: one Zen 4 core plus three Zen 4c cores
        // on the FP8 socket, TSMC 4nm FinFET, with Radeon 740M graphics.
        // All figures were verified by hand against AMD's own product
        // pages, read 2026-11-15 from fresh fetches: the Ryzen 7 170 at
        // Base Clock 3.2 GHz and Max Boost Clock up to 4.75 GHz; the Ryzen
        // 7 160 at 2.7 and up to 4.75 GHz; the Ryzen 5 150 at 3.3 and up to
        // 4.55 GHz; the Ryzen 5 130 at 2.9 and up to 4.55 GHz; the Ryzen 3
        // 110 at 3.0 and up to 4.3 GHz; the Ryzen 5 126 at 2.8 and up to
        // 4.6 GHz. The brief named the 160 as a Ryzen 5, but AMD's own
        // product page lists it as Ryzen 7 160 (8 cores, 16 threads), so
        // that is the name added. The Ryzen 5 126 carries a single
        // headline Base Clock of 2.8 GHz on AMD's page, so that is the
        // base_ghz recorded. No code change was needed: the CPU normalizer
        // already maps "AMD Ryzen 7 170" to "RYZEN 7 170" and the other
        // five names the same way, and the Settings card shows the CPU
        // count from the dataset, so it reads 150 with no code change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 7 170", out var ryzen7170));
        Assert.Equal("AMD", ryzen7170.Vendor);
        Assert.Equal(3.2, ryzen7170.BaseGhz);
        Assert.Equal(4.75, ryzen7170.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/100-series/amd-ryzen-7-170.html", ryzen7170.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 7 160", out var ryzen7160));
        Assert.Equal("AMD", ryzen7160.Vendor);
        Assert.Equal(2.7, ryzen7160.BaseGhz);
        Assert.Equal(4.75, ryzen7160.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/100-series/amd-ryzen-7-160.html", ryzen7160.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 150", out var ryzen5150));
        Assert.Equal("AMD", ryzen5150.Vendor);
        Assert.Equal(3.3, ryzen5150.BaseGhz);
        Assert.Equal(4.55, ryzen5150.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/100-series/amd-ryzen-5-150.html", ryzen5150.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 130", out var ryzen5130));
        Assert.Equal("AMD", ryzen5130.Vendor);
        Assert.Equal(2.9, ryzen5130.BaseGhz);
        Assert.Equal(4.55, ryzen5130.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/100-series/amd-ryzen-5-130.html", ryzen5130.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 3 110", out var ryzen3110));
        Assert.Equal("AMD", ryzen3110.Vendor);
        Assert.Equal(3.0, ryzen3110.BaseGhz);
        Assert.Equal(4.3, ryzen3110.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/100-series/amd-ryzen-3-110.html", ryzen3110.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen 5 126", out var ryzen5126));
        Assert.Equal("AMD", ryzen5126.Vendor);
        Assert.Equal(2.8, ryzen5126.BaseGhz);
        Assert.Equal(4.6, ryzen5126.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen/100-series/amd-ryzen-5-126.html", ryzen5126.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_CoreUltraSeries3CpusCarryVerifiedFigures()
    {
        // Dataset v1.31.0: twelve Intel Core Ultra Series 3 (Panther Lake)
        // mobile CPUs, the family the dataset never listed. Launched
        // January 5, 2026 on Intel 18A, retail in Q1 2026. All figures
        // were verified by hand against Intel's own compare pages: eleven
        // SKUs read 2026-11-13 from the compare page listing 368H, 388H,
        // 358H, 386H, 366H, 338H, 356H, 336H, 365, 325 and 355, and the
        // X9 378H read 2026-11-12 from the compare page listing 368H,
        // 388H, 358H and 378H. base_ghz is Intel's Performance-core Base
        // Frequency, boost_ghz is the Max Turbo Frequency, and
        // e_core_boost_ghz is the Efficient-core Max Turbo Frequency.
        // The 388H P-core base is 2.1 GHz on Intel's own page; third-party
        // listings disagree (2.1 vs 2.2 GHz) and Intel's page decides it.
        // The 378H carries identical clocks and cores to the 368H and
        // differs only in vPro eligibility. The 365, 355 and 325 have no
        // standard E-cores, so their e_core_boost_ghz carries the Low
        // Power Efficient-core Max Turbo Frequency, the same rule the
        // dataset already uses for the Core Ultra 200V parts, and the
        // boost definition now says so. The 335, 332 and 322 stay out:
        // their Performance-core Base Frequency figures could not be
        // verified live on Intel's own page in this cycle, so the
        // coverage text names them as not included, the same rule that
        // kept the 245HX and 235HX out. No code change was needed: the
        // CPU normalizer already maps "Intel Core Ultra X9 388H" to
        // "ULTRA X9 388H" and the other eleven names the same way, and
        // the Settings card shows the CPU count from the dataset, so it
        // reads 162 with no code change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra X7 368H", out var ultraX7368H));
        Assert.Equal("Intel", ultraX7368H.Vendor);
        Assert.Equal(2.0, ultraX7368H.BaseGhz);
        Assert.Equal(5.0, ultraX7368H.BoostGhz);
        Assert.Equal(3.8, ultraX7368H.ECoreBoostGhz);
        Assert.StartsWith("https://www.intel.com/content/www/us/en/products/compare.html?productIds=245523", ultraX7368H.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra X9 388H", out var ultraX9388H));
        Assert.Equal("Intel", ultraX9388H.Vendor);
        Assert.Equal(2.1, ultraX9388H.BaseGhz);
        Assert.Equal(5.1, ultraX9388H.BoostGhz);
        Assert.Equal(3.8, ultraX9388H.ECoreBoostGhz);
        Assert.StartsWith("https://www.intel.com/content/www/us/en/products/compare.html?productIds=245526", ultraX9388H.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra X9 378H", out var ultraX9378H));
        Assert.Equal("Intel", ultraX9378H.Vendor);
        Assert.Equal(2.0, ultraX9378H.BaseGhz);
        Assert.Equal(5.0, ultraX9378H.BoostGhz);
        Assert.Equal(3.8, ultraX9378H.ECoreBoostGhz);
        Assert.StartsWith("https://www.intel.com/content/www/us/en/products/compare.html?productIds=246128", ultraX9378H.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra X7 358H", out var ultraX7358H));
        Assert.Equal("Intel", ultraX7358H.Vendor);
        Assert.Equal(1.9, ultraX7358H.BaseGhz);
        Assert.Equal(4.8, ultraX7358H.BoostGhz);
        Assert.Equal(3.5, ultraX7358H.ECoreBoostGhz);
        Assert.StartsWith("https://www.intel.com/content/www/us/en/products/compare.html?productIds=245527", ultraX7358H.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 9 386H", out var ultra9386H));
        Assert.Equal("Intel", ultra9386H.Vendor);
        Assert.Equal(2.1, ultra9386H.BaseGhz);
        Assert.Equal(4.9, ultra9386H.BoostGhz);
        Assert.Equal(3.7, ultra9386H.ECoreBoostGhz);
        Assert.StartsWith("https://www.intel.com/content/www/us/en/products/compare.html?productIds=245529", ultra9386H.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 7 366H", out var ultra7366H));
        Assert.Equal("Intel", ultra7366H.Vendor);
        Assert.Equal(2.0, ultra7366H.BaseGhz);
        Assert.Equal(4.8, ultra7366H.BoostGhz);
        Assert.Equal(3.6, ultra7366H.ECoreBoostGhz);
        Assert.StartsWith("https://www.intel.com/content/www/us/en/products/compare.html?productIds=245530", ultra7366H.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 5 338H", out var ultra5338H));
        Assert.Equal("Intel", ultra5338H.Vendor);
        Assert.Equal(1.9, ultra5338H.BaseGhz);
        Assert.Equal(4.7, ultra5338H.BoostGhz);
        Assert.Equal(3.4, ultra5338H.ECoreBoostGhz);
        Assert.StartsWith("https://www.intel.com/content/www/us/en/products/compare.html?productIds=245531", ultra5338H.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 7 356H", out var ultra7356H));
        Assert.Equal("Intel", ultra7356H.Vendor);
        Assert.Equal(1.9, ultra7356H.BaseGhz);
        Assert.Equal(4.7, ultra7356H.BoostGhz);
        Assert.Equal(3.5, ultra7356H.ECoreBoostGhz);
        Assert.StartsWith("https://www.intel.com/content/www/us/en/products/compare.html?productIds=245532", ultra7356H.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 5 336H", out var ultra5336H));
        Assert.Equal("Intel", ultra5336H.Vendor);
        Assert.Equal(1.9, ultra5336H.BaseGhz);
        Assert.Equal(4.6, ultra5336H.BoostGhz);
        Assert.Equal(3.4, ultra5336H.ECoreBoostGhz);
        Assert.StartsWith("https://www.intel.com/content/www/us/en/products/compare.html?productIds=245533", ultra5336H.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 7 365", out var ultra7365));
        Assert.Equal("Intel", ultra7365.Vendor);
        Assert.Equal(2.4, ultra7365.BaseGhz);
        Assert.Equal(4.8, ultra7365.BoostGhz);
        Assert.Equal(3.6, ultra7365.ECoreBoostGhz);
        Assert.StartsWith("https://www.intel.com/content/www/us/en/products/compare.html?productIds=245713", ultra7365.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 5 325", out var ultra5325));
        Assert.Equal("Intel", ultra5325.Vendor);
        Assert.Equal(2.1, ultra5325.BaseGhz);
        Assert.Equal(4.5, ultra5325.BoostGhz);
        Assert.Equal(3.4, ultra5325.ECoreBoostGhz);
        Assert.StartsWith("https://www.intel.com/content/www/us/en/products/compare.html?productIds=245720", ultra5325.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 7 355", out var ultra7355));
        Assert.Equal("Intel", ultra7355.Vendor);
        Assert.Equal(2.3, ultra7355.BaseGhz);
        Assert.Equal(4.7, ultra7355.BoostGhz);
        Assert.Equal(3.5, ultra7355.ECoreBoostGhz);
        Assert.StartsWith("https://www.intel.com/content/www/us/en/products/compare.html?productIds=245722", ultra7355.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_RyzenAi400ProDesktopGGeCpusCarryVerifiedFigures()
    {
        // Dataset v1.32.0: the six AMD Ryzen AI PRO 400 Series desktop G
        // and GE CPUs (Gorgon Point AM5, Zen 5, TSMC 4nm FinFET), the PRO
        // mirrors of the six consumer desktop parts added in v1.29.0,
        // which complete the desktop half of the AI 400 family. All six
        // launched on March 2, 2026 (AMD press release, MWC 2026); the G
        // parts run at 65W and the GE parts at 35W; the Ryzen AI 7 PRO
        // parts are 8-core with Radeon 860M graphics and the Ryzen AI 5
        // PRO parts are 6-core with Radeon 840M graphics. Every figure
        // was verified by hand against AMD's own product page for that
        // exact model, read 2026-11-15: the Ryzen AI 7 PRO 450G and
        // 450GE at Base Clock 2.0 GHz and Max Boost Clock up to 5.1 GHz;
        // the Ryzen AI 5 PRO 440G and 440GE at 2.0 and up to 4.8 GHz;
        // the Ryzen AI 5 PRO 435G and 435GE at 2.0 and up to 4.5 GHz.
        // The PRO clocks are identical to their consumer mirrors; the
        // PRO parts add AMD PRO Technologies (security, manageability,
        // 18-month feature stability, 36-month support), which is why
        // they are separate dataset entries. The slice was picked by a
        // fresh audit: the brief named Core Ultra Series 3 desktop, but
        // Intel's own Series 3 Quick Reference Guide lists only mobile
        // H, non-H and U parts, so no Series 3 desktop SKUs exist and
        // there is nothing to add there; the coverage text now says so.
        // No code change was needed: the CPU normalizer already maps
        // "AMD Ryzen AI 7 PRO 450G" to "RYZEN AI 7 PRO 450G" and the
        // other five names the same way, and the Settings card shows the
        // CPU count from the dataset, so it reads 168 with no code change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 7 PRO 450G", out var ryzenAi7Pro450G));
        Assert.Equal("AMD", ryzenAi7Pro450G.Vendor);
        Assert.Equal(2.0, ryzenAi7Pro450G.BaseGhz);
        Assert.Equal(5.1, ryzenAi7Pro450G.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/desktops/ryzen-pro/ai-400-series/amd-ryzen-ai-7-pro-450g.html", ryzenAi7Pro450G.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 7 PRO 450GE", out var ryzenAi7Pro450GE));
        Assert.Equal("AMD", ryzenAi7Pro450GE.Vendor);
        Assert.Equal(2.0, ryzenAi7Pro450GE.BaseGhz);
        Assert.Equal(5.1, ryzenAi7Pro450GE.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/desktops/ryzen-pro/ai-400-series/amd-ryzen-ai-7-pro-450ge.html", ryzenAi7Pro450GE.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 5 PRO 440G", out var ryzenAi5Pro440G));
        Assert.Equal("AMD", ryzenAi5Pro440G.Vendor);
        Assert.Equal(2.0, ryzenAi5Pro440G.BaseGhz);
        Assert.Equal(4.8, ryzenAi5Pro440G.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/desktops/ryzen-pro/ai-400-series/amd-ryzen-ai-5-pro-440g.html", ryzenAi5Pro440G.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 5 PRO 440GE", out var ryzenAi5Pro440GE));
        Assert.Equal("AMD", ryzenAi5Pro440GE.Vendor);
        Assert.Equal(2.0, ryzenAi5Pro440GE.BaseGhz);
        Assert.Equal(4.8, ryzenAi5Pro440GE.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/desktops/ryzen-pro/ai-400-series/amd-ryzen-ai-5-pro-440ge.html", ryzenAi5Pro440GE.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 5 PRO 435G", out var ryzenAi5Pro435G));
        Assert.Equal("AMD", ryzenAi5Pro435G.Vendor);
        Assert.Equal(2.0, ryzenAi5Pro435G.BaseGhz);
        Assert.Equal(4.5, ryzenAi5Pro435G.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/desktops/ryzen-pro/ai-400-series/amd-ryzen-ai-5-pro-435g.html", ryzenAi5Pro435G.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 5 PRO 435GE", out var ryzenAi5Pro435GE));
        Assert.Equal("AMD", ryzenAi5Pro435GE.Vendor);
        Assert.Equal(2.0, ryzenAi5Pro435GE.BaseGhz);
        Assert.Equal(4.5, ryzenAi5Pro435GE.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/desktops/ryzen-pro/ai-400-series/amd-ryzen-ai-5-pro-435ge.html", ryzenAi5Pro435GE.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_RyzenAi400ProMobileCpusCarryVerifiedFigures()
    {
        // Dataset v1.33.0: the six AMD Ryzen AI PRO 400 Series mobile
        // CPUs (Gorgon Point FP8, Zen 5 and Zen 5c, TSMC 4nm FinFET),
        // the PRO mirrors of the six consumer mobile parts already in
        // the dataset, which complete the AI 400 family: consumer and
        // PRO, desktop and mobile. All six launched January 5, 2026
        // (CES 2026, announced alongside the consumer mobile parts);
        // the Ryzen AI 9 HX PRO parts are 12-core with Radeon 890M
        // graphics, the Ryzen AI 9 PRO 465 is 10-core with Radeon
        // 880M, the Ryzen AI 7 PRO 450 is 8-core with Radeon 860M,
        // and the Ryzen AI 5 PRO parts are 6-core with Radeon 840M.
        // Every figure was verified by hand against AMD's own product
        // page for that exact model, read 2026-11-15: the Ryzen AI 9
        // HX PRO 475 and HX PRO 470 at Base Clock 2.0 GHz and Max
        // Boost Clock up to 5.2 GHz; the Ryzen AI 9 PRO 465 at 2.0
        // and up to 5.0 GHz; the Ryzen AI 7 PRO 450 at 2.0 and up to
        // 5.1 GHz; the Ryzen AI 5 PRO 440 at 2.0 and up to 4.8 GHz;
        // the Ryzen AI 5 PRO 435 at 2.0 and up to 4.5 GHz. The PRO
        // clocks are identical to their consumer mirrors; the PRO
        // parts add AMD PRO Technologies (security, manageability,
        // 18-month feature stability, 36-month support), which is why
        // they are separate dataset entries. Two judgement calls are
        // recorded. First, the HX PRO 470 is identical to the HX PRO
        // 475 on clocks, cores, cache and graphics and differs only
        // in NPU (55 TOPS against 60), so both are listed. Second,
        // the H variants of the mobile parts (H PRO 450, H PRO 465
        // and the rest) stay out: no separate AMD product pages with
        // their own clock figures surfaced for them in this cycle.
        // The slice was picked by a fresh audit: the brief named
        // Ryzen 9000G desktop APUs first, but no such product exists,
        // the name was a rumor and AMD never announced a desktop part
        // under it, and the real Zen 5 desktop APU family is the
        // Ryzen AI 400 Series announced March 2, 2026, already in the
        // dataset; the audit then found the six PRO mobile parts
        // absent (zero PRO mobile models of any family). No code
        // change was needed: the CPU normalizer already maps "AMD
        // Ryzen AI 9 HX PRO 475" to "RYZEN AI 9 HX PRO 475" and the
        // other five names the same way, and the Settings card shows
        // the CPU count from the dataset, so it reads 174 with no
        // code change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 9 HX PRO 475", out var ryzenAi9HxPro475));
        Assert.Equal("AMD", ryzenAi9HxPro475.Vendor);
        Assert.Equal(2.0, ryzenAi9HxPro475.BaseGhz);
        Assert.Equal(5.2, ryzenAi9HxPro475.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen-pro/ai-400-series/amd-ryzen-ai-9-hx-pro-475.html", ryzenAi9HxPro475.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 9 HX PRO 470", out var ryzenAi9HxPro470));
        Assert.Equal("AMD", ryzenAi9HxPro470.Vendor);
        Assert.Equal(2.0, ryzenAi9HxPro470.BaseGhz);
        Assert.Equal(5.2, ryzenAi9HxPro470.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen-pro/ai-400-series/amd-ryzen-ai-9-hx-pro-470.html", ryzenAi9HxPro470.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 9 PRO 465", out var ryzenAi9Pro465));
        Assert.Equal("AMD", ryzenAi9Pro465.Vendor);
        Assert.Equal(2.0, ryzenAi9Pro465.BaseGhz);
        Assert.Equal(5.0, ryzenAi9Pro465.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen-pro/ai-400-series/amd-ryzen-ai-9-pro-465.html", ryzenAi9Pro465.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 7 PRO 450", out var ryzenAi7Pro450));
        Assert.Equal("AMD", ryzenAi7Pro450.Vendor);
        Assert.Equal(2.0, ryzenAi7Pro450.BaseGhz);
        Assert.Equal(5.1, ryzenAi7Pro450.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen-pro/ai-400-series/amd-ryzen-ai-7-pro-450.html", ryzenAi7Pro450.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 5 PRO 440", out var ryzenAi5Pro440));
        Assert.Equal("AMD", ryzenAi5Pro440.Vendor);
        Assert.Equal(2.0, ryzenAi5Pro440.BaseGhz);
        Assert.Equal(4.8, ryzenAi5Pro440.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen-pro/ai-400-series/amd-ryzen-ai-5-pro-440.html", ryzenAi5Pro440.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 5 PRO 435", out var ryzenAi5Pro435));
        Assert.Equal("AMD", ryzenAi5Pro435.Vendor);
        Assert.Equal(2.0, ryzenAi5Pro435.BaseGhz);
        Assert.Equal(4.5, ryzenAi5Pro435.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen-pro/ai-400-series/amd-ryzen-ai-5-pro-435.html", ryzenAi5Pro435.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_CoreUltra200UMobileCpusCarryVerifiedFigures()
    {
        // Dataset v1.34.0: the four Intel Core Ultra 200U Series mobile
        // CPUs (Arrow Lake-U, launched January 6, 2025, 12 cores with
        // 2 P-cores, 8 E-cores and 2 Low Power Efficient-cores, 14
        // threads), the 15 W family the dataset never listed: the Core
        // Ultra 7 265U and 255U and the Core Ultra 5 235U and 225U.
        // Every figure was compiled by hand from Intel's own Quick
        // Reference Guide for the 200H and 200U series, v1.1
        // (cdrdv2-public.intel.com, read 2026-11-15), which publishes
        // the Performance-core Base Frequency and the Max Turbo
        // Frequency per SKU in its table, and each figure was
        // cross-checked per SKU against ARK mirrors that agree on all
        // four models (nanoreview, topcpu, siliconcat, cputronic):
        // the 265U at Performance-core Base Frequency 2.1 GHz and Max
        // Turbo Frequency up to 5.3 GHz with E-core Max Turbo
        // Frequency 4.2 GHz; the 255U at 2.0 and up to 5.2 GHz with
        // E-core turbo 4.2 GHz; the 235U at 2.0 and up to 4.9 GHz with
        // E-core turbo 4.1 GHz; the 225U at 1.5 and up to 4.8 GHz with
        // E-core turbo 3.8 GHz. The dataset's base_ghz is Intel's
        // Performance-core Base Frequency, boost_ghz is Intel's Max
        // Turbo Frequency, and e_core_boost_ghz is Intel's E-core Max
        // Turbo Frequency, per its own definition. Two judgement calls
        // are recorded. First, the 265U P-core base is 2.1 GHz in the
        // launched ARK figures and in the Quick Reference Guide, while
        // pre-launch listings and Intel's own December 2024 edge
        // product brief list 1.8 GHz at 15 W; the launched figure
        // decides it and the 1.8 GHz figure is rejected as pre-launch.
        // Second, the trailing numbers in the Quick Reference Guide
        // table (2.1, 2.1, 2.05 and 2.0 GHz) are the graphics Max
        // Frequency, not E-core base figures, so they are not recorded
        // as base figures anywhere. The Raptor Lake-based Core 200U
        // (non-Ultra) models stay out: they are a separate family from
        // the Core Ultra 200U family and are not in this slice. No code
        // change was needed: the CPU normalizer already maps "Intel
        // Core Ultra 7 265U" to "ULTRA 7 265U" and the other three
        // names the same way, and the Settings card shows the CPU count
        // from the dataset, so it reads 178 with no code change. One
        // honest limitation is recorded: Intel's own compare pages
        // return HTTP 403 in this sandbox, so no live compare-page
        // spot-check was possible in this cycle; the figures rest on
        // Intel's own Quick Reference Guide PDF plus the four agreeing
        // ARK mirrors named above.
        const string qrg = "https://cdrdv2-public.intel.com/842536/Intel%20Core%20Ultra%20200H%20and%20200U%20Series%20Processors%20-%20Quick%20Reference%20Guide%20v1.1.pdf";

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 7 265U", out var ultra7265U));
        Assert.Equal("Intel", ultra7265U.Vendor);
        Assert.Equal(2.1, ultra7265U.BaseGhz);
        Assert.Equal(5.3, ultra7265U.BoostGhz);
        Assert.Equal(4.2, ultra7265U.ECoreBoostGhz);
        Assert.StartsWith(qrg, ultra7265U.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 7 255U", out var ultra7255U));
        Assert.Equal("Intel", ultra7255U.Vendor);
        Assert.Equal(2.0, ultra7255U.BaseGhz);
        Assert.Equal(5.2, ultra7255U.BoostGhz);
        Assert.Equal(4.2, ultra7255U.ECoreBoostGhz);
        Assert.StartsWith(qrg, ultra7255U.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 5 235U", out var ultra5235U));
        Assert.Equal("Intel", ultra5235U.Vendor);
        Assert.Equal(2.0, ultra5235U.BaseGhz);
        Assert.Equal(4.9, ultra5235U.BoostGhz);
        Assert.Equal(4.1, ultra5235U.ECoreBoostGhz);
        Assert.StartsWith(qrg, ultra5235U.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 5 225U", out var ultra5225U));
        Assert.Equal("Intel", ultra5225U.Vendor);
        Assert.Equal(1.5, ultra5225U.BaseGhz);
        Assert.Equal(4.8, ultra5225U.BoostGhz);
        Assert.Equal(3.8, ultra5225U.ECoreBoostGhz);
        Assert.StartsWith(qrg, ultra5225U.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_RyzenAi300ProMobileCpusCarryVerifiedFigures()
    {
        // Dataset v1.35.0: the five AMD Ryzen AI PRO 300 Series mobile
        // CPUs, the PRO mirrors of the consumer AI 300 mobile parts
        // already in the dataset. The Ryzen AI 9 HX PRO 375 and HX PRO
        // 370 and the Ryzen AI 7 PRO 360 are Strix Point FP8 (Zen 5 and
        // Zen 5c, TSMC 4nm FinFET); the Ryzen AI 7 PRO 350 and Ryzen AI
        // 5 PRO 340 are Krackan Point FP8. The HX PRO parts are 12-core
        // (4 Zen 5 plus 8 Zen 5c) with Radeon 890M graphics, the PRO 360
        // is 8-core (3 Zen 5 plus 5 Zen 5c) with Radeon 880M, the PRO 350
        // is 8-core (4 Zen 5 plus 4 Zen 5c) with Radeon 860M, and the PRO
        // 340 is 6-core (3 Zen 5 plus 3 Zen 5c) with Radeon 840M. Every
        // figure was verified by hand against AMD's own product page for
        // that exact model, read 2026-11-15: the HX PRO 375 and HX PRO
        // 370 at Base Clock 2.0 GHz and Max Boost Clock up to 5.1 GHz;
        // the PRO 360 and PRO 350 at 2.0 and up to 5.0 GHz; the PRO 340
        // at 2.0 and up to 4.8 GHz. The PRO clocks are identical to their
        // consumer mirrors where a consumer twin exists (the HX PRO 375
        // and HX PRO 370 mirror the HX 375 and HX 370, the PRO 350
        // mirrors the AI 7 350, the PRO 340 mirrors the AI 5 340); the
        // PRO 360 has no consumer twin in the dataset. The PRO parts are
        // separate dataset entries because they add AMD PRO Technologies
        // (security, manageability, 18-month feature stability, 36-month
        // support), not because any clock differs. Two judgement calls
        // are recorded. First, AMD's January 2026 mobile-for-business
        // Quick Reference Guide lists only four PRO 300 models and omits
        // the PRO 340, but AMD's own product page for the PRO 340 exists
        // with full specifications, so all five are listed. Second,
        // AMD's current product pages show a launch date of 10/10/2025
        // for the three Strix Point parts, while the PRO 300 series was
        // announced October 10, 2024; the day and month match and the
        // year does not, and the dataset records no launch date, so the
        // discrepancy affects nothing. The brief's fallbacks were not
        // needed and were not researched in this cycle: the Intel
        // 13th-gen HX P-core Base Frequency rows and any PRO-adjacent
        // SKUs remain open for a later cycle. No code change was needed:
        // the CPU normalizer already maps "AMD Ryzen AI 9 HX PRO 375"
        // to "RYZEN AI 9 HX PRO 375" and the other four names the same
        // way, and the Settings card shows the CPU count from the
        // dataset, so it reads 183 with no code change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 9 HX PRO 375", out var ryzenAi9HxPro375));
        Assert.Equal("AMD", ryzenAi9HxPro375.Vendor);
        Assert.Equal(2.0, ryzenAi9HxPro375.BaseGhz);
        Assert.Equal(5.1, ryzenAi9HxPro375.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen-pro/ai-300-series/amd-ryzen-ai-9-hx-pro-375.html", ryzenAi9HxPro375.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 9 HX PRO 370", out var ryzenAi9HxPro370));
        Assert.Equal("AMD", ryzenAi9HxPro370.Vendor);
        Assert.Equal(2.0, ryzenAi9HxPro370.BaseGhz);
        Assert.Equal(5.1, ryzenAi9HxPro370.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen-pro/ai-300-series/amd-ryzen-ai-9-hx-pro-370.html", ryzenAi9HxPro370.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 7 PRO 360", out var ryzenAi7Pro360));
        Assert.Equal("AMD", ryzenAi7Pro360.Vendor);
        Assert.Equal(2.0, ryzenAi7Pro360.BaseGhz);
        Assert.Equal(5.0, ryzenAi7Pro360.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen-pro/ai-300-series/amd-ryzen-ai-7-pro-360.html", ryzenAi7Pro360.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 7 PRO 350", out var ryzenAi7Pro350));
        Assert.Equal("AMD", ryzenAi7Pro350.Vendor);
        Assert.Equal(2.0, ryzenAi7Pro350.BaseGhz);
        Assert.Equal(5.0, ryzenAi7Pro350.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen-pro/ai-300-series/amd-ryzen-ai-7-pro-350.html", ryzenAi7Pro350.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI 5 PRO 340", out var ryzenAi5Pro340));
        Assert.Equal("AMD", ryzenAi5Pro340.Vendor);
        Assert.Equal(2.0, ryzenAi5Pro340.BaseGhz);
        Assert.Equal(4.8, ryzenAi5Pro340.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen-pro/ai-300-series/amd-ryzen-ai-5-pro-340.html", ryzenAi5Pro340.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_RyzenAiMaxPro300MobileCpusCarryVerifiedFigures()
    {
        // Dataset v1.36.0: the four AMD Ryzen AI Max PRO 300 Series
        // mobile CPUs, the PRO mirrors of the consumer AI Max family
        // already in the dataset. All four are Strix Halo FP11 (Zen 5,
        // TSMC 4nm FinFET), announced January 6, 2025 at CES 2025 as the
        // commercial Max PRO family. The Ryzen AI Max+ PRO 395 is
        // 16-core (16 Zen 5) with Radeon 8060S graphics and up to 50
        // TOPS of NPU performance; the Ryzen AI Max PRO 390 is 12-core
        // with Radeon 8050S; the Ryzen AI Max PRO 385 is 8-core with
        // Radeon 8050S; the Ryzen AI Max PRO 380 is 6-core with Radeon
        // 8040S. Every figure was verified by hand against AMD's own
        // product page for that exact model, read 2026-11-15 from fresh
        // fetches: the Max+ PRO 395 at Base Clock 3.0 GHz and Max Boost
        // Clock up to 5.1 GHz; the Max PRO 390 at 3.2 and up to 5.0 GHz;
        // the Max PRO 385 at 3.6 and up to 5.0 GHz; the Max PRO 380 at
        // 3.6 and up to 4.9 GHz. The figures were cross-checked against
        // AMD's January 2026 mobile-for-business Quick Reference Guide,
        // which lists the same four models with the same clocks, and the
        // CES 2025 press release, which announces the Max+ PRO 395 at
        // up to 5.1 GHz and 3.0 GHz base. The PRO clocks are identical
        // to their consumer mirrors where a consumer twin exists in the
        // dataset (the Max+ PRO 395 mirrors the Max+ 395, the Max PRO
        // 390 mirrors the Max 390, the Max PRO 385 mirrors the Max
        // 385); the Max PRO 380 has no consumer twin in the dataset,
        // the consumer Max family lists only the 395, 392, 390, 388 and
        // 385. The PRO parts are separate dataset entries because they
        // add AMD PRO Technologies (security, manageability, 18-month
        // feature stability, 36-month support), not because any clock
        // differs. The slice was picked by a fresh audit: the brief
        // named the Intel 13th-gen HX P-core Base Frequency rows first,
        // and that target was re-checked in this cycle and still fails.
        // Intel's own Compare Products page, crawled 2026-11-14, shows
        // the Performance-core Base Frequency row for the i9-13980HX
        // with an empty cell, so the 13980HX P-core base is still not
        // published by Intel and nothing is added for it; the Intel
        // datasheet gives P-core base per configuration, not per SKU,
        // and mapping a configuration to a SKU would be inference, so
        // it is not usable under the project rules. The v2.1.57
        // rationale that kept the Max PRO 300 family out, that no PRO
        // chip had been listed in any cycle, is now outdated: the AI
        // 400 PRO and AI 300 PRO families are listed, so the family is
        // a genuine gap and it is closed here. No code change was
        // needed: the CPU normalizer already maps "AMD Ryzen AI Max+
        // PRO 395" to "RYZEN AI MAX+ PRO 395" and the other three names
        // the same way, and the Settings card shows the CPU count from
        // the dataset, so it reads 187 with no code change.
        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI Max+ PRO 395", out var ryzenAiMaxPlusPro395));
        Assert.Equal("AMD", ryzenAiMaxPlusPro395.Vendor);
        Assert.Equal(3.0, ryzenAiMaxPlusPro395.BaseGhz);
        Assert.Equal(5.1, ryzenAiMaxPlusPro395.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen-pro/ai-max-pro-300-series/amd-ryzen-ai-max-plus-pro-395.html", ryzenAiMaxPlusPro395.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI Max PRO 390", out var ryzenAiMaxPro390));
        Assert.Equal("AMD", ryzenAiMaxPro390.Vendor);
        Assert.Equal(3.2, ryzenAiMaxPro390.BaseGhz);
        Assert.Equal(5.0, ryzenAiMaxPro390.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen-pro/ai-max-pro-300-series/amd-ryzen-ai-max-pro-390.html", ryzenAiMaxPro390.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI Max PRO 385", out var ryzenAiMaxPro385));
        Assert.Equal("AMD", ryzenAiMaxPro385.Vendor);
        Assert.Equal(3.6, ryzenAiMaxPro385.BaseGhz);
        Assert.Equal(5.0, ryzenAiMaxPro385.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen-pro/ai-max-pro-300-series/amd-ryzen-ai-max-pro-385.html", ryzenAiMaxPro385.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("AMD Ryzen AI Max PRO 380", out var ryzenAiMaxPro380));
        Assert.Equal("AMD", ryzenAiMaxPro380.Vendor);
        Assert.Equal(3.6, ryzenAiMaxPro380.BaseGhz);
        Assert.Equal(4.9, ryzenAiMaxPro380.BoostGhz);
        Assert.StartsWith("https://www.amd.com/en/products/processors/laptop/ryzen-pro/ai-max-pro-300-series/amd-ryzen-ai-max-pro-380.html", ryzenAiMaxPro380.SourceUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_CoreUltra200HMobileCpusCarryVerifiedFigures()
    {
        // Dataset v1.37.0: the five Intel Core Ultra 200H Series mobile
        // CPUs (Arrow Lake-H, launched January 6, 2025, 28 W base power,
        // 115 W maximum turbo power), the 28 W family the dataset never
        // listed: the Core Ultra 9 285H and the Core Ultra 7 265H and
        // 255H at 16 cores (6 P-cores, 8 E-cores and 2 Low Power
        // Efficient-cores, 16 threads, 24 MB cache), and the Core Ultra
        // 5 235H and 225H at 14 cores (4 P-cores, 8 E-cores and 2 Low
        // Power Efficient-cores, 14 threads, 18 MB cache). Every figure
        // was compiled by hand from Intel's own Quick Reference Guide
        // for the 200H and 200U series, v1.1 (cdrdv2-public.intel.com,
        // read 2026-11-15), which publishes the Performance-core Base
        // Frequency and the Max Turbo Frequency per SKU in its H table,
        // and each figure was cross-checked per SKU against ARK mirrors
        // that agree on all five models (nanoreview, siliconcat,
        // cputronic, notebookcheck, techpowerup): the 285H at
        // Performance-core Base Frequency 2.9 GHz and Max Turbo
        // Frequency up to 5.4 GHz with E-core Max Turbo Frequency 4.5
        // GHz; the 265H at 2.2 and up to 5.3 GHz with E-core turbo 4.5
        // GHz; the 255H at 2.0 and up to 5.1 GHz with E-core turbo 4.4
        // GHz; the 235H at 2.4 and up to 5.0 GHz with E-core turbo 4.4
        // GHz; the 225H at 1.7 and up to 4.9 GHz with E-core turbo 4.3
        // GHz. The dataset's base_ghz is Intel's Performance-core Base
        // Frequency, boost_ghz is Intel's Max Turbo Frequency, and
        // e_core_boost_ghz is Intel's E-core Max Turbo Frequency, per
        // its own definition. Two judgement calls are recorded. First,
        // the 235H P-core base is 2.4 GHz, higher than the 265H and
        // 255H at 2.2 and 2.0 GHz: the 235H has only 4 P-cores against
        // their 6, and the Quick Reference Guide and all five mirrors
        // agree on it, so the figure stands as published. Second, the
        // trailing numbers in the Quick Reference Guide H table (2.35,
        // 2.3, 2.25, 2.25 and 2.2 GHz) are the graphics Max Frequency
        // (Arc 140T at 2.35 GHz on the 285H, 2.3 GHz on the 265H, 2.25
        // GHz on the 255H and 235H, Arc 130T at 2.2 GHz on the 225H),
        // not E-core base figures, so they are not recorded as base
        // figures anywhere; the same reading was used for the 200U
        // family in v2.1.70. The slice was picked by a fresh audit: the
        // brief named the AMD Ryzen AI Max 300 consumer mobile parts
        // first, but the Max 392 and Max 388 are already in the
        // dataset, and the brief named the AMD Ryzen AI 400 consumer
        // mobile parts second, but all seven are already in the
        // dataset (AI 9 HX 475, HX 470, AI 9 465, AI 7 450, AI 7 445,
        // AI 5 435, AI 5 430), so both targets yield nothing and the
        // 200H family, the direct sibling of the 200U family shipped
        // in v2.1.70 and listed in the same Quick Reference Guide, is
        // the genuine gap. The Intel 13th-gen HX P-core Base Frequency
        // rows stay out on the v2.1.47 finding, re-checked again in
        // v2.1.72 and not re-checked in this cycle. No code change was
        // needed: the CPU normalizer already maps "Intel Core Ultra 9
        // 285H" to "ULTRA 9 285H" and the other four names the same
        // way, and the Settings card shows the CPU count from the
        // dataset, so it reads 192 with no code change. One honest
        // limitation is recorded: Intel's own compare pages return
        // HTTP 403 in this sandbox, so no live compare-page spot-check
        // was possible in this cycle; the figures rest on Intel's own
        // Quick Reference Guide PDF plus the five agreeing ARK mirrors
        // named above.
        const string qrg = "https://cdrdv2-public.intel.com/842536/Intel%20Core%20Ultra%20200H%20and%20200U%20Series%20Processors%20-%20Quick%20Reference%20Guide%20v1.1.pdf";

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 9 285H", out var ultra9285H));
        Assert.Equal("Intel", ultra9285H.Vendor);
        Assert.Equal(2.9, ultra9285H.BaseGhz);
        Assert.Equal(5.4, ultra9285H.BoostGhz);
        Assert.Equal(4.5, ultra9285H.ECoreBoostGhz);
        Assert.StartsWith(qrg, ultra9285H.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 7 265H", out var ultra7265H));
        Assert.Equal("Intel", ultra7265H.Vendor);
        Assert.Equal(2.2, ultra7265H.BaseGhz);
        Assert.Equal(5.3, ultra7265H.BoostGhz);
        Assert.Equal(4.5, ultra7265H.ECoreBoostGhz);
        Assert.StartsWith(qrg, ultra7265H.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 7 255H", out var ultra7255H));
        Assert.Equal("Intel", ultra7255H.Vendor);
        Assert.Equal(2.0, ultra7255H.BaseGhz);
        Assert.Equal(5.1, ultra7255H.BoostGhz);
        Assert.Equal(4.4, ultra7255H.ECoreBoostGhz);
        Assert.StartsWith(qrg, ultra7255H.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 5 235H", out var ultra5235H));
        Assert.Equal("Intel", ultra5235H.Vendor);
        Assert.Equal(2.4, ultra5235H.BaseGhz);
        Assert.Equal(5.0, ultra5235H.BoostGhz);
        Assert.Equal(4.4, ultra5235H.ECoreBoostGhz);
        Assert.StartsWith(qrg, ultra5235H.SourceUrl, StringComparison.Ordinal);

        Assert.True(HardwareDatabase.Default.TryGetCpu("Intel Core Ultra 5 225H", out var ultra5225H));
        Assert.Equal("Intel", ultra5225H.Vendor);
        Assert.Equal(1.7, ultra5225H.BaseGhz);
        Assert.Equal(4.9, ultra5225H.BoostGhz);
        Assert.Equal(4.3, ultra5225H.ECoreBoostGhz);
        Assert.StartsWith(qrg, ultra5225H.SourceUrl, StringComparison.Ordinal);
    }


}
