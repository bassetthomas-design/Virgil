; Inno Setup script for the Virgil application
; This script packages the published Virgil application into a Windows installer.

[Setup]
AppName=Virgil
AppVersion=0.1.0
AppPublisher=Virgil Project
AppPublisherURL=https://github.com/bassetthomas-design/Virgil
DefaultDirName={pf}\Virgil
DefaultGroupName=Virgil
OutputBaseFilename=VirgilSetup
Compression=lzma
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
DisableDirPage=no
DisableProgramGroupPage=no
; Show a custom license file if you add one. For now, no license page.
LicenseFile=
; Include a Readme if desired
InfoAterFile=
UninstallDisplayIcon={app}\Virgil.App.exe

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: desktopicon; Description: "Crée une icône sur le Bureau"; Flags: unchecked
Name: startmenu; Description: "Crée une icône dans le menu Démarrer"; Flags: unchecked

[Files]
; Copy all published files from the relative publish folder into the application directory.
; Before running Inno Setup, be sure to build your application with
;   dotnet publish -c Release -r win-x64 --self-contained false -o publish
; and run this script from the root of the repository. Adjust the Source path as needed.
Source: "publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

[Icons]
; Shortcuts based on the selected tasks
Name: "{group}\Virgil"; Filename: "{app}\Virgil.App.exe"; Tasks: startmenu
Name: "{commondesktop}\Virgil"; Filename: "{app}\Virgil.App.exe"; Tasks: desktopicon

[Run]
; Optionally launch the application after installation
Filename: "{app}\Virgil.App.exe"; Description: "Lancer Virgil"; Flags: nowait postinstall skipifsilent
