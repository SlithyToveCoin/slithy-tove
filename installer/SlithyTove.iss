#define MyAppName "Slithy Tove"
#define MyAppVersion GetEnv("SLITHY_INSTALLER_VERSION")
#if MyAppVersion == ""
  #error Use installer/build-installer.ps1 to set the version from the project.
#endif
#define MyAppPublisher "CS Idea Labs LLC"
#define MyAppURL "https://slithy.io"
#define MyAppSupportURL "https://slithy.io/bug-report.html"
#define MyAppExeName "Slithy Tove.exe"
#define PublishDir GetEnv("SLITHY_PUBLISH_DIR")
#if PublishDir == ""
  #error Use installer/build-installer.ps1 to create a fresh publish directory.
#endif
#define SigningMode GetEnv("SLITHY_SIGNING_MODE")

[Setup]
AppId={{B6358ED9-8389-4851-85D2-76236D2D1FCB}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppSupportURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\Slithy Tove
DefaultGroupName=Slithy Tove
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=Slithy-Tove-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
WizardImageFile=assets\wizard-large.bmp
WizardSmallImageFile=assets\wizard-small.bmp
SetupIconFile=..\Desktop\Slithy Tove\Slithy Tove\Assets\slithy.ico
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
CloseApplications=yes
CloseApplicationsFilter=Slithy Tove.exe;slithyd.exe
AppMutex=SlithyTove.Desktop.SingleInstance
RestartApplications=no
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=Slithy Tove wallet installer
VersionInfoProductName={#MyAppName}
SetupLogging=yes
#if SigningMode == "azure"
SignedUninstaller=yes
SignTool=azure
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
WelcomeLabel1=Welcome to Slithy Tove
WelcomeLabel2=This installs Slithy Tove for the new beta test network. Your previous wallet files stay on this computer. Old test balances do not carry over.
FinishedHeadingLabel=Slithy Tove is installed
FinishedLabel=You can open the wallet now. Keep your recovery words private and never send them to support.

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked
Name: "launchstartup"; Description: "Start Slithy Tove when Windows starts"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb;*.xml;*.log"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Slithy Tove"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Slithy Tove"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\Slithy Tove"; Filename: "{app}\{#MyAppExeName}"; Tasks: launchstartup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Open Slithy Tove"; Flags: nowait postinstall skipifsilent

[InstallDelete]
Type: files; Name: "{app}\Slithy Tove.pdb"

[UninstallDelete]
; Wallets, chain data, settings, and logs under the user profile are intentionally preserved.
Type: dirifempty; Name: "{app}"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
