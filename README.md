![dottype](assets/icon.png)

# dottype

Generate C# types from example JSON, CSV, or Parquet files. Use `dottype.Build` for compilation and IntelliSense, or the `dottype` CLI to save generated source.

[Usage reference](USAGE.md) · [AI agent index](llms.txt) · [MIT license](LICENSE)

## Install

Install from NuGet. Build integration requires the **.NET 10.0.400 SDK** or later:

```powershell
dotnet tool install --global dottype --version 0.2.0
```

For build integration, reference `dottype.Build` as shown below. The CLI requires the .NET 10 runtime. To upgrade an existing CLI installation, run `dotnet tool update --global dottype --version 0.2.0`. See the [0.2.0 upgrade notes](CHANGELOG.md#020---2026-09-06) for changed options and sampling defaults.

The examples below use `customer.json` containing `{"id":42,"name":"Ada"}`.

## Single-file app

```csharp
#:package dottype.Build@0.2.0
#:property dottype=customer.json --name Models.Customer

var customer = new Models.Customer { id = 42, name = "Ada" };
Console.WriteLine(customer.name);
```

Run `dotnet run --file app.cs`. Paths are relative to the app; use single quotes around paths with spaces. Try the [example app](examples/file-app/app.cs).

## Project property

```xml
<ItemGroup>
  <PackageReference Include="dottype.Build" Version="0.2.0" />
</ItemGroup>
<PropertyGroup>
  <dottype>customer.json --name Models.Customer</dottype>
</PropertyGroup>
```

Paths are relative to the project. Configure one example per project.

## Visual Studio

In **Visual Studio 2026**, install `dottype.Build`, select the example file in Solution Explorer, press **F4**, and set **Build Action** to **dottype**. In **dottype options**, enter:

```text
--name Models.Customer
```

Use either the Build Action or the project property. IntelliSense is available after restore, before the first run. Save and **Build** to refresh. See [editor setup and troubleshooting](USAGE.md#intellisense-refresh-and-errors), including VS Code, or open the [Visual Studio example](examples/visual-studio/App.csproj).

## Command line

```powershell
dottype customer.json --name Models.Customer --output Customer.cs
dottype customers.parquet --name Models.Customer --output Customer.cs
```

Omit `--output` to print source. Run `dottype --help` for options.

Parquet files use internal compression and keep the `.parquet` extension; no compression option is needed.

All entry points infer format and compression from the filename and sample the first **1,000 JSON/CSV records**. Use `--sample all` to include later fields and type changes. Generated code targets C# 10+ and has no runtime dependency on dottype. See [USAGE.md](USAGE.md) for the full argument and inference rules.
