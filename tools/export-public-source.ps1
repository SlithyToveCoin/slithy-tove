param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$target = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $target) { throw 'Choose a new destination. Existing source releases are never overwritten.' }
if ($target -eq $root) { throw 'Destination must not be the working project.' }
$rules = Get-Content (Join-Path $PSScriptRoot 'public-source.json') -Raw | ConvertFrom-Json
$paths = [Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
foreach ($name in $rules.files) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $name) -PathType Leaf)) { throw "Required source file missing: $name" }
    [void]$paths.Add($name)
}
foreach ($directory in $rules.directories) {
    $base = Join-Path $root $directory
    if (-not (Test-Path -LiteralPath $base -PathType Container)) { throw "Required source directory missing: $directory" }
    foreach ($file in Get-ChildItem -LiteralPath $base -Recurse -File -Force) {
        # OneDrive placeholders are reparse points too; reject actual links, not hydrated files.
        if ($file.LinkType -in @('SymbolicLink','Junction')) { throw "Review source link before export: $($file.FullName)" }
        $relative = [IO.Path]::GetRelativePath($root,$file.FullName).Replace('\','/')
        $parts = $relative.Split('/')
        if ($parts | Where-Object { $_ -in $rules.excludedDirectoryNames }) { continue }
        if ($relative -in $rules.excludedFiles) { continue }
        if ($rules.excludedDirectories | Where-Object { $relative.StartsWith("$_/",[StringComparison]::Ordinal) }) { continue }
        if ($file.Extension -in $rules.excludedExtensions -or $file.Name -like '*.pre-*') { continue }
        [void]$paths.Add($relative)
    }
}
New-Item -ItemType Directory -Path $target | Out-Null
$manifest = foreach ($relative in $paths) {
    $source = Join-Path $root $relative
    $destinationFile = [IO.Path]::GetFullPath((Join-Path $target $relative))
    if (-not $destinationFile.StartsWith($target + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Source path escapes destination' }
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destinationFile))
    $bytes = [IO.File]::ReadAllBytes($source)
    if ([Array]::IndexOf($bytes,[byte]0) -lt 0) {
        try {
            $utf8 = [Text.UTF8Encoding]::new($false,$true)
            $bytes = $utf8.GetBytes($utf8.GetString($bytes).Replace("`r`n","`n"))
        } catch [Text.DecoderFallbackException] { }
    }
    [IO.File]::WriteAllBytes($destinationFile,$bytes)
    [ordered]@{path=$relative; sha256=(Get-FileHash -LiteralPath $destinationFile).Hash.ToLowerInvariant()}
}
$manifest | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $target 'SOURCE-MANIFEST.json') -Encoding utf8
Write-Output "Exported $($paths.Count) source paths to $target"
Write-Output 'No Git history, private operator folders, release archives or real analytics were copied.'
Write-Output 'Scan and build the exported source before creating the public commit.'
