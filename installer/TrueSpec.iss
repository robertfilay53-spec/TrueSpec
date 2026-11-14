; TrueSpec installer — Inno Setup 6.
;
; Build on Windows only:
;   1. dotnet publish src\TrueSpec.App\TrueSpec.App.csproj -p:PublishProfile=win-x64 -o publish
;   2. iscc installer\TrueSpec.iss
;
; The publish profile (src\TrueSpec.App\Properties\PublishProfiles\win-x64.pubxml)
; is self-contained and single-file: the .NET Desktop Runtime 10 is bundled
; inside TrueSpec.App.exe, so the app runs on a machine with no .NET
; installed and the installer never needs to install a runtime.
;
; The installer only copies the published files, creates shortcuts and the
; standard uninstall entry. It writes nothing else to the system, makes no
; network calls and never installs anything without consent. It does not
; require administrator rights; run it elevated only if you want every
; sensor (some SMART and driver data needs elevation).
; This script is authored and reviewed by hand. It cannot be compiled on
; Linux (Inno Setup is Windows-only), so validate it with `iscc` on Windows
; before cutting a release. The branded icon lives at
; src\TrueSpec.App\Assets\truespec-logo.ico and is referenced below
; via SetupIconFile.

#define AppName "TrueSpec"
#define AppVersion "2.1.16"
#define AppPublisher "Rana Jawad"
#define AppURL "https://github.com/robertfilay53-spec/TrueSpec"
#define AppExeName "TrueSpec.App.exe"

[Setup]
AppId={{E5369813-E6BB-49E1-A25F-6AF3B4251361}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppCopyright=Copyright (C) 2026 Rana Jawad
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
SetupIconFile=..\src\TrueSpec.App\Assets\truespec-logo.ico
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=Output
OutputBaseFilename={#AppName}Setup-{#AppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
UninstallDisplayName={#AppName} {#AppVersion}

; Uninstall-while-running (P9). The installer auto-launches TrueSpec on
; Finish, so the app is usually still running when the user uninstalls,
; and a running single-file exe locks its install-dir file: if the app is
; not closed first, the exe (and the install folder) survives the
; uninstall. CloseApplications asks the running app to close via Restart
; Manager before any file is removed; the [Code] section below posts
; WM_CLOSE to the main window as a deterministic fallback (the single-file
; exe runs from a temp-extracted copy, so Restart Manager does not always
; associate it with the install dir). RestartApplications MUST be no: after
; an uninstall there is nothing to restart into, and the default (yes)
; would try to relaunch the just-deleted app.
CloseApplications=yes
CloseApplicationsFilter=TrueSpec.App.exe
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; The publish folder is produced by `dotnet publish ... -o publish`
; (see the header comment). Everything in it ships as-is.
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; The app-data folder holds ONLY files the app itself created (settings.json,
; history.db, startup.log, window-state.json — all under
; %LocalAppData%\TrueSpec, see PathSafety.DefaultAppDataFolder), so remove
; the whole folder on uninstall: after an uninstall no TrueSpec files may
; remain. Exports live in Documents\TrueSpec and are the user's own files,
; so that folder is removed only if it is empty.
Type: filesandordirs; Name: "{localappdata}\TrueSpec"
Type: dirifempty; Name: "{userdocs}\TrueSpec"

[Code]
// P9: close the running app before uninstalling.
//
// The installer auto-launches TrueSpec on Finish, so the app is usually
// still running when the user uninstalls. A running single-file exe locks
// its install-dir file, so the uninstaller must close the app first or the
// exe (and the install folder) survives the uninstall. Restart Manager
// (CloseApplications, above) is the first line of defence; this is the
// deterministic fallback: post WM_CLOSE to the main window
// ("TrueSpec - Command Center"), which Avalonia handles by shutting the
// app down promptly (no unsaved state, no close prompt), wait up to 10
// seconds, and if the window is still there ask the user to close it or
// cancel the uninstall. Returning False aborts the uninstall cleanly:
// nothing is half-deleted.

const
  WM_CLOSE = $0010;
  UninstallCloseWaitMs = 10000;
  UninstallClosePollMs = 250;

function PostMessage(hWnd: Longint; Msg: Cardinal; wParam: Longint; lParam: Longint): Boolean;
  external 'PostMessageW@user32.dll stdcall';

function CloseRunningApp(): Boolean;
var
  WindowHandle: Longint;
  Waited: Integer;
  Choice: Integer;
begin
  Result := True;
  while FindWindowByWindowName('TrueSpec - Command Center') <> 0 do
  begin
    WindowHandle := FindWindowByWindowName('TrueSpec - Command Center');
    PostMessage(WindowHandle, WM_CLOSE, 0, 0);
    Waited := 0;
    while (Waited < UninstallCloseWaitMs) and (FindWindowByWindowName('TrueSpec - Command Center') <> 0) do
    begin
      Sleep(UninstallClosePollMs);
      Waited := Waited + UninstallClosePollMs;
    end;
    if FindWindowByWindowName('TrueSpec - Command Center') = 0 then
      Break;
    Choice := MsgBox('TrueSpec is still running. Please close it, then click Retry to continue uninstalling, or Cancel to stop the uninstall.',
      mbInformation, MB_RETRYCANCEL);
    if Choice <> IDRETRY then
    begin
      Result := False;
      Break;
    end;
  end;
end;

function InitializeUninstall(): Boolean;
begin
  Result := CloseRunningApp();
end;
