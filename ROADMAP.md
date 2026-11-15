# TrueSpec Roadmap

How TrueSpec grows without changing what it is: an offline, honest
PC system-info and buyer-check tool. Every number is measured with its
source named, or it says "Not reported by hardware". No network calls
at runtime, no elevation prompts, no invented values. A feature that
cannot meet those rules does not ship, however useful it sounds.

## v1.0.1, shipped in this release

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

## v1.1, shipped in this release

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
the same storage subsystem the Storage page already reports, in
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
  controls updates, and the UI says so, the "Update management" spec
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
  Worn 60-79%, Replace below 60%), no second scale is invented:
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

## v1.2, shipped

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

## v2.0.3, shipped

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

## v2.0.4, shipped

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

## v2.0.5, shipped

- **Honest limits: the MemTest86 direction.** The world-class plan
  (section 8) says MemTest86 is the one tool this plan deliberately does
  not replace, and the product should say that on the memory page rather
  than hide it, that sentence is a feature, the proof the rest of the
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
  a comment, and test-file exclusions, zero `PackageReference`
  changes), so the result carries forward.

## v2.0.6, shipped

- **Windows Memory Diagnostic result on the RAM page.** The "Memory
  test" row said "Not run" without ever checking the log, a hardcoded
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
  and a test-file exclusion, zero `PackageReference` changes), so the
  result carries forward.

## v2.0.7, shipped

- **Every WMI query pinned.** The pinning tests (`CollectorWmiSelectionTests`)
  covered System, Motherboard, CPU, Battery, GPU, RAM, Drivers, Machine
  DNA and Software, but Network, Display, Storage, Security, Crash and
  Updates still wrote their queries inline, the exact shape that let
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
  version changed between v2.0.5 and v2.0.6 (only the `<Version>` bump,
  zero `PackageReference` changes), so the result carries forward.

## v2.0.8, shipped

- **History timestamps: latest scan by parsed time, not ISO string
  order.** A scan's capture time is stored as ISO with an offset, and
  two such strings sort lexicographically out of order across a DST
  change: 01:30 at -04:00 (05:30 UTC) sorts after 01:15 at -05:00
  (06:15 UTC) as text, even though it is older. Two readers picked by
  string order and could get the wrong row: `GetLatestPerMachineAsync`
  (the Fleet page's latest scan per machine, its own comment admitted
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
  version changed between v2.0.6 and v2.0.7 (only the `<Version>` bump,
  zero `PackageReference` changes), so the result carries forward.

## v2.0.9, shipped

- **History sensor-log chart: time-proportional horizontal axis.** The
  chart plotted samples by index, so its line lied about time: two
  samples an hour apart (the app was closed, the machine slept) looked
  adjacent, and a burst a second apart looked stretched. The chart now
  positions samples by their timestamps, so the horizontal axis is time,
  not sample count. The line breaks at a stretch where logging was not
  running, more than 60 seconds between two consecutive measured
  samples, twice the maximum 30-second refresh interval (`AppSettings.
  MaxRefreshSeconds`), in addition to the existing break where the
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
  version changed between v2.0.7 and v2.0.8 (only the `<Version>` bump,
  zero `PackageReference` changes), so the result carries forward.

## v2.1.0, shipped

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
  version changed between v2.0.8 and v2.0.9 (only the `<Version>` bump,
  zero `PackageReference` changes), so the result carries forward.

## v2.1.1, shipped

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
  version changed between v2.0.9 and v2.1.0 (only the `<Version>` bump,
  zero `PackageReference` changes), so the result carries forward.

## v2.1.2, shipped

- **Technician mode: remove a machine from the fleet.** The fleet
  could import machines (v2.1.0) but never remove one: a wrong import,
  or a machine the shop no longer services, stayed in the fleet
  forever, because the store had no delete path at all. Every Fleet row
  now has a Remove button. Core: `ISnapshotStore.DeleteMachineAsync`
  (exact stored-name match, returns the deleted-row count; a prefix or
  a case variant is a different machine and is never touched). App:
  `FleetViewModel.RemoveMachineCommand` with a two-step confirm on the
  row, the app has no modal confirm dialog, so the first click arms
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

## v2.1.3, shipped

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
  confirm as the fleet, the first click arms the button to "Confirm
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

## v2.1.4, shipped

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
  unmissable export confirmation as the other exports, the banner
  names the folder and the count, and "Open folder" opens the folder
  the files landed in, with no banner when nothing was exported.
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

## v2.1.5, shipped

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

## v2.1.6, shipped

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
  Security is NotFeasible, no row reaches Full, the test pins that
  headline, so a future port claiming Full must update it
  deliberately, Get returns null for an unknown id, throws on null).
- **Dependency audit re-attested for v2.1.5.** `SECURITY.md` said
  v2.1.4; the audit was re-run with
  `dotnet list package --vulnerable --include-transitive` on all four
  projects (no vulnerable packages, including transitive). No package
  version changed between v2.1.4 and v2.1.5 (only the `<Version>` bump,
  zero `PackageReference` changes in
  `git diff 3d7a59e 526ed0a -- '*csproj'`), so the result carries
  forward.

## v2.1.7, shipped

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
  fallback rule never shows a blank, a missing translation falls
  back to English, an unknown key shows the key itself, and never
  throws. The localizer never changes the thread culture, so measured
  values keep the app's existing number and date formatting:
  localisation touches chrome only, never a measurement, a unit or a
  threshold. The translated surface is the navigation rail (all 22
  items, same keys in the same order, only the labels change) and the
  Settings language card itself, so picking a language switches the
  card too. Settings has a Language picker (English / Deutsch); the
  choice is saved in `AppSettings.LanguageCode` (sanitized to a
  supported code, unknown codes fall back to English) and applies
  right away, the main view model rebuilds the rail on settings
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
  version changed between v2.1.5 and v2.1.6 (only the `<Version>` bump,
  zero `PackageReference` changes in
  `git diff 526ed0a 859308c -- '*csproj'`), so the result carries
  forward.

## v2.1.8, shipped

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
  the number 5. The old `RefreshSecondsToTextConverter` is deleted,
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
  version changed between v2.1.6 and v2.1.7 (only the `<Version>` bump,
  zero `PackageReference` changes in
  `git diff 859308c 233fbf0 -- '*csproj'`), so the result carries
  forward.

## v2.1.9, shipped

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
  version changed between v2.1.7 and v2.1.8 (only the `<Version>` bump,
  zero `PackageReference` changes in
  `git diff 233fbf0 1188692 -- '*csproj'`), so the result carries
  forward.

## v2.1.10, shipped

- **Localisation, phase 4: the component detail pages in German
  (Beyond v2.0, item 4).** Phases 1-3 translated the rail, the
  Settings page and the Core diagnostic strings; this version
  translates the next complete surface, the shared detail view
  (`ComponentDetailView`), which every one of the 15 component pages
  uses, one surface, all detail pages at once. The catalogue grows
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
  this means" text are measured data produced by the collectors,
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
  version changed between v2.1.8 and v2.1.9 (only the `<Version>` bump,
  zero `PackageReference` changes in
  `git diff 1188692 a0b0326 -- '*csproj'`), so the result carries
  forward.

## v2.1.11, shipped

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
  the key the strip updates by never changes, a metric built with
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
  collectors and the scoring engine, translating them is a later
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
  bump and test-file exclusions, zero `PackageReference` changes in
  `git diff a0b0326 9925866 -- '*csproj'`), so the result carries
  forward.

## v2.1.12, shipped

- **Localisation, phase 6: the History page in German (Beyond
  v2.0, item 4).** Phases 1-5 translated the rail, the Settings page,
  the Core diagnostic strings, the component detail pages and the
  Overview page; this version translates the next complete surface,
  the History page (`HistoryViewModel` + `HistoryView` + its row and
  winner view models), the trend cards, the saved-scans table, the
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
  stays English, translating them is a later slice. The language
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
  bump and test-file exclusions, zero `PackageReference` changes in
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

## v2.1.13, shipped

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
  a switch), and re-runs the value comparison on a switch, but
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
  bump and test-file exclusions, zero `PackageReference` changes in
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
  bump and the embedded-resource entry for the dataset, zero
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

## v2.1.16, shipped

- **Localisation, phase 8: the Comparison and Fleet pages in German
  (Beyond v2.0, item 4).** The final part of phase 8 translates the
  last two untranslated pages' chrome, closing the item: the
  catalogue grows by 86 keys (421 to 507), 35 Comparison, 51 Fleet.
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
  bump and test-file exclusions, zero `PackageReference` changes in
  `git diff 0d71547 HEAD -- '*csproj'`), so the result carries
  forward.

## v2.1.17, shipped

- **Maintenance: the seven noted major dependency upgrades.** The
  majors that the v2.1.14/15/16 cycles noted and deliberately did not
  fold in land here, one commit per package so a bad one reverts
  cleanly: Microsoft.Data.Sqlite 10.0.0 to 11.0.0 and
  SQLitePCLRaw.bundle_e_sqlite3 2.1.14 to 3.0.5 (coupled, Sqlite 11
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

## v2.1.81, shipped

- **Hardware database: four AMD Ryzen 200 PRO mobile CPUs (Beyond v2.0, item 5).** The dataset grows from v1.44.0 to v1.45.0 and adds the PRO family the dataset never listed, 216 CPUs and 95 GPUs in total. The slice was picked by the brief, which named the four Ryzen 200 PRO mobile parts as the next candidate; all four verified directly against AMD's own sources, so the dependency-audit fallback was not needed. All figures were compiled by hand (each entry records its source URL and retrieval date): the Ryzen 7 PRO 250 at Base Clock 3.3 GHz and Max Boost Clock up to 5.1 GHz, 8 cores and 16 threads, 24 MB total cache (8 MB L2, 16 MB L3), Radeon 780M with 12 compute units at 2.7 GHz; the Ryzen 5 PRO 230 at 3.5 and up to 4.9 GHz, 6 cores and 12 threads, 22 MB total cache (6 MB L2, 16 MB L3), Radeon 760M with 8 compute units at 2.6 GHz; the Ryzen 5 PRO 220 at 3.2 and up to 4.9 GHz, 6 cores and 12 threads, 22 MB total cache (6 MB L2, 16 MB L3), Radeon 740M with 4 compute units at 2.8 GHz; the Ryzen 3 PRO 210 at 3.0 and up to 4.7 GHz, 4 cores and 8 threads, 12 MB total cache (4 MB L2, 8 MB L3), Radeon 740M with 4 compute units at 2.5 GHz. All four are Hawk Point Zen 4 on TSMC 4nm FinFET at 28 W default TDP, announced at CES 2025 on January 6, 2025 and launched March 27, 2025. The figures were verified against AMD's own product page per model, read 2026-11-15 from fresh fetches, and cross-checked against AMD's own January 2026 mobile-for-business Quick Reference Guide, which publishes the same boost and base figures for all four, and against the CES 2025 press-release table as reproduced by Tom's Hardware, which lists the same four parts with the same clocks, core counts and cache. The source URL recorded for each entry is its AMD product page. The two hybrid parts (the PRO 220 with two Zen 4 plus four Zen 4c cores, the PRO 210 with one Zen 4 plus three Zen 4c cores) use AMD's headline Base Clock and Max Boost Clock, not the per-core-type figures (the PRO 220 lists Max Zen4c Clock up to 3.5 GHz, the PRO 210 up to 3.3 GHz), the same rule as the consumer Ryzen 200 hybrid parts and the AI 400 chips' Max Zen5c Clock. This overturns the v2.1.59 exclusion, which kept the PRO variants out on the ground that the dataset covered consumer parts only and no PRO chip had been listed in any cycle, a ground later cycles superseded by listing the AI 400, AI 300 and AI Max PRO families. Two later PRO 200 additions stay out for a future cycle: the Ryzen 5 PRO 215 (6 cores, Base Clock 3.2 GHz and Max Boost Clock up to 4.7 GHz, AMD's own product page, launch March 27, 2025) and the Ryzen 5 PRO 216 (6 cores, 2.9 and up to 4.7 GHz, Lenovo PSREF for the ThinkPad T14 Gen 7 and T16 Gen 5); they are outside the four CES 2025 parts, and the brief named only those four. The Intel 13th-gen HX P-core base rows were not re-checked in this cycle: no new Intel document publishing per-SKU P-core base surfaced, and the brief said to re-check only if one did, so the v2.1.47 block stands. The dataset's base_ghz is AMD's headline Base Clock and boost_ghz is AMD's Max Boost Clock, per its own definition. The CPU page shows their \"Rated base clock\" and \"Rated boost clock\" under their own labels with the dataset version as their source; they never replace the measured Min and Max clocks, and a model that is not listed gets no rated spec. No code change was needed: the CPU normalizer already maps the four names, and the Settings card shows the CPU count from the dataset, so it reads 216 with no code change. The coverage text in the dataset is corrected in the same edit: it now reads \"216 CPUs, 95 GPUs in v1.45.0\", names the four Ryzen 200 PRO parts, and records the new sourcing plus the PRO 215 and PRO 216 exclusions. No live vendor spot-check limitation applies in this cycle: every figure rests on AMD's own product pages, read directly through the search index, plus AMD's own Quick Reference Guide. The logos and icons are unchanged by Jawad's decision. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.45.0, 216 CPUs and 95 GPUs, and the verified Ryzen 7 PRO 250, Ryzen 5 PRO 230, Ryzen 5 PRO 220 and Ryzen 3 PRO 210 figures, plus the normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.45.0 in German, French and Spanish). Next candidate after this cycle: the two later Ryzen 200 PRO additions, the Ryzen 5 PRO 215 and Ryzen 5 PRO 216, recorded in this cycle as staying out.

## v2.1.80, shipped

- **Hardware database: two more Intel Core 200H mobile CPUs with disabled graphics (Beyond v2.0, item 5).** The dataset grows from v1.43.0 to v1.44.0 and completes the Core 200H and 200U non-Ultra mobile family at nine parts, which the dataset never listed in full. The slice was picked by a fresh audit: the brief named the 13th-gen HX P-core base rows and the Core 7 230H first, and both were re-checked; the 13th-gen HX rows stay blocked on the v2.1.47 finding (Intel's own compare page for the i9-13900K, i9-13900T and i9-13980HX, fetched directly in this cycle, still shows the Performance-core Base Frequency row with an empty cell for the 13980HX, 3.00 GHz for the 13900K and 1.10 GHz for the 13900T, and the Efficient-core Base Frequency cell empty too, while Max Turbo 5.60 GHz and E-core Max Turbo 4.00 GHz are published, and the mirrors still agree on 2.2 GHz, but the vendor-page rule keeps the family out); the 230H verifies this time, and its sibling the Core 5 205H verifies with it, so the pair is the slice; and the dependency-audit fallback was not needed. The 230H was deliberately absent since v2.1.79 because no source found published its P-core base distinctly, notebookcheck giving only the 1.8 to 5.2 GHz min-max range from the E-core base to the P-core turbo, and it was not inferred from the 240H. That exclusion is overturned in this cycle: notebookcheck's per-SKU pages, fetched directly (230H page 1363555, 205H page 1364948), state the P-cluster range distinctly, the 230H P-cores at 2.5 to 5.2 GHz with E-cores at 1.8 to 4.0 GHz and the 205H P-cores at 2.2 to 4.8 GHz with E-cores at 1.6 to 3.6 GHz, and VideoCardz's read of Intel's own product pages confirms the 230H has the same CPU configuration, clocks, cache and power limits as the Core 7 240H and the 205H the same specifications as the Core 5 210H, with topcpu's spec listing for the 205H (8 cores, 12 threads, 12 MB cache, 45 W, boost up to 4.8 GHz, E-core boost 3.6 GHz) stated to follow specifications published by Intel. All figures were compiled by hand (each entry records its source URL and retrieval date): the Core 7 230H at Performance-core Base Frequency 2.5 GHz and Max Turbo Frequency up to 5.2 GHz with E-core Max Turbo Frequency 4.0 GHz, 10 cores (6 P-cores, 4 E-cores, 16 threads, 24 MB cache); the Core 5 205H at 2.2 and up to 4.8 GHz with E-core turbo 3.6 GHz, 8 cores (4 P-cores, 4 E-cores, 12 threads, 12 MB cache). Both launched in June 2026 (the 230H on June 12, the 205H on June 7), Raptor Lake-H Refresh on Intel 7 at 45 W. The one difference from the 240H and the 210H is the graphics: the 230H's Xe iGPU is disabled, and the 205H has no Xe graphics unit enabled or installed. The source URL recorded for each entry is its notebookcheck per-SKU page. One judgement call is recorded, the same disclosed mirror exception as the 251HX in v2.1.76, the 5700X3D in v2.1.78 and the 250U and 220U in v2.1.79: Intel's own ARK page for the 230H exists (SKU 246028, its title confirms 24M cache and up to 5.20 GHz), but the spec table values could not be extracted here, the fetched page text carries only boilerplate, and Intel's own compare pages return HTTP 403 in this sandbox, so no live compare-page spot-check was possible in this cycle and the P-core base figures rest on notebookcheck's per-SKU pages stating the P-cluster range distinctly, corroborated by the Intel-sourced same-clocks reports. Two more blocks were re-checked in this cycle and stay out. The Core 7 245HX non-Ultra: its figures now verify distinctly on mirrors (GadReview gives P-core base 2.9 GHz, E-core base 2.6 GHz, turbo 5.1 GHz; notebookcheck gives a slower P-core base than the Ultra 5 245HX's 3.1 GHz with a 2.6 to 5.1 GHz range; VideoCardz and TweakTown, from Intel ARK, give the same clocks as the Ultra 5 235HX), but it stays out because the number 245HX already names the Ultra 5 part and a second 245HX would invite the misidentification the dataset exists to prevent. The Core 3 100U stays out: it is a Series 1 part launched January 2024 and a rebrand of the i3-1315U, not part of the Core 200U family. The dataset's base_ghz is Intel's Performance-core Base Frequency, boost_ghz is Intel's Max Turbo Frequency, and e_core_boost_ghz is Intel's E-core Max Turbo Frequency, per its own definition. The CPU page shows their \"Rated base clock\" and \"Rated boost clock\" under their own labels with the dataset version as their source; they never replace the measured Min and Max clocks, and a model that is not listed gets no rated spec. No code change was needed: the CPU normalizer already maps the two names, and the Settings card shows the CPU count from the dataset, so it reads 212 with no code change. The coverage text in the dataset is corrected in the same edit: it now reads \"212 CPUs, 95 GPUs in v1.44.0\", names all nine Core 200H and 200U parts, and replaces the v2.1.79 stays-out note for the 230H with the new sourcing plus the 245HX and 100U exclusions. One honest limitation is recorded: Intel's own compare pages return HTTP 403 in this sandbox and the ARK spec table values could not be extracted here, so no live vendor spot-check was possible in this cycle; the figures rest on notebookcheck's per-SKU pages stating the P-cluster range distinctly, corroborated by VideoCardz's and topcpu's reads of Intel's own product pages. The logos and icons are unchanged by Jawad's decision. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.44.0, 212 CPUs and 95 GPUs, and the verified Core 7 230H and Core 5 205H figures, plus the normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.44.0 in German, French and Spanish). Next candidate after this cycle: the four AMD Ryzen 200 PRO mobile parts (Ryzen 7 PRO 250, Ryzen 5 PRO 230, Ryzen 5 PRO 220, Ryzen 3 PRO 210), whose figures AMD's own CES 2025 press-release table publishes; they are already recorded in the dataset coverage text as not listed, and are the next candidate, not yet added.

## v2.1.79, shipped

- **Hardware database: seven Intel Core 200H and 200U mobile CPUs (Beyond v2.0, item 5).** The dataset grows from v1.42.0 to v1.43.0 and adds the Raptor Lake-H/U Refresh non-Ultra mobile family, which the dataset never listed. The slice was picked by a fresh audit: the brief named the PRO-adjacent gaps first, and the audit found none to add (the Ryzen AI 400 family is already complete in the dataset, consumer and PRO, desktop and mobile, and its mobile H variants have no separate AMD product pages with their own clock figures); the Intel 13th-gen HX P-core Base Frequency rows stay out on the v2.1.47 finding, not re-checked in this cycle; the Intel Core 200H and 200U non-Ultra mobile families were checked and this time they verify, so the family is added; and the dependency-audit fallback was not needed. The five H parts were deliberately absent since the dataset began because no Intel document published their figures in any earlier cycle, only the CES 2025 keynote deck, whose table text is garbled, and pre-launch leaks. That exclusion is overturned in this cycle: Intel itself published the full Core 200H table on its own website in December 2024, and Notebookcheck reproduces that table verbatim (Core 9 270H, Core 7 250H, Core 7 240H, Core 5 220H, Core 5 210H, with P-core base, P-core boost, E-core base, E-core boost, TDP and cache per SKU), fetched directly in this cycle, and the 210H figures are independently confirmed by Intel's own product comparison page as read through the search index (8 cores, 12 threads, Max Turbo 4.8 GHz, P-core base 2.2 GHz, E-core base 1.6 GHz, 12 MB cache, 45 W, launch Q4'24), plus agreeing ARK mirrors per SKU (nanoreview, topcpu, siliconcat, cputronic). All figures were compiled by hand (each entry records its source URL and retrieval date): the Core 9 270H at Performance-core Base Frequency 2.7 GHz and Max Turbo Frequency up to 5.8 GHz with E-core Max Turbo Frequency 4.1 GHz, 14 cores (6 P-cores, 8 E-cores, 20 threads, 24 MB cache); the Core 7 250H at 2.5 and up to 5.4 GHz with E-core turbo 4.0 GHz, 14 cores (6 P-cores, 8 E-cores, 20 threads, 24 MB cache); the Core 7 240H at 2.5 and up to 5.2 GHz with E-core turbo 4.0 GHz, 10 cores (6 P-cores, 4 E-cores, 16 threads, 24 MB cache); the Core 5 220H at 2.7 and up to 4.9 GHz with E-core turbo 3.7 GHz, 12 cores (4 P-cores, 8 E-cores, 16 threads, 18 MB cache); the Core 5 210H at 2.2 and up to 4.8 GHz with E-core turbo 3.6 GHz, 8 cores (4 P-cores, 4 E-cores, 12 threads, 12 MB cache). All five H parts launched December 18, 2024 (Raptor Lake-H Refresh, Intel 7, 45 W base power, 115 W maximum turbo power, no NPU, last-gen Iris Xe iGPU). The source URL recorded for each H entry is the Notebookcheck article that reproduces Intel's own table. One judgement call is recorded for the two U parts, the same disclosed mirror exception as the 251HX in v2.1.76 and the 5700X3D in v2.1.78: no Intel document with a per-SKU table for the U parts surfaced, so the Core 7 250U at 1.9 and up to 5.4 GHz with E-core turbo 4.1 GHz and the Core 5 220U at 1.5 and up to 5.0 GHz with E-core turbo 3.9 GHz rest on agreeing ARK mirrors (siliconcat, cputronic, topcpu, technical.city, and techpowerup for the 220U), not on a direct Intel read, because Intel's own compare pages return HTTP 403 in this sandbox and no live compare-page spot-check was possible in this cycle; the source URL recorded for each U entry is its siliconcat page. Both U parts launched January 6, 2025 (Raptor Lake-U Refresh, Intel 7, 10 cores with 2 P-cores and 8 E-cores, 12 threads, 12 MB cache, 15 W base power, 55 W maximum turbo power). Two mirror disagreements are resolved in this cycle: the 250U P-core base is 1.9 GHz per the agreeing ARK mirrors, while notebookcheck's per-SKU page gives 1.8 GHz because it copies the renamed predecessor 150U's clocks (pc-builds also lists 1.8 GHz); the 220U E-core turbo is 3.9 GHz per the agreeing ARK mirrors including techpowerup, while notebookcheck's per-SKU page gives 3.8 GHz, the predecessor 120U's figure. The dataset's base_ghz is Intel's Performance-core Base Frequency, boost_ghz is Intel's Max Turbo Frequency, and e_core_boost_ghz is Intel's E-core Max Turbo Frequency, per its own definition. Staying out, recorded in the dataset coverage text: the Core 7 230H, a real SKU shipping since June 12, 2026 in HP Victus, Lenovo LOQ and Chuwi laptops, because no source found publishes its P-core base distinctly (notebookcheck gives only the 1.8 to 5.2 GHz min-max range from the E-core base to the P-core turbo), and it is not inferred from the 240H; and the Core 3 100U, a Series 1 part launched January 2024 and a rebrand of the i3-1315U, not part of the Core 200U family. The CPU page shows their "Rated base clock" and "Rated boost clock" under their own labels with the dataset version as their source; they never replace the measured Min and Max clocks, and a model that is not listed gets no rated spec. No code change was needed: the CPU normalizer already maps the seven names, and the Settings card shows the CPU count from the dataset, so it reads 210 with no code change. The coverage text in the dataset is corrected in the same edit: it now reads "210 CPUs, 95 GPUs in v1.43.0", names all seven Core 200H and 200U parts, and replaces the v2.1.78 stays-out note for the family with the new sourcing plus the 230H and 100U exclusions. One honest limitation is recorded: Intel's own compare pages return HTTP 403 in this sandbox and the 210H compare page could only be read through the search index, not fetched live, so no live compare-page spot-check was possible in this cycle; the H figures rest on Intel's own table as reproduced verbatim by Notebookcheck plus the agreeing mirrors, and the U figures on the agreeing mirrors named above. The logos and icons are unchanged by Jawad's decision. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.43.0, 210 CPUs and 95 GPUs, and the verified Core 9 270H, Core 7 250H, Core 7 240H, Core 5 220H, Core 5 210H, Core 7 250U and Core 5 220U figures, plus the normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.43.0 in German, French and Spanish).

## v2.1.78, shipped

- **Hardware database: three AMD Ryzen 5000 X3D desktop CPUs (Beyond v2.0, item 5).** The dataset grows from v1.41.0 to v1.42.0 and adds the three remaining Ryzen 5000 X3D parts, which complete the AM4 X3D set alongside the 5800X3D already in the dataset. The slice was picked by a fresh audit: the brief named the PRO-adjacent gaps first, and the audit found none to add (the Ryzen AI 400 family is already complete in the dataset, consumer and PRO, desktop and mobile, and its mobile H variants have no separate AMD product pages with their own clock figures); the Intel 13th-gen HX P-core Base Frequency rows stay out on the v2.1.47 finding, re-checked in v2.1.72 and not re-checked in this cycle; the Intel Core 200H and 200U non-Ultra mobile families (Raptor Lake-H/U Refresh, launched January 2025) were checked and set aside, because no Intel document publishes their figures, only the CES 2025 keynote deck, whose table text is garbled, and pre-launch leaks, so the family stays out until an Intel document does; and the dependency-audit fallback was not needed. The Ryzen 5000 X3D gap was named in the dataset's own stays-out list, which kept the 5700X3D out because AMD's own product page could not be read live. That exclusion is overturned in this cycle for all three parts. All figures were compiled by hand (each entry records its source URL and retrieval date): the Ryzen 7 5700X3D at Base Clock 3.0 GHz and Max Boost Clock up to 4.1 GHz, 8 cores (16 threads, 96 MB L3 cache, 105 W), launched January 8, 2024; the Ryzen 5 5600X3D at 3.3 and up to 4.4 GHz, 6 cores (12 threads, 96 MB L3 cache, 105 W), launched July 7, 2023; the Ryzen 5 5500X3D at 3.0 and up to 4.0 GHz, 6 cores (12 threads, 96 MB L3 cache, 105 W), launched June 5, 2025. All three are Zen 3 Vermeer on AM4 with 3D V-Cache. The 5600X3D verifies against AMD's own live support page (Base Clock 3.3 GHz, Max. Boost Clock Up to 4.4 GHz, 96 MB L3 cache, 105 W, launch 07/07/2023) and the 5500X3D against AMD's own live product page (Base Clock 3 GHz, Max. Boost Clock Up to 4 GHz, 96 MB L3 cache, 105 W, launch 6/5/2025, Latin America). One judgement call is recorded for the 5700X3D, the same disclosed exception as the 251HX in v2.1.76: AMD has removed the per-product page, the product URL now redirects to the specifications hub and the new-style URL redirects to the AMD homepage, so no live AMD read was possible in this cycle; the figures rest on AMD's own documentation as mirrored verbatim, AMD's own spec sheet (Product ID Boxed 100-100001503WOF, launch 1/8/2024) and AMD's own user manual (Base Clock Speed 3.0 GHz, Max Boost Clock Up to 4.1 GHz, 96 MB L3 cache, 105 W), plus a dated PDF print of AMD's own product page amd.com/en/product/14101 and unanimous third-party agreement (nanoreview, siliconcat, cputronic, cpu-monkey and technical.city); the only disagreement is a pre-launch speculation piece that guessed 3.4 and 4.5 GHz before AMD announced the part, superseded by AMD's own announcement at 3.0 and 4.1 GHz. The source URL recorded for the 5700X3D entry is AMD's own product URL amd.com/en/product/14101, the URL AMD itself used for this product. The dataset's base_ghz is AMD's Base Clock and boost_ghz is AMD's Max Boost Clock, per its own definition. The CPU page shows their "Rated base clock" and "Rated boost clock" under their own labels with the dataset version as their source; they never replace the measured Min and Max clocks, and a model that is not listed gets no rated spec. No code change was needed: the CPU normalizer already maps the three names, and the Settings card shows the CPU count from the dataset, so it reads 203 with no code change. The coverage text in the dataset is corrected in the same edit: it now reads "203 CPUs, 95 GPUs in v1.42.0", names the AM4 X3D set as complete, and replaces the stays-out note for the 5700X3D with the new sourcing. The logos and icons are unchanged by Jawad's decision. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.42.0, 203 CPUs and 95 GPUs, and the verified Ryzen 7 5700X3D, Ryzen 5 5600X3D and Ryzen 5 5500X3D figures, plus the normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.42.0 in German, French and Spanish).

## v2.1.77, shipped

- **Hardware database: three Intel Core Ultra Series 3 mobile CPUs (Beyond v2.0, item 5).** The dataset grows from v1.40.0 to v1.41.0 and adds the three remaining Series 3 (Panther Lake) mobile parts, which complete the family at fifteen SKUs. The slice was picked by a fresh audit: the brief named the PRO-adjacent gaps first, and the audit found none to add, so the Series 3 gap named second in the brief was checked next. The Ryzen AI 400 family is already complete in the dataset (consumer and PRO, desktop and mobile), and its mobile H variants have no separate AMD product pages with their own clock figures: AMD's CES 2026 press release and the AI 400 product deck list the mobile family as the HX 475, HX 470, 465, 450, 445, 440 and 435 with no separate H-suffixed SKUs, so there is nothing to add there, and the coverage text keeps the note. The Intel 13th-gen HX P-core Base Frequency rows stay out on the v2.1.47 finding, re-checked in v2.1.72 and not re-checked in this cycle, and the dependency-audit fallback was not needed. The three Series 3 parts were deliberately absent since v2.1.65 because their Performance-core Base Frequency figures could not be verified live on Intel's own page in that cycle, even though mirrors agreed on the 335 at 2.2 GHz and the 332 at 2.5 GHz and disagreed on the 322 (2.5 vs 1.9 GHz). That exclusion is overturned in this cycle: Intel's own Series 3 Quick Reference Guide v1 (cdrdv2-public.intel.com 871380), fetched directly as a PDF in this cycle and text-extracted, lists all three SKUs in its SKU table with P-core Max Turbo Frequency 4.6, 4.4 and 4.4 GHz, 8, 6 and 6 cores and threads, 12 MB Intel Smart Cache, 25 W processor base power and 55 W maximum turbo power, which corroborates the SKUs, their Max Turbo, cores and cache from Intel itself. The guide publishes no P-core base frequency, so one judgement call is recorded, the same disclosed exception as the 251HX in v2.1.76: the Performance-core Base Frequencies 2.2, 2.5 and 2.5 GHz and the Low Power Efficient-core Max Turbo Frequencies 3.4, 3.3 and 3.3 GHz rest on agreeing ARK mirrors (nanoreview, topcpu, siliconcat, cputronic, cpu-monkey, eatyourbytes and notebookcheck), not on a direct Intel read, because Intel's own compare pages return HTTP 403 in this sandbox and no Intel compare page containing these three SKUs surfaced. The old 322 disagreement is resolved in this cycle: the 1.9 GHz figure is the Low Power Efficient-core base, not the P-core base (technical.city mislabels it as the base clock, and notebookcheck's "1.9 - 4.4 GHz" is a min-max range from the LP-E base to the P-core turbo); every source that reports the P-core base distinctly agrees on 2.5 GHz for both the 332 and the 322. All figures were compiled by hand (each entry records its source URL and retrieval date): the Core Ultra 5 335 at Performance-core Base Frequency 2.2 GHz and Max Turbo Frequency up to 4.6 GHz with Low Power Efficient-core Max Turbo Frequency 3.4 GHz, 8 cores (4 P-cores, 4 LP E-cores, 8 threads, 12 MB cache); the Core Ultra 5 332 at 2.5 and up to 4.4 GHz with LP E-core turbo 3.3 GHz, 6 cores (2 P-cores, 4 LP E-cores, 6 threads, 12 MB cache); the Core Ultra 5 322 at 2.5 and up to 4.4 GHz with LP E-core turbo 3.3 GHz, 6 cores (2 P-cores, 4 LP E-cores, 6 threads, 12 MB cache). All three launched January 5, 2026 (Panther Lake, Intel 18A, 25 W base power and 55 W maximum turbo power). The source URL recorded for each entry is Intel's own Series 3 Quick Reference Guide v1 PDF. The dataset's base_ghz is Intel's Performance-core Base Frequency, boost_ghz is Intel's Max Turbo Frequency, and e_core_boost_ghz is the Low Power Efficient-core Max Turbo Frequency for these three, the same rule the dataset already uses for the 365, 325 and 355, which also have no standard E-cores. The 332 and 322 are identical on clocks, cores, cache and graphics and differ only in vPro eligibility (the guide lists the 332 as eligible and the 322 as not), so both are listed, the same as the 378H and 368H. The CPU page shows their "Rated base clock" and "Rated boost clock" under their own labels with the dataset version as their source; they never replace the measured Min and Max clocks, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps the three names, and the Settings card shows the CPU count from the dataset, so it reads 200 with no code change. The coverage text in the dataset is corrected in the same edit: it now reads "200 CPUs, 95 GPUs in v1.41.0", names the Series 3 family as complete at fifteen SKUs, and replaces the v2.1.65 stays-out note for the 335, 332 and 322 with the new sourcing. One honest limitation is recorded: Intel's own compare pages return HTTP 403 in this sandbox and no Intel compare page containing these three SKUs surfaced, so no live compare-page spot-check was possible in this cycle; the Max Turbo, cores and cache rest on Intel's own Quick Reference Guide, and the P-core base and LP E-core turbo on the agreeing mirrors named above. The logos and icons are unchanged by Jawad's decision. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.41.0, 200 CPUs and 95 GPUs, and the verified Core Ultra 5 335, 332 and 322 figures, plus the normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.41.0 in German, French and Spanish).

## v2.1.76, shipped

- **Hardware database: one Intel Core Ultra 7 251HX mobile CPU (Beyond v2.0, item 5).** The dataset grows from v1.39.0 to v1.40.0 and adds the separate non-Plus Arrow Lake-HX SKU Intel announced April 1, 2026, which slots between the Core Ultra 5 245HX and the Core Ultra 7 255HX. The slice was picked by a fresh audit: the brief named the Core Ultra 7 251HX first, and the audit confirmed it is the genuine gap, so the AMD Ryzen AI 400 PRO desktop gaps (the AI 400 family is already complete in the dataset, consumer and PRO, desktop and mobile) and the dependency-audit fallback were not needed and were not researched in this cycle. The 251HX was deliberately absent since v2.1.75 because no Intel document corroborated its figures in that cycle, even though mirrors agreed on them (2.9, 5.1 and 4.5 GHz). That exclusion is overturned in this cycle: Intel's own APP Metrics for Intel Microprocessors, Revision 2, dated June 15, 2026, read directly in this cycle, lists "Intel Core Ultra 7 processor 251HX (30M Cache, up to 5.10 GHz)", which corroborates the model, the cache and the Max Turbo from Intel itself, and Intel's own Material Declaration Data Sheet for the 270HX Plus, 290HX Plus and 251HX names the SKU as an Intel product. All figures were compiled by hand (the entry records its source URL and retrieval date): the Core Ultra 7 251HX at Performance-core Base Frequency 2.9 GHz and Max Turbo Frequency up to 5.1 GHz with E-core Max Turbo Frequency 4.5 GHz, 18 cores (6 P-cores, 12 E-cores, 18 threads, 30 MB cache), 55 W base power and 160 W maximum turbo power. The source URL recorded for the entry is Intel's own document page for the Material Declaration Data Sheet (intel.com content-details 916308), because the APP Metrics PDF itself was read through the browser fetch path and no direct cdrdv2-public link for the Revision 2 document surfaced in search (the cdrdv2-public copy of the APP Metrics document is still Revision 1, dated January 6, 2026, and contains no 251HX row). One judgement call is recorded: the Performance-core Base Frequency of 2.9 GHz and the E-core Max Turbo Frequency of 4.5 GHz rest on agreeing ARK mirrors (nanoreview, topcpu, siliconcat, cputronic, eatyourbytes, rottenwifi, notebookcheck and cpubenchmark) and on outlet reads of Intel's own ARK specifications page (VideoCardz, TweakTown, WCCFTech, Lowyat, HWBusters and ThinkComputers all report the same figures from it), not on a direct Intel read, because Intel's own compare pages return HTTP 403 in this sandbox, the ARK spec table values could not be extracted here, the 200HX Quick Reference Guide v1.1 was fetched directly in this cycle and contains no 251HX row, and datasheet 832586 Rev 007 (March 2026) predates the April 1 announcement, so no newer datasheet revision surfaced. The E-core turbo from mirrors is the same rule used for the 200H, 200U, 200HX and 200HX Plus families; the P-core base from mirrors is a deliberate, disclosed exception for this one SKU, justified by the Intel APP Metrics corroboration of the SKU and its Max Turbo plus the unanimous mirrors. The only mirror disagreement is the technical.city listing that calls its base clock 2.5 GHz, which is the E-core base mislabeled. The dataset's base_ghz is Intel's Performance-core Base Frequency, boost_ghz is Intel's Max Turbo Frequency, and e_core_boost_ghz is Intel's E-core Max Turbo Frequency, per its own definition. Staying out, recorded in the dataset coverage text: the Core 7 245HX (non-Ultra, launched April 2026, a separate non-Ultra family whose clocks match the Ultra 5 235HX exactly, and its number collides with the Ultra 5 245HX); and there is no Core Ultra 5 225HX, the 200HX family is six SKUs. The Intel 13th-gen HX P-core Base Frequency rows stay out on the v2.1.47 finding, re-checked in v2.1.72 and not re-checked in this cycle. The CPU page shows their "Rated base clock" and "Rated boost clock" under their own labels with the dataset version as their source; they never replace the measured Min and Max clocks, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps the name, and the Settings card shows the CPU count from the dataset, so it reads 197 with no code change. The coverage text in the dataset is corrected in the same edit: it now reads "197 CPUs, 95 GPUs in v1.40.0", names the 251HX, and replaces the v2.1.75 stays-out note with the new sourcing. One honest limitation is recorded: Intel's own compare pages return HTTP 403 in this sandbox and the ARK spec table values could not be extracted (the fetched page text carries only boilerplate), so no live compare-page spot-check was possible in this cycle; the Max Turbo and cache rest on Intel's own APP Metrics Revision 2, and the P-core base and E-core turbo on the agreeing mirrors and outlet reads named above. The logos and icons are unchanged by Jawad's decision. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.40.0, 197 CPUs and 95 GPUs, and the verified Core Ultra 7 251HX figures, plus the normalizer case) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.40.0 in German, French and Spanish).

## v2.1.75, shipped

- **Hardware database: two Intel Core Ultra 200HX Plus Series mobile CPUs (Beyond v2.0, item 5).** The dataset grows from v1.38.0 to v1.39.0 and adds the Arrow Lake-HX Refresh family, which the dataset never listed. The slice was picked by a fresh audit: the brief named the Core Ultra 200HX Plus refresh family first, and the audit confirmed the two Plus chips are the genuine gap, so the AMD Ryzen AI 400 PRO desktop gaps and the dependency-audit fallback were not needed and were not researched in this cycle. The research also corrected the brief and the v2.1.73 and v2.1.74 coverage notes, which named the family as 290HX Plus plus 251HX with the launch dates swapped: Intel's own newsroom announcement says the series introduces two new processors, the Core Ultra 9 290HX Plus and the Core Ultra 7 270HX Plus, available from OEM partners starting March 17, 2026, and the 251HX is a separate non-Plus SKU announced April 1, 2026. All figures were compiled by hand (each entry records its source URL and retrieval date): the Core Ultra 9 290HX Plus at Performance-core Base Frequency 2.7 GHz and Max Turbo Frequency up to 5.5 GHz with E-core Max Turbo Frequency 4.7 GHz; the Core Ultra 7 270HX Plus at 2.4 and up to 5.3 GHz with E-core turbo 4.7 GHz. Both launched March 17, 2026 (Arrow Lake-HX Refresh, TSMC 3nm, 55 W base power, 160 W maximum turbo power): the 290HX Plus at 24 cores (8 P-cores, 16 E-cores, 24 threads, 36 MB cache) and the 270HX Plus at 20 cores (8 P-cores, 12 E-cores, 20 threads, 30 MB cache). The P-core base figures come from Intel's own Core Ultra 200HX Plus datasheet (832586 Rev 007, March 2026), Table 61, which publishes frequencies per configuration and the HX-Plus line has exactly two configurations, so the mapping to the two SKUs is one to one; the Max Turbo figures come from Intel's own press deck for the series and the ARK spec page titles, which publish the cache and the up-to Max Turbo per SKU; and the E-core turbo of 4.7 GHz for both comes from agreeing ARK mirrors (nanoreview, topcpu, cputronic and rottenwifi, plus Clove and Ryans spec sheets), the same mirror rule used for the 200H, 200U and 200HX families. The dataset's base_ghz is Intel's Performance-core Base Frequency, boost_ghz is Intel's Max Turbo Frequency, and e_core_boost_ghz is Intel's E-core Max Turbo Frequency, per its own definition. Staying out, recorded in the dataset coverage text: the Core Ultra 7 251HX (a separate non-Plus Arrow Lake-HX SKU announced April 1, 2026, and no Intel document corroborates its figures yet, so it stays out until one does, even though mirrors agree on 2.9, 5.1 and 4.5 GHz, 18 cores with 6 P-cores and 12 E-cores; its datasheet absence is expected because it was announced after datasheet Rev 007, and the technical.city listing that calls its base clock 2.5 GHz is the E-core base mislabeled); the Core 7 245HX (non-Ultra, launched April 2026, a separate non-Ultra family whose clocks match the Ultra 5 235HX exactly, and its number collides with the Ultra 5 245HX); and there is no Core Ultra 5 225HX, the 200HX family is six SKUs. The Intel 13th-gen HX P-core Base Frequency rows stay out on the v2.1.47 finding, re-checked in v2.1.72 and not re-checked in this cycle. The CPU page shows their "Rated base clock" and "Rated boost clock" under their own labels with the dataset version as their source; they never replace the measured Min and Max clocks, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps the two names, and the Settings card shows the CPU count from the dataset, so it reads 196 with no code change. The coverage text in the dataset is corrected in the same edit: it now reads "196 CPUs, 95 GPUs in v1.39.0", names both Plus parts, and corrects the family composition and launch dates the v2.1.73 and v2.1.74 entries carried. One honest limitation is recorded: Intel's own compare pages return HTTP 403 in this sandbox and the ARK spec table values could not be extracted (the fetched page text carries only boilerplate), so no live compare-page spot-check was possible in this cycle; the P-core base figures rest on Intel's own datasheet, the Max Turbo on Intel's own press deck and ARK page titles, and the E-core turbo on the agreeing mirrors named above. The logos and icons are unchanged by Jawad's decision. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.39.0, 196 CPUs and 95 GPUs, and the verified Core Ultra 200HX Plus figures, plus the normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.39.0 in German, French and Spanish).

## v2.1.74, shipped

- **Hardware database: two Intel Core Ultra 200HX Series mobile CPUs (Beyond v2.0, item 5).** The dataset grows from v1.37.0 to v1.38.0 and completes the Core Ultra 200HX mobile family at six SKUs, which the dataset listed with four until this cycle. The slice was picked by a fresh audit: the brief named the Intel Core Ultra 200HX family first, and the audit confirmed the four listed HX chips (285HX, 275HX, 265HX, 255HX) are already in the dataset while the two Ultra 5 chips (245HX, 235HX) are the genuine gap, so the AMD Ryzen 9000G and Ryzen AI 400 PRO desktop targets and the dependency-audit fallback were not needed and were not researched in this cycle. The two chips were deliberately absent since v2.1.47 because their figures could not be verified live on Intel's own page in that cycle, even though third-party listings agreed on them (245HX at 3.1, 5.1 and 4.5 GHz; 235HX at 2.9, 5.1 and 4.5 GHz). That exclusion is overturned in this cycle: Intel's own Quick Reference Guide for the 200HX series, v1.1 (read 2026-11-15 from a fresh fetch through the browser fetch path), publishes both rows in its HX table, and the same read confirms the four listed chips (285HX at 2.8 and up to 5.5 GHz, 275HX at 2.7 and up to 5.4 GHz, 265HX at 2.6 and up to 5.3 GHz, 255HX at 2.4 and up to 5.2 GHz). All figures were compiled by hand from that guide (each entry records its source URL and retrieval date): the Core Ultra 5 245HX at Performance-core Base Frequency 3.1 GHz and Max Turbo Frequency up to 5.1 GHz with E-core Max Turbo Frequency 4.5 GHz; the Core Ultra 5 235HX at 2.9 and up to 5.1 GHz with E-core turbo 4.5 GHz. Both launched January 6, 2025 (Arrow Lake-HX, TSMC 3nm FinFET, 55 W base power, 160 W maximum turbo power), 14 cores (6 P-cores and 8 E-cores, 14 threads, 24 MB cache). The source URL recorded for both entries is Intel's own document page for the guide (intel.com content-details 842532), because the guide PDF itself was read through the browser fetch path and no direct cdrdv2-public link for this document surfaced in search. One judgement call is recorded: the Quick Reference Guide table publishes the P-core base and the Max Turbo per SKU but no E-core turbo, so the E-core Max Turbo Frequency of 4.5 GHz for both chips comes from five agreeing ARK mirrors (nanoreview, siliconcat, techpowerup, cputronic, eatyourbytes), the same mirror rule used for the 200H and 200U families in v2.1.70 and v2.1.73; and the trailing numbers in the Quick Reference Guide HX table (2.0, 1.9, 1.9, 1.85, 1.9 and 1.8 GHz) are the graphics Max Frequency, not E-core base figures, so they are not recorded as base figures anywhere. The dataset's base_ghz is Intel's Performance-core Base Frequency, boost_ghz is Intel's Max Turbo Frequency, and e_core_boost_ghz is Intel's E-core Max Turbo Frequency, per its own definition. Staying out, recorded in the dataset coverage text: the Core Ultra 200HX Plus refresh family (Core Ultra 9 290HX Plus, launched April 1, 2026, and Core Ultra 7 251HX, launched March 17, 2026, a separate Arrow Lake-HX Refresh family whose figures were not verified against an Intel document in this cycle, the next candidate family); the Core 7 245HX (non-Ultra, launched April 2026, a separate non-Ultra family whose clocks match the Ultra 5 235HX exactly, and its number collides with the Ultra 5 245HX); and there is no Core Ultra 5 225HX, the 200HX family is six SKUs, which answers the brief's "225HX if it exists". The Intel 13th-gen HX P-core Base Frequency rows stay out on the v2.1.47 finding, re-checked in v2.1.72 and not re-checked in this cycle. The CPU page shows their "Rated base clock" and "Rated boost clock" under their own labels with the dataset version as their source; they never replace the measured Min and Max clocks, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps the two names, and the Settings card shows the CPU count from the dataset, so it reads 194 with no code change. The coverage text in the dataset is corrected in the same edit: it now reads "194 CPUs, 95 GPUs in v1.38.0" and names all six Core Ultra 200HX mobile parts. That correction also fixes a stale header the v2.1.73 cycle left behind: its ROADMAP entry claimed the coverage text read "192 CPUs, 95 GPUs in v1.37.0", but the file on disk still read "187 CPUs, 95 GPUs in v1.36.0" while the arrays already held 192 CPUs at v1.37.0; the body of the coverage text had been updated to name the 200H parts but the leading header had not. One honest limitation is recorded: Intel's own compare pages return HTTP 403 in this sandbox, so no live compare-page spot-check was possible in this cycle; the P-core base and Max Turbo figures rest on Intel's own Quick Reference Guide PDF, and the E-core turbo on the five agreeing ARK mirrors named above. The logos and icons are unchanged by Jawad's decision. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.38.0, 194 CPUs and 95 GPUs, and the verified Core Ultra 200HX Ultra 5 figures, plus the normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.38.0 in German, French and Spanish).

## v2.1.73, shipped

- **Hardware database: five Intel Core Ultra 200H Series mobile CPUs (Beyond v2.0, item 5).** The dataset grows from v1.36.0 to v1.37.0 with the 28 W Arrow Lake-H family, which the dataset never listed. The slice was picked by a fresh audit: the brief named the AMD Ryzen AI Max 300 consumer mobile parts first, but the Max 392 and Max 388 are already in the dataset, and the brief named the AMD Ryzen AI 400 consumer mobile parts second, but all seven are already in the dataset (AI 9 HX 475, HX 470, AI 9 465, AI 7 450, AI 7 445, AI 5 435, AI 5 430), so both targets yield nothing and the 200H family, the direct sibling of the 200U family shipped in v2.1.70 and listed in the same Quick Reference Guide, is the genuine gap, so the dependency-audit fallback was not needed and was not researched in this cycle. All five launched January 6, 2025 (Arrow Lake-H, TSMC 3nm FinFET, 28 W base power, 115 W maximum turbo power): the Core Ultra 9 285H and the Core Ultra 7 265H and 255H at 16 cores (6 P-cores, 8 E-cores and 2 Low Power Efficient-cores, 16 threads, 24 MB cache) with Arc 140T graphics, and the Core Ultra 5 235H and 225H at 14 cores (4 P-cores, 8 E-cores and 2 Low Power Efficient-cores, 14 threads, 18 MB cache), the 235H with Arc 140T and the 225H with Arc 130T. All figures were compiled by hand from Intel's own Quick Reference Guide for the 200H and 200U series, v1.1 (cdrdv2-public.intel.com, read 2026-11-15 from a fresh fetch), which publishes the Performance-core Base Frequency and the Max Turbo Frequency per SKU in its H table (each entry records its source URL and retrieval date): the 285H at Performance-core Base Frequency 2.9 GHz and Max Turbo Frequency up to 5.4 GHz with E-core Max Turbo Frequency 4.5 GHz; the 265H at 2.2 and up to 5.3 GHz with E-core turbo 4.5 GHz; the 255H at 2.0 and up to 5.1 GHz with E-core turbo 4.4 GHz; the 235H at 2.4 and up to 5.0 GHz with E-core turbo 4.4 GHz; the 225H at 1.7 and up to 4.9 GHz with E-core turbo 4.3 GHz. Each figure was cross-checked per SKU against ARK mirrors that agree on all five models (nanoreview, siliconcat, cputronic, notebookcheck, techpowerup). The dataset's base_ghz is Intel's Performance-core Base Frequency, boost_ghz is Intel's Max Turbo Frequency, and e_core_boost_ghz is Intel's E-core Max Turbo Frequency, per its own definition; the boost_definition text is corrected in the same edit to name the 200H family alongside the 200HX family for that field. Two judgement calls are recorded. First, the 235H P-core base is 2.4 GHz, higher than the 265H and 255H at 2.2 and 2.0 GHz: the 235H has only 4 P-cores against their 6, and the Quick Reference Guide and all five mirrors agree on it, so the figure stands as published. Second, the trailing numbers in the Quick Reference Guide H table (2.35, 2.3, 2.25, 2.25 and 2.2 GHz) are the graphics Max Frequency (Arc 140T at 2.35 GHz on the 285H, 2.3 GHz on the 265H, 2.25 GHz on the 255H and 235H, Arc 130T at 2.2 GHz on the 225H), not E-core base figures, so they are not recorded as base figures anywhere; the same reading was used for the 200U family in v2.1.70. The Intel 13th-gen HX P-core Base Frequency rows stay out on the v2.1.47 finding, re-checked in v2.1.72 and not re-checked in this cycle. The CPU page shows their "Rated base clock" and "Rated boost clock" under their own labels with the dataset version as their source; they never replace the measured Min and Max clocks, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps the five names, and the Settings card shows the CPU count from the dataset, so it reads 192 with no code change. The coverage text in the dataset is corrected in the same edit: it now reads "192 CPUs, 95 GPUs in v1.37.0" and names the five Core Ultra 200H mobile parts. One honest limitation is recorded: Intel's own compare pages return HTTP 403 in this sandbox, so no live compare-page spot-check was possible in this cycle; the figures rest on Intel's own Quick Reference Guide PDF plus the five agreeing ARK mirrors named above. The logos and icons are unchanged by Jawad's decision. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.37.0, 192 CPUs and 95 GPUs, and the verified Core Ultra 200H figures, plus the normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.37.0 in German, French and Spanish).

## v2.1.72, shipped

- **Hardware database: four AMD Ryzen AI Max PRO 300 Series mobile CPUs (Beyond v2.0, item 5).** The dataset grows from v1.35.0 to v1.36.0 with the PRO mirrors of the consumer AI Max mobile parts, which the dataset listed before this cycle with zero Max PRO 300 models. The slice was picked by a fresh audit: the brief named the Intel 13th-gen HX P-core Base Frequency rows first, and that target was re-checked in this cycle and still fails. Intel's own Compare Products page, crawled 2026-11-14, lists the i9-13980HX alongside the i9-13900K and i9-13900T and shows the Performance-core Base Frequency row, but the 13980HX cell is empty (3.00 GHz for the 13900K, 1.10 GHz for the 13900T, blank for the 13980HX), and the E-core Base Frequency cell is blank for it too; the Max Turbo Frequency of 5.60 GHz and E-core Max Turbo Frequency of 4.00 GHz are published for it, but the base rows are not, so the v2.1.47 finding stands and nothing is added for the 13th-gen HX family. Intel's own datasheet for the HX series gives P-core base per configuration (8P+16E at 55 W is 2.2 GHz, 8P+12E is 2.1 GHz), not per SKU, and mapping a configuration to a SKU would be inference, so it is not usable under the project rules; third-party mirrors list 2.2 GHz for the 13980HX, but the project rule requires vendor-page verification, the same rule that kept the Ultra 5 245HX and 235HX out. The Max PRO 300 family was the brief's second target and all four models verified cleanly, so the dependency-audit fallback was not needed and was not researched in this cycle. All four launched January 6, 2025 at CES 2025 (Strix Halo FP11, Zen 5, TSMC 4nm FinFET): the Ryzen AI Max+ PRO 395, Max PRO 390, Max PRO 385 and Max PRO 380. All figures were compiled by hand from AMD's own product page for each exact model (each entry records its source URL and retrieval date), read 2026-11-15 from fresh fetches: the Max+ PRO 395 at Base Clock 3.0 GHz and Max Boost Clock up to 5.1 GHz; the Max PRO 390 at 3.2 and up to 5.0 GHz; the Max PRO 385 at 3.6 and up to 5.0 GHz; the Max PRO 380 at 3.6 and up to 4.9 GHz. The Max+ PRO 395 is 16-core with Radeon 8060S graphics and up to 50 TOPS of NPU performance, the Max PRO 390 is 12-core with Radeon 8050S, the Max PRO 385 is 8-core with Radeon 8050S, and the Max PRO 380 is 6-core with Radeon 8040S. The figures were cross-checked against AMD's January 2026 mobile-for-business Quick Reference Guide, which lists the same four models with the same clocks, and the CES 2025 press release, which announces the Max+ PRO 395 at up to 5.1 GHz and 3.0 GHz base. The PRO clocks are identical to their consumer mirrors where a consumer twin exists in the dataset (the Max+ PRO 395 mirrors the Max+ 395, the Max PRO 390 mirrors the Max 390, the Max PRO 385 mirrors the Max 385); the Max PRO 380 has no consumer twin in the dataset, the consumer Max family lists only the 395, 392, 390, 388 and 385. The dataset's base_ghz is AMD's Base Clock and boost_ghz is AMD's Max Boost Clock, per its own definition. One judgement call is recorded: the v2.1.57 rationale that kept the Max PRO 300 family out, that no PRO chip had been listed in any cycle, is now outdated, because the AI 400 PRO desktop and mobile and AI 300 PRO families are listed, so the family is a genuine gap and it is closed here. The PRO parts are separate dataset entries only because they add AMD PRO Technologies (security, manageability, 18-month feature stability, 36-month support), not because any clock differs. The CPU page shows their "Rated base clock" and "Rated boost clock" under their own labels with the dataset version as their source; they never replace the measured Min and Max clocks, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps the four names, and the Settings card shows the CPU count from the dataset, so it reads 187 with no code change. The coverage text in the dataset is corrected in the same edit: it now reads "187 CPUs, 95 GPUs in v1.36.0" and names the four Max PRO 300 mobile parts. The logos and icons are unchanged by Jawad's decision. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.36.0, 187 CPUs and 95 GPUs, and the verified Max PRO 300 figures, plus the normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.36.0 in German, French and Spanish).

## v2.1.71, shipped

- **Hardware database: five AMD Ryzen AI PRO 300 Series mobile CPUs (Beyond v2.0, item 5).** The dataset grows from v1.34.0 to v1.35.0 with the PRO mirrors of the consumer AI 300 mobile parts, which the dataset listed before this cycle with zero PRO 300 models. The slice was picked by a fresh audit: the brief named the AMD Ryzen AI 300 PRO mobile family first, and all five models verified cleanly against AMD's own product pages, so the brief's fallbacks were not needed and were not researched in this cycle: the Intel 13th-gen HX P-core Base Frequency rows and any PRO-adjacent SKUs remain open for a later cycle. The Ryzen AI 9 HX PRO 375 and HX PRO 370 and the Ryzen AI 7 PRO 360 are Strix Point FP8 (Zen 5 and Zen 5c, TSMC 4nm FinFET); the Ryzen AI 7 PRO 350 and Ryzen AI 5 PRO 340 are Krackan Point FP8. All figures were compiled by hand from AMD's own product page for each exact model (each entry records its source URL and retrieval date), read 2026-11-15 from fresh fetches: the HX PRO 375 and HX PRO 370 at Base Clock 2.0 GHz and Max Boost Clock up to 5.1 GHz; the PRO 360 and PRO 350 at 2.0 and up to 5.0 GHz; the PRO 340 at 2.0 and up to 4.8 GHz. The HX PRO parts are 12-core (4 Zen 5 plus 8 Zen 5c) with Radeon 890M graphics, the PRO 360 is 8-core (3 Zen 5 plus 5 Zen 5c) with Radeon 880M, the PRO 350 is 8-core (4 Zen 5 plus 4 Zen 5c) with Radeon 860M, and the PRO 340 is 6-core (3 Zen 5 plus 3 Zen 5c) with Radeon 840M. The PRO clocks are identical to their consumer mirrors where a consumer twin exists (the HX PRO 375 and HX PRO 370 mirror the HX 375 and HX 370, the PRO 350 mirrors the AI 7 350, the PRO 340 mirrors the AI 5 340); the PRO 360 has no consumer twin in the dataset. The dataset's base_ghz is AMD's Base Clock and boost_ghz is AMD's Max Boost Clock, per its own definition. Two judgement calls are recorded. First, AMD's January 2026 mobile-for-business Quick Reference Guide lists only four PRO 300 models and omits the PRO 340, but AMD's own product page for the PRO 340 exists with full specifications (Base Clock 2.0 GHz, Max Boost Clock up to 4.8 GHz, 6 cores, Radeon 840M), so all five are listed. Second, AMD's current product pages show a launch date of 10/10/2025 for the three Strix Point parts, while the PRO 300 series was announced October 10, 2024; the day and month match and the year does not, and the dataset records no launch date, so the discrepancy affects nothing. The PRO parts are separate dataset entries only because they add AMD PRO Technologies (security, manageability, 18-month feature stability, 36-month support), not because any clock differs. The CPU page shows their "Rated base clock" and "Rated boost clock" under their own labels with the dataset version as their source; they never replace the measured Min and Max clocks, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps the five names, and the Settings card shows the CPU count from the dataset, so it reads 183 with no code change. The coverage text in the dataset is corrected in the same edit: it now reads "183 CPUs, 95 GPUs in v1.35.0" and names the five PRO 300 mobile parts. The logos and icons are unchanged by Jawad's decision. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.35.0, 183 CPUs and 95 GPUs, and the verified PRO 300 figures, plus the normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.35.0 in German, French and Spanish).

## v2.1.70, shipped

- **Hardware database: four Intel Core Ultra 200U Series mobile CPUs (Beyond v2.0, item 5).** The dataset grows from v1.33.0 to v1.34.0 with the 15 W Arrow Lake-U family the dataset never listed (zero 200U models before this cycle). The slice was picked by a fresh audit: the brief named the Core Ultra 200U family first, and all four models verified cleanly, so the brief's fallbacks were not needed and were not researched in this cycle: the AMD Ryzen AI 300 PRO mobile family and the Intel 13th-gen HX P-core Base Frequency rows remain open for a later cycle. All four launched January 6, 2025 (Arrow Lake-U, 12 cores with 2 P-cores, 8 E-cores and 2 Low Power Efficient-cores, 14 threads): the Core Ultra 7 265U and 255U and the Core Ultra 5 235U and 225U. All figures were compiled by hand from Intel's own Quick Reference Guide for the 200H and 200U series, v1.1 (cdrdv2-public.intel.com, read 2026-11-15), which publishes the Performance-core Base Frequency and the Max Turbo Frequency per SKU in its table, and each figure was cross-checked per SKU against ARK mirrors that agree on all four models (nanoreview, topcpu, siliconcat, cputronic): the Core Ultra 7 265U at Performance-core Base Frequency 2.1 GHz and Max Turbo Frequency up to 5.3 GHz with E-core Max Turbo Frequency 4.2 GHz; the Core Ultra 7 255U at 2.0 and up to 5.2 GHz with E-core turbo 4.2 GHz; the Core Ultra 5 235U at 2.0 and up to 4.9 GHz with E-core turbo 4.1 GHz; the Core Ultra 5 225U at 1.5 and up to 4.8 GHz with E-core turbo 3.8 GHz. Two judgement calls are recorded. First, the 265U P-core base is 2.1 GHz in the launched ARK figures and the Quick Reference Guide, while pre-launch listings and Intel's own December 2024 edge product brief list 1.8 GHz at 15 W; the launched figure decides it and the 1.8 GHz figure is rejected as pre-launch. Second, the trailing numbers in the Quick Reference Guide table (2.1, 2.1, 2.05 and 2.0 GHz) are the graphics Max Frequency, not E-core base figures, so they are not recorded as base figures anywhere. The Raptor Lake-based Core 200U (non-Ultra) models stay out: they are a separate family from the Core Ultra 200U family and are not in this slice. One honest limitation is recorded: Intel's own compare pages return HTTP 403 in this sandbox, so no live compare-page spot-check was possible in this cycle; the figures rest on Intel's own Quick Reference Guide PDF plus the four agreeing ARK mirrors. The dataset's base_ghz is Intel's Performance-core Base Frequency and boost_ghz is Intel's Max Turbo Frequency, per its own definition. The CPU page shows their "Rated base clock" and "Rated boost clock" under their own labels with the dataset version as their source; they never replace the measured Min and Max clocks, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps the four names, and the Settings card shows the CPU count from the dataset, so it reads 178 with no code change. The coverage text in the dataset is corrected in the same edit: it now reads "178 CPUs, 95 GPUs in v1.34.0" and names the 200U family, the 1.8 GHz pre-launch figure it supersedes, and the Core 200U family that stays out. The logos and icons are unchanged by Jawad's decision. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.34.0, 178 CPUs and 95 GPUs, and the verified 200U figures, plus the normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.34.0 in German, French and Spanish).

## v2.1.69, shipped

- **Hardware database: six AMD Ryzen AI PRO 400 Series mobile CPUs (Beyond v2.0, item 5).** The dataset grows from v1.32.0 to v1.33.0 with the PRO mirrors of the six consumer AI 400 mobile parts, which complete the AI 400 family in the dataset (consumer and PRO, desktop and mobile). The slice was picked by a fresh audit: the brief named Ryzen 9000G desktop APUs first, but no such product exists, the name was a rumor and AMD never announced a desktop part under it, and the real Zen 5 desktop APU family is the Ryzen AI 400 Series announced March 2, 2026, already in the dataset (consumer desktop in v2.1.62, PRO desktop in v2.1.68); the audit then found the six PRO mobile parts absent from the dataset (zero PRO mobile models of any family). All six launched January 5, 2026 (CES 2026, Gorgon Point FP8, Zen 5 and Zen 5c, TSMC 4nm FinFET): the Ryzen AI 9 HX PRO 475 and HX PRO 470, the Ryzen AI 9 PRO 465, the Ryzen AI 7 PRO 450, and the Ryzen AI 5 PRO 440 and PRO 435. All figures were compiled by hand from AMD's own product page for each exact model (each entry records its source URL and retrieval date), read 2026-11-15 from fresh fetches: the Ryzen AI 9 HX PRO 475 and HX PRO 470 at Base Clock 2.0 GHz and Max Boost Clock up to 5.2 GHz; the Ryzen AI 9 PRO 465 at 2.0 and up to 5.0 GHz; the Ryzen AI 7 PRO 450 at 2.0 and up to 5.1 GHz; the Ryzen AI 5 PRO 440 at 2.0 and up to 4.8 GHz; the Ryzen AI 5 PRO 435 at 2.0 and up to 4.5 GHz. The HX PRO parts are 12-core (4 Zen 5 plus 8 Zen 5c) with Radeon 890M graphics, the 465 is 10-core (4 plus 6) with Radeon 880M, the 450 is 8-core (4 plus 4) with Radeon 860M, and the 440 and 435 are 6-core with Radeon 840M; the 440 is 3 Zen 5 plus 3 Zen 5c and the 435 is 2 Zen 5 plus 4 Zen 5c. Two judgement calls are recorded. First, the HX PRO 470 is identical to the HX PRO 475 on clocks, cores, cache and graphics and differs only in NPU (55 TOPS against 60), so both are listed. Second, the H variants of the mobile parts (H PRO 450, H PRO 465 and the rest) stay out: no separate AMD product pages with their own clock figures surfaced for them in this cycle. The PRO clocks are identical to their consumer mirrors, so the PRO parts are separate dataset entries only because the PRO parts add AMD PRO Technologies (security, manageability, 18-month feature stability, 36-month support), not because any clock differs. The dataset's base_ghz is AMD's Base Clock and boost_ghz is AMD's Max Boost Clock, per its own definition. The CPU page shows their "Rated base clock" and "Rated boost clock" under their own labels with the dataset version as their source; they never replace the measured Min and Max clocks, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps the six names, and the Settings card shows the CPU count from the dataset, so it reads 174 with no code change. The coverage text in the dataset is corrected in the same edit: it now reads "174 CPUs, 95 GPUs in v1.33.0" and names the PRO mobile family, the completed AI 400 family, the non-existent Ryzen 9000G, and the H variants that stay out. The logos and icons are unchanged by Jawad's decision. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.33.0, 174 CPUs and 95 GPUs, and the verified PRO mobile figures, plus the normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.33.0 in German, French and Spanish).

## v2.1.68, shipped

- **Hardware database: six AMD Ryzen AI PRO 400 Series desktop CPUs (Beyond v2.0, item 5).** The dataset grows from v1.31.0 to v1.32.0 with the PRO mirrors of the six consumer AI 400 desktop G and GE parts, which complete the desktop half of the AI 400 family. The slice was picked by a fresh audit: the brief named Core Ultra Series 3 desktop, but Intel's own Series 3 Quick Reference Guide (cdrdv2-public.intel.com) lists only mobile H, non-H and U parts across its whole thirteen-SKU table, so no Series 3 desktop SKUs exist and there is nothing to add there. The audit then found the six PRO desktop parts absent from the dataset (zero PRO models of any family). All six launched March 2, 2026 (Gorgon Point AM5, Zen 5, TSMC 4nm FinFET, AM5 desktop systems from OEMs in Q2 2026): the Ryzen AI 7 PRO 450G and 450GE, the Ryzen AI 5 PRO 440G and 440GE, and the Ryzen AI 5 PRO 435G and 435GE. All figures were compiled by hand from AMD's own product page for each exact model (each entry records its source URL and retrieval date), read 2026-11-15, and each figure also matches AMD's own March 2, 2026 press release table: the Ryzen AI 7 PRO 450G and 450GE at Base Clock 2.0 GHz and Max Boost Clock up to 5.1 GHz; the Ryzen AI 5 PRO 440G and 440GE at 2.0 and up to 4.8 GHz; the Ryzen AI 5 PRO 435G and 435GE at 2.0 and up to 4.5 GHz. The G parts run at 65W and the GE parts at 35W; the Ryzen AI 7 PRO parts are 8-core with Radeon 860M graphics and the Ryzen AI 5 PRO parts are 6-core with Radeon 840M graphics. One judgement call is recorded. The PRO clocks are identical to their consumer mirrors, so the PRO parts are separate dataset entries only because the PRO parts add AMD PRO Technologies (security, manageability, 18-month feature stability, 36-month support), not because any clock differs. The dataset's base_ghz is AMD's Base Clock and boost_ghz is AMD's Max Boost Clock, per its own definition. The CPU page shows their "Rated base clock" and "Rated boost clock" under their own labels with the dataset version as their source; they never replace the measured Min and Max clocks, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps the six names, and the Settings card shows the CPU count from the dataset, so it reads 168 with no code change. The coverage text in the dataset is corrected in the same edit: it now reads "168 CPUs, 95 GPUs in v1.32.0" and names the PRO family and the fact that no Series 3 desktop SKUs exist. The logos and icons are unchanged by Jawad's decision. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.32.0, 168 CPUs and 95 GPUs, and the verified PRO figures, plus the normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.32.0 in German, French and Spanish).

## v2.1.67, shipped

- **Dependency audit: every direct package is already current, so no package version changes in this cycle.** `dotnet list package --outdated` reports zero outdated direct packages on all four projects, and each direct package was checked against nuget.org one by one: Avalonia 12.1.6, Avalonia.Desktop 12.1.6, Avalonia.Fonts.Inter 12.1.6, Avalonia.Themes.Fluent 12.1.6, AvaloniaUI.DiagnosticsSupport 2.2.3, CommunityToolkit.Mvvm 8.4.2, LibreHardwareMonitorLib 0.9.6, Microsoft.Data.Sqlite 11.0.0, Microsoft.NET.Test.Sdk 18.12.0, SQLitePCLRaw.bundle_e_sqlite3 3.0.5, System.Diagnostics.EventLog 11.0.0, System.Management 11.0.0, coverlet.collector 10.1.0, xunit 2.9.3, xunit.runner.visualstudio 4.0.1. `dotnet list package --vulnerable` reports no vulnerable packages in any project, including transitive dependencies. The transitive check does list outdated packages, but none of them is a safe bump. The major jumps (SkiaSharp 3.119.4 to 4.156.1, HarfBuzzSharp 8.3.1.3 to 14.4.0.101, System.IO.Ports 10.0.3 to 11.0.0, Microsoft.Extensions.DependencyInjection.Abstractions and Microsoft.Extensions.Logging.Abstractions 8.0.0 to 11.0.0, Microsoft.IO.RecyclableMemoryStream 3.0.1 to 4.0.1, DiskInfoToolkit 1.1.2 to 2.2.2, xunit.analyzers 1.18.0 to 2.3.0) are out of scope for this cycle and arrive with the next major release of the parent that pins them. The patch and minor transitive updates (MicroCom.Runtime 0.11.6 to 0.11.7, SQLite 3.53.4 to 3.54.1, BlackSharp.Core 1.0.7 to 1.2.11, RAMSPDToolkit-NDD 1.4.2 to 1.6.4, Tmds.DBus.Protocol 0.94.1 to 0.95.2) are pinned by a parent that is itself current, so taking them would mean running combinations the parent never shipped or tested, and the test suite cannot validate the affected runtime paths, so they are deliberately left at their pinned versions. The logos and icons are unchanged by Jawad's decision, and there is no hardware-database change in this cycle. Tests: `InstallerScriptTests` (the version test renamed to Version_Is_2_1_67_InAllThreePlaces with all three assertions updated).

## v2.1.66, shipped

- **System collector: the driver group gets an 8 second time-box.** In v2.1.65 CI, the live test DriverSignatureSummaryTests.Live_SystemCollector_ReportsExactDriverCountsWithNoTruncationNote failed twice at exactly 5.0 seconds: the 1000-row cold-WMI driver enumeration exceeded the 5 second per-group box on loaded runners (run 43665837906, attempts 1 and 2), so the driver group returned nothing and the test's exact-count assertion failed. The same test passed on main in 428 ms and in the rerun in 298 ms, so this is a marginal timeout cliff on loaded runners, not a logic error. The fix gives the driver group its own 8 second time-box (SystemCollector.DriverGroupTimeout) while every other read group stays at 5 seconds; the groups run at the same time, so the collector still finishes inside the 10 second per-collector budget. The live test is unchanged and still asserts exact driver counts with no truncation note. No values change: the same WMI queries, the same specs, the same sources, only the timing. The logos and icons are unchanged by Jawad's decision, and there is no hardware-database change in this cycle. Tests: `SystemCollectorTests` (new test pins the driver group timeout at 8 seconds and inside the per-collector budget, alongside the existing group-timeout test) and the existing live collector test, which still runs the real collector on Windows.

## v2.1.65, shipped

- **Hardware database: twelve Intel Core Ultra Series 3 mobile CPUs (Beyond v2.0, item 5).** The dataset grows from v1.30.0 to v1.31.0 with the Panther Lake family the dataset never listed. The slice was picked by a fresh audit: the consumer hardware-database families are complete (Ryzen 100 in v2.1.63, AI 400 desktop in v2.1.62) and the System-collector timeout is fixed in v2.1.64, and the audit found the whole Series 3 family absent from the dataset (zero Ultra 3xx models). All twelve launched January 5, 2026 (Panther Lake, Intel 18A, retail Q1 2026): the X9 388H and X9 378H, X7 368H and X7 358H, 9 386H, 7 366H and 7 356H, 5 338H and 5 336H, and the 7 365, 5 325 and 7 355. All figures were compiled by hand from Intel's own compare pages (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-13 from the eleven-SKU compare page and 2026-11-12 for the 378H from the four-SKU compare page: the X9 388H at Performance-core Base Frequency 2.1 GHz and Max Turbo Frequency up to 5.1 GHz; the X9 378H at 2.0 and up to 5.0 GHz; the X7 368H at 2.0 and up to 5.0 GHz; the 9 386H at 2.1 and up to 4.9 GHz; the X7 358H at 1.9 and up to 4.8 GHz; the 7 366H at 2.0 and up to 4.8 GHz; the 5 338H at 1.9 and up to 4.7 GHz; the 7 356H at 1.9 and up to 4.7 GHz; the 5 336H at 1.9 and up to 4.6 GHz; the 7 365 at 2.4 and up to 4.8 GHz; the 5 325 at 2.1 and up to 4.5 GHz; the 7 355 at 2.3 and up to 4.7 GHz. The E-core turbo figures are Intel's Efficient-core Max Turbo Frequency for the eight H models (3.8, 3.8, 3.8, 3.5, 3.7, 3.6, 3.4, 3.5 and 3.4 GHz in the same order as above), and the Low Power Efficient-core Max Turbo Frequency for the 365, 325 and 355 (3.6, 3.4 and 3.5 GHz), which have no standard E-cores; the boost definition now names the Series 3 non-H models alongside the 200V parts for that rule. Two judgement calls are recorded. First, the 388H P-core base is 2.1 GHz on Intel's own page while third-party listings disagree (2.1 vs 2.2 GHz); Intel's page decides it. Second, the 378H is identical to the 368H on clocks, cores, cache and graphics and differs only in vPro eligibility, so both are listed. The 335, 332 and 322 stay out: their Performance-core Base Frequency figures could not be verified live on Intel's own page in this cycle (Intel's spec pages render their tables in JavaScript, so a text fetch returns only the page boilerplate, and no Intel compare page containing those three SKUs surfaced). Third-party listings agree on the 335 at 2.2 GHz and the 332 at 2.5 GHz but disagree on the 322 (2.5 vs 1.9 GHz), and third-party agreement is not a dataset source, so all three stay out and the coverage text names them, the same rule that kept the 245HX and 235HX out. The dataset's base_ghz is Intel's Performance-core Base Frequency and boost_ghz is Intel's Max Turbo Frequency, per its own definition. The CPU page shows their "Rated base clock" and "Rated boost clock" under their own labels with the dataset version as their source; they never replace the measured Min and Max clocks, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps the twelve names, and the Settings card shows the CPU count from the dataset, so it reads 162 with no code change. The coverage text in the dataset is corrected in the same edit: it now reads "162 CPUs, 95 GPUs in v1.31.0" and names the Series 3 family and the three models that stay out. The logos and icons are unchanged by Jawad's decision. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.31.0, 162 CPUs and 95 GPUs, and the verified Series 3 figures, plus the normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.31.0 in German, French and Spanish).
- **Dependency audit re-attested for v2.1.65:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.64 and v2.1.65; `dotnet list package --outdated` reports zero outdated packages on all four projects.

## v2.1.64, shipped

- **System collector: one slow read can no longer take the whole System page down.** On a real standard-user laptop running v2.1.59 (November 15, 2026), the System page showed "Not available" with "Collector timed out after 10 seconds". The honesty behavior worked: no value was invented, and the age signals from the other collectors did read. The defect was in SystemCollector.CollectAsync: it read its sources strictly one after another, about ten independent blocking reads (four WMI identity queries, TPM with a fallback query, DeviceGuard, BitLocker, a 1000-row PnP driver enumeration, and a 30-day event log walk of up to 10,000 entries), so its wall time was the sum of all of them. The slow reads are the driver enumeration, the event log walk and the BitLocker provider query, each of which can take seconds on its own, plus the first WMI query paying the cold WMI service start. The fix runs the independent read groups at the same time, each on the thread pool with its own 5 second time-box (RunWithTimeoutAsync, a WhenAny race against a delay, mirroring the per-collector race in MachineScanService one level down, with later faults observed so they never surface as unobserved), so the collector finishes in about the slowest group's time, always inside the 10 second per-collector budget with room for assembly, and a group that does not finish in time contributes nothing: its specs read as not reported, exactly as a read that returned no rows does, while every other spec keeps its measured value. The snapshot assembly is extracted into the pure BuildSnapshot function of the read groups, so the partial-result path is unit-testable without live WMI (same pattern as DriverSignatureSpecs and DriversCollector.BuildSnapshot): a missing driver group leaves only the driver specs not reported, a missing event log group leaves only the three event log specs not reported, and only a missing core group reports the whole component as not available, with the same reason string as before. No values change: the same WMI queries (the SELECT lists are still pinned by CollectorWmiSelectionTests), the same specs, the same sources, only the timing. The logos and icons are unchanged by Jawad's decision, and there is no hardware-database change in this cycle. Tests: `SystemCollectorTests` (the group timeout fits inside the per-collector budget, the timeout helper returns a fast value, cuts a slow group off after its budget, propagates a fault and throws on a cancelled token, plus BuildSnapshot with all groups present, with the driver group missing, with the event log group missing, and with the core group missing) and the existing live collector test, which still runs the real collector on Windows. The System-collector 10 second timeout item from the real-laptop run is addressed by this cycle; the re-check on Jawad's laptop is still owed when he runs the new version. the remaining open items are the code-signing decision (plug-in loading stays gated on it), the per-dollar verdict sentence (stays English in every language, flagged for Jawad's veto), the v1.1 report CSV BOM decision, and the desktop shortcut task default.
- **Dependency audit re-attested for v2.1.64:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.63 and v2.1.64.

## v2.1.63, shipped

- **Hardware database: five AMD Ryzen 100 Rembrandt mobile CPUs and the hybrid Ryzen 5 126 (Beyond v2.0, item 5).** The dataset grows from v1.29.0 to v1.30.0 with the half of the consumer Ryzen 100 family the dataset never listed. This closes the item the v2.1.62 entry left open ("The older Rembrandt Zen 3+ Ryzen 100 chips ... and the hybrid Ryzen 5 126 ... stay out: both are for a future cycle"). With them the consumer Ryzen 100 family is complete: the four Hawk Point CPUs shipped in v2.1.60 (Ryzen 9 180, Ryzen 7 165, Ryzen 7 155, Ryzen 5 125), the five Rembrandt Zen 3+ CPUs and the hybrid Ryzen 5 126 in this cycle. The Rembrandt five are rebadged Ryzen 7035 (Rembrandt-R) parts, TSMC 6nm FinFET, launched October 1, 2025: the Ryzen 7 170 and 160 are 8-core with Radeon 680M graphics, the Ryzen 5 150 and 130 are 6-core with Radeon 660M graphics, and the Ryzen 3 110 is 4-core with Radeon 660M graphics. The Ryzen 5 126 is the hybrid Hawk Point part: one Zen 4 core plus three Zen 4c cores on the FP8 socket, TSMC 4nm FinFET, with Radeon 740M graphics. All figures were compiled by hand from AMD's own product pages (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-15 from fresh fetches: the Ryzen 7 170 at Base Clock 3.2 GHz and Max Boost Clock up to 4.75 GHz; the Ryzen 7 160 at 2.7 and up to 4.75 GHz; the Ryzen 5 150 at 3.3 and up to 4.55 GHz; the Ryzen 5 130 at 2.9 and up to 4.55 GHz; the Ryzen 3 110 at 3.0 and up to 4.3 GHz; the Ryzen 5 126 at 2.8 and up to 4.6 GHz. The brief named the 160 as a Ryzen 5, but AMD's own product page lists it as Ryzen 7 160 (8 cores, 16 threads), so that is the name added; no Ryzen 5 160 exists in the Ryzen 100 family. The Ryzen 5 126 carries a single headline Base Clock of 2.8 GHz on AMD's page, so that is the base_ghz recorded. The dataset's base_ghz is AMD's headline Base Clock and boost_ghz is AMD's Max Boost Clock, per its own definition. The CPU page shows their "Rated base clock" and "Rated boost clock" under their own labels with the dataset version as their source; they never replace the measured Min and Max clocks, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps the six names, and the Settings card shows the CPU count from the dataset, so it reads 150 with no code change. The coverage text in the dataset is corrected in the same edit: it still read "138 CPUs, 95 GPUs in v1.28.0" after v2.1.62 and named neither the six desktop G and GE parts of the AI 400 family nor the Rembrandt and hybrid Ryzen 100 parts; it now reads "150 CPUs, 95 GPUs in v1.30.0" and names all of them. The logos and icons are unchanged by Jawad's decision. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.30.0, 150 CPUs and 95 GPUs, and the verified Rembrandt and Ryzen 5 126 figures, plus the normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.30.0 in German, French and Spanish).
- **Dependency audit re-attested for v2.1.63:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.62 and v2.1.63.

## v2.1.62, shipped

- **Hardware database: six AMD Ryzen AI 400 desktop G and GE CPUs (Beyond v2.0, item 5).** The dataset grows from v1.28.0 to v1.29.0 with the consumer desktop G and GE parts of the AI 400 family the dataset never listed. This closes the item the v2.1.60 entry left open ("The AM5 desktop G and GE parts of the AI 400 family stay open for a future cycle"). The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, every roadmap item through v2.1.61 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. All six launched on March 2, 2026 (Gorgon Point AM5, Zen 5, TSMC 4nm FinFET): the Ryzen AI 7 450G and 450GE are the 8-core parts with Radeon 860M graphics, the Ryzen AI 5 440G and 440GE and the Ryzen AI 5 435G and 435GE are the 6-core parts with Radeon 840M graphics; the G parts run at 65W and the GE parts at 35W. All figures were compiled by hand from AMD's own product pages (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-15 from fresh fetches: the Ryzen AI 7 450G and 450GE at Base Clock 2.0 GHz and Max Boost Clock up to 5.1 GHz; the Ryzen AI 5 440G and 440GE at 2.0 and up to 4.8 GHz; the Ryzen AI 5 435G and 435GE at 2.0 and up to 4.5 GHz. The family list comes from AMD's own March 2, 2026 press release table (450G, 440G, 435G) plus the GE product pages, which also exist on AMD's site. The 440G and 440GE middle parts were not in the original brief, which named only 450G/GE and 435G/GE, but they are real consumer parts, so all six are added and the consumer desktop family is complete. The PRO mirrors (Ryzen AI 7 PRO 450G/450GE, Ryzen AI 5 PRO 440G/440GE, Ryzen AI 5 PRO 435G/435GE) stay out: consumer parts only. The older Rembrandt Zen 3+ Ryzen 100 chips (Ryzen 7 170 and 160, Ryzen 5 150 and 130, Ryzen 3 110, launched October 1, 2025) stay out, and the hybrid Ryzen 5 126 (one Zen 4 plus three Zen 4c cores, FP8 socket) stays out: both are for a future cycle. The dataset's base_ghz is AMD's headline Base Clock and boost_ghz is AMD's Max Boost Clock, per its own definition. The CPU page shows their "Rated base clock" and "Rated boost clock" under their own labels with the dataset version as their source; they never replace the measured Min and Max clocks, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps the six names, and the Settings card shows the CPU count from the dataset, so it reads 144 with no code change. The logos and icons are unchanged by Jawad's decision. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.29.0, 144 CPUs and 95 GPUs, and the verified AI 400 desktop figures, plus the normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.29.0 in German, French and Spanish).
- **Dependency audit re-attested for v2.1.62:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.61 and v2.1.62.

## v2.1.61, shipped

- **Collapsed-rail tooltips.** The audit of the shell found one genuine UI gap: when the left rail is collapsed to 68px, all 22 nav items become icon-only because the label TextBlock is hidden, and no nav item carried a tooltip, so the destinations were unidentifiable until the rail was expanded again. The same applied to the two buttons at the bottom of the rail: the theme button showed only its sun icon and the rail button only ">>". The fix is three tooltip bindings in MainWindow.axaml and no layout change: the nav item template root Grid carries ToolTip.Tip bound to the item Label (the same string the visible text uses, so it follows the UI language), the theme button carries ToolTip.Tip bound to ThemeToggleText, and the rail button carries ToolTip.Tip bound to RailToggleText. That last string already existed on MainViewModel ("Collapse rail" / "Expand rail", raised on change) but was bound by no view; it is now wired up. The logos and icons are unchanged by Jawad's decision, and there is no hardware-database change in this cycle. Tests: `NavigationTests` (the item-template Grid carries the Label tooltip, both rail buttons carry their tooltips, RailToggleText reads "Collapse rail" expanded and "Expand rail" collapsed).
- **Dependency audit re-attested for v2.1.61:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.60 and v2.1.61.

## v2.1.60, shipped

- **Hardware database: four AMD Ryzen 100 mobile CPUs (Beyond v2.0, item 5).** The dataset grows from v1.27.0 to v1.28.0 with the Hawk Point Ryzen 100 chips the dataset never listed. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, every roadmap item through v2.1.59 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. The gap itself is the finding: the consumer Ryzen AI 300, Ryzen AI Max, Ryzen AI 400 and Ryzen 200 families were completed in v2.1.56, v2.1.57, v2.1.58 and v2.1.59, and this cycle's family-by-family check found the four CPU gaps below. All four launched on June 2, 2026 (Hawk Point, Zen 4 cores, TSMC 4nm FinFET, RDNA 3 graphics): the Ryzen 9 180 is the 8-core flagship, the Ryzen 7 165 and 155 are the 6-core parts, and the Ryzen 5 125 is the 4-core entry part. All figures were compiled by hand from AMD's own product pages (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-15 from fresh fetches: the Ryzen 9 180 at Base Clock 3.6 GHz and Max Boost Clock up to 4.9 GHz; the Ryzen 7 165 at 3.3 and up to 4.7 GHz; the Ryzen 7 155 at 3.0 and up to 4.7 GHz; the Ryzen 5 125 at 2.8 and up to 4.5 GHz. The dataset's base_ghz is AMD's headline Base Clock and boost_ghz is AMD's Max Boost Clock, per its own definition. The labelling question is settled by the live pages: when these four chips appeared in July 2026, reporting (ubergizmo, and the WhyCry July 11, 2026 note) said AMD's portal listed them as Zen 3+ while their clock speeds and 4 nm process matched Zen 4 silicon. The pages as read 2026-11-15 now say "Processor Architecture, Zen 4" with "Former Codename, Hawk Point", "TSMC 4nm FinFET" and RDNA 3 graphics on all four, so the dataset records them as the Zen 4 Hawk Point parts AMD's pages now describe. Two AMD quirks are real, not errors: the Ryzen 5 125's own product-page URL slug reads "amd-ryzen-7-125" while the page title and specs are the Ryzen 5 125's, and the Ryzen 5 125 is a 4-core Ryzen 5 (its 4-core, 8-thread layout matches the Ryzen 3 8440U class, not the usual 6-core Ryzen 5). The Ryzen 9 180 and Ryzen 7 165 carry an XDNA NPU rated up to 16 TOPS; the 155 and 125 do not list one, so no NPU figure is recorded for them. The deliberately-absent list was not changed in this cycle: no candidate from it was re-examined, and nothing on it was overturned. The older Rembrandt Zen 3+ Ryzen 100 chips (Ryzen 7 170 and 160, Ryzen 5 150 and 130, Ryzen 3 110, launched October 1, 2025) stay out: they are a different sub-family (6 nm, RDNA 2) for a future cycle. The hybrid Ryzen 5 126 (one Zen 4 plus three Zen 4c cores, FP8 socket) also stays out: it is a later addition outside the four named chips. The AM5 desktop G and GE parts of the AI 400 family (450G/GE, 435G/GE, launched March 2, 2026) stay open for a future cycle. The CPU page shows their "Rated base clock" and "Rated boost clock" under their own labels with the dataset version as their source; they never replace the measured Min and Max clocks, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps "AMD Ryzen 9 180" to "RYZEN 9 180", "AMD Ryzen 7 165" to "RYZEN 7 165", "AMD Ryzen 7 155" to "RYZEN 7 155" and "AMD Ryzen 5 125" to "RYZEN 5 125", and the Settings card shows the CPU count from the dataset, so it reads 138 with no code change. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.28.0, 138 CPUs and 95 GPUs, and the verified Ryzen 100 figures, plus the normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.28.0 in German, French and Spanish).
+- **Dependency audit re-attested for v2.1.60:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.59 and v2.1.60.

## v2.1.59, shipped

- **Hardware database: fourteen AMD Ryzen 200 mobile CPUs (Beyond v2.0, item 5).** The dataset grows from v1.26.0 to v1.27.0 with the whole consumer Ryzen 200 series the dataset never listed, completing the family. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, every roadmap item through v2.1.58 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. The gap itself is the finding: the consumer Ryzen AI 300, Ryzen AI Max and Ryzen AI 400 families were completed in v2.1.56, v2.1.57 and v2.1.58, and this cycle's family-by-family check found the fourteen CPU gaps below. Seven launched at CES 2025 on January 6, 2025 (Hawk Point, Zen 4 cores, RDNA 3 graphics, XDNA NPU up to 16 TOPS), with systems from Q2 2025: the Ryzen 9 270 is the 8-core flagship, the Ryzen 7 260 and 250 are the 8-core mid stack, the Ryzen 5 240, 230 and 220 are the 6-core parts, and the Ryzen 3 210 is the 4-core entry part. Seven more were added on June 2, 2026: the Ryzen 7 253 and 249, the Ryzen 5 225 and 224, the Ryzen 7 217 and Ryzen 5 216, and the Ryzen 3 205. All figures were compiled by hand from AMD's own product pages (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-15 from fresh fetches, and cross-checked against AMD's CES 2025 press release for the original seven: the Ryzen 9 270 at Base Clock 4.0 GHz and Max Boost Clock up to 5.2 GHz; the Ryzen 7 260 at 3.8 and up to 5.1 GHz; the Ryzen 7 250 at 3.3 and up to 5.1 GHz; the Ryzen 5 240 at 4.3 and up to 5.0 GHz; the Ryzen 5 230 at 3.5 and up to 4.9 GHz; the Ryzen 5 220 at 3.2 and up to 4.9 GHz; the Ryzen 3 210 at 3.0 and up to 4.7 GHz; the Ryzen 7 253 at 3.6 and up to 4.9 GHz; the Ryzen 7 249 at 3.1 and up to 4.9 GHz; the Ryzen 5 225 at 4.1 and up to 4.8 GHz; the Ryzen 5 224 at 3.3 and up to 4.8 GHz; the Ryzen 7 217 at 3.0 and up to 4.8 GHz; the Ryzen 5 216 at 2.9 and up to 4.7 GHz; the Ryzen 3 205 at 2.8 and up to 4.6 GHz. The dataset's base_ghz is AMD's headline Base Clock and boost_ghz is AMD's Max Boost Clock, per its own definition. Five of the chips are hybrid Zen 4 plus Zen 4c parts (the 220, 210, 217, 216 and 205), and AMD also lists per-core-type clocks for them (for example the 220 at Zen4 Base Clock 3.7 GHz, Zen4c Base Clock 3.0 GHz and Max Zen4c Clock up to 3.5 GHz); those are per-core-type figures, not the headline Base Clock and Max Boost Clock, so they are not the dataset's figures, following the same rule as the AI 400 chips' Max Zen5c Clock. Two naming quirks are real, not errors: the Ryzen 3 205 has six cores but only eight threads (two Zen 4 plus four Zen 4c cores), and the Ryzen 7 217 is a six-core part (two Zen 4 plus four Zen 4c cores). The deliberately-absent list was not changed in this cycle: no candidate from it was re-examined, and nothing on it was overturned. The PRO variants stay out (Ryzen 7 PRO 250, Ryzen 5 PRO 230, Ryzen 5 PRO 220, Ryzen 3 PRO 210): the dataset covers consumer parts, and no PRO chip has been listed in any cycle. The AM5 desktop G and GE parts of the AI 400 family (450G/GE, 435G/GE, launched March 2, 2026) stay open for a future cycle, as does the Ryzen 100 consumer family (the four new Hawk Point-based Ryzen 100 chips, 180/165/155/125, added July 2026). The CPU page shows their "Rated base clock" and "Rated boost clock" under their own labels with the dataset version as their source; they never replace the measured Min and Max clocks, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps "AMD Ryzen 9 270" to "RYZEN 9 270", "AMD Ryzen 7 260" to "RYZEN 7 260", "AMD Ryzen 7 250" to "RYZEN 7 250", "AMD Ryzen 5 240" to "RYZEN 5 240", "AMD Ryzen 5 230" to "RYZEN 5 230", "AMD Ryzen 5 220" to "RYZEN 5 220", "AMD Ryzen 3 210" to "RYZEN 3 210", "AMD Ryzen 7 253" to "RYZEN 7 253", "AMD Ryzen 7 249" to "RYZEN 7 249", "AMD Ryzen 5 225" to "RYZEN 5 225", "AMD Ryzen 5 224" to "RYZEN 5 224", "AMD Ryzen 7 217" to "RYZEN 7 217", "AMD Ryzen 5 216" to "RYZEN 5 216" and "AMD Ryzen 3 205" to "RYZEN 3 205", and the Settings card shows the CPU count from the dataset, so it reads 134 with no code change. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.27.0, 134 CPUs and 95 GPUs, and the verified Ryzen 200 figures, plus the normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.27.0 in German, French and Spanish).
- **Dependency audit re-attested for v2.1.59:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.58 and v2.1.59.

## v2.1.58, shipped

- **Hardware database: seven AMD Ryzen AI 400 mobile CPUs (Beyond v2.0, item 5).** The dataset grows from v1.25.0 to v1.26.0 with the whole consumer Ryzen AI 400 series the dataset never listed, completing the family. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, every roadmap item through v2.1.57 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. The gap itself is the finding: the consumer Ryzen AI 300 and Ryzen AI Max families were completed in v2.1.56 and v2.1.57, and this cycle's family-by-family check found the seven CPU gaps below. All seven launched at CES 2026 on January 5, 2026 (Gorgon Point, a Strix Point refresh: 4 nm, Zen 5 and Zen 5c cores, RDNA 3.5 graphics, XDNA 2 NPU): the Ryzen AI 9 HX 475 and HX 470 are the 12-core flagships, the Ryzen AI 9 465 is the 10-core part, and the Ryzen AI 7 450 and 445 and the Ryzen AI 5 435 and 430 round out the stack down to 4 cores. All figures were compiled by hand from AMD's own product pages (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-15 from fresh fetches: the Ryzen AI 9 HX 475 at Base Clock 2.0 GHz and Max Boost Clock up to 5.2 GHz; the Ryzen AI 9 HX 470 at 2.0 and up to 5.2 GHz; the Ryzen AI 9 465 at 2.0 and up to 5.0 GHz; the Ryzen AI 7 450 at 2.0 and up to 5.1 GHz; the Ryzen AI 7 445 at 2.0 and up to 4.6 GHz; the Ryzen AI 5 435 at 2.0 and up to 4.5 GHz; the Ryzen AI 5 430 at 2.0 and up to 4.5 GHz. The dataset's boost_ghz is AMD's Max Boost Clock, per its own definition, and the AI 300 and AI Max chips already in the dataset follow the same rule, so these seven are consistent with them. AMD also lists a separate Max Zen5c Clock for the compact cores (3.3 GHz on the HX 475, HX 470 and 465, 3.6 GHz on the 450, 3.4 GHz on the 445, 435 and 430); that is a per-core-type figure, not the headline Max Boost Clock, so it is not the dataset's boost. Pre-launch leaks had put the HX 470 at 5.25 GHz and the 450 at 5.15 GHz; AMD's final published figures are 5.2 and 5.1 GHz, and those are the figures used here. The deliberately-absent list was not changed in this cycle: no candidate from it was re-examined, and nothing on it was overturned. The Ryzen AI 7 HX 455 stays out: AMD has not officially confirmed it, and no clock figures exist for it. The Ryzen 5 439 stays out: it is a non-AI part of the wider Ryzen 400 mobile range. The Ryzen AI PRO 400 commercial variants (HX PRO 475/470, PRO 465/450/435/440) stay out: the dataset covers consumer parts, and no PRO chip has been listed in any cycle. The AM5 desktop G and GE parts of the AI 400 family (450G/GE, 435G/GE, launched March 2, 2026) stay open for a future cycle, as does the Ryzen AI 200 consumer family. The CPU page shows their "Rated base clock" and "Rated boost clock" under their own labels with the dataset version as their source; they never replace the measured Min and Max clocks, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps "AMD Ryzen AI 9 HX 475" to "RYZEN AI 9 HX 475", "AMD Ryzen AI 9 HX 470" to "RYZEN AI 9 HX 470", "AMD Ryzen AI 9 465" to "RYZEN AI 9 465", "AMD Ryzen AI 7 450" to "RYZEN AI 7 450", "AMD Ryzen AI 7 445" to "RYZEN AI 7 445", "AMD Ryzen AI 5 435" to "RYZEN AI 5 435" and "AMD Ryzen AI 5 430" to "RYZEN AI 5 430", and the Settings card shows the CPU count from the dataset, so it reads 120 with no code change. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.26.0, 120 CPUs and 95 GPUs, and the verified AI 400 figures, plus the normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.26.0 in German, French and Spanish).
- **Dependency audit re-attested for v2.1.58:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.57 and v2.1.58.

## v2.1.57, shipped

- **Hardware database: five AMD Ryzen AI Max mobile CPUs (Beyond v2.0, item 5).** The dataset grows from v1.24.0 to v1.25.0 with the whole consumer Ryzen AI Max series the dataset never listed, completing the family. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, every roadmap item through v2.1.56 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. The gap itself is the finding: the consumer Ryzen AI 300 family was completed in v2.1.56, and this cycle's family-by-family check found the five CPU gaps below. The Ryzen AI Max+ 395, Max 390 and Max 385 launched at CES 2025 (Strix Halo, 16/12/8 Zen 5 cores, Radeon 8060S/8050S/8050S), and the Ryzen AI Max+ 392 and Max+ 388 were added at CES 2026, announced January 5, 2026 as the new additions that bring Radeon 8060S graphics (40 compute units) to 12-core and 8-core parts. All figures were compiled by hand from AMD's own pages (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-15 from fresh fetches: the Ryzen AI Max+ 395 at Base Clock 3.0 GHz and Max Boost Clock up to 5.1 GHz; the Ryzen AI Max+ 392 at 3.2 and up to 5.0 GHz; the Ryzen AI Max 390 at 3.2 and up to 5.0 GHz; the Ryzen AI Max+ 388 at 3.6 and up to 5.0 GHz; the Ryzen AI Max 385 at 3.6 and up to 5.0 GHz. The dataset's boost_ghz is AMD's Max Boost Clock, per its own definition, and the AI 300 chips already in the dataset follow the same rule, so these five are consistent with them. The 392's figures come from AMD's own driver page for the chip, which carries the same spec table as the product pages; the other four come from their product pages. The deliberately-absent list was not changed in this cycle: no candidate from it was re-examined, and nothing on it was overturned. The Ryzen AI Max PRO 300 commercial variants (Max+ PRO 395, Max PRO 390/385/380) stay out: the dataset covers consumer parts, and no PRO chip has been listed in any cycle. The newer Ryzen AI Max PRO 400 series (Max+ PRO 495, announced at CES 2026) is a commercial family and also stays out. The Ryzen AI 200 and Ryzen AI 400 consumer families stay open for a future cycle. The CPU page shows their "Rated base clock" and "Rated boost clock" under their own labels with the dataset version as their source; they never replace the measured Min and Max clocks, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps "AMD Ryzen AI Max+ 395" to "RYZEN AI MAX+ 395", "AMD Ryzen AI Max+ 392" to "RYZEN AI MAX+ 392", "AMD Ryzen AI Max 390" to "RYZEN AI MAX 390", "AMD Ryzen AI Max+ 388" to "RYZEN AI MAX+ 388" and "AMD Ryzen AI Max 385" to "RYZEN AI MAX 385", and the Settings card shows the CPU count from the dataset, so it reads 113 with no code change. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.25.0, 113 CPUs and 95 GPUs, and the verified AI Max figures, plus the normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.25.0 in German, French and Spanish).
- **Dependency audit re-attested for v2.1.57:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.56 and v2.1.57.

## v2.1.56, shipped

- **Hardware database: five AMD Ryzen AI 300 mobile CPUs (Beyond v2.0, item 5).** The dataset grows from v1.23.0 to v1.24.0 with the remainder of the consumer Ryzen AI 300 series the dataset never listed, completing the family after the Ryzen AI 9 HX 370 already in it. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, every roadmap item through v2.1.55 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. The gap itself is the finding: the RTX 40-series desktop and RX 7000 desktop GPU families were completed in v2.1.55, and this cycle's family-by-family check found the five CPU gaps below. The Ryzen AI 9 HX 375 is the July 2025 addition to the series (12 cores, Radeon 890M), the Ryzen AI 9 365 is the 10-core Strix Point part, and the Ryzen AI 7 350, Ryzen AI 7 345 and Ryzen AI 5 340 are the January 2025 Krackan Point parts rounding out the stack. All figures were compiled by hand from AMD's own product pages (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-15 from fresh fetches: the Ryzen AI 9 HX 375 at Base Clock 2.0 GHz and Max Boost Clock up to 5.1 GHz; the Ryzen AI 9 365 at 2.0 and up to 5.0 GHz; the Ryzen AI 7 350 at 2.0 and up to 5.0 GHz; the Ryzen AI 7 345 at 2.0 and up to 4.6 GHz; the Ryzen AI 5 340 at 2.0 and up to 4.8 GHz. The dataset's boost_ghz is AMD's Max Boost Clock, per its own definition, and the HX 370 already in the dataset follows the same rule, so these five are consistent with it. AMD also lists a separate Max Zen5c Clock for the compact cores (3.3 GHz on the HX 375 and 365, 3.5 GHz on the 350, 3.4 GHz on the 345 and 340); that is a per-core-type figure, not the headline Max Boost Clock, so it is not the dataset's boost. The deliberately-absent list was not changed in this cycle: no candidate from it was re-examined, and nothing on it was overturned. The Ryzen AI PRO 300 commercial variants (HX PRO 370/375, PRO 360/350/340) stay out: the dataset covers consumer parts, and no PRO chip has been listed in any cycle. The newer Ryzen AI Max, Ryzen AI 200 and Ryzen AI 400 families stay open for a future cycle. The CPU page shows their "Rated base clock" and "Rated boost clock" under their own labels with the dataset version as their source; they never replace the measured Min and Max clocks, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps "AMD Ryzen AI 9 HX 375" to "RYZEN AI 9 HX 375", "AMD Ryzen AI 9 365" to "RYZEN AI 9 365", "AMD Ryzen AI 7 350" to "RYZEN AI 7 350", "AMD Ryzen AI 7 345" to "RYZEN AI 7 345" and "AMD Ryzen AI 5 340" to "RYZEN AI 5 340", and the Settings card shows the CPU count from the dataset, so it reads 108 with no code change. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.24.0, 108 CPUs and 95 GPUs, and the verified AI 300 figures, plus the normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.24.0 in German, French and Spanish).
- **Dependency audit re-attested for v2.1.56:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.55 and v2.1.56.

## v2.1.55, shipped

- **Hardware database: NVIDIA GeForce RTX 4070 SUPER and AMD Radeon RX 7600 XT desktop GPUs (Beyond v2.0, item 5).** The dataset grows from v1.22.0 to v1.23.0 with the two desktop GPUs the dataset never listed even though both families were otherwise complete. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, `dotnet list package --outdated` reports zero outdated packages on all four projects, `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages on any project, every roadmap item through v2.1.54 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. The gap itself is the finding: the 9000 X3D and 5000 desktop families were completed in v2.1.53 and v2.1.54, and this cycle's family-by-family check found the two GPU gaps below. The RTX 4070 SUPER is the missing card of the RTX 40-series desktop family (the dataset had the 4070, the 4070 Ti and the 4070 Ti SUPER but not the SUPER between the 4070 and the 4070 Ti), and the RX 7600 XT is the missing card of the RX 7000 desktop family (the dataset had the 7600 but not the XT). All figures were compiled by hand from the vendor's own pages (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-15 from fresh fetches: the GeForce RTX 4070 SUPER at Boost Clock 2.48 GHz, 12 GB GDDR6X and Total Graphics Power 220 W on NVIDIA's 4070 family page, which carries the spec table for the whole family; the Radeon RX 7600 XT at Boost Frequency up to 2755 MHz, 16 GB and Typical Board Power 190 W on AMD's own driver page for the card, with the 190 W confirmed by AMD's launch press release. The dataset's boost_ghz is NVIDIA's Boost Clock and AMD's Boost Frequency (up to), per its own definition, and the 4070 and the 7600 already in the dataset follow the same rule, so these two are consistent with them. The deliberately-absent list was not changed in this cycle: no candidate from it was re-examined, and nothing on it was overturned. The Ryzen AI 300 remainder (365/350/340), flagged by the previous cycle, was not verified in this cycle and stays open for a future one. The GPU page shows their "Rated boost clock" under its own label with the dataset version as their source; it never replaces the measured Max clock, and a model that is not listed gets no rated specs. No code change was needed: the GPU normalizer already maps "NVIDIA GeForce RTX 4070 SUPER" to "RTX 4070 SUPER" and "AMD Radeon RX 7600 XT" to "RX 7600 XT", and the Settings card shows the GPU count from the dataset, so it reads 95 with no code change. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.23.0, 103 CPUs and 95 GPUs, and the verified 4070 SUPER and 7600 XT figures, plus the normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.23.0 in German, French and Spanish).
- **Dependency audit re-attested for v2.1.55:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.54 and v2.1.55.

## v2.1.54, shipped

- **Hardware database: AMD Ryzen 5000 desktop CPUs, the remainder of the family (Beyond v2.0, item 5).** The dataset grows from v1.21.0 to v1.22.0 with the three AMD Ryzen 5000 desktop CPUs (Zen 3) the dataset never listed, the remainder of the 5000 desktop family after the 5600, 5600X, 5800X, 5800X3D, 5900X and 5950X already in it. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, `dotnet list package --outdated` reports zero outdated packages on all four projects, `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages on any project, every roadmap item through v2.1.53 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. The gap itself is the finding: the 9000 X3D family was completed in v2.1.53, so this cycle listed the whole AMD desktop family against the dataset again and found the 5000 desktop remainder missing, three of the most-installed AM4 chips. All figures were compiled by hand from AMD's own product pages (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-15 from fresh crawls: the Ryzen 7 5700X at Base Clock 3.4 GHz and Max Boost Clock up to 4.6 GHz; the Ryzen 7 5700 at 3.7 and up to 4.6 GHz; the Ryzen 5 5500 at 3.6 and up to 4.2 GHz. AMD moved these three pages to the support downloads path, so their source URLs use amd.com/en/support/downloads/drivers.html instead of the products path the 5800X entry uses. The dataset's boost_ghz is AMD's Max Boost Clock, per its own definition, and the 5800X already in the dataset follows the same rule, so these three are consistent with it. The deliberately-absent list was not changed in this cycle: no candidate from it was re-examined, and nothing on it was overturned. The CPU page shows their "Rated boost clock" under its own label with the dataset version as their source; it never replaces the measured Max clock, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps "AMD Ryzen 7 5700X" to "RYZEN 7 5700X", "AMD Ryzen 7 5700" to "RYZEN 7 5700" and "AMD Ryzen 5 5500" to "RYZEN 5 5500", and the Settings card shows the CPU count from the dataset, so it reads 103 with no code change. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.22.0, 103 CPUs and 93 GPUs, and the verified 5000 desktop figures, plus the normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.22.0 in German, French and Spanish).
- **Dependency audit re-attested for v2.1.54:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.53 and v2.1.54.

## v2.1.53, shipped

- **Hardware database: AMD Ryzen 9000 X3D desktop CPUs, the newer launches (Beyond v2.0, item 5).** The dataset grows from v1.20.0 to v1.21.0 with the two AMD Ryzen 9000 X3D desktop CPUs (Granite Ridge, Zen 5 with 3D V-Cache) that launched after the v2.1.48 X3D slice. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, `dotnet list package --outdated` reports zero outdated packages on all four projects, `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages on any project, every roadmap item through v2.1.52 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. The gap itself is the finding: the v2.1.48 slice completed the X3D family as it stood then, but the 9850X3D launched 1/29/2026 and the 9950X3D2 Dual Edition launched 4/22/2026, after that slice, so neither was in the dataset until this cycle listed the whole AMD desktop family against it again. All figures were compiled by hand from AMD's own product pages (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-15 from fresh crawls: the Ryzen 7 9850X3D at Base Clock 4.7 GHz and Max Boost Clock up to 5.6 GHz; the Ryzen 9 9950X3D2 Dual Edition at 4.3 and up to 5.6 GHz. The dataset's boost_ghz is AMD's Max Boost Clock, per its own definition, and the 9800X3D already in the dataset follows the same rule, so these two are consistent with it. The deliberately-absent list was not changed in this cycle: no candidate from it was re-examined, and nothing on it was overturned. The CPU page shows their "Rated boost clock" under its own label with the dataset version as their source; it never replaces the measured Max clock, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps "AMD Ryzen 7 9850X3D" to "RYZEN 7 9850X3D" and "AMD Ryzen 9 9950X3D2 Dual Edition" to "RYZEN 9 9950X3D2 DUAL EDITION", and the Settings card shows the CPU count from the dataset, so it reads 100 with no code change. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.21.0, 100 CPUs and 93 GPUs, and the verified newer 9000 X3D figures, plus the normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.21.0 in German, French and Spanish).
- **Dependency audit re-attested for v2.1.53:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.52 and v2.1.53.

## v2.1.52, shipped

- **Hardware database: AMD Ryzen 9000 non-X desktop CPUs (Beyond v2.0, item 5).** The dataset grows from v1.19.0 to v1.20.0 with the three AMD Ryzen 9000 non-X desktop CPUs (Granite Ridge, Zen 5), the half of the family the dataset never listed. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, `dotnet list package --outdated` reports zero outdated packages on all four projects, `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages on any project, every roadmap item through v2.1.51 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. The gap itself is the finding: every cycle re-examined the deliberately-absent list, but no 9000 non-X chip was on it, so the gap went unnoticed until this cycle listed the whole AMD desktop family against the dataset and found the missing half of the family. All figures were compiled by hand from AMD's own product pages (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-14 from fresh crawls: the Ryzen 5 9600 at Base Clock 3.8 GHz and Max Boost Clock up to 5.2 GHz; the Ryzen 7 9700F at 3.8 and up to 5.5 GHz; the Ryzen 5 9500F at 3.8 and up to 5.0 GHz. AMD lists the 9700F with Regional Availability NA; it is a desktop part with a full AMD spec page, so it is included and the dataset coverage note records the availability. No plain Ryzen 9 9900 or Ryzen 7 9700 AMD product page could be found after targeted searches (only 9900X, 9900X3D, 9700X and 9700F surfaced), so neither is added. The dataset's boost_ghz is AMD's Max Boost Clock, per its own definition, and the 9800X3D already in the dataset follows the same rule, so these three are consistent with it. The deliberately-absent list was not changed in this cycle: no candidate from it was re-examined, and nothing on it was overturned. The CPU page shows their "Rated boost clock" under its own label with the dataset version as their source; it never replaces the measured Max clock, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps "AMD Ryzen 5 9600" to "RYZEN 5 9600", and the Settings card shows the CPU count from the dataset, so it reads 98 with no code change. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.20.0, 98 CPUs and 93 GPUs, and the verified 9000 non-X figures, plus the normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.20.0 in German, French and Spanish).
- **Dependency audit re-attested for v2.1.52:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.51 and v2.1.52.

## v2.1.51, shipped

- **Hardware database: AMD Ryzen 7000 non-X desktop CPUs (Beyond v2.0, item 5).** The dataset grows from v1.18.0 to v1.19.0 with the three AMD Ryzen 7000 non-X desktop CPUs (Raphael, Zen 4), the half of the family the dataset never listed. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, `dotnet list package --outdated` reports zero outdated packages on all four projects, `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages on any project, every roadmap item through v2.1.50 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. The gap itself is the finding: every cycle re-examined the deliberately-absent list, but no 7000 non-X chip was on it, so the gap went unnoticed until this cycle listed the whole AMD desktop family against the dataset and found the missing half of the family. All figures were compiled by hand from AMD's own product pages (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-14 from fresh crawls: the Ryzen 9 7900 at Base Clock 3.7 GHz and Max Boost Clock up to 5.4 GHz; the Ryzen 7 7700 at 3.8 and up to 5.3 GHz; the Ryzen 5 7600X at 4.7 and up to 5.3 GHz. The dataset's boost_ghz is AMD's Max Boost Clock, per its own definition, and the 7800X3D already in the dataset follows the same rule, so these three are consistent with it. The deliberately-absent list was not changed in this cycle: no candidate from it was re-examined, and nothing on it was overturned. The CPU page shows their "Rated boost clock" under its own label with the dataset version as their source; it never replaces the measured Max clock, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps "AMD Ryzen 9 7900" to "RYZEN 9 7900", and the Settings card shows the CPU count from the dataset, so it reads 95 with no code change. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.19.0, 95 CPUs and 93 GPUs, and the verified 7000 non-X figures, plus the normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.19.0 in German, French and Spanish).
- **Dependency audit re-attested for v2.1.51:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.50 and v2.1.51.

## v2.1.50, shipped

- **Hardware database: AMD Ryzen 5000 X3D desktop CPU (Beyond v2.0, item 5).** The dataset grows from v1.17.0 to v1.18.0 with the AMD Ryzen 7 5800X3D desktop CPU (Vermeer, Zen 3 with 3D V-Cache), the original X3D chip the dataset never listed. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, `dotnet list package --outdated` reports zero outdated packages on all four projects, `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages on any project, every roadmap item through v2.1.49 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. The gap itself is the finding: every cycle re-examined the deliberately-absent list, but no 5000 X3D chip was on it, so the gap went unnoticed until this cycle listed the whole AMD desktop family against the dataset and found the original X3D chip missing. The figure was compiled by hand from AMD's own product page (CC0 compilation; the entry records its source URL and retrieval date), read 2026-11-14 from a fresh crawl: the Ryzen 7 5800X3D at Base Clock 3.4 GHz and Max Boost Clock up to 4.5 GHz. The dataset's boost_ghz is AMD's Max Boost Clock, per its own definition, and the 7800X3D already in the dataset follows the same rule, so this one is consistent with it. The Ryzen 7 5700X3D was examined in the same cycle and stays out, and the deliberately-absent list gains it for that reason: AMD's own product page could not be read live (the product URL returns 404 and the legacy product-ID URL redirects to a generic specifications hub), so its figures stay out even though third-party listings agree on Base Clock 3.0 GHz and Max Boost Clock up to 4.1 GHz. Nothing else on the deliberately-absent list was re-examined, and nothing on it was overturned. The CPU page shows its "Rated boost clock" under its own label with the dataset version as its source; it never replaces the measured Max clock, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps "AMD Ryzen 7 5800X3D" to "RYZEN 7 5800X3D", and the Settings card shows the CPU count from the dataset, so it reads 92 with no code change. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.18.0, 92 CPUs and 93 GPUs, and the verified 5800X3D figure, plus the normalizer case) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.18.0 in German, French and Spanish).
- **Dependency audit re-attested for v2.1.50:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.49 and v2.1.50.

## v2.1.49, shipped

- **Hardware database: AMD Ryzen 7000 X3D desktop CPUs (Beyond v2.0, item 5).** The dataset grows from v1.16.0 to v1.17.0 with the two AMD Ryzen 7000 X3D desktop CPUs (Raphael, Zen 4 with 3D V-Cache), the family the dataset never listed beyond the Ryzen 7 7800X3D. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, `dotnet list package --outdated` reports zero outdated packages on all four projects, `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages on any project, every roadmap item through v2.1.48 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. The gap itself is the finding: every cycle re-examined the deliberately-absent list, but no 7000 X3D chip was on it, so the gap went unnoticed until this cycle listed the whole AMD desktop family against the dataset and found the missing half of the family. All figures were compiled by hand from AMD's own product pages (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-14 from fresh crawls: the Ryzen 9 7900X3D at Base Clock 4.4 GHz and Max Boost Clock up to 5.6 GHz; the Ryzen 9 7950X3D at 4.2 and up to 5.7 GHz. The dataset's boost_ghz is AMD's Max Boost Clock, per its own definition, and the 7800X3D already in the dataset follows the same rule, so these two are consistent with it. The deliberately-absent list was not changed in this cycle: no candidate from it was re-examined, and nothing on it was overturned. The CPU page shows their "Rated boost clock" under its own label with the dataset version as their source; it never replaces the measured Max clock, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps "AMD Ryzen 9 7950X3D" to "RYZEN 9 7950X3D", and the Settings card shows the CPU count from the dataset, so it reads 91 with no code change. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.17.0, 91 CPUs and 93 GPUs, and the verified 7000 X3D figures, plus the normalizer case) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.17.0 in German, French and Spanish).
- **Dependency audit re-attested for v2.1.49:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.48 and v2.1.49.

## v2.1.48, shipped

- **Hardware database: AMD Ryzen 9000 X3D desktop CPUs (Beyond v2.0, item 5).** The dataset grows from v1.15.0 to v1.16.0 with the three AMD Ryzen 9000 X3D desktop CPUs (Granite Ridge, Zen 5 with 3D V-Cache), the family the dataset never listed. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, `dotnet list package --outdated` reports zero outdated packages on all four projects, `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages on any project, every roadmap item through v2.1.47 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. The gap itself is the finding: every cycle re-examined the deliberately-absent list, but no 9000 X3D chip was on it, so the gap went unnoticed until this cycle listed the whole AMD desktop family against the dataset and found the missing family. All figures were compiled by hand from AMD's own product pages (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-14 from fresh crawls: the Ryzen 7 9800X3D at Base Clock 4.7 GHz and Max Boost Clock up to 5.2 GHz; the Ryzen 9 9900X3D at 4.4 and up to 5.5 GHz; the Ryzen 9 9950X3D at 4.3 and up to 5.7 GHz. The dataset's boost_ghz is AMD's Max Boost Clock, per its own definition, and the 7800X3D already in the dataset follows the same rule, so these three are consistent with it. The deliberately-absent list was not changed in this cycle: no candidate from it was re-examined, and nothing on it was overturned. The CPU page shows their "Rated boost clock" under its own label with the dataset version as their source; it never replaces the measured Max clock, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps "AMD Ryzen 7 9800X3D" to "RYZEN 7 9800X3D", and the Settings card shows the CPU count from the dataset, so it reads 89 with no code change. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.16.0, 89 CPUs and 93 GPUs, and the verified 9000 X3D figures, plus the normalizer case) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.16.0 in German, French and Spanish).
- **Dependency audit re-attested for v2.1.48:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.47 and v2.1.48.

## v2.1.47, shipped

- **Hardware database: Intel Core Ultra 200HX mobile CPUs (Beyond v2.0, item 5).** The dataset grows from v1.14.0 to v1.15.0 with the four Intel Core Ultra 200HX mobile CPUs (Arrow Lake HX), the family the dataset never listed. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, `dotnet list package --outdated` reports zero outdated packages on all four projects, `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages on any project, every roadmap item through v2.1.46 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. The gap itself is the finding: every cycle re-examined the deliberately-absent list, but no 200HX chip was on it, so the gap went unnoticed until this cycle listed the whole Intel mobile family against the dataset and found the missing family. The first candidate, the six 13th-gen HX mobile CPUs, was abandoned after live verification: Intel's own current product pages publish Max Turbo and E-core Max Turbo for them but no Performance-core Base Frequency, so their rows cannot be completed without inference, which also corrects the v2.1.46 claim that its compare pages publish one for every HX chip. All figures were compiled by hand from Intel's own product specification comparison page (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-14 from a fresh crawl: the Ultra 9 285HX at Performance-core Base Frequency 2.8 GHz, Performance-core Max Turbo Frequency 5.5 GHz and Efficient-core Max Turbo Frequency 4.6 GHz; the Ultra 9 275HX at 2.7, 5.4 and 4.6 GHz; the Ultra 7 265HX at 2.6, 5.3 and 4.6 GHz; the Ultra 7 255HX at 2.4, 5.2 and 4.5 GHz. The headline Max Turbo Frequency equals the P-core Max Turbo for all four, so the boost figure is the same either way. The Ultra 5 245HX and 235HX stay absent: their figures could not be verified live on Intel's own page in this cycle, so they stay out even though third-party listings agree on them (245HX at 3.1, 5.1 and 4.5 GHz; 235HX at 2.9, 5.1 and 4.5 GHz). The 12th/13th-gen mobile H/P chips stay absent on their recorded reason, which this cycle did not overturn. The CPU page shows their "Rated boost clock" under its own label with the dataset version as their source; it never replaces the measured Max clock, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps "Intel(R) Core(TM) Ultra 9 285HX" to "ULTRA 9 285HX", and the Settings card shows the CPU count from the dataset, so it reads 86 with no code change. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.15.0, 86 CPUs and 93 GPUs, and the verified 200HX figures, plus the normalizer case and the 13th-gen HX and Ultra 5 200HX chips now in the deliberately-absent assertions) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.15.0 in German, French and Spanish).
- **Dependency audit re-attested for v2.1.47:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.46 and v2.1.47.

## v2.1.46, shipped

- **Hardware database: Intel Core 14th-gen HX mobile CPUs (Beyond v2.0, item 5).** The dataset grows from v1.13.0 to v1.14.0 with the five 14th-gen HX mobile CPUs, the whole family the dataset never listed. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, `dotnet list package --outdated` reports zero outdated packages on all four projects, `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages on any project, every roadmap item through v2.1.45 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. The gap itself is the finding: every cycle re-examined the deliberately-absent list, but no 14th-gen HX chip was on it, so the gap went unnoticed until this cycle listed the whole Intel mobile family against the dataset and found the missing family. All figures were compiled by hand from Intel's own product specification comparison pages (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-14 from fresh crawls: the i9-14900HX at Performance-core Base Frequency 2.2 GHz, Performance-core Max Turbo Frequency 5.8 GHz and Efficient-core Max Turbo Frequency 4.1 GHz; the i7-14700HX at 2.1, 5.5 and 3.9 GHz; the i7-14650HX at 2.2, 5.2 and 3.7 GHz; the i5-14500HX at 2.6, 4.9 and 3.5 GHz; the i5-14450HX at 2.4, 4.8 and 3.5 GHz. The headline Max Turbo Frequency equals the P-core Max Turbo for all five, so the boost figure is the same either way. The 12th/13th-gen mobile H/P chips stay absent on their recorded reason, which this cycle did not overturn: Intel publishes no per-SKU P-core base frequency for them, while its compare pages publish one for every HX chip. The GTX 1070, GTX 1080 Ti and GTX 1060 3GB stay absent on their recorded reasons, which this cycle did not overturn: a stale cached copy of NVIDIA's 10-series family page is not a live verification, and a 2026-11-14 check of the live compare page found no GTX 10 per-card table on it either, its per-card tables ending at the GTX 16 series. The CPU page shows their "Rated boost clock" under its own label with the dataset version as their source; it never replaces the measured Max clock, and a model that is not listed gets no rated specs. No code change was needed: the CPU normalizer already maps "Intel(R) Core(TM) i9-14900HX" to "I9-14900HX", and the Settings card shows the CPU count from the dataset, so it reads 82 with no code change. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.14.0, 82 CPUs and 93 GPUs, and the verified 14th-gen HX figures, plus the normalizer case and the GTX 1070 compare-page re-check in the deliberately-absent assertions) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.14.0 in German, French and Spanish).
- **Dependency audit re-attested for v2.1.46:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.45 and v2.1.46.

## v2.1.45, shipped

- **Hardware database: NVIDIA GeForce GTX 1050 Ti desktop GPU (Beyond v2.0, item 5).** The dataset grows from v1.12.0 to v1.13.0 with the GTX 1050 Ti, the last GTX 10-series desktop card the dataset never listed that a live NVIDIA page still verifies. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, `dotnet list package --outdated` reports zero outdated packages on all four projects, `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages on any project, every roadmap item through v2.1.44 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. The gap itself is the finding: every cycle re-examined the deliberately-absent list, but no GTX 1050 Ti was on it, so the gap went unnoticed until this cycle listed the whole NVIDIA family against the dataset and found the missing card. Both figures were compiled by hand from NVIDIA's own specs table on the GeForce GTX 1050 launch page (CC0 compilation; the entry records its source URL and retrieval date), read 2026-11-14 from a fresh crawl: Boost Clock 1392 MHz and Frame Buffer 4 GB GDDR5. The entry carries no board-power figure: that live NVIDIA table lists boost clock and memory but no board-power value, the same null treatment as the RX 590. The 75 W figure the card is widely listed at was not taken from any third-party table, and NVIDIA's 10-series family page that lists it could not be confirmed live, its text copy is a 2024 crawl and a 2026-11-14 check recorded the old 10-series product page as redirecting, so the power stays null rather than resting on a stale copy. The plain GTX 1050 stays absent: its device name covers the 2 GB card (1455 MHz, verified live on the same NVIDIA table) and a later 3 GB variant (1518 MHz, 3 GB), so no single boost or memory figure exists under the bare name, and the live table lists no board-power figure for it either. The GTX 1070, GTX 1080 Ti and GTX 1060 3GB stay absent on their recorded reasons, which this cycle did not overturn: a stale cached copy of NVIDIA's 10-series family page is not a live verification. The GPU page shows their "Rated boost clock", "Rated memory" and "Rated board power" under their own labels with the dataset version as their source; they never replace the measured dedicated memory or the live GPU clock, and a model that is not listed gets no rated specs. No code change was needed: the GPU normalizer already maps "NVIDIA GeForce GTX 1050 Ti" to "GTX 1050 TI", and the Settings card shows the GPU count from the dataset, so it reads 93 with no code change. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.13.0, 77 CPUs and 93 GPUs, and the verified GTX 1050 Ti figures including the null board-power case, plus the normalizer case and the GTX 1050 now in the deliberately-absent assertions) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.13.0 in German, French and Spanish).
- **Dependency audit re-attested for v2.1.45:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.44 and v2.1.45.

## v2.1.44, shipped

- **Hardware database: AMD Radeon RX 400-series desktop GPUs (Beyond v2.0, item 5).** The dataset grows from v1.11.0 to v1.12.0 with the three RX 400-series desktop GPUs (RX 480, RX 470, RX 460), the whole family the dataset never listed. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, `dotnet list package --outdated` reports zero outdated packages on all four projects, `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages on any project, every roadmap item through v2.1.43 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. The gap itself is the finding: every cycle re-examined the deliberately-absent list, but no RX 400-series desktop card was on it, so the gap went unnoticed until this cycle listed the whole AMD family against the dataset and found the missing family. Every figure was compiled by hand from AMD's own driver/spec pages (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-14, the same pages the shipped RX 5000/6000/7000/9000, Vega and RX 500 entries cite: the RX 480 at Boost Frequency up to 1266 MHz and Typical Board Power 150 W; the RX 470 at 1206 MHz, 4 GB and 120 W; the RX 460 at 1200 MHz and 70 W. The 1266 MHz figure also settles the v2.1.42 confusion for good: it is the RX 480's boost, read from AMD's own RX 480 page, not the RX 580 4 GB card's. The RX 480 and RX 460 carry no memory figure: AMD lists 8 GB and 4 GB (RX 480) and 4 GB and 2 GB (RX 460) configurations under one device name, so the entries guess neither, the same precedent as the RX 580 and RX 550. The RX 470 carries 4 GB: AMD's reference card is 4 GB and the page lists a single 4 GB figure. The GPU page shows their "Rated boost clock", "Rated memory" and "Rated board power" under their own labels with the dataset version as their source; they never replace the measured dedicated memory or the live GPU clock, and a model that is not listed gets no rated specs. No code change was needed: the GPU normalizer already maps "AMD Radeon RX 480" to "RX 480" and the other two names the same way, and the Settings card shows the GPU count from the dataset, so it reads 92 with no code change. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.12.0, 77 CPUs and 92 GPUs, and the verified RX 480 / RX 470 / RX 460 figures including the 480/460 null-memory cases, plus the three normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.12.0 in German, French and Spanish).
- **Dependency audit re-attested for v2.1.44:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.43 and v2.1.44.

## v2.1.43, shipped

- **Hardware database: AMD Radeon RX 500-series desktop GPUs (Beyond v2.0, item 5).** The dataset grows from v1.10.0 to v1.11.0 with the three verifiable RX 500-series desktop cards (RX 590, RX 580, RX 550), the part of the family the dataset never listed. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, `dotnet list package --outdated` reports zero outdated packages on all four projects, `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages on any project, every roadmap item through v2.1.42 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. The gap itself is the finding: every cycle re-examined the deliberately-absent list, but no RX 500-series desktop card was on it, so the gap went unnoticed until this cycle listed the whole AMD family against the dataset and found the missing family. The previous cycle passed the family over on a mistaken reason: it claimed the RX 580 4 GB variant carries a different boost (1266 vs 1340 MHz), but AMD's own page lists a single Boost Frequency up to 1340 MHz and a single Typical Board Power 185 W for the RX 580; 1266 MHz is the RX 480's boost, not the RX 580 4 GB card's. The real splits are elsewhere, and they stay absent: the RX 570 page lists two boost figures (Up to 1206 MHz for the OEM card, Up to 1244 MHz for retail) under one device name, and the RX 560 page lists board power as a range (60-80 W), so neither has a single verifiable figure. Every figure was compiled by hand from AMD's own driver/spec pages (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-14, the same pages the shipped RX 5000/6000/7000/9000 and Vega entries cite: the RX 590 at Boost Frequency up to 1545 MHz and 8 GB; the RX 580 at 1340 MHz and 185 W; the RX 550 at 1183 MHz and 50 W. The RX 590 carries no board-power figure: AMD publishes no Typical Board Power value for it on its page, the same null treatment as the RX 9060 XT. The RX 580 and RX 550 carry no memory figure: AMD lists 8 GB and 4 GB (RX 580) and 4 GB and 2 GB (RX 550) configurations under one device name, so the entries guess neither, the same precedent as the RX 5500 XT. The GPU page shows their "Rated boost clock", "Rated memory" and "Rated board power" under their own labels with the dataset version as their source; they never replace the measured dedicated memory or the live GPU clock, and a model that is not listed gets no rated specs. No code change was needed: the GPU normalizer already maps "AMD Radeon RX 590" to "RX 590" and the other two names the same way, and the Settings card shows the GPU count from the dataset, so it reads 89 with no code change. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.11.0, 77 CPUs and 89 GPUs, and the verified RX 590 / RX 580 / RX 550 figures including the 590 null board-power and the 580/550 null-memory cases, the RX 570 and RX 560 now in the deliberately-absent assertions, plus the three normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.11.0 in German, French and Spanish).
- **Dependency audit re-attested for v2.1.43:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.42 and v2.1.43.

## v2.1.42, shipped

- **Hardware database: AMD Radeon RX Vega series desktop GPUs (Beyond v2.0, item 5).** The dataset grows from v1.9.0 to v1.10.0 with the three Radeon RX Vega series desktop GPUs (Vega 64, Vega 56, Radeon VII), the whole family the dataset never listed. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, `dotnet list package --outdated` reports zero outdated packages on all four projects, `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages on any project, every roadmap item through v2.1.41 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. The gap itself is the finding: every cycle re-examined the deliberately-absent list, but no Vega desktop card was on it, so the gap went unnoticed until this cycle listed the whole AMD family against the dataset and found the missing family. Every figure was compiled by hand from AMD's own driver/spec pages (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-14, the same pages the shipped RX 5000/6000/7000/9000 entries cite: the RX Vega 64 at Boost Frequency up to 1546 MHz, 8 GB HBM2 and Typical Board Power 295 W; the RX Vega 56 at 1471 MHz, 8 GB and 210 W; the Radeon VII at 1750 MHz, 16 GB HBM2 and 300 W. All three are single variants with one figure each, so all three entries are complete. The RX Vega 64 Liquid Cooled Edition (1677 MHz, 345 W) is a separate device name, not a variant of the air-cooled card, so it is not folded into this entry. AMD lists the Radeon VII under the Radeon RX Vega Series, so it ships with this family even though its name carries no RX. The GPU page shows their "Rated boost clock", "Rated memory" and "Rated board power" under their own labels with the dataset version as their source; they never replace the measured dedicated memory or the live GPU clock, and a model that is not listed gets no rated specs. No code change was needed: the GPU normalizer already maps "AMD Radeon RX Vega 64" to "RX VEGA 64" and "AMD Radeon VII" to "VII", and the Settings card shows the GPU count from the dataset, so it reads 86 with no code change. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.10.0, 77 CPUs and 86 GPUs, and the verified Vega 64 / Vega 56 / Radeon VII figures, plus the two normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.10.0 in German, French and Spanish).
- **Dependency audit re-attested for v2.1.42:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.41 and v2.1.42.

## v2.1.41, shipped

- **Hardware database: AMD Radeon RX 5000-series desktop GPUs (Beyond v2.0, item 5).** The dataset grows from v1.8.0 to v1.9.0 with the five Radeon RX 5000-series desktop GPUs (5700 XT, 5700, 5600 XT, 5500 XT, 5500), the whole family the dataset never listed. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, `dotnet list package --outdated` reports zero outdated packages on all four projects, `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages on any project, every roadmap item through v2.1.40 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. The gap itself is the finding: every cycle re-examined the deliberately-absent list, but no RX 5000-series desktop card was on it, so the gap went unnoticed until this cycle listed the whole AMD family against the dataset and found the missing family. Every figure was compiled by hand from AMD's own driver/spec pages (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-14, the same pages the shipped RX 6000/7000/9000 entries cite: the RX 5700 XT at Boost Frequency up to 1905 MHz, 8 GB GDDR6 and Typical Board Power 225 W; the RX 5700 at 1725 MHz, 8 GB and 180 W; the RX 5600 XT at 1560 MHz, 6 GB and 150 W; the RX 5500 XT at 1845 MHz and 130 W; the RX 5500 at 1845 MHz, 4 GB and 150 W. The RX 5500 XT carries no memory figure: AMD lists 8 GB and 4 GB configurations under one device name, so the entry guesses neither, the same precedent as the RTX 5070 Laptop GPU and the RX 9060 XT. The RX 5500 is the OEM card of the family, but AMD's own page lists it as a desktop board with single figures, so it is recorded like the rest. The 5600 XT page lists two memory speeds (up to 12 Gbps / up to 14 Gbps, the vBIOS update), but the memory size is a single 6 GB, so the entry is complete. The GPU page shows their "Rated boost clock", "Rated memory" and "Rated board power" under their own labels with the dataset version as their source; they never replace the measured dedicated memory or the live GPU clock, and a model that is not listed gets no rated specs. No code change was needed: the GPU normalizer already maps "AMD Radeon RX 5700 XT" to "RX 5700 XT" and the other four names the same way, and the Settings card shows the GPU count from the dataset, so it reads 83 with no code change. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.9.0, 77 CPUs and 83 GPUs, and the verified 5700 XT / 5700 / 5600 XT / 5500 XT / 5500 figures including the 5500 XT null-memory case, plus the two normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.9.0 in German, French and Spanish).
- **Documentation: the em dashes are gone from the repo docs.** README.md, ROADMAP.md and SECURITY.md carried 232 em dashes in older entries; every one is now a comma or a period, with the wording otherwise unchanged, and new entries carry none. The v2.1.40 cycle never added a roadmap entry, so this version backfills it below.
- **Dependency audit re-attested for v2.1.41:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.40 and v2.1.41.

## v2.1.40, shipped

- **Hardware database: Intel Core i5-14400 / 14400F and AMD Ryzen 7 3700U (Beyond v2.0, item 5).** The dataset grows from v1.7.0 to v1.8.0 with the three CPUs the v1.0.0 deliberately-absent list named without a blocking reason, every figure verified by hand against a manufacturer page, read 2026-11-14, each entry carrying its source URL and retrieval date, 77 CPUs and 78 GPUs in total. The Core i5-14400 at base 2.5 GHz, boost 4.7 GHz and E-core boost 3.5 GHz, against Intel's own product page (ARK ID 236788), and the same figures for the i5-14400F (ARK ID 236777; Intel's ARK page returned 403 to the reader, so its figures rest on the ARK-sourced listings, which agree exactly with the 14400). The Ryzen 7 3700U at base 2.3 GHz and Max Boost Clock up to 4.0 GHz, against AMD's own driver/spec page for the chip. The i9-11980HK stays absent: Intel's own current product page lists up to 4.90 GHz while ARK-era sources list 5.00 GHz Max Turbo, so no single verifiable figure exists. The CPU page shows their "Rated boost clock" under its own label with the dataset version as their source; it never replaces the measured Max clock, and a model that is not listed gets no rated spec at all. The database is optional (Settings, takes effect after restart) and 100% local. Tests: `HardwareDatabaseTests` (the verified 14400 / 14400F / 3700U figures) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.8.0 in German, French and Spanish).
- **Dependency audit re-attested for v2.1.40:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.39 and v2.1.40; `dotnet list package --outdated` reports zero outdated packages on all four projects.

## v2.1.39, shipped

- **Hardware database: RTX 20-series desktop GPUs (Beyond v2.0, item 5).** The dataset grows from v1.6.0 to v1.7.0 with the six GeForce RTX 20-series desktop GPUs (2080 Ti, 2080 SUPER, 2080, 2070 SUPER, 2070, 2060 SUPER), the whole family the dataset never listed. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, `dotnet list package --outdated` reports zero outdated packages on all four projects, `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages on any project, every roadmap item through v2.1.38 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. The gap itself is the finding: every cycle re-examined the deliberately-absent list, but no RTX 20-series desktop card was on it, so the gap went unnoticed until this cycle listed the whole RTX family against the dataset and found the missing family. Every figure was compiled by hand from NVIDIA's own GeForce compare page (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-14, the same page the shipped 1660/1660 SUPER/1660 Ti/1650/1650 SUPER/1630 entries cite. The page's RTX 20 Series table lists, in column order 2080 Ti / 2080 SUPER / 2080 / 2070 SUPER / 2070 / 2060 SUPER / 2060: the RTX 2080 Ti at Boost Clock 1.64 GHz, 11 GB GDDR6 and Graphics Card Power 260 W; the RTX 2080 SUPER at 1.82 GHz, 8 GB GDDR6 and 250 W; the RTX 2080 at 1.80 GHz, 8 GB GDDR6 and 225 W; the RTX 2070 SUPER at 1.77 GHz, 8 GB GDDR6 and 215 W; the RTX 2070 at 1.71 GHz, 8 GB GDDR6 and 185 W; the RTX 2060 SUPER at 1.65 GHz, 8 GB GDDR6 and 175 W. All six are single variants with one figure each, so all six entries are complete. The RTX 2060 stays absent: its boost (1.65/1.68 GHz), memory (12/6 GB) and power (185/160 W) are all split between its 12 GB and 6 GB variants under one device name, with no mapping on the page. The GPU page shows their "Rated boost clock", "Rated memory" and "Rated board power" under their own labels with the dataset version as their source; they never replace the measured dedicated memory or the live GPU clock, and a model that is not listed gets no rated specs. No code change was needed: the GPU normalizer already maps "NVIDIA GeForce RTX 2080 Ti" to "RTX 2080 TI" and the other five names the same way, and the Settings card shows the GPU count from the dataset, so it reads 78 with no code change. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.7.0, 74 CPUs and 78 GPUs, and the verified 2080 Ti / 2080 SUPER / 2080 / 2070 SUPER / 2070 / 2060 SUPER figures, the RTX 2060 now in the deliberately-absent assertions, plus the two normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.7.0 in German, French and Spanish).
- **Dependency audit re-attested for v2.1.39:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.38 and v2.1.39; `dotnet list package --outdated` reports zero outdated packages on all four projects.

## v2.1.38, shipped

- **Hardware database: GTX 1650 SUPER and GTX 1630 desktop GPUs (Beyond v2.0, item 5).** The dataset grows from v1.5.0 to v1.6.0 with the two cards that complete the GTX 16-series desktop family. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, `dotnet list package --outdated` reports zero outdated packages on all four projects, `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages on any project, every roadmap item through v2.1.37 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. The gap itself is the finding: every cycle re-examined the deliberately-absent list, but neither the 1650 SUPER nor the 1630 was on it, so the gap went unnoticed until this cycle listed the whole GTX 16-series family against the dataset and found the two missing cards. Every figure was compiled by hand from NVIDIA's own GeForce compare page (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-14, the same page the shipped 1660/1660 SUPER/1660 Ti/1650 entries cite. The page's GTX 16 Series table lists, in column order 1660 Ti / 1660 SUPER / 1660 / 1650 SUPER / 1650 (G5) / 1650 (G6) / 1630: the GTX 1650 SUPER at Boost Clock 1725 MHz, 4 GB GDDR6 and Graphics Card Power 100 W; the GTX 1630 at Boost Clock 1785 MHz, 4 GB GDDR6 and 75 W. Both are single variants with one figure each, so both entries are complete, unlike the GTX 1650, whose G5 (1665 MHz) and G6 (1590 MHz) variants differ and which therefore still carries no rated boost. The GPU page shows their "Rated boost clock", "Rated memory" and "Rated board power" under their own labels with the dataset version as their source; they never replace the measured dedicated memory or the live GPU clock, and a model that is not listed gets no rated specs. No code change was needed: the GPU normalizer already maps "NVIDIA GeForce GTX 1650 SUPER" to "GTX 1650 SUPER" and "NVIDIA GeForce GTX 1630" to "GTX 1630", and the Settings card shows the GPU count from the dataset, so it reads 72 with no code change. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.6.0, 74 CPUs and 72 GPUs, and the verified 1650 SUPER / 1630 figures, plus the two normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.6.0 in German, French and Spanish).
- **Dependency audit re-attested for v2.1.38:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.37 and v2.1.38; `dotnet list package --outdated` reports zero outdated packages on all four projects.

## v2.1.37, shipped

- **Hardware database: RTX 4060 Ti and RTX 3080 desktop GPUs (Beyond v2.0, item 5).** The dataset grows from v1.4.0 to v1.5.0 with the two desktop GPUs the deliberately-absent list excluded for a split board power. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, `dotnet list package --outdated` reports zero outdated packages on all four projects, `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages on any project, every roadmap item through v2.1.36 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. The exclusion was written when board power was a required figure; v2.1.35 made it optional end to end for the RX 9060 XT, which unblocks boost-only entries, and this cycle re-examined the absent list after that change. Both cards show a rated boost clock only, following the RX 9060 XT precedent exactly. Every figure was compiled by hand from NVIDIA's own product pages (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-14: the RTX 4060 Ti from NVIDIA's 4060/4060 Ti page (Boost Clock 2.54 GHz, the single figure on the page; memory "16 GB or 8 GB" and Total Graphics Power "165 or 160" are both split with no mapping on the page, so the entry carries neither, the 160 W / 8 GB and 165 W / 16 GB mapping is corroborated by the board pages); the RTX 3080 from NVIDIA's 3080 page (Boost Clock 1.71 GHz, the 10 GB Founders Edition; the 12 GB variant shares the same 1.71 GHz boost with 350 W board power against the 10 GB card's 320 W, corroborated by the launch coverage). The GPU page shows their "Rated boost clock" under its own label with the dataset version as their source; it never replaces the measured dedicated memory or the live GPU clock, and a model that is not listed gets no rated specs. The GTX 1070 stays absent, and this cycle verified the reason instead of carrying it forward blindly: its old nvidia.com 10-series product page now redirects to the RTX 50 Series page, so no boost or power figure exists on nvidia.com, and the retailer tables that list its figures do not satisfy the manufacturer-page source policy. RTX 3050 desktop (boost differs between variants), GTX 1060 3GB (no power figure on nvidia.com), RTX 20-series and GTX 16-series laptop GPUs and all AMD laptop GPUs stay absent for the same reasons as before. The dataset's board-power definition is fixed as part of this slice: it used to say the note is null for desktop cards, which the RX 9060 XT already broke in v1.3.0, it now says the note records the split when one device name covers variants with different board power. No code change was needed: the GPU normalizer already maps "NVIDIA GeForce RTX 4060 Ti" to "RTX 4060 TI" and "NVIDIA GeForce RTX 3080" to "RTX 3080", null memory and null board power are already handled end to end, and the Settings card shows the GPU count from the dataset, so it reads 70 with no code change. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.5.0, 74 CPUs and 70 GPUs, and the verified 4060 Ti / 3080 boost-only figures including their split-power notes, the 4060 Ti and 3080 no longer in the deliberately-absent assertions, plus the two normalizer cases) and `SettingsViewModelTests` (the dataset-version seed strings updated to v1.5.0 in German, French and Spanish).
- **Dependency audit re-attested for v2.1.37:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.36 and v2.1.37; `dotnet list package --outdated` reports zero outdated packages on all four projects.

## v2.1.36, shipped

- **Hardware database: Intel Arc A-series desktop GPUs (Beyond v2.0, item 5).** The dataset grows from v1.3.0 to v1.4.0 with the four Intel Arc A-series desktop GPUs (A770, A750, A580, A380), the last Arc family the dataset was missing. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, `dotnet list package --outdated` reports zero outdated packages on all four projects, every roadmap item through v2.1.35 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. The dataset used to say no manufacturer spec page could be located for the Arc A580/A750/A770; that turned out to be wrong, and the note is removed with this slice. Every figure was compiled by hand from an Intel page (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-14: the A770 from Intel's ARK page for the 16 GB variant (Graphics Clock 2100 MHz, Total Board Power 225 W; the 8 GB variant shares the same clock and power, only the memory differs, which is why no memory figure is recorded); the A750 from Intel's ARK page (Graphics Clock 2050 MHz, 8 GB, Total Board Power 225 W, confirmed against Intel's own A750 datasheet); the A580 from Intel's own launch announcement (Graphics Clock 1700 MHz, 8 GB, Total Board Power 185 W from Intel's published launch spec table, corroborated by the launch reviews; a few secondary databases list 175 W with no Intel document behind them, so the Intel-published figure is recorded); the A380 from Intel's own A-series spec reveal plus Intel's A380 product brief (Graphics Clock 2000 MHz, 6 GB, Total Board Power 75 W; the brief's 2300 MHz / 87 W figures are the Deep Link Dynamic Power Share conditional, not the standard rating). Intel publishes a single Graphics Clock for Arc cards, recorded as the boost figure, and the dataset definition already says so. A figure with no single value for a shared device name is left out rather than guessed: the A770 carries no memory figure (Intel lists 16 GB and 8 GB configurations under one device name), the same precedent as the RTX 5070 Laptop GPU and the RX 9060 XT. The GPU page shows the rated boost clock, memory and board power under their own labels ("Rated boost clock", "Rated memory", "Rated board power") with the dataset version as their source; they never replace the measured dedicated memory or the live GPU clock, and a model that is not listed gets no rated specs. No code change was needed: the GPU normalizer already maps "Intel(R) Arc(TM) A770/A750/A580/A380 Graphics" to "ARC A770/A750/A580/A380" (it strips the Intel prefix and keeps ARC), null memory is already handled end to end, and the Settings card shows the GPU count from the dataset, so it reads 68 with no code change. Deliberately absent in v1.4.0 (unchanged from v1.3.0 except the Arc A-series, now shipped): RTX 4060 Ti and RTX 3080 desktop, RTX 3050 desktop, GTX 1060 3GB, GTX 1070, GTX 1080 Ti, RTX 20-series and GTX 16-series laptop GPUs, all AMD laptop GPUs. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.4.0, 74 CPUs and 68 GPUs, and the verified A770/A750/A580/A380 figures including the A770 null-memory case, the A750 no longer in the deliberately-absent assertions, plus the A770/A580/A380 normalizer cases).
- **Dependency audit re-attested for v2.1.36:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.35 and v2.1.36; `dotnet list package --outdated` reports zero outdated packages on all four projects.

## v2.1.35, shipped

- **Hardware database: Intel Arc B-series, RX 9060 / 9060 XT and RTX 5050 desktop GPUs (Beyond v2.0, item 5).** The dataset grows from v1.2.0 to v1.3.0 with five desktop GPUs launched after v1.1.0: Intel Arc B580 and B570, AMD Radeon RX 9060 and RX 9060 XT, and the GeForce RTX 5050. The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, `dotnet list package --outdated` reports zero outdated packages on all four projects, every roadmap item through v2.1.34 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). Plug-in loading stays gated on the code-signing decision, and the Linux port starts only if users ask for it, so neither was picked. Every figure was compiled by hand from a manufacturer page (CC0 compilation; each entry records its source URL and retrieval date), read 2026-11-14: the RTX 5050 from NVIDIA's 50-series page (Boost Clock 2.57 GHz, 8 GB, Total Graphics Power 130 W); the RX 9060 from AMD's driver/spec page (Boost Frequency up to 2.99 GHz, 8 GB, Typical Board Power 132 W); the RX 9060 XT from AMD's driver/spec page plus AMD's own how-to-sell sheet (Boost Frequency up to 3.13 GHz, the same for both memory variants); the Arc B580 and B570 from Intel's own published B-Series Graphics Quick Reference Guide (Graphics Clock 2670 MHz / 2500 MHz, 12 GB / 10 GB, Total Board Power 190 W / 150 W), intel.com returned 403 to the reader, so the guide PDF is the cited Intel source, corroborated by the launch reviews. Intel publishes a single Graphics Clock for Arc cards, recorded as the boost figure, and the dataset definition now says so. A figure with no single value for a shared device name is left out rather than guessed: the RX 9060 XT carries no memory or board-power figure (AMD lists 8 GB / 150 W and 16 GB / 160 W configurations under one device name), the same precedent as the RTX 5070 Laptop GPU. The GPU page shows the rated boost clock, memory and board power under their own labels ("Rated boost clock", "Rated memory", "Rated board power") with the dataset version as their source; they never replace the measured dedicated memory or the live GPU clock, and a model that is not listed gets no rated specs. **The board-power figure is now optional end to end, not just in the data:** the RX 9060 XT is the first entry with no board-power figure, and the collector used to render that spec unconditionally (`(double)rated.TdpW`), which would have thrown on this card. `GpuSpec.TdpW` is now `int?`, the loader accepts a missing or null `tdp_w` (a present value must still be a positive whole number of watts, and a zero is still a data bug), and the collector renders the spec only when a figure exists. The Settings card shows the GPU count from the dataset, so it reads 64 with no code change. Deliberately absent in v1.3.0 (unchanged from v1.2.0): RTX 4060 Ti and RTX 3080 desktop, RTX 3050 desktop, GTX 1060 3GB, GTX 1070, GTX 1080 Ti, RTX 20-series and GTX 16-series laptop GPUs, all AMD laptop GPUs, Intel Arc A580/A750/A770. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.3.0, 74 CPUs and 64 GPUs, and the verified B580/B570/9060/9060 XT/5050 figures including the 9060 XT null-memory/null-power case, the invalid-entry test extended so a null board power loads like a null boost, plus the B580 normalizer case) and `GpuCollectorTests` (a model with no memory and no board power shows its rated boost clock and nothing else, and the collector does not crash).
- **Dependency audit re-attested for v2.1.35:** no vulnerable packages in any project, including transitive dependencies; `dotnet list package --outdated` reports zero outdated packages on all four projects, so there are no major bumps to note this cycle.

## v2.1.34, shipped

- **Hardware database: RTX 50-series laptop GPUs (Beyond v2.0, item 5).** The dataset grows from v1.1.0 to v1.2.0 with the six GeForce RTX 50-series laptop GPUs (5090, 5080, 5070 Ti, 5070, 5060, 5050 Laptop), compiled by hand from NVIDIA's laptop compare page (nvidia.com/en-us/geforce/laptops/compare/, read 2026-11-14; CC0 compilation; each entry records its source URL and retrieval date). The slice was picked after verification that nothing else was genuinely unshipped: the repo has no open GitHub issues, `dotnet list package --outdated` reports zero outdated packages on all four projects, every roadmap item through v2.1.33 is shipped (localisation complete in German, French and Spanish; U1-U12 and Beyond v2.0 items 1-5 shipped), and the three "not implemented" Tests-page items are deliberately not in the build (the battery runtime estimate is a rejected invented number, the Wi-Fi speed test would need the network, the quick memory test needs a reboot). The GPU page shows the rated boost clock, memory and board power under their own labels ("Rated boost clock", "Rated memory", "Rated board power") with the dataset version as their source; they never replace the measured dedicated memory or the live GPU clock, and a model that is not listed gets no rated specs. Boost is the upper end of NVIDIA's published Boost Clock range and board power is the maximum of the manufacturer-published TGP range, and the range is recorded in the dataset, exactly like the RTX 40/30-series laptop entries. A figure with no single value for a shared device name is left out rather than guessed: the RTX 5070 Laptop GPU carries no memory figure (NVIDIA lists 12 GB and 8 GB configurations under one device name), the same precedent as the RTX 3060 and RTX 5060 Ti. No separate 12 GB entry was added: there is no evidence Windows reports a distinct device name for it, and inventing one would be a guess. Deliberately absent in v1.2.0 (unchanged from v1.1.0): RTX 4060 Ti and RTX 3080 desktop, RTX 3050 desktop, GTX 1060 3GB, GTX 1070, GTX 1080 Ti, RTX 20-series and GTX 16-series laptop GPUs, all AMD laptop GPUs, Intel Arc A580/A750/A770. Tests: `HardwareDatabaseTests` (bundled dataset pinned at version 1.2.0, 74 CPUs and 59 GPUs, and the verified RTX 5090/5080/5070 Ti/5070/5060/5050 Laptop figures including the 5070 null-memory case, plus the 5090 Laptop normalizer case) and the existing `GpuCollectorTests` (rated specs under their own labels, null-memory case). The Settings card shows the GPU count from the dataset, so it reads 59 with no code change.
- **Dependency audit re-attested for v2.1.34:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.33 and v2.1.34; `dotnet list package --outdated` reports zero outdated packages on all four projects.

## v2.1.33, shipped

- **Localisation, phase 7: the Comparison, Crash History, Report, and Tests pages in Spanish (Beyond v2.0, item 4):** the same four surfaces German phase 8 (v2.1.16) and French phase 7 (v2.1.26) translated, now pinned in Spanish, the Comparison page's whole chrome including the compared-pair status template, the identical-DNA lines and the empty states; the Crash History page's whole chrome including the alert card, the table headers, the cleared-log warning and the empty states; the Report page's whole chrome including the export buttons, the DNA card and the format descriptions; and the Tests page's whole chrome including the load-test card and the per-test status words, with the failure line re-resolving from the stored exception message on a switch instead of resetting. All re-render in place on a language switch, with no restart, and transient status lines re-resolve from stored state instead of resetting. The feature already worked in Spanish, the view models take a language code generically and the Spanish pack already covers the whole 523-key catalogue, but there were no Spanish-pinned tests; this version adds them, mirroring the French phase 7 tests, plus Spanish pack tests pinning all 35 Comparison, 15 Crash History, 22 Report and 42 Tests strings and Spanish template formatting with invariant numbers. No new keys and no source change was needed, except the Spanish language card note, which now says every page is translated. The data stays English by design: machine names, the comparison summary sentence, the row cell values, the DNA changed/unidentified part lines, the crash component headline, the "What this means" fallback, the crash class names, counts, dates and plain meanings, the per-dollar verdict sentence (shown verbatim in every language), the verdict text, the score explanation, the buyer banner and summary, the generated-text line, the DNA state/hash/verify result texts, the export folder path, the per-test names and workload descriptions, the measured result summaries and details, and the load-test measured results, verdict text and live values, file and folder paths, and exception messages. With this phase every page is translated in Spanish. The Spanish wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German and French wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.33:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.32 and v2.1.33.

## v2.1.32, shipped

- **Localisation, phase 6: the History, Fleet, and Buyer Check pages in Spanish (Beyond v2.0, item 4):** the same sixth surface German phase 6 and French phase 6 (v2.1.25) translated, now pinned in Spanish. Three Spanish-seeded view models prove the three pages' chrome in place and with no restart: the History trend summary, the forecast texts, the driver-age summary, the value no-verdict reasons and the asking-price row label, the sensor-log statuses, and the remove-scan messages; the Fleet summary, the row remove/export states, the import-skipped status and the empty states; and the Buyer Check approved banner, the finding banner and the skipped-checks note. A page already open when the language changes re-analyses its stored snapshots in place, so the wordings follow the language while the measured values stay identical. The feature already worked in Spanish, the view models take a language code generically and the Spanish pack already covers the whole 523-key catalogue, but there were no Spanish-pinned tests; this version adds them, mirroring the French phase 6 tests (11 History, 8 Fleet, 4 Buyer Check view-model tests), plus three Spanish pack tests pinning all 185 History, Fleet, and Buyer Check strings. All values were read from the real Spanish pack with a scratch dump program (outside the repo), never hand-translated from the German or French tests. No new keys and no source change was needed, except the Spanish language card note, which now says the navigation rail, the Settings page, the component detail pages, the Overview page, and the History, Fleet, and Buyer Check pages are translated. The data stays English by design: component names, headlines, health and status texts, spec labels, values and sources, the "What this means" text, and the per-dollar verdict sentence are measured data from the collectors, translated in a later slice. The Spanish wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German and French wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.32:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.31 and v2.1.32.

## v2.1.31, shipped

- **Localisation, phase 5: the Overview page in Spanish (Beyond v2.0, item 4):** the same fifth surface German phase 5 (v2.1.11) and French phase 5 (v2.1.24) translated, now pinned in Spanish. One Spanish-seeded view model proves the whole landing page's chrome in place and with no restart: hero labels, section headings, the two buttons, the score note, the key-stat labels, the live-strip names, and the sensor-log and alert-setting sentences. The live strip is updated by stable key, never by display name, so a switch cannot strand a metric; a metric built with the legacy (name, unit) constructor keeps its old behaviour exactly. The feature already worked in Spanish, the view model takes a language code generically and the Spanish pack already covers the whole 523-key catalogue, but there were no Spanish-pinned tests; this version adds them, mirroring the French phase 5 tests, plus a Spanish pack test pinning all 36 Overview-page strings and Spanish template formatting with invariant numbers. All values were read from the real Spanish pack with a scratch dump program (outside the repo), never hand-translated from the German or French tests. No new keys and no source change was needed, except the Spanish language card note, which now says the navigation rail, the Settings page, the component detail pages and the Overview page are translated. The data stays English by design: component names, headlines, health and status texts, spec labels, values and sources, the score explanation and the verdict text are measured data from the collectors, translated in a later slice. The Spanish wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German and French wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.31:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.30 and v2.1.31.

## v2.1.30, shipped

- **Localisation, phase 4: the component detail pages in Spanish (Beyond v2.0, item 4):** the same fourth surface German phase 4 (v2.1.10) and French phase 4 (v2.1.23) translated, now pinned in Spanish. One Spanish-seeded view model proves all 15 pages' chrome, the in-place re-render on a language switch and back, the missing-component fallbacks, the no-match search message naming the search, the estimate suffix, the age-signals chrome and skipped-checks note, and the untrusted-signal reliability text. The feature already worked in Spanish, the view model takes a language code generically and the Spanish pack already covers the whole 523-key catalogue, but there were no Spanish-pinned tests; this version adds them, mirroring the French phase 4 tests, plus a Spanish pack test pinning all 14 detail-page strings. All values were read from the real Spanish pack with a scratch dump program (outside the repo), never hand-translated from the German or French tests. No new keys and no source change was needed, except the Spanish language card note, which now says the navigation rail, the Settings page and the component detail pages are translated. The data stays English by design: component names, headlines, health and status texts, spec labels, values and sources, and the "What this means" text are measured data from the collectors, translated in a later slice. The Spanish wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German and French wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.30:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.29 and v2.1.30.

## v2.1.29, shipped

- **Localisation, phase 3: the Core diagnostic strings in Spanish (Beyond v2.0, item 4):** the same third surface German phase 3 (v2.1.9) and French phase 3 (v2.1.22) translated, now pinned in Spanish at the Core level, the plug-in list's per-item status and reason, the manifest parser's errors, and the portable-toggle result messages, including the `with { LanguageCode = "es" }` re-resolution of a toggle result. The feature already worked in Spanish, v2.1.28's Settings-level tests proved the plug-in list and toggle message render Spanish, but the Core APIs themselves had no Spanish-pinned tests; this version adds them, mirroring the French phase 3 tests, plus a Spanish pack test pinning the same 20 Core diagnostic strings and Spanish template formatting with invariant numbers. No new keys, the Spanish pack already covers the whole 523-key catalogue, and no source change was needed: the Core APIs take a language code generically. File names, manifest field names, collector ids and TRUESPEC_PORTABLE stay untranslated inside the sentences. Detail pages stay English in Spanish in this version, the Spanish language card note stays accurate. The Spanish wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German and French wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.29:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.28 and v2.1.29.

## v2.1.28, shipped

- **Localisation, phase 2: the Settings page in Spanish (Beyond v2.0, item 4):** picking Spanish now translates the whole Settings page, the same second surface German phase 2 (v2.1.8) and French phase 2 (v2.1.21) translated, header, Appearance, refresh rate (options rebuild per language, "5 segundos", still saving the number 5), sensor logging and alerts (the alert summary keeps the app's number formatting inside the Spanish sentence, "Avisar cuando la temperatura de la CPU llegue a 90 °C durante 30 segundos."), hardware database, export folder, data location (all five portable summaries, both toggle actions), plug-ins card chrome and count lines, and Privacy, right away, with no restart, and back to English the same way. The per-item plug-in status/reason and the portable-toggle result message render Spanish too: they are Core diagnostic strings that take a language code, the same mechanism German phase 3 (v2.1.9) added. No new keys, the Spanish pack already covers the whole 523-key catalogue. One wording correction from the phase-1 catalogue: the alert threshold label is "Umbral de alerta (°C)", the term Windows itself uses, not "Avisar por encima de". The other pages stay English in Spanish in this version, the Spanish language card now says the navigation rail and the Settings page are translated, and the other pages follow in later versions. The Spanish wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German and French wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.28:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.27 and v2.1.28.

## v2.1.27, shipped

- **Localisation, phase 1: the navigation rail in Spanish (Beyond v2.0, item 4):** TrueSpec now ships a fourth language. Settings has a Language picker (English / Deutsch / Français / Español); picking Spanish translates the whole navigation rail (all 22 items, using the standard terms Windows itself uses in Spanish: Almacenamiento, Pantalla, Inicio, Configuración) and the language card itself, right away, with no restart. The choice is saved in settings and survives a restart. Detail pages stay English in Spanish in this version, the Spanish language card says so honestly, and follow in later versions. The foundation is in place for them: the Spanish pack covers the whole 523-key catalogue (the coverage test pins it: a pack that misses a key fails), so later phases only translate surfaces, never add keys. The Spanish wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German and French wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.27:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.26 and v2.1.27.

## v2.1.26, shipped

- **Localisation, phase 7: the Comparison, Crash History, Report, and Tests pages in French (Beyond v2.0, item 4):** the same four surfaces German phase 8 (v2.1.16) translated, now pinned in French, the Comparison page's whole chrome including the compared-pair status template, the identical-DNA lines and the empty states; the Crash History page's whole chrome including the alert card, the table headers, the cleared-log warning and the empty states; the Report page's whole chrome including the export buttons, the DNA card and the format descriptions; and the Tests page's whole chrome including the load-test card and the per-test status words, with the failure line re-resolving from the stored exception message on a switch instead of resetting. All re-render in place on a language switch, with no restart, and transient status lines re-resolve from stored state instead of resetting. The feature already worked in French, the view models take a language code generically and the French pack already covers the whole 523-key catalogue, but there were no French-pinned tests; this version adds them, mirroring the German phase 8 tests, plus French pack tests pinning all 35 Comparison, 15 Crash History, 22 Report and 42 Tests strings and French template formatting with invariant numbers. No new keys and no source change was needed, except the French language card note, which now says every page is translated. The data stays English by design: machine names, the comparison summary sentence, the row cell values, the DNA changed/unidentified part lines, the crash component headline, the "What this means" fallback, the crash class names, counts, dates and plain meanings, the per-dollar verdict sentence (shown verbatim in every language), the verdict text, the score explanation, the buyer banner and summary, the generated-text line, the DNA state/hash/verify result texts, the export folder path, the per-test names and workload descriptions, the measured result summaries and details, and the load-test measured results, verdict text and live values, file and folder paths, and exception messages. With this phase every page is translated in French. The French wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.26:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.25 and v2.1.26.

## v2.1.25, shipped

- **Localisation, phase 6: the History, Fleet, and Buyer Check pages in French (Beyond v2.0, item 4):** the same sixth surface German phase 6 (v2.1.12) translated, plus the Fleet and New Laptop Check pages (the same two surfaces German phase 8 translated), now pinned in French, the History page's whole chrome (trend cards, saved-scans table, two-machine comparison, value comparison, CPU temperature log) and its Core-owned wordings (trend summary, forecast texts, no-verdict reason, verified against the real Core code on the exact fixtures), the Fleet page's whole chrome including the remove arm, export-all totals and skip reasons, empty states and import skipped-file line, and the New Laptop Check page's whole chrome including the finding banner, which keeps the finding title English with only the state word translating. All re-render in place on a language switch, with no restart, and transient status lines re-resolve from stored state instead of resetting. The feature already worked in French, the view models and the Core APIs take a language code generically and the French pack already covers the whole 523-key catalogue, but there were no French-pinned tests; this version adds them, mirroring the German phase 6 and phase 8 tests, plus French pack tests pinning all 96 History, 72 Fleet and 33 New Laptop Check strings and French template formatting with invariant numbers. No new keys and no source change was needed. The data stays English by design: machine names, the per-dollar verdict sentence (shown verbatim in every language), the table's "Not measurable" / "Not reported" cell fallbacks, check titles, details and what-to-ask lines, authenticity evidence lines and source strings, file and folder paths, and exception messages. Comparison, Crash History, Report and Tests stay English in French in this version. The French language card now says the navigation rail, the Settings page, the component detail pages, the Overview page, the History page, the Fleet page and the New Laptop Check page are translated, and the other pages follow in later versions. The French wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.25:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.24 and v2.1.25.

## v2.1.24, shipped

- **Localisation, phase 5: the Overview page in French (Beyond v2.0, item 4):** the same fifth surface German phase 5 (v2.1.11) translated, now pinned in French, the landing page, the first screen a user sees, renders its whole chrome in French, in place on language switch and back to English the same way, with no restart. The live strip is updated by stable key, never by display name, so a switch cannot strand a metric. The feature already worked in French, the view model takes a language code generically and the French pack already covers the whole 523-key catalogue, but there were no French-pinned tests; this version adds them, mirroring the German phase 5 tests (OverviewViewModelTests, LiveMetricViewModelTests), plus a French pack test pinning all 36 Overview-page strings and French template formatting with invariant numbers. No new keys and no source change was needed. The data stays English by design: component names, headlines, health and status texts, spec labels, values and sources, and the score explanation and verdict text are measured data, translated in a later slice. The French language card now says the navigation rail, the Settings page, the component detail pages and the Overview page are translated, and the other pages follow in later versions. The French wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.24:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.23 and v2.1.24.

## v2.1.23, shipped

- **Localisation, phase 4: the component detail pages in French (Beyond v2.0, item 4):** the same fourth surface German phase 4 (v2.1.10) translated, now pinned in French, the shared detail view all 15 component pages use renders its chrome in French, in place on language switch and back to English the same way, with no restart. The feature already worked in French, the view model takes a language code generically and the French pack already covers the whole 523-key catalogue, but there were no French-pinned tests; this version adds them, mirroring the German phase 4 tests (ComponentDetailViewModelTests), plus a French pack test pinning all 14 detail-page strings. No new keys and no source change was needed. The data stays English by design: component names, headlines, health and status texts, spec labels, values and sources, and the "What this means" text are measured data, translated in a later slice. The French language card now says the navigation rail, the Settings page and the component detail pages are translated, and the other pages follow in later versions. The French wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.23:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.22 and v2.1.23.

## v2.1.22, shipped

- **Localisation, phase 3: the Core diagnostic strings in French (Beyond v2.0, item 4):** the same third surface German phase 3 (v2.1.9) translated, now pinned in French at the Core level, the plug-in list's per-item status and reason, the manifest parser's errors, and the portable-toggle result messages, including the `with { LanguageCode = "fr" }` re-resolution of a toggle result. The feature already worked in French, v2.1.21's Settings-level tests proved the plug-in list and toggle message render French, but the Core APIs themselves had no French-pinned tests; this version adds them, mirroring the German phase 3 tests (PluginDiscoveryTests, PluginManifestTests, PortableModeTests), plus a French pack test pinning the same 20 Core diagnostic strings and French template formatting with invariant numbers. No new keys, the French pack already covers the whole 523-key catalogue, and no source change was needed: the Core APIs take a language code generically. File names, manifest field names, collector ids and TRUESPEC_PORTABLE stay untranslated inside the sentences. Detail pages stay English in French in this version, the French language card note stays accurate. The French wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.22:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.21 and v2.1.22.

## v2.1.21, shipped

- **Localisation, phase 2: the Settings page in French (Beyond v2.0, item 4):** picking French now translates the whole Settings page, the same second surface German phase 2 (v2.1.8) translated, header, Appearance, refresh rate (options rebuild per language, "5 secondes", still saving the number 5), sensor logging and alerts (the alert summary keeps the app's number formatting inside the French sentence), hardware database, export folder, data location (all five portable summaries, both toggle actions), plug-ins card chrome and count lines, and Privacy, right away, with no restart, and back to English the same way. The per-item plug-in status/reason and the portable-toggle result message render French too: they are Core diagnostic strings that take a language code, the same mechanism German phase 3 (v2.1.9) added. No new keys, the French pack already covers the whole 523-key catalogue (the coverage test pins it). One wording correction from the phase-1 catalogue: the alert threshold label is "Seuil d'alerte (°C)", the term Windows itself uses, not "Alerte au-dessus de". Detail pages stay English in French in this version, the French language card now says the navigation rail and the Settings page are translated, and the other pages follow in later versions. The French wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language. Tests: French-seeded whole-page render, pick-French-and-back re-render, French alert summary with numbers, French plug-ins (status/reason), language-switch plug-in re-discovery in French, and language-switch portable-toggle message re-resolution in French.
- **Dependency audit re-attested for v2.1.21:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.20 and v2.1.21.

## v2.1.20, shipped

- **Localisation, phase 1: the navigation rail in French (Beyond v2.0, item 4):** TrueSpec now ships a third language. Settings has a Language picker (English / Deutsch / Français); picking French translates the whole navigation rail (all 22 items, using the standard terms Windows itself uses in French: Stockage, Affichage, Démarrage, Paramètres) and the language card itself, right away, with no restart. The choice is saved in settings and survives a restart. Detail pages stay English in French in this version, the French language card says so honestly, and follow in later versions. The foundation is in place for them: the French pack covers the whole 523-key catalogue (the coverage test pins it: a pack that misses a key fails), so later phases only translate surfaces, never add keys. The French wording is the worker's own translation, not reviewed by a native speaker (the same honest limitation as the German wording). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.20:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.19 and v2.1.20.
- **Verification note (H1/H2):** the Nov 12 review's two High defects were verified against main before this slice was picked, and both are already fixed, H1 (load-test clock average dragged ~19% low by the Bus Speed sensor) and H2 (DriversCollector silently truncating at the WMI 1000-row cap) were both fixed in commit 94288de, shipped in v1.5.0, with tests (Bus Speed exclusion in SensorSnapshotTests, end-to-end Bus Speed average in LoadTestRunnerTests, 3 truncation tests in DriversCollectorTests). Nothing was re-fixed here.

## v2.1.19, shipped

- **Localisation polish: the Fleet export-all skip reasons in German.** The export-all path was the separate leftover from v2.1.18: it shares no code with import (the reasons are produced in `ExportOrchestrator`, rendered in `FleetViewModel`), and its skipped machines were still rendered from plain-English reason strings inside the German status line. A skipped machine now carries a structured `ExportAllSkipReasonKind` (no saved scan, no score, write failed) plus the write error when the write itself failed, and the Fleet page renders the reason in the page language from the kind. The machine name and the write error stay data, and a language switch re-renders the stored result instead of keeping the old language's reason. Catalogue 520 to 523 keys. Tests: German export-all with a no-score skip and a write-failed skip (German reasons, no English fragment, re-renders both ways on a language switch), reason-rendering tests in both languages, orchestrator kind assertions, and the zero-export German line updated to the translated reasons.
- **Dependency audit re-attested for v2.1.19:** no vulnerable packages in any project, including transitive dependencies; `dotnet list package --outdated` reports zero outdated packages on all four projects, so there are no major bumps to note this cycle.

## v2.1.18, shipped

- **Localisation polish: the three honest limitations from the phase 8 report.** (1) Fleet import skip reasons: the two English fragments in the import status line ("could not be imported.", "could not be read (...)") were only the visible half, every real failure reason came from the importer's English sentence. The importer now returns a structured `SnapshotImportFailureKind` (too large, empty, wrong schema version, no scan, no machine name, missing part, score not a number, not a JSON report) alongside the unchanged English `Error` text, and the Fleet page renders the reason in the page language from the kind. The skipped entries are stored structured (file name, kind, schema version, exception message) so a language switch re-renders them instead of keeping the old language's reason. (2) New Laptop Check banner: the ViewModel's mirror of Core's `StrongestFinding` ranking is gone, Core now exposes `StrongestFailingFinding()` and its private ranking delegates to it, and the ViewModel calls it, so the banner can never name a different finding than Core's own banner. No Core logic or behaviour changed; the finding title stays English data inside the translated state word. (3) Load-test live lines: the measured branches ("Temperature: 72.3 C", "Clock: 4200 MHz") and the elapsed line ("Finished in 60 s.") are templates now, so a German user mid-test sees "Temperatur: 72.3 C", "Takt: 4200 MHz" and "Abgeschlossen in 60 s."; only the measured values stay data. Catalogue 507 to 520 keys. Tests: German Fleet import with a bad file (German reason, no English fragment, re-renders both ways on a language switch), importer kind assertions plus a reason-rendering test in both languages, banner ranking parity across all failing combos, and a real 5-second German run with a scripted monitor asserting the live lines and the elapsed line.
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
