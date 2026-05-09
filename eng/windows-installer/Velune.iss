#define AppVersion GetEnv("VELUNE_VERSION")
#define SourceDir GetEnv("VELUNE_PUBLISH_DIR")
#define OutputDir GetEnv("VELUNE_OUTPUT_DIR")
#define OutputBaseName GetEnv("VELUNE_OUTPUT_BASE_NAME")
#define IconPath GetEnv("VELUNE_ICON_PATH")

[Setup]
AppId={{1D82C7F4-0D56-4B6B-A064-0405248BA1E0}
AppName=Velune
AppVersion={#AppVersion}
AppPublisher=Velune
AppSupportURL=https://github.com/AugustinMusic/velune
DefaultDirName={autopf}\Velune
DefaultGroupName=Velune
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseName}
SetupIconFile={#IconPath}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\Velune.Windows.exe
VersionInfoVersion={#AppVersion}
VersionInfoProductName=Velune
VersionInfoProductVersion={#AppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Velune"; Filename: "{app}\Velune.Windows.exe"
Name: "{autodesktop}\Velune"; Filename: "{app}\Velune.Windows.exe"; Tasks: desktopicon

[Registry]
; PDF association
Root: HKLM; Subkey: "SOFTWARE\Classes\Velune.PDF"; ValueType: string; ValueName: ""; ValueData: "PDF Document - Velune"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\Classes\Velune.PDF\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\Velune.Windows.exe,0"
Root: HKLM; Subkey: "SOFTWARE\Classes\Velune.PDF\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Velune.Windows.exe"" ""%1"""
Root: HKLM; Subkey: "SOFTWARE\Classes\.pdf\OpenWithProgids"; ValueType: string; ValueName: "Velune.PDF"; ValueData: ""; Flags: uninsdeletevalue
; Image associations (OpenWith only, not default)
Root: HKLM; Subkey: "SOFTWARE\Classes\Velune.Image"; ValueType: string; ValueName: ""; ValueData: "Image - Velune"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\Classes\Velune.Image\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\Velune.Windows.exe,0"
Root: HKLM; Subkey: "SOFTWARE\Classes\Velune.Image\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\Velune.Windows.exe"" ""%1"""
Root: HKLM; Subkey: "SOFTWARE\Classes\.png\OpenWithProgids"; ValueType: string; ValueName: "Velune.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\Classes\.jpg\OpenWithProgids"; ValueType: string; ValueName: "Velune.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\Classes\.jpeg\OpenWithProgids"; ValueType: string; ValueName: "Velune.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\Classes\.webp\OpenWithProgids"; ValueType: string; ValueName: "Velune.Image"; ValueData: ""; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\Classes\.bmp\OpenWithProgids"; ValueType: string; ValueName: "Velune.Image"; ValueData: ""; Flags: uninsdeletevalue

[Run]
Filename: "{app}\Velune.Windows.exe"; Description: "{cm:LaunchProgram,Velune}"; Flags: nowait postinstall skipifsilent
