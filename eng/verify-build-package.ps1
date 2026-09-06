param(
    [string] $PackageDirectory = "$PSScriptRoot/../artifacts/package",
    [string] $ExpectedVersion = '0.2.0'
)
$ErrorActionPreference = 'Stop'
& "$PSScriptRoot/verify-package-listing.ps1" -PackagePath (Join-Path $PackageDirectory "dottype.Build.$ExpectedVersion.nupkg")
$scratch = [IO.Directory]::CreateTempSubdirectory('dottype-build-smoke-').FullName
$feed = [Security.SecurityElement]::Escape([IO.Path]::GetFullPath($PackageDirectory))
function Invoke-Dotnet([string[]] $Arguments, [switch] $ExpectFailure) {
    $output = (& dotnet @Arguments 2>&1 | ForEach-Object { "$_" }) -join "`n"
    if ($ExpectFailure) {
        if ($LASTEXITCODE -eq 0 -or $output -notmatch 'dottype001') { throw "Expected dottype001: $output" }
    } elseif ($LASTEXITCODE -ne 0) { throw "dotnet failed: $output" }
    return $output
}
try {
    Set-Content -LiteralPath (Join-Path $scratch 'Directory.Build.props') -Value '<Project />'
    Set-Content -LiteralPath (Join-Path $scratch 'Directory.Packages.props') -Value '<Project />'
    Set-Content -LiteralPath (Join-Path $scratch 'NuGet.Config') -Value "<configuration><packageSources><clear /><add key='local' value='$feed' /><add key='nuget.org' value='https://api.nuget.org/v3/index.json' /></packageSources><config><add key='globalPackagesFolder' value='./packages' /></config></configuration>"
    $app = Join-Path $scratch 'app.cs'
    $example = Join-Path $scratch 'sample file.json'
    Set-Content -LiteralPath $example -Value '{"id":1}'
    $appSource = @'
#:package dottype.Build@__VERSION__
#:property dottype='./sample file.json' --name=Models.Customer --sample=all

Console.WriteLine(string.Join(",", typeof(Models.Customer).GetProperties().Select(p => p.Name)));
'@
    Set-Content -LiteralPath $app -Value $appSource.Replace('__VERSION__', $ExpectedVersion)
    Invoke-Dotnet @('restore', $app, '--configfile', (Join-Path $scratch 'NuGet.Config')) | Out-Null
    foreach ($document in @('USAGE.md', 'llms.txt')) {
        if (!(Test-Path -LiteralPath (Join-Path $scratch "packages/dottype.build/$ExpectedVersion/$document"))) { throw "The build package is missing $document." }
    }
    foreach ($assembly in @('tasks/net10.0/System.CommandLine.dll', 'analyzers/dotnet/cs/System.CommandLine.dll')) {
        if (!(Test-Path -LiteralPath (Join-Path $scratch "packages/dottype.build/$ExpectedVersion/$assembly"))) { throw "The build package is missing $assembly." }
    }
    $design = Invoke-Dotnet @('build', $app, '--no-restore', '-t:Compile', '-p:DesignTimeBuild=true',
        '-p:SkipCompilerExecution=true', '-p:ProvideCommandLineArgs=true', '-getItem:AdditionalFiles,Analyzer', '-getProperty:FileBasedProgramCanSkipMSBuild') | ConvertFrom-Json
    if ($design.Properties.FileBasedProgramCanSkipMSBuild -ne 'false') { throw 'External example checks can be skipped by dotnet run.' }
    if (@($design.Items.AdditionalFiles | Where-Object { $_.dottypeExample -eq 'true' }).Count -ne 1) { throw 'Editor example was not registered.' }
    if (@($design.Items.Analyzer | Where-Object { $_.Identity -match 'dottype.Generator.dll$' }).Count -ne 1) { throw 'Editor generator was not registered.' }
    if ((Invoke-Dotnet @('run', '--file', $app, '--no-restore')).Trim() -ne 'id') { throw 'First run did not use the generated type.' }
    $compiled = Invoke-Dotnet @('build', $app, '--no-restore', '-t:dottypeGenerate', '-getItem:Compile') | ConvertFrom-Json
    $sourcePath = @($compiled.Items.Compile | Where-Object { $_.Identity -match 'dottype[/\\].*\.g\.cs$' })[0].FullPath
    $sourceTime = [IO.File]::GetLastWriteTimeUtc($sourcePath)
    Invoke-Dotnet @('run', '--file', $app, '--no-restore') | Out-Null
    if ([IO.File]::GetLastWriteTimeUtc($sourcePath) -ne $sourceTime) { throw 'Cached generation rewrote source.' }
    Set-Content -LiteralPath $example -Value '{"displayName":"Alice"}'
    if ((Invoke-Dotnet @('run', '--file', $app, '--no-restore')).Trim() -ne 'displayName') { throw 'Example change did not invalidate the file-app build cache.' }
    Set-Content -LiteralPath $example -Value '{"displayName":"Bob"}'
    $sameSchemaTime = [IO.File]::GetLastWriteTimeUtc($sourcePath)
    Invoke-Dotnet @('run', '--file', $app, '--no-restore') | Out-Null
    if ([IO.File]::GetLastWriteTimeUtc($sourcePath) -ne $sameSchemaTime) { throw 'Unchanged inferred schema rewrote source.' }
    Set-Content -LiteralPath $example -Value 'invalid'
    Invoke-Dotnet @('build', $app, '--no-restore') -ExpectFailure | Out-Null
    Invoke-Dotnet @('build', $app, '--no-restore', '-t:Compile', '-p:DesignTimeBuild=true', '-p:SkipCompilerExecution=false') -ExpectFailure | Out-Null
    Remove-Item -LiteralPath $example
    Invoke-Dotnet @('run', '--file', $app, '--no-restore') -ExpectFailure | Out-Null
    Write-Output 'Verified packed build task: fresh design-time generation, quoted paths, execution, cache reuse/invalidation, stable output, and error handling.'
} finally {
    $resolved = [IO.Path]::GetFullPath($scratch)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if ([IO.Path]::GetDirectoryName($resolved) -ne $tempRoot -or [IO.Path]::GetFileName($resolved) -notlike 'dottype-build-smoke-*') {
        throw "Refusing to remove unexpected temporary path: $resolved"
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
