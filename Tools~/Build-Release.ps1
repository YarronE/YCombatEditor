[CmdletBinding()]
param([string] $PackageRoot, [string] $OutputDirectory)

$ErrorActionPreference = 'Stop'
if (!$PackageRoot) { $PackageRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path }
$source = (Resolve-Path -LiteralPath $PackageRoot).Path
$manifest = Get-Content -LiteralPath (Join-Path $source 'package.json') -Raw | ConvertFrom-Json
if ($manifest.name -ne 'com.ethan.act-action-editor') { throw 'Unexpected package identity.' }
if ($manifest.license -ne 'MIT') { throw 'Public license has not been configured.' }
$allowedRoots = @('Runtime', 'Editor', 'Integrations', 'Tests', 'Samples~', 'Documentation~', '.github', 'Tools~')
$allowedFiles = @('package.json', 'README.md', 'CHANGELOG.md', 'LICENSE.md', 'AGENTS.md', 'CONTRIBUTING.md', '.gitignore')
$allowedExtensions = @('.cs', '.meta', '.asmdef', '.json', '.md', '.html', '.yml', '.png', '.ps1')
$legacyNames = @('ClipAnimator.cs', 'ClipState.cs', 'ClipMixerState.cs', 'ClipAnimatorActionAdapter.cs', 'RootMotionCollector.cs', 'CameraShake.cs', 'FreezeFrame.cs', 'TimeScaleManager.cs', 'HitFeelDriver.cs', 'ChromaticAberrationShake.cs')
# Do not follow junctions or links out of the reviewed directory.
$roots = @(Get-ChildItem -LiteralPath $source -Force | Where-Object { $_.Name -notin @('.git','Releases~','.ycombat-sync.json','.gitattributes','LICENSE','ROADMAP.zh-CN.md','ROADMAP.zh-CN.md.meta') })
$entries = @($roots; $roots | Where-Object { $_.PSIsContainer } | ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Recurse -Force })
if ($entries | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) {
    throw 'Links/reparse points require manual review.'
}
$files = @($entries | Where-Object { !$_.PSIsContainer } | Sort-Object FullName)
foreach ($file in $files) {
    if ($legacyNames -contains ($file.Name -replace '\.meta$', '')) { throw 'Quarantined implementation cannot be exported.' }
    $relative = $file.FullName.Substring($source.Length + 1).Replace('\', '/')
    $root = $relative.Split('/')[0]
    $rootMeta = $relative.EndsWith('.meta') -and ($allowedRoots -contains $relative.Substring(0, $relative.Length - 5))
    if (!(($allowedRoots -contains $root) -or ($allowedFiles -contains $relative) -or $rootMeta -or ($allowedFiles -contains ($relative -replace '\.meta$', '')))) {
        throw "Unreviewed root: $relative"
    }
    if ($allowedExtensions -notcontains $file.Extension -and $file.Name -ne '.gitignore') { throw "Unreviewed file type: $relative" }
    if ($file.Extension -eq '.html' -and $root -ne 'Documentation~') { throw 'HTML is allowed only in Documentation~.' }
    if ($root -eq '.github' -and $relative -notin @('.github/workflows/static.yml','.github/ISSUE_TEMPLATE/bug_report.md','.github/ISSUE_TEMPLATE/feature_request.md')) { throw "Unreviewed GitHub support file: $relative" }
    if ($file.Extension -eq '.yml' -and $relative -ne '.github/workflows/static.yml') { throw 'Only the reviewed static workflow is allowed.' }
    if ($file.Extension -eq '.png' -and !$relative.StartsWith('Documentation~/Screenshots/')) { throw 'Screenshots must be under Documentation~/Screenshots.' }
    $body = if ($file.Extension -eq '.png') { '' } else { Get-Content -LiteralPath $file.FullName -Raw }
    if ($root -ne 'Tests' -and $file.Extension -eq '.cs' -and $body -cmatch '\b(Animancer|MoreMountains|ClipAnimator|ClipState|ClipMixerState|ClipAnimatorActionAdapter|RootMotionCollector|HitFeelDriver|ChromaticAberrationShake)\b') {
        throw "Legacy implementation reference requires review: $relative"
    }
    if ($body -match '-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|gh[pousr]_[A-Za-z0-9]{30,}|AKIA[0-9A-Z]{16}') {
        throw "Possible credential in $relative (value not printed)."
    }
}
$runId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$output = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $source ('Releases~/' + $manifest.version) }
if (Test-Path -LiteralPath $output) { throw 'Release output already exists. Use a new version or a new output directory; never overwrite a released artifact.' }
$destination = Join-Path $output 'package'
$null = New-Item -ItemType Directory -Path $destination
$inventory = @()
foreach ($file in $files) {
    $relative = $file.FullName.Substring($source.Length + 1)
    $target = Join-Path $destination $relative
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force
    Copy-Item -LiteralPath $file.FullName -Destination $target
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $hash) { throw 'Copy verification failed.' }
    $inventory += [ordered]@{ path = $relative.Replace('\', '/'); sha256 = $hash }
}
$report = [ordered]@{
    version = $manifest.version
    status = 'LOCAL_CANDIDATE_NOT_PUBLICATION_APPROVAL'
    limitations = 'Marker scan is not legal clearance or an exhaustive secret scan. Provenance and visual/Git acceptance remain required.'
    files = $inventory
}
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'inventory.json') -Encoding UTF8
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = Join-Path $output ($manifest.name + '-' + $manifest.version + '-source.zip')
[IO.Compression.ZipFile]::CreateFromDirectory($destination,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
$zipCheck = [IO.Compression.ZipFile]::OpenRead($zip)
try { if ($zipCheck.Entries.Count -ne $files.Count) { throw 'ZIP inventory mismatch, including hidden support files.' } }
finally { $zipCheck.Dispose() }
$archive = Join-Path $output ($manifest.name + '-' + $manifest.version + '.tgz')
& tar -czf $archive -C $output package
if ($LASTEXITCODE -ne 0) { throw 'Tarball creation failed.' }
$archiveEntries = @(& tar -tzf $archive)
if ($LASTEXITCODE -ne 0 -or $archiveEntries -notcontains 'package/package.json') { throw 'Tarball layout verification failed.' }
$report.zip = [ordered]@{ file = (Split-Path -Leaf $zip); sha256 = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash }
$report.archive = [ordered]@{ file = (Split-Path -Leaf $archive); sha256 = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash }
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'inventory.json') -Encoding UTF8
$sums = @(
    ("{0}  {1}" -f $report.zip.sha256,$report.zip.file)
    ("{0}  {1}" -f $report.archive.sha256,$report.archive.file)
    ("{0}  inventory.json" -f (Get-FileHash -LiteralPath (Join-Path $output 'inventory.json') -Algorithm SHA256).Hash)
)
$sums | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding ASCII
Write-Output "Local candidate only: $output"
Write-Output "Package Manager tarball: $archive"
