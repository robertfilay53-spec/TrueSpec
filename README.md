# TrueSpec

**One screen. The whole truth about a laptop or PC.**

TrueSpec is an open source Windows desktop tool for laptop and PC buyers. Open it and one screen shows every component, its real specs, its measured health, and an overall score. It is built for the question every buyer asks: _is this machine really new, and is it healthy?_

The product rule is simple: every number is either **measured** (with its source named), **estimated** (clearly labeled ESTIMATE), or shown as **Not reported by hardware**. TrueSpec never invents a precise number. That honesty is the product.

## Status

P0 foundation is complete. The application shell, theme, navigation, scoring skeleton, snapshot store skeleton, report model, and stub collectors are in place. Every reading you see today is realistic sample data. Real CIM/WMI hardware access starts in P1.

Planned phases:

| Phase | Scope |
|---|---|
| P0 | Foundation: Avalonia shell, MVVM, theme, scoring and report skeletons, CI, public repo |
| P1 | Real CIM/WMI collectors |
| P2 | Storage: SMART, wear, TBW, power-on hours |
| P3 | Battery: capacities, cycles, health |
| P4 | Live sensors via LibreHardwareMonitor |
| P5 | Network, display, peripherals |
| P6 | Scoring and buyer checks on real data |
| P7 | PDF and HTML report export |
| P8 | Built-in tests |
| P9 | Installer and release |

## What it will show

- **Overview:** overall health score, plain-English verdict, key stats, component cards, live sparklines.
- **New Laptop Check:** buyer mode with a PASS, WARNING or FAIL banner and a checklist that tells you what to ask the seller.
- **Detail pages:** System, CPU, RAM, Storage, Battery, GPU, Display, Network, Motherboard, Security.
- **Report:** one data model, JSON today, HTML and PDF planned.
- **Settings:** dark and light theme, refresh rate, startup, update channel. English only in v1.

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
  TrueSpec.App/    Avalonia UI, MVVM shell, views, theme, controls
  TrueSpec.Core/   collectors, models, scoring, snapshots, reports
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
