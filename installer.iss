#ifndef MyAppVersion
  #define MyAppVersion "0.5.0"
#endif

[Setup]
AppName=Fluent Scrobbler
AppVersion={#MyAppVersion}
AppPublisher=Snow Mint
AppPublisherURL=https://github.com/snw-mint/fluent-scrobbler
AppSupportURL=https://github.com/snw-mint/fluent-scrobbler/issues
AppUpdatesURL=https://github.com/snw-mint/fluent-scrobbler/releases
DefaultDirName={localappdata}\FluentScrobbler
PrivilegesRequired=lowest
DefaultGroupName=Fluent Scrobbler
UninstallDisplayIcon={app}\FluentScrobbler.exe
Compression=lzma2/ultra64
SolidCompression=yes
OutputDir=.\Output
OutputBaseFilename=FluentScrobbler-Setup
WizardStyle=modern
CloseApplications=yes
CloseApplicationsFilter=FluentScrobbler.exe
SetupIconFile=Assets\AppIcon.ico
LicenseFile=Repo\setup\LICENSE.txt
WizardImageFile=Repo\setup\WizardImageFile.bmp
WizardSmallImageFile=Repo\setup\WizardSmallImageFile.bmp
DisableWelcomePage=no
DisableFinishedPage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Start Fluent Scrobbler with Windows"; GroupDescription: "Additional options:"
[Files]
Source: "bin\Release\net8.0-windows10.0.26100.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,*.xml,*.deps.json"

[Icons]
Name: "{group}\Fluent Scrobbler"; Filename: "{app}\FluentScrobbler.exe"
Name: "{autodesktop}\Fluent Scrobbler"; Filename: "{app}\FluentScrobbler.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "FluentScrobbler"; ValueData: """{app}\FluentScrobbler.exe"" --minimized"; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\FluentScrobbler.exe"; Description: "{cm:LaunchProgram,Fluent Scrobbler}"; Flags: nowait postinstall skipifsilent
