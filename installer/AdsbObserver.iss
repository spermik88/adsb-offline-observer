#define AppName "ADS-B Offline Observer"
#define AppVersion "0.1.0"
#define AppPublisher "AdsbObserver"
#define AppExeName "AdsbObserver.App.exe"

[Setup]
AppId={{2A7810FD-2E60-4296-8D93-14B4E9E4105C}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\AdsbObserver
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
OutputDir=..\artifacts\installer
OutputBaseFilename=AdsbObserver-Setup

[Files]
Source: "..\src\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
