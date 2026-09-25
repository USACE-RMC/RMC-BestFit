<#
.SYNOPSIS
Validates an RMC-BestFit desktop GitHub release without installing it.

.DESCRIPTION
Queries a GitHub release by tag, requires one matching desktop zip, validates
the release-body SHA256 token against GitHub's asset digest, downloads the zip
to an owned temporary directory, verifies its bytes, and checks the required
application/updater payload. The archive is opened for inspection only; it is
never extracted or executed.

.PARAMETER Tag
The GitHub release tag, for example v2.0.0.

.PARAMETER Repository
The GitHub repository in owner/name form.

.PARAMETER AssetPattern
The PowerShell wildcard pattern used to select the desktop release asset.

.EXAMPLE
.\scripts\Test-DesktopRelease.ps1 -Tag v2.0.0
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $Tag,

    [Parameter()]
    [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')]
    [string] $Repository = 'USACE-RMC/RMC-BestFit',

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $AssetPattern = 'RMC-BestFit.*.zip'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$requiredPayload = @(
    'RMC-BestFit.exe',
    'SoftwareUpdate.Updater.exe',
    'SoftwareUpdate.Updater.dll',
    'SoftwareUpdate.Updater.deps.json',
    'SoftwareUpdate.Updater.runtimeconfig.json'
)

$encodedTag = [Uri]::EscapeDataString($Tag)
$releaseUri = "https://api.github.com/repos/$Repository/releases/tags/$encodedTag"
$requestHeaders = @{
    Accept = 'application/vnd.github+json'
    'User-Agent' = 'RMC-BestFit-Release-Validator'
    'X-GitHub-Api-Version' = '2022-11-28'
}

Write-Verbose "Reading release metadata from $releaseUri"
$release = Invoke-RestMethod -Uri $releaseUri -Headers $requestHeaders -Method Get

if ($release.tag_name -cne $Tag) {
    throw "GitHub returned tag '$($release.tag_name)' when '$Tag' was requested."
}

if ([bool] $release.draft) {
    throw "Release '$Tag' is still a draft."
}

$matchingAssets = @($release.assets | Where-Object {
    -not [string]::IsNullOrWhiteSpace([string] $_.name) -and
    ([string] $_.name) -like $AssetPattern
})

if ($matchingAssets.Count -ne 1) {
    $assetNames = @($release.assets | ForEach-Object { [string] $_.name }) -join ', '
    throw "Expected exactly one asset matching '$AssetPattern' but found $($matchingAssets.Count). Available assets: $assetNames"
}

$asset = $matchingAssets[0]
if ([long] $asset.size -le 0) {
    throw "Release asset '$($asset.name)' has an invalid size: $($asset.size)."
}

$downloadUri = [Uri] ([string] $asset.browser_download_url)
if ($downloadUri.Scheme -cne [Uri]::UriSchemeHttps) {
    throw "Release asset download URL must use HTTPS: $downloadUri"
}

$releaseBody = [string] $release.body
$checksumPattern = '(?im)\bSHA256\s*[:=]\s*([0-9a-f]{64})\b'
$checksumMatches = [regex]::Matches($releaseBody, $checksumPattern)
if ($checksumMatches.Count -ne 1) {
    throw "Release '$Tag' must contain exactly one valid SHA256 token; found $($checksumMatches.Count)."
}

$releaseChecksum = $checksumMatches[0].Groups[1].Value.ToLowerInvariant()
$digestMatch = [regex]::Match(
    [string] $asset.digest,
    '^sha256:([0-9a-f]{64})$',
    [Text.RegularExpressions.RegexOptions]::IgnoreCase)

if (-not $digestMatch.Success) {
    throw "Release asset '$($asset.name)' does not provide a valid GitHub sha256 digest."
}

$assetChecksum = $digestMatch.Groups[1].Value.ToLowerInvariant()
if ($releaseChecksum -cne $assetChecksum) {
    throw "Release-body checksum '$releaseChecksum' does not match GitHub asset checksum '$assetChecksum'."
}

$validationRoot = Join-Path ([IO.Path]::GetTempPath()) 'RMC-BestFit-ReleaseValidation'
$ownedDirectory = Join-Path $validationRoot ([Guid]::NewGuid().ToString('N'))
$downloadPath = Join-Path $ownedDirectory ([IO.Path]::GetFileName([string] $asset.name))

[void] (New-Item -ItemType Directory -Path $ownedDirectory -Force)

try {
    Write-Verbose "Downloading $($asset.name) to an owned temporary directory."
    Invoke-WebRequest -Uri $downloadUri.AbsoluteUri -Headers $requestHeaders -OutFile $downloadPath -UseBasicParsing

    $downloadedFile = Get-Item -LiteralPath $downloadPath
    if ($downloadedFile.Length -ne [long] $asset.size) {
        throw "Downloaded size '$($downloadedFile.Length)' does not match GitHub asset size '$($asset.size)'."
    }

    $downloadedChecksum = (Get-FileHash -LiteralPath $downloadPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($downloadedChecksum -cne $releaseChecksum) {
        throw "Downloaded checksum '$downloadedChecksum' does not match expected checksum '$releaseChecksum'."
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($downloadPath)
    try {
        $entryNames = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
        foreach ($requiredFile in $requiredPayload) {
            $occurrences = @($entryNames | Where-Object { $_ -ceq $requiredFile }).Count
            if ($occurrences -ne 1) {
                throw "Required payload '$requiredFile' must appear exactly once; found $occurrences."
            }
        }
        $entryCount = $archive.Entries.Count
    }
    finally {
        $archive.Dispose()
    }

    [pscustomobject]@{
        Repository = $Repository
        Tag = $Tag
        ReleaseName = [string] $release.name
        AssetName = [string] $asset.name
        AssetSize = [long] $asset.size
        SHA256 = $downloadedChecksum
        ZipEntryCount = $entryCount
        RequiredPayloadCount = $requiredPayload.Count
        Status = 'Valid'
    }
}
finally {
    $resolvedRoot = [IO.Path]::GetFullPath($validationRoot).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
    $resolvedOwnedDirectory = [IO.Path]::GetFullPath($ownedDirectory).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
    $requiredPrefix = $resolvedRoot + [IO.Path]::DirectorySeparatorChar

    if ($resolvedOwnedDirectory.StartsWith($requiredPrefix, [StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $resolvedOwnedDirectory)) {
        Remove-Item -LiteralPath $resolvedOwnedDirectory -Recurse -Force
    }
}
