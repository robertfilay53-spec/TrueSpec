using TrueSpec.Core.Collectors;
using TrueSpec.Core.Models;

namespace TrueSpec.Tests;

/// <summary>
/// U8 software audit: the pure Core parser is testable without a live
/// registry or live CIM. The product-key rule is pinned here too: no
/// snapshot the parser builds may name or carry a key, and a licence
/// state CIM did not report reads as the honest no-reading value, never
/// as "activated" or "not activated".
/// </summary>
public class SoftwareAuditParserTests
{
    private static InstalledProgram Program(
        string name = "Example App",
        string? version = "1.2.3",
        string? publisher = "Example Ltd",
        DateTimeOffset? installDate = null) =>
        new(
            name,
            version,
            publisher,
            installDate ?? new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero),
            "All users (64-bit)");

    private static SoftwareAuditData Data(
        IReadOnlyList<InstalledProgram>? programs = null,
        LicenseInfo? license = null,
        IReadOnlyList<InstalledRuntime>? runtimes = null,
        bool truncated = false,
        bool omitLicense = false) =>
        new(
            programs ?? new[] { Program(), Program("Second App", "2.0", "Other Ltd") },
            omitLicense ? null : license ?? new LicenseInfo("Windows(R), Professional edition", 1, "Retail"),
            runtimes ?? new[] { new InstalledRuntime("Microsoft .NETCore.App", "8.0.10", "x64") },
            new[] { "All users (64-bit)" },
            Array.Empty<string>(),
            truncated,
            SystemComponentsSkipped: 0);

    private static string? SpecValue(ComponentSnapshot snapshot, string label) =>
        snapshot.Specs.FirstOrDefault(s => s.Label == label)?.Value;

    [Theory]
    [InlineData("20240115", 2024, 1, 15)]
    [InlineData("2024-01-15", 2024, 1, 15)]
    [InlineData("19991231", 1999, 12, 31)]
    public void ParseInstallDate_AcceptsRegistryAndIsoDates(string text, int year, int month, int day)
    {
        var date = SoftwareAuditParser.ParseInstallDate(text);

        Assert.NotNull(date);
        Assert.Equal(year, date!.Value.Year);
        Assert.Equal(month, date.Value.Month);
        Assert.Equal(day, date.Value.Day);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("15/01/2024")]
    [InlineData("January 15, 2024")]
    [InlineData("20241301")]
    [InlineData("not a date")]
    public void ParseInstallDate_RejectsAnythingElseInsteadOfGuessing(string? text)
    {
        Assert.Null(SoftwareAuditParser.ParseInstallDate(text));
    }

    [Theory]
    [InlineData(533320u, "4.8.1")]
    [InlineData(533325u, "4.8.1")]
    [InlineData(528040u, "4.8")]
    [InlineData(461808u, "4.7.2")]
    [InlineData(461308u, "4.7.1")]
    [InlineData(460798u, "4.7")]
    [InlineData(394802u, "4.6.2")]
    [InlineData(394254u, "4.6.1")]
    [InlineData(393295u, "4.6")]
    [InlineData(379893u, "4.5.2")]
    [InlineData(378675u, "4.5.1")]
    [InlineData(378389u, "4.5")]
    public void FrameworkReleaseToVersion_MapsPublishedReleases(uint release, string expected)
    {
        Assert.Equal(expected, SoftwareAuditParser.FrameworkReleaseToVersion(release));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(378388u)]
    [InlineData(100u)]
    public void FrameworkReleaseToVersion_UnknownReleaseIsNotAVersion(uint release)
    {
        Assert.Null(SoftwareAuditParser.FrameworkReleaseToVersion(release));
    }

    [Theory]
    [InlineData("Windows(R) Operating System, RETAIL channel", "Retail")]
    [InlineData("Windows(R) Operating System, OEM channel", "OEM")]
    [InlineData("Windows(R) Operating System, VOLUME channel", "Volume")]
    [InlineData("windows(r) operating system, retail channel", "Retail")]
    public void ParseLicenseChannel_ReadsTheChannelWord(string description, string expected)
    {
        Assert.Equal(expected, SoftwareAuditParser.ParseLicenseChannel(description));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Windows(R) Operating System")]
    [InlineData("Some other product")]
    public void ParseLicenseChannel_NoChannelWordMeansNoChannel(string? description)
    {
        Assert.Null(SoftwareAuditParser.ParseLicenseChannel(description));
    }

    [Theory]
    [InlineData(0u, "Not activated")]
    [InlineData(1u, "Activated")]
    [InlineData(2u, "Out-of-box grace period")]
    [InlineData(3u, "Out-of-tolerance grace period")]
    [InlineData(4u, "Non-genuine grace period")]
    [InlineData(5u, "Notification")]
    [InlineData(6u, "Extended grace period")]
    public void LicenseStatusText_MapsEveryDocumentedCode(uint status, string expected)
    {
        Assert.Equal(expected, SoftwareAuditParser.LicenseStatusText(status));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(7u)]
    [InlineData(99u)]
    public void LicenseStatusText_UndocumentedCodeIsNotAStatus(uint? status)
    {
        Assert.Null(SoftwareAuditParser.LicenseStatusText(status));
    }

    [Fact]
    public void BuildProgram_BlankDisplayNameIsNotAProgram()
    {
        Assert.Null(SoftwareAuditParser.BuildProgram(null, "1.0", "Vendor", "20240115", "All users (64-bit)"));
        Assert.Null(SoftwareAuditParser.BuildProgram("   ", "1.0", "Vendor", "20240115", "All users (64-bit)"));
    }

    [Fact]
    public void BuildProgram_KeepsMissingFieldsMissing()
    {
        var program = SoftwareAuditParser.BuildProgram("Example App", null, null, "garbage", "Current user");

        Assert.NotNull(program);
        Assert.Equal("Example App", program!.Name);
        Assert.Null(program.Version);
        Assert.Null(program.Publisher);
        Assert.Null(program.InstallDate);
    }

    [Fact]
    public void CleanText_CapsLongValuesWithAVisibleMarker()
    {
        var cleaned = SoftwareAuditParser.CleanText(new string('x', 600));

        Assert.NotNull(cleaned);
        Assert.Equal(SoftwareAuditParser.MaxValueLength, cleaned!.Length);
        Assert.EndsWith("...", cleaned, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSnapshot_HeadlineNamesCountAndActivation()
    {
        var snapshot = SoftwareAuditParser.BuildSnapshot(Data());

        Assert.Equal("software", snapshot.Id);
        Assert.Equal("2 programs - Activated", snapshot.Headline);
        Assert.Null(snapshot.HealthPercent);
        Assert.Equal("2", SpecValue(snapshot, SpecLabels.InstalledPrograms));
        Assert.Equal("Activated", SpecValue(snapshot, SpecLabels.WindowsActivation));
        Assert.Equal("Retail", SpecValue(snapshot, SpecLabels.LicenseChannel));
        Assert.Equal("Windows(R), Professional edition", SpecValue(snapshot, SpecLabels.LicensedProduct));
        Assert.Equal("1", SpecValue(snapshot, SpecLabels.InstalledRuntimes));
        Assert.Equal("1.2.3 / Example Ltd / installed 2024-01-15", SpecValue(snapshot, "Example App"));
        Assert.Equal("8.0.10", SpecValue(snapshot, "Microsoft .NETCore.App (x64)"));
        Assert.All(snapshot.Specs, spec => Assert.False(string.IsNullOrWhiteSpace(spec.Source)));
    }

    [Fact]
    public void BuildSnapshot_TypedCountsCarryNoGuessedNumbers()
    {
        var snapshot = SoftwareAuditParser.BuildSnapshot(Data());

        var programs = snapshot.Specs.First(s => s.Label == SpecLabels.InstalledPrograms);
        Assert.Equal(2d, programs.NumericValue);
        Assert.Equal("programs", programs.NumericUnit);

        var runtimes = snapshot.Specs.First(s => s.Label == SpecLabels.InstalledRuntimes);
        Assert.Equal(1d, runtimes.NumericValue);
        Assert.Equal("runtimes", runtimes.NumericUnit);
    }

    [Fact]
    public void BuildSnapshot_MissingLicenseReadsAsNotReportedNeverAsActivated()
    {
        var snapshot = SoftwareAuditParser.BuildSnapshot(Data(omitLicense: true));

        Assert.Equal("2 programs - " + SpecLabels.NotReportedByHardware, snapshot.Headline);
        Assert.Equal(SpecLabels.NotReportedByHardware, SpecValue(snapshot, SpecLabels.WindowsActivation));
        Assert.Equal(SpecLabels.NotReportedByHardware, SpecValue(snapshot, SpecLabels.LicenseChannel));
        Assert.Equal(SpecLabels.NotReportedByHardware, SpecValue(snapshot, SpecLabels.LicensedProduct));
        Assert.DoesNotContain("Activated", snapshot.Headline, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSnapshot_UnlicensedMachineSaysSoPlainly()
    {
        var snapshot = SoftwareAuditParser.BuildSnapshot(
            Data(license: new LicenseInfo("Windows(R), Professional edition", 0, "OEM")));

        Assert.Equal("2 programs - Not activated", snapshot.Headline);
        Assert.Equal("Not activated", SpecValue(snapshot, SpecLabels.WindowsActivation));
        Assert.Equal("OEM", SpecValue(snapshot, SpecLabels.LicenseChannel));
    }

    [Fact]
    public void BuildSnapshot_NoProgramsIsAnHonestEmptyList()
    {
        var snapshot = SoftwareAuditParser.BuildSnapshot(Data(programs: Array.Empty<InstalledProgram>()));

        Assert.Equal("No installed programs found", snapshot.Headline);
        Assert.Equal("0", SpecValue(snapshot, SpecLabels.InstalledPrograms));
    }

    [Fact]
    public void BuildSnapshot_UnreadableEntriesAreListedNotDropped()
    {
        var programs = new[]
        {
            Program(),
            new InstalledProgram("Unreadable entry ({A1B2C3D4-0000-0000-0000-000000000000})", null, null, null, "All users (64-bit)", IsUnreadable: true),
        };
        var snapshot = SoftwareAuditParser.BuildSnapshot(Data(programs: programs));

        Assert.Equal("1", SpecValue(snapshot, SpecLabels.UnreadableEntries));
        Assert.Equal(
            SpecLabels.NotReportedByHardware,
            SpecValue(snapshot, "Unreadable entry ({A1B2C3D4-0000-0000-0000-000000000000})"));
        // The readable count excludes the unreadable entry.
        Assert.Equal("1", SpecValue(snapshot, SpecLabels.InstalledPrograms));
    }

    [Fact]
    public void BuildSnapshot_TruncatedListHeadlineSays300Plus()
    {
        var programs = Enumerable.Range(0, SoftwareAuditParser.MaxPrograms)
            .Select(i => Program($"App {i}"))
            .ToArray();
        var snapshot = SoftwareAuditParser.BuildSnapshot(Data(programs: programs, truncated: true));

        Assert.StartsWith("300+ programs", snapshot.Headline, StringComparison.Ordinal);
        Assert.DoesNotContain("300 programs -", snapshot.Headline, StringComparison.Ordinal);
        var note = snapshot.Specs.FirstOrDefault(s => s.Label == "Program list truncated");
        Assert.NotNull(note);
        Assert.Contains("first 300", note!.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSnapshot_DuplicateProgramNamesAreNumbered()
    {
        var programs = new[] { Program("Same App"), Program("Same App"), Program("Same App") };
        var snapshot = SoftwareAuditParser.BuildSnapshot(Data(programs: programs));

        Assert.NotNull(SpecValue(snapshot, "Same App"));
        Assert.NotNull(SpecValue(snapshot, "Same App #2"));
        Assert.NotNull(SpecValue(snapshot, "Same App #3"));
    }

    [Fact]
    public void BuildSnapshot_NeverNamesOrCarriesAProductKey()
    {
        var snapshot = SoftwareAuditParser.BuildSnapshot(Data());

        Assert.All(snapshot.Specs, spec =>
        {
            Assert.DoesNotContain("PartialProductKey", spec.Label, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PartialProductKey", spec.Value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ProductKey", spec.Label, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Contains("never reads, stores or shows a product key", snapshot.WhatThisMeans, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSnapshot_MissingProgramFieldsReadAsNotReported()
    {
        var programs = new[] { new InstalledProgram("Bare App", null, null, null, "Current user") };
        var snapshot = SoftwareAuditParser.BuildSnapshot(Data(programs: programs));

        var value = SpecValue(snapshot, "Bare App");
        Assert.NotNull(value);
        Assert.Equal(3, value!.Split(SpecLabels.NotReportedByHardware, StringSplitOptions.None).Length - 1);
    }
}
