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
| Settings | Theme, live sensor refresh rate (1-30s), export folder, all saved and applied immediately |
| Security | Offline by design, no telemetry, parameterized SQLite, allowlisted external tools only |
| Tests | Real CPU single-thread and local sequential disk read benchmarks |

Windows runtime validation requires a Windows machine with the relevant hardware. The project builds on Linux for UI development, but non-Windows builds intentionally show unavailable collectors.

## What it will show

- **Overview:** overall health score with a per-component breakdown (health, weight, points contributed), plain-English verdict, key stats, component cards, live sparklines.
- **New Laptop Check:** buyer mode with a PASS, WARNING or FAIL banner and a checklist that tells you what to ask the seller. The checklist exports as a printable HTML page via the "Print checklist" button (open the file in a browser and print from there).
- **Detail pages:** System, CPU, RAM, Storage, Battery, GPU, Display, Network, Motherboard, Security, Drivers, Startup, Windows Update.
- **Report:** one data model with working JSON, HTML and CSV exports. CSV values are safe to open in Excel: values that look like formulas are neutralised.
- **Compare:** pick any two saved scans and see the score change, every component that moved, and every specification that changed.
- **Settings:** dark and light theme, live sensor refresh rate (1-30 seconds), report export folder. Every change saves immediately. English only in v1.

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

This uses `src/TrueSpec.App/Properties/PublishProfiles/win-x64-portable.pubxml`, which publishes the same self-contained, single-file build as the installer profile: the .NET Desktop Runtime 10 is bundled inside `TrueSpec.App.exe`, so it runs on a machine with no .NET installed. Copy the whole `publish-portable` folder to a USB stick and double-click `TrueSpec.App.exe` on the machine you want to check. It never asks for elevation (asInvoker): a standard-user scan works, and parts that need elevation read as "Not reported by hardware" instead of failing the scan.

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
