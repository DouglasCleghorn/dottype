![dottype](https://raw.githubusercontent.com/DouglasCleghorn/dottype/HEAD/assets/icon.png)

# dottype build integration

Generate C# models from CSV, JSON, JSON Lines, or Parquet examples at build time. The types appear in IntelliSense after restore. Use several models in a project, or start with a single C# file and the .NET 10.0.400 SDK or later.

## Read an inventory in a single-file app

Save the following as `inventory.csv`.

```csv
File path,Size bytes,Asset ID
/data/app.log,4096,42
/data/notes.txt,2048
```

Save this as `inventory.cs` in the same directory.

```csharp
#:package dottype.Build@0.3.0
#:package CsvHelper@33.1.0
#:property dottype=inventory.csv --name InventoryRow --csvhelper-map --column-type 'Asset ID=string'

using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

using var input = File.OpenText("inventory.csv");
var config = new CsvConfiguration(CultureInfo.InvariantCulture) { MissingFieldFound = null };
using var csv = new CsvReader(input, config);
csv.Context.RegisterClassMap<InventoryRowMap>();
Console.WriteLine($"{csv.GetRecords<InventoryRow>().Sum(row => row.Size_bytes)} bytes");
```

Run `dotnet run --file inventory.cs` from the directory containing both files. It prints **6144 bytes**. The map handles spaces in the original headers. The reader accepts the omitted asset ID on the second row, and pinning the ID to `string` preserves leading zeroes in future exports.

## Use an ordinary project

```shell
dotnet add package dottype.Build
```

Add one item per model. These two files can both have an `address` object, each with its own fields.

```xml
<ItemGroup>
  <None Remove="customer.json;orders.ndjson" />
  <dottype Include="customer.json" Options="--name Models.Customer" />
  <dottype Include="orders.ndjson" Options="--name Models.Order" />
</ItemGroup>
```

Put one order object per line in `orders.ndjson`. The default sample includes 1,000 orders. Set `--sample all` on that item to include fields that appear later. Use `customer.address` and `order.address` in C# to access their typed fields. [Runnable two-model project](https://github.com/DouglasCleghorn/dottype/tree/HEAD/examples/visual-studio)

For a single model, a project property such as `<dottype>customer.json --name Models.Customer</dottype>` also works. The default generated classes have no runtime dependency on dottype or CsvHelper.

In Visual Studio 2026, you can instead select the example file, press **F4**, set **Build Action** to **dottype**, and enter flags in **dottype options**. Build to refresh after saving an example.

[Project and editor setup](https://github.com/DouglasCleghorn/dottype/blob/HEAD/USAGE.md) · [Runnable inventory example](https://github.com/DouglasCleghorn/dottype/blob/HEAD/examples/file-app/inventory.cs) · [Standalone CLI](https://www.nuget.org/packages/dottype) · [Release notes](https://github.com/DouglasCleghorn/dottype/blob/HEAD/CHANGELOG.md) · [Report an issue](https://github.com/DouglasCleghorn/dottype/issues)
