#define PayloadDir GetEnv("SLITHY_FIXTURE_PAYLOAD")
#define OutputPath GetEnv("SLITHY_FIXTURE_OUTPUT")
[Setup]
AppId=SlithySecurityDisposableFixture
AppName=Slithy updater test fixture
AppVersion=0.2.0
DefaultDirName={tmp}\slithy-unused-fixture-default
PrivilegesRequired=lowest
Uninstallable=no
CreateUninstallRegKey=no
DisableProgramGroupPage=yes
CloseApplications=no
RestartApplications=no
OutputDir={#OutputPath}
OutputBaseFilename=fixture-setup
Compression=lzma2
[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
