param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [Parameter(Mandatory = $true)]
    [string] $X64Installer,

    [Parameter(Mandatory = $true)]
    [string] $Arm64Installer,

    [Parameter(Mandatory = $false)]
    [string] $OutputDirectory = "artifacts/winget"
)

$ErrorActionPreference = "Stop"
if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?$') {
    throw "Version '$Version' is not a supported semantic version."
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$x64Path = (Resolve-Path -LiteralPath $X64Installer).Path
$arm64Path = (Resolve-Path -LiteralPath $Arm64Installer).Path
$x64Hash = (Get-FileHash -LiteralPath $x64Path -Algorithm SHA256).Hash
$arm64Hash = (Get-FileHash -LiteralPath $arm64Path -Algorithm SHA256).Hash
$releaseRoot = "https://github.com/DouglasCleghorn/dottype/releases/download/v$Version"
$packageIdentifier = "DouglasCleghorn.dottype"
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory, $repositoryRoot)
$manifestDirectory = Join-Path $resolvedOutput "manifests/d/DouglasCleghorn/dottype/$Version"
New-Item -ItemType Directory -Path $manifestDirectory -Force | Out-Null

$versionManifest = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.version.1.10.0.schema.json
PackageIdentifier: $packageIdentifier
PackageVersion: $Version
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.10.0
"@

$installerManifest = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.installer.1.10.0.schema.json
PackageIdentifier: $packageIdentifier
PackageVersion: $Version
InstallerType: portable
UpgradeBehavior: uninstallPrevious
Commands:
- dottype
Installers:
- Architecture: x64
  InstallerUrl: $releaseRoot/dottype-win-x64.exe
  InstallerSha256: $x64Hash
- Architecture: arm64
  InstallerUrl: $releaseRoot/dottype-win-arm64.exe
  InstallerSha256: $arm64Hash
ManifestType: installer
ManifestVersion: 1.10.0
"@

$localeManifest = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.defaultLocale.1.10.0.schema.json
PackageIdentifier: $packageIdentifier
PackageVersion: $Version
PackageLocale: en-US
Publisher: Doug Cleghorn
PublisherUrl: https://github.com/DouglasCleghorn
PublisherSupportUrl: https://github.com/DouglasCleghorn/dottype/issues
Author: Doug Cleghorn
PackageName: dottype
PackageUrl: https://github.com/DouglasCleghorn/dottype
License: MIT
LicenseUrl: https://github.com/DouglasCleghorn/dottype/blob/v$Version/LICENSE
Copyright: Copyright © 2026 Doug Cleghorn
ShortDescription: Generate minimal C# POCOs from JSON, CSV, and Parquet files.
Description: >-
  dottype is a small command-line tool that infers serialization-oriented C#
  partial classes while preserving source field names whenever possible.
Moniker: dottype
Tags:
- csharp
- csv
- dotnet
- json
- parquet
- poco
ReleaseNotesUrl: https://github.com/DouglasCleghorn/dottype/releases/tag/v$Version
ManifestType: defaultLocale
ManifestVersion: 1.10.0
"@

function Write-Utf8Lf([string] $Path, [string] $Content) {
    $normalized = $Content.TrimEnd() -replace "`r`n", "`n"
    [System.IO.File]::WriteAllText(
        $Path,
        $normalized + "`n",
        [System.Text.UTF8Encoding]::new($false))
}

Write-Utf8Lf (Join-Path $manifestDirectory "$packageIdentifier.yaml") $versionManifest
Write-Utf8Lf (Join-Path $manifestDirectory "$packageIdentifier.installer.yaml") $installerManifest
Write-Utf8Lf (Join-Path $manifestDirectory "$packageIdentifier.locale.en-US.yaml") $localeManifest

Write-Host "Generated WinGet manifests in '$manifestDirectory'."
