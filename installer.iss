#ifndef MyAppVersion
  #define MyAppVersion "1.1.1"
#endif
#ifndef AppArch
  #define AppArch ""
#endif
#ifndef SourceDir
  #define SourceDir "publish"
#endif
#ifndef OutputFilename
  #if AppArch != ""
    #define OutputFilename "FluentScrobbler-Setup-" + AppArch
  #else
    #define OutputFilename "FluentScrobbler-Setup"
  #endif
#endif

[Setup]
AppId={{8B1A24B7-3E77-4FE8-8F6A-4A053C2D1F98}}
AppName=Fluent Scrobbler
AppVersion={#MyAppVersion}
AppPublisher=Snow Mint
AppPublisherURL=https://github.com/snw-mint/fluent-scrobbler
AppSupportURL=https://github.com/snw-mint/fluent-scrobbler/issues
AppUpdatesURL=https://github.com/snw-mint/fluent-scrobbler/releases
DefaultDirName={localappdata}\FluentScrobbler
MinVersion=10.0.17763
#if AppArch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#elif AppArch == "x64"
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
PrivilegesRequired=lowest
DefaultGroupName=Fluent Scrobbler
UninstallDisplayIcon={app}\FluentScrobbler.exe
Compression=lzma2/ultra64
SolidCompression=yes
OutputDir=.\Output
OutputBaseFilename={#OutputFilename}
WizardStyle=modern
CloseApplications=force
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
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,*.xml"

[Icons]
Name: "{group}\Fluent Scrobbler"; Filename: "{app}\FluentScrobbler.exe"
Name: "{autodesktop}\Fluent Scrobbler"; Filename: "{app}\FluentScrobbler.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "FluentScrobbler"; ValueData: """{app}\FluentScrobbler.exe"" --minimized"; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\FluentScrobbler.exe"; Description: "{cm:LaunchProgram,Fluent Scrobbler}"; Flags: nowait postinstall skipifsilent
