; Inno Setup Script para Calcpad Suite Py
; Genera un instalador setup.exe
; Variante Python-only: motor Python nativo en C# + fallback a python real.

#define MyAppName "Calcpad Suite Py"
#define MyAppVersion "1.0.4"
#define MyAppPublisher "Jorge Burbano"
#define MyAppURL "https://github.com/GiorgioBurbanelli89/Calcpad-Suite-Py"
#define MyAppExeName "CalcpadSuitePy.exe"
#define MyAppPublishDir "C:\Users\j-b-j\Desktop\CalcpadSuitePy-Installer\CalcpadSuitePy"

[Setup]
AppId={{F1E2D3C4-B5A6-4789-9C0D-1E2F3A4B5C6D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\Calcpad Suite Py
DefaultGroupName=Calcpad Suite Py
AllowNoIcons=yes
LicenseFile=LICENSE
OutputDir=.\Installer
OutputBaseFilename=CalcpadSuitePy-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "fileassoc_py"; Description: "Asociar archivos .py (Python) con Calcpad Suite Py"; GroupDescription: "Asociaciones de archivo:"

[InstallDelete]
; Limpiar Examples viejos antes de copiar — evita .py huérfanos de versiones anteriores.
Type: filesandordirs; Name: "{app}\Examples"

[Files]
; Application files — self-contained .NET 10 publish
Source: "{#MyAppPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

; Examples — scripts .py (Python) curados, bundleados en {app}\Examples.
Source: "Examples-Py\*"; DestDir: "{app}\Examples"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist

; Documentation
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme skipifsourcedoesntexist
Source: "LICENSE"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Examples (bundleados)"; Filename: "{app}\Examples"
Name: "{group}\{cm:ProgramOnTheWeb,{#MyAppName}}"; Filename: "{#MyAppURL}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; .py file association
Root: HKA; Subkey: "Software\Classes\.py\OpenWithProgids"; ValueType: string; ValueName: "CalcpadSuitePy.PyFile"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc_py
Root: HKA; Subkey: "Software\Classes\CalcpadSuitePy.PyFile"; ValueType: string; ValueName: ""; ValueData: "Calcpad Suite Py — Python Document"; Flags: uninsdeletekey; Tasks: fileassoc_py
Root: HKA; Subkey: "Software\Classes\CalcpadSuitePy.PyFile\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: fileassoc_py
Root: HKA; Subkey: "Software\Classes\CalcpadSuitePy.PyFile\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: fileassoc_py

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Iniciar Calcpad Suite Py"; Flags: nowait postinstall skipifsilent
