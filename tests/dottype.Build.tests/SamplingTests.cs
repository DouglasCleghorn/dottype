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

    private sealed class ShortReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => base.ReadAsync(buffer[..Math.Min(buffer.Length, 1)], cancellationToken);
    }
}
