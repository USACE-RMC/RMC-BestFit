[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Test
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ($Test -notmatch '^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*){2,}$') {
    throw "Test must be one exact fully qualified method name."
}

if ($Test.IndexOfAny([char[]]'*?&|=~') -ge 0) {
    throw "Wildcards, expressions, and partial filters are not allowed."
}

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$verificationRoot = Join-Path $repositoryRoot "src\RMC.BestFit.Verification"
$projectPath = Join-Path $verificationRoot "RMC.BestFit.Verification.csproj"
$segments = $Test.Split('.')
$methodName = $segments[-1]
$className = $segments[-2]
$namespaceName = [string]::Join('.', $segments[0..($segments.Length - 3)])
$sourceMatches = New-Object System.Collections.Generic.List[string]

foreach ($sourceFile in Get-ChildItem -LiteralPath $verificationRoot -Recurse -Filter *.cs -File) {
    $source = [System.IO.File]::ReadAllText($sourceFile.FullName)
    $namespaceMatch = [regex]::Match($source, 'namespace\s+([A-Za-z_][A-Za-z0-9_.]*)\s*[;{]')
    if (!$namespaceMatch.Success -or $namespaceMatch.Groups[1].Value -ne $namespaceName) {
        continue
    }

    $classPattern = '\bclass\s+' + [regex]::Escape($className) + '\b'
    $methodPattern = '\[TestMethod(?:Attribute)?\]\s+public\s+(?:async\s+)?[A-Za-z_][A-Za-z0-9_<>,.\[\]? ]+\s+' + [regex]::Escape($methodName) + '\s*\('
    if (![regex]::IsMatch($source, $classPattern)) {
        continue
    }

    foreach ($methodMatch in [regex]::Matches($source, $methodPattern)) {
        [void]$sourceMatches.Add("$($sourceFile.FullName):$($methodMatch.Index)")
    }
}

if ($sourceMatches.Count -ne 1) {
    throw "Exact source resolution found $($sourceMatches.Count) matches for '$Test'; expected exactly one."
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$safeName = $Test.Replace('.', '_')
$resultsDirectory = Join-Path $repositoryRoot "TestResults\VerificationFocused\$timestamp-$safeName"
New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null

Write-Host "Building Verification project only; no tests are being run by this step."
& dotnet build $projectPath -c Debug --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Verification project build failed."
}

Write-Host "Running exactly: $Test"
$testArguments = @(
    "test",
    $projectPath,
    "-c",
    "Debug",
    "--no-build",
    "--",
    "--filter",
    "FullyQualifiedName=$Test",
    "--report-trx",
    "--results-directory",
    $resultsDirectory
)
& dotnet @testArguments
$testExitCode = $LASTEXITCODE

$trxFiles = @(Get-ChildItem -LiteralPath $resultsDirectory -Recurse -Filter *.trx -File)
if ($trxFiles.Count -ne 1) {
    throw "Focused run produced $($trxFiles.Count) TRX files; expected exactly one."
}

[xml]$trx = Get-Content -LiteralPath $trxFiles[0].FullName -Raw
$results = @($trx.SelectNodes("//*[local-name()='UnitTestResult']"))
if ($results.Count -ne 1) {
    throw "Focused run executed $($results.Count) tests; expected exactly one."
}

$result = $results[0]
Write-Host "Focused verification outcome: $($result.outcome)"
Write-Host "TRX: $($trxFiles[0].FullName)"

if ($testExitCode -ne 0 -or $result.outcome -ne "Passed") {
    throw "Focused verification test did not pass."
}