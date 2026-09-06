param(
    [Parameter(Mandatory)]
    [string] $PackagePath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$package = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PackagePath).Path)
try {
    $specs = @($package.Entries | Where-Object FullName -like '*.nuspec')
    if ($specs.Count -ne 1) { throw 'Expected exactly one package manifest.' }
    $reader = [IO.StreamReader]::new($specs[0].Open())
    try { [xml] $nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $metadata = $nuspec.package.metadata

    foreach ($field in @('title', 'authors', 'description', 'tags', 'projectUrl', 'releaseNotes')) {
        if ([string]::IsNullOrWhiteSpace($metadata.$field)) { throw "Listing metadata is missing '$field'." }
    }
    if ($metadata.icon -cne 'icon.png' -or $metadata.readme -cne 'README.md') {
        throw 'Listing must reference the embedded icon.png and README.md.'
    }
    $icon = $package.GetEntry($metadata.icon)
    if (!$icon -or $icon.Length -lt 24 -or $icon.Length -gt 1MB) {
        throw 'Package icon is missing, empty, or larger than 1 MB.'
    }
    $stream = $icon.Open()
    try {
        $header = [byte[]]::new(24)
        $stream.ReadExactly($header, 0, $header.Length)
        if ([Convert]::ToHexString($header[0..7]) -cne '89504E470D0A1A0A' -or
            [Convert]::ToHexString($header[12..15]) -cne '49484452' -or
            [Convert]::ToHexString($header[16..23]) -cne '0000008000000080') {
            throw 'Package icon must be a 128 by 128 PNG.'
        }
    } finally { $stream.Dispose() }

    $readme = $package.GetEntry($metadata.readme)
    if (!$readme) { throw 'Package listing README is missing.' }
    $reader = [IO.StreamReader]::new($readme.Open())
    try { $markdown = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if ($markdown -notmatch '!\[dottype\]\(https://raw\.githubusercontent\.com/DouglasCleghorn/dottype/HEAD/assets/icon\.png\)') {
        throw 'Listing README must include the hosted dottype image.'
    }
    foreach ($link in [regex]::Matches($markdown, '\[[^\]]*\]\(([^)\s]+)\)')) {
        if ($link.Groups[1].Value -notmatch '^(https://|#)') {
            throw "Listing README contains a relative or insecure link: $($link.Groups[1].Value)"
        }
    }
    $install = switch -CaseSensitive ($metadata.id) {
        'dottype' { 'dotnet tool install --global dottype' }
        'dottype.Build' { 'dotnet add package dottype.Build' }
        default { throw "Unexpected package ID '$($metadata.id)'." }
    }
    if (!$markdown.Contains($install)) { throw "Listing README is missing its installation command: $install" }
    Write-Host "Verified NuGet listing assets and metadata for $($metadata.id)."
} finally { $package.Dispose() }
