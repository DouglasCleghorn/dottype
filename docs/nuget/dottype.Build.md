![dottype](https://raw.githubusercontent.com/DouglasCleghorn/dottype/HEAD/assets/icon.png)

# dottype build integration

**Use example data as C# types in your application.** Generate types from JSON, CSV, or Parquet during compilation, with IntelliSense in Visual Studio and VS Code.

This package integrates with MSBuild and file-based C# apps. For a command-line tool that saves generated source, use [dottype](https://www.nuget.org/packages/dottype).

## Install and configure

Use the **.NET 10.0.400 SDK**. Visual Studio integration requires **Visual Studio 2026**; VS Code setup is covered in the [editor guide](https://github.com/DouglasCleghorn/dottype/blob/HEAD/USAGE.md#intellisense-refresh-and-errors).

```shell
dotnet add package dottype.Build
```

Save `customer.json` next to your project file:

```json
{"id":42,"name":"Ada"}
```

Add this property to your project:

```xml
<PropertyGroup>
  <dottype>customer.json --name Models.Customer</dottype>
</PropertyGroup>
```

After restore, use the generated type in your application:

```csharp
var customer = new Models.Customer { id = 42, name = "Ada" };
Console.WriteLine(customer.name);
```

Paths are relative to the project. Configure **one example file per project**.

## Single-file C# app

Put `app.cs` next to `customer.json`:

```csharp
#:package dottype.Build@0.2.0
#:property dottype=customer.json --name Models.Customer

var customer = new Models.Customer { id = 42, name = "Ada" };
Console.WriteLine(customer.name);
```

Run `dotnet run --file app.cs`. Paths are relative to the app; use single quotes around paths with spaces.

## Visual Studio

As an alternative to the project property, select the example file in **Solution Explorer**, press **F4**, and set **Build Action** to **dottype**. Enter `--name Models.Customer` in **dottype options**.

Use either the project property or the Build Action. IntelliSense is available after restore, before the first run. Save and **Build** to refresh generated types after changing an example. No Visual Studio extension is required.

## Generation behavior

- Infer format and outer gzip/Brotli compression from the example filename. Use `.parquet` files with internal compression for Parquet; no compression option is needed.
- Examine the first **1,000 JSON/CSV records** by default; add `--sample all` to include later fields and type changes. Parquet uses its embedded schema.
- Generate C# 10+ types, including nested types and inferred nullability, with no runtime dependency on dottype.
- Preserve source field names, using mapping attributes when names need repair.
- Keep generated files under the intermediate output directory and reuse unchanged inferred schemas.

## Documentation and support

- [Usage reference: arguments and equivalent examples](https://github.com/DouglasCleghorn/dottype/blob/HEAD/USAGE.md)
- [IntelliSense, refresh, and troubleshooting](https://github.com/DouglasCleghorn/dottype/blob/HEAD/USAGE.md#intellisense-refresh-and-errors)
- [Sample applications](https://github.com/DouglasCleghorn/dottype/tree/HEAD/examples)
- [Release history and upgrade notes](https://github.com/DouglasCleghorn/dottype/blob/HEAD/CHANGELOG.md)
- [Report a bug or request a feature](https://github.com/DouglasCleghorn/dottype/issues)
- [AI agent documentation index](https://github.com/DouglasCleghorn/dottype/blob/HEAD/llms.txt)
- [MIT license](https://github.com/DouglasCleghorn/dottype/blob/HEAD/LICENSE)
