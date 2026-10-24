#ifndef PublishDir
  #define PublishDir "..\artifacts\release-1.2.6"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif
[Setup]
AppId={{5F71F699-4E43-4A4A-A8DE-984A82B030B0}
AppName=KeyEcho
AppVersion=1.2.6
AppPublisher=KeyEcho
DefaultDirName={localappdata}\Programs\KeyEcho
DefaultGroupName=KeyEcho
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir={#OutputDir}
OutputBaseFilename=KeyEcho-Setup-1.2.6-x64
SetupIconFile=..\src\KeyEcho.App\Assets\KeyEcho.ico
UninstallDisplayIcon={app}\KeyEcho.App.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
AppMutex=Local\KeyEcho.App
UninstallDisplayName=KeyEcho 键鼠音效

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\KeyEcho"; Filename: "{app}\KeyEcho.App.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\KeyEcho"; Filename: "{app}\KeyEcho.App.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\KeyEcho.App.exe"; Description: "启动 KeyEcho"; Flags: nowait postinstall skipifsilent

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "KeyEcho"; Flags: uninsdeletevalue

; User settings and imported recordings are intentionally retained during uninstall.

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and RegValueExists(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'KeyEcho') then
    RegWriteStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'KeyEcho', ExpandConstant('"{app}\KeyEcho.App.exe" --background'));
end;
