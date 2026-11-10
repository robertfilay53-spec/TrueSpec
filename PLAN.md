# PC Inspector — Plan (v1)

**Name:** TrueSpec — FINALIZED Nov 10 (Jawad).
**Project dir:** `~/workspace/production/pc-inspector/`
**Date:** Nov 10, 2026
**Status:** PLAN APPROVED IN PRINCIPLE — scope expanded Nov 10: NOT just v1. Build the most powerful system-info tool in the world, better than HWiNFO/Speccy/AIDA64, and OPEN SOURCE on GitHub (public repo, MIT license). P0 build starts now.
**UI:** English only. Design FINALIZED in `UI-DESIGN.md` ("Command Center" dashboard: left rail nav, hero score ring + verdict, component cards, live sparkline strip, dark default theme).
**Roles:** Ziggy = ARCHITECT (research, plan, QA). OpenCode DeepSeek (`opencode/deepseek-v4.1-flash-free`) = EMPLOYEE (writes code only).

## What it is

A Windows desktop tool for laptop/PC buyers. Download, install, open it, and ONE screen shows the full truth about the machine: every component, its real specs, its measured health, and an overall score. No manual commands, no single-test tools, no guessing whether a "new" laptop is actually new.

Buyer mode is the core idea: it checks the machine against "sold as new" expectations and flags anything that does not match (used SSD, cycled battery, wrong RAM size, old BIOS, old Windows install date).

## Research findings (Nov 10, 2026)

### Data sources that are real

- **CIM/WMI via .NET System.Management** (`Get-CimInstance`): `Win32_Processor`, `Win32_PhysicalMemory` (per stick: manufacturer, part number, serial, speed), `Win32_BaseBoard`, `Win32_BIOS`, `Win32_DiskDrive`, `Win32_LogicalDisk`, `Win32_NetworkAdapter`, `Win32_VideoController`, `Win32_Battery`, `Win32_PnPSignedDriver`, `Win32_TPM`, `Win32_ComputerSystem`, `WmiMonitorID` (display EDID: panel maker, model, serial, manufacture week/year).
- **WMIC is DEAD.** Microsoft removed WMIC in Windows 11 25H2. Never use it. Use CIM / .NET only.
- **Disk health (real):** `Get-PhysicalDisk | Get-StorageReliabilityCounter` gives Wear %, PowerOnHours, Temperature, error counters. NVMe exposes Percentage Used (endurance used, can exceed 100). SATA needs raw SMART passthrough (`IOCTL_SMART` / DeviceIoControl) like CrystalDiskInfo does. Windows Settings drive-health panel is NVMe-only. TBW: NVMe Data Units Written; SATA SMART attributes 241/242. Typical 1 TB consumer NVMe is rated ~600 TBW.
- **Battery health (real):** `powercfg /batteryreport` or WMI `root\WMI` battery classes. Health % = Full Charge Capacity / Design Capacity x 100. Cycle count when firmware exposes it. Bands: 80-100% normal, 60-79% noticeable wear, below 60% replace.
- **WiFi (real):** `Get-NetAdapter` (model, link speed, MAC, driver version/date), `netsh wlan show drivers` (vendor, radio types 802.11ax/be = WiFi 6/6E/7, WPA3 support), `netsh wlan show interfaces` (signal, band, channel, rates). Bluetooth: `Win32_PnPEntity` / BthEnum.
- **Sensors (real, needs a library):** .NET cannot read CPU/GPU temperatures natively. Use **LibreHardwareMonitor** (open source, MIT) for temps, fan speeds, voltages, clock speeds. No kernel driver to write.
- **RAM testing (real):** Windows Memory Diagnostic (`mdsched`) or MemTest86+ (bootable). WMI reports memory errors from the event log.

### What is IMPOSSIBLE (honest limits — never fake these numbers)

| Asked for | Truth |
|---|---|
| RAM "life %" / "health %" | No sensor exists on any RAM stick. No tool on earth measures it. What exists: SPD data (maker, part no, serial, speed, timings), slot map, error counts, and a memory test result. The tool shows those + test result, and labels life as "Not measurable". |
| CPU / motherboard "life %" | No sensor exists. Health = temperatures, throttling flags, error logs, age (BIOS date / install date). No % is invented. |
| WiFi card "manufacture date" | Not exposed by any card or driver. What exists: driver date, driver version, vendor, model, radio standards, link speed, signal. The tool shows driver date and labels hardware mfg date "Not reported by hardware". |
| Exact "years of life left" for any part | Nobody can know this. SSD wear % and battery health % are real; converting them to years is an estimate and must be labeled "estimate", never a promise. |
| SSD exact failure date | Wear % is a manufacturer endurance estimate, not a death date. A drive can work past 100% used. |

**Rule for the whole product:** every number is either measured (with source named), estimated (labeled ESTIMATE), or shown as "Not reported by hardware". Never a fake precise number. This honesty IS the product — buyers trust it because it never lies.

### Competitors (what exists, where the gap is)

- **HWiNFO** — most complete sensors + SMART, but overwhelming, no buyer verdict, no single score.
- **Speccy** — readable summary, no health scoring, no buyer checks.
- **CrystalDiskInfo** — disks only.
- **CPU-Z / GPU-Z** — CPU/RAM/GPU detail only.
- **AIDA64** — paid, encyclopedic, not buyer-focused.
- **Gap:** no tool gives ONE screen with a buyer verdict ("is this laptop really new?"), an overall health score, per-part ratings, and an exportable report. That is this product.

## Tech stack (decided)

- **Language/framework:** C# + **Avalonia UI on .NET 10 LTS** (supported until Nov 2028). XAML like WPF, modern Fluent look, and it BUILDS on this Linux VM (WPF cannot build on Linux at all — WPF would force every build through Windows CI only, which slows every iteration to a crawl). Same C# codebase, same WMI/CIM access, ships as a native Windows app.
- **Why not WPF:** WPF is Windows-only at build time. Our build machine is Linux. Every code change would need a Windows runner to verify. Avalonia removes that bottleneck and the UI design transfers directly.
- **Why not WinUI 3:** Microsoft's recommended UI, but MSIX packaging + code-signing friction and heavier agent-written codebase. Avalonia is the pragmatic pick.
- **Why not Tauri/Electron:** Tauri is small but Rust + system access needs sidecars; Electron is 150 MB+ bloat for a utility.
- **Sensors:** LibreHardwareMonitor (MIT) via NuGet.
- **SMART:** `Get-StorageReliabilityCounter` first; raw SMART passthrough (P/Invoke `DeviceIoControl`) for SATA drives that Windows does not cover.
- **Installer:** Inno Setup (free). Self-contained single-file EXE inside.
- **Signing (Jawad decision, payment):** code-signing certificate costs ~$200-300/year. Without it, Windows SmartScreen warns on first install. v1 ships unsigned (with a clear note) OR signed if Jawad approves the cert cost. This is a payment decision, so it waits for him.
- **Target:** Windows 11 first, Windows 10 supported. x64 only for v1.
- **Privacy:** 100% offline. No telemetry, no account, no internet needed (except optional WiFi speed test, off by default). This is a selling point.
- **Distribution:** GitHub Releases + own site. Built-in update check (reads GitHub releases, no auto-install without consent).

## Open source (decided Nov 10)

- **Public GitHub repo** under robertfilay53-spec, name `TrueSpec` (working name). MIT license — permissive, most forks, same license as LibreHardwareMonitor which we depend on.
- **Repo contents from day one:** README (what it does, screenshots, how to build, how to contribute), CONTRIBUTING, MIT LICENSE, issue templates, GitHub Actions CI (build + test on every push), signed releases via GitHub Releases.
- **Rules:** no secrets in the repo, no telemetry, no account. Privacy is a feature and part of the README.
- **Core is free forever.** No paywalled features in the open repo. Later income options (decide after traction): GitHub Sponsors, donations, optional hosted report-sharing service. No income promised.

## Architecture (built for the full vision, not just v1)

- **Plugin collectors:** every data source is a collector implementing one interface (`ICollector`: name, data, refresh interval, admin-required flag). New hardware support = new collector, no core changes.
- **Snapshot store:** local SQLite database. Every scan saves a snapshot. History view compares snapshots over time (degradation tracking, alerts on health drops).
- **Scoring engine:** pluggable rules (component scorers + weights). Every score shows its inputs. No black box.
- **Report engine:** one data model, three outputs (PDF/HTML/JSON).
- **Benchmark engine (v2):** CPU/disk/RAM speed tests + local comparison database.
- **Update system:** checks GitHub Releases, downloads only with consent.
- **Offline first:** everything works with no internet. Internet is used only for update checks, WiFi speed test, and warranty lookup (all opt-in).

## Features (full vision — build in phases, design once)

### Dashboard (the one screen that matters)
- Overall health score 0-100 + verdict: New / Like new / Used - healthy / Worn / Replace soon.
- Per-component cards with a rating where measurable, "Not measurable" where not.
- Buyer-mode flags: SSD wear > 0% on a "new" machine, battery cycles > 5, RAM size mismatch vs advertised, BIOS date old, Windows install date old, drive power-on hours > 0.

### Detail tabs
1. System (maker, model, serial, BIOS/UEFI, TPM, Secure Boot, BitLocker, activation, install date, uptime)
2. CPU (model, cores/threads, clocks, cache, temps, throttling)
3. RAM (per-stick SPD, slots used/free, total, test result, error counts)
4. Storage (per drive: model, interface, capacity, SMART, wear %, TBW, power-on hours, temp, health word)
5. Battery (design vs full capacity, health %, wear %, cycles, chemistry, maker)
6. GPU (model, VRAM, driver, temps)
7. Display (panel maker/model/serial, mfg date, resolution, refresh rate, HDR)
8. Network (WiFi: vendor, model, standards, driver date/version, link speed, signal, band/channel; Ethernet; Bluetooth)
9. Motherboard/BIOS (maker, model, chipset, BIOS version/date)
10. Peripherals (USB devices, audio, webcam, printers)
11. OS & Security (edition, build, activation, TPM version, Secure Boot, BitLocker, drivers age flags)
12. Report (export PDF / HTML / JSON, one click, shareable)

### Tests (built in, one click each)
- Quick RAM test (schedules Windows Memory Diagnostic, explains reboot)
- Disk read-speed benchmark
- Battery estimate (runtime at current wear)
- WiFi speed test (optional, needs internet, off by default)
- Stress test (v2): CPU/GPU load + temp monitoring to catch overheating / ex-mining GPUs

### v2 and later (designed in from day one, built after v1 ships)
- Portable version (run from USB, no install — for checking a laptop in a shop)
- History/snapshots + degradation alerts
- Advertised-vs-actual comparison mode
- Fake-part detection (SSD serial/model database, refurbished flags)
- Benchmarks vs same-model averages
- Warranty lookup via serial (Dell/HP/Lenovo)
- Price estimate from specs + health
- Phone companion via QR (view report on phone)
- AI plain-language summary ("should I buy this?")

## Scoring engine (rules, not vibes)

- Overall score = weighted average of measurable components only. Components with no sensor do not drag the score down and do not fake it up.
- Weights v1: Storage 25%, Battery 25% (laptops; 0% on desktops, redistributed), RAM 15% (errors + test, not "life"), CPU 15% (temps/throttling), GPU 10%, Network 10% (driver age + link capability).
- Each component score shows its inputs (e.g. Battery 82% = 41,000 / 50,000 mWh). Clicking a score shows the math. No black box.

## Build phases (OpenCode DeepSeek builds, Ziggy reviews + QA)

- **P0 — Foundation (STARTING NOW):** Avalonia UI .NET 10 solution, MVVM structure, navigation shell per `UI-DESIGN.md` (left rail, hero, cards, live strip, status bar), dark/light theme, plugin collector interfaces, SQLite snapshot store skeleton, scoring engine skeleton, report model skeleton, CI workflow, MIT LICENSE, README, CONTRIBUTING, public GitHub repo `TrueSpec`. Build must pass. No real collectors yet (stubs with sample data so the UI is viewable).
- **P1 — WMI collector:** all CIM classes above, one collector service, "Not reported" handling, unit tests on fake data.
- **P2 — Storage:** reliability counters + NVMe SMART + SATA passthrough + TBW + wear % + health words.
- **P3 — Battery:** capacities, cycles, health %, wear %, bands.
- **P4 — Sensors:** LibreHardwareMonitor integration, live refresh (2s), min/max.
- **P5 — Network/Display/Peripherals:** WiFi/Ethernet/Bluetooth, EDID, USB/audio/webcam.
- **P6 — Scoring + buyer checks:** engine per rules above, verdicts, mismatch flags, New Laptop Check view.
- **P7 — Report export:** PDF/HTML/JSON one-click export.
- **P8 — Tests:** RAM test launcher, disk benchmark, battery estimate, WiFi speed test.
- **P9 — Installer + release:** Inno Setup, GitHub Release v1.0, update check.
- **P10 — QA:** Ziggy reviews every phase. Real-hardware testing needs a Windows machine (the VM cannot test real sensors) — Jawad's laptop is the test machine, or a test laptop if he has one.
- **P11+ — v2:** portable version, history/alerts, advertised-vs-actual, fake-part detection, benchmarks, stress test, warranty lookup, price estimate, phone companion.

## Monetization (decided: open source, core free forever)

- Core free forever under MIT. Later options after traction: GitHub Sponsors, donations, optional hosted report-sharing. No income promised, no paywalled features in the repo.

## Maximum coverage — sab kuch jo dikhaya ja sakta hai (Nov 10)

Teen layers hain: (a) static specs — hamesha available; (b) live sensors — LibreHardwareMonitor se; (c) history/tests — waqt ya reboot test se.

1. **System:** maker, model, serial, UUID, SKU, chassis type, family
2. **BIOS/UEFI:** vendor, version, release date, SMBIOS version, Secure Boot state, boot mode
3. **CPU:** name, cores/threads, base/boost clock, cache L1/L2/L3, socket, stepping, virtualization (VT-x/AMD-V), live temp, voltage, power draw, per-core usage/clocks
4. **RAM:** per-stick maker, part number, serial, capacity, speed, DDR type, timings (CL), voltage, slots used/free, form factor, rank, ECC, error counts, memory test result
5. **Storage:** per-drive model, serial, firmware, interface (NVMe/SATA/USB), capacity, partitions, file system, free space, full SMART attributes, wear %, TBW, power-on hours, power cycles, temp, health word, TRIM support, sector size
6. **GPU:** model, vendor, VRAM, driver version/date, clocks, temp, fan, utilization, DirectX support, outputs
7. **Display:** panel maker/model/serial, manufacture date (week/year), size, resolution, refresh rate, HDR, color depth, EDID raw
8. **Battery:** chemistry, maker, design vs full capacity, health %, wear %, cycles, voltage, charge rate, estimated runtime
9. **Network:** WiFi (vendor, model, standards 802.11ax/be, driver date/version, link speed, signal, band, channel, MAC, IP, DNS, gateway), Ethernet (same), Bluetooth version, VPN adapters, data usage
10. **Motherboard:** maker, model, chipset, serial, PCIe/M.2/RAM slots
11. **Audio/USB/Peripherals:** audio devices + codecs, USB device tree + port speeds, webcam (resolution), printers, keyboard/mouse
12. **OS:** edition, version, build, install date, activation, uptime, last boot, language, timezone, Windows Update status/history, Defender + firewall state
13. **Security:** TPM version, Secure Boot, BitLocker, virtualization-based security, UAC
14. **Drivers:** full list with versions/dates, unsigned drivers, very old drivers flagged
15. **Software:** installed programs, startup programs, background processes, services
16. **Live performance:** CPU/GPU/RAM/disk/network usage graphs, temps, fan speeds, power draw, battery drain rate
17. **Event logs:** critical errors, BSOD history, disk errors, memory errors
18. **Power:** power plan, sleep states, hibernate, fast startup

### Kis tarah dikhayein (presentation)
- Dashboard: overall score + verdict, per-component cards with gauges
- Live sensor graphs (line charts), tables with search/filter, tree view (Device Manager style)
- History: snapshots save karo, months mein degradation dikhao, health girne par alert
- Buyer-mode checklist: har check pass/fail
- Advertised vs actual: seller ne jo kaha wo type karo, tool khud compare kare
- Plain-language alerts: "Battery 62% par hai, jald replace karo" (technical jargon nahi)
- Search bar: koi bhi spec search karo; har value copy kar sakte ho
- Export: PDF/HTML/JSON report, ek click
- English only UI; dark/light theme

### Improvements roadmap (competitors se behtar kaise)
- **v1:** buyer verdict + score + plain language + one screen + report (yehi gap hai)
- **v1.1:** portable version (USB se bina install chalao, shop mein laptop check karne ke liye best), history/snapshots, advertised-vs-actual mode
- **v2:** built-in benchmarks (CPU/disk/RAM speed + same-model average se comparison), stress test (overheating / used mining GPU pakdo), fake-part detection (SSD serial/model database, refurbished flags), battery calibration guide, driver update links
- **Later:** warranty lookup via serial (Dell/HP/Lenovo), price estimate (specs + health se fair value), opt-in anonymous stats (apna hardware same model ke average se compare karo), phone companion (QR scan karke report phone par dekho), AI summary ("kya ye laptop lena chahiye?")

## Open decisions for Jawad

1. **Name:** TrueSpec — FINALIZED Nov 10 (Jawad).
2. **Code signing later:** ~$200-300/year cert, or ship unsigned v1 with SmartScreen warning. (Payment decision, not now.)
3. **Test machine:** which Windows laptop/PC do we QA on?
