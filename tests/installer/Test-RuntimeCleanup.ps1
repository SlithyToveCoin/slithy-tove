param(
    [string]$Compiler = "",
    [string]$CurrentPayload = ""
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $Compiler) {
    $Compiler = @(
        (Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 6/ISCC.exe'),
        "${env:ProgramFiles(x86)}/Inno Setup 6/ISCC.exe"
    ) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}
if (-not $Compiler) { throw 'Install Inno Setup 6 or pass -Compiler.' }
$source = Join-Path $root 'installer/RuntimeCleanup.iss'
$manifest = Get-Content -LiteralPath (Join-Path $root 'installer/runtime-files-0.1.48.iss')
$entries = @($manifest | Where-Object { $_ -match '^RemovePriorRuntimeFile\(' })
if ($entries.Count -eq 0) { throw 'Missing obsolete-runtime file list.' }
$paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$rows = @()
foreach ($entry in $entries) {
    if ($entry -notmatch "^RemovePriorRuntimeFile\('((?:[a-z]{2}(?:-[A-Za-z]+)?\\)?[A-Za-z0-9_.-]+\.dll)', '([a-f0-9]{64})'\);$") {
        throw 'An obsolete-runtime entry has an invalid path or hash.'
    }
    $relative = $Matches[1]
    if (-not $paths.Add($relative)) { throw 'Duplicate obsolete-runtime path.' }
    $rows += [pscustomobject]@{Path=$relative; Line=$entry}
}
$work = Join-Path $env:LOCALAPPDATA ('SlithyBuildReview/runtime-cleanup-' + [guid]::NewGuid().ToString('N'))
$app = Join-Path $work 'app'
$outside = Join-Path $work 'outside'
New-Item -ItemType Directory -Path $app, $outside, (Join-Path $app 'nested') -Force | Out-Null
function Test-PayloadOmission([string]$PayloadPath, [string]$Label) {
    $expanded = Join-Path $work ($Label + '-expanded.iss')
    $preprocessor = Join-Path $work ($Label + '-preprocess.iss')
    $inputText = '#define PublishDir "' + [IO.Path]::GetFullPath($PayloadPath) + '"' + "`n" +
        '#include "' + (Join-Path $root 'installer/runtime-files-0.1.48.iss') + '"' + "`n" +
        '#call SaveToFile("' + $expanded + '")' + "`n"
    [IO.File]::WriteAllText($preprocessor, $inputText)
    & $Compiler '/$c-' $preprocessor *> (Join-Path $work ($Label + '-preprocess.log'))
    # Inno returns 2 with compilation disabled. The final directive writes this file.
    if ($LASTEXITCODE -notin @(0,2) -or -not (Test-Path -LiteralPath $expanded -PathType Leaf)) {
        throw 'Runtime cleanup preprocessing failed.'
    }
    $code = [IO.File]::ReadAllText($expanded)
    foreach ($row in $rows) {
        $inNewPayload = Test-Path -LiteralPath (Join-Path $PayloadPath $row.Path)
        if ($inNewPayload -eq $code.Contains($row.Line)) { throw "Wrong cleanup inclusion for $($row.Path)." }
    }
}
$futurePayload = Join-Path $work 'future-payload'
New-Item -ItemType Directory -Path $futurePayload | Out-Null
$futureFile = Join-Path $futurePayload $rows[0].Path
New-Item -ItemType Directory -Path (Split-Path -Parent $futureFile) -Force | Out-Null
[IO.File]::WriteAllText($futureFile, 'A runtime reintroduced by a future build')
Test-PayloadOmission $futurePayload 'future'
if ($CurrentPayload) { Test-PayloadOmission $CurrentPayload 'current' }
$ancestor = Get-Item -LiteralPath $app
while ($ancestor) {
    if ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Run the fixture from an ordinary local directory.' }
    $ancestor = $ancestor.Parent
}
$original = 'Old runtime test fixture'
$changed = 'Modified runtime test fixture'
$exact = Join-Path $app 'obsolete.dll'
[IO.File]::WriteAllText($exact, $original)
$hash = (Get-FileHash -LiteralPath $exact).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $app 'modified.dll'), $changed)
[IO.File]::WriteAllText((Join-Path $app 'nested/obsolete.dll'), $original)
[IO.File]::WriteAllText((Join-Path $app 'untouched.dll'), $original)
[IO.File]::WriteAllText((Join-Path $app 'notes.txt'), 'A user file')
[IO.File]::WriteAllText((Join-Path $outside 'obsolete.dll'), $original)
[IO.File]::WriteAllText((Join-Path $work 'new-marker.txt'), 'New payload copied before cleanup')
New-Item -ItemType Junction -Path (Join-Path $app 'linked') -Target $outside | Out-Null
$script = @"
[Setup]
AppId=SlithyRuntimeCleanupCheck-$([guid]::NewGuid().ToString('N'))
AppName=Slithy runtime cleanup test
AppVersion=1.0
DefaultDirName=$app
OutputDir=$work
OutputBaseFilename=cleanup-test
PrivilegesRequired=lowest
Uninstallable=no
CreateUninstallRegKey=no
CloseApplications=no
RestartApplications=no
SetupLogging=yes
Compression=none
DisableDirPage=yes
DisableProgramGroupPage=yes
[Files]
Source: "$work\new-marker.txt"; DestDir: "{app}"
[Code]
#include "$source"
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then begin
    if not FileExists(ExpandConstant('{app}\obsolete.dll')) then
      RaiseException('Cleanup happened before installation.');
  end;
  if CurStep = ssPostInstall then begin
    if not FileExists(ExpandConstant('{app}\new-marker.txt')) then
      RaiseException('The new file was not installed before cleanup.');
    RemovePriorRuntimeFile('obsolete.dll', '$hash');
    RemovePriorRuntimeFile('nested\obsolete.dll', '$hash');
    RemovePriorRuntimeFile('modified.dll', '$hash');
    RemovePriorRuntimeFile('linked\obsolete.dll', '$hash');
    RemovePriorRuntimeFile('..\outside\obsolete.dll', '$hash');
    RemovePriorRuntimeFile('$outside\obsolete.dll', '$hash');
    RemovePriorRuntimeFile('*.dll', '$hash');
    RemovePriorRuntimeFile('', '$hash');
    RemovePriorRuntimeFile('untouched.dll', 'short');
  end;
end;
"@
$fixture = Join-Path $work 'cleanup-test.iss'
[IO.File]::WriteAllText($fixture, $script)
& $Compiler $fixture *> (Join-Path $work 'compile.log')
if ($LASTEXITCODE -ne 0) { throw "Inno fixture compile failed. See $work/compile.log." }
$process = Start-Process -FilePath (Join-Path $work 'cleanup-test.exe') -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS',('/LOG="' + (Join-Path $work 'install.log') + '"')) -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(60000)) { $process.Kill(); throw 'Runtime cleanup fixture timed out.' }
if ($process.ExitCode -ne 0) { throw "Runtime cleanup fixture exited with $($process.ExitCode). See $work/install.log." }
if ((Test-Path -LiteralPath $exact) -or (Test-Path -LiteralPath (Join-Path $app 'nested/obsolete.dll'))) { throw 'Known obsolete files were not removed.' }
if ([IO.File]::ReadAllText((Join-Path $app 'modified.dll')) -cne $changed) { throw 'A modified file was removed or changed.' }
if ([IO.File]::ReadAllText((Join-Path $app 'notes.txt')) -cne 'A user file') { throw 'An unrelated user file changed.' }
foreach ($path in @((Join-Path $app 'untouched.dll'), (Join-Path $outside 'obsolete.dll'))) {
    if ((Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant() -ne $hash) { throw 'An unlisted, linked or out-of-directory file changed.' }
}
$log = [IO.File]::ReadAllText((Join-Path $work 'install.log'))
if (-not $log.Contains('Preserved modified obsolete-runtime file: modified.dll')) { throw 'A modified file was not logged.' }
if (-not $log.Contains('Preserved obsolete-runtime path with a link or unreadable parent: linked\obsolete.dll')) { throw 'A linked path was not logged.' }
[ordered]@{
    Passed = $true
    ManifestEntries = $entries.Count
    KnownFilesRemoved = 2
    ModifiedAndUnknownFilesPreserved = $true
    LinkedAndTraversalPathsPreserved = $true
    CleanupAfterInstall = $true
    CurrentPayloadChecked = [bool]$CurrentPayload
    FuturePayloadFilesExcluded = $true
    FixtureDirectory = $work
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $work 'result.json') -Encoding utf8
Write-Output "PASS Exact runtime cleanup, modified and unrelated files, linked paths, path guards, post-install order, future-payload exclusion and $($entries.Count) manifest entries."
if ($CurrentPayload) { Write-Output 'PASS Current payload files excluded from cleanup.' }
Write-Output "Evidence: $work"
