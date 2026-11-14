using TrueSpec.App.Services;

namespace TrueSpec.Tests;

/// <summary>
/// The export-all skip reasons (v2.1.19) render in the page language
/// from their structured kind, the same pattern as the import skip
/// reasons: the English sentences are the ones the orchestrator used
/// to produce inline, and the German ones carry no English fragment.
/// The write error is data and passes through untranslated.
/// </summary>
public sealed class ExportAllSkipReasonTests
{
    [Fact]
    public void NoSavedScan_RendersEnglishAndGerman()
    {
        Assert.Equal("no saved scan was found", ExportAllSkipReason.Text(ExportAllSkipReasonKind.NoSavedScan, null, "en"));
        Assert.Equal("kein gespeicherter Scan gefunden wurde", ExportAllSkipReason.Text(ExportAllSkipReasonKind.NoSavedScan, null, "de"));
    }

    [Fact]
    public void NoScore_RendersEnglishAndGerman()
    {
        Assert.Equal("the latest scan has no score to export", ExportAllSkipReason.Text(ExportAllSkipReasonKind.NoScore, null, "en"));
        Assert.Equal("der neueste Scan hat keine Bewertung zum Exportieren", ExportAllSkipReason.Text(ExportAllSkipReasonKind.NoScore, null, "de"));
    }

    [Fact]
    public void WriteFailed_KeepsTheWriteErrorAsData()
    {
        Assert.Equal(
            "could not be written (Access denied)",
            ExportAllSkipReason.Text(ExportAllSkipReasonKind.WriteFailed, "Access denied", "en"));
        Assert.Equal(
            "konnte nicht geschrieben werden (Zugriff verweigert)",
            ExportAllSkipReason.Text(ExportAllSkipReasonKind.WriteFailed, "Zugriff verweigert", "de"));
    }

    [Fact]
    public void WriteFailed_WithNoError_RendersTheBareReason()
    {
        Assert.Equal("could not be written ()", ExportAllSkipReason.Text(ExportAllSkipReasonKind.WriteFailed, null, "en"));
    }
}
