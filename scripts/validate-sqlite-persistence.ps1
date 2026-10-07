<#
.SYNOPSIS
Runs every saved example through an independently supplied BestFit runtime and records logical snapshots.
.DESCRIPTION
Uses fresh copies and one process per example. Source hashes and every executed runtime file are checked
after execution. Reports retain all schemas, rows, scientific payload hashes and initial/reopened
component identities, normalizing only LastModified columns and the Project.FullFileName column.
Compare reports from baseline and candidate runtimes, including the second serialization after reopen.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RuntimePath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$ExamplesRoot = (Join-Path $PSScriptRoot '..\examples')
)
$ErrorActionPreference = 'Stop'

<#
.SYNOPSIS
Hashes every file in the frozen runtime, including managed and native dependencies and probe outputs.
.PARAMETER Directory
The independently copied execution directory.
#>
function Get-PersistenceRuntimeManifest {
    param([string]$Directory)
    foreach ($file in Get-ChildItem -LiteralPath $Directory -Recurse -File -Force | Sort-Object FullName) {
        [ordered]@{
            path = [IO.Path]::GetRelativePath($Directory, $file.FullName).Replace('\', '/')
            length = $file.Length
            sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        }
    }
}

<#
.SYNOPSIS
Records the source identity of the probe, its project, and this runner.
#>
function Get-PersistenceSourceManifest {
    foreach ($relative in @('SQLitePersistenceProbe/Program.cs', 'SQLitePersistenceProbe/SQLitePersistenceProbe.csproj', 'validate-sqlite-persistence.ps1')) {
        [ordered]@{
            path = 'scripts/' + $relative
            sha256 = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $relative) -Algorithm SHA256).Hash
        }
    }
}

$runtime = (Resolve-Path -LiteralPath $RuntimePath).Path
$examples = (Resolve-Path -LiteralPath $ExamplesRoot).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw "Use a fresh output directory: $output" }
foreach ($protectedDirectory in @($runtime, $examples)) {
    $prefix = $protectedDirectory.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if ($output.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must be outside the supplied runtime and original examples directories.' }
}
New-Item -ItemType Directory -Path $output | Out-Null
$probe = Join-Path $output 'probe'
$probeSources = @(Get-PersistenceSourceManifest)
$probeSourcesJson = ConvertTo-Json -InputObject $probeSources -Depth 4 -Compress
Set-Content -LiteralPath (Join-Path $output 'probe-sources.json') -Value $probeSourcesJson -Encoding utf8
dotnet build (Join-Path $PSScriptRoot 'SQLitePersistenceProbe/SQLitePersistenceProbe.csproj') -c Release "-p:PersistenceRuntimePath=$runtime" -o $probe *> (Join-Path $output 'build.log')
if ($LASTEXITCODE -ne 0) { throw "Probe build failed; inspect $output/build.log" }
Get-ChildItem -LiteralPath $runtime | Copy-Item -Destination $probe -Recurse -Force
$native = Join-Path $runtime 'runtimes/win-x64/native'
if (Test-Path -LiteralPath $native) { Get-ChildItem -LiteralPath $native -File | Copy-Item -Destination $probe -Force }
$runtimeFiles = @(Get-PersistenceRuntimeManifest $probe)
$runtimeJson = ConvertTo-Json -InputObject $runtimeFiles -Depth 4 -Compress
$runtimeManifestSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($runtimeJson)))
Set-Content -LiteralPath (Join-Path $output 'runtime-manifest.json') -Value $runtimeJson -Encoding utf8
$sourceFiles = @(Get-ChildItem -LiteralPath $examples -Recurse -File -Filter '*.bestfit' | Sort-Object FullName)
if ($sourceFiles.Count -eq 0) { throw 'No saved project examples were found.' }
$results = @()
for ($index = 0; $index -lt $sourceFiles.Count; $index++) {
    $source = $sourceFiles[$index]
    $sourceHash = (Get-FileHash -LiteralPath $source.FullName -Algorithm SHA256).Hash
    $caseDirectory = Join-Path $output ('case-{0:D2}' -f $index)
    New-Item -ItemType Directory -Path $caseDirectory | Out-Null
    $copy = Join-Path $caseDirectory $source.Name
    $report = Join-Path $caseDirectory 'snapshot.json'
    dotnet (Join-Path $probe 'SQLitePersistenceProbe.dll') $source.FullName $copy $report *> (Join-Path $caseDirectory 'probe.log')
    $exitCode = $LASTEXITCODE
    $afterHash = (Get-FileHash -LiteralPath $source.FullName -Algorithm SHA256).Hash
    if ($sourceHash -ne $afterHash) { throw "Original example changed: $($source.FullName)" }
    $results += [ordered]@{
        index = $index
        example = [IO.Path]::GetRelativePath($examples, $source.FullName)
        sourceSha256 = $sourceHash
        exitCode = $exitCode
        snapshot = [IO.Path]::GetRelativePath($output, $report)
    }
    Write-Output ("{0}/{1} {2}: exit {3}" -f ($index + 1), $sourceFiles.Count, $source.Name, $exitCode)
}
$runtimeAfterJson = ConvertTo-Json -InputObject @(Get-PersistenceRuntimeManifest $probe) -Depth 4 -Compress
if ($runtimeJson -cne $runtimeAfterJson) { throw 'The executed runtime changed during validation; evidence is invalid.' }
$probeSourcesAfterJson = ConvertTo-Json -InputObject @(Get-PersistenceSourceManifest) -Depth 4 -Compress
if ($probeSourcesJson -cne $probeSourcesAfterJson) { throw 'The probe or runner sources changed during validation; evidence is invalid.' }
foreach ($result in $results) {
    $finalSourceHash = (Get-FileHash -LiteralPath (Join-Path $examples $result.example) -Algorithm SHA256).Hash
    if ($finalSourceHash -cne $result.sourceSha256) { throw "Original example changed after its probe: $($result.example)" }
}
[ordered]@{
    schemaVersion = 2
    runtimeSha256 = ($runtimeFiles | Where-Object path -ceq 'RMC.BestFit.UI.dll').sha256
    databaseManagerSha256 = ($runtimeFiles | Where-Object path -ceq 'DatabaseManager.dll').sha256
    runtimeFiles = $runtimeFiles
    runtimeManifestSha256 = $runtimeManifestSha256
    runtimeVerifiedUnchanged = $true
    probeSources = $probeSources
    probeSourcesVerifiedUnchanged = $true
    examples = $results
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'results.json') -Encoding utf8
if (@($results | Where-Object exitCode -ne 0).Count -ne 0) { throw 'One or more saved-project probes failed; inspect per-case logs.' }
