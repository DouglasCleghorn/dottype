using System.Text;

namespace dottype.Build.tests;

public sealed class SamplingTests
{
    [Theory]
    [InlineData("[{\"value\":1},{\"value\":\"later\",\"extra\":true}]", 1, "long")]
    [InlineData("[{\"value\":1},{\"value\":\"later\",\"extra\":true}]", 2, "object")]
    [InlineData("[{\"value\":1},INVALID", 1, "long")]
    public async Task Json_infers_only_complete_sampled_objects(string json, int count, string type)
    {
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes(json));
        PocoSchema schema = await JsonSchemaReader.ReadAsync(input, "Row", sampleRecords: count);
        Assert.Equal(type, schema.Classes[0].Properties.Single(p => p.SourceName == "value").TypeName);
        Assert.Equal(count == 2, schema.Classes[0].Properties.Any(p => p.SourceName == "extra"));
    }

    [Theory]
    [InlineData("[{\"value\":}]", 1)]
    [InlineData("[{\"value\":1},INVALID", 2)]
    [InlineData("[{\"value\":1}] garbage", 2)]
    [InlineData("[]", 1)]
    [InlineData("[null]", 1)]
    [InlineData("[1,INVALID", 1)]
    [InlineData("[[],INVALID", 1)]
    public async Task Invalid_data_inside_the_sample_fails(string json, int count)
    {
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await Assert.ThrowsAsync<InvalidDataException>(() => JsonSchemaReader.ReadAsync(input, "Row", sampleRecords: count));
    }

    [Fact]
    public async Task Json_lines_merges_complete_records_across_buffers_and_stops_before_invalid_utf8()
    {
        string text = "{\"x\":[],\"text\":\"" + new string('é', 8500) + "\"}\r\n{\"x\":[1],\"late\":true}\n";
        byte[] bytes = [.. Encoding.UTF8.Preamble, .. Encoding.UTF8.GetBytes(text), 255];
        await using var input = new MemoryStream(bytes);
        PocoSchema schema = await JsonSchemaReader.ReadLinesAsync(input, "Row", 2);
        Assert.Contains(schema.Classes[0].Properties, p => p.Name == "x" && p.TypeName.EndsWith("List<long>", StringComparison.Ordinal));
        Assert.Contains(schema.Classes[0].Properties, p => p.Name == "late" && p.IsNullable);
        input.Position = 0;
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => JsonSchemaReader.ReadLinesAsync(input, "Row"));
        Assert.Contains("line 3", error.Message);
    }

    [Theory]
    [InlineData("{}", true)]
    [InlineData("{}\n", true)]
    [InlineData("{}\r\n{}\r\n", true)]
    [InlineData("", false)]
    [InlineData("\n", false)]
    [InlineData("{}\n\n", false)]
    [InlineData("{}\n[{}]", false)]
    [InlineData("{} {}", false)]
    [InlineData("{\n}", false)]
    public async Task Json_lines_requires_one_object_per_line(string data, bool valid)
    {
        await using var stream = new ShortReadStream(Encoding.UTF8.GetBytes(data));
        if (valid) Assert.Equal("Row", (await JsonSchemaReader.ReadLinesAsync(stream, "Row")).Name);
        else await Assert.ThrowsAsync<InvalidDataException>(() => JsonSchemaReader.ReadLinesAsync(stream, "Row"));
    }

    [Theory]
    [InlineData("[]", "[1]", "long")]
    [InlineData("[1]", "[]", "long")]
    [InlineData("[]", "[\"a\"]", "string")]
    [InlineData("[null]", "[1]", "long?")]
    public async Task Empty_arrays_preserve_the_element_nullability_observed_in_other_records(string first, string second, string type)
    {
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes($"[{{\"items\":{first}}},{{\"items\":{second}}}]"));
        var schema = await JsonSchemaReader.ReadAsync(input, "Row");
        Assert.Equal($"global::System.Collections.Generic.List<{type}>", Assert.Single(schema.Classes[0].Properties).TypeName);
    }

    [Fact]
    public async Task Json_handles_long_tokens_nested_arrays_and_a_standalone_object()
    {
        string json = "{\"text\":\"" + new string('x', 17000) + "\",\"children\":[{\"a\":1},{\"b\":true}]}";
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes(json));
        PocoSchema schema = await JsonSchemaReader.ReadAsync(input, "Row", sampleRecords: 1);
        Assert.Equal(2, schema.Classes.Count);
        Assert.Contains(schema.Classes[1].Properties, p => p.SourceName == "b");
    }

    [Fact]
    public async Task Json_stops_reading_large_tail_and_handles_short_reads_and_bom()
    {
        byte[] bytes = [0xef, 0xbb, 0xbf, .. Encoding.UTF8.GetBytes("[{\"text\":\"é\\\"\"}," + new string('!', 10000))];
        await using var input = new ShortReadStream(bytes);
        PocoSchema schema = await JsonSchemaReader.ReadAsync(input, "Row", sampleRecords: 1);
        Assert.Single(schema.Classes[0].Properties);
        Assert.True(input.Position < 100);
    }

    [Theory]
    [InlineData("value,note\r\n1,\"hello\r\n\"\"world\"\"\"\r\ntext,tail\r\n", 1, "long")]
    [InlineData("value,note\r\n1,\"hello\r\n\"\"world\"\"\"\r\ntext,tail\r\n", 2, "string")]
    [InlineData("value\n1\n\"unterminated", 1, "long")]
    [InlineData("value\n1", 1, "long")]
    [InlineData("\r\nvalue\r\n\r\n1\r\ntext", 1, "long")]
    public async Task Csv_counts_records_instead_of_lines(string csv, int count, string type)
    {
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        PocoSchema schema = await CsvSchemaReader.ReadAsync(input, "Row", sampleRecords: count);
        Assert.Equal(type, schema.Classes[0].Properties[0].TypeName);
    }

    [Theory]
    [InlineData("value\n\"unterminated")]
    [InlineData("value\n\"closed\"bad\n")]
    [InlineData("value,value\n1,2\n")]
    public async Task Csv_rejects_invalid_sampled_records(string csv)
    {
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        await Assert.ThrowsAsync<InvalidDataException>(() => CsvSchemaReader.ReadAsync(input, "Row", sampleRecords: 1));
    }

    [Theory]
    [InlineData("\n", false)]
    [InlineData("\"\"\n", true)]
    [InlineData(",\n", true)]
    public async Task Csv_skips_blank_lines_and_keeps_explicit_empty_cells(string row, bool nullable)
    {
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes("id,note\n1,ok\n" + row));
        var schema = await CsvSchemaReader.ReadAsync(input, "Row");
        Assert.All(schema.Classes[0].Properties, property => Assert.Equal(nullable, property.IsNullable));
    }

    private sealed class ShortReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => base.ReadAsync(buffer[..Math.Min(buffer.Length, 1)], cancellationToken);
    }
}
