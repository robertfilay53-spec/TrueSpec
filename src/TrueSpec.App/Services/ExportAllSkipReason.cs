using TrueSpec.Core.Localization;

namespace TrueSpec.App.Services;

/// <summary>
/// Why a machine was skipped by "Export all machines", as a structured
/// kind instead of a sentence. The Fleet page renders the reason in the
/// page language from the kind, so a German user sees German; the
/// English sentence stays the renderer's text for the English page.
/// This is the same pattern v2.1.18 used for the import skip reasons
/// (<see cref="TrueSpec.Core.Reports.SnapshotImportFailureKind"/>):
/// the export-all path is separate from import, but the two paths
/// render their skips the same way.
/// </summary>
public enum ExportAllSkipReasonKind
{
    NoSavedScan,
    NoScore,
    WriteFailed,
}

/// <summary>
/// Renders an <see cref="ExportAllSkipReasonKind"/> as the reason
/// sentence in the requested language, from the catalogue keys. The
/// write error is data (an exception message), never translated.
/// </summary>
public static class ExportAllSkipReason
{
    public static string Text(ExportAllSkipReasonKind kind, string? writeError, string? languageCode) =>
        kind switch
        {
            ExportAllSkipReasonKind.NoSavedScan => StringLocalizer.Get(AppStringKeys.FleetExportAllSkipNoSavedScan, languageCode),
            ExportAllSkipReasonKind.NoScore => StringLocalizer.Get(AppStringKeys.FleetExportAllSkipNoScore, languageCode),
            ExportAllSkipReasonKind.WriteFailed => StringLocalizer.GetFormatted(
                AppStringKeys.FleetExportAllSkipWriteFailed,
                languageCode,
                writeError ?? string.Empty),
            _ => StringLocalizer.Get(AppStringKeys.FleetExportAllSkipNoSavedScan, languageCode),
        };
}
