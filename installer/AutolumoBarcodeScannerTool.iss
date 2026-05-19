; AutolumoBarcodeScannerTool — Inno Setup Script
; Compilar con: ISCC.exe installer\AutolumoBarcodeScannerTool.iss

#define MyAppName "Autolumo Barcode Scanner Tool"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "Labotech"
#define MyAppExeName "AutolumoBarcodeScannerTool.exe"

[Setup]
AppId={{B7C49E3A-7C2F-4A6D-9F11-0AE3D6E5E0F1}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Autolumo\BarcodeScannerTool
DefaultGroupName=Autolumo
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=AutolumoBarcodeScannerTool-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0.17763
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "autostart"; Description: "Iniciar al iniciar sesión de Windows"; GroupDescription: "Opciones:"
Name: "launch"; Description: "Ejecutar al finalizar la instalación"; GroupDescription: "Opciones:"

[Files]
Source: "..\src\AutolumoBarcodeScannerTool\bin\Release\net10.0-windows\win-x64\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\src\AutolumoBarcodeScannerTool\bin\Release\net10.0-windows\win-x64\publish\appsettings.default.ini"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
    ValueName: "AutolumoBarcodeScannerTool"; \
    ValueData: """{app}\{#MyAppExeName}"" --minimized"; \
    Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Ejecutar"; Flags: postinstall nowait skipifsilent; Tasks: launch
