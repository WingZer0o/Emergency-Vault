; Per-user installer (no admin/UAC prompt) that installs to %LOCALAPPDATA%\Programs\Emergency-Vault
; instead of %ProgramFiles% - WebView2's default user-data-folder (next to the exe) needs a location
; a standard user can actually write to; Program Files isn't, which is what broke the .msi build.
#define AppName "Emergency Vault"
#define AppExeName "emergency-vault.exe"
#define SourceDir "..\emergency-vault"

[Setup]
AppName={#AppName}
AppVersion=1.0.0
DefaultDirName={localappdata}\Programs\Emergency-Vault
DefaultGroupName={#AppName}
PrivilegesRequired=lowest
OutputBaseFilename=EmergencyVaultSetup
OutputDir=.
Compression=lzma2
SolidCompression=yes
DisableProgramGroupPage=yes
SetupIconFile=icon\app-icon.ico
UninstallDisplayIcon={app}\app-icon.ico

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; deno desktop's --icon flag doesn't actually patch the compiled exe (it's a cached launcher
; stub reused unmodified across builds - confirmed via its unchanging file timestamp), so
; emergency-vault.exe has no usable embedded icon. Shortcuts below point at this file directly
; instead of extracting one from the exe.
Source: "icon\app-icon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\app-icon.ico"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"; IconFilename: "{app}\app-icon.ico"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\app-icon.ico"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
