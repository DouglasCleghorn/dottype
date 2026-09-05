param(
    [Parameter(Mandatory = $false)]
    [string] $PackageDirectory = "artifacts/package",

    [Parameter(Mandatory = $false)]
    [string] $ExpectedVersion = "0.1.0"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$resolvedDirectory = (Resolve-Path -LiteralPath $PackageDirectory).Path
$packages = @(Get-ChildItem -LiteralPath $resolvedDirectory -Filter "dottype.*.nupkg" -File |
    Where-Object { $_.Name -notlike "*.snupkg" })
$symbols = @(Get-ChildItem -LiteralPath $resolvedDirectory -Filter "dottype.*.snupkg" -File)

if ($packages.Count -ne 1) {
    throw "Expected exactly one dottype nupkg in '$resolvedDirectory'; found $($packages.Count)."
}

if ($symbols.Count -ne 1) {
    throw "Expected exactly one dottype snupkg in '$resolvedDirectory'; found $($symbols.Count)."
}

$package = [System.IO.Compression.ZipFile]::OpenRead($packages[0].FullName)
try {
    $entries = @($package.Entries | ForEach-Object { $_.FullName })
    $required = @(
        "tools/net10.0/any/DotnetToolSettings.xml",
        "tools/net10.0/any/Dottype.dll",
        "tools/net10.0/any/Dottype.xml",
        "tools/net10.0/any/Dottype.deps.json",
        "tools/net10.0/any/Dottype.runtimeconfig.json",
        "tools/net10.0/any/Parquet.dll",
        "README.md",
        "CHANGELOG.md",
        "RELEASING.md",
        "LICENSE",
        "THIRD-PARTY-NOTICES.txt"
    )

    foreach ($entry in $required) {
        if ($entries -notcontains $entry) {
            throw "Package is missing required entry '$entry'."
        }
    }

    if ($entries | Where-Object { $_ -match '(?i)(Dottype\.Tests|Microsoft\.CodeAnalysis|xunit|testhost|\.cs$)' }) {
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
    if ($metadata.id -ne "dottype") {
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
    if ($toolSettings.DotNetCliTool.Commands.Command.Name -ne "dottype") {
        throw "The .NET tool command name is not 'dottype'."
    }
}
finally {
    $package.Dispose()
}

$symbolPackage = [System.IO.Compression.ZipFile]::OpenRead($symbols[0].FullName)
try {
    $symbolEntries = @($symbolPackage.Entries | ForEach-Object { $_.FullName })
    if ($symbolEntries -notcontains "tools/net10.0/any/Dottype.pdb") {
        throw "Symbol package is missing the portable PDB."
    }
}
finally {
    $symbolPackage.Dispose()
}

Write-Host "Verified NuGet tool package and symbols for dottype $ExpectedVersion."
