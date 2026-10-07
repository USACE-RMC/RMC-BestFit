<#
.SYNOPSIS
Compares all 32 saved-project cases and every captured JSON snapshot field without further normalization.
.DESCRIPTION
The probe alone normalizes LastModified and Project.FullFileName. This comparator preserves all other
fields, values, property identities and collection order. It verifies current original source SHA256
values against both reports, requires successful probes and all five integrity checks, writes every
observed difference, and fails if any difference or missing evidence exists. Runtime hashes are recorded
as provenance because baseline and candidate assemblies are intentionally different.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BaselineResults,
    [Parameter(Mandatory)][string]$CandidateResults,
    [Parameter(Mandatory)][string]$ReportPath,
    [string]$ExamplesRoot = (Join-Path $PSScriptRoot '..\examples')
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$differences = [Collections.Generic.List[object]]::new()
$documents = [Collections.Generic.List[System.Text.Json.JsonDocument]]::new()
$evidencePaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

function Add-PersistenceDifference {
    <# .SYNOPSIS Records a structured difference without truncating scientific evidence. #>
    param([string]$FieldPath, $Baseline, $Candidate, [string]$Reason)
    $differences.Add([ordered]@{ path = $FieldPath; baseline = $Baseline; candidate = $Candidate; reason = $Reason })
}

function Read-PersistenceDocument {
    <# .SYNOPSIS Parses JSON while retaining exact types, arrays and duplicate-property evidence. #>
    param([string]$Path)
    $null = $evidencePaths.Add([IO.Path]::GetFullPath($Path))
    $document = [System.Text.Json.JsonDocument]::Parse([IO.File]::ReadAllText($Path))
    $documents.Add($document)
    return $document.RootElement
}

function Compare-PersistenceJson {
    <# .SYNOPSIS Recursively records every scalar, field, object-shape and ordered-array difference. #>
    param([System.Text.Json.JsonElement]$Baseline, [System.Text.Json.JsonElement]$Candidate, [string]$FieldPath)
    if ($Baseline.ValueKind -ne $Candidate.ValueKind) {
        Add-PersistenceDifference $FieldPath $Baseline.GetRawText() $Candidate.GetRawText() 'JSON value type changed'
        return
    }
    switch ([string]$Baseline.ValueKind) {
        'Object' {
            $before = @($Baseline.EnumerateObject())
            $after = @($Candidate.EnumerateObject())
            $beforeNames = @($before | ForEach-Object Name)
            $afterNames = @($after | ForEach-Object Name)
            if (($beforeNames | ConvertTo-Json -Compress) -cne ($afterNames | ConvertTo-Json -Compress)) {
                Add-PersistenceDifference "$FieldPath.<fields>" $beforeNames $afterNames 'Object fields or field order changed'
            }
            $allNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
            foreach ($name in @($beforeNames) + @($afterNames)) { $null = $allNames.Add($name) }
            foreach ($name in $allNames) {
                $left = @($before | Where-Object { $_.Name -ceq $name })
                $right = @($after | Where-Object { $_.Name -ceq $name })
                if ($left.Count -gt 1 -or $right.Count -gt 1) {
                    Add-PersistenceDifference "$FieldPath.$name" $left.Count $right.Count 'Duplicate JSON property is ambiguous'
                }
                elseif ($left.Count -eq 0) { Add-PersistenceDifference "$FieldPath.$name" $null $right[0].Value.GetRawText() 'Added field' }
                elseif ($right.Count -eq 0) { Add-PersistenceDifference "$FieldPath.$name" $left[0].Value.GetRawText() $null 'Removed field' }
                else { Compare-PersistenceJson $left[0].Value $right[0].Value "$FieldPath.$name" }
            }
        }
        'Array' {
            $before = @($Baseline.EnumerateArray())
            $after = @($Candidate.EnumerateArray())
            if ($before.Count -ne $after.Count) { Add-PersistenceDifference "$FieldPath.<length>" $before.Count $after.Count 'Collection shape changed' }
            for ($index = 0; $index -lt [Math]::Max($before.Count, $after.Count); $index++) {
                if ($index -ge $before.Count) { Add-PersistenceDifference "$FieldPath[$index]" $null $after[$index].GetRawText() 'Added collection item' }
                elseif ($index -ge $after.Count) { Add-PersistenceDifference "$FieldPath[$index]" $before[$index].GetRawText() $null 'Removed collection item' }
                else { Compare-PersistenceJson $before[$index] $after[$index] "$FieldPath[$index]" }
            }
        }
        'String' {
            if ($Baseline.GetString() -cne $Candidate.GetString()) { Add-PersistenceDifference $FieldPath $Baseline.GetString() $Candidate.GetString() 'String or scientific cell identity changed' }
        }
        default {
            if ($Baseline.GetRawText() -cne $Candidate.GetRawText()) { Add-PersistenceDifference $FieldPath $Baseline.GetRawText() $Candidate.GetRawText() 'Scalar changed' }
        }
    }
}

function Resolve-PersistenceEvidencePath {
    <# .SYNOPSIS Requires a relative evidence path contained within its declared root. #>
    param([string]$Root, [string]$Relative)
    if ([string]::IsNullOrWhiteSpace($Relative) -or [IO.Path]::IsPathRooted($Relative)) { throw 'Evidence paths must be nonempty and relative.' }
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $path = [IO.Path]::GetFullPath((Join-Path $rootPath $Relative))
    if (!$path.StartsWith($rootPath, [StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence path escapes its root.' }
    return $path
}

function Assert-PersistenceCaseEvidence {
    <# .SYNOPSIS Verifies source identity, original hash and integrity evidence for one runtime case. #>
    param($Case, [string]$Label, [string]$ResultsRoot, [string]$OriginalRoot)
    $snapshot = $null
    try {
        $example = $Case.GetProperty('example').GetString()
        $sourceSha = $Case.GetProperty('sourceSha256').GetString()
        if ($sourceSha -notmatch '^[A-Fa-f0-9]{64}$') { throw 'Invalid source SHA256.' }
        $sourcePath = Resolve-PersistenceEvidencePath $OriginalRoot $example
        $null = $evidencePaths.Add($sourcePath)
        $actual = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
        if ($actual -cne $sourceSha) { Add-PersistenceDifference "$Label.$example.sourceSha256" $sourceSha $actual 'Original source hash changed or differs from report' }
        if ($Case.GetProperty('exitCode').GetInt32() -ne 0) { Add-PersistenceDifference "$Label.$example.exitCode" 0 $Case.GetProperty('exitCode').GetRawText() 'Probe did not succeed' }
        $snapshotPath = Resolve-PersistenceEvidencePath $ResultsRoot $Case.GetProperty('snapshot').GetString()
        $snapshot = Read-PersistenceDocument $snapshotPath
        $properties = @($snapshot.EnumerateObject())
        foreach ($stage in @('components','source','opened','saved','reopened','reopenedComponents','resaved')) {
            if (@($properties | Where-Object { $_.Name -ceq $stage }).Count -ne 1) { throw "Missing or duplicate snapshot stage: $stage" }
        }
        if ($snapshot.GetProperty('components').ValueKind -ne [System.Text.Json.JsonValueKind]::Array) { throw 'Component collection must be an array.' }
        foreach ($stage in @('source','opened','saved','reopened','resaved')) {
            $integrity = $snapshot.GetProperty($stage).GetProperty('Integrity').GetString()
            if ($integrity -cne 'ok') { Add-PersistenceDifference "$Label.$example.$stage.Integrity" 'ok' $integrity 'SQLite integrity check did not pass' }
        }
        return $snapshot
    }
    catch { Add-PersistenceDifference "$Label.case-evidence" $null $null $_.Exception.Message; return $snapshot }
}

function Assert-PersistenceProvenance {
    <# .SYNOPSIS Verifies schema2 executed-runtime and unchanged probe-source provenance. #>
    param([System.Text.Json.JsonElement]$Results, [string]$Label)
    if ($Results.GetProperty('schemaVersion').GetInt32() -ne 2) { throw "$Label results must use probe schemaVersion2." }
    foreach ($flag in @('runtimeVerifiedUnchanged','probeSourcesVerifiedUnchanged')) {
        if ($Results.GetProperty($flag).ValueKind -ne [System.Text.Json.JsonValueKind]::True) { throw "$Label $flag must be boolean true." }
    }
    $runtimeFiles = $Results.GetProperty('runtimeFiles')
    if ($runtimeFiles.ValueKind -ne [System.Text.Json.JsonValueKind]::Array -or $runtimeFiles.GetArrayLength() -eq 0) { throw "$Label runtimeFiles must be a nonempty array." }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $runtimeFiles.EnumerateArray()) {
        $path = $file.GetProperty('path').GetString()
        if ([string]::IsNullOrWhiteSpace($path) -or $path -match '(^/|\\|:|(^|/)\.\.(/|$))' -or !$seen.Add($path)) { throw "$Label runtime file path is unsafe or duplicated." }
        $length = $file.GetProperty('length').GetInt64()
        if ($length -lt 0) { throw "$Label runtime file length must be nonnegative." }
        if ($file.GetProperty('sha256').GetString() -notmatch '^[A-Fa-f0-9]{64}$') { throw "$Label runtime file SHA256 is invalid." }
        $fields = @($file.EnumerateObject() | ForEach-Object Name)
        if (($fields -join ',') -cne 'path,length,sha256') { throw "$Label runtime record fields/order differ from the probe schema." }
    }
    # The runner hashes UTF8 of PowerShell's compact JSON, excluding the saved file newline/BOM.
    $parsedFiles = ConvertFrom-Json -InputObject $runtimeFiles.GetRawText() -Depth 100 -NoEnumerate
    $canonical = ConvertTo-Json -InputObject $parsedFiles -Depth 4 -Compress
    $manifestHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($canonical)))
    $recordedManifestHash = $Results.GetProperty('runtimeManifestSha256').GetString()
    if ($recordedManifestHash -notmatch '^[A-Fa-f0-9]{64}$' -or $recordedManifestHash -cne $manifestHash) { throw "$Label runtime manifest SHA256 does not match its exact file inventory." }
    foreach ($pair in @(@('RMC.BestFit.UI.dll','runtimeSha256'),@('DatabaseManager.dll','databaseManagerSha256'))) {
        $matching = @($runtimeFiles.EnumerateArray() | Where-Object { $_.GetProperty('path').GetString() -ceq $pair[0] })
        if ($matching.Count -ne 1 -or $matching[0].GetProperty('sha256').GetString() -cne $Results.GetProperty($pair[1]).GetString()) { throw "$Label executed-copy hash does not match $($pair[0]) runtime inventory." }
    }
    $sources = $Results.GetProperty('probeSources')
    if ($sources.ValueKind -ne [System.Text.Json.JsonValueKind]::Array -or $sources.GetArrayLength() -ne 3) { throw "$Label must identify all three probe source files." }
    $expected = @('scripts/SQLitePersistenceProbe/Program.cs','scripts/SQLitePersistenceProbe/SQLitePersistenceProbe.csproj','scripts/validate-sqlite-persistence.ps1')
    $seenSources = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($source in $sources.EnumerateArray()) {
        $path = $source.GetProperty('path').GetString()
        if ($expected -cnotcontains $path -or !$seenSources.Add($path) -or $source.GetProperty('sha256').GetString() -notmatch '^[A-Fa-f0-9]{64}$') { throw "$Label probe source identity/hash is invalid or duplicated." }
        if ((@($source.EnumerateObject() | ForEach-Object Name) -join ',') -cne 'path,sha256') { throw "$Label probe source fields differ from schema2." }
    }
}

$provenance = [ordered]@{}
try {
    $baselinePath = (Resolve-Path -LiteralPath $BaselineResults).Path
    $candidatePath = (Resolve-Path -LiteralPath $CandidateResults).Path
    $originalRoot = (Resolve-Path -LiteralPath $ExamplesRoot).Path
    $baseline = Read-PersistenceDocument $baselinePath
    $candidate = Read-PersistenceDocument $candidatePath
    foreach ($entry in @(@('baseline',$baseline),@('candidate',$candidate))) {
        $label = $entry[0]; $results = $entry[1]
        try { Assert-PersistenceProvenance $results $label }
        catch { Add-PersistenceDifference "$label.provenance" $null $null $_.Exception.Message }
        foreach ($key in @('schemaVersion','runtimeManifestSha256','runtimeVerifiedUnchanged','probeSourcesVerifiedUnchanged')) {
            $field = [System.Text.Json.JsonElement]::new()
            if ($results.TryGetProperty($key,[ref]$field)) { $provenance["$label.$key"] = $field.GetRawText() }
        }
        foreach ($hash in @('runtimeSha256','databaseManagerSha256')) {
            $value = $results.GetProperty($hash).GetString()
            if ($value -notmatch '^[A-Fa-f0-9]{64}$') { throw "Invalid $label runtime hash: $hash" }
            $provenance["$label.$hash"] = $value
        }
    }
    Compare-PersistenceJson $baseline.GetProperty('probeSources') $candidate.GetProperty('probeSources') 'probeSources'
    $before = @($baseline.GetProperty('examples').EnumerateArray())
    $after = @($candidate.GetProperty('examples').EnumerateArray())
    foreach ($entry in @(@('baseline',$before),@('candidate',$after))) {
        if ($entry[1].Count -ne 32) { Add-PersistenceDifference "$($entry[0]).case-count" 32 $entry[1].Count 'Exactly 32 saved-project cases are required' }
    }
    # Compare report case identity and ordered metadata independently of runtime assembly provenance.
    Compare-PersistenceJson $baseline.GetProperty('examples') $candidate.GetProperty('examples') 'examples'
    $sourceNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($source in Get-ChildItem -LiteralPath $originalRoot -Recurse -File -Filter '*.bestfit') {
        $null = $sourceNames.Add([IO.Path]::GetRelativePath($originalRoot, $source.FullName))
    }
    if ($sourceNames.Count -ne 32) { Add-PersistenceDifference 'originals.case-count' 32 $sourceNames.Count 'Exact original case set required' }
    $maps = @{}
    foreach ($entry in @(@('baseline',$before,(Split-Path $baselinePath)),@('candidate',$after,(Split-Path $candidatePath)))) {
        $label = $entry[0]; $cases = $entry[1]; $resultsRoot = $entry[2]
        $map = [Collections.Generic.Dictionary[string,System.Text.Json.JsonElement]]::new([StringComparer]::Ordinal)
        $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($case in $cases) {
            $name = $case.GetProperty('example').GetString()
            if (!$seen.Add($name)) { Add-PersistenceDifference "$label.$name" 1 2 'Duplicate case identity'; continue }
            if (!$sourceNames.Contains($name)) { Add-PersistenceDifference "$label.$name" $null $name 'Report case is absent from original source set' }
            $snapshot = Assert-PersistenceCaseEvidence $case $label $resultsRoot $originalRoot
            if ($null -ne $snapshot) { $map.Add($name,$snapshot) }
        }
        foreach ($name in $sourceNames) {
            if (!$seen.Contains($name)) { Add-PersistenceDifference "$label.$name" $name $null 'Missing original case identity' }
        }
        $maps[$label] = $map
    }
    foreach ($name in $sourceNames) {
        if ($maps.baseline.ContainsKey($name) -and $maps.candidate.ContainsKey($name)) {
            Compare-PersistenceJson $maps.baseline[$name] $maps.candidate[$name] "snapshots.$name"
        }
    }
}
catch { Add-PersistenceDifference 'report-evidence' $null $null $_.Exception.Message }
finally {
    $fullReport = [IO.Path]::GetFullPath($ReportPath)
    if ($evidencePaths.Contains($fullReport)) {
        foreach ($document in $documents) { $document.Dispose() }
        throw "Comparison report must not overwrite source or snapshot evidence: $fullReport"
    }
    $null = New-Item -ItemType Directory -Path (Split-Path $fullReport) -Force
    [ordered]@{ schemaVersion = 1; success = ($differences.Count -eq 0); baselineResults = $BaselineResults; candidateResults = $CandidateResults; expectedCaseCount = 32; provenance = $provenance; differenceCount = $differences.Count; differences = @($differences.ToArray()) } |
        ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $fullReport -Encoding utf8NoBOM
    foreach ($document in $documents) { $document.Dispose() }
}
if ($differences.Count -ne 0) {
    foreach ($difference in $differences) { Write-Output "$($difference.path): $($difference.reason)" }
    throw "Saved-project comparison failed with $($differences.Count) differences. Full evidence: $fullReport"
}
Write-Output "PASS: all 32 source identities, original hashes, integrity checks and complete saved-project snapshots match. Report: $fullReport"
