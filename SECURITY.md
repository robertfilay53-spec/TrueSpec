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
  re-validated). Settings are written atomically (randomly named temp file
  in the same folder + move, so a fixed temp name can never be pre-planted).
- Runs one instance only, guarded by a named mutex. The mutex name is
  session-scoped (`Local\`) and per-user (base name + 16 hex characters of
  the SHA-256 of the logon identity, `SingleInstanceIdentity`): a fixed
  well-known name would let any other local process create the mutex first
  and block the app from launching. The plain username never appears in
  the name.
- Opens raw physical drives (`\\.\PhysicalDriveN`) only for SATA SMART
  reads, and only with a drive number from WMI that `SataSmartReader`
  validates to 0-255 first: the number lands in the device path and in the
  single DriveNumber byte of the ATA command, so an out-of-range value must
  never reach the P/Invoke boundary.

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
| Single-instance mutex | Per-user name (base + SHA-256 of logon identity); no fixed squattable name |
| Settings temp file | Random name per save (`.truespec-<guid>.tmp`), deleted on failure |
| SMART drive number | Rejected unless 0-255 before the device path is built |

## Reverse engineering and tamper resistance

TrueSpec is MIT open source: the source is public, so **full
reverse-engineering prevention is impossible** and is not claimed. Anyone
can read the code, rebuild it, and patch the binary. What hardening does
provide is tamper *evidence* and a smaller attack surface:

- **No debug backdoors.** There are no test hooks, no hidden flags, no
  hardcoded test values in release builds. The only `#if DEBUG` in the app
  is Avalonia's developer tools in `Program.cs`, compiled out of Release.
  The diagnostics package (`AvaloniaUI.DiagnosticsSupport`) is excluded
  from non-Debug builds in the `.csproj`.
- **Deterministic builds.** `Directory.Build.props` sets
  `<Deterministic>true</Deterministic>`: the same source produces
  byte-identical assemblies. Verify by building twice into two folders
  and comparing SHA-256 hashes; a mismatch means the build is not
  reproducible and must be investigated before release.
- **Tamper-evident release assets.** Every GitHub Release publishes the
  installer and the portable zip **plus SHA-256 checksums** for both
  (see `RELEASE-CHECKLIST.md` Gate 6). A user verifies with
  `Get-FileHash <file> -Algorithm SHA256` and compares against the
  release notes. The release workflow must generate the checksums; if it
  does not, the release is incomplete and must not ship as final.
- **No obfuscation.** Obfuscation is deliberately not used: it would break
  the MIT open-source promise (the shipped binary should match the public
  source), it breaks Avalonia reflection and trimming assumptions, and it
  provides no real protection against a determined reverser.

## Code signing (how to sign, not yet set up)

No signing key exists in the repo and none should ever be committed: a
private key in a public repository is compromised by definition. When
signing is set up, it is done in CI from a secret, never from a file in
the tree:

1. Obtain a code-signing certificate (OV or EV) or use Azure Trusted
   Signing, which holds the key in the cloud and signs via the service.
2. In the release workflow, sign the installer and the portable exe
   after building them and before uploading, for example with
   `signtool sign /tr http://timestamp.digicert.com /td sha256 /fd sha256 /a <file>`
   (or the Trusted Signing equivalent). Timestamping is required so the
   signature stays valid after the certificate expires.
3. Verify with `signtool verify /pa <file>` before upload. An unsigned
   asset must never be uploaded as a release.
4. **Strong-naming is not used.** A strong name proves assembly identity,
   not safety, and the `.snk` private key would need the same secret
   handling as a certificate for no user-facing benefit on a desktop app
   that is not loaded by other assemblies. If strong-naming is ever
   wanted, generate the keypair once, commit only the public key, keep
   the private key in a CI secret, and sign with `PublicSign` in CI.
   Do not commit a `.snk` containing a private key.

## Dependencies

All NuGet packages are pinned to exact versions in the `.csproj` files.
Audit before every release:

```bash
dotnet list package --vulnerable --include-transitive
```

As of the last audit (Nov 14, 2026, TrueSpec v2.1.10, .NET 10):
**no vulnerable packages** in any project, including transitive
dependencies. The audit was run with
`dotnet list package --vulnerable --include-transitive` on all four
projects (TrueSpec.App, TrueSpec.Core, TrueSpec.Collectors.Windows,
TrueSpec.Tests). No package version changed between v2.1.9 and v2.1.10
(verified: `git diff a0b0326 9925866 -- '*csproj'` shows only the
`<Version>` bump and test-file exclusions), so the v2.1.9 audit result
carries forward and was re-run for v2.1.10. The build itself now
fails on a vulnerable package (NuGetAuditMode=all in
Directory.Build.props, with TreatWarningsAsErrors), and the CI
"Check for vulnerable packages" step fails the job when the scan
reports one. If the audit ever reports a vulnerability, upgrade or
replace the package before release; do not ship with a known vulnerable
dependency.

## Build and release checklist

1. `dotnet build TrueSpec.sln -c Release` — 0 warnings, 0 errors.
2. `dotnet test TrueSpec.sln -c Release` — all tests pass (run on Windows;
   the Linux CI build compiles the tests; the Windows CI run executes them).
3. `dotnet list package --vulnerable --include-transitive` — no vulnerable
   packages.
4. `grep` the tree for `HttpClient`, `wmic`, passwords and API keys — none.
5. Sign the installer when code signing is set up (see `installer/`).
