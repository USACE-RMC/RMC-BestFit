[CmdletBinding()]
param(
    [string]$NodePath = "",
    [string]$NodeModulesPath = "",
    [string]$PythonPath = "",
    [string]$LatexPath = "",
    [string]$DvisvgmPath = "",
    [string]$BrowserPath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$temporaryDirectory = Join-Path $repositoryRoot "tmp\pdfs"
$outputDirectory = Join-Path $repositoryRoot "output\pdf"
$htmlPath = Join-Path $temporaryDirectory "rmc-bestfit-technical-reference.html"
$equationTexPath = Join-Path $temporaryDirectory "technical-reference-equations.tex"
$equationDviPath = Join-Path $temporaryDirectory "technical-reference-equations.dvi"
$equationDirectory = Join-Path $temporaryDirectory "equations"
$rawPdfPath = Join-Path $temporaryDirectory "rmc-bestfit-technical-reference.raw.pdf"
$finalPdfPath = Join-Path $outputDirectory "rmc-bestfit-technical-reference.pdf"
$browserProfilePath = Join-Path $temporaryDirectory "chromium-profile"

function Select-ExistingPath {
    param(
        [string[]]$Candidates,
        [string]$Description
    )

    foreach ($candidate in $Candidates) {
        if (![string]::IsNullOrWhiteSpace($candidate) -and (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw "Unable to locate $Description. Supply its path explicitly."
}

$nodeFromPath = Get-Command node -ErrorAction SilentlyContinue
$pythonFromPath = Get-Command python -ErrorAction SilentlyContinue
$latexFromPath = Get-Command latex -ErrorAction SilentlyContinue
$dvisvgmFromPath = Get-Command dvisvgm -ErrorAction SilentlyContinue
$userProfilePath = [Environment]::GetFolderPath("UserProfile")
$programFilesX86 = [Environment]::GetEnvironmentVariable("ProgramFiles(x86)")
$codexDependencyRoot = Join-Path $userProfilePath ".cache\codex-runtimes\codex-primary-runtime\dependencies"
$miktexDirectory = Join-Path $userProfilePath "AppData\Local\Programs\MiKTeX\miktex\bin\x64"

$nodeExecutable = Select-ExistingPath -Candidates @(
    $NodePath,
    (Join-Path $codexDependencyRoot "node\bin\node.exe"),
    $(if ($null -ne $nodeFromPath) { $nodeFromPath.Source })
) -Description "Node.js"

$pythonExecutable = Select-ExistingPath -Candidates @(
    $PythonPath,
    (Join-Path $codexDependencyRoot "python\python.exe"),
    $(if ($null -ne $pythonFromPath) { $pythonFromPath.Source })
) -Description "Python"

$latexExecutable = Select-ExistingPath -Candidates @(
    $LatexPath,
    (Join-Path $miktexDirectory "latex.exe"),
    $(if ($null -ne $latexFromPath) { $latexFromPath.Source })
) -Description "LaTeX"

$dvisvgmExecutable = Select-ExistingPath -Candidates @(
    $DvisvgmPath,
    (Join-Path $miktexDirectory "dvisvgm.exe"),
    $(if ($null -ne $dvisvgmFromPath) { $dvisvgmFromPath.Source })
) -Description "dvisvgm"

$browserExecutable = Select-ExistingPath -Candidates @(
    $BrowserPath,
    (Join-Path $env:ProgramFiles "Google\Chrome\Application\chrome.exe"),
    (Join-Path $programFilesX86 "Microsoft\Edge\Application\msedge.exe")
) -Description "Chrome or Edge"

if ([string]::IsNullOrWhiteSpace($NodeModulesPath)) {
    $nodeRoot = Split-Path -Parent (Split-Path -Parent $nodeExecutable)
    $NodeModulesPath = Join-Path $nodeRoot "node_modules"
}

if (!(Test-Path -LiteralPath $NodeModulesPath -PathType Container)) {
    throw "Node dependency directory does not exist: $NodeModulesPath"
}

New-Item -ItemType Directory -Path $temporaryDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $equationDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $browserProfilePath -Force | Out-Null

$env:BESTFIT_NODE_MODULES = (Resolve-Path -LiteralPath $NodeModulesPath).Path

& $pythonExecutable (Join-Path $PSScriptRoot "generate-technical-reference-bibliography.py") "--check"
if ($LASTEXITCODE -ne 0) {
    throw "The consolidated bibliography is stale or invalid."
}

& $nodeExecutable (Join-Path $PSScriptRoot "build-technical-reference-book.mjs") $htmlPath $equationTexPath
if ($LASTEXITCODE -ne 0) {
    throw "The canonical HTML and equation-source build failed."
}

$latexArguments = @(
    "-interaction=nonstopmode",
    "-halt-on-error",
    "-file-line-error",
    "-output-directory=$temporaryDirectory",
    $equationTexPath
)
& $latexExecutable $latexArguments
if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $equationDviPath -PathType Leaf)) {
    throw "LaTeX did not produce the equation DVI."
}

$dvisvgmArguments = @(
    "--no-fonts",
    "--verbosity=2",
    "--exact-bbox",
    "--page=1-",
    "--output=$equationDirectory\eq-%p.svg",
    $equationDviPath
)
& $dvisvgmExecutable $dvisvgmArguments
if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath (Join-Path $equationDirectory "eq-0001.svg") -PathType Leaf)) {
    throw "dvisvgm did not produce the offline equation assets."
}

$htmlUri = ([Uri](Resolve-Path -LiteralPath $htmlPath).Path).AbsoluteUri
$browserArguments = @(
    "--headless=new",
    "--disable-background-networking",
    "--disable-component-update",
    "--disable-default-apps",
    "--disable-extensions",
    "--disable-gpu",
    "--disable-sync",
    "--metrics-recording-only",
    "--no-default-browser-check",
    "--no-first-run",
    "--allow-file-access-from-files",
    "--run-all-compositor-stages-before-draw",
    "--no-pdf-header-footer",
    "--user-data-dir=$browserProfilePath",
    "--print-to-pdf=$rawPdfPath",
    $htmlUri
)
if (Test-Path -LiteralPath $rawPdfPath -PathType Leaf) {
    Remove-Item -LiteralPath $rawPdfPath -Force
}

& $browserExecutable $browserArguments
if ($LASTEXITCODE -ne 0) {
    throw "Chromium failed while printing the technical-reference PDF."
}

$pdfDeadline = [DateTime]::UtcNow.AddSeconds(15)
while (!(Test-Path -LiteralPath $rawPdfPath -PathType Leaf) -and [DateTime]::UtcNow -lt $pdfDeadline) {
    Start-Sleep -Milliseconds 100
}

if (!(Test-Path -LiteralPath $rawPdfPath -PathType Leaf)) {
    throw "Chromium did not produce the raw technical-reference PDF within 15 seconds."
}
& $pythonExecutable (Join-Path $PSScriptRoot "finalize-technical-reference-pdf.py") $rawPdfPath $finalPdfPath
if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $finalPdfPath -PathType Leaf)) {
    throw "The final technical-reference PDF was not produced."
}

Write-Host "Technical-reference release PDF: $finalPdfPath"
exit 0
