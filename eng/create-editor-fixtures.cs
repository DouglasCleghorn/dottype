#:package Parquet.Net
#:property PublishAot=false

using Parquet;
using Parquet.Schema;

foreach (string name in new[] { "sample", "changed" })
{
    var schema = name == "sample"
        ? new ParquetSchema(new DataField<int>("id"))
        : new ParquetSchema(new DataField<string>("displayName"));
    await using var file = File.Create(Path.Combine(args[0], name + ".parquet"));
    await using ParquetWriter writer = await ParquetWriter.CreateAsync(schema, file);
}
