#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#ifndef MySourceDir
  #error "MySourceDir must point to the published application directory."
#endif

#ifndef MyOutputDir
  #error "MyOutputDir must point to the installer output directory."
#endif

[Setup]
AppId={{749E417E-E86A-4B7A-9E4D-475026AEDF61}
AppName=Wineclouds Studio
AppVersion={#MyAppVersion}
AppPublisher=Wineclouds Studio
DefaultDirName={autopf}\Wineclouds Studio
DefaultGroupName=Wineclouds Studio
DisableProgramGroupPage=yes
OutputDir={#MyOutputDir}
OutputBaseFilename=WinecloudsStudio-Setup-{#MyAppVersion}-win-x64
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\WinecloudsStudio.exe
CloseApplications=yes

[Files]
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Icons]
Name: "{group}\Wineclouds Studio"; Filename: "{app}\WinecloudsStudio.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Wineclouds Studio"; Filename: "{app}\WinecloudsStudio.exe"; WorkingDir: "{app}"; Tasks: desktopicon
