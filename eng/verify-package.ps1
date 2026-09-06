param(
    [Parameter(Mandatory = $false)]
    [string] $PackageDirectory = "artifacts/package",

    [Parameter(Mandatory = $false)]
    [string] $ExpectedVersion = "0.2.0"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$resolvedDirectory = (Resolve-Path -LiteralPath $PackageDirectory).Path
$packages = @(Get-ChildItem -LiteralPath $resolvedDirectory -File |
    Where-Object { $_.Name -ceq "dottype.$ExpectedVersion.nupkg" })
$symbols = @(Get-ChildItem -LiteralPath $resolvedDirectory -File |
    Where-Object { $_.Name -ceq "dottype.$ExpectedVersion.snupkg" })

if ($packages.Count -ne 1) {
    throw "Expected exactly one dottype nupkg in '$resolvedDirectory'; found $($packages.Count)."
}

if ($symbols.Count -ne 1) {
    throw "Expected exactly one dottype snupkg in '$resolvedDirectory'; found $($symbols.Count)."
}

& "$PSScriptRoot/verify-package-listing.ps1" -PackagePath $packages[0].FullName

$package = [System.IO.Compression.ZipFile]::OpenRead($packages[0].FullName)
try {
    $entries = @($package.Entries | ForEach-Object { $_.FullName })
    $required = @(
        "tools/net10.0/any/DotnetToolSettings.xml",
        "tools/net10.0/any/dottype.dll",
        "tools/net10.0/any/dottype.xml",
        "tools/net10.0/any/dottype.deps.json",
        "tools/net10.0/any/dottype.runtimeconfig.json",
        "tools/net10.0/any/Parquet.dll",
        "tools/net10.0/any/System.CommandLine.dll",
        "README.md",
        "USAGE.md",
        "llms.txt",
        "CHANGELOG.md",
        "RELEASING.md",
        "LICENSE",
        "THIRD-PARTY-NOTICES.txt"
    )

    foreach ($entry in $required) {
        if ($entries -cnotcontains $entry) {
            throw "Package is missing required entry '$entry'."
        }
    }

    if ($entries | Where-Object { $_ -match '(?i)(dottype\.tests|Microsoft\.CodeAnalysis|xunit|testhost|\.cs$)' }) {
        throw 'The release package contains test tooling or loose source files.'
    }

    $unexpectedFrameworks = @($entries | Where-Object {
        $_ -like "tools/*" -and $_ -notlike "tools/net10.0/any/*"
    })
    if ($unexpectedFrameworks.Count -ne 0) {
        throw "Package contains unexpected tool entries: $($unexpectedFrameworks -join ', ')."
    }

    $nuspecEntry = @($package.Entries | Where-Object { $_.FullName -like "*.nuspec" })
    if ($nuspecEntry.Count -ne 1) {
        throw "Expected exactly one nuspec; found $($nuspecEntry.Count)."
    }

    $reader = [System.IO.StreamReader]::new($nuspecEntry[0].Open())
    try {
        [xml] $nuspec = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }

    $metadata = $nuspec.package.metadata
    if ($metadata.id -cne "dottype") {
        throw "Unexpected package ID '$($metadata.id)'."
    }
    if ($metadata.version -ne $ExpectedVersion) {
        throw "Package version '$($metadata.version)' does not match '$ExpectedVersion'."
    }
    if ($metadata.license.'#text' -ne "MIT") {
        throw "Package license must be the MIT expression."
    }
    if ($metadata.repository.url -ne "https://github.com/DouglasCleghorn/dottype") {
        throw "Unexpected repository URL '$($metadata.repository.url)'."
    }
    if (@($metadata.packageTypes.packageType | Where-Object { $_.name -eq "DotnetTool" }).Count -ne 1) {
        throw "Package is not marked as a .NET tool."
    }

    $toolSettingsEntry = $package.GetEntry("tools/net10.0/any/DotnetToolSettings.xml")
    $reader = [System.IO.StreamReader]::new($toolSettingsEntry.Open())
    try {
        [xml] $toolSettings = $reader.ReadToEnd()
    }
    finally {
        $reader.Dispose()
    }
    if ($toolSettings.DotNetCliTool.Commands.Command.Name -cne "dottype") {
        throw "The .NET tool command name is not 'dottype'."
    }
    if ($toolSettings.DotNetCliTool.Commands.Command.EntryPoint -cne "dottype.dll") {
        throw "The .NET tool entry point is not 'dottype.dll'."
    }
}
finally {
    $package.Dispose()
}

$symbolPackage = [System.IO.Compression.ZipFile]::OpenRead($symbols[0].FullName)
try {
    $symbolEntries = @($symbolPackage.Entries | ForEach-Object { $_.FullName })
    if ($symbolEntries -cnotcontains "tools/net10.0/any/dottype.pdb") {
        throw "Symbol package is missing the portable PDB."
    }
}
finally {
    $symbolPackage.Dispose()
}

Write-Host "Verified NuGet tool package and symbols for dottype $ExpectedVersion."
