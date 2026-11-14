using System.Collections.Generic;

namespace TrueSpec.App.Services;

/// <summary>
/// Outcome of a batch machine-CSV export (v2.1.4, Technician mode):
/// one file per machine in the export folder. A machine is skipped,
/// never faked, when its latest scan cannot be read or has no score;
/// a write failure for one machine never stops the rest. The folder is
/// the one the orchestrator wrote into, so the status line can name it.
/// Localisation polish (v2.1.19): a skipped machine carries its
/// structured skip reason kind (and the write error, when the write
/// itself failed) instead of an English sentence, so the Fleet page
/// renders the reason in the page language and a language switch
/// re-renders it; the machine name and the write error stay data.
/// </summary>
public sealed record ExportAllMachinesResult(
    string Folder,
    IReadOnlyList<string> ExportedPaths,
    IReadOnlyList<ExportAllSkippedMachine> Skipped);

/// <summary>
/// One machine "Export all machines" skipped: the machine name, the
/// structured reason kind, and the write error when the write itself
/// failed (an exception message, data, never translated). The other
/// kinds carry no error.
/// </summary>
public sealed record ExportAllSkippedMachine(
    string MachineName,
    ExportAllSkipReasonKind Kind,
    string? WriteError = null);
