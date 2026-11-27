# Using dottype

dottype infers C# types from example JSON, JSON Lines, CSV, or Parquet data. Use the `dottype` CLI to produce a source file, or the `dottype.Build` NuGet package to generate models for compilation and IntelliSense.

## Getting started

Try the CLI once with the .NET 10 SDK:

```shell
dotnet tool exec dottype@0.3.0 -- customer.json --name Models.Customer
```

Or install the command from NuGet. Build integration requires the .NET **10.0.400 SDK** or later:

```powershell
dotnet tool install --global dottype --version 0.3.0
```

Reference `dottype.Build` version **0.3.0** using the examples below. The CLI needs the .NET 10 runtime once installed. Upgrade it with `dotnet tool update --global dottype --version 0.3.0`. When upgrading from 0.1.0, replace `--name Customer --namespace Models` with `--name Models.Customer`, use lowercase option values, and add `--sample all` to retain full-input inference.

## Same generation options everywhere

Run `dottype --help` or `dottype -h` without an input file for the installed version's help. It lists option aliases, defaults, examples, exit codes, and the build-only cache flag. [llms.txt](llms.txt) is the small documentation index included in both packages.

Options accept separate tokens (`--name Models.Customer`) or an equals sign (`--name=Models.Customer`). Flag names and values are case-sensitive: use lowercase format, compression, and `all` values. Type names retain their supplied casing, and format/compression inference from file extensions remains case-insensitive.

| Argument | Values and default | Where it works |
| --- | --- | --- |
| `--name`, `-n` | Type name, optionally qualified: `Models.Customer` generates `Customer` in `Models`. A plain name uses the global namespace. Defaults to the input filename without format/compression extensions; `Root` for stdin. | All entry points |
| `--format`, `-f` | `json`, `jsonl` (alias `ndjson`), `csv`, or `parquet` (alias `parq`). Defaults to the inner file extension. Required for stdin. | All entry points |
| `--compression` | `auto` (default), `none`, `gzip` (`gz` alias), `brotli` (`br` alias). `auto` uses the suffix; stdin defaults to uncompressed. | All entry points |
| `--sample`, `--sample-records` | Positive record count, or `all`. Defaults to **1000**. | All entry points |
| `--csvhelper-map` | Generate a CsvHelper `ClassMap<T>` named after the root type plus `Map`. Off by default. | CSV, all entry points |
| `--column-type` | `header=type` using the original header and one of `string`, `bool`, `long`, `decimal`, or `double`. Repeat for different headers. Omitted columns are inferred. | CSV, all entry points |
| `--force` | Ignore the metadata cache. Temporarily add, save/build, then remove. | Build package: property, directive, or Visual Studio options |
| `--output`, `-o` | Output source path; omitted or `-` means stdout. | CLI only |
| `--help`, `-h` | Display command-line help. | CLI only |

The CLI always generates, so it rejects `--force`. The build package manages generated output, so it rejects `--output`, `--help`, and stdin.

The last segment of `--name` is the type name; preceding segments must form a valid C# namespace. For example, `Acme.Models.Customer` uses namespace `Acme.Models`. Replace older `--name Customer --namespace Models` settings with `--name Models.Customer`; the separate `--namespace` option has been removed.

Put options before `--` when a filename starts with a dash: `dottype --name Customer -- -sample.json`. Only `--column-type` accepts repeated values (one per occurrence). Response files (`@args.txt`) and bundled short flags are disabled in every entry point. The CLI accepts the `[suggest]` completion directive described below; other directives are unsupported. Build and editor settings reject all command-line directives.

## Read a CSV with CsvHelper

The inventory example reads a spreadsheet export and adds up its `Size bytes` column. From `examples/file-app`, run `dotnet run --file inventory.cs` to get `6144 bytes`. Pass another file after `--` to query a later export with the same model. [C# example](https://github.com/DouglasCleghorn/dottype/blob/HEAD/examples/file-app/inventory.cs) · [Sample CSV](https://github.com/DouglasCleghorn/dottype/blob/HEAD/examples/file-app/inventory.csv)

Use `--csvhelper-map` to generate a map alongside your class. For `--name Models.InventoryRow`, register it with `csv.Context.RegisterClassMap<Models.InventoryRowMap>()` before calling `csv.GetRecords<Models.InventoryRow>()`. The map matches headers by original name, including spaces, punctuation, and names repaired to avoid collisions. Column order can change. Reference CsvHelper in the application using the map (the example uses 33.1.0) and initialize the reader with `CultureInfo.InvariantCulture`, as dottype uses invariant numeric formats.

Use `--column-type "Asset ID=string"` to keep a numeric-looking identifier as text. A pin also works independently of map generation. Header matching is exact and case-sensitive. Quote the entire assignment when the header contains spaces, using single quotes in file-app directives. The final equals sign separates the header and type. Unknown headers, duplicate pins, and values in sampled nonempty cells that cannot be read as the pinned type are errors. Missing and empty cells still make a column nullable.

### Run the example from a checkout

The CSV options require dottype 0.3.0. To try the checked-out build before installing a published version, start in the repository root and run the following in PowerShell. A checkout-specific package cache keeps the sample separate from packages used by other projects.

```powershell
dotnet restore dottype.slnx --locked-mode
dotnet pack src/dottype.Build/dottype.Build.csproj -c Release --no-restore -o artifacts/package
$env:NUGET_PACKAGES = Join-Path $PWD ("artifacts/example-packages-" + [Guid]::NewGuid().ToString("N"))
dotnet restore examples/file-app/inventory.cs --source ./artifacts/package --source https://api.nuget.org/v3/index.json
Set-Location examples/file-app
dotnet run --file inventory.cs --no-restore
```

For the two-model project, run the following from the repository root using the same local feed and package cache.

```powershell
dotnet restore examples/visual-studio/App.csproj --source ./artifacts/package --source https://api.nuget.org/v3/index.json
dotnet run --project examples/visual-studio/App.csproj --no-restore
```

## Read JSON Lines

Each UTF-8 line contains one JSON object. For example, save these two lines as `events.jsonl`.

```jsonl
{"id":1,"event":"started"}
{"id":2,"event":"finished","elapsedMs":30}
```

Run `dottype events.jsonl --name Models.Event -o Event.cs`. The generated type has `long id`, `string @event`, and `long? elapsedMs` because only the second record has that field. Deserialize each line with `JsonSerializer.Deserialize<Models.Event>(line)`.

Both `.jsonl` and `.ndjson` work, including files such as `events.ndjson.gz` and `events.jsonl.br`. For stdin, use `dottype - --format jsonl --name Models.Event < events.jsonl` in Bash or `cmd.exe`. Use `--compression gzip` or `--compression br` as well for compressed stdin.

The default sample ends after 1,000 objects. Use `--sample all` to read to the end, one record at a time. Within the sample, blank lines, arrays, scalars, and malformed records report the one-based line number. LF and CRLF are accepted, as are an initial UTF-8 BOM and a final record without a newline.

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
# jsonl
dottype '[suggest:18]' 'dottype --format js --name Customer'
# json
# jsonl
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
#:package dottype.Build@0.3.0
#:property dottype=./customer.json --name Models.Customer

var customer = new Models.Customer { id = 42, name = "Ada" };
Console.WriteLine(customer.name);
```

Run `dotnet run --file app.cs`. Paths are relative to `app.cs`. Use **single quotes** for paths with spaces: `#:property dottype='./example data/customer.json' --name Customer`. These directives do not accept double quotes.

### Project property

```xml
<ItemGroup>
  <PackageReference Include="dottype.Build" Version="0.3.0" />
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

### Multiple models in one project

Set the Build Action for each example, or put one `<dottype Include="…"/>` item per model in the project file. Give each model a distinct qualified type name. Each item's options and cache are independent.

```xml
<ItemGroup>
  <None Remove="customer.json;orders.ndjson" />
  <dottype Include="customer.json" Options="--name Models.Customer" />
  <dottype Include="orders.ndjson" Options="--name Models.Order --sample all" />
</ItemGroup>
```

With multiple items, each root owns its generated child classes. A customer and an order can both have an `address` field with different shapes. Code that accesses `customer.address.city` works as before. When switching from a single example, update explicit child type references or partial declarations such as `Models.addressType` to `Models.Customer.addressType`. The CLI and single-example projects keep their existing type names.

Use the property or file-app directive for one example. Use item declarations for several. Combining the property and items, or generating the same fully qualified type or CsvHelper map name twice, produces `dottype001`. The sample project reads both files and prints the orders for the customer. [Runnable project](https://github.com/DouglasCleghorn/dottype/tree/HEAD/examples/visual-studio)

## Inference and generated code

- JSON arrays, JSON Lines, and CSV examine the first 1,000 complete records by default, in every entry point. CSV headers do not count. Use `--sample 2000` for a larger sample, or `--sample all` when fields or type variations later in the file matter. `--sample-records` remains an alias. Unsampled data is not validated.
- Standalone JSON objects and sampled array elements are examined completely, including nested arrays. Parquet uses its embedded schema regardless of the sampling setting.
- CSV requires unique headers. Blank physical lines between records are skipped. Empty cells, including quoted empty fields, count as missing values for nullability. Numeric inference preserves leading zeroes, surrounding whitespace, and nonfinite numbers as text.
- JSON, JSON Lines, and CSV files can use one outer `.gz` or `.br` wrapper, detected automatically. Parquet files use internal compression and keep the `.parquet` extension. ZIP, tar, and nested compression wrappers are unsupported. Buffering/decompression and editor change tracking may still read more than the inference sample.
- Generated code targets C# 10+, includes types for nested objects, and has no runtime dependency on dottype. Field-name repairs preserve original names through `JsonPropertyName` and `DataMember` attributes. The optional CsvHelper map requires CsvHelper in the consuming project. Other serializers or mappers may need configuration.

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
