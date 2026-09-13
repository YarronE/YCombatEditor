[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string] $CandidateRoot,
    [Parameter(Mandatory=$true)][string] $Destination,
    [switch] $Initialize,
    [switch] $SourceOnly
)
$ErrorActionPreference = 'Stop'
$candidate = (Resolve-Path -LiteralPath $CandidateRoot).Path
$source = Join-Path $candidate 'package'
$targetRoot = [IO.Path]::GetFullPath($Destination).TrimEnd('\','/')
if ($targetRoot.Length -le [IO.Path]::GetPathRoot($targetRoot).Length) { throw 'Destination cannot be a drive root.' }
if (!(Test-Path -LiteralPath (Join-Path $targetRoot '.git'))) { throw 'Destination must be an existing independent Git repository.' }
foreach ($root in @($candidate,$targetRoot)) {
    $check = $root
    while ($check) {
        if ((Test-Path -LiteralPath $check) -and ((Get-Item -LiteralPath $check -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Linked paths are not supported.' }
        $check = Split-Path -Parent $check
    }
    if (Get-ChildItem -LiteralPath $root -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'Linked entries are not supported.' }
}
function SafePath([string] $root, [string] $relative) {
    if (!$relative -or $relative -match '(^|/)[.][.](/|$)|^/|^[.]git(/|$)' -or $relative.Contains('\') -or $relative.Contains(':')) { throw 'Unsafe manifest path.' }
    $path = [IO.Path]::GetFullPath((Join-Path $root $relative))
    if (!$path.StartsWith($root.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Manifest path escapes root.' }
    return $path
}
function Matches([string] $path, [string] $hash) {
    return (Test-Path -LiteralPath $path -PathType Leaf) -and (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $hash
}
$inventory = Get-Content -LiteralPath (Join-Path $candidate 'inventory.json') -Raw | ConvertFrom-Json
$localOnly = @('ROADMAP.zh-CN.md','ROADMAP.zh-CN.md.meta')
if ($inventory.files | Where-Object { $_.path -in $localOnly }) { throw 'Roadmap is local-only. Re-export the candidate without it.' }
$manifest = Get-Content -LiteralPath (Join-Path $source 'package.json') -Raw | ConvertFrom-Json
if ($manifest.name -ne 'com.ethan.act-action-editor' -or $manifest.version -ne $inventory.version -or $manifest.version -notmatch '^\d+\.\d+\.\d+[-.A-Za-z0-9]*$') { throw 'Unexpected package identity/version.' }
$statePath = Join-Path $targetRoot '.ycombat-sync.json'
$previous = if (Test-Path -LiteralPath $statePath) { Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json } else { $null }
if (!$previous -and !$Initialize) { throw 'First sync requires -Initialize. Review the target first.' }
if ($previous) {
    $previous.files = @($previous.files | Where-Object { $_.path -notin $localOnly })
    foreach ($entry in $previous.files) {
        if (!(Matches (SafePath $targetRoot $entry.path) $entry.sha256)) { throw "Independent target edit: $($entry.path). Port it to the development source before syncing." }
    }
}
foreach ($entry in $inventory.files) {
    if (!(Matches (SafePath $source $entry.path) $entry.sha256)) { throw "Candidate changed: $($entry.path)" }
    $target = SafePath $targetRoot $entry.path
    if ((Test-Path -LiteralPath $target) -and (!$previous -or $previous.files.path -notcontains $entry.path) -and !(Matches $target $entry.sha256)) {
        if (!(!$previous -and $Initialize -and $entry.path -eq 'README.md')) { throw "Unmanaged target collision: $($entry.path)" }
    }
}
$releaseFiles = @()
$releaseRoot = Join-Path $targetRoot ('Releases~/' + $manifest.version)
if (!$SourceOnly) {
    foreach ($artifact in @($inventory.zip,$inventory.archive)) {
        if (!(Matches (SafePath $candidate $artifact.file) $artifact.sha256)) { throw 'Release artifact changed after export.' }
    }
    $validation = Get-Content -LiteralPath (Join-Path $candidate 'validation.json') -Raw | ConvertFrom-Json
    if ($validation.result -ne 'Passed' -or !$validation.installedAsTarball -or $validation.failed -ne 0 -or $validation.skipped -ne 0 -or $validation.passed -le 0 -or $validation.archiveSha256 -ne $inventory.archive.sha256) { throw 'Final tarball validation is missing or does not match the archive.' }
    $releaseFiles = @($inventory.zip.file,$inventory.archive.file,'SHA256SUMS.txt','inventory.json','validation.json','RELEASE-NOTES.zh-CN.md')
    foreach ($name in $releaseFiles) {
        $from = SafePath $candidate $name
        $to = SafePath $releaseRoot $name
        $hash = (Get-FileHash -LiteralPath $from -Algorithm SHA256).Hash
        if ((Test-Path -LiteralPath $to) -and !(Matches $to $hash)) { throw "Immutable release already exists: $name. Bump the version or use -SourceOnly." }
    }
}
# All collision checks precede mutation; only exact manifest-owned files may be removed.
foreach ($entry in $inventory.files) {
    $target = SafePath $targetRoot $entry.path
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force
    Copy-Item -LiteralPath (SafePath $source $entry.path) -Destination $target
    if (!(Matches $target $entry.sha256)) { throw 'Source copy hash mismatch.' }
}
if ($previous) {
    $previous.files = @($previous.files | Where-Object { $_.path -notin $localOnly })
    foreach ($entry in $previous.files) {
        if ($inventory.files.path -notcontains $entry.path) { Remove-Item -LiteralPath (SafePath $targetRoot $entry.path) }
    }
}
foreach ($name in $releaseFiles) {
    $null = New-Item -ItemType Directory -Path $releaseRoot -Force
    Copy-Item -LiteralPath (SafePath $candidate $name) -Destination (SafePath $releaseRoot $name)
    if (!(Matches (SafePath $releaseRoot $name) (Get-FileHash -LiteralPath (SafePath $candidate $name) -Algorithm SHA256).Hash)) { throw 'Release copy hash mismatch.' }
}
$inventory | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $statePath -Encoding UTF8
Write-Output "Synchronized $($inventory.files.Count) package files to $targetRoot"
Write-Output 'Existing Git history and remote were preserved. No commit, push or GitHub Release was performed.'
