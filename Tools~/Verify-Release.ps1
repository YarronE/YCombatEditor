[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $UnityEditor,
    [string] $PackageRoot,
    [string] $WorkRoot,
    [string] $PackageArchive,
    [string] $PackageGitUrl,
    [switch] $BuildPlayer
)

$ErrorActionPreference = 'Stop'
if (!$PackageRoot) { $PackageRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path }
if (!$WorkRoot) { $WorkRoot = Join-Path ([IO.Path]::GetTempPath()) 'YCombatEditorValidation' }
if ($PackageArchive -and $PackageGitUrl) { throw 'Choose a tarball or Git installation, not both.' }
$repo = (Resolve-Path -LiteralPath $PackageRoot).Path
$workPath = [IO.Path]::GetFullPath($WorkRoot).TrimEnd('\','/')
if ($workPath -eq $repo -or $workPath.StartsWith($repo.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'WorkRoot must be outside the package source.' }
$unity = (Resolve-Path -LiteralPath $UnityEditor).Path
$package = $repo
if (!(Test-Path -LiteralPath (Join-Path $package 'package.json'))) { throw 'Package not found.' }

# Every run receives a fresh project. Never overwrite or delete the user's project,
# and never reuse old assemblies or stale test results as evidence.
$runId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$project = Join-Path $WorkRoot ("projects/" + $runId)
$evidence = Join-Path $WorkRoot ("evidence/" + $runId)
$null = New-Item -ItemType Directory -Path $project, $evidence
$null = New-Item -ItemType Directory -Path (Join-Path $project 'Assets'), (Join-Path $project 'Packages'), (Join-Path $project 'ProjectSettings')

function Get-Snapshot([string] $directory) {
    @(Get-ChildItem -LiteralPath $directory -File -Recurse | Sort-Object FullName | ForEach-Object {
        [ordered]@{
            path = $_.FullName.Substring($repo.Length + 1).Replace('\', '/')
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    })
}

$packageBefore = Get-Snapshot $package
$assetsBefore = @()
if (!$PackageArchive -and !$PackageGitUrl) { throw 'Supply a built PackageArchive or a published PackageGitUrl for release verification.' }

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Validation/manifest.json') -Destination (Join-Path $project 'Packages/manifest.json')
[IO.File]::WriteAllText((Join-Path $project 'ProjectSettings/ProjectVersion.txt'), "m_EditorVersion: 6000.3.6f1`n", (New-Object Text.UTF8Encoding($false)))

$archiveHash = $null
if ($PackageArchive) {
    $PackageArchive = (Resolve-Path -LiteralPath $PackageArchive).Path
    $archiveHash = (Get-FileHash -LiteralPath $PackageArchive -Algorithm SHA256).Hash
    $members = @(& tar -tzf $PackageArchive)
    if ($LASTEXITCODE -ne 0 -or $members -notcontains 'package/package.json') { throw 'Invalid package archive.' }
    if ($members | Where-Object { $_ -notmatch '^package/' -or $_ -match '(^|/)[.][.](/|$)' -or $_.Contains('\') }) { throw 'Unsafe archive member path.' }
    $details = @(& tar -tvzf $PackageArchive)
    if ($LASTEXITCODE -ne 0 -or ($details | Where-Object { $_ -notmatch '^[-d]' })) { throw 'Archive links or special entries are not supported.' }
    $unpacked = Join-Path $evidence 'unpacked'
    $null = New-Item -ItemType Directory -Path $unpacked
    & tar -xzf $PackageArchive -C $unpacked
    if ($LASTEXITCODE -ne 0) { throw 'Archive extraction failed.' }
    $manifestPath = Join-Path $project 'Packages/manifest.json'
    $testManifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $testManifest.dependencies | Add-Member -NotePropertyName 'com.ethan.act-action-editor' -NotePropertyValue ('file:' + $PackageArchive.Replace('\', '/'))
    [IO.File]::WriteAllText($manifestPath, ($testManifest | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding($false)))
    $sampleSource = Join-Path $unpacked 'package/Samples~/BasicPlayback'
} else {
    $sampleSource = Join-Path $package 'Samples~/BasicPlayback'
}
$gitCommit = $null
if ($PackageGitUrl) {
    if ($PackageGitUrl -notmatch '^(https://github[.]com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+[.]git)#(v[0-9][A-Za-z0-9_.-]*)$') { throw 'Supply an HTTPS GitHub URL pinned to a version tag.' }
    $gitRemote = $Matches[1]; $gitTag = $Matches[2]
    $gitSource = Join-Path $evidence 'git-source'
    & git clone --depth 1 --branch $gitTag -- $gitRemote $gitSource
    if ($LASTEXITCODE -ne 0) { throw 'Cannot fetch the release tag.' }
    $gitCommit = (& git -C $gitSource rev-parse HEAD).Trim()
    $manifestPath = Join-Path $project 'Packages/manifest.json'
    $testManifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $testManifest.dependencies | Add-Member -NotePropertyName 'com.ethan.act-action-editor' -NotePropertyValue $PackageGitUrl
    [IO.File]::WriteAllText($manifestPath, ($testManifest | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding($false)))
    $sampleSource = Join-Path $gitSource 'Samples~/BasicPlayback'
}
Copy-Item -LiteralPath $sampleSource -Destination (Join-Path $project 'Assets/BasicPlayback') -Recurse
$results = Join-Path $evidence 'TestResults.xml'
$log = Join-Path $evidence 'Unity.log'
# Window repaint regressions need a graphics device even in batch mode.
$arguments = '-batchmode -projectPath "{0}" -runTests -testPlatform EditMode -testResults "{1}" -logFile "{2}"' -f $project, $results, $log
Write-Output "Testing package in fresh Unity project: $project"
$process = Start-Process -FilePath $unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
$exitCode = $process.ExitCode
if (!(Test-Path -LiteralPath $results)) { throw "Unity produced no test results (exit $exitCode). Inspect $log" }

$xml = New-Object System.Xml.XmlDocument
$xml.Load($results)
$run = $xml.'test-run'
$sampleResult = $xml.SelectSingleNode("//test-case[@name='ImportedSample_GeneratesUniqueAssets_AndPlays']")
$samplePassed = $null -ne $sampleResult -and $sampleResult.result -eq 'Passed'
$interactionResult = $xml.SelectSingleNode("//test-case[@name='ImportedInteractionSample_ResolvesRealWindowsAndCustomConditions']")
$interactionPassed = $null -ne $interactionResult -and $interactionResult.result -eq 'Passed'
$packageAfter = Get-Snapshot $package
$assetsAfter = @()
$packageUnchanged = (ConvertTo-Json -InputObject $packageBefore -Depth 4 -Compress) -ceq (ConvertTo-Json -InputObject $packageAfter -Depth 4 -Compress)
$assetsUnchanged = (ConvertTo-Json -InputObject $assetsBefore -Depth 4 -Compress) -ceq (ConvertTo-Json -InputObject $assetsAfter -Depth 4 -Compress)
$archiveUnchanged = !$PackageArchive -or (Get-FileHash -LiteralPath $PackageArchive -Algorithm SHA256).Hash -eq $archiveHash
$installedAsTarball = $false
if ($PackageArchive) {
    $lock = Get-Content -LiteralPath (Join-Path $project 'Packages/packages-lock.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $installed = $lock.dependencies.'com.ethan.act-action-editor'
    $installedAsTarball = $installed.source -eq 'local-tarball' -and $installed.version -eq ('file:' + $PackageArchive.Replace('\', '/'))
}
$installedAsGit = $false
if ($PackageGitUrl) {
    $lock = Get-Content -LiteralPath (Join-Path $project 'Packages/packages-lock.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $installed = $lock.dependencies.'com.ethan.act-action-editor'
    $installedAsGit = $installed.source -eq 'git' -and $installed.hash -eq $gitCommit
}
$buildPassed = $false
if ($BuildPlayer -and $exitCode -eq 0 -and $run.result -eq 'Passed') {
    $buildLog = Join-Path $evidence 'PlayerBuild.log'
    $buildArguments = '-batchmode -nographics -projectPath "{0}" -executeMethod Ethan.ActionEditor.Samples.SamplePlayerBuild.Run -logFile "{1}"' -f $project,$buildLog
    $buildProcess = Start-Process -FilePath $unity -ArgumentList $buildArguments -WindowStyle Hidden -PassThru
    $buildProcess.WaitForExit()
    $buildResult = Join-Path $project 'Builds/result.json'
    if (Test-Path -LiteralPath $buildResult) {
        Copy-Item -LiteralPath $buildResult -Destination (Join-Path $evidence 'PlayerBuild.json')
        $buildPassed = $buildProcess.ExitCode -eq 0 -and (Get-Content -LiteralPath $buildResult -Raw -Encoding UTF8 | ConvertFrom-Json).success
    }
}
$report = [ordered]@{
    gitUrl = $PackageGitUrl
    gitCommit = $gitCommit
    installedAsGit = $installedAsGit
    playerBuildRequested = [bool]$BuildPlayer
    playerBuildPassed = $buildPassed
    archiveUnchangedDuringRun = $archiveUnchanged
    installedAsTarball = $installedAsTarball
    archive = $PackageArchive
    archiveSha256 = $archiveHash
    createdUtc = [DateTime]::UtcNow.ToString('o')
    unity = $unity
    project = $project
    exitCode = $exitCode
    result = [string]$run.result
    total = [int]$run.total
    passed = [int]$run.passed
    failed = [int]$run.failed
    skipped = [int]$run.skipped
    importedSamplePassed = $samplePassed
    importedInteractionSamplePassed = $interactionPassed
    sourceUnchangedDuringRun = $packageUnchanged
    legacyAssetsUnchanged = $assetsUnchanged
    packageSnapshot = $packageBefore
    legacyAssetSnapshot = $assetsBefore
    scope = 'Core, imported playback/interaction samples, selected installation mode and optional Windows Player build; not manual UX, optional integrations or full game parity.'
}
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $evidence 'summary.json') -Encoding UTF8
Write-Output ("{0}: {1}/{2} passed; evidence: {3}" -f $run.result, $run.passed, $run.total, $evidence)
if (!$interactionPassed -or [int]$run.skipped -ne 0 -or ($BuildPlayer -and !$buildPassed) -or ($PackageGitUrl -and !$installedAsGit) -or !$archiveUnchanged -or ($PackageArchive -and !$installedAsTarball) -or $exitCode -ne 0 -or $run.result -ne 'Passed' -or [int]$run.total -eq 0 -or !$samplePassed -or !$packageUnchanged -or !$assetsUnchanged) {
    throw 'Validation failed or source changed during the run. See summary.json and Unity.log.'
}
