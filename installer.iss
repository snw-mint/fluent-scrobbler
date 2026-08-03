[Setup]
AppName=Fluent Scrobbler
AppVersion=0.2.0
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

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Launch Fluent Scrobbler at Windows startup"; GroupDescription: "Additional options:"; Flags: unchecked

[Files]
Source: "bin\Release\net8.0-windows10.0.26100.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\Fluent Scrobbler"; Filename: "{app}\FluentScrobbler.exe"
Name: "{autodesktop}\Fluent Scrobbler"; Filename: "{app}\FluentScrobbler.exe"; Tasks: desktopicon
Name: "{userstartup}\Fluent Scrobbler"; Filename: "{app}\FluentScrobbler.exe"; Tasks: autostart

[Run]
Filename: "{app}\FluentScrobbler.exe"; Description: "{cm:LaunchProgram,Fluent Scrobbler}"; Flags: nowait postinstall skipifsilent