![dottype](assets/icon.png)

# dottype

Generate C# types from CSV, JSON, JSON Lines, or Parquet. Query spreadsheet exports with CsvHelper, deserialize API responses, or read logs one record at a time. Generate a source file with the CLI or keep examples in your project for build-time generation and IntelliSense.

This guide covers 0.3.0. To run this checkout before release, use the [local package instructions](USAGE.md#run-the-example-from-a-checkout). For installations of 0.2.0, use the [0.2.0 usage reference](https://github.com/DouglasCleghorn/dottype/blob/v0.2.0/USAGE.md).

## Read a CSV and calculate a total

Save these two inventory records in a file named `inventory.csv`.

```csv
File path,Size bytes,Asset ID
/data/app.log,4096,42
/data/notes.txt,2048,81
```

With the .NET 10.0.400 SDK or later, you can query those records directly from a C# file. Save the following as `inventory.cs` in the same directory.

```csharp
#:package dottype.Build@0.3.0
#:package CsvHelper@33.1.0
#:property dottype=inventory.csv --name InventoryRow --csvhelper-map --column-type 'Asset ID=string'

using System.Globalization;
using CsvHelper;

using var input = File.OpenText(args.Length == 0 ? "inventory.csv" : args[0]);
using var csv = new CsvReader(input, CultureInfo.InvariantCulture);
csv.Context.RegisterClassMap<InventoryRowMap>();
long bytes = csv.GetRecords<InventoryRow>().Sum(row => row.Size_bytes);
Console.WriteLine($"{bytes} bytes");
```

Run `dotnet run --file inventory.cs` from that directory. It prints **6144 bytes**. To read the next export with the same model, run `dotnet run --file inventory.cs -- next-inventory.csv`. The map matches the original headers even if they move. The type override keeps an ID such as `00501` as text.

[Example app](examples/file-app/inventory.cs) · [Sample CSV](examples/file-app/inventory.csv) · [Run from a source checkout](USAGE.md#run-the-example-from-a-checkout)

## Generate a source file

To keep the generated code in your project, run the tool once from the directory containing your data.

```shell
dotnet tool exec dottype@0.3.0 -- inventory.csv --name InventoryRow --csvhelper-map --column-type "Asset ID=string" -o InventoryRow.cs
```

Add the generated source and CsvHelper to a console project to use the same query. The map is optional. By default dottype emits C# 10+ classes with no dependency on CsvHelper or dottype.

```shell
dotnet tool install --global dottype --version 0.3.0
dottype customer.json --name Models.Customer --output Customer.cs
dottype events.jsonl.gz --sample 2000 --name Models.Event --output Event.cs
dottype inventory.parquet --name InventoryRow
```

The installed CLI requires the .NET 10 runtime. Omit `--output` to print source. CSV, JSON arrays, and JSON Lines use the first 1,000 records by default. Use `--sample all` to include later fields or type changes. Parquet uses its embedded schema.

## Generate several models in one project

Reference `dottype.Build` and add one project item per model. Each file can use its own format, type name, and sample size.

```xml
<ItemGroup>
  <dottype Include="customer.json" Options="--name Models.Customer" />
  <dottype Include="orders.ndjson" Options="--name Models.Order" />
</ItemGroup>
```

The example project uses both types to print a customer's city and the postal code for each order. In Visual Studio 2026, set each file's **Build Action** to **dottype** and enter its flags in **dottype options**. Build after saving an example to refresh its type. [Runnable project](examples/visual-studio) · [Project setup](USAGE.md#multiple-models-in-one-project)

[Usage reference](USAGE.md) · [Visual Studio example](examples/visual-studio/App.csproj) · [CLI on NuGet](https://www.nuget.org/packages/dottype) · [Build package on NuGet](https://www.nuget.org/packages/dottype.Build) · [Agent index](llms.txt) · [MIT license](LICENSE)
