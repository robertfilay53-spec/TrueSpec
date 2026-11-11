# TrueSpec Security

TrueSpec is a local, offline hardware scanner. This document states the
threat model, what the app does and does not do, and how the code is kept
safe. If you find a security issue, please open a GitHub issue or email the
maintainers through the repository. Do not publish exploit details before a
fix is available.

## Threat model

TrueSpec runs on a machine the user owns or is buying. The assets it
protects are:

- The accuracy of the numbers it shows (a wrong health reading can cost the
  buyer real money).
- The user's local files (reports, snapshot history, settings).
- The user's privacy (nothing about the machine may leave it).

The app is **not** a privileged service. It runs as the invoking user
(`requestedExecutionLevel level="asInvoker"`), never asks for elevation, and
treats every hardware reading as untrusted input until validated.

## What the app does

- Reads hardware through CIM/WMI (`System.Management`), LibreHardwareMonitor
  sensors, raw SATA SMART passthrough, and NVMe reliability counters.
- Runs exactly two read-only external tools, both allowlisted:
  `netsh wlan show interfaces` and `netsh wlan show drivers`. No shell is
  involved (`UseShellExecute = false`), arguments are validated against an
  allowlist (`ExternalToolValidator`), and a run that does not finish in
  5 seconds is killed.
- Stores scan history in a local SQLite database under
  `%LocalApplicationData%\TrueSpec\history.db`. Every query is parameterized;
  no SQL is ever built by string concatenation.
- Writes reports (JSON, HTML, CSV) and the printable buyer checklist only to
  the export folder the user picked, or to `Documents\TrueSpec` by default.
  Export folders are validated by `PathSafety`: the path must be absolute,
  must not be a drive root, a Windows system folder, or a Program Files
  folder, and must not escape through `..` segments.
- Saves settings as JSON under `%LocalApplicationData%\TrueSpec\settings.json`.
  A missing or corrupt file falls back to defaults; every loaded value is
  sanitized (refresh interval clamped to 1-30 seconds, export folder
  re-validated). Settings are written atomically (temp file + move).

## What the app does NOT do

- **No network calls.** There is no `HttpClient`, no socket, no telemetry, no
  analytics, no update check, and no account. Verified by grep in CI review.
  The app manifest requests `asInvoker` only and declares no network
  capability. The "Check for updates" concept was removed from settings
  because an offline app must not promise a network feature.
- **No WMIC.** WMIC was removed in Windows 11 25H2. All Windows reads use
  CIM/WMI only.
- **No fake numbers.** Anything without a real sensor shows
  "Not reported by hardware" or "Not available". The honesty rule is tested
  (`HardwareMathTests`, `CollectorAndScanTests`).
- **No secrets.** The repository contains no passwords, API keys or tokens.
  The app never asks for a password and stores no credentials.
- **No code execution from data.** Hardware strings are data, never code:
  HTML reports HTML-encode every value, and CSV exports neutralize
  spreadsheet-formula injection (values starting with `=`, `+`, `-`, `@`,
  `|` or `%` get a leading tab so Excel treats them as text).

## Input validation

| Input | Validation |
|---|---|
| Export folder (settings) | `PathSafety.ValidateExportFolder`: absolute, not a drive root, not `Windows` / `System32` / `Program Files` |
| Snapshot database path | `PathSafety.ValidateDatabasePath`: must live under the app data folder, `.db` extension only |
| Report file names | `PathSafety.SafeReportFileName`: strips separators and invalid characters |
| External tool calls | `ExternalToolValidator.IsAllowed`: allowlist of tools and exact argument strings |
| SQLite history limit | Clamped to 1-500 rows (`SqliteSnapshotStore.GetHistoryAsync`) |
| Settings file | JSON parse failure falls back to defaults; values sanitized on load |
| Snapshot payloads | `GetSnapshotAsync` returns null on corrupt JSON instead of crashing |

## Dependencies

All NuGet packages are pinned to exact versions in the `.csproj` files.
Audit before every release:

```bash
dotnet list package --vulnerable --include-transitive
```

As of the last audit (TrueSpec v1, .NET 10): **no vulnerable packages** in
any project, including transitive dependencies. If the audit ever reports a
vulnerability, upgrade or replace the package before release; do not ship
with a known vulnerable dependency.

## Build and release checklist

1. `dotnet build TrueSpec.sln -c Release` — 0 warnings, 0 errors.
2. `dotnet test TrueSpec.sln -c Release` — all tests pass (run on Windows;
   the Linux CI build compiles the tests; the Windows CI run executes them).
3. `dotnet list package --vulnerable --include-transitive` — no vulnerable
   packages.
4. `grep` the tree for `HttpClient`, `wmic`, passwords and API keys — none.
5. Sign the installer when code signing is set up (see `installer/`).
