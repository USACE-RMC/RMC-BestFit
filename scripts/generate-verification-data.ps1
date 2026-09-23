[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9-]+$')]
    [string]$Family,
    [string]$PythonPath = "",
    [string]$RscriptPath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ($Family -eq "all") {
    throw "Broad regeneration is prohibited. Specify one benchmark family."
}

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$pythonRoot = Join-Path $repositoryRoot "verification\python"
$rRoot = Join-Path $repositoryRoot "verification\r"
$pythonFamily = Join-Path $pythonRoot $Family
$rFamily = Join-Path $rRoot $Family
$scripts = New-Object System.Collections.Generic.List[object]

if (Test-Path -LiteralPath $pythonFamily -PathType Container) {
    foreach ($script in Get-ChildItem -LiteralPath $pythonFamily -Filter *.py -File | Sort-Object Name) {
        [void]$scripts.Add([pscustomobject]@{ Runtime = "Python"; Path = $script.FullName })
    }
}

if (Test-Path -LiteralPath $rFamily -PathType Container) {
    foreach ($script in Get-ChildItem -LiteralPath $rFamily -Filter *.R -File | Sort-Object Name) {
        [void]$scripts.Add([pscustomobject]@{ Runtime = "R"; Path = $script.FullName })
    }
}

if ($scripts.Count -eq 0) {
    throw "No generators were found for family '$Family'."
}

if ([string]::IsNullOrWhiteSpace($PythonPath)) {
    $restoredPython = Join-Path $pythonRoot ".venv\Scripts\python.exe"
    if (Test-Path -LiteralPath $restoredPython -PathType Leaf) {
        $PythonPath = $restoredPython
    }
    else {
        $pythonCommand = Get-Command python -ErrorAction SilentlyContinue
        if ($null -ne $pythonCommand) {
            $PythonPath = $pythonCommand.Source
        }
    }
}

if ([string]::IsNullOrWhiteSpace($RscriptPath)) {
    $defaultRscript = "C:\Program Files\R\R-4.4.3\bin\Rscript.exe"
    if (Test-Path -LiteralPath $defaultRscript -PathType Leaf) {
        $RscriptPath = $defaultRscript
    }
}

foreach ($script in $scripts) {
    if ($script.Runtime -eq "Python") {
        if ([string]::IsNullOrWhiteSpace($PythonPath) -or !(Test-Path -LiteralPath $PythonPath -PathType Leaf)) {
            throw "Python was not found. Supply -PythonPath."
        }

        & $PythonPath $script.Path
    }
    else {
        if ([string]::IsNullOrWhiteSpace($RscriptPath) -or !(Test-Path -LiteralPath $RscriptPath -PathType Leaf)) {
            throw "Rscript was not found. Supply -RscriptPath."
        }

        $normalizedRRoot = (Resolve-Path -LiteralPath $rRoot).Path.Replace('\', '/')
        $normalizedScript = $script.Path.Replace('\', '/')
        $rExpression = "renv::load(project = '$normalizedRRoot'); source('$normalizedScript', echo = TRUE)"
        & $RscriptPath -e $rExpression
    }

    if ($LASTEXITCODE -ne 0) {
        throw "$($script.Runtime) generator failed: $($script.Path)"
    }
}

Write-Host "Regenerated '$Family'. Review verification/data diffs and update MANIFEST.md hashes before use."