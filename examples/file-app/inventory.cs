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
