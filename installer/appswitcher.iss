; Inno Setup script for AppSwitcher
; Requires: Inno Setup 6 (ISCC.exe)

#define MyAppName "AppSwitcher"
#ifndef MyAppVersion
#define MyAppVersion "0.1.1"
#endif
#define MyAppPublisher "G3dar"
#define MyAppURL "https://github.com/G3dar/worktools"
#define MyAppExeName "AppSwitcher.exe"

; Path to published EXE (built by apps\AppSwitcher\build.bat)
#define PublishDir "..\\apps\\AppSwitcher\\bin\\Release\\net8.0-windows\\win-x64\\publish"

[Setup]
AppId={{07B8C0FA-658E-4B97-A5C2-8A6F9E19C7FD}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename={#MyAppName}-{#MyAppVersion}-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
WizardStyle=modern
SetupIconFile="appsetup.ico"

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#PublishDir}\\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked

[Run]
Filename: "{app}\\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent


