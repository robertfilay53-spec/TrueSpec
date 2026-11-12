# TrueSpec Release Checklist — Big-Tech Grade (10/10)

Owner: Ziggy (architect). Builder: OpenCode DeepSeek (code only).
Rule: NOTHING ships until every gate below passes. A failed gate blocks the release, no exceptions.

## Target
- Windows 11 first, Windows 10 supported (10 version 1809 or newer, .NET 10 requirement). x64 only for v1.
- Runs as the invoking user, never asks for elevation (app.manifest: asInvoker). Sensors that need admin rights report "Not available", never a fake value.
- Stack: C# + Avalonia UI 12.x on .NET 10 LTS (supported until Nov 2028).

## Gate 1 — Build (on the Linux VM)
- `dotnet build -c Release` passes with 0 warnings and 0 errors.
- `dotnet test` passes (tests compile and run where the platform allows; on Linux, tests that need Windows APIs are reported honestly as not runnable, never faked).
- No WMIC anywhere. No HttpClient / network calls anywhere (the tool never phones home).
- No secrets, keys, or tokens in the repo. `.env` files never committed.

## Gate 2 — Startup (real Windows, AppOnFly RDP)
- The app window appears within 5 seconds of launch, every time, on a cold start.
- No invisible-process state: the process must have a visible main window (MainWindowHandle != 0), a taskbar icon, and respond to Alt+Tab.
- No UAC prompt on launch (asInvoker). No crash, no hang, no error dialog on first run.
- A startup log file is written (milestones + per-collector timing + errors) so a headless launch leaves evidence.
- The scan never blocks startup: window shows first with a loading state, scan runs async, 30s overall timeout; a timed-out collector reports "Not reported by hardware".

## Gate 3 — Every screen (real Windows)
For EACH screen (Command Center, CPU, RAM, Motherboard, Storage/SSD, Battery, Network/WiFi, GPU): it renders, shows real data where the hardware reports it, and honest "Not reported by hardware" where it does not. No blank screens, no placeholder text, no overlapping text, no unreadable labels, no fake numbers.
- Every button does what it says. Every export works (CSV, JSON, HTML) and the files open cleanly.
- Settings persist across restarts. Snapshot compare works. Printable buyer checklist prints cleanly.
- Honesty rule: RAM/CPU/motherboard life percent and WiFi manufacture date do not exist as sensors — always "Not reported by hardware", never invented.

## Gate 4 — Permissions (real Windows)
- Standard user (no admin): app runs fully, admin-only sensors degrade honestly, no elevation prompt, no crash.
- Administrator: same, plus admin-only sensors show real data where available.
- No file writes outside the user's own folders (LocalAppData for state/logs, chosen folder for exports).

## Gate 5 — Installer
- installer/TrueSpec.iss compiles (Windows-only step) into a working installer.
- Install: completes cleanly, Start Menu + desktop shortcut work, app launches from the shortcut.
- Uninstall: removes the app, shortcuts, and its files; no leftovers except the user's own exported files.

## Gate 6 — Release (GitHub)
- Versioned tag (v1.0.0, then v1.0.1, ...). GitHub Release with: installer exe, portable zip, SHA256 checksums, release notes (what changed, what was fixed, known issues).
- README is accurate: correct Windows versions, correct install steps, correct feature list, no stale claims.
- No release is published from a build that has not passed Gates 1-5.

## Gate 7 — Re-test matrix
- Windows 11 (AppOnFly RDP) AND Windows 10 if a machine is available.
- RDP session AND real hardware (Jawad's Windows laptop) before calling any release final.
- AppOnFly retry: if the trial RDP is not available, retry in an incognito window or a new tab before giving up. Free trial only, never pay, never enter a card.

## History
- P4 (Nov 10, e15b64e): A-to-Z hardening, verified build clean.
- P5 (Nov 11, in progress): non-blocking startup + scan timeouts + startup log — fixes the launch-blocking no-window bug found on AppOnFly (window never appeared, MainWindowHandle=0; cause: MainViewModel constructor blocked the UI thread on ScanAsync().GetAwaiter().GetResult()).
