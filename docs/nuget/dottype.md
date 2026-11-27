![dottype](https://raw.githubusercontent.com/DouglasCleghorn/dottype/HEAD/assets/icon.png)

# dottype

Generate C# source from CSV, JSON, JSON Lines, or Parquet. Use the generated types to read a spreadsheet export, deserialize an API response, or query a log with LINQ.

## Import a spreadsheet export

Create a console project with `dotnet new console` and save the following as `inventory.csv`.

```csv
File path,Size bytes,Asset ID
/data/app.log,4096,42
/data/notes.txt,2048,81
```

Use the .NET 10 SDK to generate the model and add the CSV reader to your application.

```shell
dotnet tool exec dottype@0.3.0 -- inventory.csv --name InventoryRow --csvhelper-map --column-type "Asset ID=string" -o InventoryRow.cs
dotnet add package CsvHelper --version 33.1.0
```

Replace `Program.cs` with the following and run `dotnet run`.

```csharp
using System.Globalization;
using CsvHelper;

using var input = File.OpenText("inventory.csv");
using var csv = new CsvReader(input, CultureInfo.InvariantCulture);
csv.Context.RegisterClassMap<InventoryRowMap>();
Console.WriteLine($"{csv.GetRecords<InventoryRow>().Sum(row => row.Size_bytes)} bytes");
```

The result is **6144 bytes**. The map binds `Size_bytes` to the original `Size bytes` header, including when columns change order. The type override keeps asset IDs as strings. For a build-time type with IntelliSense, see [dottype.Build](https://www.nuget.org/packages/dottype.Build).

## Install the command

```shell
dotnet tool install --global dottype
dottype customer.json --name Models.Customer -o Customer.cs
dottype events.jsonl.gz --name Models.Event -o Event.cs
dottype events.parquet --name Event
```

The installed CLI requires the .NET 10 runtime. Omit `-o` to print source. Default output is C# 10+ and has no runtime dependency on dottype. Only the optional CSV map needs CsvHelper. CSV, JSON arrays, and JSON Lines sample the first 1,000 records. Use `--sample all` for full inference. Parquet uses its embedded schema.

For JSON Lines, put one object on each line in a `.jsonl` or `.ndjson` file. Compression is inferred from `.gz` and `.br`. To infer a model from a pipe, use `dottype - --format jsonl --name Models.Event`.

[Usage and format reference](https://github.com/DouglasCleghorn/dottype/blob/HEAD/USAGE.md) · [Examples](https://github.com/DouglasCleghorn/dottype/tree/HEAD/examples) · [Release notes](https://github.com/DouglasCleghorn/dottype/blob/HEAD/CHANGELOG.md) · [Report an issue](https://github.com/DouglasCleghorn/dottype/issues)
