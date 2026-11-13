using System.Collections.Generic;

namespace TrueSpec.App.Services;

/// <summary>
/// Outcome of a batch machine-CSV export (v2.1.4, Technician mode):
/// one file per machine in the export folder. A machine is skipped,
/// never faked, when its latest scan cannot be read or has no score;
/// a write failure for one machine never stops the rest. The folder is
/// the one the orchestrator wrote into, so the status line can name it.
/// </summary>
public sealed record ExportAllMachinesResult(
    string Folder,
    IReadOnlyList<string> ExportedPaths,
    IReadOnlyList<(string MachineName, string Reason)> Skipped);
