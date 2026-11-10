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
; before cutting a release. There is no SetupIconFile yet: add a branded
; truespec.ico under src\TrueSpec.App\Assets\ and point the script at it
; before the first release build.

#define AppName "TrueSpec"
#define AppVersion "1.0.0"
#define AppPublisher "TrueSpec"
#define AppURL "https://github.com/robertfilay53-spec/TrueSpec"
#define AppExeName "TrueSpec.App.exe"

[Setup]
AppId={{E5369813-E6BB-49E1-A25F-6AF3B4251361}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
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
; Remove only the app's own data folders, and only if they are empty of
; user files the app did not create. Exports live in Documents\TrueSpec;
; settings, history and the startup log live in %LocalAppData%\TrueSpec
; (PathSafety.DefaultAppDataFolder), NOT in Roaming %APPDATA%.
Type: dirifempty; Name: "{userdocs}\TrueSpec"
Type: dirifempty; Name: "{localappdata}\TrueSpec"
