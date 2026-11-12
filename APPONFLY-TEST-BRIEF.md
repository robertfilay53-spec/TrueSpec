# AppOnFly Test Brief — TrueSpec (publish + run)

Reusable brief for verifying a TrueSpec build live on a real Windows machine.
Use it for the v1.1 build check and any later build check. A monitor or a
human runs the same steps.

## Session rules

- Free AppOnFly trial only (20 min). NEVER sign in, NEVER pay, NEVER enter a
  card anywhere. If the session redirects to a Subscribe/sign-in page or a
  Guacamole sign-in page, STOP and report — never sign in or pay.
- AppOnFly only offers Windows Server (no Windows 10/11 option). Note that
  honestly; it is not a test failure.
- Close all tabs/windows when done.

## Setup (fast path — publish only, no installer)

1. Install .NET SDK 10: download dotnet-install.ps1, run with
   `-Channel 10.0` to `C:\dotnet`, add `C:\dotnet` to the session PATH,
   verify `dotnet --version` (expect 10.0.404). winget has NO .NET SDK 10.
2. Download the repo ZIP from GitHub (robertfilay53-spec/TrueSpec, main,
   at the commit under test). It expands to `TrueSpec-main`, sometimes
   nested one level deeper — find `TrueSpec.sln`.
3. Publish only (saves time — a full Release build takes ~318s on the
   1-core VM, publish ~56-109s):
   `dotnet publish src/TrueSpec.App/TrueSpec.App.csproj -c Release -o C:\TrueSpecTest`
   If NuGet restore fails with "Root element is missing", delete the 0-byte
   `%APPDATA%\NuGet\NuGet.Config` and retry.

## Run and verify

4. Launch `C:\TrueSpecTest\TrueSpec.App.exe`. Verify: window opens, scan
   completes, a score is shown, the status bar shows the version under
   test (e.g. "TrueSpec 1.1.0 (v1)") and "Developed by Rana Jawad", no
   crash, no elevation prompt.
5. Check the v1.1 features:
   - Storage volumes: per-drive-letter total/free space and filesystem
     are shown.
   - Windows Update status: last successful update install and the
     installed-hotfix list are shown; if the machine is managed
     (WSUS), the UI says so instead of implying it is unpatched.
   - Battery-health alerts: the History page loads and shows the alert
     rule/band note where applicable. On this VM the battery is likely
     "Not measurable" — that is honest reporting, not a failure; verify
     the page loads and the alert UI exists.
6. Honesty checks: any value the hardware does not report says
   "Not reported by hardware" (never a fake number). CPU temperature
   0.0 C on the VM is expected ("Not reported by hardware").
7. Standard-user test ONLY if time remains after steps 4-6: create a
   TestUser, launch with
   `Start-Process -Credential -LoadUserProfile -WorkingDirectory C:\TrueSpecTest`,
   verify the window opens, the scan completes, and there is no UAC
   prompt and no crash.

## RDP typing quirk

Type a colon directly — Shift+; produces a semicolon in RDP and breaks
URLs. Do NOT use `choco` (it is NOT installed on AppOnFly VMs).

## Report

Publish result, app run result (score + version + credit visible), each
v1.1 feature (pass/fail + what was actually observed), standard-user
result if done. Note the time left on the trial when you finish.
