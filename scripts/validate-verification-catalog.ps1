<#
.SYNOPSIS
Validates the RMC.BestFit Verification method catalog against source and report content.

.DESCRIPTION
Validates catalog structure using docs/verification/verification-catalog.schema.json,
discovers ordinary MSTest methods and named DataRow execution units beneath SourceRoot,
and reconciles every discovered method with exactly one catalog entry. Recovery claims
must use exactly 1,000 observational units. Every catalog entry must resolve to an
existing C# source and Markdown report anchor.

Default mode permits entries whose status is "open" and reports their gap text.
-RequireComplete turns every open entry into an error. Accepted limitations and
execution-excluded evidence remain explicit but do not fail strict mode.

The process-level command contract is:

- Exit code 0: validation passed. Each open-gap notice is
  "GAP [<method-or-row>] <gap>", and the final line is
  "Verification catalog validation passed with <n> open gap(s). Discovered <m>
  method declaration(s) and <u> execution unit(s)."
- Exit code 1: validation failed. Each diagnostic begins "ERROR [<code>]" and the
  final line is "Verification catalog validation FAILED with <n> error(s)."

The script does not build or execute RMC.BestFit.Verification.

.PARAMETER Catalog
Path to the method-level Verification catalog JSON file.

.PARAMETER SourceRoot
Path to the Verification C# source root to inventory.

.PARAMETER RequireComplete
Reject every catalog entry whose status is "open".

.EXAMPLE
.\scripts\validate-verification-catalog.ps1 `
    -Catalog .\docs\verification\verification-catalog.json `
    -SourceRoot .\src\RMC.BestFit.Verification

.EXAMPLE
.\scripts\validate-verification-catalog.ps1 `
    -Catalog .\docs\verification\verification-catalog.json `
    -SourceRoot .\src\RMC.BestFit.Verification `
    -RequireComplete
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Catalog,
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$SourceRoot,
    [switch]$RequireComplete
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$validationErrors = New-Object System.Collections.Generic.List[string]
$openGaps = New-Object System.Collections.Generic.List[string]

function Add-ValidationError {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Code,
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    [void]$script:validationErrors.Add("ERROR [$Code] $Message")
}

function Test-ObjectProperty {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Object,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    return $null -ne $Object.PSObject.Properties[$Name]
}

function Test-JsonValueType {
    param(
        [AllowNull()]
        [object]$Value,
        [Parameter(Mandatory = $true)]
        [string]$TypeName
    )

    switch ($TypeName) {
        'null' { return $null -eq $Value }
        'object' { return $Value -is [System.Management.Automation.PSCustomObject] }
        'array' { return $Value -is [System.Array] }
        'string' { return $Value -is [string] }
        'boolean' { return $Value -is [bool] }
        'integer' {
            return $Value -is [System.Byte] -or $Value -is [System.SByte] -or
                $Value -is [System.Int16] -or $Value -is [System.UInt16] -or
                $Value -is [System.Int32] -or $Value -is [System.UInt32] -or
                $Value -is [System.Int64] -or $Value -is [System.UInt64]
        }
        default { return $false }
    }
}

function Test-StrictJsonEquality {
    param(
        [AllowNull()]
        [object]$Left,
        [AllowNull()]
        [object]$Right
    )

    if ($null -eq $Left -or $null -eq $Right) {
        return $null -eq $Left -and $null -eq $Right
    }

    $leftIsInteger = Test-JsonValueType -Value $Left -TypeName 'integer'
    $rightIsInteger = Test-JsonValueType -Value $Right -TypeName 'integer'
    if ($leftIsInteger -or $rightIsInteger) {
        return $leftIsInteger -and $rightIsInteger -and ([decimal]$Left -eq [decimal]$Right)
    }
    if ($Left.GetType() -ne $Right.GetType()) {
        return $false
    }
    return [object]::Equals($Left, $Right)
}

function Get-ReferencedSchemaNode {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Reference,
        [Parameter(Mandatory = $true)]
        [object]$RootSchema
    )

    $referenceMatch = [regex]::Match($Reference, '^#/\$defs/(?<name>[A-Za-z_][A-Za-z0-9_]*)$')
    if (-not $referenceMatch.Success) {
        Add-ValidationError -Code 'SCHEMA_INVALID' -Message "Unsupported schema reference '$Reference'."
        return $null
    }

    $definitionName = $referenceMatch.Groups['name'].Value
    if (-not (Test-ObjectProperty -Object $RootSchema.'$defs' -Name $definitionName)) {
        Add-ValidationError -Code 'SCHEMA_INVALID' -Message "Schema reference '$Reference' does not resolve."
        return $null
    }
    return $RootSchema.'$defs'.$definitionName
}

function Test-JsonSchemaNode {
    param(
        [AllowNull()]
        [object]$Value,
        [Parameter(Mandatory = $true)]
        [object]$SchemaNode,
        [Parameter(Mandatory = $true)]
        [object]$RootSchema,
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (Test-ObjectProperty -Object $SchemaNode -Name '$ref') {
        $referencedNode = Get-ReferencedSchemaNode -Reference $SchemaNode.'$ref' -RootSchema $RootSchema
        if ($null -ne $referencedNode) {
            Test-JsonSchemaNode -Value $Value -SchemaNode $referencedNode -RootSchema $RootSchema -Path $Path
        }
        return
    }

    if (Test-ObjectProperty -Object $SchemaNode -Name 'type') {
        $allowedTypes = @($SchemaNode.type)
        $typeMatched = $false
        foreach ($allowedType in $allowedTypes) {
            if (Test-JsonValueType -Value $Value -TypeName ([string]$allowedType)) {
                $typeMatched = $true
                break
            }
        }
        if (-not $typeMatched) {
            Add-ValidationError -Code 'INVALID_FIELD_TYPE' -Message "$Path must have JSON type $($allowedTypes -join ' or ')."
            return
        }
    }

    if (Test-ObjectProperty -Object $SchemaNode -Name 'const') {
        if (-not (Test-StrictJsonEquality -Left $Value -Right $SchemaNode.const)) {
            Add-ValidationError -Code 'INVALID_FIELD_VALUE' -Message "$Path must equal '$($SchemaNode.const)' with the same JSON type."
            return
        }
    }

    if (Test-ObjectProperty -Object $SchemaNode -Name 'enum') {
        $enumMatched = $false
        foreach ($allowedValue in @($SchemaNode.enum)) {
            if (Test-StrictJsonEquality -Left $Value -Right $allowedValue) {
                $enumMatched = $true
                break
            }
        }
        if (-not $enumMatched) {
            $code = if ($Path -match '\.primaryEvidenceKind$' -or $Path -match '\.evidenceTags\[\d+\]$') {
                'UNKNOWN_EVIDENCE_KIND'
            }
            elseif ($Path -match '\.disposition$') {
                'UNKNOWN_DISPOSITION'
            }
            elseif ($Path -match '\.status$') {
                'UNKNOWN_STATUS'
            }
            else {
                'INVALID_FIELD_VALUE'
            }
            Add-ValidationError -Code $code -Message "$Path value '$Value' is not allowed by the catalog schema."
            return
        }
    }

    if ($null -eq $Value) {
        return
    }

    if ($Value -is [System.Management.Automation.PSCustomObject]) {
        $requiredNames = if (Test-ObjectProperty -Object $SchemaNode -Name 'required') { @($SchemaNode.required) } else { @() }
        foreach ($requiredName in $requiredNames) {
            if (-not (Test-ObjectProperty -Object $Value -Name ([string]$requiredName))) {
                Add-ValidationError -Code 'MISSING_REQUIRED_FIELD' -Message "$Path is missing required field '$requiredName'."
            }
        }

        $propertySchemas = if (Test-ObjectProperty -Object $SchemaNode -Name 'properties') { $SchemaNode.properties } else { $null }
        foreach ($property in $Value.PSObject.Properties) {
            $hasPropertySchema = $null -ne $propertySchemas -and (Test-ObjectProperty -Object $propertySchemas -Name $property.Name)
            if (-not $hasPropertySchema) {
                if ((Test-ObjectProperty -Object $SchemaNode -Name 'additionalProperties') -and $SchemaNode.additionalProperties -eq $false) {
                    Add-ValidationError -Code 'UNKNOWN_FIELD' -Message "$Path contains unknown field '$($property.Name)'."
                }
                continue
            }
            Test-JsonSchemaNode -Value $property.Value -SchemaNode $propertySchemas.($property.Name) -RootSchema $RootSchema -Path "$Path.$($property.Name)"
        }
    }

    if ($Value -is [System.Array]) {
        if ((Test-ObjectProperty -Object $SchemaNode -Name 'minItems') -and $Value.Count -lt [int]$SchemaNode.minItems) {
            Add-ValidationError -Code 'INVALID_FIELD_VALUE' -Message "$Path must contain at least $($SchemaNode.minItems) item(s)."
        }
        if ((Test-ObjectProperty -Object $SchemaNode -Name 'uniqueItems') -and $SchemaNode.uniqueItems) {
            $seenItems = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::Ordinal)
            foreach ($item in $Value) {
                $serializedItem = $item | ConvertTo-Json -Depth 100 -Compress
                if (-not $seenItems.Add([string]$serializedItem)) {
                    Add-ValidationError -Code 'INVALID_FIELD_VALUE' -Message "$Path must contain unique items."
                    break
                }
            }
        }
        if (Test-ObjectProperty -Object $SchemaNode -Name 'items') {
            for ($itemIndex = 0; $itemIndex -lt $Value.Count; $itemIndex++) {
                Test-JsonSchemaNode -Value $Value[$itemIndex] -SchemaNode $SchemaNode.items -RootSchema $RootSchema -Path "$Path[$itemIndex]"
            }
        }
    }

    if ($Value -is [string]) {
        if ((Test-ObjectProperty -Object $SchemaNode -Name 'minLength') -and $Value.Length -lt [int]$SchemaNode.minLength) {
            Add-ValidationError -Code 'INVALID_FIELD_VALUE' -Message "$Path must contain at least $($SchemaNode.minLength) character(s)."
        }
        if ((Test-ObjectProperty -Object $SchemaNode -Name 'pattern') -and $Value -notmatch $SchemaNode.pattern) {
            Add-ValidationError -Code 'INVALID_FIELD_VALUE' -Message "$Path does not match required pattern '$($SchemaNode.pattern)'."
        }
    }

    if ((Test-JsonValueType -Value $Value -TypeName 'integer') -and
        (Test-ObjectProperty -Object $SchemaNode -Name 'minimum') -and
        [decimal]$Value -lt [decimal]$SchemaNode.minimum) {
        Add-ValidationError -Code 'INVALID_FIELD_VALUE' -Message "$Path must be at least $($SchemaNode.minimum)."
    }
}

function Get-EffectiveValue {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Entry,
        [AllowNull()]
        [object]$Override,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $value = $null
    if ($null -ne $Override -and (Test-ObjectProperty -Object $Override -Name $Name)) {
        $value = $Override.$Name
    }
    elseif (Test-ObjectProperty -Object $Entry -Name $Name) {
        $value = $Entry.$Name
    }

    if ($value -is [System.Array]) {
        return ,$value
    }
    return $value
}

function ConvertTo-ReportAnchor {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Heading
    )

    $anchor = $Heading.Trim().ToLowerInvariant()
    $anchor = [regex]::Replace($anchor, '<[^>]+>', '')
    $anchor = [regex]::Replace($anchor, '[^\p{L}\p{Nd}\s_-]', '')
    $anchor = [regex]::Replace($anchor, '\s+', '-')
    return $anchor
}

function Test-ReportReference {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Reference,
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot,
        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    $separatorIndex = $Reference.LastIndexOf('#')
    if ($separatorIndex -le 0 -or $separatorIndex -eq $Reference.Length - 1) {
        Add-ValidationError -Code 'REPORT_ANCHOR_NOT_FOUND' -Message "$Label reportAnchor must be a repository-relative Markdown path followed by #anchor: '$Reference'."
        return
    }

    $relativePath = $Reference.Substring(0, $separatorIndex)
    $requestedAnchor = $Reference.Substring($separatorIndex + 1)
    $nativePath = $relativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
    $reportPath = [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $nativePath))
    $repositoryPrefix = $RepositoryRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $reportPath.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
        Add-ValidationError -Code 'REPORT_ANCHOR_NOT_FOUND' -Message "$Label report file does not exist: '$relativePath'."
        return
    }

    $anchors = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::Ordinal)
    foreach ($line in [System.IO.File]::ReadLines($reportPath)) {
        $headingMatch = [regex]::Match($line, '^\s{0,3}#{1,6}\s+(?<heading>.+?)\s*#*\s*$')
        if ($headingMatch.Success) {
            [void]$anchors.Add((ConvertTo-ReportAnchor -Heading $headingMatch.Groups['heading'].Value))
        }
    }

    if (-not $anchors.Contains($requestedAnchor)) {
        Add-ValidationError -Code 'REPORT_ANCHOR_NOT_FOUND' -Message "$Label report anchor '#$requestedAnchor' was not found in '$relativePath'."
    }
}

function Get-CSharpMatchingBraceIndex {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,
        [Parameter(Mandatory = $true)]
        [int]$OpeningIndex
    )

    if ($OpeningIndex -lt 0 -or $OpeningIndex -ge $Text.Length -or $Text[$OpeningIndex] -ne '{') {
        return -1
    }

    $depth = 0
    $state = 'Code'
    for ($index = $OpeningIndex; $index -lt $Text.Length; $index++) {
        $character = $Text[$index]
        $nextCharacter = if ($index + 1 -lt $Text.Length) { $Text[$index + 1] } else { [char]0 }

        switch ($state) {
            'LineComment' {
                if ($character -eq "`n") { $state = 'Code' }
                continue
            }
            'BlockComment' {
                if ($character -eq '*' -and $nextCharacter -eq '/') {
                    $state = 'Code'
                    $index++
                }
                continue
            }
            'String' {
                if ($character -eq '\\') {
                    $index++
                }
                elseif ($character -eq '"') {
                    $state = 'Code'
                }
                continue
            }
            'VerbatimString' {
                if ($character -eq '"' -and $nextCharacter -eq '"') {
                    $index++
                }
                elseif ($character -eq '"') {
                    $state = 'Code'
                }
                continue
            }
            'Character' {
                if ($character -eq '\\') {
                    $index++
                }
                elseif ($character -eq "'") {
                    $state = 'Code'
                }
                continue
            }
        }

        if ($character -eq '/' -and $nextCharacter -eq '/') {
            $state = 'LineComment'
            $index++
        }
        elseif ($character -eq '/' -and $nextCharacter -eq '*') {
            $state = 'BlockComment'
            $index++
        }
        elseif ($character -eq '"') {
            $state = if ($index -gt 0 -and $Text[$index - 1] -eq '@') { 'VerbatimString' } else { 'String' }
        }
        elseif ($character -eq "'") {
            $state = 'Character'
        }
        elseif ($character -eq '{') {
            $depth++
        }
        elseif ($character -eq '}') {
            $depth--
            if ($depth -eq 0) {
                return $index
            }
        }
    }

    return -1
}

function Find-RepositoryRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResolvedSourceRoot
    )

    $sourceRootPath = [System.IO.Path]::GetFullPath($ResolvedSourceRoot)
    $candidate = [System.IO.DirectoryInfo]::new($sourceRootPath)
    while ($null -ne $candidate) {
        $verificationRoot = Join-Path $candidate.FullName 'src\RMC.BestFit.Verification'
        if (Test-Path -LiteralPath $verificationRoot -PathType Container) {
            $verificationRootPath = [System.IO.Path]::GetFullPath($verificationRoot)
            $verificationPrefix = $verificationRootPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
            if ($sourceRootPath.Equals($verificationRootPath, [System.StringComparison]::OrdinalIgnoreCase) -or
                $sourceRootPath.StartsWith($verificationPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                return $candidate.FullName
            }
        }
        $candidate = $candidate.Parent
    }

    return $null
}

function Get-DiscoveredMethods {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot
    )

    $methods = New-Object System.Collections.Generic.List[object]
    $fileRecords = New-Object System.Collections.Generic.List[object]
    $knownTestClasses = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::Ordinal)
    $methodPattern = '(?ms)(?<attributes>(?:^[ \t]*(?:\[[^\r\n]*\]|//[^\r\n]*)[ \t]*\r?\n)+)^[ \t]*public[ \t]+(?:(?:static|async|virtual|override|new)[ \t]+)*(?<returnType>[A-Za-z_][A-Za-z0-9_<>,.\[\]?]*)[ \t]+(?<method>[A-Za-z_][A-Za-z0-9_]*)[ \t]*\('
    $classPattern = '(?ms)(?<attributes>(?:^[ \t]*\[[^\r\n]*\][ \t]*\r?\n)*)^[ \t]*(?:(?:public|internal|private|protected)(?:[ \t]+internal)?[ \t]+)?(?:(?:sealed|partial|abstract|static)[ \t]+)*class[ \t]+(?<class>[A-Za-z_][A-Za-z0-9_]*)[^\{]*?(?<openBrace>\{)'
    $namespacePattern = '(?m)^\s*namespace\s+(?<namespace>[A-Za-z_][A-Za-z0-9_.]*)\s*[;{]'
    $dataRowPattern = '(?m)^[ \t]*\[DataRow(?:Attribute)?\s*\((?<arguments>.*)\)\s*\][ \t]*\r?$'
    $displayNamePattern = '\bDisplayName\s*=\s*"(?<name>(?:\\.|[^"])*)"'

    $files = Get-ChildItem -LiteralPath $Root -Recurse -Filter *.cs -File |
        Where-Object { $_.FullName -notmatch '\\(bin|obj|TestResults)\\' }

    foreach ($file in $files) {
        $sourceText = [System.IO.File]::ReadAllText($file.FullName)
        $namespaceMatch = [regex]::Match($sourceText, $namespacePattern)
        if (-not $namespaceMatch.Success) {
            continue
        }

        $classes = New-Object System.Collections.Generic.List[object]
        foreach ($classMatch in [regex]::Matches($sourceText, $classPattern)) {
            $openingIndex = $classMatch.Groups['openBrace'].Index
            $closingIndex = Get-CSharpMatchingBraceIndex -Text $sourceText -OpeningIndex $openingIndex
            if ($closingIndex -lt 0) {
                Add-ValidationError -Code 'SOURCE_DISCOVERY' -Message "Could not resolve the closing brace for class '$($classMatch.Groups['class'].Value)' in '$($file.FullName)'."
                continue
            }
            $className = $classMatch.Groups['class'].Value
            [void]$classes.Add([pscustomobject]@{
                Name = $className
                OpeningIndex = $openingIndex
                ClosingIndex = $closingIndex
            })
            if ([regex]::IsMatch($classMatch.Groups['attributes'].Value, '(?m)^\s*\[TestClass(?:Attribute)?(?:\s*\(\s*\))?\s*\]')) {
                [void]$knownTestClasses.Add("$($namespaceMatch.Groups['namespace'].Value).$className")
            }
        }

        [void]$fileRecords.Add([pscustomobject]@{
            File = $file
            SourceText = $sourceText
            Namespace = $namespaceMatch.Groups['namespace'].Value
            Classes = $classes.ToArray()
        })
    }

    foreach ($fileRecord in $fileRecords) {
        $file = $fileRecord.File
        $sourceText = $fileRecord.SourceText
        foreach ($methodMatch in [regex]::Matches($sourceText, $methodPattern)) {
            $attributes = $methodMatch.Groups['attributes'].Value
            $isOrdinary = [regex]::IsMatch($attributes, '(?m)^\s*\[TestMethod(?:Attribute)?(?:\s*\(\s*\))?\s*\]')
            $isDataDriven = [regex]::IsMatch($attributes, '(?m)^\s*\[DataTestMethod(?:Attribute)?(?:\s*\(\s*\))?\s*\]')
            if (-not $isOrdinary -and -not $isDataDriven) {
                continue
            }

            $containingClasses = @($fileRecord.Classes | Where-Object {
                $_.OpeningIndex -lt $methodMatch.Index -and $_.ClosingIndex -gt $methodMatch.Index
            } | Sort-Object -Property OpeningIndex -Descending)
            if ($containingClasses.Count -eq 0) {
                Add-ValidationError -Code 'SOURCE_DISCOVERY' -Message "Could not associate test method '$($methodMatch.Groups['method'].Value)' with an enclosing class in '$($file.FullName)'."
                continue
            }
            $className = $containingClasses[0].Name
            $classKey = "$($fileRecord.Namespace).$className"
            if (-not $knownTestClasses.Contains($classKey)) {
                Add-ValidationError -Code 'SOURCE_DISCOVERY' -Message "Test method '$($methodMatch.Groups['method'].Value)' is enclosed by '$classKey', which is not declared as a TestClass in the source scope."
                continue
            }

            $rowNames = New-Object System.Collections.Generic.List[string]
            if ($isDataDriven) {
                foreach ($rowMatch in [regex]::Matches($attributes, $dataRowPattern)) {
                    $displayMatch = [regex]::Match($rowMatch.Groups['arguments'].Value, $displayNamePattern)
                    if (-not $displayMatch.Success) {
                        Add-ValidationError -Code 'UNNAMED_DATAROW' -Message "DataRow on '$($fileRecord.Namespace).$className.$($methodMatch.Groups['method'].Value)' must declare DisplayName."
                        continue
                    }
                    [void]$rowNames.Add([regex]::Unescape($displayMatch.Groups['name'].Value))
                }
            }

            $sourceFullPath = [System.IO.Path]::GetFullPath($file.FullName)
            $repositoryPrefix = $RepositoryRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
            $relativeSource = if ($sourceFullPath.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                $sourceFullPath.Substring($repositoryPrefix.Length).Replace([System.IO.Path]::DirectorySeparatorChar, '/')
            }
            else {
                $sourceFullPath
            }

            [void]$methods.Add([pscustomobject]@{
                Source = $relativeSource
                SourceFullPath = $sourceFullPath
                Namespace = $fileRecord.Namespace
                Class = $className
                Method = $methodMatch.Groups['method'].Value
                IsDataDriven = $isDataDriven
                DataRowNames = @($rowNames)
            })
        }
    }

    return $methods.ToArray()
}

function Test-ScientificFields {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Entry,
        [AllowNull()]
        [object]$Override,
        [Parameter(Mandatory = $true)]
        [string]$Label,
        [Parameter(Mandatory = $true)]
        [string[]]$EvidenceKinds,
        [Parameter(Mandatory = $true)]
        [string[]]$Dispositions,
        [Parameter(Mandatory = $true)]
        [string[]]$Statuses,
        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot
    )

    $primaryEvidenceKind = Get-EffectiveValue -Entry $Entry -Override $Override -Name 'primaryEvidenceKind'
    $evidenceTagsValue = Get-EffectiveValue -Entry $Entry -Override $Override -Name 'evidenceTags'
    $evidenceTags = @($evidenceTagsValue)
    $disposition = Get-EffectiveValue -Entry $Entry -Override $Override -Name 'disposition'
    $status = Get-EffectiveValue -Entry $Entry -Override $Override -Name 'status'
    $gap = Get-EffectiveValue -Entry $Entry -Override $Override -Name 'gap'
    $sampleSize = Get-EffectiveValue -Entry $Entry -Override $Override -Name 'sampleSize'
    $reportAnchor = Get-EffectiveValue -Entry $Entry -Override $Override -Name 'reportAnchor'

    if ($primaryEvidenceKind -notin $EvidenceKinds) {
        Add-ValidationError -Code 'UNKNOWN_EVIDENCE_KIND' -Message "$Label has unknown primaryEvidenceKind '$primaryEvidenceKind'."
    }

    if ($evidenceTagsValue -isnot [System.Array]) {
        Add-ValidationError -Code 'INVALID_FIELD_TYPE' -Message "$Label evidenceTags must be a JSON array."
    }
    elseif ($evidenceTags.Count -eq 0) {
        Add-ValidationError -Code 'EVIDENCE_TAGS_REQUIRED' -Message "$Label must declare at least one evidenceTags value."
    }
    else {
        $seenTags = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::Ordinal)
        foreach ($tag in $evidenceTags) {
            if ($tag -notin $EvidenceKinds) {
                Add-ValidationError -Code 'UNKNOWN_EVIDENCE_KIND' -Message "$Label has unknown evidence tag '$tag'."
            }
            if (-not $seenTags.Add([string]$tag)) {
                Add-ValidationError -Code 'DUPLICATE_EVIDENCE_TAG' -Message "$Label repeats evidence tag '$tag'."
            }
        }
        if ($primaryEvidenceKind -in $EvidenceKinds -and $primaryEvidenceKind -notin $evidenceTags) {
            Add-ValidationError -Code 'PRIMARY_EVIDENCE_NOT_TAGGED' -Message "$Label primaryEvidenceKind '$primaryEvidenceKind' must also appear in evidenceTags."
        }
    }

    if ($disposition -notin $Dispositions) {
        Add-ValidationError -Code 'UNKNOWN_DISPOSITION' -Message "$Label has unknown disposition '$disposition'."
    }
    if ($status -notin $Statuses) {
        Add-ValidationError -Code 'UNKNOWN_STATUS' -Message "$Label has unknown status '$status'."
    }

    if ($primaryEvidenceKind -eq 'recovery' -or 'recovery' -in $evidenceTags) {
        if ($sampleSize -ne 1000 -and $status -ne 'open') {
            Add-ValidationError -Code 'RECOVERY_SAMPLE_SIZE' -Message "$Label is a recovery claim with sampleSize '$sampleSize'; exactly 1000 observational units are required."
        }
    }

    if ([string]::IsNullOrWhiteSpace([string]$reportAnchor)) {
        Add-ValidationError -Code 'REPORT_ANCHOR_NOT_FOUND' -Message "$Label must declare a reportAnchor."
    }
    else {
        Test-ReportReference -Reference ([string]$reportAnchor) -RepositoryRoot $RepositoryRoot -Label $Label
    }

    if ($status -eq 'verified' -and -not [string]::IsNullOrWhiteSpace([string]$gap)) {
        Add-ValidationError -Code 'STATUS_GAP_MISMATCH' -Message "$Label is verified but declares gap text."
    }
    if ($status -ne 'verified' -and [string]::IsNullOrWhiteSpace([string]$gap)) {
        Add-ValidationError -Code 'STATUS_GAP_MISMATCH' -Message "$Label status '$status' requires non-empty gap text."
    }
    if ($status -eq 'open') {
        [void]$script:openGaps.Add("GAP [$Label] $gap")
        if ($RequireComplete) {
            Add-ValidationError -Code 'OPEN_GAP' -Message "$Label remains open: $gap"
        }
    }
}

function Write-ValidationFailureAndExit {
    foreach ($failure in $script:validationErrors) {
        Write-Host $failure
    }
    Write-Host "Verification catalog validation FAILED with $($script:validationErrors.Count) error(s)."
    exit 1
}

try {
    $schemaPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'docs\verification\verification-catalog.schema.json'
    if (-not (Test-Path -LiteralPath $schemaPath -PathType Leaf)) {
        Add-ValidationError -Code 'SCHEMA_NOT_FOUND' -Message "Catalog schema not found: '$schemaPath'."
        Write-ValidationFailureAndExit
    }

    try {
        $schema = Get-Content -LiteralPath $schemaPath -Raw | ConvertFrom-Json
    }
    catch {
        Add-ValidationError -Code 'SCHEMA_INVALID' -Message "Catalog schema is not valid JSON: $($_.Exception.Message)"
        Write-ValidationFailureAndExit
    }

    $requiredFields = @($schema.'$defs'.catalogEntry.required)
    $entryProperties = @($schema.'$defs'.catalogEntry.properties.PSObject.Properties.Name)
    $overrideProperties = @($schema.'$defs'.methodOverride.properties.PSObject.Properties.Name)
    $evidenceKinds = @($schema.'$defs'.evidenceKind.enum)
    $dispositions = @($schema.'$defs'.disposition.enum)
    $statuses = @($schema.'$defs'.status.enum)
    $expectedFields = @(
        'source', 'namespace', 'class', 'method', 'analysis', 'model',
        'primaryEvidenceKind', 'evidenceTags', 'disposition', 'methodOverrides',
        'executesEstimator', 'sampleUnit', 'sampleSize', 'seed', 'oracle',
        'acceptanceRule', 'artifact', 'reportAnchor', 'status', 'gap'
    )
    foreach ($field in $expectedFields) {
        if ($field -notin $requiredFields -or $field -notin $entryProperties) {
            Add-ValidationError -Code 'SCHEMA_INVALID' -Message "Catalog schema must define required field '$field'."
        }
    }

    if (-not (Test-Path -LiteralPath $Catalog -PathType Leaf)) {
        Add-ValidationError -Code 'CATALOG_NOT_FOUND' -Message "Catalog not found: '$Catalog'."
    }
    if (-not (Test-Path -LiteralPath $SourceRoot -PathType Container)) {
        Add-ValidationError -Code 'SOURCE_ROOT_NOT_FOUND' -Message "Source root not found: '$SourceRoot'."
    }
    if ($validationErrors.Count -gt 0) {
        Write-ValidationFailureAndExit
    }

    $catalogPath = (Resolve-Path -LiteralPath $Catalog).Path
    $sourceRootPath = (Resolve-Path -LiteralPath $SourceRoot).Path
    $repositoryRoot = Find-RepositoryRoot -ResolvedSourceRoot $sourceRootPath
    if ([string]::IsNullOrWhiteSpace($repositoryRoot)) {
        Add-ValidationError -Code 'REPOSITORY_ROOT_NOT_FOUND' -Message "Could not locate an ancestor containing src/RMC.BestFit.Verification for SourceRoot '$sourceRootPath'."
        Write-ValidationFailureAndExit
    }

    try {
        $catalogObject = Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json
    }
    catch {
        Add-ValidationError -Code 'CATALOG_INVALID_JSON' -Message "Catalog is not valid JSON: $($_.Exception.Message)"
        Write-ValidationFailureAndExit
    }

    Test-JsonSchemaNode -Value $catalogObject -SchemaNode $schema -RootSchema $schema -Path '$'
    if ($validationErrors.Count -gt 0) {
        Write-ValidationFailureAndExit
    }

    if (-not (Test-ObjectProperty -Object $catalogObject -Name 'schemaVersion') -or $catalogObject.schemaVersion -ne 1) {
        Add-ValidationError -Code 'SCHEMA_VERSION' -Message 'Catalog schemaVersion must be 1.'
    }
    if (-not (Test-ObjectProperty -Object $catalogObject -Name 'entries')) {
        Add-ValidationError -Code 'MISSING_REQUIRED_FIELD' -Message "Catalog is missing required field 'entries'."
        $entries = @()
    }
    else {
        $entries = @($catalogObject.entries)
    }

    $catalogByMethod = New-Object 'System.Collections.Generic.Dictionary[string,object]' ([System.StringComparer]::Ordinal)
    $sourceRootPrefix = $sourceRootPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

    for ($entryIndex = 0; $entryIndex -lt $entries.Count; $entryIndex++) {
        $entry = $entries[$entryIndex]
        $label = "entry[$entryIndex]"
        foreach ($field in $requiredFields) {
            if (-not (Test-ObjectProperty -Object $entry -Name $field)) {
                Add-ValidationError -Code 'MISSING_REQUIRED_FIELD' -Message "$label is missing required field '$field'."
            }
        }
        foreach ($property in $entry.PSObject.Properties.Name) {
            if ($property -notin $entryProperties) {
                Add-ValidationError -Code 'UNKNOWN_FIELD' -Message "$label contains unknown field '$property'."
            }
        }

        $identityFields = @('source', 'namespace', 'class', 'method', 'analysis', 'model')
        foreach ($field in $identityFields) {
            if ((Test-ObjectProperty -Object $entry -Name $field) -and [string]::IsNullOrWhiteSpace([string]$entry.$field)) {
                Add-ValidationError -Code 'INVALID_FIELD_VALUE' -Message "$label field '$field' must be a non-empty string."
            }
        }

        $hasIdentity = @('namespace', 'class', 'method') | ForEach-Object { Test-ObjectProperty -Object $entry -Name $_ } | Where-Object { -not $_ }
        if (@($hasIdentity).Count -eq 0) {
            $methodKey = "$($entry.namespace).$($entry.class).$($entry.method)"
            $label = $methodKey
            if ($catalogByMethod.ContainsKey($methodKey)) {
                Add-ValidationError -Code 'DUPLICATE_ENTRY' -Message "Catalog contains more than one entry for '$methodKey'."
            }
            else {
                $catalogByMethod.Add($methodKey, $entry)
            }
        }

        if ((Test-ObjectProperty -Object $entry -Name 'executesEstimator') -and $entry.executesEstimator -isnot [bool]) {
            Add-ValidationError -Code 'INVALID_FIELD_TYPE' -Message "$label executesEstimator must be boolean."
        }
        if ((Test-ObjectProperty -Object $entry -Name 'sampleSize') -and $null -ne $entry.sampleSize -and $entry.sampleSize -isnot [int] -and $entry.sampleSize -isnot [long]) {
            Add-ValidationError -Code 'INVALID_FIELD_TYPE' -Message "$label sampleSize must be an integer or null."
        }

        if (Test-ObjectProperty -Object $entry -Name 'source') {
            $nativeSource = ([string]$entry.source).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
            $entrySourcePath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $nativeSource))
            if (-not $entrySourcePath.StartsWith($sourceRootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                Add-ValidationError -Code 'SOURCE_OUTSIDE_ROOT' -Message "$label source is outside SourceRoot: '$($entry.source)'."
            }
            elseif (-not (Test-Path -LiteralPath $entrySourcePath -PathType Leaf)) {
                Add-ValidationError -Code 'SOURCE_NOT_FOUND' -Message "$label source file does not exist: '$($entry.source)'."
            }
        }

        if (Test-ObjectProperty -Object $entry -Name 'methodOverrides') {
            $overrides = @($entry.methodOverrides)
            $overrideNames = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::Ordinal)
            foreach ($override in $overrides) {
                foreach ($property in $override.PSObject.Properties.Name) {
                    if ($property -notin $overrideProperties) {
                        Add-ValidationError -Code 'UNKNOWN_FIELD' -Message "$label method override contains unknown field '$property'."
                    }
                }
                if (-not (Test-ObjectProperty -Object $override -Name 'name') -or [string]::IsNullOrWhiteSpace([string]$override.name)) {
                    Add-ValidationError -Code 'MISSING_REQUIRED_FIELD' -Message "$label method override must declare a non-empty name."
                    continue
                }
                if (-not $overrideNames.Add([string]$override.name)) {
                    Add-ValidationError -Code 'DUPLICATE_METHOD_OVERRIDE' -Message "$label repeats method override '$($override.name)'."
                }
                Test-ScientificFields -Entry $entry -Override $override -Label "$label [$($override.name)]" -EvidenceKinds $evidenceKinds -Dispositions $dispositions -Statuses $statuses -RepositoryRoot $repositoryRoot
            }
        }

        $canValidateScientificFields = @('primaryEvidenceKind', 'evidenceTags', 'disposition', 'reportAnchor', 'status', 'gap') |
            ForEach-Object { Test-ObjectProperty -Object $entry -Name $_ } |
            Where-Object { -not $_ }
        if (@($canValidateScientificFields).Count -eq 0) {
            Test-ScientificFields -Entry $entry -Override $null -Label $label -EvidenceKinds $evidenceKinds -Dispositions $dispositions -Statuses $statuses -RepositoryRoot $repositoryRoot
        }
    }

    $discoveredMethods = @(Get-DiscoveredMethods -Root $sourceRootPath -RepositoryRoot $repositoryRoot)
    $discoveredByMethod = New-Object 'System.Collections.Generic.Dictionary[string,object]' ([System.StringComparer]::Ordinal)
    foreach ($method in $discoveredMethods) {
        $methodKey = "$($method.Namespace).$($method.Class).$($method.Method)"
        if ($discoveredByMethod.ContainsKey($methodKey)) {
            Add-ValidationError -Code 'DUPLICATE_SOURCE_METHOD' -Message "Source discovery found more than one declaration for '$methodKey'."
            continue
        }
        $discoveredByMethod.Add($methodKey, $method)

        if (-not $catalogByMethod.ContainsKey($methodKey)) {
            $code = if ($method.IsDataDriven) { 'UNLISTED_DATA_METHOD' } else { 'UNLISTED_METHOD' }
            Add-ValidationError -Code $code -Message "Source method '$methodKey' is not listed in the catalog."
            continue
        }

        $entry = $catalogByMethod[$methodKey]
        $nativeSource = ([string]$entry.source).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        $catalogSourcePath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $nativeSource))
        if (-not $catalogSourcePath.Equals($method.SourceFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
            Add-ValidationError -Code 'SOURCE_MISMATCH' -Message "Catalog source '$($entry.source)' does not contain '$methodKey'."
        }

        $overrides = @($entry.methodOverrides)
        if ($method.IsDataDriven) {
            if ($overrides.Count -ne $method.DataRowNames.Count) {
                Add-ValidationError -Code 'DATAROW_COUNT_MISMATCH' -Message "'$methodKey' has $($method.DataRowNames.Count) named DataRow unit(s) but $($overrides.Count) method override(s)."
            }
            $catalogRowNames = @($overrides | ForEach-Object { if (Test-ObjectProperty -Object $_ -Name 'name') { [string]$_.name } })
            foreach ($rowName in $method.DataRowNames) {
                if ($rowName -notin $catalogRowNames) {
                    Add-ValidationError -Code 'DATAROW_NAME_MISMATCH' -Message "'$methodKey' DataRow '$rowName' is not named by a method override."
                }
            }
        }
        elseif ($overrides.Count -gt 0) {
            Add-ValidationError -Code 'UNEXPECTED_METHOD_OVERRIDE' -Message "Ordinary test method '$methodKey' cannot declare methodOverrides."
        }
    }

    foreach ($methodKey in $catalogByMethod.Keys) {
        if (-not $discoveredByMethod.ContainsKey($methodKey)) {
            Add-ValidationError -Code 'METHOD_NOT_FOUND' -Message "Catalog entry '$methodKey' does not resolve to a discovered TestMethod or DataTestMethod."
        }
    }

    if ($validationErrors.Count -gt 0) {
        Write-ValidationFailureAndExit
    }

    foreach ($gapNotice in $openGaps) {
        Write-Host $gapNotice
    }
    $executionUnitCount = 0
    foreach ($method in $discoveredMethods) {
        $executionUnitCount += if ($method.IsDataDriven) { $method.DataRowNames.Count } else { 1 }
    }
    Write-Host "Verification catalog validation passed with $($openGaps.Count) open gap(s). Discovered $($discoveredMethods.Count) method declaration(s) and $executionUnitCount execution unit(s)."
    exit 0
}
catch {
    Add-ValidationError -Code 'VALIDATOR_EXCEPTION' -Message $_.Exception.Message
    Write-ValidationFailureAndExit
}
