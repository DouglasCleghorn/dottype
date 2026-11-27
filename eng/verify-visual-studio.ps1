param(
    [string] $PackageDirectory = "$PSScriptRoot/../artifacts/package",
    [string] $MSBuildPath,
    [string] $ExpectedVersion = '0.3.0'
)
$ErrorActionPreference = 'Stop'
$scratch = [IO.Directory]::CreateTempSubdirectory('dottype-vs-smoke-').FullName
$feed = [Security.SecurityElement]::Escape([IO.Path]::GetFullPath($PackageDirectory))
function Invoke-Build([string[]] $Arguments, [switch] $ExpectFailure) {
    $output = if ($MSBuildPath) { & $MSBuildPath @Arguments -nr:false 2>&1 } else { & dotnet msbuild @Arguments -nr:false 2>&1 }
    $output = ($output | ForEach-Object { "$_" }) -join "`n"
    if ($ExpectFailure) {
        if ($LASTEXITCODE -eq 0 -or $output -notmatch 'dottype001') { throw "Expected dottype001: $output" }
        # Do not propagate the expected failure to the CI PowerShell wrapper.
        $global:LASTEXITCODE = 0
    } elseif ($LASTEXITCODE -ne 0) { throw "MSBuild failed: $output" }
    return $output
}
try {
    Set-Content -LiteralPath (Join-Path $scratch 'Directory.Build.props') -Value '<Project><PropertyGroup><RestorePackagesPath>$(MSBuildThisFileDirectory)packages</RestorePackagesPath></PropertyGroup></Project>'
    Set-Content -LiteralPath (Join-Path $scratch 'Directory.Packages.props') -Value '<Project />'
    Set-Content -LiteralPath (Join-Path $scratch 'NuGet.Config') -Value "<configuration><packageSources><clear /><add key='local' value='$feed' /><add key='nuget.org' value='https://api.nuget.org/v3/index.json' /></packageSources><packageSourceMapping><packageSource key='local'><package pattern='dottype.Build' /></packageSource><packageSource key='nuget.org'><package pattern='*' /></packageSource></packageSourceMapping></configuration>"
    $project = Join-Path $scratch 'App.csproj'
    $example = Join-Path $scratch "customer's sample.json"
    $projectXml = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="dottype.Build" Version="__VERSION__" />
    <None Remove="customer's sample.json" />
    <dottype Include="customer's sample.json" Options="--name Models.Customer" />
  </ItemGroup>
</Project>
'@
    $projectXml = $projectXml.Replace('__VERSION__', $ExpectedVersion)
    Set-Content -LiteralPath $project -Value $projectXml
    Set-Content -LiteralPath $example -Value '{"id":1}'
    Set-Content -LiteralPath (Join-Path $scratch 'Use.cs') -Value 'public class Use { public long Id => new Models.Customer().id; }'
    Invoke-Build @($project, '-restore', '-t:Restore', '-nologo') | Out-Null
    $designArgs = @($project, '-t:Compile', '-p:DesignTimeBuild=true', '-p:BuildingInsideVisualStudio=true',
        '-p:SkipCompilerExecution=true', '-p:ProvideCommandLineArgs=true', '-nologo',
        '-getItem:Compile,Analyzer,AvailableItemName,UpToDateCheckInput,PropertyPageSchema', '-getProperty:dottype')
    $design = Invoke-Build $designArgs | ConvertFrom-Json
    if (@($design.Items.AvailableItemName | Where-Object Identity -eq 'dottype').Count -ne 1) { throw 'Build Action dottype is missing.' }
    $schemas = @($design.Items.PropertyPageSchema | Where-Object Identity -match '[/\\]dottype\.xaml$')
    if ($schemas.Count -ne 1 -or $schemas[0].Context -ne 'File;BrowseObject') { throw 'File Properties schema is not registered.' }
    [xml] $rule = Get-Content -Raw -LiteralPath $schemas[0].FullPath
    if ($rule.Rule.'Rule.DataSource'.DataSource.ItemType -ne 'dottype' -or
        $rule.Rule.'Rule.DataSource'.DataSource.Persistence -ne 'ProjectFile' -or
        @($rule.Rule.StringProperty | Where-Object Name -eq 'Options').Count -ne 1) { throw 'Options field does not persist to the selected example.' }
    if (@($design.Items.UpToDateCheckInput | Where-Object FullPath -eq $example).Count -lt 1) { throw 'Visual Studio does not track the input.' }
    if (@($design.Items.Analyzer | Where-Object Identity -match 'dottype.Generator.dll$').Count -ne 0) { throw 'Visual Studio still loads the .NET-only editor generator.' }
    $generated = @($design.Items.Compile | Where-Object Identity -match 'dottype[/\\].*\.g\.cs$')
    if ($generated.Count -ne 1) { throw 'Design-time build did not supply exactly one generated source.' }
    $source = $generated[0].FullPath
    if ((Get-Content -Raw -LiteralPath $source) -notmatch 'long id') { throw 'Type was not generated before the first build.' }
    $timestamp = [IO.File]::GetLastWriteTimeUtc($source)
    Invoke-Build $designArgs | Out-Null
    if ([IO.File]::GetLastWriteTimeUtc($source) -ne $timestamp) { throw 'Cached design-time build rewrote source.' }
    Invoke-Build @($project, '-t:Build', '-p:BuildingInsideVisualStudio=true', '-nologo') | Out-Null
    # Changing the metadata saved by the Properties window must invalidate cached types.
    Set-Content -LiteralPath $project -Value $projectXml.Replace('--name Models.Customer', '--name Changed.Renamed --sample all')
    $changedDesign = Invoke-Build $designArgs | ConvertFrom-Json
    $changedFile = @($changedDesign.Items.Compile | Where-Object Identity -match 'dottype[/\\].*\.g\.cs$')[0].FullPath
    $configuredSource = Get-Content -Raw -LiteralPath $changedFile
    if ($configuredSource -notmatch 'class Renamed' -or $configuredSource -notmatch 'namespace Changed') { throw 'Updated Options did not regenerate the configured type.' }
    Set-Content -LiteralPath $project -Value $projectXml
    Set-Content -LiteralPath $example -Value '{"displayName":"Ada"}'
    Set-Content -LiteralPath (Join-Path $scratch 'Use.cs') -Value 'public class Use { public string? Name => new Models.Customer().displayName; }'
    Invoke-Build $designArgs | Out-Null
    if ((Get-Content -Raw -LiteralPath $source) -notmatch 'string displayName') { throw 'Design-time generation did not refresh.' }
    Invoke-Build @($project, '-t:Build', '-p:BuildingInsideVisualStudio=true', '-nologo') | Out-Null
    Set-Content -LiteralPath $example -Value 'invalid'
    Invoke-Build $designArgs -ExpectFailure | Out-Null
    Invoke-Build @($project, '-t:Build', '-p:BuildingInsideVisualStudio=true', '-nologo') -ExpectFailure | Out-Null
    Set-Content -LiteralPath $example -Value '{"displayName":"Ada"}'
    Invoke-Build $designArgs | Out-Null
    # Two files with the same nested field must have independently usable types in both editor paths.
    $orders = Join-Path $scratch 'orders.ndjson'
    Set-Content -LiteralPath $example -Value '{"address":{"city":"Denver"}}'
    Set-Content -LiteralPath $orders -Value '{"address":{"postal":"00501"}}'
    $multi = $projectXml.Replace('</ItemGroup>', '<dottype Include="orders.ndjson" Options="--name Models.Order" /></ItemGroup>')
    Set-Content -LiteralPath $project -Value $multi
    Set-Content -LiteralPath (Join-Path $scratch 'Use.cs') -Value 'public class Use { public string City(Models.Customer.addressType address) => address.city; public string Postal(Models.Order.addressType address) => address.postal; }'
    $two = Invoke-Build $designArgs | ConvertFrom-Json
    if (@($two.Items.Compile | Where-Object Identity -match 'dottype[/\\].*\.g\.cs$').Count -ne 2) { throw 'Two models must supply two sources.' }
    Invoke-Build @($project, '-t:Build', '-p:BuildingInsideVisualStudio=true', '-nologo') | Out-Null
    Invoke-Build @($project, '-t:Rebuild', '-p:DesignTimeBuild=true', '-p:SkipCompilerExecution=false', '-nologo') | Out-Null
    Set-Content -LiteralPath $orders -Value @('{}', 'invalid')
    $partial = Invoke-Build @($project, '-t:Compile', '-p:DesignTimeBuild=true', '-p:BuildingInsideVisualStudio=true',
        '-p:SkipCompilerExecution=true', '-p:ProvideCommandLineArgs=true', '-getItem:CscCommandLineArgs', '-nologo') -ExpectFailure
    if (!$partial.Contains([IO.Path]::GetFileName($source)) -or $partial -notmatch 'line 2') {
        throw 'An invalid order must report its line and preserve the customer model in the editor compilation.'
    }
    Set-Content -LiteralPath $orders -Value '{"address":{"postal":"00501"}}'
    # Characters special to .editorconfig must survive transit to the editor.
    Set-Content -LiteralPath (Join-Path $scratch 'items.csv') -Value @('ID#code;v1', '42')
    $csvItem = '<dottype Include="items.csv" Options="--name Models.Import --column-type ''ID#code;v1=string''" />'
    Set-Content -LiteralPath $project -Value $multi.Replace('</ItemGroup>', "$csvItem</ItemGroup>")
    Set-Content -LiteralPath (Join-Path $scratch 'Use.cs') -Value 'public class Use { public string Id(Models.Import row) => row.ID_code_v1; }'
    Invoke-Build @($project, '-t:Rebuild', '-p:DesignTimeBuild=true', '-p:SkipCompilerExecution=false', '-nologo') | Out-Null
    $normal = Invoke-Build @($project, '-t:Build', '-getItem:Analyzer', '-nologo') | ConvertFrom-Json
    if (@($normal.Items.Analyzer | Where-Object Identity -match 'dottype\.build[/\\].*[/\\]analyzers[/\\]').Count -gt 0) {
        throw 'A regular build loaded the editor generator or its dependencies.'
    }
    # Going back to the single example removes the other models and restores its original child names.
    Set-Content -LiteralPath $project -Value $projectXml
    Set-Content -LiteralPath (Join-Path $scratch 'Use.cs') -Value 'public class Use { public string City(Models.addressType address) => address.city; }'
    $single = Invoke-Build $designArgs | ConvertFrom-Json
    if (@($single.Items.Compile | Where-Object Identity -match 'dottype[/\\].*\.g\.cs$').Count -ne 1) { throw 'Removing project items left stale models.' }
    Invoke-Build @($project, '-t:Build', '-nologo') | Out-Null
    Set-Content -LiteralPath $project -Value $multi.Replace('--name Models.Order', '--name Models.Customer')
    Invoke-Build $designArgs -ExpectFailure | Out-Null
    Set-Content -LiteralPath $project -Value $projectXml.Replace('</PropertyGroup>', '<dottype>another.json</dottype></PropertyGroup>')
    Invoke-Build $designArgs -ExpectFailure | Out-Null
    # Changing Build Action back to None must remove the generated type.
    Set-Content -LiteralPath $project -Value $projectXml.Replace('<dottype Include="customer''s sample.json" Options="--name Models.Customer" />', '')
    $removed = Invoke-Build $designArgs | ConvertFrom-Json
    if (@($removed.Items.Compile | Where-Object Identity -match 'dottype[/\\].*\.g\.cs$').Count -ne 0) { throw 'Removing Build Action left a generated type in compilation.' }
    Write-Output 'Verified project items, single and multiple models, design-time compilation, per-file options, cache refresh, diagnostics, and removal.'
} finally {
    $resolved = [IO.Path]::GetFullPath($scratch)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if ([IO.Path]::GetDirectoryName($resolved) -ne $tempRoot -or [IO.Path]::GetFileName($resolved) -notlike 'dottype-vs-smoke-*') {
        throw "Refusing to remove unexpected temporary path: $resolved"
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
