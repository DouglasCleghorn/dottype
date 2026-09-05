# dottype

`dottype` generates a small, serialization-oriented C# POCO from JSON, CSV, or Parquet data. It preserves source field names exactly whenever C# permits it. When it must repair a name, the generated property carries `DataMember` and `JsonPropertyName` metadata containing the original spelling.

> dottype is pre-release software. Version 0.1 is intended for evaluation and
> its command-line surface may change before 1.0.

## Install

After the first public release, install the cross-platform .NET tool from NuGet:

```powershell
dotnet tool install --global dottype
```

Or install the self-contained Windows executable through WinGet:

```powershell
winget install DouglasCleghorn.dottype
```

The NuGet tool requires the .NET 10 runtime. The WinGet package is self-contained and is published for Windows x64 and Arm64.

## Usage

```powershell
# stdout (a file dropped onto the executable is passed as this argument)
dottype customers.json

# gzip or Brotli; format and class name come from the inner filename
dottype customers.json.br
dottype customers.csv.gz --output Customer.cs
dottype customers.parquet.gz

# output file
dottype customers.csv --output Customer.cs

# stdin
Get-Content customers.json | dottype - --format json --name Customer
```

Options:

```text
dottype <input> [-o|--output <path>] [-n|--name <type>]
                [--namespace <namespace>] [-f|--format json|csv|parquet]
                [--compression auto|none|gzip|brotli]
```

One outer `.gz` (gzip) or `.br` (Brotli) suffix is recognized, case-insensitively.
For example, `customers.json.br` generates `customers`, not `customers_json`.
`--compression` overrides detection; `gz` and `br` are accepted aliases. The default
`auto` uses the filename and assumes uncompressed stdin. For compressed stdin,
specify both options, for example in Bash or `cmd.exe`:

```text
dottype - --format json --compression br < customers.json.br
```

Use a binary-safe pipe/redirection for compressed data; PowerShell `Get-Content`
text output is unsuitable. Compression does not change format inference, field
names, nullability, or output encoding. ZIP, tar, nested compression wrappers,
and other codecs are not supported. Parquet's internal column compression is
handled independently by the Parquet reader.

JSON objects and arrays of objects are supported, including nested objects and arrays. CSV uses the first record as its header and conservatively infers booleans and numeric types; other values remain strings. Parquet types and nullability come directly from its embedded schema.

The generated code targets C# 10+ and uses framework attributes, with no dependency
on dottype. `JsonPropertyName` supports System.Text.Json; `DataMember(Name=...)`
also records the original name for mapping tools. Other serializers, CSV mappers,
and Parquet writers may require their own configuration. Duplicate CSV headers
are rejected because they cannot be mapped unambiguously.

Inference currently reads complete JSON/CSV input into memory. Compressed or
piped Parquet is also buffered because Parquet requires seeking. Memory usage
therefore follows decompressed size. Run large files with sufficient memory.
When dropping a file from Explorer, stdout may disappear as the console closes;
use a terminal and `--output` to save the generated source.

Exit codes: `0` success, `1` invalid data or I/O failure, `2` invalid usage.
Output files are replaced atomically; existing symlink/hard-link aliases are
replaced rather than writing through to their targets.

## Build and install

The project currently targets .NET 10.

```powershell
dotnet restore dottype.slnx --locked-mode
dotnet test dottype.slnx -c Release --no-restore
dotnet pack src/Dottype/Dottype.csproj -c Release --no-restore -p:RuntimeIdentifiers= -o artifacts/package
./eng/verify-package.ps1

dotnet tool install --global `
  --add-source artifacts/package `
  dottype --version 0.1.0
```

To build the self-contained WinGet executables and manifests:

```powershell
./eng/publish-portables.ps1
./eng/new-winget-manifests.ps1 `
  -Version 0.1.0 `
  -X64Installer artifacts/portable/release/dottype-win-x64.exe `
  -Arm64Installer artifacts/portable/release/dottype-win-arm64.exe
./eng/verify-winget-manifests.ps1
```

See [RELEASING.md](RELEASING.md) for the protected NuGet trusted-publishing and WinGet submission process.

This repository currently ships a CLI tool package, not a referenceable library
API. A separately versioned core library can be added if programmatic generation
is needed.

## License

dottype is licensed under the [MIT License](LICENSE).
