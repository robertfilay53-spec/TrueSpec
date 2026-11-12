# TrueSpec World-Class Plan

**Status:** research + planning only. No code changed for this document.
**Date:** 2026-11-12. **Baseline:** TrueSpec v1.1.0 shipped (5702c9e).
**Author:** research subagent for Ziggy (architect) and Rana Jawad.
**Goal:** make TrueSpec the most powerful system-info tool in the world, so a user can
delete the paid tools and use TrueSpec instead, for $0, offline, forever.

**The one rule that governs this whole plan:** every number is measured with its
source named, or the app says "Not reported by hardware". A feature that cannot meet
that rule does not ship, however useful it sounds. Where TrueSpec cannot honestly
replace a specialist tool, the plan says so plainly and tells the user which tool to
use instead. That honesty is the product, and it is also the moat: no competitor
leads with it.

---

## 1. Vision: what "most powerful in the world" means

Most system-info tools answer one question: "what is inside this machine?"
TrueSpec answers a harder, more valuable question: **"can I trust this machine,
is it healthy, and what will it cost me?"**

That means four things no competitor does together:

1. **A verdict, not a data dump.** One screen, one score, one PASS / WARNING / FAIL
   banner, in plain English. HWiNFO shows 400 sensors and leaves the user to
   interpret them. AIDA64 shows 50 pages of inventory. TrueSpec decides, and shows
   its working.
2. **Buyer truth.** The used-laptop market runs on trust and the buyer has none.
   TrueSpec proves, with cross-checked measurements, whether a machine is really
   new, really the spec on the listing, and really healthy. No other tool is built
   for the moment money changes hands.
3. **Health over time, not a snapshot.** A single scan can be faked by a fresh
   Windows install. A history of scans cannot. TrueSpec keeps every scan locally
   and turns them into trends, alerts, and honest forecasts.
4. **Free, offline, open.** No licence, no trial that stops working, no account, no
   telemetry, no ads, no upsell. The whole paid stack it replaces costs about
   $189 a year (see section 8). TrueSpec costs $0, and the source is MIT-licensed
   so anyone can verify the numbers are real.

Success looks like this: a buyer walks into a shop with TrueSpec on a USB stick,
runs one scan, and knows in two minutes whether to pay. A technician replaces
AIDA64 + HWiNFO + CrystalDiskInfo + Hard Disk Sentinel with one tool. A home user
never pays for a "PC health" subscription again.

---

## 2. What TrueSpec is today (v1.1.0 baseline)

From README.md and ROADMAP.md, shipped and verified:

| Area | What exists |
|---|---|
| Collectors | System, CPU, RAM, Storage, Battery, GPU, Display, Network, Motherboard, Security, Drivers, Startup, Windows Update |
| Storage | NVMe reliability counters, SATA SMART passthrough, wear, power-on hours, errors, volume breakdown (total/free/filesystem per drive letter) |
| Battery | Design/full charge capacity, cycles, health bands (Normal 80-100%, Worn 60-79%, Replace below 60%), charge/discharge rate |
| Live sensors | LibreHardwareMonitor temperatures, fans, voltages, power, load, clocks; sparklines; refresh 1-30s |
| Scoring | Measured health only; unmeasurable components excluded, never faked |
| Buyer checks | SSD wear, battery cycles, power-on hours, advertised RAM, BIOS date, Windows install date, critical errors, signed drivers; New Laptop Check with PASS / WARNING / FAIL |
| Reports | JSON, HTML, CSV, hand-written PDF (no dependency), source labels on every reading |
| Compare | Any two saved snapshots side by side: score change, per-component deltas, changed specs |
| History | Every scan in local SQLite; score, battery-health and storage-wear trends; battery-health alerts (band drop, fast fall over 5 points) |
| Benchmarks | Real CPU single-thread and local sequential disk-read benchmarks |
| Security | 100% offline, no telemetry, parameterized SQLite, allowlisted external tools only, asInvoker (never asks for elevation) |
| Platform | Windows 11 first, Windows 10 1809+, x64; C# + Avalonia UI on .NET 10 LTS |

In flight at the time of writing: v1.2 (driver-age summary on History, history CSV
export, multi-machine history) and a hardening pass. This plan builds on v1.2, it
does not duplicate it.

---

## 3. Competitive analysis

Prices checked November 2026 from vendor pages and current reviews. Prices move;
check before quoting. Where sources disagree, the vendor page wins and the
disagreement is noted.

### 3.1 The tools, one by one

**HWiNFO (REALiX) — free for personal use; Pro Personal $29 first year**
The gold standard for live sensors. Deepest sensor coverage of any free tool:
temperatures, voltages, fan speeds, clocks, power draw, SSD wear, memory timings,
throttling flags, logging, alerts, and (Pro) remote monitoring and command-line
reporting. Actively maintained (v8.60, November 2026).
Strengths: sensor depth, accuracy, logging, trusted by technicians and vendors.
Weaknesses: the data is a firehose. Hundreds of rows with no verdict, no health
score, no history view, no buyer mode. The interface is functional but dated.
Commercial use requires a Pro licence (Personal $29/yr for 1 user / 5 PCs,
Corporate $44, Engineer $232; lifetime from $199).
User complaints: "overwhelming amount of information", "too much detail for a
non-technical user", basic charts.
Source: hwinfo.com/licenses, ghacks.net, rankite.com best-pc-diagnostic-software.

**AIDA64 Extreme (FinalWire) — $65.95 for 1 year, up to 3 home PCs**
The paid all-rounder, in the market for three decades. Hardware detection engine
widely called unrivalled, 50+ pages of hardware info plus 50+ pages of installed
software, licenses, security and Windows settings. System Stability Test (CPU,
memory, GPU, storage stress with live graphs), benchmarks (CPU, memory, cache,
GPGPU), sensor panel / LCD output, audit-grade reports. Engineer edition adds
command-line and remote monitoring and commercial use; Business adds network audit,
SQL database, Active Directory, change management (Business $199.90 for 10 clients
with 1-year maintenance; Network Audit $99.95 for 10 clients; Engineer $199.90 for
1 engineer — FinalWire v4.30 pricing via NikkTech; reseller listings vary).
Strengths: detection depth, stability test, benchmarks, reports, audit.
Weaknesses: **no free version at all**. 30-day trial hides values as TRIAL VERSION
and stops working after 30 days until a key is entered. Interface described as
outdated and cluttered. Support reported slow. Home-use licence only; businesses
must buy the pricier editions.
Source: winningpc.com (Nov 2026), exedepot.com, techspot.com, nikktech.com,
saashub.com compare pages.

**Speccy (Piriform / Gen Digital) — free; Professional $19.95/year**
The friendly face of system info. Clean summary page (OS, CPU, RAM, motherboard,
GPU, monitors, storage) with live temperatures, and deep dives per category.
100% of core detection is free; Pro adds automatic updates, scheduled scans,
premium support and no ads. Current build v1.34.84 (June 2026).
Strengths: readability. The best tool for a non-technical user who wants "what is
in this PC" in one glance.
Weaknesses: no stress test, no SMART detail beyond basics, no logging over time,
no alerts, no health score, no history. Reports are whole-system dumps with no
per-component report. Updates are infrequent compared with HWiNFO.
Source: tutorialpedia.org, codegenes.net, filehorse.com, rankite.com.

**CPU-Z (CPUID) — free**
Instant, detailed CPU and motherboard identification, memory timings, cache,
validation and online result sharing. The fastest answer to "what CPU and RAM is
this really".
Strengths: speed, accuracy, tiny footprint, trusted validation database.
Weaknesses: CPU and memory only. No health, no sensors beyond basics, no battery,
no storage, no report a buyer can hand to a seller.
Source: rankite.com best-pc-health-check-software; CPUID product pages.

**GPU-Z (TechPowerUp) — free**
GPU-only equivalent of CPU-Z: exact GPU model, BIOS version, VRAM, clocks,
sensors, and a BIOS database. The standard tool for spotting a fake or
re-flashed graphics card.
Weaknesses: GPU only, no whole-system view, no verdict, no history.

**CrystalDiskInfo (Crystal Dew World) — free, MIT open source**
The standard drive-health checker. Reads SMART for SATA, NVMe and most USB
drives, colour-coded Good / Caution / Bad, temperature, power-on hours, total
writes, NVMe wear and remaining life, tray alerts and email alerts.
Strengths: free including commercial use, focused, light, honest about limits.
Weaknesses: drives only. Needs administrator rights. Many USB enclosures block
SMART passthrough, so external drives often show nothing. Cannot predict sudden
failure: a 2007 Google study found 36% of failed drives showed zero SMART
warnings before failure. No whole-system view, no history of the machine.
Source: mdrepairs.com, codegenes.net, softwr.com.

**Belarc Advisor (Belarc) — free for personal use only**
System audit in a browser report: hardware, installed software with versions and
install dates, software licence keys, missing Microsoft hotfixes, antivirus
status, security vulnerabilities. Its licence-key recovery is the differentiator.
Strengths: software audit and licence inventory no free tool matches; no data
leaves the machine.
Weaknesses: personal use only — commercial use needs the paid BelManage product.
No real-time monitoring, no sensors, no health score, no history. Report is a
static HTML page.
Source: codegenes.net, filehorse.com, appmus.com.

**Open Hardware Monitor — free, open source; effectively unmaintained**
Real-time sensor dashboard with CSV logging. Last release 0.9.6, December 2020,
so support for the newest platforms is limited. The idea is right; the project
is stale. TrueSpec already uses its successor library, LibreHardwareMonitor,
for live sensors.
Source: rankite.com, axiobench.com.

**HWMonitor (CPUID) — free; HWMonitor PRO Standard EUR 19.95, Extended EUR 34.95**
The quickest full-system sensor tree: current / min / max for every temperature,
voltage and fan. PRO adds remote monitoring, graph generation and tray sensors.
Weaknesses: no alerts in the free version, no logging, interface unchanged for
years, updates less frequent than HWiNFO.
Source: cpuid.com HWMonitor PRO registration page, techdows.com, rankite.com.

**OCCT (OCBASE) — free Personal; paid from $5/month**
The leading stability-test tool: CPU, GPU, memory, VRAM and power-supply stress
tests with safety thresholds that abort automatically, plus embedded HWiNFO
monitoring and a Stability Certificate (paid). Free Personal has a 1-hour test
limit (since v7.0); Personal licence $5/month or $25/year unlocks unlimited
duration and certificates; Pro $150/year, Enterprise $499/year.
Strengths: the best short stability test available, with automatic abort.
Weaknesses: free tier is personal-use only and time-limited; certificates and
unlimited runs are paid.
Source: techspot.com, ocbase.com, downloadcrew.com, betanews.com.

**Argus Monitor (Argotronic) — 30-day trial, then 1 year $16.45, 3 years $35.95**
Fan control done properly: per-fan curves tied to any temperature source (CPU,
GPU, drives, motherboard), plus monitoring and drive-health prediction.
Strengths: the most polished fan-curve editor, broad controller support.
Weaknesses: paid after the trial. Fan control almost never works on laptops, and
depends on a supported monitoring chip. No overclocking, no voltage control.
Source: itechguides.com (facts checked 20 Sep 2026), rankite.com,
filecroco.com.

**Hard Disk Sentinel (H.D.S. Hungary) — Standard $22/PC lifetime, Professional $33/PC lifetime**
Turns raw SMART into a health percentage with plain-language explanations,
tracks temperature and transfer speed, alerts by sound, message or email.
Professional adds surface tests, disk repair, scheduled and on-failure backups.
Strengths: the most sensitive health rating in the category, catches degradation
other tools miss, lifetime licence.
Weaknesses: drives only. The health percentage is the vendor's own assessment,
not a manufacturer score.
Source: exedepot.com, codegenes.net, techjockey.com.

**PassMark BurnInTest — Standard $59, Professional $99; MemTest86 Pro $116 download**
BurnInTest is the standard PC-manufacturer burn-in: CPU, RAM, disk, GPU, sound,
network and peripherals under load with a pass/fail certificate. MemTest86 is the
industry baseline for RAM faults and boots from USB outside the OS; Free edition
exists, Pro is $116 per the official compare page (older listings show $44;
the vendor page is current).
Strengths: definitive, certifiable, trusted by manufacturers and repair depots.
Weaknesses: paid, and MemTest86 needs a USB boot and a reboot — it is a
procedure, not a quick check.
Source: findalternatives.net, memtest86.com/compare.html and pricing.html.

**PassMark PerformanceTest ($39 one-time), Geekbench 7 Pro ($99), 3DMark ($34.99),
Novabench Plus ($36/year), Cinebench 2026 (free)**
The benchmark shelf. PerformanceTest has the largest baseline database
(millions of submitted results since 1998). Geekbench is the cross-platform
standard. 3DMark is the GPU standard. All are single-purpose: they measure
performance, not health, and none of them tells a buyer whether the machine is
sound.
Source: rankite.com best-benchmarking-software.

**BatteryMon (PassMark) — free for personal use; BatteryInfoView (NirSoft) — free**
BatteryMon graphs charge/discharge rate in real time, logs over time, and
identifies the critical discharge point. BatteryInfoView shows design/full
capacity, cycle count and health at a glance. Both are single-purpose battery
utilities with no whole-system view.
Source: geckoandfly.com, saashub.com.

**PC-Doctor Toolbox — from $19.99**
Hardware diagnostics for consumers and technicians: component tests with
pass/fail. Paid, narrow, and little-known outside repair shops.
Source: itechfaqs.com.

### 3.2 Feature comparison: TrueSpec vs the field

Legend: Y = yes, P = partial, N = no. "TrueSpec v1.1" is the shipped baseline;
"Plan" means this document proposes it. Prices are the current vendor prices
from section 3.1.

| Capability | TrueSpec v1.1 | HWiNFO free | AIDA64 Extreme $65.95/yr | Speccy free / Pro $19.95 | CrystalDiskInfo free | Belarc free personal | Hard Disk Sentinel Pro $33 | OCCT free Personal |
|---|---|---|---|---|---|---|---|---|
| Whole-system spec inventory | Y | Y | Y | Y | N (drives only) | Y | N (drives only) | N |
| Live sensors (temp, fan, voltage, power, clocks) | Y | Y | Y | P (temps only) | P (drive temp) | N | P (drive temp) | Y |
| Sensor logging + threshold alerts | N | Y | Y | N | P (alerts only) | N | Y | P |
| Health score with per-component breakdown | Y | N | N | N | N | N | P (drive % only) | N |
| Buyer verdict (PASS / WARNING / FAIL) | Y | N | N | N | N | N | N | N |
| Authenticity cross-checks (new vs refurbished) | P | N | N | N | N | N | N | N |
| Battery health + cycles + history alerts | Y | P | P | P | N | N | N | N |
| SSD wear + power-on hours + NVMe counters | Y | Y | Y | P | Y | N | Y | N |
| History / trends across scans | Y | N | N | N | N | N | P (drive only) | N |
| Compare two machines side by side | Y (snapshots) | N | N | N | N | N | N | N |
| Stress / stability test | P (short benchmarks) | N | Y | N | N | N | P (surface test) | Y |
| Crash / WHEA forensics | P (critical errors listed) | N | N | N | N | N | N | N |
| Software audit (installed programs, licences) | N | N | Y | N | N | Y | N | N |
| Windows Update / patch status | Y | N | P (audit) | N | N | Y (missing hotfixes) | N | N |
| Reports (JSON / HTML / CSV / PDF) | Y | Y (report) | Y | Y (dump) | N | Y (HTML) | Y | Y (paid cert) |
| Fan control | N (read-only by design) | N | N | N | N | N | N | N |
| Price | $0 | $0 personal; Pro $29/yr | $65.95/yr | $0; Pro $19.95/yr | $0 | $0 personal | $33 lifetime | $0 personal; paid from $5/mo |
| Licence for commercial use | MIT, free | Pro licence required | Paid licence required | Business licence required | MIT, free | BelManage required | Paid licence required | Paid licence required |
| Offline, no account, no telemetry | Y | Y | Y | Y | Y | Y | Y | Y |
| Open source | Y (MIT) | N | N | N | Y (MIT) | N | N | N |

Reading the table: no competitor has a health score, a buyer verdict, history
trends, or a two-machine comparison. Every competitor is either a sensor dump
(HWiNFO), an inventory dump (AIDA64, Belarc), a single-component tool
(CrystalDiskInfo, Hard Disk Sentinel, OCCT, CPU-Z, GPU-Z), or a pretty summary
with no depth (Speccy). TrueSpec is the only tool that combines inventory,
sensors, health, history, comparison and a verdict in one free offline package.

### 3.3 Where TrueSpec already wins

1. **The verdict.** Nobody else gives a PASS / WARNING / FAIL answer. This is
   the single most valuable thing for the target user (a buyer), and it is
   already shipped.
2. **Honesty as a feature.** "Not reported by hardware" with the reason named.
   HWiNFO shows a blank or a zero when a sensor is missing; AIDA64's trial
   hides values behind TRIAL VERSION; CrystalDiskInfo shows nothing for a USB
   drive whose enclosure blocks SMART, which users read as "healthy".
   TrueSpec names the gap. That is a trust advantage, not a limitation.
3. **History and trends.** No competitor keeps a local scan history with
   battery-health alerts. Hard Disk Sentinel tracks drive history only.
   TrueSpec tracks the whole machine.
4. **Reports a buyer can hand to a seller.** JSON, HTML, CSV and a
   dependency-free PDF, all from one model so they always agree. AIDA64's
   reports are the only comparable ones, and they cost $65.95 a year.
5. **Free for commercial use, open source.** HWiNFO, AIDA64, Speccy and Belarc
   all charge for commercial use. TrueSpec is MIT: a repair shop can install
   it on every bench machine, inspect the source, and pay nothing.
6. **Standard-user operation.** asInvoker, no elevation prompt. CrystalDiskInfo
   needs admin for SMART; HWiNFO's full sensor set benefits from admin.
   TrueSpec degrades honestly instead of demanding elevation.

### 3.4 Where TrueSpec loses today (the gaps this plan closes)

1. **No real stability test.** AIDA64's System Stability Test and OCCT are the
   reason technicians pay. TrueSpec has short CPU/disk benchmarks, not a
   sustained, temperature-guarded load test. (Plan: U2 safe load test.)
2. **No sensor logging or threshold alerts.** HWiNFO's logging is the reason
   it stays open on a technician's second monitor. TrueSpec has live sensors
   but does not record them or alert on them. (Plan: U6.)
3. **No crash forensics.** Windows keeps a full record of blue screens, WHEA
   hardware errors and disk errors in the Event Log. TrueSpec lists "critical
   errors" but does not decode them into a crash history a buyer can read.
   (Plan: U3.)
4. **No authenticity engine.** The New Laptop Check has the right checks but
   does not cross-check them against each other. A wiped refurbished laptop
   (fresh Windows install, old SSD) passes the individual checks today.
   (Plan: U1.)
5. **No software audit.** AIDA64 and Belarc list installed programs, licences
   and missing updates. TrueSpec has drivers and updates but no installed
   software inventory. (Plan: U8.)
6. **No fan-health reporting.** Argus Monitor and HWiNFO show fan behaviour;
   TrueSpec shows fan RPM in the sensor list but never judges whether a fan
   is dead or not spinning up under load. (Plan: U5.)
7. **No measured degradation forecast.** Hard Disk Sentinel predicts drive
   failure from SMART trends. TrueSpec has the trend data but does not project
   it. (Plan: U4, with honest ranges, never a single fake date.)
8. **No tamper evidence.** Nothing stops a seller from swapping a part after
   a scan and claiming the report is for the machine in front of you.
   (Plan: U7.)
9. **No value comparison.** Two laptops, two prices, one choice. TrueSpec
   compares snapshots but does not answer "which one is worth the money".
   (Plan: U9.)
10. **Benchmarks are narrow.** CPU single-thread and sequential disk read only.
    No multi-thread, no random disk I/O, no memory bandwidth, no GPU check.
    (Plan: U10.)

---

## 4. Design principles for every new feature

Before the feature list, the rules every feature in this plan must pass. These
are the product's constitution, and they are also what make the features
defensible against the paid tools.

1. **Measured or "Not reported".** Every number comes from a named source
   (WMI class, registry key, SMART attribute, sensor, event log) or the UI
   says it is not reported and why. No estimates presented as measurements.
   A forecast is labelled a forecast, shown as a range, with the measurement
   window it came from.
2. **Read-only.** TrueSpec never changes the machine. No driver installs, no
   file deletion, no fan control, no registry writes outside its own settings.
   A diagnostic tool that can break the machine it diagnoses is a liability.
3. **Offline.** No network calls at runtime, no account, no telemetry. A
   feature that needs a server does not ship. (A bundled, versioned data file
   is acceptable only if it is small, open, and optional; the plan avoids
   this wherever a measured value exists instead.)
4. **Standard user first.** Everything works without elevation. Anything that
   needs admin is reported as a degraded path with the reason, never a
   blocking prompt. (SMART via the existing passthrough already follows this
   pattern where the OS allows it.)
5. **One screen, one answer.** Every feature ends in a plain-English sentence
   a non-technical buyer can act on, with the numbers behind it one click
   away. If a feature cannot be explained in one sentence, it is not finished.
6. **No invented judgements.** No "risk scores" for startup programs, no
   "suspicious" labels from a name heuristic, no product-key extraction.
   Facts only; the buyer decides what to do about them.

## 5. Unique features (prioritized)

Priority: P0 ships first (the buyer-truth core), P1 next (health over time),
P2 after (audit and depth). Every feature lists its real data source; if a
source is not real, the feature is not in this plan. Difficulty: S = days,
M = about a week, L = two to four weeks of focused work.

### U1. Authenticity engine: "Is this machine really new?" — P0, M

**What it does.** Cross-checks the machine's age signals against each other
and reports contradictions as evidence, in plain English. A fresh Windows
install resets the install date but cannot reset the SSD's power-on hours,
the battery's cycle count, or the BIOS date. The engine compares:
SSD power-on hours and total host writes; Windows install date; BIOS release
date; battery cycle count and health; earliest installed hotfix date; first
boot / setup events in the Event Log; and the Windows Update first-success
date. Each pair that disagrees produces one evidence line, for example:
"SSD has 4,120 power-on hours but Windows was installed 3 days ago. This
machine was used before this Windows install."
**Why it is valuable.** This is the exact scam in the used-laptop market:
a wiped, refurbished machine sold as new. No tool checks for it. Buyer
guides tell people to run `powercfg /batteryreport` and CrystalDiskInfo
separately and do the arithmetic themselves (helpbyexperts.com,
swappa.com, technolandrepairs.com). TrueSpec does the arithmetic and states
the conclusion.
**Replaces:** the manual multi-tool ritual (CrystalDiskInfo + powercfg +
guesswork). No paid tool does this.
**Data source (all real, already collected or one query away):** NVMe/SMART
power-on hours and total writes (existing Storage collector); Win32_BIOS
ReleaseDate; Win32_OperatingSystem InstallDate (existing System collector);
BatteryCycleCount (existing Battery collector); Win32_QuickFixEngineering
earliest InstalledOn (existing Updates collector); Event Log System channel,
Microsoft-Windows-Kernel-General / setup events for first boot.
**Honesty rule:** every signal is reported with its source and its own
confidence. If a signal is missing, the engine says which check it could not
run instead of guessing. A contradiction is evidence, not proof; the verdict
says "signs of prior use", never "this seller lied".

### U2. Safe load test with thermal-throttle detective — P0, L

**What it does.** A short, temperature-guarded stability test the buyer runs
before paying: 60 seconds of CPU load (all cores), memory pattern exercise,
and sequential disk reads, while sampling temperature, clock speed, fan RPM
and power every second from LibreHardwareMonitor. The test aborts
automatically if any CPU temperature exceeds 95 C or a sensor stops
reporting. The report states, in one sentence per component, what happened:
"CPU held 3.9 GHz average (92% of its rated 4.2 GHz max) at 88 C peak. No
throttling detected." or "CPU throttled to 1.8 GHz (43% of rated max) at
97 C. Cooling is not coping."
**Why it is valuable.** A laptop that looks fine at idle can throttle to half
speed under load because of dried thermal paste or a blocked vent. That is
the most expensive hidden fault in a used laptop, and the only way to find it
today is AIDA64's System Stability Test ($65.95/yr) or OCCT (time-limited
free tier). TrueSpec's version is shorter, safer (auto-abort), and the result
is a verdict, not a graph the user must interpret.
**Replaces:** AIDA64 System Stability Test and OCCT Personal for the
buyer/technician check. It does not replace a 24-hour burn-in; the report
says so.
**Data source:** existing CPU benchmark harness extended to all cores;
LibreHardwareMonitor temperature / clock / fan / power sensors (already
integrated); Win32_Processor MaxClockSpeed as the rated-max reference
(a published firmware value, not an estimate).
**Honesty rule:** the report states exactly what ran, for how long, and the
abort threshold. A 60-second test is a functional check, not a burn-in
certificate, and the UI says that.

### U3. Crash forensics: the machine's accident history — P0, M

**What it does.** Reads the Windows System event log and decodes the crash
record into a plain-English history: blue screens (BugCheck, event 1001),
hardware errors (WHEA-Logger 18/19/20, including which component reported
them), unexpected shutdowns (Kernel-Power 41), and disk errors (disk 7/11,
ntfs 55, Ntfs 98/140). Each entry shows count, first and last occurrence,
and one line of meaning: "3 blue screens in the last 30 days, most recent
12 days ago" or "14 WHEA corrected hardware errors from the CPU cache".
**Why it is valuable.** A seller can wipe Windows and hide a crash history
from the desktop, but the event log keeps the record (until the log is
cleared, which is itself a signal: see U1). No system-info tool surfaces
this. AIDA64, HWiNFO and Speccy do not read the event log at all. A machine
with weekly blue screens is worth far less than one with none, and today the
buyer has no way to know.
**Replaces:** nothing directly, which is the point: it is a capability no
paid tool offers. It strengthens the buyer verdict more than any single
sensor.
**Data source:** System event log via System.Diagnostics.Eventing.Reader
(EventLogQuery / EventLogReader, read-only). Standard-user readable for the
System channel on a normal machine; if access is denied, the page reports
that instead of failing the scan.
**Honesty rule:** counts are exact log counts in a stated window. A cleared
log is reported as "event log has no entries before <date>" with the
earliest-entry date shown, so a wiped log reads as a gap, not as a clean
record.

### U4. Degradation forecasting with honest ranges — P1, M

**What it does.** Uses the existing history database to project wear forward
and states the result as a range, never a single date. Example: "SSD wear
rose 2% over the last 4 scans (91 days). At that measured rate, wear reaches
the Replace threshold in 18 to 30 months. Range reflects the measurement
window; a shorter window widens the range." Same for battery health. The
forecast only appears when at least 3 scans over at least 30 days exist;
otherwise the page says how many more scans are needed.
**Why it is valuable.** Hard Disk Sentinel's headline feature is failure
prediction, and it costs $22-33 per PC. TrueSpec already owns the trend data;
forecasting is the natural next step, and the honest-range presentation is
something Sentinel does not do (it gives a single health percentage).
**Replaces:** Hard Disk Sentinel's predictive alerts.
**Data source:** the existing SQLite snapshot store and HistoryTrendAnalyzer;
linear fit over measured points only, with the slope's confidence shown
through the range width. No external data, no model of "typical" drives.
**Honesty rule:** the forecast is labelled a forecast, the window is shown,
and the range widens when data is thin. A single scan can never produce a
forecast. The Replace threshold is the same band rule the rest of the app
uses (battery below 60%, SSD wear per the existing health rule).

### U5. Fan-health check — P1, S

**What it does.** Reports each fan's RPM at idle and under the U2 load test,
and flags two measured faults: a fan that reports 0 RPM while the machine is
hot, and a fan whose RPM does not rise when temperature rises. One line each:
"Fan 2 (GPU): 0 RPM at 81 C. This fan is not spinning."
**Why it is valuable.** A dead fan is a cheap part and an expensive repair if
it cooks the machine first. Nobody reports it as a verdict; HWiNFO shows the
number and leaves the user to notice it is zero.
**Replaces:** the fan-monitoring half of Argus Monitor ($16.45/yr). It does
not replace fan control, and TrueSpec will never control fans (see section 9).
**Data source:** LibreHardwareMonitor fan sensors (already integrated),
sampled at idle and during the U2 load test.
**Honesty rule:** some machines do not expose fan sensors; the page says so
instead of inventing a fan. A 0 RPM reading is only flagged when a
temperature sensor on the same component is simultaneously high, so a
legitimately stopped fan at idle (fan-stop mode) is not a false alarm.

### U6. Sensor logging and threshold alerts — P1, M

**What it does.** Records live sensor readings to a local CSV log while
TrueSpec runs (temperature, fan, voltage, power, clocks, battery rate),
with user-set thresholds and a plain-English alert when one is crossed:
"CPU temperature exceeded 90 C for 30 seconds at 14:32." The log is the
machine's own record, viewable as a chart on the History page.
**Why it is valuable.** This is HWiNFO's core daily use: leave it running,
catch the overheat that only happens in a game or a render. TrueSpec has the
sensors; logging turns a snapshot tool into a monitoring tool, and it is the
single most-requested shape of feature in this category.
**Replaces:** HWiNFO's logging and alerting (free tier) and HWMonitor PRO's
logging (EUR 19.95).
**Data source:** the existing LibreHardwareMonitor sensor pipeline, sampled
at the user's refresh rate (1-30s, already a setting), appended to a local
CSV under %LocalAppData%\TrueSpec. No network, no cloud.
**Honesty rule:** the log records exactly what the sensors reported,
including gaps when a sensor stopped reporting. Thresholds are user-set;
TrueSpec ships with no threshold that implies a diagnosis, only the
measured crossing.

### U7. Machine DNA and tamper-evident buyer certificate — P1, M

**What it does.** Builds a fingerprint of the machine from its serial
numbers (motherboard, BIOS, each disk, each RAM module, CPU ID) and hashes
it (SHA-256) into the scan. The PDF report becomes a one-page buyer
certificate: the PASS / WARNING / FAIL banner, the key numbers, the machine
DNA hash, and the scan date. A later re-scan recomputes the hash and reports
"same machine, same parts" or names exactly what changed ("RAM module in
slot 2 serial changed since the 2026-11-12 scan").
**Why it is valuable.** A report is only useful if it belongs to the machine
in front of you. Today a seller can show a clean AIDA64 report from a
different, healthy machine. Machine DNA closes that hole, and no competitor
does it. It also gives repair shops a way to prove what they received versus
what they return.
**Replaces:** the paid AIDA64 report as a sale-listing document, with a
stronger guarantee (AIDA64 reports carry no machine binding).
**Data source:** Win32_BaseBoard SerialNumber, Win32_BIOS SerialNumber,
Win32_DiskDrive SerialNumber, Win32_PhysicalMemory SerialNumber,
Win32_Processor ProcessorId. All already available through CIM; the storage
collector already reads disk serials. SHA-256 over a canonical, ordered
string. The hash is stored in the snapshot row, not in a new database.
**Honesty rule:** serial numbers are shown truncated in the UI (first and
last characters) and full only in the local report file, so a screenshot
cannot be used to clone the identity. If a serial is not reported by
hardware, the DNA says which part is unidentified instead of hashing a gap.

### U8. Software audit: installed programs, activation, licences — P2, M

**What it does.** Lists installed programs (name, version, install date,
publisher) from the registry, the Windows activation state (activated yes/no,
licence channel: retail / OEM / volume), and the installed .NET / runtime
versions that matter for a buyer. It does not extract or display any product
key, ever.
**Why it is valuable.** This is Belarc Advisor's and AIDA64's audit half.
A buyer wants to know whether Windows is genuinely activated (an
unactivated machine costs the buyer a licence) and whether the "Office
included" claim is real (installed, version, date). A technician wants the
inventory without paying AIDA64 or breaking Belarc's personal-use licence.
**Replaces:** Belarc Advisor's software audit and AIDA64's software audit
for the buyer/technician use case.
**Data source:** HKLM and HKCU
SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall (read-only registry);
Win32_SoftwareLicensingProduct / SoftwareLicensingService for activation
state (CIM, standard user); registry for installed runtimes. All read-only.
**Honesty rule:** activation state is reported, keys are never read or
shown. An entry the registry cannot decode is listed as unreadable, never
guessed.

### U9. Value comparison: "which laptop should I buy?" — P1, M

**What it does.** Extends the existing two-snapshot comparison (v1.2
multi-machine history) with a buyer decision: pick two machines, enter each
asking price (clearly labelled as user-entered, not measured), and TrueSpec
shows the measured health score per unit of currency, the per-component
winner, and one sentence: "Machine B scores 82 vs 71 and costs $120 less.
On measured health per dollar, B is the better buy." The price is never
scraped, never fetched, never assumed.
**Why it is valuable.** This is the actual decision a used-laptop buyer
makes, and no tool in the category helps with it. Comparison sites compare
spec sheets; TrueSpec compares measured health.
**Replaces:** nothing directly; it is a new capability that makes the
comparison feature (which no competitor has) into a buying decision.
**Data source:** existing snapshot store and comparer (v1.2), plus a
user-entered price stored with the comparison only, never in the snapshot.
**Honesty rule:** the score is measured; the price is user input; the
"per dollar" number is arithmetic on those two, labelled as such. No market
price data is claimed.

### U10. Extended benchmarks: multi-thread, random I/O, memory bandwidth — P2, L

**What it does.** Extends the existing CPU single-thread and sequential
disk-read benchmarks with: multi-thread CPU (all cores, same workload),
random 4K disk read/write (the number that decides how a machine feels),
memory copy bandwidth, and disk latency percentiles under load. Each result
is reported with the exact workload (threads, block size, queue depth,
duration) so two runs are comparable.
**Why it is valuable.** A single-thread score and a sequential read do not
describe a real machine. Random 4K I/O is what makes a laptop feel fast or
slow, and it is the number sellers never quote. This closes the gap with
PassMark PerformanceTest's standard suite ($39) and Novabench ($36/yr) for
the buyer use case.
**Replaces:** PassMark PerformanceTest standard tests and Novabench for
whole-machine baselining. It does not replace Geekbench (cross-platform
comparison) or 3DMark (GPU rendering); the plan says so in the UI.
**Data source:** the existing benchmark harness (TrueSpec.Core/Benchmarks),
extended with the same measurement discipline: same code path, same
reporting, warm-up run, median of three runs where cheap.
**Honesty rule:** every result names its workload. A benchmark run on a
machine under load from another program is labelled with the concurrent
load note (CPU load sampled during the run).

### U11. Display panel truth (EDID) — P2, S

**What it does.** Reads the monitor EDID and reports the panel's real
manufacturer, model, manufacture week and year, native resolution, and
colour depth, next to the resolution Windows is currently using. Catches
"4K screen" listings that are a 1080p panel, and panels older than the
laptop they sit in (a replaced screen).
**Why it is valuable.** The screen is the most expensive part of a laptop
and the easiest to misrepresent. Speccy shows monitor basics; nobody shows
the panel's manufacture date or checks the listing's resolution claim
against the EDID.
**Replaces:** the monitor half of Speccy, with a check Speccy does not do.
**Data source:** WMI root\wmi WmiMonitorID (manufacturer, product code,
serial, manufacture week/year) and Win32_VideoController / display
settings for the active resolution. All read-only, standard user.
**Honesty rule:** EDID data is reported exactly as the panel declares it.
If the panel does not report a serial or a date, the page says so.

### U12. Network truth: Wi-Fi standard, link speed, driver age — P2, S

**What it does.** Reports the Wi-Fi radio type (802.11n/ac/ax/be) as the
adapter actually negotiated it, the current link speed, the adapter driver
date, and whether the driver is older than two years. One line a buyer can
use: "Wi-Fi 6 (802.11ax) adapter, linked at 1,201 Mbps, driver from 2024."
**Why it is valuable.** "Wi-Fi 6 laptop" is a common listing claim that is
often wrong, and a 2019 driver on a 2024 laptop is a maintenance signal.
No system-info tool states the negotiated radio type plainly.
**Replaces:** the network half of Speccy and AIDA64's network inventory,
with the negotiated standard made explicit.
**Data source:** `netsh wlan show interfaces` (already allowlisted pattern
for external tools) for radio type and link speed; Win32_NetworkAdapter /
driver date from the existing Network collector. Read-only.
**Honesty rule:** link speed is the negotiated rate at scan time, labelled
as such; it is not a throughput measurement and the UI says so.

## 6. Phased roadmap

v1.2 is in flight (driver-age summary, history CSV export, multi-machine
history). This plan starts at v1.3. Each phase is independently shippable and
independently verifiable: build green, tests green, CI green, live AppOnFly
check, then release. No phase depends on a later phase. Effort is in
focused-work weeks for the current team shape (coordinator + child agents);
phases are ordered by value per effort, not by ease.

### v1.3 — "Buyer Truth" (P0: U1, U3, U11, U12)

The release that makes TrueSpec the tool you take to the shop.
Ships: authenticity engine (U1), crash forensics (U3), display panel truth
(U11), network truth (U12), and the New Laptop Check rebuilt on top of U1
so the PASS / WARNING / FAIL banner now cites its evidence lines.
Why first: these four are the buyer core, they need no new hardware access
beyond what the collectors already do, and they are the features no
competitor has at any price. U1 and U3 reuse the Storage, System, Battery,
Updates and (new) Event Log collectors; U11 and U12 are small collectors.
Exit criteria: a wiped refurbished test machine (old SSD, fresh Windows) is
correctly flagged by U1; a machine with a known blue-screen history shows
it in U3; AppOnFly live check passes; release checklist gates pass.

### v1.4 — "Health Watch" (P1: U2, U5, U6, U4)

The release that makes TrueSpec the tool that stays open on the desk.
Ships: safe load test with thermal-throttle detective (U2), fan-health
check (U5, measured during U2), sensor logging and threshold alerts (U6),
degradation forecasting with honest ranges (U4).
Why second: U2 is the largest build in the plan and it unlocks U5 (fans are
measured during the load test). U4 needs the history depth v1.0.1-v1.3
created, so it cannot usefully ship earlier. U6 turns the existing sensor
pipeline into a monitor.
Exit criteria: the load test aborts correctly at the temperature threshold
on a hot test machine; a throttling machine is detected and reported with
its measured percentage of rated clock; a 30-day synthetic history produces
a forecast range and a 1-scan history correctly refuses to forecast.

### v1.5 — "Audit and Depth" (P2: U8, U10, U9)

The release that replaces the audit tools and the benchmark shelf.
Ships: software audit (U8), extended benchmarks (U10), value comparison
(U9, built on the v1.2 multi-machine history).
Why third: U8 and U10 are the AIDA64/Belarc/PassMark replacements, and U9
needs v1.2's multi-machine work to be settled first. None of them is on the
buyer's critical path, so they wait for the two releases above.
Exit criteria: an unactivated Windows install is reported as such with no
key displayed; the extended benchmarks report their workload on every
result; a two-machine comparison with user-entered prices produces the
value sentence and labels the price as user input.

### v2.0 — "The Replacement" (P1: U7, plus hardening of everything)

The release that makes the paid stack unnecessary.
Ships: machine DNA and the tamper-evident buyer certificate (U7), the buyer
certificate as a one-page printable PDF, a portable "buyer kit" mode
(single folder, no install, runs from USB), and a full pass over every
feature for the honesty wording, the empty states, and the standard-user
degraded paths.
Why last: U7 is the trust anchor for the whole product, and it only matters
once the measurements it certifies (v1.3-v1.5) are complete. The portable
mode is the distribution story: a buyer carries TrueSpec on a USB stick.
Exit criteria: a part swap between two scans is detected and named; the
certificate PDF verifies against a re-scan; the portable build runs from a
USB stick on a clean Windows 11 machine with no .NET installed and no
elevation prompt.

### Beyond v2.0 — only if the core is solid

1. **Technician mode.** Batch scans of many machines into one folder, a
   fleet summary table (machine, score, worst component, last scan date),
   and a CSV export per machine. Same local SQLite store, no server. This
   is the honest, offline answer to AIDA64 Business network audit for a
   small shop: the technician carries the USB stick to each machine instead
   of polling a network. Needs a real repair-shop workflow study first.
2. **Collector plug-in architecture.** Signed, versioned collector plug-ins
   so the community can add hardware support without touching the core.
   Only after the core collector contract (ICollector) is proven stable
   across v1.3-v2.0. Unsigned plug-ins are never loaded.
3. **Linux collector parity study.** Avalonia already builds on Linux; the
   collectors are Windows-only by design. A Linux collector set (sysfs,
   smartctl, upower) is a separate, large project. Study first, build only
   if a real user base asks for it. Windows stays the shipping target.
4. **Localisation.** English only in v1. The buyer audience is global and
   the used-laptop market is largest outside the English-speaking world.
   Localisation is a v2.x project, after the feature set is stable, and it
   must not change any measured value or threshold.
5. **Hardware database (bundled, optional).** A small, versioned, openly
   licensed dataset of CPU/GPU model specifications, used only to label
   "this CPU's rated boost clock is X" from the model string. This is the
   one place a bundled dataset is defensible, and only if it stays small
   and optional. It is listed last because every value it would provide
   already has a measured alternative (Win32_Processor MaxClockSpeed),
   and a stale database is worse than no database.

---

## 7. Blueprints (top 5 features, implementation-ready)

Each blueprint is written so the implementation team can build from it
without re-deciding the design. File paths refer to the real repo layout
verified 2026-11-12. Naming follows the existing conventions
(`*Collector.cs` in `src/TrueSpec.Collectors.Windows/Collectors/`,
`*Parser` / model types in `src/TrueSpec.Core/Collectors/`,
`ComponentSnapshot` + `CollectorSupport.Spec(label, value, source)`).

### Blueprint 1 — U1 Authenticity engine

**Goal.** One verdict, "Signs of prior use" or "No signs of prior use",
with a list of evidence lines, each naming its two sources.

**Data sources (all read-only, standard user).**
- SSD power-on hours + total host writes: existing Storage collector
  (NVMe reliability counters, SATA SMART passthrough).
- Windows install date: `Win32_OperatingSystem.InstallDate` (existing
  System collector, already a spec).
- BIOS release date: `Win32_BIOS.ReleaseDate` (existing System collector).
- Battery cycle count + health: existing Battery collector
  (`BatteryCycleCount`, `BatteryFullChargedCapacity`).
- Earliest hotfix date: `Win32_QuickFixEngineering.InstalledOn`, earliest
  row (existing Updates collector).
- First-boot evidence: System event log, earliest entry date for the
  `Microsoft-Windows-Kernel-General` provider (event 12, OS start) and
  the earliest `Microsoft-Windows-Setup` / `Kernel-Boot` entry. Read via
  `System.Diagnostics.Eventing.Reader.EventLogQuery` on the System channel.
  If the log is unavailable, the check is skipped and reported as such.

**Core (TrueSpec.Core).**
- New file `src/TrueSpec.Core/Collectors/AuthenticityEvidence.cs`:
  `AuthenticitySignal` (Name, Value, Source, CollectedAt),
  `AuthenticityCheck` (Title, EvidenceLine, Severity: Info/Watch/Strong,
  SourceA, SourceB), `AuthenticityResult` (Checks[], Verdict, Summary).
- New file `src/TrueSpec.Core/History/AuthenticityAnalyzer.cs`: pure
  functions over a `SystemSnapshot`. Rules, all as pure functions with
  unit-testable boundaries:
  - R1 SSD hours vs Windows age: if SSD power-on hours > (now - install
    date) in hours + 72h tolerance, emit Strong check.
  - R2 SSD writes vs Windows age: if total host writes > 0 and install
    date is under 30 days old and writes exceed 500 GB, emit Watch check
    (a fresh install writes tens of GB, not hundreds; the threshold is a
    constant, documented, and tunable).
  - R3 Battery cycles vs Windows age: cycles > 50 with install date under
    30 days, emit Watch check.
  - R4 BIOS date vs Windows age: BIOS release date after the Windows
    install date is impossible data, emit Strong check (bad clock or
    tampered firmware date) and mark the signal unreliable rather than
    failing the scan.
  - R5 Earliest hotfix vs install date: a hotfix dated before the install
    date means the install date is not the machine's first install, emit
    Info check.
  - R6 Event-log gap: earliest log entry newer than the install date by
    more than 7 days means the log was cleared or the install is not the
    first, emit Watch check.
- `HardwareMath` stays the home of the band rules; the analyzer holds no
  second copy of any threshold the scoring engine owns.

**Windows collector.**
- No new collector. The analyzer reads the existing component snapshots
  from `MachineScanService` output. The only new collection is the
  earliest-log-entry date, added as a spec on the System component by
  `SystemCollector` (one extra read, same pattern as the existing
  `LastBootUpTime` spec), so the analyzer stays a pure function over the
  snapshot and stays testable on Linux.

**UI.**
- New section on the New Laptop Check page ("Authenticity"), listing each
  check as one line with its two sources and a severity icon. The page
  banner (PASS / WARNING / FAIL) drops one level when any Strong check
  fires, and the banner text names the strongest check.
- New nav-visible detail: the System page gains an "Age signals" group
  showing every signal with its source, so a user can see the raw numbers
  behind the verdict.

**Scoring impact.** Authenticity is a buyer-check verdict, not a health
component: it changes the New Laptop Check banner and adds a
`BuyerCheck` entry (existing `BuyerCheckEvaluator` pattern), it does not
change the 0-100 health score. Rationale: a used machine can be perfectly
healthy; lying about its age is a trust finding, not a health finding.
The two must never be conflated.

**Tests.**
- `AuthenticityAnalyzerTests` (Core, runs everywhere): R1-R6 each with a
  passing and a firing case, tolerance boundaries (72h), missing-signal
  cases (no SSD hours, no battery, no log) asserting "check skipped" and
  never a false positive, and the impossible-data case (R4) asserting the
  signal is marked unreliable.
- `SystemCollectorTests` (Windows leg): asserts the earliest-log-entry
  spec exists on a real machine and is a parseable date or "Not reported".

**Risks.** A legitimately re-imaged corporate machine fires R1/R5 by
design; the wording must say "signs of prior use", never "refurbished
fraud". A replaced SSD (new drive, old machine) inverts the signal; the
engine reports the drive's age, not the machine's, and the UI says so.

### Blueprint 2 — U2 Safe load test with thermal-throttle detective

**Goal.** A 60-second, auto-aborting load test that reports, per component,
whether it held its rated performance, with the numbers.

**Data sources.**
- Load: CPU burn on all logical cores (managed threads, `Thread.Priority`
  normal, cancellable via `CancellationToken`), memory pattern exercise
  (allocate 256 MB, write/read a fixed pattern, verify), sequential disk
  reads (existing disk benchmark code path, 128 MB).
- Sensors: LibreHardwareMonitor via the existing sensor pipeline
  (`TrueSpec.Core/Sensors/`), sampled at 1 Hz during the test: CPU
  package temperature, per-core clocks, CPU power, fan RPM, GPU
  temperature.
- Rated reference: `Win32_Processor.MaxClockSpeed` (MHz, firmware value)
  from the existing CPU collector.

**Core (TrueSpec.Core).**
- New file `src/TrueSpec.Core/Benchmarks/LoadTest.cs`: `LoadTestConfig`
  (DurationSeconds = 60, AbortTemperatureC = 95, SampleIntervalMs = 1000),
  `LoadTestSample` (Timestamp, CpuTempC, AvgClockMHz, MaxClockMHz,
  FanRpm, CpuPowerW), `LoadTestResult` (Samples[], PeakTempC,
  AvgClockMHz, RatedMaxMHz, ClockHeldPercent, Throttled: bool,
  AbortReason: null | "temperature" | "sensor-lost" | "cancelled",
  VerdictText).
- New file `src/TrueSpec.Core/Benchmarks/LoadTestAnalyzer.cs`: pure
  functions. Throttled = average clock over the last 30 seconds is below
  80% of rated max while peak temperature is above 85 C. If the average
  clock is below 80% of rated max but temperature stayed low, the result
  is "power-limited or firmware-limited", not "throttled", with that
  wording. A sample gap (sensor stopped reporting) is recorded as a gap,
  never interpolated.
- The load test runs on a background thread with a progress callback; the
  UI stays responsive and the Cancel button works at any second.

**Windows collector.**
- No new collector. The test drives the existing sensor pipeline and the
  existing benchmark harness. The only new Windows surface is the
  per-core clock sampling, which LibreHardwareMonitor already exposes.

**UI.**
- New "Load test" card on the Tests page: duration selector (30 / 60 /
  120 seconds), Start / Cancel, live temperature and clock readouts during
  the run, and a result card after: peak temp, average clock, percent of
  rated max, throttle verdict in one sentence, and a small temperature
  sparkline (reuse the existing Sparkline control). The result is saved
  into the snapshot as a `LoadTestResult` so history can show it.

**Scoring impact.** The load test is NotMeasurable for the health score
(it is a user-initiated test, not a passive measurement). It adds a
`BuyerCheck` entry: "CPU held rated clock under load" (pass) or "CPU
throttled under load" (fail), which affects the New Laptop Check banner.
A throttling machine can still score 90 on passive health; the banner
must show the throttle finding separately.

**Tests.**
- `LoadTestAnalyzerTests` (Core, runs everywhere): throttled case,
  power-limited case, clean case, abort case, sensor-gap case, and the
  boundary (80% clock, 85 C) in both directions.
- `LoadTestIntegrationTests` (Windows leg, marked long-running): runs a
  10-second test on the real machine, asserts samples were recorded, the
  abort path works when the threshold is set to 1 C (forced abort), and
  the cancel path returns within 2 seconds.
- Safety test: the load test must never leave a thread running after
  cancel or abort; assert via a thread-count check in the test teardown.

**Risks.** A 60-second full load on a machine with a failing cooler can
push temperatures high; the 95 C abort and the 1 Hz sampling are the
mitigation, and the UI warns before starting ("This will load your CPU
for 60 seconds. It stops automatically if the temperature exceeds 95 C").
The test must never run on battery saver without warning: if the machine
is on battery, the UI shows "running on battery; results may be
power-limited" before Start.

### Blueprint 3 — U3 Crash forensics

**Goal.** A readable accident history: counts, first/last dates, and one
line of meaning per failure class.

**Data sources (all read-only, System channel).**
- Blue screens: provider `Microsoft-Windows-WER-SystemErrorReporting`,
  event 1001 (BugCheck), and `Microsoft-Windows-WHEA-Logger` is separate.
- Hardware errors: provider `Microsoft-Windows-WHEA-Logger`, events 18
  (fatal), 19 (corrected), 20 (info). Event 18's message names the
  reporting component; event 19's count is the corrected-error rate.
- Unexpected shutdowns: provider `Microsoft-Windows-Kernel-Power`,
  event 41.
- Disk errors: provider `Disk`, events 7, 11, 51; provider `Ntfs`,
  events 55, 98, 140; provider `Microsoft-Windows-Ntfs`, event 140.
- Read via `System.Diagnostics.Eventing.Reader.EventLogQuery` with an
  XPath filter per provider/event, capped at 500 events per class,
  newest first. Window: last 90 days for counts, all-time for first
  occurrence.

**Core (TrueSpec.Core).**
- New file `src/TrueSpec.Core/Collectors/CrashHistory.cs`:
  `CrashEvent` (OccurredAt, Class: BugCheck / WheaFatal / WheaCorrected /
  UnexpectedShutdown / DiskError, Detail, Source), `CrashClassSummary`
  (Class, Count90d, FirstAt, LastAt, PlainMeaning), `CrashHistory`
  (Summaries[], EarliestLogEntry, LogClearedSuspected: bool).
- New file `src/TrueSpec.Core/Collectors/CrashHistoryParser.cs`: pure
  functions. The parser takes raw event rows (provider, event id,
  timestamp, message) and returns the summaries. The plain-meaning text
  is a fixed table, not generated: BugCheck = "Windows stopped
  unexpectedly (blue screen)"; WHEA 18 = "a hardware component reported a
  fatal error"; WHEA 19 = "a hardware error was corrected automatically";
  Kernel-Power 41 = "the machine lost power or shut down unexpectedly";
  Disk 7/11 = "the disk reported an error". No diagnosis beyond that.
- `LogClearedSuspected`: earliest log entry is newer than the Windows
  install date + 7 days (same rule as U1 R6, shared constant).

**Windows collector.**
- New file
  `src/TrueSpec.Collectors.Windows/Collectors/CrashCollector.cs`
  (`ICollector`, Id "crash", Category "Diagnostics", RequiresAdministrator
  false, RefreshInterval 1 hour). Reads the System channel with the XPath
  filters above, maps rows through `CrashHistoryParser`, and emits one
  spec group per class (count, first, last) plus the headline
  ("3 blue screens in the last 90 days" or "No crashes recorded in the
  last 90 days"). If the channel cannot be read, the component is
  NotAvailable with the reason, never a failed collector.

**UI.**
- New nav item "Crash history" (reuses the Diagnostics grouping) with a
  table: class, count (90 days), first, last, meaning. An alert card
  appears when any BugCheck or WHEA fatal occurred in the last 30 days.
  The New Laptop Check banner drops one level when a BugCheck occurred
  in the last 30 days.

**Scoring impact.** Crash history is a buyer-check input, not a health
component. A machine with 3 recent blue screens gets a `BuyerCheck`
failure ("Recent blue screens recorded") and a banner drop. Corrected
WHEA errors alone do not fail a check; they are shown as information,
because corrected errors are normal on some hardware and a single number
without context would be a false alarm.

**Tests.**
- `CrashHistoryParserTests` (Core, runs everywhere): one test per event
  class with a synthetic row, the 90-day window boundary, the empty-log
  case, the capped-at-500 case, and the cleared-log rule.
- `CrashCollectorTests` (Windows leg): runs the real collector, asserts
  it returns a snapshot (or an honest NotAvailable) and never throws,
  and asserts no event is fabricated when the log is empty.

**Risks.** Event-log access can be restricted by policy; the degraded
path must be clean. Message text varies by Windows version; the parser
must rely on provider + event id + timestamp only, never on parsing the
free-text message for facts.

### Blueprint 4 — U7 Machine DNA and tamper-evident buyer certificate

**Goal.** A fingerprint that binds a report to a machine, and a one-page
certificate a buyer can verify with a re-scan.

**Data sources (all CIM, read-only, standard user).**
- `Win32_BaseBoard.SerialNumber`, `Win32_BIOS.SerialNumber`,
  `Win32_DiskDrive.SerialNumber` (per physical disk),
  `Win32_PhysicalMemory.SerialNumber` (per module),
  `Win32_Processor.ProcessorId`. All already reachable through the
  existing `Wmi.Query` helper; disk serials are already read by the
  Storage collector.

**Core (TrueSpec.Core).**
- New file `src/TrueSpec.Core/Comparison/MachineDna.cs`:
  `MachineDnaPart` (Kind: Board / Bios / Disk / Memory / Cpu, Label,
  SerialTruncated), `MachineDna` (Parts[], Hash). The canonical string
  is the parts sorted by (Kind, Label) joined with `|` as
  `Kind=Label:Serial`, hashed with SHA-256, hex-encoded. The hash input
  uses the full serial; the UI and the stored snapshot show only the
  truncated form (first 4 + last 4 characters) for privacy.
- New file `src/TrueSpec.Core/Comparison/MachineDnaComparer.cs`: pure
  functions. `Compare(old, new)` returns Added / Removed / Changed parts
  by (Kind, Label), and a verdict: Identical / Changed (with the list) /
  Unverifiable (a part that was unidentified in either scan).
- `SystemSnapshot` gains a `MachineDnaHash` field and a
  `MachineDnaParts` list. The snapshot store schema gains two columns;
  old snapshots without DNA read as "DNA not recorded", never as a
  mismatch.

**Reports.**
- `PdfReportExporter` gains a certificate layout: one page, the PASS /
  WARNING / FAIL banner, the overall score, the five most important
  numbers (SSD wear, battery health, cycles, power-on hours, throttle
  result if a load test ran), the DNA hash (full, in the file only), and
  the scan date. The JSON export gains the same fields so a re-scan can
  be verified programmatically.

**UI.**
- The Report page gains a "Buyer certificate" button (PDF, one page) and
  a "Verify this machine" action that re-scans and compares DNA against
  the selected snapshot, showing "Same machine, same parts" or the exact
  changed parts. The Compare page gains a DNA row at the top of the
  comparison.

**Scoring impact.** None. DNA is identity, not health. It never changes
the score or the banner; it only certifies which machine the score
belongs to.

**Tests.**
- `MachineDnaTests` (Core, runs everywhere): canonical ordering (same
  parts in different order give the same hash), truncation rules, a
  changed-part comparison, a removed-part comparison, an unidentified
  part in either scan giving Unverifiable, and the empty-parts case
  (a machine that reports no serials at all gives "DNA not available").
- `SnapshotStoreTests` extension: a snapshot with DNA round-trips
  through SQLite, and an old snapshot without DNA loads as "not
  recorded".
- `PdfReportExporterTests` extension: the certificate PDF parses and
  contains the banner text and the hash.

**Risks.** Serial numbers are the privacy-sensitive part of this feature.
The full serial must never appear in the UI, in a screenshot-friendly
view, or in a log. Some OEMs report the same placeholder serial
("To be filled by O.E.M.") on many machines; the DNA builder treats a
known placeholder as "not reported" (fixed list, documented) so two
different machines cannot share a DNA by accident.

### Blueprint 5 — U4 Degradation forecasting with honest ranges

**Goal.** A forecast that says what it is, shows its window, and gives a
range, never a single date.

**Data sources.**
- The existing SQLite snapshot store: per-scan battery health percent,
  SSD wear percent, and scan date. No new collection. The trend
  analyser (`HistoryTrendAnalyzer`) already computes deltas and gaps.

**Core (TrueSpec.Core).**
- New file `src/TrueSpec.Core/History/DegradationForecast.cs`:
  `ForecastPoint` (Date, Value), `DegradationForecast` (Metric: Battery /
  SsdWear, PointsUsed, WindowDays, SlopePerDay, ForecastLowMonths,
  ForecastHighMonths, Threshold, BasisText). The slope is an ordinary
  least-squares fit over the measured points (gaps skipped, never zero).
  The range comes from the slope's standard error: the low estimate uses
  slope + 1 standard error, the high estimate slope - 1 standard error,
  clamped to a minimum range width of 25% of the midpoint so a very
  clean fit does not pretend to be precise.
- Rules, all in the pure function and all unit-tested: fewer than 3
  measured points, or a window under 30 days, returns "not enough data"
  with the count of scans still needed; a slope of zero or improving
  returns "no measurable decline in the window"; the threshold is the
  existing band rule (battery Replace below 60%, SSD wear per the
  existing storage health rule) and is named in the output.
- New file `src/TrueSpec.Core/History/ForecastTests` is not a file; the
  tests live in `tests/TrueSpec.Tests/HistoryForecastTests.cs` (see below).

**UI.**
- The History page gains a forecast card under each trend sparkline,
  showing the range and the basis: "SSD wear rose 2% over 4 scans
  (91 days). At that measured rate, wear reaches the Replace threshold
  in 18 to 30 months." When data is insufficient, the card says how many
  more scans are needed and over what span. The card is hidden entirely
  when the metric has no history.

**Scoring impact.** None. A forecast is information, not a measurement.
It must never lower the score; the current health is what the score
measures. The forecast informs the buyer ("this battery will need
replacing within two years at the measured rate"), it does not punish
the machine for the future.

**Tests.**
- `HistoryForecastTests` (Core, runs everywhere): 2-point refusal,
  29-day window refusal, 30-day acceptance, zero-slope case, improving
  case, a synthetic linear decline producing the expected range, a gap
  in the middle being skipped, and the minimum-range-width clamp.
- A test with a real SQLite store holding 4 synthetic snapshots, asserting
  the ViewModel surfaces the forecast text.

**Risks.** A user who replaces a battery or an SSD resets the series;
the analyser must detect a step change (a jump of more than 20 points
between consecutive scans) and restart the window at the step, with a
note ("battery replaced between scans; forecast restarts here"). Without
this, a replaced part produces a nonsense slope. This rule is part of
the blueprint, not a later fix.

## 8. What "kicks out" each paid tool

Status: **Replaced** means TrueSpec covers the reason most people pay for
that tool. **Mostly** means the core paid value is covered and the gap is
named. **Partially** means a real part stays with the paid tool. **Not
replaced** means the plan honestly defers to the specialist tool.

| Paid tool | Price | Status after v2.0 | What replaces it | What honestly stays with the paid tool |
|---|---|---|---|---|
| AIDA64 Extreme | $65.95/yr (3 PCs) | Mostly | System inventory, U2 load test, U10 benchmarks, U8 software audit, reports, U3 crash forensics | The 50-page hardware database depth, sensor-panel / LCD output, and the System Stability Test's long-duration runs |
| HWiNFO Pro Personal | $29/yr | Mostly | Live sensors, U6 logging and alerts, health score, buyer verdict | Remote monitoring of other machines, commercial multi-machine use |
| Speccy Professional | $19.95/yr | Replaced | Clean summary, reports, U11 panel truth, U12 network truth | Nothing material; Pro's scheduled scans are covered by history |
| Hard Disk Sentinel Pro | $33 lifetime | Mostly | Drive health, wear, U4 forecasting, U6 alerts | Surface tests and automatic on-failure backup |
| OCCT Personal | $25/yr ($5/mo) | Mostly | U2 safe load test with auto-abort and throttle verdict | Unlimited-duration burn-in and paid Stability Certificates |
| Argus Monitor | $16.45/yr | Partially | U5 fan-health monitoring (read-only) | Fan control. TrueSpec will never control fans; see section 9 |
| Belarc Advisor | Free personal; BelManage paid for commercial use | Mostly | U8 software audit, U1 age signals, Updates status | Licence-key recovery. TrueSpec never extracts keys; see section 9 |
| HWMonitor PRO | EUR 19.95 | Replaced | U6 logging and alerts (local) | Remote monitoring over a network |
| Novabench Plus | $36/yr | Mostly | U10 extended benchmarks | Cross-machine online comparison database |
| PassMark BurnInTest Standard | $59 one-time | Partially | U2 short functional test, U3 crash forensics, U10 benchmarks | Manufacturer burn-in certification |
| PassMark PerformanceTest | $39 one-time | Mostly | U10 benchmarks | The baseline database of millions of submitted results |
| MemTest86 Pro | $116 download | **Not replaced** | U10 includes a quick in-OS memory pattern exercise, and the UI directs the user to MemTest86 when crashes point at RAM | The definitive bootable RAM test. No in-OS tool can replace it, and this plan does not pretend otherwise |
| BatteryMon | Free personal | Replaced | Battery health bands, history, alerts, U6 logging | Nothing material |
| PC-Doctor Toolbox | From $19.99 | Mostly | U2 load test, U3 crash forensics, U10 benchmarks | Branded technician certification workflows |

**The money.** The recurring stack a home user or small shop pays today for
the capabilities TrueSpec covers:

| Tool | Per year |
|---|---|
| AIDA64 Extreme | $65.95 |
| HWiNFO Pro Personal | $29.00 |
| Speccy Professional | $19.95 |
| OCCT Personal (annual billing) | $25.00 |
| Argus Monitor (1 year) | $16.45 |
| Novabench Plus | $36.00 |
| **Total recurring** | **$192.35/year** |

One-time purchases covered in part: Hard Disk Sentinel Pro $33 and
BurnInTest Standard $59 ($92 one-time). MemTest86 Pro ($116) is the one
tool this plan deliberately does not replace, and the product should say
that on the memory page rather than hide it. That sentence is a feature:
it is the proof the rest of the claims are honest.

**The free tools TrueSpec also absorbs** (no money saved, but one install
instead of six): CrystalDiskInfo, CPU-Z, GPU-Z, BatteryInfoView, Open
Hardware Monitor, and the manual `powercfg /batteryreport` ritual.

---

## 9. What TrueSpec will never build

These are rejected by principle, not by priority. Each one has a real user
ask behind it, and each one fails the honesty rule, the read-only rule, or
the safety rule. Listing them here stops a future version from drifting.

- **Fan control.** Controlling a fan can cook hardware if the curve is
  wrong, and it almost never works on laptops (Argus Monitor's own
  documentation says so). TrueSpec reports fans; it never drives them.
- **Driver auto-update / "fix my drivers".** Downloading and installing
  drivers is a network action with real bricking risk. Reporting old
  drivers is the honest version. (Already rejected in ROADMAP.md.)
- **Disk cleanup / anything that deletes files.** A buyer-check tool must
  never delete. (Already rejected in ROADMAP.md.)
- **Overclocking or voltage control.** Same reason as fan control, with
  higher stakes.
- **Battery runtime prediction as a single number.** The firmware's
  EstimatedRunTime is a snapshot, not a forecast. (Already rejected in
  ROADMAP.md.) U4 forecasts health decline as a range; it never predicts
  "2 hours 14 minutes remaining".
- **A single-date SMART failure prediction.** A drive fails on a
  distribution, not a date. U4 gives a range with its window; a single
  date would be an invented number.
- **Product-key extraction or display.** Belarc recovers keys; TrueSpec
  reports activation state only. Keys in a report file are a leak waiting
  to happen.
- **Startup "risk scores" / flagging unknown programs.** A heuristic that
  calls a program suspicious from its name is an invented judgement.
  (Already rejected in ROADMAP.md.)
- **Cloud sync, accounts, or a web dashboard.** History is local SQLite by
  design. (Already rejected in ROADMAP.md.) Technician mode (beyond v2.0)
  is a USB stick carried between machines, not a server.
- **Remote monitoring or remote control of other machines.** That is a
  network service with an attack surface, and it contradicts the offline
  promise. HWiNFO Pro and AIDA64 Business own this; TrueSpec does not
  compete there.
- **A bundled hardware database as a source of truth.** Model
  specifications change, and a stale database produces confident wrong
  answers. Measured values (MaxClockSpeed, EDID, SMART) are the source;
  a bundled dataset may only ever label, never measure.
- **Ads, upsells, or a paid tier.** The product is MIT open source. A
  "Pro" tier would destroy the trust the whole plan is built on.

---

## 10. Sources

Prices and claims were checked 2026-11-12. Vendor pages win over
review sites where they disagree. Prices move; verify before quoting.

- HWiNFO licences (Personal $29, Corporate $44, Engineer $232, lifetime
  from $199): https://www.hwinfo.com/licenses/ ; also
  https://winningpc.com/hwinfo64-discount-promo-codes/ and
  https://www.ghacks.net/2021/03/09/hwinfo64-7-00-split-into-free-and-commercial-versions/
- AIDA64 Extreme ($65.95/yr, 3 PCs; 2-year $98.95):
  https://winningpc.com/aida64-extreme-product-key-coupon-code-free/ and
  https://exedepot.com/en/soft/aida64/ ; trial behaviour and editions:
  https://www.techspot.com/downloads/433-aida64-extreme-edition.html
- AIDA64 Engineer / Business / Network Audit pricing (FinalWire v4.30):
  https://www.nikktech.com/main/news/3266-finalwire-debuts-aida64-v4-30
- Speccy Professional ($19.95/yr; v1.34.84, June 2026):
  https://www.tutorialpedia.org/blog/speccy-windows-11/ and
  https://www.codegenes.net/blog/speccy-download/ ; CCleaner Pro Plus
  bundle ($59.95-64.95/yr): https://ciroapp.com/software/ccleaner
- HWMonitor PRO (Standard EUR 19.95, Extended EUR 34.95):
  https://www.cpuid.com/softwares/hwmonitor-pro/registration.html and
  https://techdows.com/download-hwmonitor-for-pc-windows-11-10-mac.html
- OCCT (free Personal, 1-hour limit since v7.0; Personal $5/mo or
  $25/yr; Pro $150/yr; Enterprise $499/yr):
  https://www.techspot.com/downloads/4665-overclock-checking-tool.html ,
  https://www.ocbase.com/download-personal ,
  https://www.downloadcrew.com/news/43242/occt_70_unveils_testing_time_limit_in_free_version_splits_ui_into_three_distinct_sections
- Argus Monitor (1 year $16.45, 3 years $35.95; 30-day trial):
  https://www.itechguides.com/products/argus-monitor/ (facts checked
  20 Sep 2026) and https://rankite.com/blog/best-fan-control-software
- Hard Disk Sentinel (Standard $22, Professional $33, lifetime):
  https://exedepot.com/en/soft/hard-disk-sentinel/ and
  https://www.codegenes.net/blog/hard-disk-sentinel-download/
- PassMark BurnInTest (Standard $59, Professional $99) and MemTest86 Pro
  ($116 download): https://findalternatives.net/categories/computer-hardware-diagnostic-software ,
  https://www.memtest86.com/compare.html ,
  https://www.memtest86.com/pricing.html
- Benchmark pricing (PerformanceTest $39, Geekbench 7 Pro $99, 3DMark
  $34.99, Novabench Plus $36/yr): https://rankite.com/blog/best-benchmarking-software
- CrystalDiskInfo (free, MIT; limits incl. the Google study figure that
  36% of failed drives showed zero SMART warnings):
  https://mdrepairs.com/blog/crystaldiskinfo-guide/ ,
  https://www.codegenes.net/blog/crystaldiskinfo-download/ ,
  https://www.softwr.com/software/crystaldiskinfo
- Belarc Advisor (free personal use only; BelManage for commercial use;
  licence-key recovery): https://www.codegenes.net/blog/belarc-advisor-download/ ,
  https://www.filehorse.com/download-belarc-advisor/?amp ,
  https://appmus.com/software/belarc-advisor?platform=windows
- BatteryMon / BatteryInfoView (free for personal use):
  https://www.geckoandfly.com/7112/software-to-test-laptop-notebook-battery-life-status-load-and-rechargeable-capacity/
- PC-Doctor Toolbox (from $19.99):
  https://www.itechfaqs.com/best/hardware-diagnostic-software/
- Comparative reviews (strengths, weaknesses, user complaints):
  https://rankite.com/blog/best-pc-diagnostic-software ,
  https://rankite.com/blog/best-pc-health-check-software ,
  https://rankite.com/blog/best-pc-hardware-monitoring-software ,
  https://rankite.com/blog/best-system-temperature-monitoring-software ,
  https://windowscape.org/how-to-use-hwinfo-speccy-and-aida64-to-check-laptop-temperatures-and-hardware-health/ ,
  https://axiobench.com/best/hardware-diagnostic-software/ ,
  https://cdn-b.saashub.com/compare-aida64-vs-hwinfo ,
  https://fossbytes.com/best-system-information-software-specs-windows/ ,
  https://www.techcommuters.com/best-free-system-information-tools/
- Used-laptop buyer checks (battery 80% rule, SSD health, power-on hours,
  locks, `powercfg /batteryreport`):
  https://www.helpbyexperts.com/blog/refurbished-laptop-what-to-check ,
  https://swappa.com/blog/how-to-inspect-a-used-laptop/ ,
  https://www.technolandrepairs.com/guides/buying-a-refurbished-laptop ,
  https://www.best-discount.cc/used-laptop-inspection-checklist/
- TrueSpec itself: `README.md` and `ROADMAP.md` in this repo
  (v1.1.0 baseline, v1.2 in flight, rejected list), and the source layout
  verified 2026-11-12 (`src/TrueSpec.Core/`, `src/TrueSpec.Collectors.Windows/`,
  `src/TrueSpec.App/`, `tests/TrueSpec.Tests/`).

---

## Document control

- Version: 1.0, 2026-11-12. Planning only; no code was changed for this
  document, nothing was committed.
- Baseline: v1.1.0 (5702c9e). In flight, not duplicated here: v1.2
  (driver-age summary, history CSV export, multi-machine history) and the
  hardening pass.
- Standing rules carried into implementation: all 8 Claude skills
  (senior-frontend, senior-backend, micro-saas-launcher,
  claude-engineering-team, software-architecture, code-review-checklist,
  testing-patterns, security-audit), web parts never used, no cloud, honesty
  rule, verify local build + tests + CI green, no tag or release from the
  implementation team, do not create or edit `.github/workflows/*`, do not
  touch the uncommitted `LAPTOP-TEST-CHECKLIST.md` edit.
- Next step: implement v1.3 first (U1, U3, U11, U12), one feature at a time,
  each verified by build + tests + CI + a live AppOnFly check before the
  next begins.
