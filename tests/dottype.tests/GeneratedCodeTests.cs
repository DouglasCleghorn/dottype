using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Parquet;
using Parquet.File.Values.Primitives;
using Parquet.Schema;

namespace dottype.tests;

public sealed class GeneratedCodeTests
{
    // TRUSTED_PLATFORM_ASSEMBLIES also includes the test host's NuGet dependencies.
    // Only assemblies next to System.Private.CoreLib belong to the .NET runtime.
    private static readonly MetadataReference[] FrameworkReferences =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Where(path => string.Equals(Path.GetDirectoryName(path),
            Path.GetDirectoryName(typeof(object).Assembly.Location),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        .Select(path => MetadataReference.CreateFromFile(path))
        .ToArray();

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
        using var assembly = new MemoryStream();
        var result = CreateCompilation(source).Emit(assembly);
        Assert.True(result.Success, source + "\n" + string.Join("\n", result.Diagnostics));
        return Assembly.Load(assembly.ToArray());
    }

    private static CSharpCompilation CreateCompilation(string source) => CSharpCompilation.Create(
        "Generated_" + Guid.NewGuid().ToString("N"),
        [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp10))],
        FrameworkReferences,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
            generalDiagnosticOption: ReportDiagnostic.Error));

    [Theory]
    [InlineData("#nullable enable\npublic class Row { public string Name { get; set; } }", "CS8618")]
    [InlineData("public class Row { public global::Parquet.ParquetReader Reader { get; set; } }", "CS0400")]
    public void Compilation_rejects_nullable_warnings_and_package_dependencies(string source, string diagnosticId)
    {
        using var assembly = new MemoryStream();
        var result = CreateCompilation(source).Emit(assembly);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == diagnosticId && diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Theory]
    [InlineData(typeof(bool), typeof(bool))]
    [InlineData(typeof(byte), typeof(byte))]
    [InlineData(typeof(sbyte), typeof(sbyte))]
    [InlineData(typeof(short), typeof(short))]
    [InlineData(typeof(ushort), typeof(ushort))]
    [InlineData(typeof(int), typeof(int))]
    [InlineData(typeof(uint), typeof(uint))]
    [InlineData(typeof(long), typeof(long))]
    [InlineData(typeof(ulong), typeof(ulong))]
    [InlineData(typeof(float), typeof(float))]
    [InlineData(typeof(double), typeof(double))]
    [InlineData(typeof(decimal), typeof(decimal))]
    [InlineData(typeof(string), typeof(string))]
    [InlineData(typeof(ReadOnlyMemory<char>), typeof(string))]
    [InlineData(typeof(byte[]), typeof(byte[]))]
    [InlineData(typeof(ReadOnlyMemory<byte>), typeof(byte[]))]
    [InlineData(typeof(DateTime), typeof(DateTime))]
    [InlineData(typeof(DateOnly), typeof(DateTime))]
    [InlineData(typeof(TimeOnly), typeof(long))]
    [InlineData(typeof(Guid), typeof(Guid))]
    // The reader's default interprets legacy INT96 as DateTime.
    [InlineData(typeof(System.Numerics.BigInteger), typeof(DateTime))]
    public async Task Parquet_scalar_types_compile_with_required_optional_and_collection_values(Type inputType, Type expectedType)
    {
        var schema = new ParquetSchema(
            new DataField("required", inputType, false),
            new DataField("optional", inputType, true),
            new ListField("items", new DataField("element", inputType, true)),
            new MapField("values", new DataField<string>("key", false), new DataField("value", inputType, true)));
        Type root = Compile(await GenerateParquet(schema)).GetType("Models.Row", throwOnError: true)!;
        Type optionalType = expectedType.IsValueType ? typeof(Nullable<>).MakeGenericType(expectedType) : expectedType;
        Assert.Equal(expectedType, root.GetProperty("required")!.PropertyType);
        Assert.Equal(optionalType, root.GetProperty("optional")!.PropertyType);
        Assert.Equal(typeof(List<>).MakeGenericType(optionalType), root.GetProperty("items")!.PropertyType);
        Assert.Equal(typeof(Dictionary<,>).MakeGenericType(typeof(string), optionalType), root.GetProperty("values")!.PropertyType);

        var nullability = new NullabilityInfoContext();
        Assert.Equal(NullabilityState.NotNull, nullability.Create(root.GetProperty("required")!).ReadState);
        Assert.Equal(NullabilityState.Nullable, nullability.Create(root.GetProperty("optional")!).ReadState);
        Assert.Equal(NullabilityState.Nullable, nullability.Create(root.GetProperty("items")!).GenericTypeArguments[0].ReadState);
        Assert.Equal(NullabilityState.NotNull, nullability.Create(root.GetProperty("values")!).GenericTypeArguments[0].ReadState);
        Assert.Equal(NullabilityState.Nullable, nullability.Create(root.GetProperty("values")!).GenericTypeArguments[1].ReadState);
    }

    [Theory]
    [InlineData("decimal", typeof(decimal))]
    [InlineData("big-decimal", typeof(decimal))]
    [InlineData("date", typeof(DateTime))]
    [InlineData("timestamp-millis", typeof(DateTime))]
    [InlineData("timestamp-micros", typeof(DateTime))]
    [InlineData("timestamp-nanos", typeof(DateTime))]
    [InlineData("time-millis", typeof(int))]
    [InlineData("time-micros", typeof(long))]
    [InlineData("time-nanos", typeof(long))]
    public async Task Parquet_logical_types_compile_with_framework_only_types(string logicalType, Type expectedType)
    {
        DataField Field(string name, bool nullable) => logicalType switch
        {
            "decimal" => new DecimalDataField(name, 18, 4, isNullable: nullable),
            // Use a precision that fits decimal, the reader's default decimal representation.
            "big-decimal" => new BigDecimalDataField(name, 18, 4, isNullable: nullable),
            "date" => new DateTimeDataField(name, DateTimeFormat.Date, isNullable: nullable),
            "timestamp-millis" => new DateTimeDataField(name, DateTimeFormat.Timestamp, unit: DateTimeTimeUnit.Millis, isNullable: nullable),
            "timestamp-micros" => new DateTimeDataField(name, DateTimeFormat.Timestamp, unit: DateTimeTimeUnit.Micros, isNullable: nullable),
            "timestamp-nanos" => new DateTimeDataField(name, DateTimeFormat.Timestamp, unit: DateTimeTimeUnit.Nanos, isNullable: nullable),
            "time-millis" => new TimeDataField(name, TimeUnitPrecision.Millis, isNullable: nullable),
            "time-micros" => new TimeDataField(name, TimeUnitPrecision.Micros, isNullable: nullable),
            "time-nanos" => new TimeDataField(name, TimeUnitPrecision.Nanos, isNullable: nullable),
            _ => throw new ArgumentOutOfRangeException(nameof(logicalType))
        };
        string source = await GenerateParquet(new ParquetSchema(Field("required", false), Field("optional", true)));
        Type root = Compile(source).GetType("Models.Row", throwOnError: true)!;
        Assert.Equal(expectedType, root.GetProperty("required")!.PropertyType);
        Assert.Equal(typeof(Nullable<>).MakeGenericType(expectedType), root.GetProperty("optional")!.PropertyType);
    }

    [Fact]
    public async Task Parquet_interval_reports_unsupported_type_instead_of_emitting_a_package_dependency()
    {
        var schema = new ParquetSchema(new DataField<Interval>("duration"));
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => GenerateParquet(schema));
        Assert.Contains("duration", error.Message);
        Assert.Contains("Interval", error.Message);
        Assert.Contains("unsupported", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(typeof(int), typeof(int?[]))]
    [InlineData(typeof(string), typeof(string[]))]
    [InlineData(typeof(byte[]), typeof(byte[][]))]
    public async Task Parquet_legacy_repeated_fields_compile_with_the_readers_nullability(Type inputType, Type expectedType)
    {
        // Parquet.Net marks legacy repeated DataFields nullable when reading the schema.
        string source = await GenerateParquet(new ParquetSchema(new DataField("items", inputType, false, isArray: true)));
        Type root = Compile(source).GetType("Models.Row", throwOnError: true)!;
        PropertyInfo property = root.GetProperty("items")!;
        Assert.Equal(expectedType, property.PropertyType);
        Assert.Equal(NullabilityState.Nullable, new NullabilityInfoContext().Create(property).ElementType!.ReadState);
    }

    [Fact]
    public async Task Nested_parquet_models_compile_with_framework_only_types()
    {
        var schema = new ParquetSchema(
            new DataField<DateTime>("created-at"),
            new StructField("child", new DataField<int>("count"),
                new DataField<string>("name", false), new DataField<byte[]>("payload", false)),
            new ListField("items", new DataField<int?>("element")),
            new MapField("labels", new DataField<string>("key", false), new DataField<string>("value")));
        string source = await GenerateParquet(schema);
        Compile(source);
        Assert.Contains("global::System.DateTime", source);
        Assert.Contains("List<int?>", source);
        Assert.DoesNotContain("global::Parquet", source);
    }

    private static async Task<string> GenerateParquet(ParquetSchema schema)
    {
        await using var input = new MemoryStream();
        await using (ParquetWriter writer = await ParquetWriter.CreateAsync(schema, input))
        {
        }
        input.Position = 0;
        PocoSchema result = await ParquetSchemaReader.ReadAsync(input, "Row");
        return CSharpWriter.Write(result, "Models");
    }
}
