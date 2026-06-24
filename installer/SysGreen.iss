; SysGreen installer (ADR-0009): a traditional, per-machine, unpackaged installer.
; Installs to Program Files and runs elevated, because the co-located SysGreen.Helper.exe
; carries a requireAdministrator manifest (ADR-0004) and the app writes HKLM autostart state.
;
; Built by installer/build.ps1 (dotnet publish self-contained -> ISCC) or the
; .github/workflows/installer.yml GitHub Actions workflow. build.ps1 injects the version and the
; payload path: ISCC ... /DMyAppVersion=<Directory.Build.props Version> /DPayloadDir=<publish dir>.

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#ifndef PayloadDir
  #define PayloadDir "..\artifacts\publish"
#endif

#define MyAppName "SysGreen"
#define MyAppPublisher "SysGreen"
#define MyAppExeName "SysGreen.App.exe"
#define MyAppUrl "https://github.com/xer4yx/SysGreen"

[Setup]
; Stable AppId — keep constant across versions so upgrades replace in place.
AppId={{C62ED894-1455-491B-A823-773A3710FB43}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}
VersionInfoVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Per-machine: needs admin to write Program Files (and matches the Helper's elevation model).
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\assets\logo.ico
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=SysGreen-{#MyAppVersion}-setup

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; The published payload co-locates SysGreen.App.exe + SysGreen.Helper.exe + SysGreen.Agent.exe +
; knowledge-base.json + the runtime (self-contained), exactly as the app expects at runtime.
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Launch as the original (non-elevated) user — the UI runs non-elevated (ADR-0004); it elevates the
; Helper on demand. Without runasoriginaluser the app would inherit the installer's admin token.
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallRun]
; The resident Tray Agent (ADR-0014) and/or the app may be running; stop them so their exes aren't
; locked while the uninstaller removes files. Best-effort, hidden, ignore failures.
Filename: "{sys}\taskkill.exe"; Parameters: "/IM SysGreen.Agent.exe /F"; Flags: runhidden; RunOnceId: "StopAgent"
Filename: "{sys}\taskkill.exe"; Parameters: "/IM SysGreen.App.exe /F"; Flags: runhidden; RunOnceId: "StopApp"
