#:package dottype.Build@0.3.0
#:package CsvHelper@33.1.0
#:property dottype=inventory.csv --name InventoryRow --csvhelper-map --column-type 'Asset ID=string'

using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

using var input = File.OpenText(args.Length == 0 ? "inventory.csv" : args[0]);
var config = new CsvConfiguration(CultureInfo.InvariantCulture) { MissingFieldFound = null };
using var csv = new CsvReader(input, config);
csv.Context.RegisterClassMap<InventoryRowMap>();
long bytes = csv.GetRecords<InventoryRow>().Sum(row => row.Size_bytes);
Console.WriteLine($"{bytes} bytes");
