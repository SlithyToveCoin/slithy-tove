$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$work = Join-Path $temporaryRoot ('slithy-export-fixture-' + [Guid]::NewGuid().ToString('N'))
$links = [Collections.Generic.List[string]]::new()
$checks = 0
$skipped = 0
$engine = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
[void][IO.Directory]::CreateDirectory($work)

function Check([bool]$Value, [string]$Message) {
    if (-not $Value) { throw "FAIL: $Message" }
    $script:checks++
    Write-Output "PASS: $Message"
}

function New-Fixture([string]$Name) {
    $folder = Join-Path $work $Name
    [void][IO.Directory]::CreateDirectory((Join-Path $folder 'tools'))
    Copy-Item -LiteralPath (Join-Path $repo 'tools/export-public-source.ps1') -Destination (Join-Path $folder 'tools/export-public-source.ps1')
    [IO.File]::WriteAllText((Join-Path $folder 'README.md'), "Fixture`r`n")
    [void][IO.Directory]::CreateDirectory((Join-Path $folder 'src'))
    [IO.File]::WriteAllText((Join-Path $folder 'src/app.cs'), "public class Fixture { }`r`n")
    return $folder
}

function Write-Rules([string]$Folder, [object]$Overrides = @{}) {
    $production = Get-Content -LiteralPath (Join-Path $repo 'tools/public-source.json') -Raw | ConvertFrom-Json
    $rules = [ordered]@{
        files = @('README.md')
        directories = @('src')
        excludedDirectories = @('src/internal')
        excludedFiles = @()
        excludedDirectoryNames = @($production.excludedDirectoryNames)
        excludedExtensions = @($production.excludedExtensions)
        sourceDirectoryChildren = $production.sourceDirectoryChildren
    }
    foreach ($key in $Overrides.Keys) { $rules[$key] = $Overrides[$key] }
    [IO.File]::WriteAllText((Join-Path $Folder 'tools/public-source.json'), (ConvertTo-Json -InputObject $rules))
}

function Export-Fixture([string]$Folder, [string]$Destination) {
    $start = [Diagnostics.ProcessStartInfo]::new($engine)
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in @('-NoProfile', '-File', (Join-Path $Folder 'tools/export-public-source.ps1'), '-Destination', $Destination)) {
        [void]$start.ArgumentList.Add($argument)
    }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(30000)) { $process.Kill($true); throw 'Fixture export timed out.' }
        return [pscustomobject]@{ ExitCode = $process.ExitCode; Output = $stdout.Result + $stderr.Result }
    } finally { $process.Dispose() }
}

function New-DirectoryLink([string]$Link, [string]$Target) {
    $kind = if ($IsWindows) { 'Junction' } else { 'SymbolicLink' }
    New-Item -ItemType $kind -Path $Link -Value $Target | Out-Null
    $links.Add($Link)
}

try {
    $fixture = New-Fixture 'ordinary'
    Write-Rules $fixture
    $destination = Join-Path $work 'ordinary-export'
    $result = Export-Fixture $fixture $destination
    Check ($result.ExitCode -eq 0) 'ordinary files export successfully'
    Check ([IO.File]::ReadAllText((Join-Path $destination 'README.md')) -ceq "Fixture`n") 'text is normalized to LF'
    $manifest = @(Get-Content -LiteralPath (Join-Path $destination 'SOURCE-MANIFEST.json') -Raw | ConvertFrom-Json)
    Check ($manifest.Count -eq 2) 'source manifest contains the selected files'
    foreach ($entry in $manifest) {
        Check ((Get-FileHash -LiteralPath (Join-Path $destination $entry.path)).Hash.ToLowerInvariant() -ceq $entry.sha256) 'manifest hash matches exported bytes'
    }
    $result = Export-Fixture $fixture $destination
    Check ($result.ExitCode -ne 0) 'an existing export cannot be overwritten'

    foreach ($bad in @('../outside.txt', '/outside.txt', 'src/../../outside.txt', 'src\outside.txt', 'C:/outside.txt')) {
        Write-Rules $fixture @{ files = @($bad) }
        $target = Join-Path $work ('bad-rule-' + [Guid]::NewGuid().ToString('N'))
        $result = Export-Fixture $fixture $target
        Check ($result.ExitCode -ne 0 -and -not (Test-Path -LiteralPath $target)) 'unsafe file rule fails before output is created'
    }
    Write-Rules $fixture @{ directories = @('../outside') }
    $result = Export-Fixture $fixture (Join-Path $work 'bad-directory')
    Check ($result.ExitCode -ne 0) 'directory rules cannot traverse outside the project'
    Write-Rules $fixture @{ files = @('private.pem') }
    [IO.File]::WriteAllText((Join-Path $fixture 'private.pem'), 'excluded fixture')
    $result = Export-Fixture $fixture (Join-Path $work 'blocked-extension')
    Check ($result.ExitCode -ne 0) 'an explicit file cannot bypass an excluded extension'
    foreach ($private in @('.ssh/session.txt', 'wallets/test/state.txt', 'private/token.txt', 'secrets/token.txt', 'release.key', 'release.p12', 'local.sqlite', '.env', '.cookie', 'wallet.dat', 'id_ed25519')) {
        $privatePath = Join-Path $fixture $private
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($privatePath))
        [IO.File]::WriteAllText($privatePath, 'excluded fixture')
        Write-Rules $fixture @{ files = @($private) }
        $target = Join-Path $work ('private-rule-' + [Guid]::NewGuid().ToString('N'))
        $result = Export-Fixture $fixture $target
        Check ($result.ExitCode -ne 0 -and -not (Test-Path -LiteralPath $target)) 'private material is refused even when explicitly listed'
    }
    Write-Rules $fixture
    $result = Export-Fixture $fixture (Join-Path $fixture 'src/nested-export')
    Check ($result.ExitCode -ne 0) 'export cannot create a future copy of itself inside a source directory'

    $outside = Join-Path $work 'outside'
    [void][IO.Directory]::CreateDirectory($outside)
    [IO.File]::WriteAllText((Join-Path $outside 'private.txt'), 'private canary')
    $fixture = New-Fixture 'listed-directory-link'
    Write-Rules $fixture @{ directories = @('redirect') }
    New-DirectoryLink (Join-Path $fixture 'redirect') $outside
    $result = Export-Fixture $fixture (Join-Path $work 'linked-root-export')
    Check ($result.ExitCode -ne 0) 'an allowlisted directory link is rejected'

    Write-Rules $fixture @{ files = @('redirect/private.txt'); directories = @() }
    $result = Export-Fixture $fixture (Join-Path $work 'linked-file-parent-export')
    Check ($result.ExitCode -ne 0) 'an explicitly listed file cannot have a linked parent'

    $fixture = New-Fixture 'nested-directory-link'
    Write-Rules $fixture
    New-DirectoryLink (Join-Path $fixture 'src/redirect') $outside
    $result = Export-Fixture $fixture (Join-Path $work 'nested-link-export')
    Check ($result.ExitCode -ne 0) 'a nested directory link is rejected'

    $fixture = New-Fixture 'destination-link'
    Write-Rules $fixture
    $redirect = Join-Path $work 'destination-redirect'
    New-DirectoryLink $redirect $outside
    $result = Export-Fixture $fixture (Join-Path $redirect 'export')
    Check ($result.ExitCode -ne 0 -and -not (Test-Path -LiteralPath (Join-Path $outside 'export'))) 'a linked destination parent cannot redirect writes'

    $fixture = New-Fixture 'excluded-directory'
    Write-Rules $fixture
    [void][IO.Directory]::CreateDirectory((Join-Path $fixture 'src/Internal'))
    [IO.File]::WriteAllText((Join-Path $fixture 'src/Internal/private.txt'), 'excluded canary')
    $destination = Join-Path $work 'excluded-export'
    $result = Export-Fixture $fixture $destination
    Check ($result.ExitCode -eq 0 -and -not (Test-Path -LiteralPath (Join-Path $destination 'src/Internal'))) 'excluded directory matching tolerates letter case'

    $fixture = New-Fixture 'depends-output'
    foreach ($name in @('core/depends/README.md', 'core/depends/packages/example.mk', 'core/depends/patches/example/fix.patch', 'core/depends/x86_64-pc-linux-gnu/include/local.h', 'core/depends/aarch64-linux-gnu/lib/local.a')) {
        $path = Join-Path $fixture $name
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))
        [IO.File]::WriteAllText($path, 'source or generated fixture')
    }
    Write-Rules $fixture @{ directories = @('core') }
    $destination = Join-Path $work 'depends-export'
    $result = Export-Fixture $fixture $destination
    Check ($result.ExitCode -eq 0 -and (Test-Path -LiteralPath (Join-Path $destination 'core/depends/packages/example.mk')) -and (Test-Path -LiteralPath (Join-Path $destination 'core/depends/patches/example/fix.patch')) -and (Test-Path -LiteralPath (Join-Path $destination 'core/depends/README.md'))) 'dependency recipes and documentation remain in source exports'
    Check (-not (Test-Path -LiteralPath (Join-Path $destination 'core/depends/x86_64-pc-linux-gnu')) -and -not (Test-Path -LiteralPath (Join-Path $destination 'core/depends/aarch64-linux-gnu'))) 'generated dependency targets are excluded, including their headers'
    Write-Rules $fixture @{ files = @('core/depends/x86_64-pc-linux-gnu/include/local.h'); directories = @() }
    $result = Export-Fixture $fixture (Join-Path $work 'explicit-target-export')
    Check ($result.ExitCode -ne 0) 'an explicit file cannot bypass generated dependency exclusions'

    $fixture = New-Fixture 'file-links'
    Write-Rules $fixture @{ files = @('linked.txt'); directories = @() }
    $link = Join-Path $fixture 'linked.txt'
    $created = $false
    try {
        [void][IO.File]::CreateSymbolicLink($link, (Join-Path $outside 'private.txt'))
        $links.Add($link); $created = $true
    } catch [System.UnauthorizedAccessException] { }
      catch [System.IO.IOException] { }
    if ($created) {
        $result = Export-Fixture $fixture (Join-Path $work 'file-link-export')
        Check ($result.ExitCode -ne 0) 'an explicitly listed file link is rejected'
        Remove-Item -LiteralPath $link -Force
        [void][IO.File]::CreateSymbolicLink($link, (Join-Path $outside 'absent.txt'))
        $result = Export-Fixture $fixture (Join-Path $work 'dangling-file-export')
        Check ($result.ExitCode -ne 0) 'a dangling source file link is rejected'
    } else {
        $skipped += 2
        Write-Output 'SKIP: this account cannot create file symlinks; directory link cases still ran.'
    }
    Check ([IO.File]::ReadAllText((Join-Path $outside 'private.txt')) -ceq 'private canary') 'outside canary remains unchanged'
    Write-Output "PASS: $checks public-source export checks, $skipped skipped. No project files were exported."
} finally {
    # Remove fixture links themselves before deleting this task's temporary tree.
    foreach ($link in $links) {
        $item = Get-Item -LiteralPath $link -Force -ErrorAction SilentlyContinue
        if ($null -ne $item -and $null -ne $item.LinkTarget) { Remove-Item -LiteralPath $link -Force }
    }
    $resolved = [IO.Path]::GetFullPath($work)
    $prefix = [IO.Path]::TrimEndingDirectorySeparator($temporaryRoot) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -notlike 'slithy-export-fixture-*') { throw 'Unexpected test cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
