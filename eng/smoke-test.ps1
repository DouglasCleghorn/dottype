param(
    [Parameter(Mandatory = $true)]
    [string] $Executable,

    [string[]] $PrefixArguments = @()
)

$ErrorActionPreference = 'Stop'
$scratch = [System.IO.Directory]::CreateTempSubdirectory('dottype-smoke-').FullName
$payload = [System.Text.Encoding]::UTF8.GetBytes('{"id":1,"display-name":"Alice"}')

function Invoke-dottype([string[]] $CliArguments, [byte[]] $StandardInput) {
    $start = [System.Diagnostics.ProcessStartInfo]::new($Executable)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in @($PrefixArguments) + @($CliArguments)) {
        $start.ArgumentList.Add($argument)
    }
    $process = [System.Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if ($StandardInput) { $process.StandardInput.BaseStream.Write($StandardInput) }
        $process.StandardInput.Close()
        if (-not $process.WaitForExit(30000)) {
            $process.Kill($true)
            throw 'dottype smoke test timed out.'
        }
        $result = $stdout.GetAwaiter().GetResult()
        $errorText = $stderr.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0 -or $errorText) {
            throw "dottype failed ($($process.ExitCode)): $errorText"
        }
        if ($result -notmatch 'public long id' -or $result -notmatch 'JsonPropertyName\("display-name"\)') {
            throw "Unexpected generated source: $result"
        }
        return $result
    }
    finally { $process.Dispose() }
}

try {
    foreach ($compression in @('none', 'gz', 'br')) {
        $path = Join-Path $scratch 'customers.json'
        $bytes = $payload
        if ($compression -ne 'none') {
            $path += ".$compression"
            $buffer = [System.IO.MemoryStream]::new()
            try {
                $encoder = if ($compression -eq 'gz') {
                    [System.IO.Compression.GZipStream]::new($buffer, [System.IO.Compression.CompressionLevel]::Optimal, $true)
                } else {
                    [System.IO.Compression.BrotliStream]::new($buffer, [System.IO.Compression.CompressionLevel]::Optimal, $true)
                }
                try { $encoder.Write($payload) } finally { $encoder.Dispose() }
                $bytes = $buffer.ToArray()
            }
            finally { $buffer.Dispose() }
        }
        [System.IO.File]::WriteAllBytes($path, $bytes)
        $source = Invoke-dottype -CliArguments @($path)
        if ($source -notmatch 'public partial class customers\s') { throw 'Filename inference failed.' }
        $source = Invoke-dottype -CliArguments @('-', '--format', 'json', '--compression', $compression) -StandardInput $bytes
        if ($source -notmatch 'public partial class Root\s') { throw 'Stdin name inference failed.' }
    }
    Write-Host 'Verified CLI file and binary-stdin generation for plain, gzip, and Brotli JSON.'
}
finally {
    # Only remove the exact fresh temporary directory created by this script.
    $resolved = [System.IO.Path]::GetFullPath($scratch)
    $tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    if ([System.IO.Path]::GetDirectoryName($resolved) -ne $tempRoot -or
        -not [System.IO.Path]::GetFileName($resolved).StartsWith('dottype-smoke-', [System.StringComparison]::Ordinal)) {
        throw "Refusing to clean unexpected temporary directory '$resolved'."
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
