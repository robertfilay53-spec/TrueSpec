# TrueSpec — Laptop Test Checklist (for Jawad's Windows laptop)

Prepared Nov 10, 2026, updated Nov 11, 2026. v1.0.0 is RELEASED and verified:
https://github.com/robertfilay53-spec/TrueSpec/releases/tag/v1.0.0
The VM cannot test real sensors, so this run needs a real Windows machine.
About 10 minutes. No .NET SDK needed anymore, use the installer below.

## Easiest way: installer (no build, no SDK)

1. Download **TrueSpecSetup-1.0.0.exe** (35 MB) from the release page above.
   SHA256: 145ab36271ae18f2f0c7a03b477bac8ba979d0e7a8bef98efaaa4baa6c82887a
   (Alternative, no install at all: **TrueSpec-1.0.0-portable.zip**, 47 MB,
   SHA256: b3b1ff969a49ade513988adbb6bc0db93379a33303167f427d27393b619d7de1,
   extract and run TrueSpec.App.exe.)
2. Double-click the installer, install, run the app. No admin needed, no
   account, 100% offline. The app opens on the Overview dashboard (dark
   theme, score ring at the top).
3. Then do the 10 checks below.

## Fallback: build from source (only if the installer does not work)

- Windows 10 or 11 laptop/PC with .NET 10 SDK installed.
  No SDK yet: install from https://dotnet.microsoft.com/download/dotnet/10.0
  (SDK, not just runtime), then restart the terminal.
- No admin rights needed to run, but run as Administrator if you want the full
  SMART + event-log readings.

```powershell
git clone https://github.com/robertfilay53-spec/TrueSpec.git
cd TrueSpec
dotnet run --project src/TrueSpec.App
```

First run restores and builds; it takes a minute or two.

## What to check (tick off as you go)

1. **Overview loads** — score ring (0-100) + verdict word visible, 4 key stats
   (CPU temp, RAM, disk health, battery health) showing real numbers.
2. **Component cards** — CPU, RAM, Storage, Battery, GPU, Display, Network,
   Motherboard, Security, Drivers all present with a headline value.
3. **Live strip moves** — CPU temp / GPU temp / usage sparklines update every
   ~2 seconds. If they are flat for 30 seconds, note it.
4. **Battery page** — design capacity vs full charge capacity, cycle count,
   health % shown. (Health % is real math from the battery report.)
5. **Storage page** — SSD wear % and power-on hours shown for the main drive.
   NVMe shows Percentage Used; SATA shows SMART wear.
6. **Network page** — WiFi card name, maker, driver date, link speed shown.
7. **New Laptop Check** (left rail) — PASS / WARNING / FAIL banner + checklist
   rows (SSD wear, battery cycles, RAM matches, power-on hours, BIOS date,
   Windows install date, critical errors, signed drivers).
8. **Export** — Report view, export JSON and HTML. Both files should open and
   show the same numbers as the screen.
9. **Honesty check (important)** — RAM life %, CPU life %, motherboard life %
   and WiFi manufacture date do NOT exist as hardware sensors anywhere. The app
   must show "Not reported by hardware" (or "Not measurable") for these, never
   a made-up percentage. If you see a % there, that is a bug: screenshot it.
10. **Anything blank or "Not available"** — note which component and what it says.
    On Windows, "Not available" for a real part is a bug; on Linux it is expected.

## What to send back

- 2 screenshots: Overview dashboard, and New Laptop Check.
- The exported report file (JSON or HTML, either one).
- One line per item above that looked wrong, blank, or fake, with the exact
  wording on screen.
- Your laptop model + Windows version (for the test log).

## After the test

- Close the app. Nothing is uploaded; the app is 100% offline, no account,
  no telemetry.
- If you want it gone from the laptop afterwards: uninstall from Windows
  Settings (verified clean Nov 11: install folder, app data, Start Menu entry
  and registry key all removed; your exports in Documents\TrueSpec are never
  deleted). Portable version: just delete the extracted folder.
