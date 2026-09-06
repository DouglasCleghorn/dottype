param(
    [Parameter(Mandatory = $true)][string] $CodeExecutable,
    [Parameter(Mandatory = $true)][string] $ExtensionsDirectory,
    [string] $PackageDirectory = "$PSScriptRoot/../artifacts/package",
    [ValidateSet('json', 'csv', 'parquet')][string] $Format = 'json',
    [switch] $Wait
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath("$PSScriptRoot/..")
$scratch = Join-Path $repo ('artifacts/editor-test-' + [Guid]::NewGuid().ToString('N'))
$appDirectory = Join-Path $scratch 'app'
$userData = Join-Path $scratch 'user-data'
New-Item -ItemType Directory -Path $appDirectory, (Join-Path $userData 'User') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $appDirectory 'Directory.Build.props') -Value '<Project />'
Set-Content -LiteralPath (Join-Path $appDirectory 'Directory.Packages.props') -Value '<Project />'
$feed = [Security.SecurityElement]::Escape([IO.Path]::GetFullPath($PackageDirectory))
Set-Content -LiteralPath (Join-Path $appDirectory 'NuGet.Config') -Value "<configuration><packageSources><clear /><add key='local' value='$feed' /><add key='nuget.org' value='https://api.nuget.org/v3/index.json' /></packageSources><config><add key='globalPackagesFolder' value='../packages' /></config></configuration>"
if ($Format -eq 'parquet') {
    & dotnet run --file "$PSScriptRoot/create-editor-fixtures.cs" -- $appDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Could not create Parquet editor fixtures.' }
} elseif ($Format -eq 'csv') {
    Set-Content -LiteralPath (Join-Path $appDirectory 'sample.csv') -Value "id`n1"
    Set-Content -LiteralPath (Join-Path $appDirectory 'changed.csv') -Value "displayName`nAlice"
} else {
    Set-Content -LiteralPath (Join-Path $appDirectory 'sample.json') -Value '{"id":1}'
    Set-Content -LiteralPath (Join-Path $appDirectory 'changed.json') -Value '{"displayName":"Alice"}'
}
$appSource = @'
#:package dottype.Build@0.2.0
#:property dottype=./sample.FORMAT --name Customer

var customer = new Customer();
Console.WriteLine(customer.);
'@
Set-Content -LiteralPath (Join-Path $appDirectory 'app.cs') -Value $appSource.Replace('sample.FORMAT', "sample.$Format")
Set-Content -LiteralPath (Join-Path $userData 'User/settings.json') -Value '{"security.workspace.trust.enabled":false,"extensions.autoUpdate":false,"extensions.autoCheckUpdates":false,"update.mode":"none","telemetry.telemetryLevel":"off","dotnet.fileBasedApps.enableAutomaticDiscovery":true,"dotnet.server.sourceGeneratorExecution":"Automatic"}'
$env:dottype_EDITOR_TEST_DIR = $appDirectory
$env:dottype_EDITOR_TEST_RESULT = Join-Path $scratch 'result.json'
$env:dottype_EDITOR_TEST_FORMAT = $Format
$arguments = @('--user-data-dir', "`"$userData`"", '--extensions-dir', "`"$ExtensionsDirectory`"",
    '--extensionDevelopmentPath', "`"$repo/tests/vscode`"", '--extensionTestsPath', "`"$repo/tests/vscode/index.cjs`"",
    '--skip-welcome', '--skip-release-notes', '--disable-workspace-trust', '--disable-gpu', "`"$appDirectory`"")
# Disable unrelated installed extensions in this isolated test window only.
foreach ($manifest in Get-ChildItem -LiteralPath $ExtensionsDirectory -Filter package.json -Recurse -Depth 1) {
    $metadata = Get-Content -LiteralPath $manifest.FullName -Raw | ConvertFrom-Json
    $id = "$($metadata.publisher).$($metadata.name)"
    if ($id -notin @('ms-dotnettools.csharp', 'ms-dotnettools.csdevkit', 'ms-dotnettools.vscode-dotnet-runtime')) {
        $arguments += @('--disable-extension', $id)
    }
}
$process = Start-Process -FilePath $CodeExecutable -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "Editor test PID: $($process.Id)"
Write-Output "Results: $env:dottype_EDITOR_TEST_RESULT"
Write-Output "Logs: $userData/logs"
if ($Wait) {
    if (-not $process.WaitForExit(180000)) { $process.Kill($true); throw 'Editor test timed out.' }
    if (-not (Test-Path -LiteralPath $env:dottype_EDITOR_TEST_RESULT)) { throw 'Editor test did not produce a result.' }
    $result = Get-Content -LiteralPath $env:dottype_EDITOR_TEST_RESULT -Raw | ConvertFrom-Json
    if (-not ($result.beforeFirstRun -and $result.refresh -and $result.invalidInput -and $result.recovery)) {
        throw "Editor test failed: $($result | ConvertTo-Json -Depth 5)"
    }
    Write-Output "Verified $Format IntelliSense before first run, refresh, diagnostics, and recovery."
}
