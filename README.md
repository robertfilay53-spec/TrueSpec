<img src="src/TrueSpec.App/Assets/truespec-logo.png" alt="TrueSpec logo" width="180" />

# TrueSpec

**One screen. The whole truth about a laptop or PC.**

*Developed by Rana Jawad*

TrueSpec is an open source Windows desktop tool for laptop and PC buyers. Open it and one screen shows every component, its real specs, its measured health, and an overall score. It is built for the question every buyer asks: _is this machine really new, and is it healthy?_

The product rule is simple: every number is either **measured** (with its source named) or shown as **Not reported by hardware**. TrueSpec never invents a precise number. That honesty is the product.

## What's new in v2.0

- **Machine DNA:** a SHA-256 identity fingerprint for the machine, built from the serials of its identified parts. The Report page's "Verify this machine" action re-scans and compares the fingerprint, so a buyer can prove the machine in front of them is the same one that was scanned.
- **Buyer certificate:** a tamper-evident one-page PDF (plus JSON) that records the machine's identity and health at scan time, for handing to a buyer or keeping as proof.
- **Portable buyer kit:** run TrueSpec from a USB stick with no install. See "Portable buyer kit" below.
- **History:** keeps the newest 200 entries per machine.

## What's fixed in v2.0.1

- **Wired network on virtual machines:** a Hyper-V/VMware/VirtualBox/virtio wired adapter now surfaces with its Ethernet specs and a wired buyer summary, instead of the Network screen reading Wi-Fi-only.
- **Motherboard navigation:** the Motherboard item in the left rail navigates to its detail page, and navigation during the first scan is no longer reset to Overview when the scan completes.
- **Machine DNA wording:** when the fingerprint matches and no part changed, the verification now says so, and names the unidentified part as the only reason a full verified identity match cannot be claimed. The verdict itself stays conservative: an unidentified part is never reported as a verified match.

## What's new in v2.0.3

- **Technician mode (Fleet):** one row per machine in the local snapshot store, lowest score first, with the worst component named. Carry the portable app on a USB stick, scan each machine, and every scan lands in the same store on the stick — the Fleet page is the summary of that store. The table exports as CSV.

## What's new in v2.0.4

- **Per-machine CSV export (Technician mode):** every row of the Fleet table has an Export CSV button that writes that one machine's latest scan as its own file — the fleet table's values for the machine, then one row per scored component, lowest health first. Hand the file to a buyer or keep it with the machine's records.
- **Fleet verdict text fixed:** the fleet table and its CSV now show the verdict the way the rest of the app shows it ("Used - healthy"), instead of the concatenated enum name ("UsedHealthy").

## What's new in v2.0.5

- **Honest limits: the MemTest86 direction:** TrueSpec's memory check is a quick in-OS pattern exercise during the load test, not a definitive RAM test, and the product now says so on the RAM page instead of hiding it. When the pattern check does not verify, the load-test verdict tells the user to run MemTest86 (bootable) for a definitive RAM test. No in-OS tool can replace it.
- **Dependency audit re-attested for v2.0.4:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.0.1 and v2.0.4.

## What's new in v2.0.6

- **Windows Memory Diagnostic result on the RAM page:** the "Memory test" row used to say "Not run" without ever checking. It now reads the System event log (provider Microsoft-Windows-MemoryDiagnostics-Results) and reports the newest result: "Passed on 2026-11-01" or "Failed on 2026-11-01". When the log holds no result event it says "Not run" — now a measured statement, because the log was actually read — and when the log cannot be read it says "Not reported by hardware" instead of guessing.
- **Dependency audit re-attested for v2.0.5:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.0.4 and v2.0.5.

## What's new in v2.0.7

- **Every WMI query is now pinned:** the remaining collectors (Network, Display, Storage, Security, Crash, Updates) had their queries written inline, so a property read but not selected silently came back null and the value showed as not reported even though the machine has it. Their queries are now named constants, and a test pins each SELECT list against the properties the collector actually reads — the same guard that caught the BIOS-vendor defect.
- **Export privacy note on the Report page:** an exported report is handed to a seller, and it contains this machine's hardware serials, MAC addresses, IP addresses and Wi-Fi network names. The Report page now says so before the export happens, instead of letting the buyer find out after.
- **Dependency audit re-attested for v2.0.6:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.0.5 and v2.0.6.

## What's new in v2.0.8

- **History timestamps: the latest scan is picked by parsed time, not by ISO string order:** a scan's capture time is stored as ISO with an offset, and two such strings can sort out of order across a daylight-saving change — a scan at 01:30 at -04:00 (05:30 UTC) sorts after a scan at 01:15 at -05:00 (06:15 UTC) as text, even though it is older. The Fleet page's latest scan per machine and the retention prune both picked by string order, so a machine scanned twice inside the transition hour could show its second-latest scan as latest, and the prune could evict the truly-newest scan. Both now compare the parsed timestamps, the same rule the History list already used.
- **Dependency audit re-attested for v2.0.7:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.0.6 and v2.0.7.

## What's new in v2.0.9

- **History sensor-log chart: the horizontal axis is now time, not sample count:** the chart used to plot samples by index, so two samples an hour apart (the app was closed, the machine slept) looked adjacent, and the line's shape lied about time. Samples are now positioned by their timestamps, and the line breaks where logging was not running at all — a stretch longer than 60 seconds between two measured samples, twice the maximum 30-second refresh interval — in addition to the existing break where the sensor did not report. If every reading stands alone across gaps, the History page says so plainly instead of showing a blank chart.
- **Dependency audit re-attested for v2.0.8:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.0.7 and v2.0.8.

## What's new in v2.1.0

- **Technician mode: import JSON reports into the fleet (Beyond v2.0, item 1):** the Fleet page already summarized every machine scanned with the portable app on a USB stick, but a machine scanned with the installed app had no way in. The Fleet page now has an Import JSON reports button: pick the JSON files the Report page exported from other machines, and their scans join this store and appear in the fleet table. Each file is validated first (it must parse, carry report schema version 1.0, and hold a scan with a machine name and a finite score); a file that fails is named in the status line and the rest still import, and importing the same file twice stores one scan, never a duplicate. Files over 10 MB are refused without being read.
- **Dependency audit re-attested for v2.0.9:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.0.8 and v2.0.9.

## What's new in v2.1.1

- **Portable buyer kit works out of the box:** the released portable zip shipped only the exe, so a buyer who extracted it to a USB stick ran with installed-app behaviour — scans landed in `%LocalAppData%\TrueSpec` on the seller's machine, not on the stick — until someone hand-created an empty `TrueSpec.portable` file next to the exe. There is now a switch in the app: Settings > Data location shows the folder the app keeps its data in and whether portable mode is on, and one button turns it on or off by creating or removing the marker file next to the exe. It takes effect after a restart, and scans already saved stay in the folder they were saved in. Enabling is refused for a system or protected app folder (portable mode would fall back to the default there anyway), and when the `TRUESPEC_PORTABLE` environment variable still requests portable mode after the marker is gone, the message says so instead of pretending it is off. A manual portable publish (`win-x64-portable` profile) now writes the marker into the publish folder, so copying that folder to a stick is enough.
- **Dependency audit re-attested for v2.1.0:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.0.9 and v2.1.0.

## What's new in v2.1.2

- **Technician mode: remove a machine from the fleet (Beyond v2.0, item 1):** the Fleet page could import machines but never remove one — a wrong import, or a machine the shop no longer services, stayed in the fleet forever, because the store had no delete path at all. Every row now has a Remove button: the first click arms it ("Confirm remove") and names how many saved scans would go; the second click deletes all of that machine's saved scans from this data folder and reloads the fleet. The match is the exact stored machine name, so removing "SHOP-PC" never touches "SHOP-PC-2". The status line states the store's own deleted-row count, including 0 when the scans were already gone.
- **Dependency audit re-attested for v2.1.1:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.0 and v2.1.1.

## What's new in v2.1.3

- **History: delete one saved scan (Beyond v2.0, item 1):** the Fleet page could remove a whole machine (v2.1.2), but the History table could not remove a single scan — a bad or duplicate scan stayed in the trend forever. Every row of the Saved scans table now has a Remove button: the first click arms it ("Confirm remove") and names the scan date, the second click deletes exactly that one scan from this data folder and reloads the trend, so the table, the chart and the CSV export all agree on what remains. The delete is by the scan's snapshot id, so it can never touch another scan of the same machine; the status line states the store's own deleted-row count, including 0 when the scan was already gone.
- **Dependency audit re-attested for v2.1.2:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.1 and v2.1.2.

## What's new in v2.1.4

- **Technician mode: export all machines (Beyond v2.0, item 1):** the Fleet page could export the whole table as one CSV, or one machine at a time from its row, but a technician with a dozen machines clicked Export a dozen times. The Machines card now has an "Export all machines" button: one click writes one CSV per machine into the export folder, each file exactly like the per-row export, so the files agree with the table row for row. A machine whose latest scan cannot be read, or has no score, is skipped and named in the status line with its reason, and a write failure for one machine never stops the rest. Two machines whose names sanitise to the same file name never overwrite each other — the second file gets a -2 suffix — and exporting the same fleet twice never overwrites the first run. The batch raises the same unmissable confirmation as the other exports, naming the folder and the count, with "Open folder" opening the folder the files landed in.
- **Dependency audit re-attested for v2.1.3:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.2 and v2.1.3.

## What's new in v2.1.5

- **Collector plug-ins, phase 1: versioned contract + manifest + discovery (Beyond v2.0, item 2):** the collector contract (`ICollector`) is now versioned — contract version 1, pinned by a test so it cannot drift silently — and a plug-in is described by a plugin.json manifest (id, name, version, contract version, assembly file, collectors) that is strictly validated before anything is trusted: unknown fields are rejected, the assembly must be a plain .dll file name inside the plug-in's own folder, and manifests over 64 KB are refused without being read. Settings has a new Plug-ins card that lists every plug-in folder under this run's data folder (`%LocalAppData%\TrueSpec\Plugins`, or the Plugins folder next to the exe in portable mode) with its status: Ready, needs a newer TrueSpec, manifest problem, or assembly missing. Plug-ins are discovered here but never loaded in this version: only signed plug-ins load, and plug-in signing is not set up yet. Loading is the next phase, once the signing story is decided.
- **Dependency audit re-attested for v2.1.4:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.3 and v2.1.4.

## What's new in v2.1.6

- **Linux collector parity study (Beyond v2.0, item 3):** the plan called for a study before any Linux build, and the study is done: every one of the 15 collectors was checked against a real Ubuntu 24.04 machine (the same OS the CI runners use). No collector ports as-is: 14 map partially to a Linux source (/proc, /sys, journalctl, dpkg), and Security has no Linux equivalent — Device Guard, BitLocker, Windows Defender and TPM attestation are Windows products, so a Linux security view would need a redesign, not a port. The tools a Linux build would need (smartctl, upower, dmidecode, lspci, lm-sensors) are not installed by default on Ubuntu, and hardware serials are root-only on Linux — the product serial was unreadable even to root on the study machine — so serials are not a standard-user fact the way they are on Windows. The study lives as a machine-checked catalogue in the code (`LinuxCollectorParity`), so it cannot silently drop a collector, and the full per-collector table is in the roadmap. Windows stays the shipping target; a Linux build starts only if users ask for it.
- **Dependency audit re-attested for v2.1.5:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.4 and v2.1.5.

## What's new in v2.1.7

- **Localisation, phase 1: the navigation rail in German (Beyond v2.0, item 4):** TrueSpec now ships a second language. Settings has a Language picker (English / Deutsch); picking German translates the whole navigation rail (all 22 items, using the standard terms Windows itself uses in German: Speicher, Anzeige, Autostart, Einstellungen) and the language card itself, right away, with no restart. The choice is saved in settings and survives a restart. Detail pages stay English in this version — the card says so honestly — and follow in later versions. The foundation is in place for them: a string-key catalogue, strict pack validation (a pack that misses a key or carries an unknown one fails validation), and a fallback rule that never shows a blank (a missing translation falls back to English, an unknown key shows the key). Measured values, units, numbers, dates and thresholds are never translated: localisation touches chrome only, so a measurement can never change with the language.
- **Dependency audit re-attested for v2.1.6:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.5 and v2.1.6.

## What's new in v2.1.8

- **Localisation, phase 2: the Settings page in German (Beyond v2.0, item 4):** picking German now translates the whole Settings page too — every card (Appearance, refresh rate, sensor logging and alerts, export folder, data location, plug-ins, Privacy), including the alert summary with its numbers ("Warnung, wenn die CPU-Temperatur 90 °C für 30 Sekunden erreicht.") and the plug-in count ("2 Plug-ins gefunden."). The refresh-rate and duration pickers show their values in German ("5 Sekunden"), and the whole page re-renders in the new language the moment the picker changes, with no restart. Numbers keep the app's own formatting inside a translated sentence, so a measurement can never change with the language. Two strings on the page stay English in this version and are named honestly: the plug-in list's per-item status and reason, and the message after toggling portable mode — both are diagnostic strings produced by the core plug-in and portable-mode code, which takes no language parameter yet; they follow in a later "Core strings" slice. Other detail pages are still English.
- **Dependency audit re-attested for v2.1.7:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.6 and v2.1.7.

## What's new in v2.1.9

- **Localisation, phase 3: the Core diagnostic strings in German (Beyond v2.0, item 4):** the two strings the Settings page left English in v2.1.8 now follow the language too. The plug-in list's per-item status and reason ("Bereit — noch nicht geladen", "Assembly fehlt", "Die Plug-in-Datei 'GpuZBridge.dll' fehlt in diesem Ordner.") and the message after toggling portable mode ("Der tragbare Modus ist aus. ...") render in German when German is picked, including the detailed manifest errors ("Dem Manifest fehlt die 'id', daher wurde es übersprungen."). The language code is now threaded through the plug-in discovery, manifest parser and portable-mode APIs, so the strings are resolved in the requested language at the point they are produced; picking German after the page loaded re-discovers the plug-ins in German and re-resolves a toggle message shown before the switch, with no restart. File names, manifest field names, collector ids and the TRUESPEC_PORTABLE variable name stay untranslated inside the sentences, and numbers inside a translated sentence keep the app's own formatting. Other detail pages are still English.
- **Dependency audit re-attested for v2.1.8:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.7 and v2.1.8.

## What's new in v2.1.10

- **Localisation, phase 4: the component detail pages in German (Beyond v2.0, item 4):** the shared detail view is the one surface every component page uses, so picking German now translates the chrome of all 15 detail pages at once — the "Specifications" heading ("Spezifikationen"), the search box ("Spezifikationen durchsuchen"), the copy hint, the "What this means" heading ("Was das bedeutet"), the captured line ("Messwert erfasst 2026-11-12 10:30:00"), the empty states, and the System page's age-signals group ("Alterssignale", "Die Rohwerte hinter dem Echtheitsurteil."). A page already open when the language changes re-renders in place, with no restart. The data the page shows stays English in this version and is named honestly: component names, headlines, health and status texts, spec labels, values and sources, and the "What this means" text are measured data produced by the collectors, not page chrome; translating them is a later slice. The "(ESTIMATE)" marker on a value is page chrome, so it renders as "(geschätzt)" in German. The language card's note now says Navigation, Settings and component detail pages are translated.
- **Dependency audit re-attested for v2.1.9:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.8 and v2.1.9.

## What's new in v2.1.11

- **Localisation, phase 5: the Overview page in German (Beyond v2.0, item 4):** the landing page is the first screen a user sees, so picking German now translates its whole chrome — the hero labels ("Gesamtzustand", "von 100"), the buttons ("Neuen Laptop prüfen", "Bericht exportieren"), the section headings ("Komponentenzustand", "Live-Sensoren"), the score note, the key-stat labels ("CPU-Temperatur", "Arbeitsspeicher"), the live-strip names, and the sensor-log and alert-setting sentences ("Warnung, wenn die CPU-Temperatur 90 °C für 30 Sekunden erreicht."), with no restart. The live strip is updated by stable key, never by display name, so a switch cannot strand a metric. The data the page shows stays English in this version and is named honestly: component names, headlines, health and status texts, spec labels, values and sources, the score explanation and the verdict text are measured data produced by the collectors and the scoring engine, not page chrome; translating them is a later slice. The language card's note now says Navigation, Settings, component detail pages and the Overview page are translated.
- **Dependency audit re-attested for v2.1.10:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.9 and v2.1.10.

## What's new in v2.1.12

- **Localisation, phase 6: the History page in German (Beyond v2.0, item 4):** picking German now translates the whole History page too — the trend cards ("Gesamtpunktzahl", "Akkugesundheit", "Speicherverschleiß"), the saved-scans table ("Gespeicherte Scans", "Gescannt", "Punktzahl"), the two-machine comparison ("Zwei Geräte vergleichen", "Test Laptop mit OTHER-PC verglichen.") and the value comparison ("Welches ist der bessere Kauf?", "Wert vergleichen", "Sieger pro Komponente"), plus the CPU temperature log sentences, with no restart. An armed remove confirmation re-resolves in the new language instead of resetting. The data the page shows stays English in this version and is named honestly: the trend summary and forecast texts, the value verdict sentence, and the table's "Not measurable" / "Not reported" cell fallbacks (which the CSV export agrees with row for row) are measured or Core-owned wordings, not page chrome; translating them is a later slice. The language card's note now says Navigation, Settings, component detail pages, the Overview page and the History page are translated.
- **Dependency audit re-attested for v2.1.11:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.10 and v2.1.11.
- **`.gitignore` guard for the local-only files:** the root `.gitignore` now guards the six local-only paths (`WORLD-CLASS-PLAN.md`, `LAPTOP-TEST-RESULTS-2026-11-12.md`, `REVIEW-NEXT-STEPS-2026-11-12.md`, `V1.4.1-FIX-BRIEF.md`, `logo-options/`, `release-artifacts/`), which a `git add -A` nearly committed during the v2.1.11 bump. Stage named files only, never `git add -A` in this repo.

## What's new in v2.1.13

- **Localisation, phase 7: the History page's remaining Core-owned wordings in German (Beyond v2.0, item 4):** picking German now translates the trend summary ("4 Scans von 2026-01-10 bis 2026-04-10. Punktzahl 90 bis 87 (um 3 Punkte gefallen)."), the battery-health alert ("Die Akkugesundheit ist seit dem ersten gespeicherten Scan von Normal (90 %) auf Abgenutzt (70 %) gefallen."), the driver-age summary, the forecast texts ("Akkugesundheit ist um 3 Punkte über 4 Scans (90 Tage) gefallen. Bei dieser gemessenen Rate wird Austausch-Schwelle (Akkugesundheit unter 60 %) in 23 bis 30 Monaten erreicht."), the value no-verdict reasons and the asking-price row label, with no restart. A page already open when the language changes re-analyses its stored snapshots in place, so the wordings follow the language while the measured values stay identical. The data that stays English in this version and is named honestly: the per-dollar verdict sentence ("Machine B scores 82 vs 71 and costs $120 less. On measured health per dollar, B is the better buy."), shown verbatim in every language, and the table's "Not measurable" / "Not reported" cell fallbacks, which the CSV export agrees with row for row.
- **Dependency audit re-attested for v2.1.12:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.11 and v2.1.12.

## What's new in v2.1.15

- **Hardware database: rated GPU specs from the bundled dataset (Beyond v2.0, item 5):** the dataset grows from v1.0.0 to v1.1.0 with 53 GPU entries, every figure verified by hand against NVIDIA GeForce / AMD product pages, each entry carrying its source URL and retrieval date. When your graphics card is listed, the GPU page shows its "Rated boost clock", "Rated memory" and "Rated board power" under their own labels with the dataset version as their source. They never replace the measured dedicated memory or the live GPU clock, and a model that is not listed gets no rated specs at all. For laptop GPUs the board power is the maximum of the manufacturer-published TGP range (the range is recorded in the dataset). A figure that has no single value for a shared device name is left out rather than guessed: the GTX 1650 shows no rated boost clock (G5/G6 variants), and the RTX 3060 and RTX 5060 Ti show no rated memory. Deliberately absent in v1.1.0: RTX 4060 Ti and RTX 3080 desktop, RTX 3050 desktop, GTX 1060 3GB, GTX 1070, GTX 1080 Ti, RTX 20-series and GTX 16-series laptop GPUs, all AMD laptop GPUs (AMD publishes Game Frequency, not Boost Frequency, for them), and Intel Arc A580/A750/A770 (no manufacturer spec page locatable). The database is optional (Settings, takes effect after restart) and 100% local.
- **Dependency audit re-attested for v2.1.15:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.14 and v2.1.15.

## What's new in v2.1.14

- **Hardware database: rated CPU boost clocks from a bundled dataset (Beyond v2.0, item 5):** a small, versioned, openly licensed (CC0) dataset of CPU specifications is now bundled with the app — 74 CPUs in v1.0.0, every figure verified by hand against Intel ARK / AMD product pages, each entry carrying its source URL and retrieval date. When your processor is listed, the CPU page shows its "Rated boost clock" (Intel ARK Max Turbo Frequency, AMD Max Boost Clock; the E-core figure rides along for Intel hybrid chips) under its own label with the dataset version as its source. It never replaces the measured Max clock, and a model that is not listed gets no rated spec at all. The database is optional (Settings, takes effect after restart) and 100% local. Deliberately absent in v1.0.0: the 12th/13th-gen Intel mobile H/P chips (Intel publishes no per-SKU P-core base frequency for them, so their rows cannot be completed without inference), the i3-8100 (no Turbo Boost, so no boost figure exists), i5-14400, i9-11980HK and Ryzen 7 3700U.
- **Dependency audit re-attested for v2.1.14:** no vulnerable packages in any project, including transitive dependencies; no package version changed between v2.1.13 and v2.1.14.

## Status

Real hardware access is implemented. Windows builds use CIM/WMI collectors, LibreHardwareMonitor live sensors and SATA/NVMe SMART data. There is no sample-data mode: on a platform without hardware access every component is shown as **Not available** with the reason named.

Implemented in this build:

| Area | Scope |
|---|---|
| Collectors | System, CPU, RAM, Storage, Battery, GPU, Display, Network, Motherboard, Security, Drivers, Startup, Windows Update |
| Storage | NVMe reliability counters, SATA SMART passthrough, wear, power-on hours, errors, volume breakdown (per-drive-letter total/free space, filesystem) |
| Battery | Design/full charge capacity, cycles, health bands, charge and discharge rate |
| Live sensors | LibreHardwareMonitor temperatures, fans, voltages, power, load and clocks |
| Scoring | Measured health only; unmeasurable components are excluded, never faked |
| Buyer checks | SSD wear, battery cycles, power-on hours, advertised RAM, BIOS date, Windows install date, critical errors, signed drivers |
| Reports | JSON, self-contained HTML, CSV and PDF, with source labels on every reading |
| Machine DNA | SHA-256 identity fingerprint per machine, with a comparer that flags added, removed or replaced parts |
| Buyer certificate | Tamper-evident one-page PDF + JSON export of the machine's identity and health at scan time |
| Portable mode | Run from USB with a portable data root (marker file `TrueSpec.portable` or `TRUESPEC_PORTABLE=1`); no install, no elevation |
| Compare | Compare any two saved snapshots side by side: score change, per-component health deltas, changed specs |
| History | Every scan is saved to a local SQLite history, so comparisons work across visits; battery-health alerts when health drops a band or falls fast |
| Fleet | Technician mode: one row per machine in the local store (latest score, worst component, last scan date, scan count), lowest score first, with a CSV export of the table and a per-machine CSV export on every row |
| Settings | Theme, live sensor refresh rate (1-30s), export folder, hardware-database toggle, all saved and applied immediately |
| Hardware database | Bundled CC0 dataset of rated CPU boost clocks (74 CPUs) and GPU specs (53 GPUs in v1.1.0), optional, offline; never overrides a measured value |
| Security | Offline by design, no telemetry, parameterized SQLite, allowlisted external tools only |
| Tests | Real CPU single-thread and local sequential disk read benchmarks |

Windows runtime validation requires a Windows machine with the relevant hardware. The project builds on Linux for UI development, but non-Windows builds intentionally show unavailable collectors.

## What it will show

- **Overview:** overall health score with a per-component breakdown (health, weight, points contributed), plain-English verdict, key stats, component cards, live sparklines.
- **New Laptop Check:** buyer mode with a PASS, WARNING or FAIL banner and a checklist that tells you what to ask the seller. The checklist exports as a printable HTML page via the "Print checklist" button (open the file in a browser and print from there).
- **Detail pages:** System, CPU, RAM, Storage, Battery, GPU, Display, Network, Motherboard, Security, Drivers, Startup, Windows Update.
- **Report:** one data model with working JSON, HTML and CSV exports. CSV values are safe to open in Excel: values that look like formulas are neutralised.
- **Compare:** pick any two saved scans and see the score change, every component that moved, and every specification that changed.
- **Settings:** dark and light theme, live sensor refresh rate (1-30 seconds), report export folder, hardware-database toggle. Every change saves immediately. English only in v1.

## Privacy

- 100% offline. No telemetry, no account, and no network calls in the app.
- Scans and reports stay on your machine.
- There is no update check and no startup registration. See [SECURITY.md](SECURITY.md) for the threat model.

## Build, test and install

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/). Avalonia builds on Linux, Windows and macOS, which keeps CI and local development fast. The Windows build is the shipping target; running the app on Linux is useful for UI work only (collectors report Not Available there).

```bash
git clone https://github.com/robertfilay53-spec/TrueSpec.git
cd TrueSpec

# Build the app (also builds Core and, on Windows, the collectors)
dotnet build src/TrueSpec.App -c Release

# Run the tests. The test project is multi-targeted (net10.0 and
# net10.0-windows) because the app targets net10.0-windows on Windows and
# net10.0 elsewhere; run the framework that matches the OS, or restore
# fails with NU1201 on Windows.
# Windows:
dotnet test TrueSpec.sln -c Release -f net10.0-windows
# Linux / macOS:
dotnet test TrueSpec.sln -c Release -f net10.0

# Run the desktop app
dotnet run --project src/TrueSpec.App -c Release
```

### Install on Windows (from a release build)

1. Build the app in Release mode on Windows:
   ```powershell
   dotnet publish src/TrueSpec.App/TrueSpec.App.csproj -p:PublishProfile=win-x64 -o publish
   ```
   This uses `src/TrueSpec.App/Properties/PublishProfiles/win-x64.pubxml`, which publishes self-contained and single-file: the .NET Desktop Runtime 10 is bundled inside `TrueSpec.App.exe`, so an end user can double-click it on a machine with no .NET installed. The installer ships the publish folder as-is and installs no runtime.
2. Build the installer with [Inno Setup 6](https://jrsoftware.org/isdl.php):
   ```
   iscc installer\TrueSpec.iss
   ```
   This produces `installer\Output\TrueSpecSetup-<version>.exe`. The script only installs the published files, creates a Start Menu shortcut and an optional desktop shortcut, and never writes to the registry beyond the standard uninstall entry. It does not require administrator rights; run it elevated only if you want every sensor (some SMART and driver data needs elevation).
3. Run the installer. Reports are stored under `Documents\TrueSpec` (or the folder you pick in Settings); settings, snapshot history and the startup log live under `%LocalAppData%\TrueSpec`. Requires Windows 10 version 1809 or later, or Windows 11, x64.

### Portable buyer kit (USB stick, no install)

For checking a laptop in a shop before paying, without installing anything on the seller's machine:

```powershell
dotnet publish src/TrueSpec.App/TrueSpec.App.csproj -p:PublishProfile=win-x64-portable -o publish-portable
```

This uses `src/TrueSpec.App/Properties/PublishProfiles/win-x64-portable.pubxml`, which publishes the same self-contained, single-file build as the installer profile: the .NET Desktop Runtime 10 is bundled inside `TrueSpec.App.exe`, so it runs on a machine with no .NET installed. Copy the whole `publish-portable` folder to a USB stick and double-click `TrueSpec.App.exe` on the machine you want to check. It never asks for elevation (asInvoker): a standard-user scan works, and parts that need elevation read as "Not reported by hardware" instead of failing the scan. The publish folder already contains the empty `TrueSpec.portable` marker file, so the stick works as a buyer kit with no extra step. The same switch exists in the app: Settings > Data location shows the folder the app keeps its data in and turns portable mode on or off by creating or removing the marker next to the exe (takes effect after a restart; scans already saved stay where they were saved). The released portable zip now includes the marker file too, so a downloaded zip works as a buyer kit with no extra step (verified in v2.1.3: the zip ships `TrueSpec.App.exe` + `TrueSpec.portable`).

Two honest limits, read before trusting a stick:

- **Portable data lives next to the exe.** Put an empty file named `TrueSpec.portable` next to `TrueSpec.App.exe` (or set the environment variable `TRUESPEC_PORTABLE=1`) and the app keeps its data beside the exe instead of under `%LocalAppData%\TrueSpec`: `settings.json`, `history.db`, `sensor-log.csv`, `startup.log` and `window-state.json`, all in the same folder as the exe. Without the marker file the app behaves exactly as before and writes under `%LocalAppData%\TrueSpec`. If the exe folder fails validation (a system folder, Program Files), the app falls back to `%LocalAppData%\TrueSpec` instead of writing somewhere unsafe. The marker is only ever looked for next to the exe, never in the working directory.
- **One stick's history mixes machines.** History is keyed by machine name (a renamable string), not by machine identity, so the history of one machine must never be read as proof about a different machine. The Report page's "Verify this machine" action compares the machine in front of you against the snapshot on screen; that is the check to use at the point of sale, not an old history file.

### Tests

Tests are xUnit in `tests/TrueSpec.Tests`. They cover scoring, buyer checks, collectors, report exporters (JSON/HTML/CSV), the snapshot store, snapshot comparison, settings sanitization and the security validators. They run in CI on Ubuntu and Windows. On Linux, `dotnet test` compiles but the test host cannot connect in this environment (a VM limit, not a code failure); the build step above is the local check.

### Dependency audit

Before each release, check for known vulnerabilities in every package:

```bash
dotnet list package --vulnerable
```

Fix or pin anything it reports. The app has no network calls and no telemetry, so the dependency surface is the main thing to watch. See [SECURITY.md](SECURITY.md).

## Project layout

```
TrueSpec.sln
src/
  TrueSpec.App/                  Avalonia UI, MVVM shell, views, theme, controls
  TrueSpec.Core/                 models, scoring, health rules, buyer checks, reports, benchmarks,
                                 settings, snapshot comparison, security validators
  TrueSpec.Collectors.Windows/   CIM/WMI, SMART, event log and LibreHardwareMonitor collectors
tests/
  TrueSpec.Tests/  xUnit tests for the core
installer/
  TrueSpec.iss     Inno Setup installer script (build on Windows only)
.github/
  workflows/       CI: build and test on Ubuntu and Windows
```

## Contributing

Contributions are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) first. In short: keep the app offline, keep the numbers honest, write English only, and add tests for core changes.

- [Open an issue](https://github.com/robertfilay53-spec/TrueSpec/issues)
- [Read the contribution guide](CONTRIBUTING.md)

## License

MIT. See [LICENSE](LICENSE).

TrueSpec is independent and not affiliated with HWiNFO, Speccy, AIDA64, Intel, AMD, Microsoft, or any hardware vendor.
