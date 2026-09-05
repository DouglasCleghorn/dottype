using System.Text;
using Parquet;
using Parquet.Schema;

namespace dottype.tests;

public sealed class SchemaReaderTests
{
    [Fact]
    public async Task Json_preserves_names_and_merges_array_objects()
    {
        const string json = """
            [
              { "lowercase": 1, "hyphen-name": true, "class": "first", "child": { "x": 2 } },
              { "lowercase": 2.5, "hyphen-name": null, "class": "second" }
            ]
            """;
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes(json));

        PocoSchema schema = await JsonSchemaReader.ReadAsync(input, "sample-data");
        string source = CSharpWriter.Write(schema, null);

        Assert.Equal("sample_data", schema.Name);
        Assert.Contains("public decimal lowercase", source);
        Assert.Contains("public bool? hyphen_name", source);
        Assert.Contains("DataMember(Name = \"hyphen-name\")", source);
        Assert.Contains("public string @class", source);
        Assert.Contains("public childType? child", source);
        Assert.DoesNotContain("JsonPropertyName(\"class\")", source);
    }

    [Fact]
    public async Task Csv_handles_quotes_missing_values_and_conservative_numbers()
    {
        const string csv = "id,display name,notes,zip\r\n1,Alice,\"hello, world\",00123\r\n2,Bob,,90210\r\n";
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        PocoSchema schema = await CsvSchemaReader.ReadAsync(input, "people");
        string source = CSharpWriter.Write(schema, "Example.Models");

        Assert.Contains("public long id", source);
        Assert.Contains("public string display_name", source);
        Assert.Contains("JsonPropertyName(\"display name\")", source);
        Assert.Contains("public string? notes", source);
        Assert.Contains("public string zip", source);
    }

    [Fact]
    public async Task Parquet_uses_embedded_types_nullability_and_names()
    {
        var parquetSchema = new ParquetSchema(
            new DataField<long>("id"),
            new DataField<string>("display-name", true));
        await using var stream = new MemoryStream();
        await using (ParquetWriter writer = await ParquetWriter.CreateAsync(parquetSchema, stream))
        {
        }
        stream.Position = 0;

        PocoSchema schema = await ParquetSchemaReader.ReadAsync(stream, "people");
        string source = CSharpWriter.Write(schema, null);

        Assert.Contains("public long id", source);
        Assert.True(source.Contains("public string? display_name", StringComparison.Ordinal), source);
        Assert.Contains("DataMember(Name = \"display-name\")", source);
    }

    [Theory]
    [InlineData("lowercase", "lowercase")]
    [InlineData("class", "@class")]
    [InlineData("9lives", "_9lives")]
    [InlineData("with space", "with_space")]
    public void Identifier_changes_as_little_as_possible(string source, string expected)
    {
        Assert.Equal(expected, Identifier.MakeValid(source));
    }
}
