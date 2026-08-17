[Setup]
AppName=Fluent Scrobbler
AppVersion=0.3.1
AppPublisher=Snow Mint
AppPublisherURL=https://github.com/snw-mint/fluent-scrobbler
AppSupportURL=https://github.com/snw-mint/fluent-scrobbler/issues
AppUpdatesURL=https://github.com/snw-mint/fluent-scrobbler/releases
DefaultDirName={localappdata}\Fluent Scrobbler
PrivilegesRequired=lowest
DefaultGroupName=Fluent Scrobbler
UninstallDisplayIcon={app}\Fluent Scrobbler.exe
Compression=lzma2/ultra64
SolidCompression=yes
OutputDir=.\Output
OutputBaseFilename=Fluent Scrobbler-Setup
WizardStyle=modern
CloseApplications=yes
CloseApplicationsFilter=Fluent Scrobbler.exe
SetupIconFile=Assets\AppIcon.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Start Fluent Scrobbler with Windows"; GroupDescription: "Additional options:"

[Files]
Source: "bin\Release\net8.0-windows10.0.26100.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\Fluent Scrobbler"; Filename: "{app}\Fluent Scrobbler.exe"
Name: "{autodesktop}\Fluent Scrobbler"; Filename: "{app}\Fluent Scrobbler.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Fluent Scrobbler"; ValueData: """{app}\Fluent Scrobbler.exe"" --minimized"; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\Fluent Scrobbler.exe"; Description: "{cm:LaunchProgram,Fluent Scrobbler}"; Flags: nowait postinstall skipifsilent
