namespace dottype;

internal static class ExampleInference
{
    public static async Task<string> GenerateAsync(Stream input, InputFormat format, CliOptions options, CancellationToken cancellationToken)
    {
        string name = options.TypeName ?? options.DefaultTypeName;
        PocoSchema schema = format switch
        {
            InputFormat.Json => await JsonSchemaReader.ReadAsync(input, name, options.SampleRecords, options.NestChildTypes, cancellationToken),
            InputFormat.JsonLines => await JsonSchemaReader.ReadLinesAsync(input, name, options.SampleRecords, options.NestChildTypes, cancellationToken),
            InputFormat.Csv => await CsvSchemaReader.ReadAsync(input, name, options.SampleRecords, options.ColumnTypes, cancellationToken),
            InputFormat.Parquet => await ParquetSchemaReader.ReadAsync(input, name, options.NestChildTypes, cancellationToken),
            _ => throw new System.Diagnostics.UnreachableException()
        };
        return CSharpWriter.Write(schema, options.Namespace, options.CsvHelperMap);
    }
}
