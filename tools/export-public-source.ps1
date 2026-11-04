param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$target = [IO.Path]::GetFullPath($Destination)
$comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }

function Assert-NoLink([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    $current = [IO.Path]::GetPathRoot($full)
    foreach ($part in $full.Substring($current.Length).Split([IO.Path]::DirectorySeparatorChar, [StringSplitOptions]::RemoveEmptyEntries)) {
        $current = Join-Path $current $part
        $item = if ([IO.Directory]::Exists($current)) { [IO.DirectoryInfo]::new($current) } else { [IO.FileInfo]::new($current) }
        # OneDrive files can be reparse points without redirecting to another path.
        # LinkTarget catches real links, including a dangling file or junction.
        if ($null -ne $item.LinkTarget) { throw 'A source or destination path contains a symbolic link or junction.' }
    }
}

function Assert-RelativePath([string]$Name) {
    if ([string]::IsNullOrWhiteSpace($Name) -or [IO.Path]::IsPathRooted($Name) -or
        $Name -match '[\\:*?"<>|\x00-\x1f]' -or
        @($Name.Split('/') | Where-Object { $_ -in @('', '.', '..') }).Count -gt 0) {
        throw 'Export rules must use plain relative paths separated by forward slashes.'
    }
}

function Resolve-Child([string]$Base, [string]$Relative) {
    Assert-RelativePath $Relative
    $candidate = [IO.Path]::GetFullPath((Join-Path $Base $Relative))
    $prefix = [IO.Path]::TrimEndingDirectorySeparator($Base) + [IO.Path]::DirectorySeparatorChar
    if (-not $candidate.StartsWith($prefix, $comparison)) { throw 'An export path escapes its selected directory.' }
    return $candidate
}

Assert-NoLink $root
Assert-NoLink $target
if (Test-Path -LiteralPath $target) { throw 'Choose a new destination. Existing source releases are never overwritten.' }
if ($target.Equals($root, $comparison)) { throw 'Destination must not be the working project.' }
$rulesPath = Join-Path $PSScriptRoot 'public-source.json'
Assert-NoLink $rulesPath
$rules = Get-Content -LiteralPath $rulesPath -Raw | ConvertFrom-Json
foreach ($property in @('files', 'directories', 'excludedDirectories', 'excludedFiles', 'excludedDirectoryNames', 'excludedExtensions')) {
    if ($rules.$property -isnot [Array]) { throw "Export rule $property must be an array." }
    foreach ($value in $rules.$property) {
        if ($value -isnot [string]) { throw "Export rule $property must contain strings." }
        Assert-RelativePath $value
        if ($property -in @('excludedDirectoryNames', 'excludedExtensions') -and $value.Contains('/')) {
            throw "Export rule $property must contain names, not paths."
        }
    }
}
if ($null -eq $rules.sourceDirectoryChildren -or $rules.sourceDirectoryChildren -isnot [pscustomobject]) {
    throw 'Export rules must include a sourceDirectoryChildren object.'
}
foreach ($entry in $rules.sourceDirectoryChildren.PSObject.Properties) {
    Assert-RelativePath $entry.Name
    if ($entry.Value -isnot [Array]) { throw 'Allowed source directory children must be an array.' }
    foreach ($child in $entry.Value) {
        if ($child -isnot [string]) { throw 'Allowed source directory children must be names.' }
        Assert-RelativePath $child
        if ($child.Contains('/')) { throw 'Allowed source directory children must be direct child names.' }
    }
}

function Test-Excluded([string]$Relative, [bool]$Directory) {
    if (@($Relative.Split('/') | Where-Object { $_ -in $rules.excludedDirectoryNames }).Count -gt 0) { return $true }
    # depends writes target-specific libraries beside its recipes. Copy the
    # named source folders, not whatever build targets happen to exist locally.
    foreach ($entry in $rules.sourceDirectoryChildren.PSObject.Properties) {
        if ($Relative.StartsWith($entry.Name + '/', [StringComparison]::OrdinalIgnoreCase)) {
            $tail = $Relative.Substring($entry.Name.Length + 1)
            $child = $tail.Split('/')[0]
            if (($Directory -or $tail.Contains('/')) -and $child -notin $entry.Value) { return $true }
        }
    }
    foreach ($excluded in $rules.excludedDirectories) {
        if ($Relative.Equals($excluded, [StringComparison]::OrdinalIgnoreCase) -or
            $Relative.StartsWith("$excluded/", [StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    if (-not $Directory) {
        if ($Relative -in $rules.excludedFiles -or [IO.Path]::GetExtension($Relative) -in $rules.excludedExtensions -or
            [IO.Path]::GetFileName($Relative) -like '*.pre-*' -or
            [IO.Path]::GetFileName($Relative) -in @('.env', '.cookie', 'wallet.dat', 'id_rsa', 'id_ed25519', 'id_ecdsa', 'id_dsa') -or
            [IO.Path]::GetFileName($Relative) -like '.env.*') { return $true }
    }
    return $false
}

$paths = [Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
foreach ($name in $rules.files) {
    $source = Resolve-Child $root $name
    Assert-NoLink $source
    if (Test-Excluded $name $false) { throw "An explicitly listed source file is excluded by the export rules: $name" }
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Required source file missing: $name" }
    [void]$paths.Add($name)
}
foreach ($directory in $rules.directories) {
    $base = Resolve-Child $root $directory
    Assert-NoLink $base
    if (Test-Excluded $directory $true) { throw "An explicitly listed source directory is excluded: $directory" }
    if (-not (Test-Path -LiteralPath $base -PathType Container)) { throw "Required source directory missing: $directory" }
    if ($target.Equals($base, $comparison) -or $target.StartsWith($base + [IO.Path]::DirectorySeparatorChar, $comparison)) {
        throw 'Choose an export destination outside the source directories being copied.'
    }
    # Walk directories ourselves so a linked directory cannot be skipped or
    # traversed differently by PowerShell versions. Never enter excluded folders.
    $pending = [Collections.Generic.Stack[string]]::new()
    $pending.Push($base)
    while ($pending.Count -gt 0) {
        $folder = $pending.Pop()
        Assert-NoLink $folder
        foreach ($item in Get-ChildItem -LiteralPath $folder -Force) {
            $relative = [IO.Path]::GetRelativePath($root, $item.FullName).Replace('\', '/')
            Assert-RelativePath $relative
            if (Test-Excluded $relative $item.PSIsContainer) { continue }
            Assert-NoLink $item.FullName
            if ($item.PSIsContainer) { $pending.Push($item.FullName) }
            else { [void]$paths.Add($relative) }
        }
    }
}
$portableNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($relative in $paths) {
    if (-not $portableNames.Add($relative)) { throw 'Two exported paths differ by letter case. Rename one before publishing.' }
}
Assert-NoLink $target
[void][IO.Directory]::CreateDirectory($target)
Assert-NoLink $target
$manifest = foreach ($relative in $paths) {
    $source = Resolve-Child $root $relative
    $destinationFile = Resolve-Child $target $relative
    Assert-NoLink $source
    Assert-NoLink $destinationFile
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destinationFile))
    Assert-NoLink $destinationFile
    $bytes = [IO.File]::ReadAllBytes($source)
    if ([Array]::IndexOf($bytes,[byte]0) -lt 0) {
        try {
            $utf8 = [Text.UTF8Encoding]::new($false,$true)
            $bytes = $utf8.GetBytes($utf8.GetString($bytes).Replace("`r`n","`n"))
        } catch [Text.DecoderFallbackException] { }
    }
    $output = [IO.File]::Open($destinationFile, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $output.Write($bytes); $output.Flush($true) } finally { $output.Dispose() }
    [ordered]@{path=$relative; sha256=(Get-FileHash -LiteralPath $destinationFile).Hash.ToLowerInvariant()}
}
$manifestPath = Join-Path $target 'SOURCE-MANIFEST.json'
Assert-NoLink $manifestPath
$manifestBytes = [Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject @($manifest) -Depth 3) + "`n")
$output = [IO.File]::Open($manifestPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try { $output.Write($manifestBytes); $output.Flush($true) } finally { $output.Dispose() }
Write-Output "Exported $($paths.Count) source paths to $target"
Write-Output 'Files were selected by public-source.json. Excluded paths and linked paths were not copied.'
Write-Output 'Scan and build the exported source before creating the public commit.'
