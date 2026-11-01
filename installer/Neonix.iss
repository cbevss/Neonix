; Inno Setup script for the Neonix installer.
; Built by the GitHub workflow:  ISCC /DAppVersion=0.1.0 installer\Neonix.iss

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

[Setup]
AppId={{B7F4C2E1-5A3D-4E8B-9C1F-3D6A2E7B8F40}
AppName=Neonix
AppVersion={#AppVersion}
AppPublisher=Neonix
AppPublisherURL=https://github.com/cbevss/Neonix
AppSupportURL=https://github.com/cbevss/Neonix/issues
DefaultDirName={autopf}\Neonix
DefaultGroupName=Neonix
DisableProgramGroupPage=yes
OutputDir=..\installer-out
OutputBaseFilename=Neonix-Setup-{#AppVersion}
SetupIconFile=..\src\Neonix\Assets\neonix.ico
UninstallDisplayIcon={app}\Neonix.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
LicenseFile=..\LICENSE

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"
Name: "startup"; Description: "Start Neonix with &Windows (minimised to the tray)"; GroupDescription: "Startup:"

[Files]
Source: "..\publish\Neonix.exe"; DestDir: "{app}"; Flags: ignoreversion
; The RGB driver (OpenRGB) is bundled when the build could fetch it.
#ifexist "..\openrgb\OpenRGB.exe"
Source: "..\openrgb\*"; DestDir: "{app}\OpenRGB"; Flags: ignoreversion recursesubdirs createallsubdirs
#endif

[Icons]
Name: "{autoprograms}\Neonix"; Filename: "{app}\Neonix.exe"
Name: "{autodesktop}\Neonix"; Filename: "{app}\Neonix.exe"; Tasks: desktopicon

[Run]
; Neonix needs admin rights, which Windows ignores in the normal Run key, so "start with Windows" is a logon task.
Filename: "schtasks.exe"; Parameters: "/Create /F /TN Neonix /SC ONLOGON /RL HIGHEST /TR ""\""{app}\Neonix.exe\"" --minimized"""; Flags: runhidden; Tasks: startup
Filename: "{app}\Neonix.exe"; Description: "Launch Neonix"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "schtasks.exe"; Parameters: "/Delete /F /TN Neonix"; Flags: runhidden; RunOnceId: "RemoveNeonixTask"
