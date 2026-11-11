using TrueSpec.Core.Collectors;

namespace TrueSpec.Tests;

public class WindowsUpdateStatusParserTests
{
    [Fact]
    public void ParseLastSuccessTime_RegistryFormatParsesAsLocalTime()
    {
        var parsed = WindowsUpdateStatusParser.ParseLastSuccessTime("2026-11-05 14:23:11");

        Assert.NotNull(parsed);
        Assert.Equal(2026, parsed.Value.Year);
        Assert.Equal(11, parsed.Value.Month);
        Assert.Equal(5, parsed.Value.Day);
        Assert.Equal(14, parsed.Value.Hour);
        Assert.Equal(23, parsed.Value.Minute);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a date")]
    [InlineData("2026-13-45 99:99:99")]
    public void ParseLastSuccessTime_UnparseableIsNullNeverAGuess(string? value) =>
        Assert.Null(WindowsUpdateStatusParser.ParseLastSuccessTime(value));

    [Fact]
    public void ParseHotfixDate_USLocaleDateParses()
    {
        var parsed = WindowsUpdateStatusParser.ParseHotfixDate("11/5/2026");

        Assert.NotNull(parsed);
        Assert.Equal(2026, parsed.Value.Year);
        Assert.Equal(11, parsed.Value.Month);
        Assert.Equal(5, parsed.Value.Day);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a date")]
    public void ParseHotfixDate_UnparseableIsNull(string? value) =>
        Assert.Null(WindowsUpdateStatusParser.ParseHotfixDate(value));

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("http://wsus.corp.local:8530", true)]
    public void IsManaged_WsusServerMeansManaged(string? server, bool expected) =>
        Assert.Equal(expected, WindowsUpdateStatusParser.IsManaged(server));

    [Fact]
    public void OrderHotfixes_NewestFirstUndatedLastCappedAtTen()
    {
        var hotfixes = new[]
        {
            new InstalledHotfix("KB100", null, "Undated"),
            new InstalledHotfix("KB200", new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), "October"),
            new InstalledHotfix("KB300", new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.Zero), "November"),
        }.Concat(Enumerable.Range(1, 12).Select(i =>
            new InstalledHotfix($"KB9{i:00}", new DateTimeOffset(2025, 1, i, 0, 0, 0, TimeSpan.Zero), "Old")));

        var ordered = WindowsUpdateStatusParser.OrderHotfixes(hotfixes);

        Assert.Equal(WindowsUpdateStatusParser.MaxHotfixesShown, ordered.Count);
        Assert.Equal("KB300", ordered[0].Id);
        Assert.Equal("KB200", ordered[1].Id);
        // The undated hotfix sorts after every dated one, so the cap drops it.
        Assert.DoesNotContain(ordered, h => h.Id == "KB100");
    }

    [Fact]
    public void DaysSince_TenDaysAgoIsTen()
    {
        var now = new DateTimeOffset(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
        var moment = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(10d, WindowsUpdateStatusParser.DaysSince(moment, now));
    }

    [Fact]
    public void DaysSince_MissingOrFutureIsNull()
    {
        var now = new DateTimeOffset(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

        Assert.Null(WindowsUpdateStatusParser.DaysSince(null, now));
        // A future date is a bad clock, not "updated tomorrow".
        Assert.Null(WindowsUpdateStatusParser.DaysSince(now.AddDays(5), now));
    }

    [Fact]
    public void Headline_CountAndDateKnownNamesBoth()
    {
        var headline = WindowsUpdateStatusParser.Headline(
            42,
            new DateTimeOffset(2026, 11, 5, 14, 23, 11, TimeSpan.Zero));

        Assert.Equal("42 updates installed - last update 2026-11-05", headline);
    }

    [Fact]
    public void Headline_CountKnownDateUnknownSaysSo()
    {
        Assert.Equal(
            "42 updates installed - last update not reported",
            WindowsUpdateStatusParser.Headline(42, null));
    }

    [Fact]
    public void Headline_CountUnknownDateKnownPrefersTheKnownDate()
    {
        // The hotfix query failed, so the count is unknown — but the install
        // date is known, and the headline must not contradict the spec below
        // it that shows that date.
        Assert.Equal(
            "Last update 2026-11-05",
            WindowsUpdateStatusParser.Headline(
                null,
                new DateTimeOffset(2026, 11, 5, 14, 23, 11, TimeSpan.Zero)));
    }

    [Fact]
    public void Headline_NeitherKnownSaysSo()
    {
        Assert.Equal("Last update not reported", WindowsUpdateStatusParser.Headline(null, null));
    }

    [Fact]
    public void WindowsUpdateStatus_HotfixCountIsTheFullCountNotTheShownCap()
    {
        var status = new WindowsUpdateStatus(
            null,
            null,
            false,
            null,
            Enumerable.Range(1, 25).Select(i => new InstalledHotfix($"KB{i}", null, null)).ToList());

        Assert.Equal(25, status.HotfixCount);
    }
}
