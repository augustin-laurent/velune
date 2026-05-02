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

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Velune"; Filename: "{app}\Velune.Windows.exe"
Name: "{autodesktop}\Velune"; Filename: "{app}\Velune.Windows.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Velune.Windows.exe"; Description: "{cm:LaunchProgram,Velune}"; Flags: nowait postinstall skipifsilent
