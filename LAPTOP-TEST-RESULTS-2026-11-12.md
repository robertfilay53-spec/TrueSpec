# TrueSpec 1.4.0 — Real-hardware laptop test results (Jawad, Nov 12, 2026)

Tester: Jawad, on his own laptop. 22 screenshots: SmartScreen, installer
wizard, Overview (during + after scan), component cards, GPU / Network /
Security / Drivers / History / Tests / Report / Settings pages.

## Machine

- Lenovo, motherboard LENOVO 20L6S6WQ00
- CPU: Intel(R) Core(TM) i5-8350U @ 1.70GHz
- RAM: 8.0 GB DDR4 2400 MT/s
- Storage: SAMSUNG MZVLB256HAHQ-000L7 (256 GB NVMe)
- GPU: Intel(R) UHD Graphics 620, driver 30.0.101.1122 dated 2021-11-15
- Network: Intel(R) Dual Band Wireless-AC 8265, driver 20.70.32.1 dated 2022-07-24
- Display: 1920 x 1080 60 Hz
- Windows version: UNKNOWN from the app (System page shows "Not available")
- Ran as: standard user (status bar: "Admin: standard user"), not admin
- TrueSpec 1.4.0 (v1), installed from TrueSpecSetup-1.4.0.exe

## Install

- SmartScreen blocked the installer: "Windows protected your PC",
  "Microsoft Defender SmartScreen prevented an unrecognized app from
  starting", App: TrueSpecSetup-1.4.0.exe, Publisher: Unknown publisher.
  Jawad clicked More info, then "Run anyway". Install then succeeded.
- MIT License Agreement shown (Copyright (c) 2026 TrueSpec contributors).
- Destination: C:\Users\RanaJawadLaptop\AppData\Local\Programs\TrueSpec
  (per-user install, no admin needed). Desktop shortcut created.
- Setup completed, "Launch TrueSpec" checked, app launched.

## Scan result

- Verdict: **Worn, 64.8 out of 100.** "Measured weight 50 of 100. Only
  components with a real sensor are included."
- Breakdown: Battery 47.6% (weight 25, +23.8 pts), RAM 100% (weight 15,
  +30 pts), Network 55% (weight 10, +11 pts).
- Report page: "FAIL - this machine does not look new. 0 of 10 buyer
  checks passed." Snapshot 2026-11-12 13:46:25.
- Live sensors work: CPU usage 25.1% with sparkline, GPU load 5.2%.
- Last scan 13:46:25. "Real scan complete."

## Real findings (tool working correctly, not bugs)

- Battery genuinely worn: 23,940 mWh, 47.6% health, 42 cycles.
- Wi-Fi driver 4 years old (2022-07-24, older than 2 years) — correctly flagged.
- Link speed 72.2 Mbps (low — 20 MHz single-stream rate).
- RAM healthy at 100%.

## Defects found

### P1 — GPU color count overflow (real bug, visible on GPU page)

- GPU detail page: Current mode "1920 x 1080 x 4294967296 colors".
- 4294967296 = 2^32. The bits-per-pixel value is being displayed as a
  raw color count. Should read "32-bit color" (or equivalent).
- Fix: display bit depth, never the raw 2^32 number.

### P1 — Security collector times out on real hardware

- Security page: "Not available". Status: "Collector timed out after
  10 seconds. TrueSpec does not invent a value when the hardware does
  not report one."
- Security (Defender / Secure Boot / TPM state) is a core buyer check.
  It must not time out on a real laptop. Suspect: slow WMI query or
  standard-user access. Fix: longer timeout, faster query, or run the
  collector in a way that works as standard user.

### P1 — Drivers collector times out on real hardware

- Drivers page: "Not available". Status: "Scan timed out while
  collecting this component."
- Same class of defect as Security. Enumerating drivers must complete
  on a real machine.

### P1 — Half the components report nothing as standard user

- System: Not available. Security: Not available. Drivers: Not
  available. Windows Update: Not available. Startup: Not available.
  Crash history: Not available. Disk health: Not measurable (SMART /
  NVMe reliability counters). CPU temperature: Not reported by hardware.
- The app runs asInvoker, so admin-gated sensors stay silent. That is
  by design, but on a real laptop it means the headline features (disk
  wear %, CPU temp, System info incl. Windows version) do not work out
  of the box. The app should tell the user plainly: "Run as
  administrator for full results" — or request elevation for the scan.
- Note: the System page being blank also means the app cannot even
  report the Windows version of the machine it runs on.

### P1 — CPU temperature never reports; History chart is empty

- Live sensors: CPU temp "Not available / No sensor reading", GPU temp
  "Not available". History page: "CPU temperature, 89 samples from
  08:45 to 08:48. 89 samples, 89 gaps where the sensor did not report."
  The chart is completely empty.
- LibreHardwareMonitor exposes no CPU temp sensor on this Lenovo (EC
  sensor not exposed, or needs admin). Not a code bug by itself, but
  the U6 chart showing nothing but gaps looks broken, and the U2 load
  test cannot judge throttling without a temp/clock sensor (the v1.4
  honest-verdict fix handles this correctly — it must say "could not
  be judged" here, never guess).
- Also: History chart times read 08:45-08:48 while the scan ran at
  13:45-13:48 local — the chart appears to use UTC while everything
  else shows local time. Verify and use local time everywhere.

### P2 — Display copy bug

- Display card: "Not reported by hardware 1920 x 1080 60 Hz".
- Resolution and refresh ARE reported; only the panel model/name is
  not. The copy reads as if the whole display was not detected.
- Fix: show the real values first, and mark only the missing field as
  not reported (e.g. "Panel model: Not reported by hardware").

### P2 — Wi-Fi details all unreported despite a live connection

- Network page: Signal, Band, Channel, Radio type, Wi-Fi standard
  (negotiated), Receive rate, Transmit rate, SSID — all "Not reported
  by hardware" (source: netsh wlan show interfaces).
- The machine IS on Wi-Fi (link speed 72.2 Mbps reported from CIM).
  On a connected machine netsh should return SSID and signal. Suspect
  the netsh output parsing fails (interface name match, locale, or
  the command itself failing as standard user). Investigate and fix
  the parser; do not silently show blanks for a connected adapter.

### P2 — GPU driver age not flagged (inconsistent with Network)

- GPU driver is from 2021-11-15 (5 years old) and nothing flags it,
  while the Network page flags its 2022 driver as "4 years old, older
  than 2 years". Apply the same driver-age rule to every component.

### P2 — "0 of 10 buyer checks passed" needs verification

- RAM is 100% healthy, yet the report says 0 of 10 buyer checks
  passed. Confirm the 10 checks and whether 0/10 is correct for this
  machine, or whether checks that should pass (RAM, and any others)
  are being counted as failed because their inputs are "Not reported".

### P1 — Export saves silently to the wrong expected place (repro confirmed by user, Nov 12)

- User clicked Export PDF: nothing visible happened. He expected the
  file in the Downloads folder. The app actually writes to
  C:\Users\RanaJawadLaptop\Documents\TrueSpec — so he looked in the
  wrong folder and concluded the export failed.
- Evidence the export DID write a file: the status bar on the Report
  page shows "Report exported to
  C:\Users\RanaJawadLaptop\Documents\TrueSpec\TrueSpec-report-20..."
  after a click. Same applies to JSON, HTML and CSV.
- The real defects are UX, not a missing file:
  1. No visible confirmation after clicking export — no toast, no
     dialog, no folder opened. Only a small status-bar line.
  2. The save location (Documents\TrueSpec) does not match the user's
     expectation (Downloads), and nothing tells him where the file went.
- Fix for v1.4.1: after every export, show an unmissable confirmation
  with the exact path plus "Open folder" and "Open file" actions.
  Consider defaulting the export folder to Downloads, or asking on
  first export. Settings already lets the user change the folder
  (Report export folder), but nobody finds that after a silent click.
- User to confirm: open Documents\TrueSpec in File Explorer and check
  which files are actually there (JSON / HTML / CSV / PDF).

## SmartScreen / code signing (known, needs a decision)

- The installer is unsigned, so SmartScreen shows "Unknown publisher"
  and blocks it until the user clicks More info, then "Run anyway".
- For a buyer-trust tool this is the biggest trust problem in the
  whole flow: a buyer will not click "Run anyway" on an unknown
  publisher. Fix is a code-signing certificate (~$200-300/yr, payment
  decision, Jawad's call). Reputation builds over time even with a
  standard OV cert; EV removes the warning immediately but costs more.

## Not yet sent by the tester

- New Laptop Check screen screenshot (the Report page shows its
  verdict, but the dedicated screen was not captured).
- The exported report file itself (JSON/HTML/CSV/PDF from
  Documents\TrueSpec).
- Windows version (System page blank, so it is unknown).

## Verdict

Install works, the app runs, the scan completes, and the honest
"Not reported" behaviour is working as designed. But on real hardware
as a standard user, the tool is much weaker than on the AppOnFly VM:
two collectors time out, disk wear and CPU temp never report, and the
GPU page shows a visible overflow bug. The fixes above (collectors,
elevation guidance, GPU copy, netsh parsing, display copy, History
timezone) are the v1.4.1 scope. Code signing is the separate trust
decision.

## Addendum — findings from the exported PDF report (Nov 12, 13:50)

Jawad exported the report (JSON 99 KB, PDF 28 KB, CSV 13 KB — all present
in Documents\TrueSpec, folder screenshot confirmed). The PDF
(TrueSpec-report-20261112-135017.pdf, 6 pages) was read in full. New
defects beyond the screenshots:

- **Ethernet link speed overflow (same class as GPU colors):**
  "Ethernet link speed: 9223372036854.8 Mbps" — Int64.MaxValue sentinel
  divided by 1e6. Must show Not reported when the raw value is the
  unknown sentinel.
- **System collector timed out too:** System, Security, Drivers each
  timed out after 10s; Windows Update, Startup, Crash history never
  ran ("Scan timed out before this collector ran"). One slow collector
  starves the rest — structural scan-budget issue.
- **New Laptop Check headline is misleading:** "0 of 10 buyer checks
  passed" = 1 Failed (battery cycles 42) + 9 Not applicable. Report
  the real split. Also the BIOS-date check says "BIOS date was not
  available" while the Motherboard page shows BIOS date 2025-09-06 —
  the check must use the date already collected.
- **Battery Chemistry raw value:** "5269836" instead of Li-ion etc.
  Decode the enum.
- **RAM Form factor wrong:** "RIMM" — this machine is SODIMM; likely
  an off-by-one in the mapping table.
- **Storage "Operational status: 2"** — raw number, should read "OK".
- **Panel serial "0"** — should be Not reported.
- **PDF header stray "Not reported":** "RANAJAWADLAPTOP / Not reported
  Generated 2026-11-12 13:50:17..." — a header field with no value.
- **PDF New Laptop Check section is a run-together wall** — each check
  needs its own line/row with clear separators.
- **BIOS vendor "Not reported"** while BIOS version (N24ET81W 1.56)
  and date (2025-09-06) are present.
- **Ethernet status Up but IP/gateway/DNS Not reported** — check the
  adapter-config read.
- **Driver-age rule inconsistent (confirmed):** GPU driver 2021-11-15
  and Ethernet driver 2019-06-06 get no flag; Wi-Fi driver 2022 does.
- **Machine confirmed:** ThinkPad T480 (System family), BIOS
  2025-09-06, panel LEN 40A9 made 2016, 13.9", 32-bit colour (this
  confirms the GPU "4294967296 colors" is the same
  CurrentBitsPerPixel=32 value misformatted).
- **Export confirmed working:** JSON/PDF/CSV all written to
  Documents\TrueSpec. The "not working" report was the silent save +
  wrong expected folder (Downloads).
