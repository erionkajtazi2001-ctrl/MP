; Inno Setup script. Build with:  ISCC.exe installer\MagicPanel.iss
; Expects the published app in ..\publish (see build.ps1 / the GitHub workflow).

#define AppName "Magic Panel"
#define AppVersion "0.1.0"
#define AppExe "MagicPanel.exe"

[Setup]
AppId={{B6E1C3A2-5D4F-4E7A-9C1B-2F8A7D3E6A10}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Magic Panel
DefaultDirName={autopf}\MagicPanel
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\installer-output
OutputBaseFilename=MagicPanel-Setup
SetupIconFile=..\src\MagicPanel\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Tasks]
; Checked by default, so the desktop shortcut is created unless the user unticks it.
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
