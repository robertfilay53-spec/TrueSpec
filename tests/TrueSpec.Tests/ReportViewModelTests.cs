using System.Windows.Input;
using TrueSpec.App.Services;
using TrueSpec.App.ViewModels;
using TrueSpec.Core.Models;

namespace TrueSpec.Tests;

/// <summary>
/// Tests for the Report page's v2.0 U7 additions: the Buyer certificate
/// and Verify this machine buttons, the DNA states, and the honest
/// empty states. DNA is identity, not health: no verification result
/// may ever change the score or the verdict. Windows leg only (it drives
/// TrueSpec.App's ViewModels), like ComparisonViewModelTests.
/// </summary>
public class ReportViewModelTests
{
    private sealed class TestCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
        }
    }

    private static ReportViewModel Report(
        MachineDnaVerification? verification = null,
        ICommand? certificateCommand = null,
        ICommand? verifyCommand = null) =>
        new(
            TestSnapshots.Healthy(),
            new TestCommand(),
            new TestCommand(),
            new TestCommand(),
            "/tmp",
            exportCertificateCommand: certificateCommand,
            verifyMachineCommand: verifyCommand,
            dnaVerification: verification);

    [Fact]
    public void NoCommands_ButtonsHideThemselves()
    {
        var report = Report();

        Assert.False(report.HasCertificateExport);
        Assert.False(report.HasVerifyMachine);
        Assert.Null(report.ExportCertificateCommand);
        Assert.Null(report.VerifyMachineCommand);
    }

    [Fact]
    public void CommandsSupplied_ButtonsShow()
    {
        var report = Report(certificateCommand: new TestCommand(), verifyCommand: new TestCommand());

        Assert.True(report.HasCertificateExport);
        Assert.True(report.HasVerifyMachine);
    }

    [Fact]
    public void NoVerification_DnaReadsNotRecorded_NeverAMismatch()
    {
        var report = Report();

        Assert.Contains("not recorded", report.DnaStateText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not recorded", report.DnaHashText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Re-scan", report.VerifyResultText, StringComparison.Ordinal);
        Assert.Empty(report.DnaChangedParts);
    }

    [Fact]
    public void IdenticalVerification_SaysSameMachineSameParts()
    {
        var report = Report(MachineDnaVerification.Identical("abcdef12…34567890"));

        Assert.Equal("Same machine, same parts.", report.DnaStateText);
        Assert.Contains("abcdef12…34567890", report.DnaHashText, StringComparison.Ordinal);
        Assert.Equal("Same machine, same parts.", report.VerifyResultText);
    }

    [Fact]
    public void ChangedVerification_NamesTheChangedParts()
    {
        var changes = new[]
        {
            new DnaPartChange("Memory", "Slot 2", "serial changed since the earlier scan"),
        };
        var report = Report(MachineDnaVerification.Changed(changes, "abcdef12…34567890"));

        Assert.Contains("changed", report.DnaStateText, StringComparison.OrdinalIgnoreCase);
        Assert.Single(report.DnaChangedParts);
        Assert.Contains("Slot 2", report.DnaChangedParts[0], StringComparison.Ordinal);
        Assert.Contains("serial changed", report.DnaChangedParts[0], StringComparison.Ordinal);
    }

    [Fact]
    public void UnverifiableVerification_SaysItCannotBeVerified()
    {
        var report = Report(MachineDnaVerification.Unverifiable(null));

        Assert.Contains("could not be verified", report.DnaStateText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChangedDna_NeverChangesTheScoreOrVerdict()
    {
        var snapshot = TestSnapshots.Healthy();
        var changes = new[]
        {
            new DnaPartChange("Memory", "Slot 2", "serial changed since the earlier scan"),
        };
        var report = Report(MachineDnaVerification.Changed(changes, "abcdef12…34567890"));

        Assert.Equal($"{snapshot.Score.OverallScore:0.#}", report.ScoreText);
        Assert.Equal(snapshot.Score.Verdict.ToDisplayText(), report.VerdictText);
    }
}
