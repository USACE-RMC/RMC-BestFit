<#
.SYNOPSIS
Runs fixture-driven behavioral tests for the Verification catalog validator.

.DESCRIPTION
Creates isolated source, catalog, and report fixtures and invokes
validate-verification-catalog.ps1 in a child PowerShell process. The fixtures exercise
successful validation, default and strict gap handling, source traceability, duplicate
detection, evidence-kind validation, recovery sample-size governance, report anchors,
ordinary and data-driven method discovery, and DataRow execution-unit reconciliation.

The validator's command contract is intentionally tested at the process boundary:

- Exit code 0 means the catalog is structurally valid. The final message is
  "Verification catalog validation passed" and reports the number of open gaps.
- Exit code 1 means validation failed. Every diagnostic begins with
  "ERROR [<code>]", and the final message is
  "Verification catalog validation FAILED with <n> error(s)."
- Default mode reports open gaps without failing; -RequireComplete emits
  ERROR [OPEN_GAP] for each unresolved catalog entry and exits 1.

No Verification test assembly or scientific test method is built or executed.

.EXAMPLE
.\scripts\test-verification-catalog-validator.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$validatorPath = Join-Path $PSScriptRoot 'validate-verification-catalog.ps1'
$powerShellPath = (Get-Process -Id $PID).Path

if (-not (Test-Path -LiteralPath $validatorPath -PathType Leaf)) {
    Write-Error "Validator script not found: $validatorPath"
    exit 1
}

function Write-Utf8File {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Content
    )

    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }

    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
}

function New-CatalogEntry {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Method,
        [string]$Class = 'SampleTests',
        [string]$Source = 'src/RMC.BestFit.Verification/SampleTests.cs',
        [object[]]$MethodOverrides = @()
    )

    return [ordered]@{
        source = $Source
        namespace = 'Fixture.Verification'
        class = $Class
        method = $Method
        analysis = 'SampleAnalysis'
        model = 'SampleModel'
        primaryEvidenceKind = 'analytical'
        evidenceTags = @('analytical', 'independent')
        disposition = 'retain-verification'
        methodOverrides = @($MethodOverrides)
        executesEstimator = $false
        sampleUnit = $null
        sampleSize = $null
        seed = $null
        oracle = 'Hand-derived exact value.'
        acceptanceRule = 'Exact equality.'
        artifact = $null
        reportAnchor = 'docs/verification/sample-report.md#sample-evidence'
        status = 'verified'
        gap = $null
    }
}

function Initialize-Fixture {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,
        [switch]$IncludeDataMethod,
        [switch]$IncludeSecondOrdinaryMethod,
        [switch]$IncludeNestedHelper,
        [switch]$IncludeSecondTestClass,
        [switch]$IncludePartialClassFile,
        [switch]$ScopedSourceRoot
    )

    $verificationRoot = Join-Path $Root 'src\RMC.BestFit.Verification'
    $sourceRoot = if ($ScopedSourceRoot) { Join-Path $verificationRoot 'ModelEstimation' } else { $verificationRoot }
    $catalogPath = Join-Path $Root 'docs\verification\verification-catalog.json'
    $sourcePath = Join-Path $sourceRoot 'SampleTests.cs'
    $reportPath = Join-Path $Root 'docs\verification\sample-report.md'

    $secondOrdinary = if ($IncludeSecondOrdinaryMethod) {
        @'

    [TestMethod]
    public void Ordinary_Second()
    {
    }
'@
    }
    else {
        ''
    }

    $nestedHelper = if ($IncludeNestedHelper) {
        @'

    private sealed class NestedHelper
    {
    }
'@
    }
    else {
        ''
    }

    $dataMethod = if ($IncludeDataMethod) {
        @'

    [DataTestMethod]
    [DataRow(1, DisplayName = "row-one")]
    [DataRow(2, DisplayName = "row-two")]
    public void Data_IsCataloged(int value)
    {
    }
'@
    }
    else {
        ''
    }

    $secondTestClass = if ($IncludeSecondTestClass) {
        @'

[TestClass]
public class SecondTests
{
    [TestMethod]
    public void Second_IsCataloged()
    {
    }
}
'@
    }
    else {
        ''
    }

    $sampleClassDeclaration = if ($IncludePartialClassFile) { 'public partial class SampleTests' } else { 'public class SampleTests' }
    $source = @"
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fixture.Verification;

[TestClass]
$sampleClassDeclaration
{
    [TestMethod]
    public void Ordinary_IsCataloged()
    {
    }$nestedHelper$secondOrdinary$dataMethod
}
$secondTestClass
"@

    $sourceWithWindowsLineEndings = [regex]::Replace($source, '\r?\n', "`r`n")
    Write-Utf8File -Path $sourcePath -Content $sourceWithWindowsLineEndings
    if ($IncludePartialClassFile) {
        $partialSource = @'
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Fixture.Verification;

public partial class SampleTests
{
    [TestMethod]
    public void Partial_IsCataloged()
    {
    }
}
'@
        $partialPath = Join-Path $sourceRoot 'SampleTests.Partial.cs'
        $partialWithWindowsLineEndings = [regex]::Replace($partialSource, '\r?\n', "`r`n")
        Write-Utf8File -Path $partialPath -Content $partialWithWindowsLineEndings
    }
    Write-Utf8File -Path $reportPath -Content "# Sample Report`n`n## Sample Evidence`n"

    return [pscustomobject]@{
        Root = $Root
        SourceRoot = $sourceRoot
        CatalogPath = $catalogPath
        SourcePath = $sourcePath
    }
}

function Write-Catalog {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [object[]]$Entries
    )

    $catalog = [ordered]@{
        schemaVersion = 1
        entries = @($Entries)
    }
    Write-CatalogDocument -Path $Path -Catalog $catalog
}

function Write-CatalogDocument {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [object]$Catalog
    )

    Write-Utf8File -Path $Path -Content ($Catalog | ConvertTo-Json -Depth 20)
}

function Invoke-CatalogValidator {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Catalog,
        [Parameter(Mandatory = $true)]
        [string]$SourceRoot,
        [switch]$RequireComplete
    )

    $arguments = @(
        '-NoLogo',
        '-NoProfile',
        '-File',
        $validatorPath,
        '-Catalog',
        $Catalog,
        '-SourceRoot',
        $SourceRoot
    )
    if ($RequireComplete) {
        $arguments += '-RequireComplete'
    }

    $output = & $powerShellPath @arguments 2>&1 | Out-String
    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output = $output
    }
}

function Assert-ValidatorResult {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [int]$ExpectedExitCode,
        [Parameter(Mandatory = $true)]
        [string]$ExpectedMessage,
        [string]$ExpectedAdditionalMessage,
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Actual
    )

    if ($Actual.ExitCode -ne $ExpectedExitCode) {
        throw "$Name expected exit code $ExpectedExitCode but received $($Actual.ExitCode). Output:`n$($Actual.Output)"
    }
    if ($Actual.Output.IndexOf($ExpectedMessage, [System.StringComparison]::Ordinal) -lt 0) {
        throw "$Name expected output containing '$ExpectedMessage'. Output:`n$($Actual.Output)"
    }
    if (-not [string]::IsNullOrWhiteSpace($ExpectedAdditionalMessage) -and
        $Actual.Output.IndexOf($ExpectedAdditionalMessage, [System.StringComparison]::Ordinal) -lt 0) {
        throw "$Name expected output containing '$ExpectedAdditionalMessage'. Output:`n$($Actual.Output)"
    }

    $outputLines = @($Actual.Output -split '\r?\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($outputLines.Count -eq 0) {
        throw "$Name produced no non-empty output lines."
    }
    $finalLine = $outputLines[-1]
    $diagnosticLines = if ($outputLines.Count -gt 1) { @($outputLines[0..($outputLines.Count - 2)]) } else { @() }
    if ($ExpectedExitCode -eq 0) {
        if ($finalLine -notmatch '^Verification catalog validation passed with \d+ open gap\(s\)\. Discovered \d+ method declaration\(s\) and \d+ execution unit\(s\)\.$') {
            throw "$Name final success line does not match the documented contract: '$finalLine'."
        }
        foreach ($line in $diagnosticLines) {
            if (-not $line.StartsWith('GAP [', [System.StringComparison]::Ordinal)) {
                throw "$Name success notice does not begin with 'GAP [': '$line'."
            }
        }
    }
    else {
        if ($finalLine -notmatch '^Verification catalog validation FAILED with \d+ error\(s\)\.$') {
            throw "$Name final failure line does not match the documented contract: '$finalLine'."
        }
        foreach ($line in $diagnosticLines) {
            if (-not $line.StartsWith('ERROR [', [System.StringComparison]::Ordinal)) {
                throw "$Name failure diagnostic does not begin with 'ERROR [': '$line'."
            }
        }
    }

    Write-Host "PASS: $Name"
}

function Invoke-FixtureCase {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [scriptblock]$Body
    )

    $caseRoot = Join-Path $script:testRoot $Name
    New-Item -ItemType Directory -Path $caseRoot -Force | Out-Null
    & $Body $caseRoot
}

$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("rmc-verification-catalog-validator-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null

try {
    Invoke-FixtureCase -Name 'valid-catalog' -Body {
        param($caseRoot)
        $fixture = Initialize-Fixture -Root $caseRoot -IncludeDataMethod
        $ordinary = New-CatalogEntry -Method 'Ordinary_IsCataloged'
        $data = New-CatalogEntry -Method 'Data_IsCataloged' -MethodOverrides @(
            [ordered]@{ name = 'row-one' },
            [ordered]@{ name = 'row-two' }
        )
        Write-Catalog -Path $fixture.CatalogPath -Entries @($ordinary, $data)
        $actual = Invoke-CatalogValidator -Catalog $fixture.CatalogPath -SourceRoot $fixture.SourceRoot
        Assert-ValidatorResult -Name 'valid catalog' -ExpectedExitCode 0 -ExpectedMessage 'Verification catalog validation passed with 0 open gap(s).' -Actual $actual
    }

    Invoke-FixtureCase -Name 'single-evidence-tag' -Body {
        param($caseRoot)
        $fixture = Initialize-Fixture -Root $caseRoot
        $entry = New-CatalogEntry -Method 'Ordinary_IsCataloged'
        $entry.evidenceTags = @('analytical')
        Write-Catalog -Path $fixture.CatalogPath -Entries @($entry)
        $actual = Invoke-CatalogValidator -Catalog $fixture.CatalogPath -SourceRoot $fixture.SourceRoot
        Assert-ValidatorResult -Name 'single evidence tag remains an array' -ExpectedExitCode 0 -ExpectedMessage 'Verification catalog validation passed with 0 open gap(s).' -Actual $actual
    }

    Invoke-FixtureCase -Name 'nested-helper-containment' -Body {
        param($caseRoot)
        $fixture = Initialize-Fixture -Root $caseRoot -IncludeNestedHelper -IncludeSecondOrdinaryMethod
        $first = New-CatalogEntry -Method 'Ordinary_IsCataloged'
        $second = New-CatalogEntry -Method 'Ordinary_Second'
        Write-Catalog -Path $fixture.CatalogPath -Entries @($first, $second)
        $actual = Invoke-CatalogValidator -Catalog $fixture.CatalogPath -SourceRoot $fixture.SourceRoot
        Assert-ValidatorResult -Name 'nested helper does not replace enclosing test class' -ExpectedExitCode 0 -ExpectedMessage 'Verification catalog validation passed with 0 open gap(s).' -Actual $actual
    }

    Invoke-FixtureCase -Name 'multiple-test-classes' -Body {
        param($caseRoot)
        $fixture = Initialize-Fixture -Root $caseRoot -IncludeSecondTestClass
        $first = New-CatalogEntry -Method 'Ordinary_IsCataloged'
        $second = New-CatalogEntry -Method 'Second_IsCataloged' -Class 'SecondTests'
        Write-Catalog -Path $fixture.CatalogPath -Entries @($first, $second)
        $actual = Invoke-CatalogValidator -Catalog $fixture.CatalogPath -SourceRoot $fixture.SourceRoot
        Assert-ValidatorResult -Name 'multiple test classes retain their own identities' -ExpectedExitCode 0 -ExpectedMessage 'Verification catalog validation passed with 0 open gap(s).' -Actual $actual
    }

    Invoke-FixtureCase -Name 'partial-test-class' -Body {
        param($caseRoot)
        $fixture = Initialize-Fixture -Root $caseRoot -IncludePartialClassFile
        $first = New-CatalogEntry -Method 'Ordinary_IsCataloged'
        $partial = New-CatalogEntry -Method 'Partial_IsCataloged' -Source 'src/RMC.BestFit.Verification/SampleTests.Partial.cs'
        Write-Catalog -Path $fixture.CatalogPath -Entries @($first, $partial)
        $actual = Invoke-CatalogValidator -Catalog $fixture.CatalogPath -SourceRoot $fixture.SourceRoot
        Assert-ValidatorResult -Name 'partial declaration inherits TestClass identity across files' -ExpectedExitCode 0 -ExpectedMessage 'Verification catalog validation passed with 0 open gap(s).' -Actual $actual
    }

    Invoke-FixtureCase -Name 'scoped-source-root' -Body {
        param($caseRoot)
        $fixture = Initialize-Fixture -Root $caseRoot -ScopedSourceRoot
        $entry = New-CatalogEntry -Method 'Ordinary_IsCataloged' -Source 'src/RMC.BestFit.Verification/ModelEstimation/SampleTests.cs'
        Write-Catalog -Path $fixture.CatalogPath -Entries @($entry)
        $actual = Invoke-CatalogValidator -Catalog $fixture.CatalogPath -SourceRoot $fixture.SourceRoot
        Assert-ValidatorResult -Name 'scoped SourceRoot resolves repository-relative paths' -ExpectedExitCode 0 -ExpectedMessage 'Verification catalog validation passed with 0 open gap(s).' -Actual $actual
    }

    Invoke-FixtureCase -Name 'default-open-gap' -Body {
        param($caseRoot)
        $fixture = Initialize-Fixture -Root $caseRoot
        $entry = New-CatalogEntry -Method 'Ordinary_IsCataloged'
        $entry.status = 'open'
        $entry.gap = 'Independent oracle is pending.'
        Write-Catalog -Path $fixture.CatalogPath -Entries @($entry)
        $actual = Invoke-CatalogValidator -Catalog $fixture.CatalogPath -SourceRoot $fixture.SourceRoot
        Assert-ValidatorResult -Name 'default mode reports an open gap' -ExpectedExitCode 0 -ExpectedMessage 'Verification catalog validation passed with 1 open gap(s).' -ExpectedAdditionalMessage 'GAP [Fixture.Verification.SampleTests.Ordinary_IsCataloged] Independent oracle is pending.' -Actual $actual
    }

    Invoke-FixtureCase -Name 'strict-open-gap' -Body {
        param($caseRoot)
        $fixture = Initialize-Fixture -Root $caseRoot
        $entry = New-CatalogEntry -Method 'Ordinary_IsCataloged'
        $entry.status = 'open'
        $entry.gap = 'Independent oracle is pending.'
        Write-Catalog -Path $fixture.CatalogPath -Entries @($entry)
        $actual = Invoke-CatalogValidator -Catalog $fixture.CatalogPath -SourceRoot $fixture.SourceRoot -RequireComplete
        Assert-ValidatorResult -Name 'strict mode rejects an open gap' -ExpectedExitCode 1 -ExpectedMessage 'ERROR [OPEN_GAP]' -Actual $actual
    }

    Invoke-FixtureCase -Name 'missing-source' -Body {
        param($caseRoot)
        $fixture = Initialize-Fixture -Root $caseRoot
        $entry = New-CatalogEntry -Method 'Ordinary_IsCataloged'
        $entry.source = 'src/RMC.BestFit.Verification/MissingTests.cs'
        Write-Catalog -Path $fixture.CatalogPath -Entries @($entry)
        $actual = Invoke-CatalogValidator -Catalog $fixture.CatalogPath -SourceRoot $fixture.SourceRoot
        Assert-ValidatorResult -Name 'missing source entry' -ExpectedExitCode 1 -ExpectedMessage 'ERROR [SOURCE_NOT_FOUND]' -Actual $actual
    }

    Invoke-FixtureCase -Name 'duplicate-entry' -Body {
        param($caseRoot)
        $fixture = Initialize-Fixture -Root $caseRoot
        $entry = New-CatalogEntry -Method 'Ordinary_IsCataloged'
        Write-Catalog -Path $fixture.CatalogPath -Entries @($entry, $entry)
        $actual = Invoke-CatalogValidator -Catalog $fixture.CatalogPath -SourceRoot $fixture.SourceRoot
        Assert-ValidatorResult -Name 'duplicate entry' -ExpectedExitCode 1 -ExpectedMessage 'ERROR [DUPLICATE_ENTRY]' -Actual $actual
    }

    Invoke-FixtureCase -Name 'unknown-evidence' -Body {
        param($caseRoot)
        $fixture = Initialize-Fixture -Root $caseRoot
        $entry = New-CatalogEntry -Method 'Ordinary_IsCataloged'
        $entry.primaryEvidenceKind = 'same-production-path'
        Write-Catalog -Path $fixture.CatalogPath -Entries @($entry)
        $actual = Invoke-CatalogValidator -Catalog $fixture.CatalogPath -SourceRoot $fixture.SourceRoot
        Assert-ValidatorResult -Name 'unknown evidence kind' -ExpectedExitCode 1 -ExpectedMessage 'ERROR [UNKNOWN_EVIDENCE_KIND]' -Actual $actual
    }

    Invoke-FixtureCase -Name 'evidence-tags-shape' -Body {
        param($caseRoot)
        $fixture = Initialize-Fixture -Root $caseRoot
        $entry = New-CatalogEntry -Method 'Ordinary_IsCataloged'
        $entry.evidenceTags = 'analytical'
        Write-Catalog -Path $fixture.CatalogPath -Entries @($entry)
        $actual = Invoke-CatalogValidator -Catalog $fixture.CatalogPath -SourceRoot $fixture.SourceRoot
        Assert-ValidatorResult -Name 'evidence tags must be a JSON array' -ExpectedExitCode 1 -ExpectedMessage 'ERROR [INVALID_FIELD_TYPE]' -Actual $actual
    }

    Invoke-FixtureCase -Name 'unknown-root-field' -Body {
        param($caseRoot)
        $fixture = Initialize-Fixture -Root $caseRoot
        $entry = New-CatalogEntry -Method 'Ordinary_IsCataloged'
        $catalog = [ordered]@{ schemaVersion = 1; entries = @($entry); unexpected = 'value' }
        Write-CatalogDocument -Path $fixture.CatalogPath -Catalog $catalog
        $actual = Invoke-CatalogValidator -Catalog $fixture.CatalogPath -SourceRoot $fixture.SourceRoot
        Assert-ValidatorResult -Name 'unknown root field is rejected' -ExpectedExitCode 1 -ExpectedMessage 'ERROR [UNKNOWN_FIELD]' -Actual $actual
    }

    Invoke-FixtureCase -Name 'entries-shape' -Body {
        param($caseRoot)
        $fixture = Initialize-Fixture -Root $caseRoot
        $entry = New-CatalogEntry -Method 'Ordinary_IsCataloged'
        $catalog = [ordered]@{ schemaVersion = 1; entries = $entry }
        Write-CatalogDocument -Path $fixture.CatalogPath -Catalog $catalog
        $actual = Invoke-CatalogValidator -Catalog $fixture.CatalogPath -SourceRoot $fixture.SourceRoot
        Assert-ValidatorResult -Name 'entries must be a JSON array' -ExpectedExitCode 1 -ExpectedMessage 'ERROR [INVALID_FIELD_TYPE]' -Actual $actual
    }

    Invoke-FixtureCase -Name 'schema-version-type' -Body {
        param($caseRoot)
        $fixture = Initialize-Fixture -Root $caseRoot
        $entry = New-CatalogEntry -Method 'Ordinary_IsCataloged'
        $catalog = [ordered]@{ schemaVersion = $true; entries = @($entry) }
        Write-CatalogDocument -Path $fixture.CatalogPath -Catalog $catalog
        $actual = Invoke-CatalogValidator -Catalog $fixture.CatalogPath -SourceRoot $fixture.SourceRoot
        Assert-ValidatorResult -Name 'schema version uses strict JSON type equality' -ExpectedExitCode 1 -ExpectedMessage 'ERROR [INVALID_FIELD_VALUE]' -Actual $actual
    }

    Invoke-FixtureCase -Name 'method-overrides-null' -Body {
        param($caseRoot)
        $fixture = Initialize-Fixture -Root $caseRoot
        $entry = New-CatalogEntry -Method 'Ordinary_IsCataloged'
        $entry.methodOverrides = $null
        Write-Catalog -Path $fixture.CatalogPath -Entries @($entry)
        $actual = Invoke-CatalogValidator -Catalog $fixture.CatalogPath -SourceRoot $fixture.SourceRoot
        Assert-ValidatorResult -Name 'methodOverrides cannot be null' -ExpectedExitCode 1 -ExpectedMessage 'ERROR [INVALID_FIELD_TYPE]' -Actual $actual
    }

    Invoke-FixtureCase -Name 'override-sample-size-type' -Body {
        param($caseRoot)
        $fixture = Initialize-Fixture -Root $caseRoot -IncludeDataMethod
        $ordinary = New-CatalogEntry -Method 'Ordinary_IsCataloged'
        $data = New-CatalogEntry -Method 'Data_IsCataloged' -MethodOverrides @(
            [ordered]@{ name = 'row-one'; sampleSize = '1000' },
            [ordered]@{ name = 'row-two' }
        )
        $data.primaryEvidenceKind = 'recovery'
        $data.evidenceTags = @('recovery')
        $data.executesEstimator = $true
        $data.sampleUnit = 'scalar observations'
        $data.sampleSize = 1000
        $data.seed = 12345
        Write-Catalog -Path $fixture.CatalogPath -Entries @($ordinary, $data)
        $actual = Invoke-CatalogValidator -Catalog $fixture.CatalogPath -SourceRoot $fixture.SourceRoot
        Assert-ValidatorResult -Name 'method override sampleSize must be an integer' -ExpectedExitCode 1 -ExpectedMessage 'ERROR [INVALID_FIELD_TYPE]' -Actual $actual
    }

    Invoke-FixtureCase -Name 'recovery-sample-size' -Body {
        param($caseRoot)
        $fixture = Initialize-Fixture -Root $caseRoot
        $entry = New-CatalogEntry -Method 'Ordinary_IsCataloged'
        $entry.primaryEvidenceKind = 'recovery'
        $entry.evidenceTags = @('recovery')
        $entry.executesEstimator = $true
        $entry.sampleUnit = 'scalar observations'
        $entry.sampleSize = 999
        $entry.seed = 12345
        Write-Catalog -Path $fixture.CatalogPath -Entries @($entry)
        $actual = Invoke-CatalogValidator -Catalog $fixture.CatalogPath -SourceRoot $fixture.SourceRoot
        Assert-ValidatorResult -Name 'recovery sample size other than 1000' -ExpectedExitCode 1 -ExpectedMessage 'ERROR [RECOVERY_SAMPLE_SIZE]' -Actual $actual
    }

    Invoke-FixtureCase -Name 'open-recovery-sample-size' -Body {
        param($caseRoot)
        $fixture = Initialize-Fixture -Root $caseRoot
        $entry = New-CatalogEntry -Method 'Ordinary_IsCataloged'
        $entry.primaryEvidenceKind = 'recovery'
        $entry.evidenceTags = @('recovery')
        $entry.executesEstimator = $true
        $entry.sampleUnit = 'scalar observations'
        $entry.sampleSize = 999
        $entry.seed = 12345
        $entry.status = 'open'
        $entry.gap = 'Planned recovery normalization will use exactly 1000 observations.'
        Write-Catalog -Path $fixture.CatalogPath -Entries @($entry)

        $default = Invoke-CatalogValidator -Catalog $fixture.CatalogPath -SourceRoot $fixture.SourceRoot
        Assert-ValidatorResult -Name 'open recovery sample-size gap in default mode' -ExpectedExitCode 0 -ExpectedMessage 'Verification catalog validation passed with 1 open gap(s).' -Actual $default

        $strict = Invoke-CatalogValidator -Catalog $fixture.CatalogPath -SourceRoot $fixture.SourceRoot -RequireComplete
        Assert-ValidatorResult -Name 'open recovery sample-size gap in strict mode' -ExpectedExitCode 1 -ExpectedMessage 'ERROR [OPEN_GAP]' -Actual $strict
    }

    Invoke-FixtureCase -Name 'missing-report-anchor' -Body {
        param($caseRoot)
        $fixture = Initialize-Fixture -Root $caseRoot
        $entry = New-CatalogEntry -Method 'Ordinary_IsCataloged'
        $entry.reportAnchor = 'docs/verification/sample-report.md#missing-anchor'
        Write-Catalog -Path $fixture.CatalogPath -Entries @($entry)
        $actual = Invoke-CatalogValidator -Catalog $fixture.CatalogPath -SourceRoot $fixture.SourceRoot
        Assert-ValidatorResult -Name 'missing report anchor' -ExpectedExitCode 1 -ExpectedMessage 'ERROR [REPORT_ANCHOR_NOT_FOUND]' -Actual $actual
    }

    Invoke-FixtureCase -Name 'unlisted-ordinary' -Body {
        param($caseRoot)
        $fixture = Initialize-Fixture -Root $caseRoot -IncludeSecondOrdinaryMethod
        $entry = New-CatalogEntry -Method 'Ordinary_IsCataloged'
        Write-Catalog -Path $fixture.CatalogPath -Entries @($entry)
        $actual = Invoke-CatalogValidator -Catalog $fixture.CatalogPath -SourceRoot $fixture.SourceRoot
        Assert-ValidatorResult -Name 'unlisted ordinary method' -ExpectedExitCode 1 -ExpectedMessage 'ERROR [UNLISTED_METHOD]' -Actual $actual
    }

    Invoke-FixtureCase -Name 'unlisted-data-method' -Body {
        param($caseRoot)
        $fixture = Initialize-Fixture -Root $caseRoot -IncludeDataMethod
        $entry = New-CatalogEntry -Method 'Ordinary_IsCataloged'
        Write-Catalog -Path $fixture.CatalogPath -Entries @($entry)
        $actual = Invoke-CatalogValidator -Catalog $fixture.CatalogPath -SourceRoot $fixture.SourceRoot
        Assert-ValidatorResult -Name 'unlisted data-driven method' -ExpectedExitCode 1 -ExpectedMessage 'ERROR [UNLISTED_DATA_METHOD]' -Actual $actual
    }

    Invoke-FixtureCase -Name 'datarow-count' -Body {
        param($caseRoot)
        $fixture = Initialize-Fixture -Root $caseRoot -IncludeDataMethod
        $ordinary = New-CatalogEntry -Method 'Ordinary_IsCataloged'
        $data = New-CatalogEntry -Method 'Data_IsCataloged' -MethodOverrides @(
            [ordered]@{ name = 'row-one' }
        )
        Write-Catalog -Path $fixture.CatalogPath -Entries @($ordinary, $data)
        $actual = Invoke-CatalogValidator -Catalog $fixture.CatalogPath -SourceRoot $fixture.SourceRoot
        Assert-ValidatorResult -Name 'mismatched DataRow count' -ExpectedExitCode 1 -ExpectedMessage 'ERROR [DATAROW_COUNT_MISMATCH]' -Actual $actual
    }

    Write-Host ''
    Write-Host 'Validator fixture tests passed (22/22).'
}
finally {
    $resolvedTestRoot = [System.IO.Path]::GetFullPath($testRoot)
    $resolvedTempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
    if ($resolvedTestRoot.StartsWith($resolvedTempRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $resolvedTestRoot -PathType Container)) {
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
    }
}
