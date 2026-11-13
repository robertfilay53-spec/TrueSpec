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

## v1.1 — shipped in this release

### 1. Storage volume breakdown (on the Storage page)

The Storage component reported physical disks (health, wear, power-on
hours) but not volumes. It now also reports per-drive-letter total/free
space and filesystem from Win32_LogicalDisk (read-only, standard user).

- Core: `TrueSpec.Core/Collectors/StorageVolumes.cs`
  (`StorageVolume` + `StorageVolumeParser`). The parser is the contract:
  only local fixed disks (DriveType 3) build a volume; network drives,
  CD-ROM and RAM disks are dropped. Free percent is derived from the two
  measured byte counts; a missing or zero size, or free larger than total
  (impossible data), returns null and renders as "Not reported by
  hardware", never a zero.
- Windows: `StorageCollector.AddVolumes`, called after the per-drive spec
  loop. One spec group per volume, C: first then alphabetical: `Volume
  {letter} - Total space`, `Volume {letter} - Free space`, `Volume
  {letter} - Free space (%)` (one decimal), `Volume {letter} -
  Filesystem`. No local volumes at all renders a single `Volumes` spec
  as "Not reported by hardware". A WMI failure returns no rows and
  degrades to that fallback, never a failed collector. The headline and
  the storage health rule are untouched: the rule matches `Wear level`
  by exact label, and no volume label collides with it.
- Tests: `StorageVolumeParserTests` (Core, runs everywhere) and
  `StorageCollectorTests` (Windows leg only, runs the real collector
  and asserts a `Volume C:` spec exists).

**Design decision (the open question this release answers): volumes
belong on the Storage page, not in their own component.** Three reasons,
all from the existing structure. First, volumes are the logical view of
the same storage subsystem the Storage page already reports — in
domain terms they are entities inside the Storage bounded context, not
a second context, and the buyer question is the same ("is this drive
nearly full?"). Second, a separate component would add a 19th nav item
while the left rail only fits ~16 items at the default 1280x800 window;
History already had to be moved to stay visible in v1.0.1, and growing
the rail risks the same below-the-fold defect. Third, Win32_LogicalDisk
is readable by a standard user, like the rest of the Storage
collector's degraded paths, so no new elevation requirement appears.

### 2. Windows Update status (new Updates component)

Drivers were already covered (signed / old driver inventory). Patch
state was missing; it is now a component of its own: last successful
update install and check from the Windows Update registry results, plus
the installed-hotfix list (Win32_QuickFixEngineering), both read-only.
The Windows Update Agent COM API (IUpdateSession) is deliberately not
used (see "Rejected").

- Core: `TrueSpec.Core/Collectors/WindowsUpdateStatus.cs`
  (`InstalledHotfix` + `WindowsUpdateStatus` + `WindowsUpdateStatusParser`).
  The parser is the contract: `LastSuccessTime` parses from the
  registry's "yyyy-MM-dd HH:mm:ss" local-time format (anything
  unparseable is null); hotfix dates parse from the locale-formatted
  `InstalledOn` string (unparseable stays null, the hotfix still lists,
  sorted after dated ones); `OrderHotfixes` is newest first, capped at
  10 shown (the full count is reported separately, so the cap never
  looks like the total); `DaysSince` returns null for a missing or
  future date (a future date is a bad clock, not "updated tomorrow").
  `Headline` builds the component headline from what is actually
  known, in four forms: count and date known ("42 updates installed -
  last update 2026-11-05"), count known and date unknown, count unknown
  and date known ("Last update 2026-11-05"), neither known ("Last
  update not reported"). The headline never contradicts a spec on the
  same page.
- Windows: `TrueSpec.Collectors.Windows/Collectors/UpdatesCollector.cs`.
  Registry reads are read-only (`writable: false`, 64-bit view) and
  each value is wrapped so a denied, missing or malformed key means
  unreadable, never an exception out of the collector. The component is
  NotMeasurable, so the score is unchanged. Managed machines: a WSUS
  server configured by policy (`WUServer`) means the organization
  controls updates, and the UI says so — the "Update management" spec
  reads "Managed - updates controlled by your organization (WSUS:
  ...)", and the page text explains the last-success time is the last
  install the organization allowed, not proof every patch is installed.
  The component reports NotAvailable only when the install time AND
  the check time are both null AND the hotfix query returned no rows
  AND no WSUS server is configured. TrueSpec never installs updates
  or changes update settings.
- App: new nav item "Updates" after "Drivers" (History moves to index
  14, still inside the 0-15 visible region); the component also appears
  in the honest fallback catalogue on non-Windows machines.
- Tests: `WindowsUpdateStatusParserTests` (Core, runs everywhere,
  including all four headline forms) and `UpdatesCollectorTests`
  (Windows leg only, reads the real registry and WMI; no dates or
  counts asserted, they depend on the machine). `NavigationTests` pins
  the Updates page and its routing to the component detail view.

### 3. Battery-health alerts (on the History page)

A small extension of the History work: when a new scan's battery health
drops below a band, or falls faster than the previous scans, the History
page says so in an alert card above the trend sparklines. The trend
data already existed; only the band rule and wording are new.

- Core: `HistoryTrendAnalyzer.BatteryHealthAlert` in
  `TrueSpec.Core/History/HistoryTrends.cs`, surfaced as
  `HistoryTrend.BatteryAlert` (null = no alert). Two rules, both aligned
  with the battery bands already in `HardwareMath` (Normal 80-100%,
  Worn 60-79%, Replace below 60%) — no second scale is invented:
  **band drop**: the latest measured health sits in a worse band than
  the first measured health ("Battery health dropped from Normal
  (85%) to Worn (78%) since the first saved scan."); **fast fall**:
  the latest measured health is more than 5 points below the previous
  measured health ("Battery health fell 8 points between the last two
  scans that reported it (78% to 70%)."). Normal scan-to-scan variation
  is under a point, so a drop over 5 points in one interval means the
  battery fell faster than the previous scans' pace. Both rules can
  fire in one alert string. Fewer than two measured values, no band
  change, and no fast fall means no alert; a battery already in the
  Replace band on the first scan does not alert here (the Battery page
  already names that band). Gaps are skipped, never treated as zero.
- App: `HistoryViewModel` exposes `BatteryAlertText` /
  `HasBatteryAlert`, derived from the trend (the ViewModel holds no
  second copy of the rule); `HistoryView` shows one alert card, visible
  only when an alert exists, reusing the existing card style.
- Tests: `BatteryHealthAlertTests` (Core, runs everywhere: steady
  health, both band drops, fast fall within one band, single scan,
  gaps, already-Replace, the 80% / 5-point boundaries).

## v1.2 — shipped

- **Driver age summary on the History page.** Driver dates are
  already collected; trending the oldest-driver date across scans
  shows whether a machine is being maintained. Its prerequisite (the
  Windows Update status above) shipped in v1.1, so it is unblocked.
  Shipped as a summary card above the History table
  (`HistoryTrends.BuildDriverAgeSummary`, surfaced by
  `HistoryViewModel.DriverAgeSummaryText`).
- **Export the history table as CSV.** The History page's table is
  the only view without an export. Small, but only useful once
  people actually accumulate months of scans. Shipped as an
  "Export history as CSV" button on the History page
  (`HistoryCsvExporter`, UTF-8 with BOM, one row per scan, agreeing
  with the table row for row).
- **Multi-machine history.** The store and the trend analyser
  already refuse to mix machines. Comparing two machines side by
  side is a real buyer use case (two used laptops, one choice).
  Shipped as a machine picker on the History page
  (`MachineHistoryComparer`): the two machines' latest scans
  compared row by row (score, battery health, storage wear, oldest
  driver date).

## v2.0.3 — shipped

- **Technician mode: fleet summary.** The plan's Beyond v2.0 item 1,
  first step: a Fleet page listing every machine in the local snapshot
  store (latest score, worst component, last scan date, scan count),
  lowest score first, with a CSV export of the table. Collection needs
  no new code: the portable buyer kit already saves every machine's
  scans into the same store on the stick, and the store already prunes
  per machine (newest 200). Core: `TrueSpec.Core/History/FleetSummary.cs`
  (pure functions; the worst component is the lowest-health row of the
  score breakdown, null when nothing is measurable); store:
  `ISnapshotStore.GetLatestPerMachineAsync` (one window-function query:
  latest row per machine plus scan count); reports: `FleetCsvExporter`
  (UTF-8 with BOM, same escaping as the History CSV). App:
  `FleetViewModel` + `FleetView` (new nav item "Fleet" after Compare,
  so History keeps its visible slot). Tests: `FleetSummaryTests`,
  `FleetCsvExporterTests`, store tests for the new query,
  `FleetViewModelTests`, navigation tests.

## v2.0.4 — shipped

- **Technician mode: per-machine CSV export.** The plan's Beyond v2.0 item 1,
  second step: every row of the Fleet table has an Export CSV button that
  writes that one machine's latest scan as its own file (header block with
  the fleet table's values for the machine, then one row per scored
  component, lowest health first). The machine name goes through
  `SafeReportFileName`, so a hostile machine name cannot inject a path
  through the file name. Core: `TrueSpec.Core/Reports/MachineCsvExporter.cs`
  (UTF-8 with BOM, same escaping as the Fleet CSV); App:
  `ExportOrchestrator.ExportMachineCsvAsync` plus a per-row command on
  `FleetViewModel` (the row's snapshot comes from the map the page builds
  while loading). Tests: `MachineCsvExporterTests`, `FleetViewModelTests`
  (per-row command).
- **Fleet verdict text fixed.** The fleet table and its CSV showed the raw
  enum name ("UsedHealthy") instead of the display text the rest of the
  app shows ("Used - healthy"). `FleetSummary.Build` now uses
  `Verdict.ToDisplayText()`; the shared `CsvDisplayText` helper (score /
  percent text) is used by the Fleet and per-machine exporters. Tests:
  `FleetSummaryTests`, `FleetCsvExporterTests`.

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
