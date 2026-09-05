using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Parquet;
using Parquet.Schema;

namespace dottype.tests;

public sealed class GeneratedCodeTests
{
    [Theory]
    [InlineData("Root", "{\"Root\":1,\"a-b\":true,\"a_b\":2,\"class\":\"ok\"}")]
    [InlineData("record", "{\"record\":1,\"field\":2,\"required\":3}")]
    [InlineData("Root", "{\"x\":{},\"xType\":1,\"line\\u2028break\":1,\"a\\u200db\":1,\"ab\":2}")]
    [InlineData("Root", "{\"𐐀\":1,\"9lives\":2,\"\":3,\"quote\\\"\":4}")]
    public async Task Generated_json_compiles_and_round_trips_field_names(string name, string json)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        PocoSchema schema = await JsonSchemaReader.ReadAsync(stream, name);
        string source = CSharpWriter.Write(schema, "Models");
        Assembly assembly = Compile(source);
        Type root = assembly.GetType("Models." + Identifier.Unescaped(schema.Name), throwOnError: true)!;
        object? value = JsonSerializer.Deserialize(json, root);
        using JsonDocument original = JsonDocument.Parse(json);
        using JsonDocument roundTrip = JsonDocument.Parse(JsonSerializer.Serialize(value, root));
        foreach (JsonProperty property in original.RootElement.EnumerateObject())
        {
            Assert.True(roundTrip.RootElement.TryGetProperty(property.Name, out _), source);
        }
    }

    [Fact]
    public async Task Csv_preserves_already_valid_names_when_a_repaired_name_collides()
    {
        await using var stream = new MemoryStream("a-b,a_b,Row\n1,2,3\n"u8.ToArray());
        PocoSchema schema = await CsvSchemaReader.ReadAsync(stream, "Row");
        Assert.Equal("a_b", schema.Classes[0].Properties.Single(p => p.SourceName == "a_b").Name);
        Compile(CSharpWriter.Write(schema, null));
    }

    private static Assembly Compile(string source)
    {
        string[] platformAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var compilation = CSharpCompilation.Create(
            "Generated_" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source)],
            platformAssemblies.Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var assembly = new MemoryStream();
        var result = compilation.Emit(assembly);
        Assert.True(result.Success, source + "\n" + string.Join("\n", result.Diagnostics));
        return Assembly.Load(assembly.ToArray());
    }

    [Fact]
    public async Task Nested_parquet_models_compile_with_framework_only_types()
    {
        var schema = new ParquetSchema(
            new DataField<DateTime>("created-at"),
            new StructField("child", new DataField<int>("count")),
            new ListField("items", new DataField<int?>("element")),
            new MapField("labels", new DataField<string>("key", false), new DataField<string>("value")));
        await using var input = new MemoryStream();
        await using (ParquetWriter writer = await ParquetWriter.CreateAsync(schema, input))
        {
        }
        input.Position = 0;
        PocoSchema result = await ParquetSchemaReader.ReadAsync(input, "Row");
        string source = CSharpWriter.Write(result, "Models");
        Compile(source);
        Assert.Contains("global::System.DateTime", source);
        Assert.Contains("List<int?>", source);
        Assert.DoesNotContain("global::Parquet", source);
    }
}
