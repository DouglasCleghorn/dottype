param(
    [Parameter(Mandatory = $false)]
    [string] $Version = "0.1.0",

    [Parameter(Mandatory = $false)]
    [string] $ManifestDirectory = "artifacts/winget/manifests/d/DouglasCleghorn/dottype/0.1.0",

    [Parameter(Mandatory = $false)]
    [string] $X64Installer = "artifacts/portable/release/dottype-win-x64.exe",

    [Parameter(Mandatory = $false)]
    [string] $Arm64Installer = "artifacts/portable/release/dottype-win-arm64.exe"
)

$ErrorActionPreference = "Stop"
$packageIdentifier = "DouglasCleghorn.dottype"
$resolvedDirectory = (Resolve-Path -LiteralPath $ManifestDirectory).Path
$requiredFiles = @(
    "$packageIdentifier.yaml",
    "$packageIdentifier.installer.yaml",
    "$packageIdentifier.locale.en-US.yaml"
)

$files = @(Get-ChildItem -LiteralPath $resolvedDirectory -Filter "*.yaml" -File)
if ($files.Count -ne $requiredFiles.Count) {
    throw "Expected exactly three WinGet manifests; found $($files.Count)."
}
foreach ($fileName in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $resolvedDirectory $fileName) -PathType Leaf)) {
        throw "WinGet manifest set is missing '$fileName'."
    }
}

$allContent = $files | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
foreach ($content in $allContent) {
    if ($content -notmatch "(?m)^PackageIdentifier: $([regex]::Escape($packageIdentifier))$") {
        throw "A WinGet manifest has the wrong package identifier."
    }
    if ($content -notmatch "(?m)^PackageVersion: $([regex]::Escape($Version))$") {
        throw "A WinGet manifest has the wrong package version."
    }
    if ($content -notmatch "(?m)^ManifestVersion: 1\.10\.0$") {
        throw "A WinGet manifest does not use schema version 1.10.0."
    }
}

$installerPath = Join-Path $resolvedDirectory "$packageIdentifier.installer.yaml"
$installerContent = Get-Content -LiteralPath $installerPath -Raw
if ($installerContent -match '(?m)^Scope:') {
    throw 'Portable installer manifests must not specify Scope.'
}
$expectedHashes = @(
    (Get-FileHash -LiteralPath $X64Installer -Algorithm SHA256).Hash,
    (Get-FileHash -LiteralPath $Arm64Installer -Algorithm SHA256).Hash
)
foreach ($hash in $expectedHashes) {
    if ($installerContent -notmatch "(?m)^\s*InstallerSha256: $hash$") {
        throw "The WinGet manifest does not contain expected installer hash '$hash'."
    }
}

foreach ($requiredValue in @(
    "InstallerType: portable",
    "UpgradeBehavior: uninstallPrevious",
    "Architecture: x64",
    "Architecture: arm64",
    "Commands:`n- dottype"
)) {
    if (-not $installerContent.Replace("`r`n", "`n").Contains($requiredValue, [StringComparison]::Ordinal)) {
        throw "The WinGet installer manifest is missing '$requiredValue'."
    }
}

$localeContent = Get-Content -LiteralPath (Join-Path $resolvedDirectory "$packageIdentifier.locale.en-US.yaml") -Raw
foreach ($requiredValue in @(
    "PackageLocale: en-US",
    "Publisher: Doug Cleghorn",
    "PackageName: dottype",
    "License: MIT",
    "Moniker: dottype"
)) {
    if (-not $localeContent.Contains($requiredValue, [StringComparison]::Ordinal)) {
        throw "The WinGet locale manifest is missing '$requiredValue'."
    }
}

Write-Host "Verified WinGet manifests and installer hashes for dottype $Version."
