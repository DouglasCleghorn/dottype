# Using dottype

dottype infers C# types from example JSON, CSV, or Parquet data. Choose the `dottype` CLI to produce a source file, or the `dottype.Build` NuGet package to generate types for compilation and IntelliSense. There is no public API for calling inference from application code.

## Getting started

Install from NuGet. Build integration requires the .NET **10.0.400 SDK** or later:

```powershell
dotnet tool install --global dottype --version 0.2.0
```

Reference `dottype.Build` version **0.2.0** using the examples below. The CLI needs the .NET 10 runtime once installed. Upgrade it with `dotnet tool update --global dottype --version 0.2.0`. When upgrading from 0.1.0, replace `--name Customer --namespace Models` with `--name Models.Customer`, use lowercase option values, and add `--sample all` to retain full-input inference.

## Same generation options everywhere

Run `dottype --help` or `dottype -h` without an input file for the installed version's help. It lists option aliases, defaults, examples, exit codes, and the build-only cache flag. [llms.txt](llms.txt) is the small documentation index included in both packages.

Options accept separate tokens (`--name Models.Customer`) or an equals sign (`--name=Models.Customer`). Flag names and values are case-sensitive: use lowercase format, compression, and `all` values. Type names retain their supplied casing, and format/compression inference from file extensions remains case-insensitive.

| Argument | Values and default | Where it works |
| --- | --- | --- |
| `--name`, `-n` | Type name, optionally qualified: `Models.Customer` generates `Customer` in `Models`. A plain name uses the global namespace. Defaults to the input filename without format/compression extensions; `Root` for stdin. | All entry points |
| `--format`, `-f` | `json`, `csv`, `parquet` (`parq` alias). Inferred from the inner file extension; required for stdin. | All entry points |
| `--compression` | `auto` (default), `none`, `gzip` (`gz` alias), `brotli` (`br` alias). `auto` uses the suffix; stdin defaults to uncompressed. | All entry points |
| `--sample`, `--sample-records` | Positive record count, or `all`. Defaults to **1000**. | All entry points |
| `--force` | Ignore the metadata cache. Temporarily add, save/build, then remove. | Build package: property, directive, or Visual Studio options |
| `--output`, `-o` | Output source path; omitted or `-` means stdout. | CLI only |
| `--help`, `-h` | Display command-line help. | CLI only |

The CLI always generates, so it rejects `--force`. The build package manages generated output, so it rejects `--output`, `--help`, and stdin.

The last segment of `--name` is the type name; preceding segments must form a valid C# namespace. For example, `Acme.Models.Customer` uses namespace `Acme.Models`. Replace older `--name Customer --namespace Models` settings with `--name Models.Customer`; the separate `--namespace` option has been removed.

Put options before `--` when a filename starts with a dash: `dottype --name Customer -- -sample.json`. Specify each value option once. Response files (`@args.txt`) and bundled short flags are disabled in every entry point. The CLI accepts the `[suggest]` completion directive described below; other directives are unsupported. Build and editor settings reject all command-line directives.

## CLI tab completion

The CLI suggests option names and aliases, format and compression values (including aliases), `all` and `1000` for sampling, and files/directories for the input and `--output`. It also suggests `-` for stdin/stdout. `--sample` still accepts any positive record count. Completion matches prefixes case-insensitively and suggests the accepted lowercase values. The build-only `--force` flag is hidden from CLI completion.

To enable Tab in your shell, install `dotnet-suggest` and register the installed CLI. In PowerShell:

```powershell
dotnet tool install --global dotnet-suggest
dotnet-suggest register --command-path (Get-Command dottype -CommandType Application).Source
```

Add the appropriate `dotnet-suggest` shim to your shell profile and restart the shell, following the [System.CommandLine shell setup instructions](https://learn.microsoft.com/en-us/dotnet/standard/commandline/how-to-enable-tab-completion). They cover PowerShell, Bash, and Zsh. Then try `dottype --format ` followed by Tab, or `dottype --compression g` followed by Tab. Shell setup is separate from installing dottype.

For shell integrations or a direct check, pass a complete command line as one quoted argument:

```powershell
dottype '[suggest]' 'dottype --format j'
# json
dottype '[suggest:18]' 'dottype --format js --name Customer'
# json (cursor immediately after js)
```

`[suggest]` defaults to the end of the supplied line; `[suggest:N]` uses a zero-based character position within it. Suggestions are written one per line, without reading example data or generating output. Quoted paths and `--option=value` are supported; shell integrations are responsible for quoting inserted paths and retaining an option's `--option=` prefix when replacing that whole word. No completion directive belongs in build or editor settings.

## Equivalent examples

Given `customer.json` containing `{"id":42,"name":"Ada"}`, these all generate a `Models.Customer` type with `long id` and `string name` properties.

### Command line

```powershell
dottype customer.json --name Models.Customer --output Customer.cs
```

For Parquet input, use a `.parquet` file with its internal compression:

```powershell
dottype customers.parquet --name Models.Customer --output Customer.cs
```

`--compression` controls an outer file wrapper, not Parquet's internal compression; leave it at the default for `.parquet` files.

Paths are relative to the current directory. The shell handles quoting; quote paths with spaces. To read text from stdin in PowerShell:

```powershell
Get-Content customer.json | dottype - --format json --name Models.Customer
```

Use binary-safe redirection for compressed or Parquet stdin, not `Get-Content` text output.

### Single-file C# app

```csharp
#:package dottype.Build@0.2.0
#:property dottype=./customer.json --name Models.Customer

var customer = new Models.Customer { id = 42, name = "Ada" };
Console.WriteLine(customer.name);
```

Run `dotnet run --file app.cs`. Paths are relative to `app.cs`. Use **single quotes** for paths with spaces: `#:property dottype='./example data/customer.json' --name Customer`. These directives do not accept double quotes.

### Project property

```xml
<ItemGroup>
  <PackageReference Include="dottype.Build" Version="0.2.0" />
</ItemGroup>
<PropertyGroup>
  <dottype>./customer.json --name Models.Customer</dottype>
</PropertyGroup>
```

Paths are relative to the project directory. Quote paths with spaces. This property is an argument string, not a shell command.

### Visual Studio file Properties

Install `dottype.Build`, select `customer.json` **in Solution Explorer**, press **F4**, and set **Build Action** to **dottype**. Enter only the flags in **dottype options**; the selected file supplies the input path:

```text
--name Models.Customer
```

Visual Studio persists the same configuration as item metadata:

```xml
<ItemGroup>
  <None Remove="customer.json" />
  <dottype Include="customer.json" Options="--name Models.Customer" />
</ItemGroup>
```

Use one example per project, configured through either the property or the file item. Selecting multiple examples or combining the two styles is an error.

## Inference and generated code

- JSON arrays and CSV examine the first 1,000 complete data records by default, in every entry point. CSV headers do not count. Use `--sample 2000` for a larger sample, or `--sample all` when fields or type variations later in the file matter. `--sample-records` remains an alias. Unsampled data is not validated.
- Standalone JSON objects and sampled array elements are examined completely, including nested arrays. Parquet uses its embedded schema regardless of the sampling setting.
- CSV requires a header and rejects duplicate headers. Boolean and numeric types are inferred conservatively; other values remain strings.
- JSON and CSV files can use one outer `.gz` or `.br` wrapper, detected automatically. Parquet files use internal compression and keep the `.parquet` extension. ZIP, tar, and nested compression wrappers are unsupported. Buffering/decompression and editor change tracking may still read more than the inference sample.
- Generated code targets C# 10+, includes nested types, and has no runtime dependency on dottype. Field-name repairs preserve original names through `JsonPropertyName` and `DataMember` attributes. Other serializers or mappers may need configuration.

## IntelliSense, refresh, and errors

### Visual Studio

Build integration generates before compilation. Visual Studio **2026** receives generated types during design-time builds after restore, before the first run. Save and **Build** after changing an example; immediate refresh on every save is not guaranteed.

No Visual Studio extension is required. Change Build Action back to **None** to stop generation.

### VS Code

Use the C# extension with C# Dev Kit. The tested combination is C# **2.151.28 (pre-release)** and C# Dev Kit **3.20.199**; C# **2.140.9** is incompatible with this SDK's file-app integration. Types are available after restore, before the first run.

For automatic refresh after saving example data, add this setting and restart the C# extension:

```json
"dotnet.server.sourceGeneratorExecution": "Automatic"
```

Alternatively, run **C#: Rerun Source Generators**. Inference uses the saved file on disk.

### Cache and troubleshooting

The build cache uses input modification time, size, settings, and generator identity. Use `--force` when replacing content with identical metadata. Generated files stay under the intermediate output directory; do not edit them. `--no-build` skips refresh checks.

Invalid build configuration, missing examples, or invalid sampled data report `dottype001`. The CLI exits with `0` for success, `1` for invalid data/I/O failure, or `2` for invalid usage.

For compressed stdin, specify format and compression and use binary-safe redirection, for example in Bash or `cmd.exe`:

```text
dottype - --format json --compression br < customer.json.br
```

If the Visual Studio options field is missing, select the data file in Solution Explorer and reload the project after restoring the package. During local development, rebuilding the same package version does **not** replace an already restored copy. Use distinct preview package versions, or refresh only that package's cache and restore again.
