# TrueSpec Roadmap

How TrueSpec grows without changing what it is: an offline, honest
PC system-info and buyer-check tool. Every number is measured with its
source named, or it says "Not reported by hardware". No network calls
at runtime, no elevation prompts, no invented values. A feature that
cannot meet those rules does not ship, however useful it sounds.

## v1.0.1 — shipped in this release

### 1. History and trends (new History page)

The app already saved every scan to a local SQLite database, and the
Compare page already compared two scans, but nothing showed how a
machine changes over time. The History page does that: overall-score,
battery-health and storage-wear trend lines (the existing Sparkline
control) plus a table of every saved scan, oldest first, with score,
battery health, cycle count, storage wear and power-on hours.

- Core: `TrueSpec.Core/History/HistoryTrends.cs`
  (`HistoryTrendAnalyzer`). Pure functions over saved snapshots: the
  score comes from the scoring engine, battery health from the battery
  component's "Health" spec, storage wear from the storage component's
  "Wear level" spec. A value a scan did not report is a gap in the
  line and in the table, never a zero. Deltas need two scans that
  actually reported the value. Snapshots from different machines are
  refused, like the comparer refuses them.
- App: `HistoryViewModel` + `HistoryView` (new nav item "History").
  Loads through the same snapshot store the Compare page uses.
- Tests: `HistoryTrendTests` (deltas, gaps, mixed machines, loading
  from a real SQLite store).

### 2. PDF report export

JSON, HTML and CSV exports existed; the missing one was the format a
buyer actually hands to a seller or files with a receipt. PDF export
is now a fourth button on the Report page, from the same report model,
so all four formats always agree.

- Core: `TrueSpec.Core/Reports/PdfReportExporter.cs` and a new
  `ReportFormat.Pdf`. No third-party library: a minimal PDF 1.4
  writer using only the standard Helvetica fonts (regular + bold),
  with text wrapping, pagination and a measured cross-reference
  table. Non-ASCII characters become `?` and parentheses/backslashes
  are escaped, so an unusual machine name can never break the file.
  Deliberately no dependency: see "Rejected" below.
- App: `ExportPdf` command in `MainViewModel`, optional
  `ExportPdfCommand` on `ReportViewModel` (existing callers keep
  compiling), a PDF button and card on the Report page.
- Tests: `PdfReportExporterTests`, including a test that parses the
  xref table and checks every offset points at the object it claims.
  The generated file was also opened with poppler `pdftotext` on
  Linux: text extracts correctly across pages.

### 3. Startup programs (new Startup component)

A buyer-check question the app could not answer: what launches when
this machine signs in? Many entries slow sign-in, and an entry the
buyer does not recognise is worth asking the seller about.

- Core: `TrueSpec.Core/Collectors/StartupPrograms.cs`
  (`StartupProgram` + `StartupProgramParser`). The parser decodes
  Explorer's StartupApproved state (first DWORD 2 = enabled,
  3 = disabled; anything else is "Not reported by Windows", never a
  guess) and cleans stored commands.
- Windows: `TrueSpec.Collectors.Windows/Collectors/StartupCollector.cs`.
  Read-only registry reads, no elevation: current-user and all-users
  Run and RunOnce keys (64-bit and 32-bit views), plus the
  StartupApproved state for the current user's Run key. A key that
  cannot be read is listed as unreadable instead of failing the
  scan; a missing key is normal and counts as read with zero entries.
  The list is capped at 100 entries. The component is
  NotMeasurable, so it does not change the score. TrueSpec only
  lists entries: it never disables or removes anything.
- The component also appears in the honest fallback catalogue on
  non-Windows machines ("Not available"), and has its own nav item.
- Tests: `StartupProgramParserTests` (Core, runs everywhere) and
  `StartupCollectorTests` (Windows leg only, reads the real registry).

## v1.1 — next

- **Storage volume breakdown.** The Storage component reports
  physical disks (health, wear, power-on hours) but not volumes:
  per-drive-letter total/free space and filesystem, from
  Win32_LogicalDisk (read-only, standard user). The open design
  question is whether volumes belong on the Storage page or get
  their own component; that decision, not the data, is why this is
  v1.1 and not v1.0.1.
- **Windows Update status.** Drivers are already covered (signed /
  old driver inventory). What is missing is patch state: last
  successful update install from the Windows Update registry results
  and the installed-hotfix list (Win32_QuickFixEngineering), both
  read-only. Needs care before it ships: on WSUS / managed machines
  the "last success" time can be misleading, and the UI must say
  when the machine is managed instead of implying it is unpatched.
- **Battery-health alerts.** A small extension of the History work:
  when a new scan's battery health drops below a band (or falls
  faster than the previous scans), say so on the History page. The
  trend data already exists; only the band rule and wording are new.

## v1.2 — later

- **Driver age summary on the History page.** Driver dates are
  already collected; trending the oldest-driver date across scans
  shows whether a machine is being maintained. Low value until the
  Windows Update status above lands, so it waits behind it.
- **Export the history table as CSV.** The History page's table is
  the only view without an export. Small, but only useful once
  people actually accumulate months of scans.
- **Multi-machine history.** The store and the trend analyser
  already refuse to mix machines. Comparing two machines side by
  side is a real buyer use case (two used laptops, one choice), but
  it needs UI for picking machines and a clear "different machines"
  presentation; it is a design task, not a data task.

## Rejected

- **PDF via QuestPDF / iText / PdfSharp.** QuestPDF is MIT-licensed
  and well-established, but it is a large dependency for a one-page
  report, and PDF libraries in general have a history of licence
  changes (iText moved to AGPL). The hand-rolled writer is ~300
  lines, has no licence risk, no native code, and is verified by
  xref tests plus a real reader. If the PDF ever needs tables,
  images or embedded fonts, revisit a library then.
- **Disk-cleanup actions / hints that delete files.** A buyer-check
  tool must never delete, and even "safe" temp-file cleanup needs
  directory enumeration that is slow and permission-sensitive on a
  standard-user account. Free-space reporting (v1.1) answers the
  real question ("is this drive nearly full?") without the risk.
- **Driver auto-update / "fix my drivers".** Downloading and
  installing drivers is a network action with real bricking risk,
  and it contradicts the offline-first rule. Reporting old drivers
  (already shipped) is the honest version.
- **Cloud sync / account for history.** History is local SQLite by
  design. Syncing scans to a server would need accounts, uploads
  and a privacy story for a tool whose promise is "nothing leaves
  this machine".
- **Startup "risk scores" / flagging unknown programs.** A
  heuristic that calls a program suspicious from its name would be
  an invented judgement, not a measurement. The list, the command
  and the enabled state are facts; what to do about them is the
  buyer's call.
- **Windows Update via the Windows Update Agent COM API.**
  `IUpdateSession` can trigger update scans and needs COM apartment
  care; the registry + hotfix list gives the same "when was this
  machine last patched" answer read-only. If that ever proves
  unreliable, revisit.
- **Battery runtime estimate / remaining-time prediction.** The
  firmware's EstimatedRunTime is a snapshot, not a forecast, and a
  prediction from one reading would be an invented number. The
  Tests page already says this is not in the build.
