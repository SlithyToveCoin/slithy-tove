#requires -Version 7.2
<#
Run this on a fresh, disposable Windows machine. It installs the public release
with its real AppId, checks it, and uninstalls it. It never removes wallet data.
The guards run before any download or write. Do not weaken them to use your PC.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('GitHubHosted', 'DisposableVM')]
    [string]$EnvironmentKind,
    [Parameter(Mandatory)]
    [switch]$ConfirmDisposableMachine,
    [switch]$PreflightOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$version = '0.1.44'
$installerUri = 'https://slithy.io/downloads/windows/Slithy-Tove-Setup-0.1.44.exe'
$installerHash = 'D2C55C6866650A09942B6EBBA929BED163ADE73E93F782289942E6E8EFEC60D9'
$appId = '{B6358ED9-8389-4851-85D2-76236D2D1FCB}_is1'
$uninstallRoot = 'Software\Microsoft\Windows\CurrentVersion\Uninstall'

function Assert-Check([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Get-SlithyRegistration {
    foreach ($hive in @([Microsoft.Win32.RegistryHive]::CurrentUser, [Microsoft.Win32.RegistryHive]::LocalMachine)) {
        foreach ($view in @([Microsoft.Win32.RegistryView]::Registry64, [Microsoft.Win32.RegistryView]::Registry32)) {
            $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey($hive, $view)
            try {
                $parent = $base.OpenSubKey($uninstallRoot)
                if ($null -eq $parent) { continue }
                try {
                    foreach ($name in $parent.GetSubKeyNames()) {
                        $key = $parent.OpenSubKey($name)
                        if ($null -eq $key) { continue }
                        try {
                            $display = [string]$key.GetValue('DisplayName', '')
                            if ($name -eq $appId -or $display -like '*Slithy*') {
                                [pscustomobject]@{
                                    Hive = $hive.ToString(); View = $view.ToString(); Key = $name
                                    DisplayName = $display; Version = [string]$key.GetValue('DisplayVersion', '')
                                    Publisher = [string]$key.GetValue('Publisher', '')
                                    Location = [string]$key.GetValue('InstallLocation', '')
                                    UninstallString = [string]$key.GetValue('UninstallString', '')
                                }
                            }
                        } finally { $key.Dispose() }
                    }
                } finally { $parent.Dispose() }
            } finally { $base.Dispose() }
        }
    }
}

function Assert-NoReparsePoint([string]$Path) {
    $current = [IO.Path]::GetFullPath($Path)
    while ($current) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force
            Assert-Check (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) "A test path uses a link or junction: $current"
        }
        $current = [IO.Path]::GetDirectoryName($current)
    }
}

# An explicit disposable-machine acknowledgement is required even in CI.
Assert-Check ($IsWindows -and [Environment]::Is64BitProcess) 'Use 64-bit PowerShell on a disposable Windows machine.'
Assert-Check ([bool]$ConfirmDisposableMachine) 'Pass -ConfirmDisposableMachine after checking that this machine can be discarded.'
if ($EnvironmentKind -eq 'GitHubHosted') {
    Assert-Check ($env:GITHUB_ACTIONS -eq 'true' -and $env:RUNNER_ENVIRONMENT -eq 'github-hosted') 'GitHubHosted requires a GitHub-hosted runner. Self-hosted runners are not accepted.'
} else {
    Assert-Check ([string]::IsNullOrEmpty($env:GITHUB_ACTIONS)) 'Use GitHubHosted for GitHub Actions. Do not use a self-hosted runner.'
}

$localData = [Environment]::GetFolderPath('LocalApplicationData')
$roamingData = [Environment]::GetFolderPath('ApplicationData')
$installRoot = Join-Path $localData 'Programs/Slithy Tove'
$profileRoot = Join-Path $roamingData 'Slithy Tove'
$shortcutPaths = @(
    (Join-Path ([Environment]::GetFolderPath('Programs')) 'Slithy Tove/Slithy Tove.lnk'),
    (Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'Slithy Tove.lnk'),
    (Join-Path ([Environment]::GetFolderPath('Startup')) 'Slithy Tove.lnk')
)
$blockedPaths = @(
    $installRoot, $profileRoot, (Join-Path $localData 'Slithy Tove'),
    (Join-Path $roamingData 'Slithy'), (Join-Path $localData 'Slithy'),
    (Join-Path $env:USERPROFILE '.slithy'), (Join-Path $env:USERPROFILE '.slithy-tove'),
    (Join-Path $env:ProgramFiles 'Slithy Tove'), (Join-Path ${env:ProgramFiles(x86)} 'Slithy Tove'),
    (Join-Path ([Environment]::GetFolderPath('CommonApplicationData')) 'Slithy Tove'),
    (Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'Slithy Tove'),
    (Join-Path ([Environment]::GetFolderPath('CommonDesktopDirectory')) 'Slithy Tove.lnk'),
    (Join-Path ([Environment]::GetFolderPath('CommonStartup')) 'Slithy Tove.lnk')
) + $shortcutPaths
Assert-Check (@(Get-SlithyRegistration).Count -eq 0) 'A Slithy installation is registered. Nothing was changed.'
foreach ($path in $blockedPaths) {
    Assert-Check (-not (Test-Path -LiteralPath $path)) "Existing Slithy files were found at $path. Nothing was changed."
    Assert-NoReparsePoint $path
}
$running = @(Get-Process | Where-Object { $_.ProcessName -match '^(Slithy Tove|slithyd|slithy-cli|slithy-wallet|slithy-tx|Slithy-Tove.*Setup.*)$' })
Assert-Check ($running.Count -eq 0) 'A Slithy process is running. Nothing was changed.'
$mutex = $null
if ([Threading.Mutex]::TryOpenExisting('SlithyTove.Desktop.SingleInstance', [ref]$mutex)) {
    $mutex.Dispose()
    throw 'The Slithy app mutex exists. Nothing was changed.'
}
foreach ($port in @(42629, 42631, 42633, 42634)) {
    Assert-Check (@(Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue).Count -eq 0) "Regtest port $port is already in use. Nothing was changed."
}
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
Assert-NoReparsePoint $tempRoot
Assert-Check ([IO.DriveInfo]::new([IO.Path]::GetPathRoot($installRoot)).AvailableFreeSpace -ge 3GB) 'The installation drive needs at least 3 GiB free.'
Assert-Check ([IO.DriveInfo]::new([IO.Path]::GetPathRoot($tempRoot)).AvailableFreeSpace -ge 3GB) 'The temporary drive needs at least 3 GiB free.'
if ($PreflightOnly) { Write-Output 'PASS Fresh disposable Windows prerequisites. Nothing was changed.'; return }

$resultsRoot = Join-Path $tempRoot ('slithy-installer-validation-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($resultsRoot) | Out-Null
Write-Output "Evidence directory: $resultsRoot"
if ($env:GITHUB_OUTPUT) { "evidence=$resultsRoot" | Add-Content -LiteralPath $env:GITHUB_OUTPUT -Encoding utf8 }
$checks = [Collections.Generic.List[object]]::new()
$signatures = [Collections.Generic.List[object]]::new()
$guiProcess = $null

function Save-Json($Value, [string]$Name) {
    $Value | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $resultsRoot $Name) -Encoding utf8
}

function Pass([string]$Name) {
    $checks.Add([pscustomobject]@{ Check = $Name; Passed = $true; Utc = [DateTimeOffset]::UtcNow })
    Save-Json $checks 'checks.json'
    Write-Output "PASS $Name"
}

function Check-Signature([string]$Path, [string]$Label) {
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    Assert-Check ($signature.Status -eq 'Valid') "$Label has no valid Windows Authenticode signature: $($signature.Status)"
    $publisher = $signature.SignerCertificate.GetNameInfo([Security.Cryptography.X509Certificates.X509NameType]::SimpleName, $false)
    Assert-Check ($publisher -ceq 'CS Idea Labs LLC') "$Label has an unexpected publisher."
    Assert-Check ($null -ne $signature.TimeStamperCertificate) "$Label has no signing timestamp."
    $signatures.Add([pscustomobject]@{
        File = $Label; Sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
        Status = $signature.Status.ToString(); Publisher = $publisher
        Certificate = $signature.SignerCertificate.Thumbprint
        TimestampAuthority = $signature.TimeStamperCertificate.GetNameInfo([Security.Cryptography.X509Certificates.X509NameType]::SimpleName, $false)
    })
    Save-Json $signatures 'signatures.json'
}

function Run-Program([string]$File, [string[]]$Arguments, [string]$Label, [int]$TimeoutSeconds = 180, [switch]$AllowFailure) {
    $start = [Diagnostics.ProcessStartInfo]::new($File)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $output = $process.StandardOutput.ReadToEndAsync()
        $errors = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw "$Label exceeded its time limit. Its test process was stopped."
        }
        $text = $output.GetAwaiter().GetResult() + $errors.GetAwaiter().GetResult()
        $text = $text -replace '(?i)(rpcpassword=)[^\s]+', '$1[redacted]'
        $text = $text -replace '(https?://)[^\s/@:]+:[^\s/@]+@', '$1[redacted]@'
        # Inno writes its own log. Captured stdout must not overwrite that file.
        $text | Set-Content -LiteralPath (Join-Path $resultsRoot "$Label.process.log") -Encoding utf8
        if ($AllowFailure) { return [pscustomobject]@{ ExitCode = $process.ExitCode; Output = $text.Trim() } }
        Assert-Check ($process.ExitCode -eq 0) "$Label failed with exit code $($process.ExitCode)."
    } finally { $process.Dispose() }
}

function Check-Registration {
    $registrations = @(Get-SlithyRegistration)
    Save-Json $registrations 'registration.json'
    Assert-Check ($registrations.Count -ge 1) 'The installer did not register an uninstall entry.'
    foreach ($entry in $registrations) {
        Assert-Check ($entry.Hive -eq 'CurrentUser' -and $entry.Key -eq $appId) 'The installer wrote an unexpected or machine-wide registration.'
        Assert-Check ($entry.Version -eq $version -and $entry.Publisher -ceq 'CS Idea Labs LLC') 'The registered version or publisher is wrong.'
        Assert-Check ($entry.Location.TrimEnd('\') -ieq $installRoot) 'The registered installation directory is wrong.'
    }
}

function Check-Shortcut([string]$Path) {
    Assert-Check (Test-Path -LiteralPath $Path -PathType Leaf) "A shortcut is missing: $Path"
    $shell = New-Object -ComObject WScript.Shell
    try {
        $shortcut = $shell.CreateShortcut($Path)
        try {
            Assert-Check ($shortcut.TargetPath -ieq (Join-Path $installRoot 'Slithy Tove.exe')) "A shortcut points to the wrong program: $Path"
            Assert-Check ([string]::IsNullOrWhiteSpace($shortcut.Arguments)) "A shortcut has unexpected arguments: $Path"
        } finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut) }
    } finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) }
}

function Get-CanaryInventory {
    foreach ($file in Get-ChildItem -LiteralPath $profileRoot -File -Recurse) {
        [pscustomobject]@{ Path = [IO.Path]::GetRelativePath($profileRoot, $file.FullName); Sha256 = (Get-FileHash -LiteralPath $file.FullName).Hash }
    }
}

function Check-Canaries($Before) {
    foreach ($entry in $Before) {
        $file = Join-Path $profileRoot $entry.Path
        Assert-Check (Test-Path -LiteralPath $file -PathType Leaf) "The installer removed fixture data: $($entry.Path)"
        Assert-Check ((Get-FileHash -LiteralPath $file).Hash -eq $entry.Sha256) "The installer changed fixture data: $($entry.Path)"
    }
}

try {
    $installer = Join-Path $resultsRoot 'Slithy-Tove-Setup-0.1.44.exe'
    $handler = [Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $client = [Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromMinutes(3)
    $client.MaxResponseContentBufferSize = 200MB
    try {
        $response = $client.GetAsync($installerUri).GetAwaiter().GetResult()
        try {
            Assert-Check ([int]$response.StatusCode -eq 200) 'The pinned installer URL did not return HTTP 200. Redirects are not accepted.'
            $bytes = $response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
            Assert-Check ($bytes.Length -gt 1MB -and $bytes.Length -le 200MB) 'Unexpected installer size.'
            [IO.File]::WriteAllBytes($installer, $bytes)
            $bytes = $null
        } finally { $response.Dispose() }
    } finally { $client.Dispose(); $handler.Dispose() }
    Assert-Check ((Get-FileHash -LiteralPath $installer).Hash -eq $installerHash) 'The published installer does not match the reviewed release hash.'
    Check-Signature $installer 'public installer'
    Pass 'Pinned public installer hash, publisher, trust chain and timestamp'

    # No /DIR override and no alternate AppId: this is the real per-user installation.
    $setupFlags = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/SP-', '/NORESTART', '/NOCLOSEAPPLICATIONS', '/NORESTARTAPPLICATIONS')
    Run-Program $installer ($setupFlags + @('/TASKS=', ('/LOG=' + (Join-Path $resultsRoot 'install.log')))) 'fresh-install' 300
    Check-Registration
    $app = Join-Path $installRoot 'Slithy Tove.exe'
    Assert-Check ((Get-Item -LiteralPath $app).VersionInfo.ProductVersion -eq $version) 'The installed application version is wrong.'
    foreach ($relative in @('Slithy Tove.exe', 'Slithy Tove.dll', 'Binaries/slithyd.exe', 'Binaries/slithy-cli.exe', 'Binaries/slithy-wallet.exe', 'Binaries/slithy-tx.exe')) {
        Check-Signature (Join-Path $installRoot $relative) $relative
    }
    $uninstallers = @(Get-ChildItem -LiteralPath $installRoot -Filter 'unins*.exe' -File)
    Assert-Check ($uninstallers.Count -eq 1) 'Expected one installed uninstaller.'
    $uninstaller = $uninstallers[0].FullName
    Check-Signature $uninstaller 'installed uninstaller'
    $registered = @(Get-SlithyRegistration)[0]
    Assert-Check ($registered.UninstallString.Trim('"') -ieq $uninstaller) 'The registered uninstaller path is wrong.'
    Check-Shortcut $shortcutPaths[0]
    Assert-Check (-not (Test-Path -LiteralPath $shortcutPaths[1]) -and -not (Test-Path -LiteralPath $shortcutPaths[2])) 'Optional Desktop or Startup shortcut was created without being selected.'
    Pass 'Fresh install, per-user registration, signed payload and default Start Menu shortcut'

    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class SlithyInstallerWindowProbe {
    public delegate bool EnumCallback(IntPtr handle, IntPtr state);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumCallback callback, IntPtr state);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr handle, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr handle, StringBuilder text, int max);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr handle, uint message, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr handle, IntPtr dc, uint flags);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr handle, out Rect rect);
    public static IntPtr FindTerms(int process) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((handle, state) => {
            uint owner; GetWindowThreadProcessId(handle, out owner);
            if (owner != process) return true;
            var text = new StringBuilder(512); GetWindowText(handle, text, 512);
            if (text.ToString() == "Slithy Tove | Terms of Use") { found = handle; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
'@
    $launch = [Diagnostics.ProcessStartInfo]::new($app)
    $launch.UseShellExecute = $false
    $guiProcess = [Diagnostics.Process]::Start($launch)
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    $window = [IntPtr]::Zero
    while ($window -eq [IntPtr]::Zero -and [DateTime]::UtcNow -lt $deadline -and -not $guiProcess.HasExited) {
        Start-Sleep -Milliseconds 250
        $window = [SlithyInstallerWindowProbe]::FindTerms($guiProcess.Id)
    }
    Assert-Check ($window -ne [IntPtr]::Zero) 'The installed application did not open its first-run terms window.'
    Assert-Check (@(Get-Process -Name slithyd -ErrorAction SilentlyContinue).Count -eq 0) 'A node started before the terms were accepted.'
    Save-Json @{ WindowTitle = 'Slithy Tove | Terms of Use'; ProcessId = $guiProcess.Id; TermsAccepted = $false; NodeStarted = $false } 'first-launch.json'
    try {
        Add-Type -Path (Join-Path $installRoot 'System.Drawing.Common.dll')
        $rect = [SlithyInstallerWindowProbe+Rect]::new()
        Assert-Check ([SlithyInstallerWindowProbe]::GetWindowRect($window, [ref]$rect)) 'The terms window rectangle was unavailable.'
        $bitmap = [Drawing.Bitmap]::new(($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top))
        try {
            $graphics = [Drawing.Graphics]::FromImage($bitmap)
            try {
                $dc = $graphics.GetHdc()
                try { $captured = [SlithyInstallerWindowProbe]::PrintWindow($window, $dc, 2) }
                finally { $graphics.ReleaseHdc($dc) }
                Assert-Check $captured 'Windows could not render the terms window screenshot.'
            } finally { $graphics.Dispose() }
            $bitmap.Save((Join-Path $resultsRoot 'first-run-terms.png'), [Drawing.Imaging.ImageFormat]::Png)
        } finally { $bitmap.Dispose() }
    } catch { $_.Exception.Message | Set-Content -LiteralPath (Join-Path $resultsRoot 'screenshot-unavailable.txt') }
    # Close means decline. No agreement is accepted on behalf of a person.
    [void][SlithyInstallerWindowProbe]::PostMessage($window, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)
    Assert-Check ($guiProcess.WaitForExit(15000) -and $guiProcess.ExitCode -eq 0) 'The first-run application did not exit after declining.'
    $guiProcess.Dispose(); $guiProcess = $null
    Assert-Check (-not (Test-Path -LiteralPath (Join-Path $profileRoot 'terms-acceptance.json'))) 'Declining unexpectedly saved an agreement.'
    Pass 'Installed GUI opens terms, starts no node before agreement, and exits on decline'

    foreach ($test in @('updates', 'wallet-restore', 'wallet-rpc', 'mining-rpc', 'seed-wallet')) {
        Run-Program $app @('--self-test-' + $test) ('self-test-' + $test)
        Pass "Installed application self-test: $test"
    }
    $bundledAttempts = [Collections.Generic.List[object]]::new()
    $bundled = Run-Program $app @('--self-test-send-receive') 'self-test-send-receive-attempt-1' -AllowFailure
    $bundledAttempts.Add($bundled)
    $repeatedCancellation = $false
    $cancellationDiagnostic = 'Disposable node test failed: The operation was canceled.'
    if ($bundled.ExitCode -ne 0 -and $bundled.Output -ceq $cancellationDiagnostic) {
        Write-Warning 'The bundled bulk-mining test reached its RPC timeout. Its first failure is retained; retrying it once.'
        $bundled = Run-Program $app @('--self-test-send-receive') 'self-test-send-receive-attempt-2' -AllowFailure
        $bundledAttempts.Add($bundled)
        $repeatedCancellation = $bundled.ExitCode -ne 0 -and $bundled.Output -ceq $cancellationDiagnostic
    }
    Save-Json $bundledAttempts 'bundled-send-receive-attempts.json'
    if ($repeatedCancellation) {
        Write-Warning 'The bundled test timed out twice. Finishing the chunked transaction and uninstall checks before failing this job.'
    } else {
        Assert-Check ($bundled.ExitCode -eq 0) 'The bundled send/receive test failed. See its process log.'
        Pass "Installed application self-test: send-receive ($($bundledAttempts.Count) attempt(s))"
    }
    Assert-Check (@(Get-Process -Name slithyd -ErrorAction SilentlyContinue).Count -eq 0) 'A disposable self-test node did not stop.'
    $transactionScript = Join-Path $PSScriptRoot 'Test-InstalledWalletTransactions.ps1'
    $powerShell = (Get-Process -Id $PID).Path
    Run-Program $powerShell @('-NoLogo', '-NoProfile', '-NonInteractive', '-File', $transactionScript,
        '-InstalledDirectory', $installRoot, '-EvidenceDirectory', $resultsRoot) 'chunked-transactions' 420
    Pass 'Installed wallet assembly sends, confirms, backs up and restores using bounded regtest block batches'

    # Real offline wallet data lives in the fresh runner profile, never in uploaded evidence.
    $networkRoot = Join-Path $profileRoot 'networks/beta-20260909'
    $walletRoot = Join-Path $networkRoot 'wallets/installer-validation-wallet'
    $nodeFixture = Join-Path $resultsRoot 'wallet-tool-data'
    [IO.Directory]::CreateDirectory($nodeFixture) | Out-Null
    [IO.Directory]::CreateDirectory((Split-Path -Parent $walletRoot)) | Out-Null
    $walletTool = Join-Path $installRoot 'Binaries/slithy-wallet.exe'
    Run-Program $walletTool @('-regtest', ('-datadir=' + $nodeFixture), ('-wallet=' + $walletRoot), 'create') 'create-preservation-wallet'
    Assert-Check (Test-Path -LiteralPath (Join-Path $walletRoot 'wallet.dat')) 'The disposable preservation wallet was not created.'
    foreach ($relative in @('config/settings.json', 'daemon/testnet3/blocks/fixture.dat', 'logs/installer-fixture.log')) {
        $file = Join-Path $networkRoot $relative
        [IO.Directory]::CreateDirectory((Split-Path -Parent $file)) | Out-Null
        [IO.File]::WriteAllText($file, '{"installerPreservationFixture":true}')
    }
    $canaries = @(Get-CanaryInventory)
    Save-Json $canaries 'preserved-data-hashes.json'

    Run-Program $installer ($setupFlags + @('/TASKS=desktopicon,launchstartup', ('/LOG=' + (Join-Path $resultsRoot 'repair.log')))) 'repair-install' 300
    Check-Registration
    foreach ($shortcut in $shortcutPaths) { Check-Shortcut $shortcut }
    Check-Canaries $canaries
    Pass 'Same-version repair adds selected Desktop and Startup shortcuts and preserves fixture data'

    # Run the installed signed uninstaller, not a substitute or rebuilt harness.
    Check-Signature $uninstaller 'uninstaller before removal'
    Run-Program $uninstaller @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', ('/LOG=' + (Join-Path $resultsRoot 'uninstall.log'))) 'uninstall' 180
    # Inno's first process can exit before its temporary cleanup child finishes.
    $uninstallLog = Join-Path $resultsRoot 'uninstall.log'
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    $uninstallFinished = $false
    while (-not $uninstallFinished -and [DateTime]::UtcNow -lt $deadline) {
        if (-not (Test-Path -LiteralPath $uninstaller) -and (Test-Path -LiteralPath $uninstallLog)) {
            try {
                $logHandle = [IO.File]::Open($uninstallLog, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
                $logHandle.Dispose()
                $uninstallFinished = $true
            } catch [IO.IOException] { }
        }
        if (-not $uninstallFinished) { Start-Sleep -Milliseconds 250 }
    }
    Assert-Check $uninstallFinished 'The uninstaller cleanup child did not finish within 30 seconds.'
    $uninstallText = [IO.File]::ReadAllText($uninstallLog)
    Assert-Check ($uninstallText.Contains('Uninstallation process succeeded.') -and $uninstallText.Contains('Removed all? Yes')) 'Inno did not report a complete uninstall.'
    Assert-Check (@(Get-SlithyRegistration).Count -eq 0) 'Uninstall left a registered Slithy installation.'
    foreach ($shortcut in $shortcutPaths) { Assert-Check (-not (Test-Path -LiteralPath $shortcut)) "Uninstall left a shortcut: $shortcut" }
    Assert-Check (-not (Test-Path -LiteralPath $app)) 'Uninstall left the application executable.'
    Assert-Check (-not (Test-Path -LiteralPath $uninstaller)) 'The signed uninstaller did not finish removing itself.'
    if (Test-Path -LiteralPath $installRoot) {
        Assert-Check (@(Get-ChildItem -LiteralPath $installRoot -Force).Count -eq 0) 'Uninstall left installed payload files behind.'
    }
    Check-Canaries $canaries
    Assert-Check (@(Get-Process | Where-Object { $_.ProcessName -in @('Slithy Tove', 'slithyd') }).Count -eq 0) 'A tested application or node process survived uninstall.'
    Pass 'Public uninstall removes registration, shortcuts and payload without changing wallet, settings, chain or log fixtures'
    Assert-Check (-not $repeatedCancellation) 'The bundled bulk-mining self-test timed out twice. Other evidence is retained, but this run is not a pass.'
    Save-Json @{
        Passed = $true; Version = $version; InstallerUrl = $installerUri; InstallerSha256 = $installerHash
        EnvironmentKind = $EnvironmentKind; OS = [Environment]::OSVersion.VersionString
        Checks = $checks.Count; GuiScope = 'First-run terms window, then decline. Regtest self-tests exercise the installed wallet and miner.'
        BootstrapCovered = $false; DataRemoved = $false
    } 'result.json'
    Write-Output "PASS Published Windows installer validation. Evidence: $resultsRoot"
} catch {
    Save-Json @{ Passed = $false; Message = $_.Exception.Message; Version = $version; InstallerSha256 = $installerHash } 'result.json'
    throw
} finally {
    # Stop this test's GUI process if a check failed. Leave the disposable VM for inspection.
    if ($null -ne $guiProcess) {
        if (-not $guiProcess.HasExited) { $guiProcess.Kill($true); $guiProcess.WaitForExit() }
        $guiProcess.Dispose()
    }
}
