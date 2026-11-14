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
        Assert.False(database.TryGetCpu("I7-12700H", out _));
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

        Assert.Equal("1.0.0", database.Version);
        Assert.Equal("2026-11-14", database.Updated);
        Assert.Equal(74, database.CpuCount);
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
}
