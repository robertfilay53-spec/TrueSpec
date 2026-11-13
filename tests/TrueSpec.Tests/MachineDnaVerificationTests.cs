using TrueSpec.App.Services;
using TrueSpec.Core.Comparison;
using TrueSpec.Core.Models;

namespace TrueSpec.Tests;

/// <summary>
/// A controllable stand-in for the Core DNA comparer, so the Report and
/// Compare page tests can exercise every DNA state (identical, changed,
/// unverifiable, not recorded) with canned results. The UI only ever
/// talks to <see cref="IMachineDnaVerifier"/>, so the fake proves the
/// rendering logic, not the fingerprint itself.
/// </summary>
public sealed class FakeMachineDnaVerifier : IMachineDnaVerifier
{
    public MachineDnaVerification DescribeResult { get; set; } = MachineDnaVerification.NotRecorded();

    public MachineDnaVerification CompareResult { get; set; } = MachineDnaVerification.NotRecordedForPair();

    public MachineDnaVerification Describe(SystemSnapshot snapshot) => DescribeResult;

    public MachineDnaVerification Compare(SystemSnapshot before, SystemSnapshot after) => CompareResult;
}

/// <summary>
/// Tests for the App-layer DNA verifier (v2.0 U7): the hash truncation
/// rule, the honest empty states (never a fake match, never a fake
/// mismatch), and the real comparison over stored snapshot DNA.
/// Windows leg only (the verifier lives in TrueSpec.App), like
/// ComparisonViewModelTests.
/// </summary>
public class MachineDnaVerificationTests
{
    private const string HashA = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string HashB = "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";

    private static SystemSnapshot WithDna(
        string? hash,
        IReadOnlyList<MachineDnaPart>? parts) =>
        TestSnapshots.Healthy() with { MachineDnaHash = hash, MachineDnaParts = parts };

    private static MachineDnaPart Bios(string? serial) =>
        MachineDnaPart.FromSerial(MachineDnaPartKind.Bios, "BIOS", serial);

    private static MachineDnaPart Board(string? serial) =>
        MachineDnaPart.FromSerial(MachineDnaPartKind.Board, "Board", serial);

    [Fact]
    public void TruncateHash_NullOrBlank_ReturnsNull()
    {
        Assert.Null(MachineDnaVerification.TruncateHash(null));
        Assert.Null(MachineDnaVerification.TruncateHash(string.Empty));
        Assert.Null(MachineDnaVerification.TruncateHash("   "));
    }

    [Fact]
    public void TruncateHash_ShortHash_ReturnedUnchanged()
    {
        Assert.Equal("abcdef0123456789", MachineDnaVerification.TruncateHash("abcdef0123456789"));
    }

    [Fact]
    public void TruncateHash_LongHash_KeepsFirstAndLastEight()
    {
        Assert.Equal("01234567…89abcdef", MachineDnaVerification.TruncateHash(HashA));
    }

    [Fact]
    public void Describe_NoDnaParts_ReportsNotRecorded_NeverClaimsAMatch()
    {
        var result = MachineDnaVerifier.Instance.Describe(TestSnapshots.Healthy());

        Assert.Equal(DnaVerificationState.NotRecorded, result.State);
        Assert.Null(result.HashDisplay);
        Assert.Empty(result.ChangedParts);
        Assert.Contains("not recorded", result.SummaryText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("same machine", result.SummaryText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Describe_PartsButNoHash_ReportsNotAvailable()
    {
        var snapshot = WithDna(null, new[] { Bios("To be filled by O.E.M.") });
        var result = MachineDnaVerifier.Instance.Describe(snapshot);

        Assert.Equal(DnaVerificationState.Unverifiable, result.State);
        Assert.Contains("not available", result.SummaryText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Describe_DnaRecorded_ReturnsIdenticalWithTruncatedHash()
    {
        var snapshot = WithDna(HashA, new[] { Bios("ABCDEF1234567890"), Board("BOARD123456789") });
        var result = MachineDnaVerifier.Instance.Describe(snapshot);

        Assert.Equal(DnaVerificationState.Identical, result.State);
        Assert.Equal("01234567…89abcdef", result.HashDisplay);
    }

    [Fact]
    public void Compare_IdenticalSnapshots_SaysSameMachineSameParts()
    {
        var parts = new[] { Bios("ABCDEF1234567890"), Board("BOARD123456789") };
        var before = WithDna(HashA, parts);
        var after = WithDna(HashA, parts);

        var result = MachineDnaVerifier.Instance.Compare(before, after);

        Assert.Equal(DnaVerificationState.Identical, result.State);
        Assert.Equal("Same machine, same parts.", result.SummaryText);
        Assert.Empty(result.ChangedParts);
    }

    [Fact]
    public void Compare_ChangedSerial_NamesTheChangedPart()
    {
        var before = WithDna(HashA, new[] { Bios("ABCDEF1234567890"), Board("BOARD123456789") });
        var after = WithDna(HashB, new[] { Bios("ZZZZZZ9876543210"), Board("BOARD123456789") });

        var result = MachineDnaVerifier.Instance.Compare(before, after);

        Assert.Equal(DnaVerificationState.Changed, result.State);
        Assert.Single(result.ChangedParts);
        Assert.Contains("BIOS", result.ChangedParts[0].Label, StringComparison.Ordinal);
        Assert.Contains("changed", result.ChangedParts[0].Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compare_OneSideHasNoDna_ReportsNotRecordedForPair_NeverAMismatch()
    {
        var before = TestSnapshots.Healthy();
        var after = WithDna(HashA, new[] { Bios("ABCDEF1234567890") });

        var result = MachineDnaVerifier.Instance.Compare(before, after);

        Assert.Equal(DnaVerificationState.NotRecorded, result.State);
        Assert.Empty(result.ChangedParts);
        Assert.Contains("not a mismatch", result.SummaryText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compare_UnidentifiedPart_ReturnsUnverifiable()
    {
        var before = WithDna(HashA, new[] { Bios("ABCDEF1234567890"), Board("BOARD123456789") });
        var after = WithDna(HashB, new[] { Bios("ABCDEF1234567890"), Board(null) });

        var result = MachineDnaVerifier.Instance.Compare(before, after);

        Assert.Equal(DnaVerificationState.Unverifiable, result.State);
        Assert.Contains("could not be verified", result.SummaryText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IdenticalFactory_SaysSameMachineSameParts()
    {
        var result = MachineDnaVerification.Identical("abcdef12…34567890");

        Assert.Equal(DnaVerificationState.Identical, result.State);
        Assert.Equal("Same machine, same parts.", result.SummaryText);
        Assert.Empty(result.ChangedParts);
    }

    [Fact]
    public void ChangedFactory_NamesTheChangedParts()
    {
        var changes = new[]
        {
            new DnaPartChange("Memory", "Slot 2", "serial changed since the earlier scan"),
        };
        var result = MachineDnaVerification.Changed(changes, "abcdef12…34567890");

        Assert.Equal(DnaVerificationState.Changed, result.State);
        Assert.Single(result.ChangedParts);
        Assert.Contains("changed", result.SummaryText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnverifiableFactory_SaysTheComparisonCannotBeTrusted()
    {
        var result = MachineDnaVerification.Unverifiable(null);

        Assert.Equal(DnaVerificationState.Unverifiable, result.State);
        Assert.Contains("cannot be verified", result.SummaryText, StringComparison.OrdinalIgnoreCase);
    }
}
