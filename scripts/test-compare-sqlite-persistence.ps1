<#
.SYNOPSIS
Runs deterministic saved-project comparison fixtures without executing BestFit or changing real examples.
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$compare = Join-Path $PSScriptRoot 'compare-sqlite-persistence.ps1'
$temp = Join-Path ([IO.Path]::GetTempPath()) "persistence-comparison-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $temp | Out-Null
$script:Checks = 0
function Write-FixtureJson {
    <# .SYNOPSIS Writes fixture JSON with deterministic property and array order. #>
    param($Value, [string]$Path)
    $Value | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $Path -Encoding utf8NoBOM
}
function Invoke-FixtureComparison {
    <# .SYNOPSIS Runs the actual comparator and requires expected exit status and difference evidence. #>
    param([string]$Name, [bool]$Success, [string]$ExpectedPath = '')
    $report = Join-Path $temp "$Name-report.json"
    $output = & pwsh -NoProfile -File $compare -BaselineResults (Join-Path $temp 'baseline/results.json') -CandidateResults (Join-Path $temp 'candidate/results.json') -ExamplesRoot (Join-Path $temp 'originals') -ReportPath $report 2>&1
    $exitCode = $LASTEXITCODE
    $result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json -Depth 100
    if (($exitCode -eq 0) -ne $Success -or $result.success -ne $Success) { throw "Unexpected $Name outcome ($exitCode): $output" }
    if (!$Success -and ($result.differenceCount -eq 0 -or !(@($result.differences | Where-Object { $_.path.Contains($ExpectedPath) }).Count))) { throw "Missing $Name difference details: $ExpectedPath" }
    $script:Checks++
    Write-Host "PASS $Name (differences=$($result.differenceCount), exit=$exitCode)"
    return $result
}
function Reset-CandidateFixture {
    <# .SYNOPSIS Restores candidate reports after each isolated mutation. #>
    Copy-Item -LiteralPath (Join-Path $temp 'baseline/results.json') -Destination (Join-Path $temp 'candidate/results.json') -Force
    for ($index = 0; $index -lt 32; $index++) {
        Copy-Item -LiteralPath (Join-Path $temp ('baseline/case-{0:D2}/snapshot.json' -f $index)) -Destination (Join-Path $temp ('candidate/case-{0:D2}/snapshot.json' -f $index)) -Force
    }
}
try {
    foreach ($folder in @('originals','baseline','candidate')) { New-Item -ItemType Directory -Path (Join-Path $temp $folder) | Out-Null }
    $cases = @()
    for ($index = 0; $index -lt 32; $index++) {
        $name = 'example-{0:D2}.bestfit' -f $index
        $original = Join-Path $temp "originals/$name"
        Set-Content -LiteralPath $original -Value "deterministic original fixture $index" -Encoding utf8NoBOM
        $cases += [ordered]@{ index = $index; example = $name; sourceSha256 = (Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash; exitCode = 0; snapshot = ('case-{0:D2}/snapshot.json' -f $index) }
        $snapshot = [ordered]@{ components = @([ordered]@{ Name = 'Input Data'; Elements = @([ordered]@{ Name = 'Scientific source'; Type = 'RMC.BestFit.UI.InputData' }) }) }
        foreach ($stage in @('source','opened','saved','reopened','resaved')) {
            $snapshot[$stage] = [ordered]@{ Integrity = 'ok'; Schemas = @(,@('table','Scientific','Scientific','CREATE TABLE Scientific (value BLOB, identity TEXT)')); Tables = [ordered]@{ Scientific = [ordered]@{ Columns = @('value','identity'); Rows = @(@('Byte[]:4:AAAA','String:3:BBBB'),@('Byte[]:4:CCCC','String:3:DDDD')) } } }
        }
        $snapshot['reopenedComponents'] = $snapshot.components
        foreach ($folder in @('baseline','candidate')) {
            $directory = Join-Path $temp "$folder/case-$('{0:D2}' -f $index)"
            New-Item -ItemType Directory -Path $directory | Out-Null
            Write-FixtureJson $snapshot (Join-Path $directory 'snapshot.json')
        }
    }
    $runtimeFiles = @([ordered]@{ path = 'RMC.BestFit.UI.dll'; length = 100L; sha256 = 'A'*64 }, [ordered]@{ path = 'DatabaseManager.dll'; length = 200L; sha256 = 'B'*64 })
    $canonical = ConvertTo-Json -InputObject $runtimeFiles -Depth 4 -Compress
    $results = [ordered]@{
        schemaVersion = 2; runtimeSha256 = 'A'*64; databaseManagerSha256 = 'B'*64
        runtimeFiles = $runtimeFiles
        runtimeManifestSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($canonical)))
        runtimeVerifiedUnchanged = $true
        probeSources = @(@('scripts/SQLitePersistenceProbe/Program.cs','scripts/SQLitePersistenceProbe/SQLitePersistenceProbe.csproj','scripts/validate-sqlite-persistence.ps1') | ForEach-Object { [ordered]@{ path = $_; sha256 = 'E'*64 } })
        probeSourcesVerifiedUnchanged = $true; examples = $cases
    }
    Write-FixtureJson $results (Join-Path $temp 'baseline/results.json')
    Reset-CandidateFixture
    $null = Invoke-FixtureComparison 'identical32' $true
    # Candidate assembly identity intentionally changes while persistence remains identical.
    $candidateResults = Get-Content (Join-Path $temp 'candidate/results.json') -Raw | ConvertFrom-Json -Depth 100
    $candidateResults.runtimeSha256 = 'C'*64; $candidateResults.databaseManagerSha256 = 'D'*64
    $candidateResults.runtimeFiles[0].sha256 = 'C'*64; $candidateResults.runtimeFiles[1].sha256 = 'D'*64
    $canonical = ConvertTo-Json -InputObject $candidateResults.runtimeFiles -Depth 4 -Compress
    $candidateResults.runtimeManifestSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($canonical)))
    Write-FixtureJson $candidateResults (Join-Path $temp 'candidate/results.json')
    $null = Invoke-FixtureComparison 'distinct-runtime-provenance' $true
    $snapshotPath = Join-Path $temp 'candidate/case-00/snapshot.json'
    foreach ($stage in @('source','opened','saved','reopened','resaved')) {
        Reset-CandidateFixture
        $snapshot = Get-Content -LiteralPath $snapshotPath -Raw | ConvertFrom-Json -Depth 100
        $snapshot.$stage.Tables.Scientific.Rows[0][0] = 'Byte[]:4:FFFF'
        Write-FixtureJson $snapshot $snapshotPath
        $null = Invoke-FixtureComparison "scientific-bytes-$stage" $false "$stage.Tables.Scientific.Rows"
    }
    Reset-CandidateFixture
    $snapshot = Get-Content -LiteralPath $snapshotPath -Raw | ConvertFrom-Json -Depth 100
    $snapshot.saved.Schemas[0][3] = 'CREATE TABLE Scientific (value TEXT, identity TEXT)'
    Write-FixtureJson $snapshot $snapshotPath
    $null = Invoke-FixtureComparison 'schema-change' $false 'saved.Schemas'
    Reset-CandidateFixture
    $snapshot = Get-Content -LiteralPath $snapshotPath -Raw | ConvertFrom-Json -Depth 100
    $rows = $snapshot.saved.Tables.Scientific.Rows
    $snapshot.saved.Tables.Scientific.Rows = @($rows[1],$rows[0])
    Write-FixtureJson $snapshot $snapshotPath
    $null = Invoke-FixtureComparison 'row-order-change' $false 'saved.Tables.Scientific.Rows'
    Reset-CandidateFixture
    $snapshot = Get-Content -LiteralPath $snapshotPath -Raw | ConvertFrom-Json -Depth 100
    $snapshot.saved.Tables.Scientific.Columns = @('identity','value')
    Write-FixtureJson $snapshot $snapshotPath
    $null = Invoke-FixtureComparison 'column-order-change' $false 'saved.Tables.Scientific.Columns'
    Reset-CandidateFixture
    $snapshot = Get-Content -LiteralPath $snapshotPath -Raw | ConvertFrom-Json -Depth 100
    $snapshot.components[0].Elements = @()
    Write-FixtureJson $snapshot $snapshotPath
    $null = Invoke-FixtureComparison 'collection-shape-change' $false 'components'
    Reset-CandidateFixture
    $candidateResults = Get-Content (Join-Path $temp 'candidate/results.json') -Raw | ConvertFrom-Json -Depth 100
    $candidateResults.examples = @($candidateResults.examples[0..30])
    Write-FixtureJson $candidateResults (Join-Path $temp 'candidate/results.json')
    $null = Invoke-FixtureComparison 'missing-case' $false 'case-count'
    Reset-CandidateFixture
    $candidateResults = Get-Content (Join-Path $temp 'candidate/results.json') -Raw | ConvertFrom-Json -Depth 100
    $candidateResults.examples[0].sourceSha256 = 'E'*64
    Write-FixtureJson $candidateResults (Join-Path $temp 'candidate/results.json')
    $null = Invoke-FixtureComparison 'source-report-hash-change' $false 'sourceSha256'
    Reset-CandidateFixture
    $originalPath = Join-Path $temp 'originals/example-00.bestfit'
    $originalBytes = [IO.File]::ReadAllBytes($originalPath)
    Set-Content -LiteralPath $originalPath -Value 'changed original bytes'
    $null = Invoke-FixtureComparison 'original-file-hash-change' $false 'sourceSha256'
    [IO.File]::WriteAllBytes($originalPath,$originalBytes)
    foreach ($stage in @('source','opened','saved','reopened','resaved')) {
        Reset-CandidateFixture
        $snapshot = Get-Content -LiteralPath $snapshotPath -Raw | ConvertFrom-Json -Depth 100
        $snapshot.$stage.Integrity = 'corrupt'
        Write-FixtureJson $snapshot $snapshotPath
        $null = Invoke-FixtureComparison "integrity-$stage" $false "$stage.Integrity"
    }
    Reset-CandidateFixture
    $snapshot = Get-Content -LiteralPath $snapshotPath -Raw | ConvertFrom-Json -Depth 100
    $snapshot.saved.Tables.Scientific.Rows[0][0] = 'Byte[]:4:FFFF'
    $snapshot.reopened.Tables.Scientific.Rows[1][1] = 'String:3:EEEE'
    $snapshot.components[0].Name = 'Changed collection'
    Write-FixtureJson $snapshot $snapshotPath
    $all = Invoke-FixtureComparison 'all-differences-retained' $false 'components'
    if ($all.differenceCount -ne 3) { throw "Expected every one of three independent changes, got $($all.differenceCount)." }
    Reset-CandidateFixture
    $snapshot = Get-Content -LiteralPath $snapshotPath -Raw | ConvertFrom-Json -Depth 100
    $snapshot.reopenedComponents[0].Elements[0].Type = 'Changed scientific component type'
    Write-FixtureJson $snapshot $snapshotPath
    $null = Invoke-FixtureComparison 'reopened-components-payload' $false 'reopenedComponents'
    foreach ($stage in @('components','source','opened','saved','reopened','reopenedComponents','resaved')) {
        Reset-CandidateFixture
        $snapshot = Get-Content -LiteralPath $snapshotPath -Raw | ConvertFrom-Json -Depth 100
        $snapshot.PSObject.Properties.Remove($stage)
        Write-FixtureJson $snapshot $snapshotPath
        $null = Invoke-FixtureComparison "missing-stage-$stage" $false 'case-evidence'
    }
    foreach ($flag in @('runtimeVerifiedUnchanged','probeSourcesVerifiedUnchanged')) {
        Reset-CandidateFixture
        $candidateResults = Get-Content (Join-Path $temp 'candidate/results.json') -Raw | ConvertFrom-Json -Depth 100
        $candidateResults.$flag = $false
        Write-FixtureJson $candidateResults (Join-Path $temp 'candidate/results.json')
        $null = Invoke-FixtureComparison "false-$flag" $false 'provenance'
    }
    foreach ($mutation in @('schema','manifest-hash','runtime-empty','runtime-bad-hash','runtime-fractional-length','runtime-duplicate','sources-empty','sources-bad-hash','sources-hash-mismatch','sources-duplicate')) {
        Reset-CandidateFixture
        $candidateResults = Get-Content (Join-Path $temp 'candidate/results.json') -Raw | ConvertFrom-Json -Depth 100
        switch ($mutation) {
            'schema' { $candidateResults.schemaVersion = 1 }
            'manifest-hash' { $candidateResults.runtimeManifestSha256 = 'F'*64 }
            'runtime-empty' { $candidateResults.runtimeFiles = @() }
            'runtime-bad-hash' { $candidateResults.runtimeFiles[0].sha256 = 'bad' }
            'runtime-fractional-length' { $candidateResults.runtimeFiles[0].length = 0.5 }
            'runtime-duplicate' { $candidateResults.runtimeFiles[1].path = $candidateResults.runtimeFiles[0].path }
            'sources-empty' { $candidateResults.probeSources = @() }
            'sources-bad-hash' { $candidateResults.probeSources[0].sha256 = 'bad' }
            'sources-hash-mismatch' { $candidateResults.probeSources[0].sha256 = 'F'*64 }
            'sources-duplicate' { $candidateResults.probeSources[1].path = $candidateResults.probeSources[0].path }
        }
        Write-FixtureJson $candidateResults (Join-Path $temp 'candidate/results.json')
        $expected = if ($mutation -eq 'sources-hash-mismatch') { 'probeSources' } else { 'provenance' }
        $null = Invoke-FixtureComparison "provenance-$mutation" $false $expected
    }
    Reset-CandidateFixture
    $protectedPath = Join-Path $temp 'baseline/results.json'
    $beforeHash = (Get-FileHash -LiteralPath $protectedPath -Algorithm SHA256).Hash
    $null = & pwsh -NoProfile -File $compare -BaselineResults $protectedPath -CandidateResults (Join-Path $temp 'candidate/results.json') -ExamplesRoot (Join-Path $temp 'originals') -ReportPath $protectedPath 2>&1
    if ($LASTEXITCODE -eq 0 -or (Get-FileHash -LiteralPath $protectedPath -Algorithm SHA256).Hash -ne $beforeHash) { throw 'Evidence overwrite was not refused safely.' }
    $script:Checks++
    Write-Host 'PASS source/report evidence overwrite refused'
    Write-Host "PASS: $script:Checks deterministic persistence comparison checks. Real examples were not used or changed."
}
finally {
    $resolved = [IO.Path]::GetFullPath($temp)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if ((Split-Path $resolved -Parent) -ne $tempRoot -or (Split-Path $resolved -Leaf) -notmatch '^persistence-comparison-[a-f0-9]{32}$') { throw 'Unsafe fixture cleanup target.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
