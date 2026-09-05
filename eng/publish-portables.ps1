param(
    [Parameter(Mandatory = $false)]
    [string] $Version = "0.1.0",

    [Parameter(Mandatory = $false)]
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",

    [Parameter(Mandatory = $false)]
    [string] $OutputDirectory = "artifacts/portable"
)

$ErrorActionPreference = "Stop"
if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:[-+][0-9A-Za-z.-]+)?$') {
    throw "Version '$Version' is not a supported semantic version."
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot "src/Dottype/Dottype.csproj"
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory, $repositoryRoot)
$releaseDirectory = Join-Path $resolvedOutput "release"
New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null

foreach ($notice in @("LICENSE", "THIRD-PARTY-NOTICES.txt")) {
    Copy-Item `
        -LiteralPath (Join-Path $repositoryRoot $notice) `
        -Destination (Join-Path $releaseDirectory $notice) `
        -Force
}

foreach ($runtime in @("win-x64", "win-arm64")) {
    $publishDirectory = Join-Path $resolvedOutput "publish/$runtime"
    dotnet publish $project `
        --configuration $Configuration `
        --runtime $runtime `
        --self-contained true `
        --output $publishDirectory `
        -p:Version=$Version `
        -p:PublishSingleFile=true `
        -p:PublishTrimmed=false `
        -p:PublishReadyToRun=false `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -p:RestoreLockedMode=true
    if ($LASTEXITCODE -ne 0) {
        throw "Publishing dottype for $runtime failed with exit code $LASTEXITCODE."
    }

    $publishedExecutable = Join-Path $publishDirectory "Dottype.exe"
    $releaseExecutable = Join-Path $releaseDirectory "dottype-$runtime.exe"
    Copy-Item -LiteralPath $publishedExecutable -Destination $releaseExecutable -Force

    # A self-contained release also redistributes the runtime and its native dependencies.
    $assets = Get-Content (Join-Path $repositoryRoot 'src/Dottype/obj/project.assets.json') -Raw |
        ConvertFrom-Json -AsHashtable
    $runtimePackage = $assets.project.frameworks['net10.0'].downloadDependencies |
        Where-Object { $_.name -eq "Microsoft.NETCore.App.Runtime.$runtime" }
    if (-not $runtimePackage) { throw "Cannot locate the restored runtime notices for $runtime." }
    $runtimeVersion = ($runtimePackage.version.Trim('[', ']') -split ',')[0].Trim()
    $packageRelativePath = "microsoft.netcore.app.runtime.$runtime/$runtimeVersion"
    $runtimeDirectory = $assets.packageFolders.Keys |
        ForEach-Object { Join-Path $_ $packageRelativePath } |
        Where-Object { Test-Path -LiteralPath $_ -PathType Container } |
        Select-Object -First 1
    if (-not $runtimeDirectory) { throw "Runtime package directory not found: $packageRelativePath" }
    Copy-Item -LiteralPath (Join-Path $runtimeDirectory 'LICENSE.TXT') `
        -Destination (Join-Path $releaseDirectory "DOTNET-$runtime-LICENSE.txt") -Force
    Copy-Item -LiteralPath (Join-Path $runtimeDirectory 'THIRD-PARTY-NOTICES.TXT') `
        -Destination (Join-Path $releaseDirectory "DOTNET-$runtime-THIRD-PARTY-NOTICES.txt") -Force
}

Get-ChildItem -LiteralPath $releaseDirectory -Filter "dottype-win-*.exe" -File |
    Sort-Object Name |
    ForEach-Object {
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        Write-Host "$($_.Name) $hash"
    }
