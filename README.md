# TrueSpec

**One screen. The whole truth about a laptop or PC.**

TrueSpec is an open source Windows desktop tool for laptop and PC buyers. Open it and one screen shows every component, its real specs, its measured health, and an overall score. It is built for the question every buyer asks: _is this machine really new, and is it healthy?_

The product rule is simple: every number is either **measured** (with its source named), **estimated** (clearly labeled ESTIMATE), or shown as **Not reported by hardware**. TrueSpec never invents a precise number. That honesty is the product.

## Status

Real hardware access is implemented. Windows builds use CIM/WMI collectors, LibreHardwareMonitor live sensors and SATA/NVMe SMART data. There is no sample-data mode: on a platform without hardware access every component is shown as **Not available** with the reason named.

Implemented in this build:

| Area | Scope |
|---|---|
| Collectors | System, CPU, RAM, Storage, Battery, GPU, Display, Network, Motherboard, Security, Drivers |
| Storage | NVMe reliability counters, SATA SMART passthrough, wear, power-on hours, errors |
| Battery | Design/full charge capacity, cycles, health bands, charge and discharge rate |
| Live sensors | LibreHardwareMonitor temperatures, fans, voltages, power, load and clocks |
| Scoring | Measured health only; unmeasurable components are excluded, never faked |
| Buyer checks | SSD wear, battery cycles, power-on hours, advertised RAM, BIOS date, Windows install date, critical errors, signed drivers |
| Reports | JSON and self-contained HTML, with source labels on every reading |
| Tests | Real CPU single-thread and local sequential disk read benchmarks |

Windows runtime validation requires a Windows machine with the relevant hardware. The project builds on Linux for UI development, but non-Windows builds intentionally show unavailable collectors.

## What it will show

- **Overview:** overall health score, plain-English verdict, key stats, component cards, live sparklines.
- **New Laptop Check:** buyer mode with a PASS, WARNING or FAIL banner and a checklist that tells you what to ask the seller.
- **Detail pages:** System, CPU, RAM, Storage, Battery, GPU, Display, Network, Motherboard, Security.
- **Report:** one data model with working JSON and HTML exports. PDF is not implemented.
- **Settings:** dark and light theme. Live sensors refresh every 2 seconds. English only in v1.

## Privacy

- 100% offline. No telemetry, no account, and no network calls in the app.
- Scans and reports stay on your machine.
- Update checks, when added, only read the public GitHub releases page and never install without consent.

## Build

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/). Avalonia builds on Linux, Windows and macOS, which keeps CI and local development fast.

```bash
git clone https://github.com/robertfilay53-spec/TrueSpec.git
cd TrueSpec

dotnet restore TrueSpec.sln
dotnet build TrueSpec.sln
dotnet test TrueSpec.sln

# Run the desktop app
dotnet run --project src/TrueSpec.App
```

The Windows build is the shipping target. Running the app on Linux is useful for UI work.

## Project layout

```
TrueSpec.sln
src/
  TrueSpec.App/                  Avalonia UI, MVVM shell, views, theme, controls
  TrueSpec.Core/                 models, scoring, health rules, buyer checks, reports, benchmarks
  TrueSpec.Collectors.Windows/   CIM/WMI, SMART, event log and LibreHardwareMonitor collectors
tests/
  TrueSpec.Tests/  xUnit tests for the core
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
