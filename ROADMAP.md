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

## v2.0.5 — shipped

- **Honest limits: the MemTest86 direction.** The world-class plan
  (section 8) says MemTest86 is the one tool this plan deliberately does
  not replace, and the product should say that on the memory page rather
  than hide it — that sentence is a feature, the proof the rest of the
  claims are honest. The RAM page now says TrueSpec's memory check is a
  quick in-OS pattern exercise during the load test, not a definitive RAM
  test, and that no in-OS tool can replace the bootable MemTest86. When
  the pattern check does not verify, the load-test verdict directs the
  user to run MemTest86. Core: `LoadTestAnalyzer.BuildVerdict` (failed
  note gains the MemTest86 direction; a passing check never mentions
  it); Windows: the RAM collector's `WhatThisMeans` carries the standing
  note. Tests: `LoadTestAnalyzerTests` (failed note names MemTest86,
  passing verdict does not), `RamCollectorTests` (Windows leg only,
  runs the real collector and pins the RAM page text).
- **Dependency audit re-attested for v2.0.4.** `SECURITY.md` said
  v2.0.1; the audit was re-run with
  `dotnet list package --vulnerable --include-transitive` on all four
  projects (no vulnerable packages, including transitive). No package
  version changed between v2.0.1 and v2.0.4 (only the `<Version>` bump,
  a comment, and test-file exclusions — zero `PackageReference`
  changes), so the result carries forward.

## v2.0.6 — shipped

- **Windows Memory Diagnostic result on the RAM page.** The "Memory
  test" row said "Not run" without ever checking the log — a hardcoded
  label, not a measurement. It now reads the System event log
  (provider `Microsoft-Windows-MemoryDiagnostics-Results`, result
  events 1101 / 1201 for a pass, 1102 / 1202 for a failure, newest
  first) and reports "Passed on &lt;date&gt;" or "Failed on
  &lt;date&gt;". When the log holds no result event it says "Not run",
  which is now a measured statement because the log was actually read;
  when the log cannot be read it says "Not reported by hardware"
  instead of guessing. Core: `MemoryDiagnosticParser` (pure, event id
  + timestamp only, never message text); Windows: the RAM collector
  reads the log with a provider-filtered, capped query (same pattern
  as the crash collector). Tests: `MemoryDiagnosticParserTests`
  (both legs), `RamCollectorTests` (Windows leg only, pins the honest
  value shape).
- **Dependency audit re-attested for v2.0.5.** `SECURITY.md` said
  v2.0.4; the audit was re-run with
  `dotnet list package --vulnerable --include-transitive` on all four
  projects (no vulnerable packages, including transitive). No package
  version changed between v2.0.4 and v2.0.5 (only the `<Version>` bump
  and a test-file exclusion — zero `PackageReference` changes), so the
  result carries forward.

## v2.0.7 — shipped

- **Every WMI query pinned.** The pinning tests (`CollectorWmiSelectionTests`)
  covered System, Motherboard, CPU, Battery, GPU, RAM, Drivers, Machine
  DNA and Software, but Network, Display, Storage, Security, Crash and
  Updates still wrote their queries inline — the exact shape that let
  the F9 BIOS-vendor defect happen (a property read but not selected
  comes back null, and the value silently shows as not reported). Their
  queries are now named `internal const string` constants on the
  collector, and the pinning tests assert each SELECT list contains
  every property the collector reads: Network (adapter, configuration,
  Bluetooth, driver lookup), Display (EDID, video controller, basic
  display params), Storage (physical disk, logical disk, partition,
  reliability counter), Security (Device Guard, antivirus, firewall,
  TPM, BitLocker volume), Crash (Windows install date), Updates
  (hotfix list). The driver lookup keeps its interpolated WHERE clause
  as a format string; the SELECT list is fixed. Startup has no WMI
  query (registry only), so there is nothing to pin for it.
- **Export privacy note on the Report page.** An exported report is
  handed to a seller, and it contains hardware serials, MAC addresses,
  IP addresses and Wi-Fi network names. The Report page now says so
  under the export buttons (`ReportViewModel.ExportPrivacyNote`), so
  the buyer knows before exporting. Tests: `ReportViewModelTests`
  (the note names all four identifier kinds).
- **Dependency audit re-attested for v2.0.6.** `SECURITY.md` said
  v2.0.5; the audit was re-run with
  `dotnet list package --vulnerable --include-transitive` on all four
  projects (no vulnerable packages, including transitive). No package
  version changed between v2.0.5 and v2.0.6 (only the `<Version>` bump
  — zero `PackageReference` changes), so the result carries forward.

## v2.0.8 — shipped

- **History timestamps: latest scan by parsed time, not ISO string
  order.** A scan's capture time is stored as ISO with an offset, and
  two such strings sort lexicographically out of order across a DST
  change: 01:30 at -04:00 (05:30 UTC) sorts after 01:15 at -05:00
  (06:15 UTC) as text, even though it is older. Two readers picked by
  string order and could get the wrong row: `GetLatestPerMachineAsync`
  (the Fleet page's latest scan per machine — its own comment admitted
  a machine scanned twice inside the transition hour could show its
  second-latest scan as latest) and the retention prune in `SaveAsync`
  (which could evict the truly-newest scan once a machine passed 200
  rows). Both now compare parsed `DateTimeOffset` values in C#, the
  same rule `GetHistoryAsync` already used for the History list: the
  latest-per-machine read groups rows in C# and picks the max by
  (parsed time, id string descending for a deterministic tie-break),
  and the prune keeps the newest 200 by the same order, deleting the
  rest in chunks. A timestamp that does not parse sorts as the oldest
  in the prune (the app always writes round-trip ISO, so an
  unparseable value is a hand-edited row). Core:
  `SqliteSnapshotStore` (`CompareNewestFirst` + `TryParseCapturedAt`,
  shared by both paths). Tests: `SnapshotStoreTests` (DST latest pick,
  same-timestamp id tie-break, DST prune keeping the true newest over
  200 older rows).
- **Dependency audit re-attested for v2.0.7.** `SECURITY.md` said
  v2.0.6; the audit was re-run with
  `dotnet list package --vulnerable --include-transitive` on all four
  projects (no vulnerable packages, including transitive). No package
  version changed between v2.0.6 and v2.0.7 (only the `<Version>` bump
  — zero `PackageReference` changes), so the result carries forward.

## v2.0.9 — shipped

- **History sensor-log chart: time-proportional horizontal axis.** The
  chart plotted samples by index, so its line lied about time: two
  samples an hour apart (the app was closed, the machine slept) looked
  adjacent, and a burst a second apart looked stretched. The chart now
  positions samples by their timestamps, so the horizontal axis is time,
  not sample count. The line breaks at a stretch where logging was not
  running — more than 60 seconds between two consecutive measured
  samples, twice the maximum 30-second refresh interval (`AppSettings.
  MaxRefreshSeconds`) — in addition to the existing break where the
  sensor did not report. The mapping is a pure function in Core
  (`SensorLogChartMapper.BuildSegments`, `SensorLogChartPoint`):
  points are sorted by time (a hand-edited row with an out-of-order
  timestamp cannot invert the axis), fractions are clamped to the
  measured span, a segment with fewer than two points is dropped, and
  when every sample shares one instant the mapping falls back to even
  spacing by order. The control (`SensorLogChart`) now takes segments
  instead of values and renders each one as a single line; the
  ViewModel exposes `CpuTemperatureLogSegments` and, when no segment
  can be drawn (every reading stands alone across gaps), shows an
  empty state that says so instead of a blank chart. The caption under
  the chart now says the axis is time and names both kinds of gap.
  Tests: `SensorLogChartMapperTests` (even and uneven spacing,
  sensor-gap break, logging-gap break and its 60/61-second boundary,
  single-point and empty input, all-timestamps-equal fallback,
  unsorted input, values pass through exactly) and the updated
  `HistorySensorLogViewModelTests` (partial gaps still chart with
  time-proportional positions, readings too far apart show the empty
  state) and `SensorAlertViewModelTests` (chart segments carry the
  threshold test's values at 0, 0.5, 1).
- **Dependency audit re-attested for v2.0.8.** `SECURITY.md` said
  v2.0.7; the audit was re-run with
  `dotnet list package --vulnerable --include-transitive` on all four
  projects (no vulnerable packages, including transitive). No package
  version changed between v2.0.7 and v2.0.8 (only the `<Version>` bump
  — zero `PackageReference` changes), so the result carries forward.

## v2.1.0 — shipped

- **Technician mode: import JSON reports into the fleet store.** The
  plan's Beyond v2.0 item 1, next step after the fleet table (v2.0.3)
  and the per-machine CSV (v2.0.4): the portable stick already collects
  scans into one store, but a machine scanned with the installed app
  had no way into the fleet. The Fleet page now has an "Import JSON
  reports" button that reads the JSON files the Report page exports
  (`JsonReportExporter`, schema version "1.0") and saves each valid
  scan into the local store, so those machines appear in the fleet
  table. Core: `SnapshotReportImporter` (pure, runs on both legs):
  the file must parse as a TrueSpec report, carry schema version
  "1.0", hold a scan with a machine name and a finite score; a finite
  score outside 0-100 is clamped into range (the same rule a component
  snapshot applies to a health value) instead of rejecting the file
  over one number; files over 10 MB are refused without being read.
  A bad file is a failed import with a plain-English reason, never an
  exception and never a partial scan. App: `FleetViewModel.
  ImportReportsCommand` with a `ReportFilePicker` supplied by the
  view (the same seam as the Settings folder picker); each file is
  imported and saved in turn, so one bad file never stops the rest,
  and the fleet reloads afterwards. Saving uses the store's upsert,
  so importing the same file twice stores one scan. Tests:
  `SnapshotReportImporterTests` (export round-trip with DNA parts,
  wrong schema, missing scan, empty machine name, score clamping both
  directions, oversized file, duplicate import into a real SQLite
  store) and `FleetViewModelTests` (import of one good + one bad file,
  no picker, cancelled picker).
- **Dependency audit re-attested for v2.0.9.** `SECURITY.md` said
  v2.0.8; the audit was re-run with
  `dotnet list package --vulnerable --include-transitive` on all four
  projects (no vulnerable packages, including transitive). No package
  version changed between v2.0.8 and v2.0.9 (only the `<Version>` bump
  — zero `PackageReference` changes), so the result carries forward.

## v2.1.1 — shipped

- **Portable buyer kit works out of the box.** The released portable
  zip shipped only the exe, so a buyer who extracted it to a USB stick
  ran with installed-app behaviour: scans landed in
  `%LocalAppData%\TrueSpec` on the seller's machine, not on the stick,
  until someone hand-created an empty `TrueSpec.portable` file next to
  the exe. There is now a switch in the app: Settings > Data location
  shows the folder the app keeps its data in and whether portable mode
  is on, and one button turns it on or off by creating or removing the
  marker file next to the exe. It takes effect after a restart, and
  scans already saved stay in the folder they were saved in. Enabling
  is refused for a system or protected app folder (portable mode would
  fall back to the default there anyway), and when the
  `TRUESPEC_PORTABLE` environment variable still requests portable mode
  after the marker is gone, the message says so instead of pretending
  it is off. A manual portable publish (`win-x64-portable` profile)
  now writes the marker into the publish folder, so copying that folder
  to a stick is enough. Core: `PortableMode` (status + marker switch,
  both legs); App: `SettingsViewModel` data-location section +
  `SettingsView` card. Tests: `PortableModeTests` (status cases, enable
  / disable, invalid folder refused, unwritable folder fails honestly,
  env-var edge cases) and `SettingsViewModelTests` (status text, toggle
  creates / removes the marker, env-var note) and the extended
  `PortablePublishProfileTests` (marker target in the profile). One
  honest gap remains, and it needs a workflow change, not code: the
  released portable zip still ships only the exe, so a downloaded zip
  needs the Settings switch flipped once, until the release workflow's
  zip step is updated to include the marker.
- **Dependency audit re-attested for v2.1.0.** `SECURITY.md` said
  v2.0.9; the audit was re-run with
  `dotnet list package --vulnerable --include-transitive` on all four
  projects (no vulnerable packages, including transitive). No package
  version changed between v2.0.9 and v2.1.0 (only the `<Version>` bump
  — zero `PackageReference` changes), so the result carries forward.

## v2.1.2 — shipped

- **Technician mode: remove a machine from the fleet.** The fleet
  could import machines (v2.1.0) but never remove one: a wrong import,
  or a machine the shop no longer services, stayed in the fleet
  forever, because the store had no delete path at all. Every Fleet row
  now has a Remove button. Core: `ISnapshotStore.DeleteMachineAsync`
  (exact stored-name match, returns the deleted-row count; a prefix or
  a case variant is a different machine and is never touched). App:
  `FleetViewModel.RemoveMachineCommand` with a two-step confirm on the
  row — the app has no modal confirm dialog, so the first click arms
  the button to "Confirm remove" and names the scan count, and only
  the second click deletes; the armed flag resets when the fleet
  reloads. The status line states the store's own deleted-row count,
  including 0 when the scans were already gone. Deleting touches only
  TrueSpec's own history file, never the machine itself. Tests:
  `SnapshotStoreTests` (exact-match only, unknown machine returns 0,
  delete-then-resave works, blank name throws) and
  `FleetViewModelTests` (arm then delete, already-gone reports
  honestly, null store reports unavailable, button text follows the
  armed state).
- **Dependency audit re-attested for v2.1.1.** `SECURITY.md` said
  v2.1.0; the audit was re-run with
  `dotnet list package --vulnerable --include-transitive` on all four
  projects (no vulnerable packages, including transitive). No package
  version changed between v2.1.0 and v2.1.1 (zero `PackageReference`
  changes in `git diff 38dc7d4 a31d021 -- '*csproj'`), so the result
  carries forward.

## v2.1.3 — shipped

- **History: delete one saved scan.** The fleet could remove a whole
  machine (v2.1.2), but the History table could not remove a single
  scan: a bad or duplicate scan stayed in the trend forever, because
  the store had no single-scan delete path. Every History row now has
  a Remove button. Core: `ISnapshotStore.DeleteSnapshotAsync` (exact
  id match, returns the deleted-row count; an empty id is rejected
  rather than treated as a no-op) and `HistoryTrend.SnapshotIds`,
  which names, in the same order as the points, the saved snapshot
  each point came from, so a table row can never drift from its scan.
  App: `HistoryViewModel.RemoveScanCommand` with the same two-step
  confirm as the fleet — the first click arms the button to "Confirm
  remove" and names the scan date, and only the second click deletes;
  the armed flag resets when the trend reloads. The status line states
  the store's own deleted-row count, including 0 when the scan was
  already gone, and names how many scans remain. Deleting touches
  only TrueSpec's own history file, never the machine itself. Tests:
  `SnapshotStoreTests` (deletes only that scan, unknown id returns 0,
  delete-then-resave works, empty id throws),
  `HistoryTrendTests` (ids match the points in order; empty trend has
  no ids) and `HistoryRemoveScanViewModelTests` (arm then delete,
  already-gone reports honestly, last scan leaves the empty state,
  null store reports unavailable, button text follows the armed
  state). The CSV-agreement test now also asserts each row's id is
  the trend's id at the same index.
- **Dependency audit re-attested for v2.1.2.** `SECURITY.md` said
  v2.1.1; the audit was re-run with
  `dotnet list package --vulnerable --include-transitive` on all four
  projects (no vulnerable packages, including transitive). No package
  version changed between v2.1.1 and v2.1.2 (zero `PackageReference`
  changes in `git diff a31d021 0e6a153 -- '*csproj'`), so the result
  carries forward.

## v2.1.4 — shipped

- **Technician mode: export all machines.** The fleet could export
  the whole table as one CSV, or one machine at a time from its row,
  but a technician with a dozen machines clicked Export a dozen
  times. The Machines card now has an "Export all machines" button.
  App: `ExportOrchestrator.ExportAllMachinesCsvAsync` writes one CSV
  per machine into the export folder, each written exactly like the
  per-row export (same `MachineCsvExporter`, same file-name rule), so
  the files agree with the table row for row; the result record names
  the folder, the paths written, and every machine skipped with a
  plain-English reason. A machine whose latest scan cannot be read,
  or has no score, is skipped, never exported with invented numbers,
  and a write failure for one machine never stops the rest. File
  names go through `SafeReportFileName`, and a name that already
  exists in the folder (two machines whose names sanitise
  identically, exported in the same second) gets a -2, -3 suffix
  instead of overwriting the first file. `FleetViewModel.
  ExportAllMachinesCommand` has the same reentrancy guard as the
  other commands, reports "Export all is unavailable." with no
  delegate wired up, and renders the status line from the result:
  how many machines were exported, the folder, and any machine
  skipped by name with its reason. `MainViewModel` raises the same
  unmissable export confirmation as the other exports — the banner
  names the folder and the count, and "Open folder" opens the folder
  the files landed in — with no banner when nothing was exported.
  Tests: `ExportAllMachinesTests` (one CSV per machine with contents
  agreeing with `MachineCsvExporter.Build`, no-score machine skipped
  and the rest still export, missing snapshot skipped, same-second
  name collision keeps both files, a second run never overwrites the
  first, empty fleet exports nothing and creates no folder, export
  folder as an existing file throws) and `FleetViewModelTests`
  (delegate called once with the loaded rows and the snapshot map, no
  delegate reports unavailable, empty fleet never calls the delegate,
  partial export names the skipped machine and its reason, second
  click while running does nothing).
- **Dependency audit re-attested for v2.1.3.** `SECURITY.md` said
  v2.1.2; the audit was re-run with
  `dotnet list package --vulnerable --include-transitive` on all four
  projects (no vulnerable packages, including transitive). No package
  version changed between v2.1.2 and v2.1.3 (zero `PackageReference`
  changes in `git diff 0e6a153 a31402b -- '*csproj'`), so the result
  carries forward.

## v2.1.5 — shipped

- **Collector plug-ins, phase 1: versioned contract + manifest +
  discovery.** The plan's Beyond v2.0 item 2, first slice: the
  collector contract is now versioned (`CollectorContract.
  CurrentVersion = 1`, the `ICollector` surface as shipped in
  v2.1.4), and its shape is pinned by `CollectorContractTests`, so a
  change to the interface breaks the test and forces a deliberate
  version bump instead of a silent drift. A plug-in is described by
  a plugin.json manifest (id, name, version, contract version,
  assembly file, collectors) parsed by `PluginManifestParser` with
  the same strictness as the report importer: unknown fields are
  rejected, the assembly must be a plain .dll file name inside the
  plug-in's own folder (a path, a parent-directory escape, or a
  non-.dll file is rejected), collector ids follow the built-in
  shape (lowercase letters, digits, dashes) and must be unique, and
  manifests over 64 KB are refused without being read. `PluginDiscovery`
  scans the data root's Plugins folder (installed mode:
  `%LocalAppData%\TrueSpec\Plugins`; portable mode: the Plugins
  folder next to the exe, so the stick carries its plug-ins) and
  reports each folder as Ready, IncompatibleContract, InvalidManifest
  or MissingAssembly, with a plain-English reason; a missing folder
  reads as no plug-ins, never an error, and discovery never throws.
  App: the Settings page gains a Plug-ins card showing the folder,
  the count, each plug-in's name, version and status, and a Refresh
  button; the list re-runs when portable mode is toggled, because
  the toggle moves the data folder. Plug-ins are discovered but
  never loaded in this version: only signed plug-ins load, and
  plug-in signing is not set up yet. Loading is the next phase, once
  the signing story is decided. Tests: `CollectorContractTests`
  (version + interface shape pin), `PluginManifestTests` (every
  validation rule, oversized file, never throws),
  `PluginDiscoveryTests` (empty/missing folder, ready, incompatible,
  invalid, missing assembly, folder order), and
  `SettingsViewModelTests` (summary text, listing, refresh, toggle
  moves the folder).
- **Dependency audit re-attested for v2.1.4.** `SECURITY.md` said
  v2.1.3; the audit was re-run with
  `dotnet list package --vulnerable --include-transitive` on all four
  projects (no vulnerable packages, including transitive). No package
  version changed between v2.1.3 and v2.1.4 (zero `PackageReference`
  changes in `git diff a31402b 3d7a59e -- '*csproj'`), so the result
  carries forward.

## v2.1.6 — shipped

- **Linux collector parity study (Beyond v2.0, item 3).** The plan
  says study first, build only if a real user base asks for it, and
  Windows stays the shipping target. The study is the deliverable:
  every one of the 15 collectors was checked against a real Ubuntu
  24.04 machine (the same OS the CI runners use) by reading /proc and
  /sys directly and checking which tools exist on a stock install. The
  result lives as a machine-checked catalogue in the code
  (`LinuxCollectorParity` in TrueSpec.Core, pinned by
  `LinuxCollectorParityTests` against the collector list, so a
  collector can never be silently dropped from the study), and the
  table below is the human-readable form of the same rows.
  Feasibility: Partial = the core readings map but some need a tool
  that is not installed by default, need elevation, or have no
  equivalent; NotFeasible = the readings are Windows products with no
  Linux counterpart.

  | Collector | Linux source | Feasibility | What maps, what does not |
  |---|---|---|---|
  | System | /sys/devices/virtual/dmi/id + /proc/version | Partial | BIOS date/vendor/version, product name and OS version read as a standard user. Windows install date has no equivalent; serials are root-only. |
  | CPU | /proc/cpuinfo + cpufreq | Partial | Model, cores, MHz and cache from cpuinfo; rated max from cpufreq on real hardware, absent in a VM. Live temps need hwmon/lm-sensors, not installed by default. |
  | RAM | /proc/meminfo | Partial | Total/free/available read as a standard user. Per-module detail needs dmidecode (root, not installed). The Windows memory-diagnostic result has no equivalent. |
  | Storage | /sys/block + smartctl | Partial | Devices, sizes and filesystems from /sys/block. SMART/wear/power-on hours need smartctl (not installed, needs root); NVMe counters need nvme-cli (not installed). |
  | Battery | /sys/class/power_supply/BAT0 | Partial | On a laptop: design/full capacity, cycles, chemistry, manufacturer, serial and charge rate, no extra tool, no elevation. Empty on a VM with no battery. Firmware estimated-runtime has no equivalent. |
  | GPU | lspci + /sys/class/drm | Partial | lspci names the model (not installed by default); clocks, temps and VRAM need hwmon or vendor tools. This headless VM exposes no DRM device. |
  | Display | /sys/class/drm/\*/edid | Partial | The EDID is the same panel truth as on Windows, when a display is attached. This VM has no DRM connectors; xrandr is not installed by default. |
  | Network | /sys/class/net + iw | Partial | Adapters, MACs and link state from /sys/class/net. Wi-Fi standard and link speed need iw (not installed); driver dates need modinfo/ethtool. |
  | Motherboard | /sys/devices/virtual/dmi/id | Partial | Board vendor/product, BIOS vendor/version/date read as a standard user. Serials are mode 0400 root-only and were unreadable even to root on this VM. |
  | Security | No Linux equivalent | NotFeasible | Device Guard, BitLocker, Windows Defender and TPM attestation are Windows products. LUKS, AppArmor/SELinux and firewalls are different facts, not the same collector ported. A Linux security view would need a redesign, not a port. |
  | Drivers | /sys/bus + modinfo | Partial | Loaded modules and versions. Driver age maps poorly (modules carry no install date the way Windows drivers do); no signed-driver inventory equivalent. |
  | Updates | apt/dpkg history | Partial | dpkg lists installed packages with name, version and install date. No single last-update-check or last-install equivalent; unattended-upgrades logs vary by setup. |
  | Startup | systemd user units + autostart dirs | Partial | The standard autostart locations cover what launches at sign-in. No StartupApproved enabled/disabled state equivalent; the directories do not exist on a minimal install. |
  | Software | dpkg/rpm | Partial | Installed programs with name, version and install date. Windows activation state has no Linux equivalent and would simply not apply. |
  | Crash | journalctl (system journal) | Partial | Kernel oops, panics, service failures and unexpected shutdowns are in the journal. BugCheck/WHEA event IDs are Windows-only, so the crash classes would need new definitions. |

  What the probe actually found on Ubuntu 24.04: /proc/cpuinfo and
  /proc/meminfo read as a standard user; /sys/devices/virtual/dmi/id
  exposes BIOS date, vendor, version and product name, but
  product_serial is mode 0400 (root-only) and was unreadable even to
  root on this VM; /sys/class/block lists devices;
  /sys/class/power_supply is empty on a VM with no battery;
  /sys/class/drm has no connectors on a headless VM;
  /sys/class/hwmon is empty; cpufreq is absent in a VM. Tools:
  smartctl, upower, dmidecode, lspci, lsusb, lm-sensors, xrandr and
  ufw all absent on a stock install; journalctl, apt and dpkg present.

  Verdict: no collector ports as-is. 14 of 15 map partially; Security
  has no Linux equivalent. A Linux build would need to bundle or
  require smartctl, upower, dmidecode and friends, accept that serials
  are not a standard-user fact, and redesign the Security collector
  from scratch. Windows stays the shipping target; a Linux build
  starts only if users ask for it.

  Tests: `LinuxCollectorParityTests` (every collector has exactly one
  row, every row names a source and a reason, spot mappings pinned,
  Security is NotFeasible, no row reaches Full — the test pins that
  headline, so a future port claiming Full must update it
  deliberately — Get returns null for an unknown id, throws on null).
- **Dependency audit re-attested for v2.1.5.** `SECURITY.md` said
  v2.1.4; the audit was re-run with
  `dotnet list package --vulnerable --include-transitive` on all four
  projects (no vulnerable packages, including transitive). No package
  version changed between v2.1.4 and v2.1.5 (only the `<Version>` bump
  — zero `PackageReference` changes in
  `git diff 3d7a59e 526ed0a -- '*csproj'`), so the result carries
  forward.

## v2.1.7 — shipped

- **Localisation, phase 1: the navigation rail in German (Beyond
  v2.0, item 4).** The plan lists localisation after the feature set
  is stable, and the set is stable now (U1–U12, technician mode,
  plug-ins phase 1, the Linux study). The first slice is the
  foundation plus one complete surface, not a half-translated app:
  a string-key catalogue (`AppStringKeys`, 25 keys), strict pack
  validation (`LanguagePack.Validate`: a missing key, an unknown key,
  an empty translation or a malformed code all fail), built-in
  English and German packs (`LanguagePacks`, in code like the Linux
  parity catalogue), and a localizer (`StringLocalizer`) whose
  fallback rule never shows a blank — a missing translation falls
  back to English, an unknown key shows the key itself — and never
  throws. The localizer never changes the thread culture, so measured
  values keep the app's existing number and date formatting:
  localisation touches chrome only, never a measurement, a unit or a
  threshold. The translated surface is the navigation rail (all 22
  items, same keys in the same order, only the labels change) and the
  Settings language card itself, so picking a language switches the
  card too. Settings has a Language picker (English / Deutsch); the
  choice is saved in `AppSettings.LanguageCode` (sanitized to a
  supported code, unknown codes fall back to English) and applies
  right away — the main view model rebuilds the rail on settings
  apply, re-selecting by key so the user stays on the same page, and
  the rebuild never navigates (a guard flag), because re-selecting
  during the Settings page's own construction would re-enter page
  creation while `GetOrCreate` has not cached the page yet. Detail
  pages stay English in this version and follow page by page in
  later versions; the card says so. Tests: `LanguagePackTests`
  (catalogue coverage in every pack, validation errors, fallback
  rules, code normalization, the pinned German terms),
  `SettingsTests` (language default/sanitize/round-trip),
  `NavigationTests` (German rail keys, order and all 22 labels,
  unknown-language fallback) and `SettingsViewModelTests` (picker
  defaults, picking German rebuilds the rail and keeps the page,
  persistence across a fresh shell, the construction guard, unknown
  stored code).
- **Dependency audit re-attested for v2.1.6.** `SECURITY.md` said
  v2.1.5; the audit was re-run with
  `dotnet list package --vulnerable --include-transitive` on all four
  projects (no vulnerable packages, including transitive). No package
  version changed between v2.1.5 and v2.1.6 (only the `<Version>` bump
  — zero `PackageReference` changes in
  `git diff 526ed0a 859308c -- '*csproj'`), so the result carries
  forward.

## v2.1.8 — shipped

- **Localisation, phase 2: the Settings page in German (Beyond
  v2.0, item 4).** Phase 1 translated the rail; this version
  translates the next complete surface, the page the picker itself
  lives on, so picking German re-renders the whole Settings page
  with no restart. All 49 new keys live in the catalogue
  (`AppStringKeys`, now 74 keys) and in both packs, pinned by the
  existing coverage test (a pack that misses a key fails). The
  translated surface is every card the page owns: header, Appearance
  (theme labels and the "Dark (default)" marker), refresh rate, sensor
  logging and alerts, export folder, data location (all five portable
  summaries and both toggle actions), plug-ins (card chrome and the
  count lines), and Privacy. Two template strings carry numbers
  (`AlertSummaryOn`, `PluginsSummaryMany`): they are formatted by
  the new `StringLocalizer.GetFormatted`, which formats with the
  invariant culture, so "90" stays "90" inside a German sentence,
  and returns the template itself instead of throwing when a
  translation's placeholders do not match. The refresh-rate and
  duration pickers no longer use a converter: their options are
  (value, text) pairs rebuilt for the current language
  (`RefreshOption`), so the picker shows "5 Sekunden" and still saves
  the number 5. The old `RefreshSecondsToTextConverter` is deleted —
  it had no language to read, and a converter cannot see the view
  model. When the language changes, the view model raises every
  localized property, including the two option lists, and re-resolves
  a rejection message shown before the switch, so no text stays in
  the old language. Deliberately NOT translated in this version, and
  named on the page's own terms in the docs: the plug-in list's
  per-item status/reason and the portable-toggle result message are
  diagnostic strings produced by Core (`PluginDiscovery`,
  `PortableMode`), which take no language parameter; threading one
  through those APIs is a later "Core strings" slice. The language
  card's note now says Navigation and Settings are translated.
  Tests: `LanguagePackTests` (German Settings terms pinned,
  `GetFormatted` with invariant numbers, fallback to the English
  template, never throws on a bad template) and
  `SettingsViewModelTests` (a German-seeded shell renders the whole
  page, picking German re-renders it and back again, the alert summary
  shows its numbers in German, plug-in counts in German with the
  per-item status still English).
- **Dependency audit re-attested for v2.1.7.** `SECURITY.md` said
  v2.1.6; the audit was re-run with
  `dotnet list package --vulnerable --include-transitive` on all four
  projects (no vulnerable packages, including transitive). No package
  version changed between v2.1.6 and v2.1.7 (only the `<Version>` bump
  — zero `PackageReference` changes in
  `git diff 859308c 233fbf0 -- '*csproj'`), so the result carries
  forward.

## v2.1.9 — shipped

- **Localisation, phase 3: the Core diagnostic strings in German
  (Beyond v2.0, item 4).** Phase 2 translated the Settings page but
  left two strings English, naming them honestly: the plug-in list's
  per-item status/reason and the portable-toggle result message, both
  produced by Core code that took no language parameter. This version
  threads a language code through those APIs, so the whole Settings
  page is translated now, with no string left behind. The catalogue
  grows by 45 keys (74 to 119): 6 plug-in status texts, 5 plug-in
  reasons, 2 discovery errors, 22 manifest-parser errors, and 10
  portable-toggle messages, each in both packs, pinned by the
  existing coverage test (a pack that misses a key fails). The
  language parameter is optional everywhere and defaults to English,
  so every existing caller and test keeps its exact English strings:
  `PluginManifestParser.Parse(stream, languageCode)`,
  `PluginDiscovery.Discover(folder, languageCode)`,
  `PortableMode.SetEnabled(dir, enable, env, languageCode)`.
  `DiscoveredPlugin` carries its language (`LanguageCode`, null reads
  English) and its `StatusText` resolves from the status enum through
  the localizer; the reason is formatted in the language at discovery
  time, including the parser's error, which the parser now localizes
  at the source. `PortableModeToggleResult` carries its message as a
  key plus arguments (`MessageKey`, `MessageArgs`) with a computed
  `Message`, so the Settings page can re-resolve it when the language
  changes without toggling again (a `with { LanguageCode = ... }`
  copy). The view model passes its language to discovery and to the
  toggle, re-discovers the plug-ins when the language changes, and
  re-resolves a toggle message shown before the switch. File names,
  manifest field names, collector ids and the TRUESPEC_PORTABLE
  variable name stay untranslated inside the sentences; numbers in a
  translated template keep the invariant formatting. Tests:
  `LanguagePackTests` (German core strings pinned, templates format
  with invariant numbers), `PluginManifestTests` (German errors,
  unknown-language fallback), `PluginDiscoveryTests` (German
  status/reason for all four statuses, bad-manifest reason in
  German, fallback), `PortableModeTests` (German toggle messages,
  env-var case in German, fallback, re-resolution by `with` copy),
  `SettingsViewModelTests` (the v2.1.8 test that pinned the status
  staying English now pins it German; picking German re-discovers
  the list in German; a toggle message re-resolves on the switch).
- **Dependency audit re-attested for v2.1.8.** `SECURITY.md` said
  v2.1.7; the audit was re-run with
  `dotnet list package --vulnerable --include-transitive` on all four
  projects (no vulnerable packages, including transitive). No package
  version changed between v2.1.7 and v2.1.8 (only the `<Version>` bump
  — zero `PackageReference` changes in
  `git diff 233fbf0 1188692 -- '*csproj'`), so the result carries
  forward.

## v2.1.10 — shipped

- **Localisation, phase 4: the component detail pages in German
  (Beyond v2.0, item 4).** Phases 1-3 translated the rail, the
  Settings page and the Core diagnostic strings; this version
  translates the next complete surface, the shared detail view
  (`ComponentDetailView`), which every one of the 15 component pages
  uses — one surface, all detail pages at once. The catalogue grows
  by 14 keys (119 to 133): the six section labels (Specifications,
  search placeholder, copy hint, Age signals title + intro, What this
  means), the captured line, the two empty states (nothing collected,
  no search match), the skipped-checks note, the untrusted-signal
  warning, and the "(ESTIMATE)" suffix, each in both packs, pinned
  by the existing coverage test (a pack that misses a key fails).
  Templates carry their dynamic values through
  `StringLocalizer.GetFormatted` with the invariant culture, so the
  captured date ("2026-11-12 10:30:00"), the search string and the
  skipped-check count keep the app's existing formatting inside a
  German sentence. The view model takes an optional language code
  (`ComponentDetailViewModel(snapshot, id, languageCode)`), exposes
  every chrome string as a localized property, and re-renders in
  place through `ApplyLanguage` (spec rows and age-signal rows are
  rebuilt, the empty-state message recomputed for the current
  search); the shell passes its language at both construction sites
  and re-languages every cached detail page when settings are
  applied, the same pattern it already used for the History page.
  Deliberately NOT translated in this version, and named on the
  page's own terms in the docs: component names, headlines, health
  and status texts, spec labels, values and sources, and the "What
  this means" text are measured data produced by the collectors —
  translating them is a later slice. The language card's note now
  says Navigation, Settings and component detail pages are
  translated. Tests: `LanguagePackTests` (German detail terms
  pinned, LanguageNote updated) and `ComponentDetailViewModelTests`
  (English default, German-seeded whole page, switching both ways,
  unknown-language fallback, data stays English, both empty states
  in both languages, estimate suffix in both languages, age-signals
  group chrome, skipped-checks note, untrusted-signal warning,
  captured-date shape).
- **Dependency audit re-attested for v2.1.9.** `SECURITY.md` said
  v2.1.8; the audit was re-run with
  `dotnet list package --vulnerable --include-transitive` on all four
  projects (no vulnerable packages, including transitive). No package
  version changed between v2.1.8 and v2.1.9 (only the `<Version>` bump
  — zero `PackageReference` changes in
  `git diff 1188692 a0b0326 -- '*csproj'`), so the result carries
  forward.

## v2.1.11 — shipped

- **Localisation, phase 5: the Overview page in German (Beyond
  v2.0, item 4).** Phases 1-4 translated the rail, the Settings page,
  the Core diagnostic strings and the component detail pages; this
  version translates the next complete surface, the landing page
  (`OverviewViewModel` + `OverviewView`), which is the first screen a
  user sees and the one long-lived page in the app. The catalogue
  grows by 36 keys (133 to 169): the hero labels, the section
  headings, the two buttons, the score note, the key-stat labels, the
  live-strip names, the sensor-log and alert-setting sentences, the
  refresh / live-update / read-failed templates, the weight and
  contribution templates, and the scanning hero, each in both packs,
  pinned by the existing coverage test (a pack that misses a key
  fails). Templates carry their dynamic values through
  `StringLocalizer.GetFormatted` with the invariant culture, so a
  threshold ("90 °C"), a duration, a file path and a weight keep the
  app's existing formatting inside a German sentence. The refresh
  interval reuses the Settings seconds keys instead of a second copy
  of the same words. The live strip is built from stable keys
  (`LiveMetricViewModel.CpuTempKey` etc.): the display name resolves
  in the page language and re-resolves through `ApplyLanguage`, while
  the key the strip updates by never changes — a metric built with
  the legacy (name, unit) constructor keeps its old behaviour
  exactly and `ApplyLanguage` is a no-op for it. The view model
  takes an optional language code, exposes every chrome string as a
  localized property, and re-renders in place through `ApplyLanguage`
  (the snapshot-derived rows are rebuilt from the last scan, the
  cards are rebuilt with the new language, the sensor sentences are
  rebuilt from the stored settings); the shell passes its language
  at construction and re-languages the long-lived page when settings
  are applied, the same in-place pattern the detail pages use.
  Deliberately NOT translated in this version, and named on the
  page's own terms in the docs: component names, headlines, health
  and status texts, spec labels, values and sources, the score
  explanation and the verdict text are measured data produced by the
  collectors and the scoring engine — translating them is a later
  slice; the admin notice and the threshold-alert texts are
  diagnostic strings produced by Core with no language parameter
  yet. The language card's note now says Navigation, Settings,
  component detail pages and the Overview page are translated.
  Tests: `LanguagePackTests` (German Overview terms pinned,
  LanguageNote updated), `OverviewViewModelTests` (English default,
  German-seeded whole page, switching both ways, unknown-language
  fallback, data stays English, key-stat labels and values,
  weight/contribution template agreement, card labels, scanning hero,
  live strip by key, sensor sentences with writer and threshold),
  and `LiveMetricViewModelTests` (key constructor in both languages,
  colour follows the key, `ApplyLanguage` re-resolves, legacy
  untouched, unknown key shows the key).
- **Dependency audit re-attested for v2.1.10.** `SECURITY.md` said
  v2.1.9; the audit was re-run with
  `dotnet list package --vulnerable --include-transitive` on all four
  projects (no vulnerable packages, including transitive). No package
  version changed between v2.1.9 and v2.1.10 (only the `<Version>`
  bump and test-file exclusions — zero `PackageReference` changes in
  `git diff a0b0326 9925866 -- '*csproj'`), so the result carries
  forward.

## v2.1.12 — shipped

- **Localisation, phase 6: the History page in German (Beyond
  v2.0, item 4).** Phases 1-5 translated the rail, the Settings page,
  the Core diagnostic strings, the component detail pages and the
  Overview page; this version translates the next complete surface,
  the History page (`HistoryViewModel` + `HistoryView` + its row and
  winner view models) — the trend cards, the saved-scans table, the
  two-machine comparison and the value comparison, plus the CPU
  temperature log. The catalogue grows by 91 keys (169 to 260): the
  headings, the machine picker, the trend-card titles, the sensor-log
  sentences, the table headers and remove buttons, the compare and
  value labels, and the status lines (loading, empty, one-scan and
  count, read failures, remove confirmation and removed-scan counts,
  comparison prompts and results, value prompts and the honesty
  note), each in both packs, pinned by the existing coverage test (a
  pack that misses a key fails). Templates carry their dynamic values
  through `StringLocalizer.GetFormatted` with the invariant culture,
  so a machine name, a scan date, a sample count and an exception
  message keep the app's existing formatting inside a German
  sentence. The range labels reuse the table header keys (Score,
  Battery health, Storage wear) instead of a second copy of the same
  words, and the removed-scan message uses two keys (RemovedOne /
  RemovedMany) instead of the old inline plural hack. The view model
  takes an optional language code at the end of its constructor, exposes
  every chrome string as a localized property, and re-renders in place
  through `ApplyLanguage` (the table rows are rebuilt from the stored
  trend, the comparison and value rows and the winner rows from the
  stored comparison, the range texts are re-derived, and the sensor
  log is re-read); the transient status lines are re-derived from
  small stored fields (which message is showing, its scan date, its
  error), so a language switch re-resolves an armed remove
  confirmation instead of silently resetting it. The shell passes its
  language at the History construction site and re-languages the
  cached page when settings are applied, next to the existing sensor
  settings call. Deliberately NOT translated in this version, and
  named on the page's own terms in the docs: the trend summary and
  the battery / storage / driver-age forecast texts are Core-owned
  wordings produced by the trend analyzer with no language parameter
  yet; the value verdict sentence and its no-verdict reason are
  Core-owned wordings produced by ValueComparer (the asking-price row
  label is its Core-owned label too); and the table's cell fallbacks
  ("Not measurable" / "Not reported") are measured-data fallbacks
  that the CSV export agrees with row for row, so the CSV contract
  stays English — translating them is a later slice. The language
  card's note now says Navigation, Settings, component detail pages,
  the Overview page and the History page are translated. Tests:
  `LanguagePackTests` (German History terms pinned, LanguageNote
  updated) and `HistoryLocalizationViewModelTests` (English default,
  German-seeded whole page, switching both ways, unknown-language
  fallback, data stays English, status and range lines in both
  languages, remove-button text and armed confirmation across a
  switch, removed-scan messages in both languages, comparison rows +
  value rows + winner rows in German with the verdict staying
  English, value-compare-first prompt, sensor-log statuses in German
  and across a switch).
- **Dependency audit re-attested for v2.1.11.** `SECURITY.md` said
  v2.1.10; the audit was re-run with
  `dotnet list package --vulnerable --include-transitive` on all four
  projects (no vulnerable packages, including transitive). No package
  version changed between v2.1.10 and v2.1.11 (only the `<Version>`
  bump and test-file exclusions — zero `PackageReference` changes in
  `git diff 9925866 8413746 -- '*csproj'`), so the result carries
  forward.
- **`.gitignore` guard for the local-only files.** The v2.1.11 bump
  nearly committed 68 local-only files (`WORLD-CLASS-PLAN.md`,
  `LAPTOP-TEST-RESULTS-2026-11-12.md`,
  `REVIEW-NEXT-STEPS-2026-11-12.md`, `V1.4.1-FIX-BRIEF.md`,
  `logo-options/`, `release-artifacts/`) when a `git add -A` swept
  them into the bump commit; the push was killed in time and the
  commit was redone with only the named bump files. The root
  `.gitignore` now guards all six paths (verified: none of them is
  tracked, so ignoring them is safe, and `git status` no longer shows
  them as untracked). The rule stands: stage named files only, never
  `git add -A` in this repo.

## v2.1.13 — shipped

- **Localisation, phase 7: the History page's remaining Core-owned
  wordings in German (Beyond v2.0, item 4).** Phase 6 translated the
  History page's chrome; this version translates the wordings the
  page's Core layer owns, which phase 6 explicitly left out: the
  trend summary (scan counts, score / battery / wear deltas), the
  battery-health alert (band drop and fast fall), the driver-age
  summary (oldest-driver dates and the older-than-5-years counts),
  the degradation-forecast basis texts (not-enough-data, no-decline,
  already-reached and ready ranges, for battery and storage wear,
  including the step-change note and the threshold text), the value
  no-verdict reasons, and the asking-price row label (which was the
  last Core-owned label on the page: it now comes from the catalogue
  key instead of `ValueComparer.AskingPriceLabel`). The catalogue
  grows by 52 keys (260 to 312): 9 trend, 5 battery alert, 11 driver
  age (including the ", and" / ", und" clause join, so a two-clause
  driver-age sentence does not carry an English conjunction into a
  German sentence), 24 forecast, 3 value reasons. The German
  translations keep the formal "Sie" style, put the article inside
  the part-noun keys ("der Akku" / "das Laufwerk") so the step-change
  cause reads in the language's own article and gender, and use the
  passive ("wird ... erreicht") where a pronoun would force a gender
  choice. Numbers inside German sentences keep the app's existing
  invariant formatting (one decimal, dot separator), so a measured
  value reads the same in both languages.
- **Design (the phase 6 pattern).** An optional `languageCode`
  (default null, English when blank or unknown) at the END of
  `HistoryTrendAnalyzer.Analyze` / `LoadAsync`,
  `BatteryHealthAlert`, `DegradationForecaster.Forecast` /
  `ForBattery` / `ForStorageWear` and `ValueComparer.Compare`, so
  every existing caller keeps compiling and the no-language call
  produces the exact English wording it always did (pinned
  byte-for-byte by the new tests). `LoadAsync` extracts
  `LoadSnapshotsAsync`, so the view model can keep the snapshots and
  re-analyse on a language switch without re-reading the store.
  `HistoryViewModel` stores those snapshots, extracts an
  `ApplyTrend` method used by both the load path and
  `ApplyLanguage` (which re-analyses in the new language; the
  status line is not touched, so a transient remove status survives
  a switch), and re-runs the value comparison on a switch — but
  only while the price fields still hold the prices the stored
  result was built on, so a price typed since the last compare is
  never silently applied by a language switch. Deliberately NOT
  translated in this version, and named on the page's own terms in
  the docs: the U9 per-dollar verdict sentence stays English in
  every language (shown verbatim, never translated, pending Jawad's
  veto decision); the table's cell fallbacks ("Not measurable" /
  "Not reported") stay English so the CSV export still agrees with
  the table row for row. Tests: `HistoryCoreWordingsTests` (German
  trend summary / battery alert / driver-age summary / all five
  forecast cases / both step-change directions / both no-verdict
  reasons, the verdict sentence staying English, and the English
  pins) and `HistoryLocalizationViewModelTests` (the TwoScans test
  now pins the summary and both forecasts following the language
  while the row cells stay identical, a switch re-analyses the
  summary and both forecasts in place and back, the no-verdict
  reason follows the language, and the price-typed-since guard; the
  asking-price row label is German in German and English in
  English). The compare tests wait for the compare to be possible
  (`CanCompareMachines`) instead of only for the status line: on a
  loaded runner the status can leave Loading before the load
  assigns the compare pair, and the compare then silently no-ops.
- **Dependency audit re-attested for v2.1.12.** `SECURITY.md` said
  v2.1.11; the audit was re-run with
  `dotnet list package --vulnerable --include-transitive` on all four
  projects (no vulnerable packages, including transitive). No package
  version changed between v2.1.11 and v2.1.12 (only the `<Version>`
  bump and test-file exclusions — zero `PackageReference` changes in
  `git diff 8413746 0788596 -- '*csproj'`), so the result carries
  forward.
- **Hardware database: rated CPU boost clocks from a bundled dataset
  (Beyond v2.0, item 5).** The plan's last Beyond v2.0 item: a small,
  versioned, openly licensed dataset of CPU/GPU model specifications,
  used only to label "this CPU's rated boost clock is X" from the
  model string. Dataset v1.0.0 holds 74 CPUs (Intel Core 8th-14th gen
  desktop, Core Ultra 100/200 desktop and mobile, Intel 8th/10th/11th
  gen mobile; AMD Ryzen 1000-9000 desktop, Ryzen 3000-8000 and AI 300
  mobile), compiled by hand from Intel ARK and AMD product pages
  (CC0 compilation; each entry records its source URL and retrieval
  date). No third-party dataset was suitable: the open ones are stale
  or empty, the complete ones are proprietary. The CPU page shows the
  rated boost clock under its own label ("Rated boost clock") with
  the dataset version as its source, plus the E-core figure for Intel
  hybrid chips; it never replaces the measured Max clock, and a model
  that is not listed gets no rated spec. The database is optional (a
  Settings toggle, takes effect after a restart, like the
  portable-mode toggle) and the dataset version and entry count are
  shown in Settings. GPU entries are a later slice, from the same
  seed approach on NVIDIA/AMD spec pages. Deliberately absent in
  v1.0.0: the 12th/13th-gen Intel mobile H/P chips (Intel publishes
  no per-SKU P-core base frequency), the i3-8100 (no Turbo Boost),
  i5-14400, i9-11980HK and Ryzen 7 3700U. Tests:
  `HardwareDatabaseTests` (normalizer, lookup, fail-safe loading,
  invalid-entry skipping, bundled dataset pinned at 74 entries and
  the verified i7-13700K boost) and the CpuCollector rated-spec
  tests (spec present with its own label, value and source, after
  the measured Max clock; absent for unknown models and with no
  database).
- **Dependency audit re-attested for v2.1.14.** `SECURITY.md` said
  v2.1.12; the audit was re-run with
  `dotnet list package --vulnerable --include-transitive` on all four
  projects (no vulnerable packages, including transitive). No package
  version changed between v2.1.13 and v2.1.14 (only the `<Version>`
  bump and the embedded-resource entry for the dataset — zero
  `PackageReference` changes in `git diff cd0392e HEAD -- '*csproj'`),
  so the result carries forward.
- **Hardware database: rated GPU specs from the bundled dataset
  (Beyond v2.0, item 5).** The plan's stated later slice of the
  v1.0.0 CPU dataset: dataset v1.1.0 adds 53 GPUs (NVIDIA GeForce
  RTX 50/40/30-series desktop, RTX 40/30-series laptop, GTX
  1660/1660 SUPER/1660 Ti/1650 desktop, GTX 1060 6GB and GTX 1080
  desktop; AMD Radeon RX 6000/7000/9000 desktop), compiled by hand
  from NVIDIA GeForce and AMD product pages (CC0 compilation; each
  entry records its source URL and retrieval date). The GPU page
  shows the rated boost clock, memory and board power under their
  own labels ("Rated boost clock", "Rated memory", "Rated board
  power") with the dataset version as their source; they never
  replace the measured dedicated memory or the live GPU clock, and
  a model that is not listed gets no rated specs. For laptop GPUs
  the board power is the maximum of the manufacturer-published TGP
  range, and the range is recorded in the dataset. A figure with no
  single value for a shared device name is left out rather than
  guessed: the GTX 1650 carries no boost figure (G5/G6 variants),
  and the RTX 3060 and RTX 5060 Ti carry no memory figure. The
  Settings card now shows the GPU count next to the CPU count.
  Deliberately absent in v1.1.0: RTX 4060 Ti and RTX 3080 desktop
  (board power split between variants with no mapping on the NVIDIA
  page), RTX 3050 desktop (boost, memory and power all differ
  between its 8 GB and 6 GB variants under one device name), GTX
  1060 3GB (no power figure on nvidia.com), GTX 1070 and GTX 1080
  Ti (no boost or power figure on nvidia.com), RTX 20-series and
  GTX 16-series laptop GPUs (NVIDIA publishes no laptop spec tables
  for them), all AMD laptop GPUs (AMD publishes Game Frequency, not
  Boost Frequency, for them), Intel Arc A580/A750/A770 (no
  manufacturer spec page locatable). Tests: `HardwareDatabaseTests`
  (GPU normalizer, lookup, fail-safe loading, invalid-entry
  skipping, bundled dataset pinned at 53 GPU entries and the
  verified RTX 4060 Laptop / RX 7900 XTX figures, null-figure
  entries) and the new `GpuCollectorTests` (rated specs present
  with their own labels, values and sources, after the measured
  dedicated memory; absent for unknown models and with no database;
  null boost and null memory cases).
- **Dependency audit re-attested for v2.1.15.** `SECURITY.md` said
  v2.1.14; the audit was re-run with
  `dotnet list package --vulnerable --include-transitive` on all four
  projects (no vulnerable packages, including transitive). No package
  version changed between v2.1.14 and v2.1.15 (only the `<Version>`
  bump and a test-file exclusion, zero `PackageReference` changes in
  `git diff 9dab61a HEAD -- '*csproj'`), so the result carries
  forward.

## v2.1.16 — shipped

- **Localisation, phase 8: the Comparison and Fleet pages in German
  (Beyond v2.0, item 4).** The final part of phase 8 translates the
  last two untranslated pages' chrome, closing the item: the
  catalogue grows by 86 keys (421 to 507) — 35 Comparison, 51 Fleet.
  The Comparison page moves its eyebrow, title, intro, Before/After
  labels and picker placeholders, the Compare button, the Result
  heading, the DNA note, the no-changes line, the Added/Removed
  headings and the footer note, plus the transient status lines
  (loading, no snapshots, one snapshot, the count line, the compared
  line, the failure lines) and the DNA state/hash lines. The DNA
  heading and the unidentified-parts heading reuse the Report page's
  keys instead of second copies. The Fleet page moves its eyebrow,
  title, intro, the Import button and note, the Machines heading,
  the two export buttons, the table note, the eight column headers,
  the row Export button and the Remove/Confirm-remove button, plus
  the transient status lines (loading, no scans, the summary, the
  export-all / remove / import lines). The summary and the
  export-all / remove / import lines are templates with one/many
  keys instead of inline plural hacks, and the remove-arm line
  quotes the translated Confirm-remove label.
- **Design (the phase 6 pattern).** An optional `languageCode`
  (default null, English when blank or unknown) at the END of the
  `ComparisonViewModel` and `FleetViewModel` constructors (and the
  `FleetRowViewModel` constructor, which the Fleet page passes its
  own code into), so every existing caller keeps compiling and the
  no-language call produces the exact English wording it always did
  (pinned by the existing tests). Both view models store a status
  kind plus the values its line needs (the compared dates, the
  export-all result, the import lists) and re-resolve the line on a
  switch, the same in-place pattern the History page uses; the
  Comparison page additionally stores the DNA state and the two hash
  displays. The Fleet remove/import guards that used to read the
  English status line (`StartsWith("Fleet could not be read")`)
  now read the stored status kind, so they still work in German.
  `MainViewModel` passes `_settings.LanguageCode` at construction
  and calls `ApplyLanguage` on the cached pages in `ApplySettings`.
  Deliberately NOT translated in this version, and named on the
  pages' own terms in the docs: the U9 per-dollar verdict sentence
  stays English in every language (shown verbatim, never translated,
  pending Jawad's veto decision); the row cell values stay English,
  including the "Not measurable" / "Not reported" fallbacks, so the
  CSV export still agrees with the table row for row; the import
  skipped-file reason fragments ("could not be imported.",
  "could not be read (...)") stay English like exception messages.
  Tests: `GermanPack_ComparisonPageTerms` /
  `GermanPack_FleetPageTerms` (exact-equality pins for all 86 keys,
  plus template formatting) and
  `ComparisonFleetLocalizationViewModelTests` (English default /
  German seeded / switch-both-ways / unknown-language fallback /
  data-stays-English for both pages, plus the identical-DNA state
  in German, the remove-arm and confirmed-remove lines in both
  languages, the export-all some/zero lines in both languages, the
  empty states, and the import skipped-file line). The LanguageNote
  now says all pages are translated; the two tests that pinned the
  old note are updated.
- **Dependency audit re-attested for v2.1.16.** `SECURITY.md` said
  v2.1.15; the audit was re-run with
  `dotnet list package --vulnerable --include-transitive` on all four
  projects (no vulnerable packages, including transitive). No package
  version changed between v2.1.15 and v2.1.16 (only the `<Version>`
  bump and test-file exclusions — zero `PackageReference` changes in
  `git diff 0d71547 HEAD -- '*csproj'`), so the result carries
  forward.

## v2.1.17 — shipped

- **Maintenance: the seven noted major dependency upgrades.** The
  majors that the v2.1.14/15/16 cycles noted and deliberately did not
  fold in land here, one commit per package so a bad one reverts
  cleanly: Microsoft.Data.Sqlite 10.0.0 to 11.0.0 and
  SQLitePCLRaw.bundle_e_sqlite3 2.1.14 to 3.0.5 (coupled — Sqlite 11
  refuses to restore against bundle 2.x with NU1605, so they land
  together), System.Diagnostics.EventLog and System.Management
  10.0.14 to 11.0.0 (Windows-only project: restore clean locally,
  build and tests verified by CI), coverlet.collector 6.0.4 to
  10.1.0, Microsoft.NET.Test.Sdk 17.14.1 to 18.12.0, and
  xunit.runner.visualstudio 3.1.4 to 4.0.1 (its nuspec states it runs
  v1, v2 and v3 tests, so the xunit 2.9.3 suite is covered). No API
  breakage needed adapting in any project. After the upgrades
  `dotnet list package --outdated` reports zero outdated packages on
  all four projects, and the vulnerability audit reports no vulnerable
  packages, including transitive.
- **Coverage-report finding: not a bug.** The "Report coverage"
  warning the project carried as a known issue since v2.1.5 does not
  actually fire: on the v2.1.16 main run the step printed real line
  rates (Ubuntu 0.9189, Windows 0.7521), the `::warning::` strings in
  the log are the runner echoing the step's script text, and the
  run's annotations carry no coverage warning. No workflow change was
  needed for coverage. The only real annotation on recent runs is the
  Node.js 20 deprecation for the pinned v4 actions; re-pinning
  checkout and setup-dotnet to their v5 SHAs clears it, prepared as a
  diff for the parent to apply via GitHub web (workflow files are
  parent-only, and the token has no workflow scope).

## v2.1.36 — shipped

- **Hardware database: Intel Arc A-series desktop GPUs (Beyond v2.0, item 5).** The dataset grows from v1.3.0 to v1.4.0 with the four Intel Arc A-series desktop GPUs (A770, A750, A580, A380), the last Arc family the dataset was missing. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, `dotnet list package --outdated` reports zero outdated packages on all four projects, every roadmap item through v2.1.35 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. The dataset used to say no manufacturer spec page could be located for the Arc A580/A750/A770; that turned out to be wrong, and the note is removed with this slice. Every figure was compiled by hand from an Intel page (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-14: the A770 from Intel's ARK page for the 16 GB variant (Graphics Clock 2100 MHz, Total Board Power 225 W; the 8 GB variant shares the same clock and power, only the memory differs, which is why no memory figure is recorded); the A750 from Intel's ARK page (Graphics Clock 2050 MHz, 8 GB, Total Board Power 225 W, confirmed against Intel's own A750 datasheet); the A580 from Intel's own launch announcement (Graphics Clock 1700 MHz, 8 GB, Total Board Power 185 W from Intel's published launch spec table, corroborated by the launch reviews; a few secondary databases list 175 W with no Intel document behind them, so the Intel-published figure is recorded); the A380 from Intel's own A-series spec reveal plus Intel's A380 product brief (Graphics Clock 2000 MHz, 6 GB, Total Board Power 75 W; the brief's 2300 MHz / 87 W figures are the Deep Link Dynamic Power Share conditional, not the standard rating). Intel publishes a single Graphics Clock for Arc cards, recorded as the boost figure, and the dataset definition already says so. A figure with no single value for a shared device name is left out rather than guessed: the A770 carries no memory figure (Intel lists 16 GB and 8 GB configurations under one device name), the same precedent as the RTX 5070 Laptop GPU and the RX 9060 XT. The GPU page shows the rated boost clock, memory and board power under their own labels ("Rated boost clock", "Rated memory", "Rated board power") with the dataset version as their source; they never replace the measured dedicated memory or the live GPU clock, and a model that is not listed gets no rated specs. No code change was needed: the GPU normalizer already maps "Intel(R) Arc(TM) A770/A750/A580/A380 Graphics" to "ARC A770/A750/A580/A380" (it strips the Intel prefix and keeps ARC), null memory is already handled end to end, and the Settings card shows the GPU count from the dataset, so it reads 68 with no code change. Deliberately absent in v1.4.0 (unchanged from v1.3.0 except the Arc A-series, now shipped): RTX 4060 Ti and RTX 3080 desktop, RTX 3050 desktop, GTX 1060 3GB, GTX 1070, GTX 1080 Ti, RTX 20-series and GTX 16-series laptop GPUs, all AMD laptop GPUs. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.4.0, 74 CPUs and 68 GPUs, and the verified A770/A750/A580/A380 figures including the A770 null-memory case, the A750 no longer in the deliberately-absent assertions, plus the A770/A580/A380 normalizer cases).
- **Dependency audit re-attested for v2.1.36:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.35 and v2.1.36; `dotnet list package --outdated` reports zero outdated packages on all four projects.

## v2.1.35 — shipped

- **Hardware database: Intel Arc B-series, RX 9060 / 9060 XT and RTX 5050 desktop GPUs (Beyond v2.0, item 5).** The dataset grows from v1.2.0 to v1.3.0 with five desktop GPUs launched after v1.1.0: Intel Arc B580 and B570, AMD Radeon RX 9060 and RX 9060 XT, and the GeForce RTX 5050. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, `dotnet list package --outdated` reports zero outdated packages on all four projects, every roadmap item through v2.1.34 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. Every figure was compiled by hand from a manufacturer page (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-14: the RTX 5050 from NVIDIA's 50-series page (Boost Clock 2.57 GHz, 8 GB, Total Graphics Power 130 W); the RX 9060 from AMD's driver/spec page (Boost Frequency up to 2.99 GHz, 8 GB, Typical Board Power 132 W); the RX 9060 XT from AMD's driver/spec page plus AMD's own how-to-sell sheet (Boost Frequency up to 3.13 GHz, the same for both memory variants); the Arc B580 and B570 from Intel's own published B-Series Graphics Quick Reference Guide (Graphics Clock 2670 MHz / 2500 MHz, 12 GB / 10 GB, Total Board Power 190 W / 150 W) — intel.com returned 403 to the reader, so the guide PDF is the cited Intel source, corroborated by the launch reviews. Intel publishes a single Graphics Clock for Arc cards, recorded as the boost figure, and the dataset definition now says so. A figure with no single value for a shared device name is left out rather than guessed: the RX 9060 XT carries no memory or board-power figure (AMD lists 8 GB / 150 W and 16 GB / 160 W configurations under one device name), the same precedent as the RTX 5070 Laptop GPU. The GPU page shows the rated boost clock, memory and board power under their own labels ("Rated boost clock", "Rated memory", "Rated board power") with the dataset version as their source; they never replace the measured dedicated memory or the live GPU clock, and a model that is not listed gets no rated specs. **The board-power figure is now optional end to end, not just in the data:** the RX 9060 XT is the first entry with no board-power figure, and the collector used to render that spec unconditionally (`(double)rated.TdpW`), which would have thrown on this card. `GpuSpec.TdpW` is now `int?`, the loader accepts a missing or null `tdp_w` (a present value must still be a positive whole number of watts, and a zero is still a data bug), and the collector renders the spec only when a figure exists. The Settings card shows the GPU count from the dataset, so it reads 64 with no code change. Deliberately absent in v1.3.0 (unchanged from v1.2.0): RTX 4060 Ti and RTX 3080 desktop, RTX 3050 desktop, GTX 1060 3GB, GTX 1070, GTX 1080 Ti, RTX 20-series and GTX 16-series laptop GPUs, all AMD laptop GPUs, Intel Arc A580/A750/A770. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.3.0, 74 CPUs and 64 GPUs, and the verified B580/B570/9060/9060 XT/5050 figures including the 9060 XT null-memory/null-power case, the invalid-entry test extended so a null board power loads like a null boost, plus the B580 normalizer case) and `GpuCollectorTests` (a model with no memory and no board power shows its rated boost clock and nothing else, and the collector does not crash).
- **Dependency audit re-attested for v2.1.35:** no vulnerable packages in any project, including transitive dependencies; `dotnet list package --outdated` reports zero outdated packages on all four projects, so there are no major bumps to note this cycle.

## v2.1.34 — shipped

- **Hardware database: RTX 50-series laptop GPUs (Beyond v2.0, item 5).** The dataset grows from v1.1.0 to v1.2.0 with the six GeForce RTX 50-series laptop GPUs (5090, 5080, 5070 Ti, 5070, 5060, 5050 Laptop), compiled by hand from NVIDIA's laptop compare page (nvidia.com/en-us/geforce/laptops/compare/, read 2026-11-14; CC0 compilation; each entry records its source URL and retrieval date). The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, `dotnet list package --outdated` reports zero outdated packages on all four projects, every roadmap item through v2.1.33 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). The GPU page shows the rated boost clock, memory and board power under their own labels ("Rated boost clock", "Rated memory", "Rated board power") with the dataset version as their source; they never replace the measured dedicated memory or the live GPU clock, and a model that is not listed gets no rated specs. Boost is the upper end of NVIDIA's published Boost Clock range and board power is the maximum of the manufacturer-published TGP range, and the range is recorded in the dataset, exactly like the RTX 40/30-series laptop entries. A figure with no single value for a shared device name is left out rather than guessed: the RTX 5070 Laptop GPU carries no memory figure (NVIDIA lists 12 GB and 8 GB configurations under one device name), the same precedent as the RTX 3060 and RTX 5060 Ti. No separate 12 GB entry was added: there is no evidence Windows reports a distinct device name for it, and inventing one would be a guess. Deliberately absent in v1.2.0 (unchanged from v1.1.0): RTX 4060 Ti and RTX 3080 desktop, RTX 3050 desktop, GTX 1060 3GB, GTX 1070, GTX 1080 Ti, RTX 20-series and GTX 16-series laptop GPUs, all AMD laptop GPUs, Intel Arc A580/A750/A770. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.2.0, 74 CPUs and 59 GPUs, and the verified RTX 5090/5080/5070 Ti/5070/5060/5050 Laptop figures including the 5070 null-memory case, plus the 5090 Laptop normalizer case) and the existing `GpuCollectorTests` (rated specs under their own labels, null-memory case). The Settings card shows the GPU count from the dataset, so it reads 59 with no code change.
- **Dependency audit re-attested for v2.1.34:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.33 and v2.1.34; `dotnet list package --outdated` reports zero outdated packages on all four projects.

## v2.1.33 — shipped

- **Localisation, phase 7: the Comparison, Crash History, Report, and Tests pages in Spanish (Beyond v2.0, item 4):** the same four surfaces German phase 8 (v2.1.16) and French phase 7 (v2.1.26) translated, now pinned in Spanish — the Comparison page's whole chrome including the compared-pair status template, the identical-DNA lines and the empty states; the Crash History page's whole chrome including the alert card, the table headers, the cleared-log warning and the empty states; the Report page's whole chrome including the export buttons, the DNA card and the format descriptions; and the Tests page's whole chrome including the load-test card and the per-test status words, with the failure line re-resolving from the stored exception message on a switch instead of resetting. All re-render in place on a language switch, with no restart, and transient status lines re-resolve from stored state instead of resetting. The feature already worked in Spanish — the view models take a language code generically and the Spanish pack already covers the whole 523-key catalogue — but there were no Spanish-pinned tests; this version adds them, mirroring the French phase 7 tests, plus Spanish pack tests pinning all 35 Comparison, 15 Crash History, 22 Report and 42 Tests strings and Spanish template formatting with invariant numbers. No new keys and no source change was needed, except the Spanish language card note, which now says every page is translated. The data stays English by design: machine names, the comparison summary sentence, the row cell values, the DNA changed/unidentified part lines, the crash component headline, the "What this means" fallback, the crash class names, counts, dates and plain meanings, the per-dollar verdict sentence (shown verbatim in every language), the verdict text, the score explanation, the buyer banner and summary, the generated-text line, the DNA state/hash/verify result texts, the export folder path, the per-test names and workload descriptions, the measured result summaries and details, and the load-test measured results, verdict text and live values, file and folder paths, and exception messages. With this phase every page is translated in Spanish. The Spanish wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German and French wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.33:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.32 and v2.1.33.

## v2.1.32 — shipped

- **Localisation, phase 6: the History, Fleet, and Buyer Check pages in Spanish (Beyond v2.0, item 4):** the same sixth surface German phase 6 and French phase 6 (v2.1.25) translated, now pinned in Spanish. Three Spanish-seeded view models prove the three pages' chrome in place and with no restart: the History trend summary, the forecast texts, the driver-age summary, the value no-verdict reasons and the asking-price row label, the sensor-log statuses, and the remove-scan messages; the Fleet summary, the row remove/export states, the import-skipped status and the empty states; and the Buyer Check approved banner, the finding banner and the skipped-checks note. A page already open when the language changes re-analyses its stored snapshots in place, so the wordings follow the language while the measured values stay identical. The feature already worked in Spanish, the view models take a language code generically and the Spanish pack already covers the whole 523-key catalogue, but there were no Spanish-pinned tests; this version adds them, mirroring the French phase 6 tests (11 History, 8 Fleet, 4 Buyer Check view-model tests), plus three Spanish pack tests pinning all 185 History, Fleet, and Buyer Check strings. All values were read from the real Spanish pack with a scratch dump program (outside the repo), never hand-translated from the German or French tests. No new keys and no source change was needed, except the Spanish language card note, which now says the navigation rail, the Settings page, the component detail pages, the Overview page, and the History, Fleet, and Buyer Check pages are translated. The data stays English by design: component names, headlines, health and status texts, spec labels, values and sources, the "What this means" text, and the per-dollar verdict sentence are measured data from the collectors, translated in a later slice. The Spanish wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German and French wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.32:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.31 and v2.1.32.

## v2.1.31 — shipped

- **Localisation, phase 5: the Overview page in Spanish (Beyond v2.0, item 4):** the same fifth surface German phase 5 (v2.1.11) and French phase 5 (v2.1.24) translated, now pinned in Spanish. One Spanish-seeded view model proves the whole landing page's chrome in place and with no restart: hero labels, section headings, the two buttons, the score note, the key-stat labels, the live-strip names, and the sensor-log and alert-setting sentences. The live strip is updated by stable key, never by display name, so a switch cannot strand a metric; a metric built with the legacy (name, unit) constructor keeps its old behaviour exactly. The feature already worked in Spanish, the view model takes a language code generically and the Spanish pack already covers the whole 523-key catalogue, but there were no Spanish-pinned tests; this version adds them, mirroring the French phase 5 tests, plus a Spanish pack test pinning all 36 Overview-page strings and Spanish template formatting with invariant numbers. All values were read from the real Spanish pack with a scratch dump program (outside the repo), never hand-translated from the German or French tests. No new keys and no source change was needed, except the Spanish language card note, which now says the navigation rail, the Settings page, the component detail pages and the Overview page are translated. The data stays English by design: component names, headlines, health and status texts, spec labels, values and sources, the score explanation and the verdict text are measured data from the collectors, translated in a later slice. The Spanish wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German and French wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.31:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.30 and v2.1.31.

## v2.1.30 — shipped

- **Localisation, phase 4: the component detail pages in Spanish (Beyond v2.0, item 4):** the same fourth surface German phase 4 (v2.1.10) and French phase 4 (v2.1.23) translated, now pinned in Spanish. One Spanish-seeded view model proves all 15 pages' chrome, the in-place re-render on a language switch and back, the missing-component fallbacks, the no-match search message naming the search, the estimate suffix, the age-signals chrome and skipped-checks note, and the untrusted-signal reliability text. The feature already worked in Spanish, the view model takes a language code generically and the Spanish pack already covers the whole 523-key catalogue, but there were no Spanish-pinned tests; this version adds them, mirroring the French phase 4 tests, plus a Spanish pack test pinning all 14 detail-page strings. All values were read from the real Spanish pack with a scratch dump program (outside the repo), never hand-translated from the German or French tests. No new keys and no source change was needed, except the Spanish language card note, which now says the navigation rail, the Settings page and the component detail pages are translated. The data stays English by design: component names, headlines, health and status texts, spec labels, values and sources, and the "What this means" text are measured data from the collectors, translated in a later slice. The Spanish wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German and French wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.30:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.29 and v2.1.30.

## v2.1.29 — shipped

- **Localisation, phase 3: the Core diagnostic strings in Spanish (Beyond v2.0, item 4):** the same third surface German phase 3 (v2.1.9) and French phase 3 (v2.1.22) translated, now pinned in Spanish at the Core level — the plug-in list's per-item status and reason, the manifest parser's errors, and the portable-toggle result messages, including the `with { LanguageCode = "es" }` re-resolution of a toggle result. The feature already worked in Spanish — v2.1.28's Settings-level tests proved the plug-in list and toggle message render Spanish — but the Core APIs themselves had no Spanish-pinned tests; this version adds them, mirroring the French phase 3 tests, plus a Spanish pack test pinning the same 20 Core diagnostic strings and Spanish template formatting with invariant numbers. No new keys — the Spanish pack already covers the whole 523-key catalogue — and no source change was needed: the Core APIs take a language code generically. File names, manifest field names, collector ids and TRUESPEC_PORTABLE stay untranslated inside the sentences. Detail pages stay English in Spanish in this version — the Spanish language card note stays accurate. The Spanish wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German and French wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.29:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.28 and v2.1.29.

## v2.1.28 — shipped

- **Localisation, phase 2: the Settings page in Spanish (Beyond v2.0, item 4):** picking Spanish now translates the whole Settings page, the same second surface German phase 2 (v2.1.8) and French phase 2 (v2.1.21) translated — header, Appearance, refresh rate (options rebuild per language, "5 segundos", still saving the number 5), sensor logging and alerts (the alert summary keeps the app's number formatting inside the Spanish sentence, "Avisar cuando la temperatura de la CPU llegue a 90 °C durante 30 segundos."), hardware database, export folder, data location (all five portable summaries, both toggle actions), plug-ins card chrome and count lines, and Privacy — right away, with no restart, and back to English the same way. The per-item plug-in status/reason and the portable-toggle result message render Spanish too: they are Core diagnostic strings that take a language code, the same mechanism German phase 3 (v2.1.9) added. No new keys — the Spanish pack already covers the whole 523-key catalogue. One wording correction from the phase-1 catalogue: the alert threshold label is "Umbral de alerta (°C)", the term Windows itself uses, not "Avisar por encima de". The other pages stay English in Spanish in this version — the Spanish language card now says the navigation rail and the Settings page are translated, and the other pages follow in later versions. The Spanish wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German and French wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.28:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.27 and v2.1.28.

## v2.1.27 — shipped

- **Localisation, phase 1: the navigation rail in Spanish (Beyond v2.0, item 4):** TrueSpec now ships a fourth language. Settings has a Language picker (English / Deutsch / Français / Español); picking Spanish translates the whole navigation rail (all 22 items, using the standard terms Windows itself uses in Spanish: Almacenamiento, Pantalla, Inicio, Configuración) and the language card itself, right away, with no restart. The choice is saved in settings and survives a restart. Detail pages stay English in Spanish in this version — the Spanish language card says so honestly — and follow in later versions. The foundation is in place for them: the Spanish pack covers the whole 523-key catalogue (the coverage test pins it: a pack that misses a key fails), so later phases only translate surfaces, never add keys. The Spanish wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German and French wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.27:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.26 and v2.1.27.

## v2.1.26 — shipped

- **Localisation, phase 7: the Comparison, Crash History, Report, and Tests pages in French (Beyond v2.0, item 4):** the same four surfaces German phase 8 (v2.1.16) translated, now pinned in French — the Comparison page's whole chrome including the compared-pair status template, the identical-DNA lines and the empty states; the Crash History page's whole chrome including the alert card, the table headers, the cleared-log warning and the empty states; the Report page's whole chrome including the export buttons, the DNA card and the format descriptions; and the Tests page's whole chrome including the load-test card and the per-test status words, with the failure line re-resolving from the stored exception message on a switch instead of resetting. All re-render in place on a language switch, with no restart, and transient status lines re-resolve from stored state instead of resetting. The feature already worked in French — the view models take a language code generically and the French pack already covers the whole 523-key catalogue — but there were no French-pinned tests; this version adds them, mirroring the German phase 8 tests, plus French pack tests pinning all 35 Comparison, 15 Crash History, 22 Report and 42 Tests strings and French template formatting with invariant numbers. No new keys and no source change was needed, except the French language card note, which now says every page is translated. The data stays English by design: machine names, the comparison summary sentence, the row cell values, the DNA changed/unidentified part lines, the crash component headline, the "What this means" fallback, the crash class names, counts, dates and plain meanings, the per-dollar verdict sentence (shown verbatim in every language), the verdict text, the score explanation, the buyer banner and summary, the generated-text line, the DNA state/hash/verify result texts, the export folder path, the per-test names and workload descriptions, the measured result summaries and details, and the load-test measured results, verdict text and live values, file and folder paths, and exception messages. With this phase every page is translated in French. The French wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.26:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.25 and v2.1.26.

## v2.1.25 — shipped

- **Localisation, phase 6: the History, Fleet, and Buyer Check pages in French (Beyond v2.0, item 4):** the same sixth surface German phase 6 (v2.1.12) translated, plus the Fleet and New Laptop Check pages (the same two surfaces German phase 8 translated), now pinned in French — the History page's whole chrome (trend cards, saved-scans table, two-machine comparison, value comparison, CPU temperature log) and its Core-owned wordings (trend summary, forecast texts, no-verdict reason, verified against the real Core code on the exact fixtures), the Fleet page's whole chrome including the remove arm, export-all totals and skip reasons, empty states and import skipped-file line, and the New Laptop Check page's whole chrome including the finding banner, which keeps the finding title English with only the state word translating. All re-render in place on a language switch, with no restart, and transient status lines re-resolve from stored state instead of resetting. The feature already worked in French — the view models and the Core APIs take a language code generically and the French pack already covers the whole 523-key catalogue — but there were no French-pinned tests; this version adds them, mirroring the German phase 6 and phase 8 tests, plus French pack tests pinning all 96 History, 72 Fleet and 33 New Laptop Check strings and French template formatting with invariant numbers. No new keys and no source change was needed. The data stays English by design: machine names, the per-dollar verdict sentence (shown verbatim in every language), the table's "Not measurable" / "Not reported" cell fallbacks, check titles, details and what-to-ask lines, authenticity evidence lines and source strings, file and folder paths, and exception messages. Comparison, Crash History, Report and Tests stay English in French in this version. The French language card now says the navigation rail, the Settings page, the component detail pages, the Overview page, the History page, the Fleet page and the New Laptop Check page are translated, and the other pages follow in later versions. The French wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.25:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.24 and v2.1.25.

## v2.1.24 — shipped

- **Localisation, phase 5: the Overview page in French (Beyond v2.0, item 4):** the same fifth surface German phase 5 (v2.1.11) translated, now pinned in French — the landing page, the first screen a user sees, renders its whole chrome in French, in place on language switch and back to English the same way, with no restart. The live strip is updated by stable key, never by display name, so a switch cannot strand a metric. The feature already worked in French — the view model takes a language code generically and the French pack already covers the whole 523-key catalogue — but there were no French-pinned tests; this version adds them, mirroring the German phase 5 tests (OverviewViewModelTests, LiveMetricViewModelTests), plus a French pack test pinning all 36 Overview-page strings and French template formatting with invariant numbers. No new keys and no source change was needed. The data stays English by design: component names, headlines, health and status texts, spec labels, values and sources, and the score explanation and verdict text are measured data, translated in a later slice. The French language card now says the navigation rail, the Settings page, the component detail pages and the Overview page are translated, and the other pages follow in later versions. The French wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.24:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.23 and v2.1.24.

## v2.1.23 — shipped

- **Localisation, phase 4: the component detail pages in French (Beyond v2.0, item 4):** the same fourth surface German phase 4 (v2.1.10) translated, now pinned in French — the shared detail view all 15 component pages use renders its chrome in French, in place on language switch and back to English the same way, with no restart. The feature already worked in French — the view model takes a language code generically and the French pack already covers the whole 523-key catalogue — but there were no French-pinned tests; this version adds them, mirroring the German phase 4 tests (ComponentDetailViewModelTests), plus a French pack test pinning all 14 detail-page strings. No new keys and no source change was needed. The data stays English by design: component names, headlines, health and status texts, spec labels, values and sources, and the "What this means" text are measured data, translated in a later slice. The French language card now says the navigation rail, the Settings page and the component detail pages are translated, and the other pages follow in later versions. The French wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.23:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.22 and v2.1.23.

## v2.1.22 — shipped

- **Localisation, phase 3: the Core diagnostic strings in French (Beyond v2.0, item 4):** the same third surface German phase 3 (v2.1.9) translated, now pinned in French at the Core level — the plug-in list's per-item status and reason, the manifest parser's errors, and the portable-toggle result messages, including the `with { LanguageCode = "fr" }` re-resolution of a toggle result. The feature already worked in French — v2.1.21's Settings-level tests proved the plug-in list and toggle message render French — but the Core APIs themselves had no French-pinned tests; this version adds them, mirroring the German phase 3 tests (PluginDiscoveryTests, PluginManifestTests, PortableModeTests), plus a French pack test pinning the same 20 Core diagnostic strings and French template formatting with invariant numbers. No new keys — the French pack already covers the whole 523-key catalogue — and no source change was needed: the Core APIs take a language code generically. File names, manifest field names, collector ids and TRUESPEC_PORTABLE stay untranslated inside the sentences. Detail pages stay English in French in this version — the French language card note stays accurate. The French wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.22:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.21 and v2.1.22.

## v2.1.21 — shipped

- **Localisation, phase 2: the Settings page in French (Beyond v2.0, item 4):** picking French now translates the whole Settings page, the same second surface German phase 2 (v2.1.8) translated — header, Appearance, refresh rate (options rebuild per language, "5 secondes", still saving the number 5), sensor logging and alerts (the alert summary keeps the app's number formatting inside the French sentence), hardware database, export folder, data location (all five portable summaries, both toggle actions), plug-ins card chrome and count lines, and Privacy — right away, with no restart, and back to English the same way. The per-item plug-in status/reason and the portable-toggle result message render French too: they are Core diagnostic strings that take a language code, the same mechanism German phase 3 (v2.1.9) added. No new keys — the French pack already covers the whole 523-key catalogue (the coverage test pins it). One wording correction from the phase-1 catalogue: the alert threshold label is "Seuil d'alerte (°C)", the term Windows itself uses, not "Alerte au-dessus de". Detail pages stay English in French in this version — the French language card now says the navigation rail and the Settings page are translated, and the other pages follow in later versions. The French wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language. Tests: French-seeded whole-page render, pick-French-and-back re-render, French alert summary with numbers, French plug-ins (status/reason), language-switch plug-in re-discovery in French, and language-switch portable-toggle message re-resolution in French.
- **Dependency audit re-attested for v2.1.21:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.20 and v2.1.21.

## v2.1.20 — shipped

- **Localisation, phase 1: the navigation rail in French (Beyond v2.0, item 4):** TrueSpec now ships a third language. Settings has a Language picker (English / Deutsch / Français); picking French translates the whole navigation rail (all 22 items, using the standard terms Windows itself uses in French: Stockage, Affichage, Démarrage, Paramètres) and the language card itself, right away, with no restart. The choice is saved in settings and survives a restart. Detail pages stay English in French in this version — the French language card says so honestly — and follow in later versions. The foundation is in place for them: the French pack covers the whole 523-key catalogue (the coverage test pins it: a pack that misses a key fails), so later phases only translate surfaces, never add keys. The French wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.20:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.19 and v2.1.20.
- **Verification note (H1/H2):** the Nov 12 review's two High defects were verified against main before this slice was picked, and both are already fixed — H1 (load-test clock average dragged ~19% low by the Bus Speed sensor) and H2 (DriversCollector silently truncating at the WMI 1000-row cap) were both fixed in commit 94288de, shipped in v1.5.0, with tests (Bus Speed exclusion in SensorSnapshotTests, end-to-end Bus Speed average in LoadTestRunnerTests, 3 truncation tests in DriversCollectorTests). Nothing was re-fixed here.

## v2.1.19 — shipped

- **Localisation polish: the Fleet export-all skip reasons in German.** The export-all path was the separate leftover from v2.1.18: it shares no code with import (the reasons are produced in `ExportOrchestrator`, rendered in `FleetViewModel`), and its skipped machines were still rendered from plain-English reason strings inside the German status line. A skipped machine now carries a structured `ExportAllSkipReasonKind` (no saved scan, no score, write failed) plus the write error when the write itself failed, and the Fleet page renders the reason in the page language from the kind. The machine name and the write error stay data, and a language switch re-renders the stored result instead of keeping the old language's reason. Catalogue 520 to 523 keys. Tests: German export-all with a no-score skip and a write-failed skip (German reasons, no English fragment, re-renders both ways on a language switch), reason-rendering tests in both languages, orchestrator kind assertions, and the zero-export German line updated to the translated reasons.
- **Dependency audit re-attested for v2.1.19:** no vulnerable packages in any project, including transitive dependencies; `dotnet list package --outdated` reports zero outdated packages on all four projects, so there are no major bumps to note this cycle.

## v2.1.18 — shipped

- **Localisation polish: the three honest limitations from the phase 8 report.** (1) Fleet import skip reasons: the two English fragments in the import status line ("could not be imported.", "could not be read (...)") were only the visible half — every real failure reason came from the importer's English sentence. The importer now returns a structured `SnapshotImportFailureKind` (too large, empty, wrong schema version, no scan, no machine name, missing part, score not a number, not a JSON report) alongside the unchanged English `Error` text, and the Fleet page renders the reason in the page language from the kind. The skipped entries are stored structured (file name, kind, schema version, exception message) so a language switch re-renders them instead of keeping the old language's reason. (2) New Laptop Check banner: the ViewModel's mirror of Core's `StrongestFinding` ranking is gone — Core now exposes `StrongestFailingFinding()` and its private ranking delegates to it, and the ViewModel calls it, so the banner can never name a different finding than Core's own banner. No Core logic or behaviour changed; the finding title stays English data inside the translated state word. (3) Load-test live lines: the measured branches ("Temperature: 72.3 C", "Clock: 4200 MHz") and the elapsed line ("Finished in 60 s.") are templates now, so a German user mid-test sees "Temperatur: 72.3 C", "Takt: 4200 MHz" and "Abgeschlossen in 60 s."; only the measured values stay data. Catalogue 507 to 520 keys. Tests: German Fleet import with a bad file (German reason, no English fragment, re-renders both ways on a language switch), importer kind assertions plus a reason-rendering test in both languages, banner ranking parity across all failing combos, and a real 5-second German run with a scripted monitor asserting the live lines and the elapsed line.
- **Dependency audit re-attested for v2.1.18:** no vulnerable packages in any project, including transitive dependencies; `dotnet list package --outdated` reports zero outdated packages on all four projects, so there are no major bumps to note this cycle.

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
