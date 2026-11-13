<img src="src/TrueSpec.App/Assets/truespec-logo.png" alt="TrueSpec logo" width="180" />

# TrueSpec

**One screen. The whole truth about a laptop or PC.**

*Developed by Rana Jawad*

TrueSpec is an open source Windows desktop tool for laptop and PC buyers. Open it and one screen shows every component, its real specs, its measured health, and an overall score. It is built for the question every buyer asks: _is this machine really new, and is it healthy?_

The product rule is simple: every number is either **measured** (with its source named) or shown as **Not reported by hardware**. TrueSpec never invents a precise number. That honesty is the product.

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
| Compare | Compare any two saved snapshots side by side: score change, per-component health deltas, changed specs |
| History | Every scan is saved to a local SQLite history, so comparisons work across visits; battery-health alerts when health drops a band or falls fast |
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
