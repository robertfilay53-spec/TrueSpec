using TrueSpec.Core.Collectors;

namespace TrueSpec.Tests;

/// <summary>
/// Core tests for the Windows Memory Diagnostic result parser: the
/// diagnostic writes its result to the System event log under the
/// provider Microsoft-Windows-MemoryDiagnostics-Results, event 1101 / 1201
/// for a pass and 1102 / 1202 for a failure. The parser classifies by
/// event id + timestamp only and never reads message text. Every case here
/// runs on both legs: the parser is a pure function over raw rows.
/// </summary>
public class MemoryDiagnosticParserTests
{
    private static readonly DateTimeOffset Day1 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Day2 = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    private static MemoryDiagnosticParser.RawMemoryDiagnosticEvent Row(int eventId, DateTimeOffset at) =>
        new(eventId, at);

    [Theory]
    [InlineData(1101)]
    [InlineData(1201)]
    public void Parse_PassEventIds_ReturnPassed(int eventId)
    {
        var result = MemoryDiagnosticParser.Parse([Row(eventId, Day1)]);

        Assert.NotNull(result);
        Assert.Equal(MemoryDiagnosticOutcome.Passed, result.Outcome);
        Assert.Equal(Day1, result.RanAt);
    }

    [Theory]
    [InlineData(1102)]
    [InlineData(1202)]
    public void Parse_FailEventIds_ReturnFailed(int eventId)
    {
        var result = MemoryDiagnosticParser.Parse([Row(eventId, Day1)]);

        Assert.NotNull(result);
        Assert.Equal(MemoryDiagnosticOutcome.Failed, result.Outcome);
        Assert.Equal(Day1, result.RanAt);
    }

    [Fact]
    public void Parse_NewestRowDecides_OlderFailNewerPass_ReturnsPassed()
    {
        var result = MemoryDiagnosticParser.Parse([
            Row(1102, Day1),
            Row(1201, Day2),
        ]);

        Assert.NotNull(result);
        Assert.Equal(MemoryDiagnosticOutcome.Passed, result.Outcome);
        Assert.Equal(Day2, result.RanAt);
    }

    [Fact]
    public void Parse_NewestRowDecides_OlderPassNewerFail_ReturnsFailed()
    {
        var result = MemoryDiagnosticParser.Parse([
            Row(1101, Day1),
            Row(1102, Day2),
        ]);

        Assert.NotNull(result);
        Assert.Equal(MemoryDiagnosticOutcome.Failed, result.Outcome);
        Assert.Equal(Day2, result.RanAt);
    }

    [Fact]
    public void Parse_RowsInAnyOrder_NewestStillDecides()
    {
        // The caller reads newest first, but the parser must not depend on
        // input order: an older pass listed after a newer fail still loses.
        var result = MemoryDiagnosticParser.Parse([
            Row(1102, Day2),
            Row(1101, Day1),
        ]);

        Assert.NotNull(result);
        Assert.Equal(MemoryDiagnosticOutcome.Failed, result.Outcome);
        Assert.Equal(Day2, result.RanAt);
    }

    [Fact]
    public void Parse_SameSecondCompanionPair_ClassifiesEitherRow()
    {
        // Each run writes a companion pair at the same second (1101 with
        // 1201, or 1102 with 1202). Whichever the reader surfaces first,
        // the classification must agree.
        var passed = MemoryDiagnosticParser.Parse([
            Row(1101, Day1),
            Row(1201, Day1),
        ]);
        Assert.NotNull(passed);
        Assert.Equal(MemoryDiagnosticOutcome.Passed, passed.Outcome);

        var failed = MemoryDiagnosticParser.Parse([
            Row(1202, Day1),
            Row(1102, Day1),
        ]);
        Assert.NotNull(failed);
        Assert.Equal(MemoryDiagnosticOutcome.Failed, failed.Outcome);
    }

    [Fact]
    public void Parse_UnrecognisedEventIds_AreIgnored()
    {
        var result = MemoryDiagnosticParser.Parse([
            Row(1001, Day1),
            Row(1203, Day2),
        ]);

        Assert.Null(result);
    }

    [Fact]
    public void Parse_EmptyRows_ReturnsNull()
    {
        Assert.Null(MemoryDiagnosticParser.Parse([]));
    }

    [Fact]
    public void DisplayText_PassedResult_NamesTheDate()
    {
        var text = MemoryDiagnosticParser.DisplayText(
            new MemoryDiagnosticResult(MemoryDiagnosticOutcome.Passed, Day1));

        Assert.Equal("Passed on 2026-10-01", text);
    }

    [Fact]
    public void DisplayText_FailedResult_NamesTheDate()
    {
        var text = MemoryDiagnosticParser.DisplayText(
            new MemoryDiagnosticResult(MemoryDiagnosticOutcome.Failed, Day1));

        Assert.Equal("Failed on 2026-10-01", text);
    }

    [Fact]
    public void DisplayText_NoResult_SaysNotRun()
    {
        Assert.Equal("Not run", MemoryDiagnosticParser.DisplayText(null));
    }
}
