; Inno Setup script — The Markdown Web native client installer.
; (Code signing via Azure Trusted Signing is enabled in release-windows.yml; both this installer
; and the bundled app exe are Authenticode-signed when SIGNING_ENABLED is set.)
;
; Packages the SELF-CONTAINED win-x64 publish output of clients/windows/App
; (TheMarkdownWeb.App.exe + its .NET runtime + Rendering/Markdig/ColorCode) into a
; single setup.exe with a wizard, Start-Menu + optional Desktop shortcut, and an
; uninstaller. Self-contained => the end user needs NO .NET runtime preinstalled.
;
; Built in CI (release-windows.yml) on windows-latest:
;   dotnet publish clients/windows/App -c Release -r win-x64 --self-contained true -o publish
;   iscc /DPublishDir=<abs publish path> /DAppVersion=<version> clients/windows/installer/TheMarkdownWeb.iss
;
; NOTE on SmartScreen: this installer is UNSIGNED (no code-signing certificate yet), so Windows
; SmartScreen shows an "unknown publisher" prompt on first run (More info -> Run anyway). Buying an
; OV/EV code-signing cert and signing both the .exe and setup.exe removes that warning — tracked as
; deferred work; it does not block distribution.

#ifndef PublishDir
  #define PublishDir "..\App\bin\Release\net10.0-windows\win-x64\publish"
#endif
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

#define AppName "The Markdown Web"
#define AppPublisher "The Markdown Web"
#define AppExeName "TheMarkdownWeb.App.exe"
#define AppUrl "https://themarkdownweb.com"

[Setup]
AppId={{8B1F2E94-7C3D-4A5B-9E6F-2D4C6A8B0E13}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
DefaultDirName={autopf}\TheMarkdownWeb
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Per-user install by default (no admin/UAC prompt) — friendliest for a "download and try" flow.
PrivilegesRequiredOverridesAllowed=dialog
PrivilegesRequired=lowest
OutputDir=installer-out
; STABLE filename (no version) so https://github.com/<repo>/releases/latest/download/TheMarkdownWeb-Setup.exe
; always resolves to the newest installer. The version lives in AppVersion + the Release tag/title.
OutputBaseFilename=TheMarkdownWeb-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
; Version/publisher metadata embedded in setup.exe — good hygiene, and it pairs with code signing.
VersionInfoVersion={#AppVersion}
VersionInfoProductVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Setup

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Recurse the entire self-contained publish folder (exe + runtime + dependency dlls).
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; Offer to launch the client right after install.
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
