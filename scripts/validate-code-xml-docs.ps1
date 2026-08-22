<#
.SYNOPSIS
Validates the XML documentation and namespace conventions of the RMC.BestFit solution.

.DESCRIPTION
Runs the documentation gate described in CLAUDE.md:

1. Builds every source project with EnforceXmlDocumentation=true (skipped with -SkipBuild);
   any build error or CS15xx XML documentation warning fails the gate.
2. Scans the C# sources for namespace violations: a file that declares the exact namespace
   RMC.BestFit, any RMC.BestFit.Model (singular) namespace or using, or a bare using RMC.BestFit;.
3. Scans the C# sources for private, internal, or protected classes and methods whose
   declaration is not preceded by an XML documentation comment (the compile-checked
   documentation snippets under src\RMC.BestFit.Tests\Documentation\Examples are excluded
   because they are extracted verbatim into the technical reference).

The namespace scan is a hard failure. The private documentation scan reports every
undocumented declaration and fails the gate unless -ReportOnly is supplied.

.PARAMETER Configuration
Build configuration (Debug or Release). Default: Debug. RMC.BestFit.Verification is built only in Debug.

.PARAMETER SkipBuild
Skip the strict builds and run only the namespace and private-documentation scans.

.PARAMETER ReportOnly
Report undocumented private declarations without failing the gate.

.EXAMPLE
.\scripts\validate-code-xml-docs.ps1 -Configuration Debug

.EXAMPLE
.\scripts\validate-code-xml-docs.ps1 -SkipBuild
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$SkipBuild,
    [switch]$ReportOnly
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $repositoryRoot 'src'
$failures = New-Object System.Collections.Generic.List[string]

$projects = @(
    'RMC.BestFit\RMC.BestFit.csproj',
    'RMC.BestFit.UI\RMC.BestFit.UI.csproj',
    'RMC.BestFit.App\RMC.BestFit.App.csproj',
    'RMC.BestFit.Api\RMC.BestFit.Api.csproj',
    'RMC.BestFit.Tests\RMC.BestFit.Tests.csproj',
    'RMC.BestFit.UI.Tests\RMC.BestFit.UI.Tests.csproj',
    'RMC.BestFit.App.Tests\RMC.BestFit.App.Tests.csproj',
    'RMC.BestFit.Api.Tests\RMC.BestFit.Api.Tests.csproj'
)
if ($Configuration -eq 'Debug') {
    $projects += 'RMC.BestFit.Verification\RMC.BestFit.Verification.csproj'
}

if (-not $SkipBuild) {
    foreach ($relative in $projects) {
        $projectPath = Join-Path $sourceRoot $relative
        if (-not (Test-Path -LiteralPath $projectPath)) {
            $failures.Add("Project not found: $relative")
            continue
        }
        Write-Host "Building $relative ($Configuration) with EnforceXmlDocumentation=true"
        $output = & dotnet build $projectPath -c $Configuration -p:EnforceXmlDocumentation=true --nologo -v q 2>&1
        $exitCode = $LASTEXITCODE
        $xmlWarnings = @($output | Where-Object { $_ -match 'warning CS15[0-9]{2}' })
        $errors = @($output | Where-Object { $_ -match ': error ' })
        if ($exitCode -ne 0 -or $errors.Count -gt 0) {
            $failures.Add("Build failed: $relative")
            $errors | Select-Object -First 10 | ForEach-Object { Write-Host "  $_" }
        }
        if ($xmlWarnings.Count -gt 0) {
            $failures.Add("XML documentation warnings: $relative ($($xmlWarnings.Count))")
            $xmlWarnings | Select-Object -First 10 | ForEach-Object { Write-Host "  $_" }
        }
    }
}

$sourceFiles = Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter *.cs -File |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|TestResults)\\' -and $_.Name -notmatch '\.(g|Designer|AssemblyAttributes|AssemblyInfo|GlobalUsings\.g)\.cs$' }

# Compile-checked documentation snippets (src\RMC.BestFit.Tests\Documentation\Examples) are extracted
# verbatim into the technical reference between '#region doc:' markers, so their private helpers are
# intentionally undocumented and are excluded from the private-documentation scan only.
$privateScanFiles = @($sourceFiles | Where-Object { $_.FullName -notmatch '\\Documentation\\Examples\\' })

# --- Namespace scan -------------------------------------------------------------------------
$namespaceViolations = New-Object System.Collections.Generic.List[string]
foreach ($file in $sourceFiles) {
    $lineNumber = 0
    foreach ($line in [System.IO.File]::ReadLines($file.FullName)) {
        $lineNumber++
        if ($line -match '^\s*namespace\s+RMC\.BestFit\s*[;{]?\s*$') {
            $namespaceViolations.Add("$($file.FullName):$lineNumber exact namespace RMC.BestFit")
        }
        if ($line -match '^\s*namespace\s+RMC\.BestFit\.Model(\s|[;{.]|$)' -or $line -match '^\s*using\s+RMC\.BestFit\.Model\s*;') {
            $namespaceViolations.Add("$($file.FullName):$lineNumber deleted namespace RMC.BestFit.Model")
        }
        if ($line -match '^\s*using\s+RMC\.BestFit\s*;') {
            $namespaceViolations.Add("$($file.FullName):$lineNumber bare using RMC.BestFit;")
        }
    }
}
if ($namespaceViolations.Count -gt 0) {
    $failures.Add("Namespace violations: $($namespaceViolations.Count)")
    $namespaceViolations | ForEach-Object { Write-Host "  $_" }
}

# --- Private / internal XML documentation scan --------------------------------------------
$declarationPattern = '^\s*(private|internal|protected|protected\s+internal|private\s+protected)\s+(?:static\s+|readonly\s+|sealed\s+|partial\s+|abstract\s+|virtual\s+|override\s+|async\s+|unsafe\s+|new\s+|extern\s+)*(?:class|struct|record|enum|interface)\s+\w+|^\s*(private|internal|protected|protected\s+internal|private\s+protected)\s+(?:static\s+|sealed\s+|partial\s+|abstract\s+|virtual\s+|override\s+|async\s+|unsafe\s+|new\s+|extern\s+)*[A-Za-z_][\w<>\[\],.?\s]*\s+\w+\s*\('
$undocumented = New-Object System.Collections.Generic.List[string]
foreach ($file in $privateScanFiles) {
    $lines = [System.IO.File]::ReadAllLines($file.FullName)
    for ($i = 0; $i -lt $lines.Length; $i++) {
        $line = $lines[$i]
        if ($line -notmatch $declarationPattern) { continue }
        # Skip fields, property accessors, lambdas, delegates-as-fields, and expression-bodied members on one line.
        if ($line -match '=>' -or $line -match '=\s*new\b' -or $line -match ';\s*$' -or $line -match '^\s*(private|internal|protected)\s+(event|delegate)\b') { continue }
        $j = $i - 1
        while ($j -ge 0 -and ($lines[$j] -match '^\s*\[' -or $lines[$j].Trim() -eq '' -or $lines[$j] -match '^\s*#')) { $j-- }
        if ($j -lt 0 -or $lines[$j] -notmatch '^\s*///') {
            $undocumented.Add("$($file.FullName):$($i + 1) $($line.Trim())")
        }
    }
}
if ($undocumented.Count -gt 0) {
    Write-Host "Undocumented private/internal/protected declarations: $($undocumented.Count)"
    $undocumented | Select-Object -First 50 | ForEach-Object { Write-Host "  $_" }
    if ($undocumented.Count -gt 50) { Write-Host "  ... $($undocumented.Count - 50) more" }
    if (-not $ReportOnly) {
        $failures.Add("Undocumented private/internal/protected declarations: $($undocumented.Count)")
    }
}

if ($failures.Count -gt 0) {
    Write-Host ''
    Write-Host 'XML documentation gate FAILED:'
    $failures | ForEach-Object { Write-Host "  - $_" }
    exit 1
}

Write-Host ''
Write-Host "XML documentation gate passed ($($sourceFiles.Count) source files scanned; build $(if ($SkipBuild) { 'skipped' } else { 'strict ' + $Configuration }))."
exit 0
