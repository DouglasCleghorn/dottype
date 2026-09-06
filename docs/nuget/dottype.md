![dottype](https://raw.githubusercontent.com/DouglasCleghorn/dottype/HEAD/assets/icon.png)

# dottype

**Turn example data into C# types.** Generate source from JSON, CSV, or Parquet files with a single command.

This package installs the `dottype` command-line tool. For generation during builds and IntelliSense, use [dottype.Build](https://www.nuget.org/packages/dottype.Build).

## Install

Requires the **.NET 10 runtime** and a .NET SDK to install the tool.

```shell
dotnet tool install --global dottype
```

To update an existing installation:

```shell
dotnet tool update --global dottype
```

## Generate your first type

Save this example as `customer.json`:

```json
{"id":42,"name":"Ada"}
```

Generate a `Customer` type in the `Models` namespace:

```shell
dottype customer.json --name Models.Customer --output Customer.cs
```

The generated type has `long id` and `string name` properties. Add `Customer.cs` to your application and use it directly:

```csharp
var customer = new Models.Customer { id = 42, name = "Ada" };
Console.WriteLine(customer.name);
```

Omit `--output` to print source to standard output. Run `dottype --help` for the installed version's options.

## What it handles

- JSON, CSV with headers, and Parquet, including nested types and inferred nullability.
- gzip (`.gz`) and Brotli (`.br`) wrappers for JSON and CSV, detected from the filename. Parquet files use internal compression and keep the `.parquet` extension.
- Source field names, with mapping attributes when a name needs repair for C#.
- File or standard input, and file or standard output.
- C# 10+ generated code with no runtime dependency on dottype.

JSON and CSV inference examines the first **1,000 records** by default. Use `--sample all` to include later fields and type changes. Parquet uses its embedded schema.

```shell
dottype customers.json.gz --name Models.Customer --sample all --output Customer.cs
```

## Documentation and support

- [Usage reference: all arguments, formats, and inference rules](https://github.com/DouglasCleghorn/dottype/blob/HEAD/USAGE.md)
- [Build and editor integration](https://www.nuget.org/packages/dottype.Build)
- [Release history and upgrade notes](https://github.com/DouglasCleghorn/dottype/blob/HEAD/CHANGELOG.md)
- [Report a bug or request a feature](https://github.com/DouglasCleghorn/dottype/issues)
- [AI agent documentation index](https://github.com/DouglasCleghorn/dottype/blob/HEAD/llms.txt)
- [MIT license](https://github.com/DouglasCleghorn/dottype/blob/HEAD/LICENSE)
