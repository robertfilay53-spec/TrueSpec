# TrueSpec — Laptop Test Checklist (for Jawad's Windows laptop)

Prepared Nov 10, 2026. P1-P3 build is done and pushed (commit 63254d8, public repo
https://github.com/robertfilay53-spec/TrueSpec). The VM cannot test real sensors,
so this run needs a real Windows machine. About 10 minutes.

## Before you start

- Windows 10 or 11 laptop/PC with .NET 10 SDK installed.
  No SDK yet: install from https://dotnet.microsoft.com/download/dotnet/10.0
  (SDK, not just runtime), then restart the terminal.
- No admin rights needed to run, but run as Administrator if you want the full
  SMART + event-log readings.

## Run it (3 commands)

```powershell
git clone https://github.com/robertfilay53-spec/TrueSpec.git
cd TrueSpec
dotnet run --project src/TrueSpec.App
```

First run restores and builds; it takes a minute or two. The app opens on the
Overview dashboard (dark theme, score ring at the top).

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

- Close the app. Nothing is installed system-wide and nothing is uploaded;
  the app is 100% offline, no account, no telemetry.
- If you want it gone from the laptop afterwards: delete the TrueSpec folder.
- CI note for Ziggy (not your step): `.github/workflows/ci.yml` is ready on disk
  at `~/workspace/production/pc-inspector/.github/workflows/ci.yml` but is NOT
  in the repo yet (gh token lacks workflow scope). It goes in via the GitHub
  web UI, then CI builds + tests on Ubuntu and Windows on every push.
