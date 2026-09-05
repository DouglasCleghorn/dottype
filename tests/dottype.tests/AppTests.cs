using System.Globalization;
using System.IO.Compression;
using System.Text;
using Parquet;
using Parquet.Schema;

namespace dottype.tests;

public sealed class AppTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("dottype-tests-").FullName;

    [Theory]
    [InlineData("json", "")]
    [InlineData("json", ".gz")]
    [InlineData("json", ".br")]
    [InlineData("csv", "")]
    [InlineData("csv", ".gz")]
    [InlineData("csv", ".br")]
    [InlineData("parquet", "")]
    [InlineData("parquet", ".gz")]
    [InlineData("parquet", ".br")]
    [InlineData("JSON", ".BR")]
    [InlineData("CSV", ".GZ")]
    public async Task Files_infer_format_compression_and_name(string format, string suffix)
    {
        byte[] payload = await MakePayload(format);
        string path = Path.Combine(_directory, $"customers.{format}{suffix}");
        await File.WriteAllBytesAsync(path, Compress(payload, suffix));

        var result = await Run([path]);

        Assert.Equal(0, result.Code);
        Assert.Empty(result.Error);
        Assert.Contains("public partial class customers", result.Output);
        Assert.Contains("public long id", result.Output);
        Assert.Contains("JsonPropertyName(\"display-name\")", result.Output);
        Assert.DoesNotContain("customers_json", result.Output);
    }

    [Theory]
    [InlineData("json", "gzip", ".gz")]
    [InlineData("json", "brotli", ".br")]
    [InlineData("csv", "gz", ".gz")]
    [InlineData("csv", "br", ".br")]
    [InlineData("parquet", "gzip", ".gz")]
    [InlineData("parquet", "br", ".br")]
    public async Task Compressed_binary_stdin_supports_all_formats(string format, string compression, string suffix)
    {
        await using var input = new MemoryStream(Compress(await MakePayload(format), suffix));
        var result = await Run(["-", "--format", format, "--compression", compression], input);
        Assert.Equal(0, result.Code);
        Assert.Empty(result.Error);
        Assert.Contains("public partial class Root", result.Output);
        Assert.Contains("public long id", result.Output);
    }

    [Fact]
    public async Task Explicit_format_compression_and_name_override_filename()
    {
        string path = Path.Combine(_directory, "download.bin");
        await File.WriteAllBytesAsync(path, Compress(await MakePayload("json"), ".gz"));
        var result = await Run([path, "--format", "json", "--compression", "gzip", "--name", "Person"]);
        Assert.Equal(0, result.Code);
        Assert.Contains("public partial class Person", result.Output);
    }

    [Fact]
    public async Task Compression_none_overrides_suffix_and_output_can_be_written_to_file()
    {
        string path = Path.Combine(_directory, "customers.json.gz");
        string output = Path.Combine(_directory, "generated", "customers.cs");
        await File.WriteAllBytesAsync(path, await MakePayload("json"));
        var result = await Run([path, "--compression", "none", "-o", output]);
        Assert.Equal(0, result.Code);
        Assert.Empty(result.Output);
        Assert.Contains("public partial class customers", await File.ReadAllTextAsync(output));
    }

    [Theory]
    [InlineData(".gz")]
    [InlineData(".br")]
    public async Task Invalid_compression_fails_without_writing_output(string suffix)
    {
        string path = Path.Combine(_directory, "broken.csv" + suffix);
        await File.WriteAllBytesAsync(path, Enumerable.Repeat((byte)255, 64).ToArray());
        var result = await Run([path]);
        Assert.Equal(1, result.Code);
        Assert.Empty(result.Output);
        Assert.StartsWith("dottype:", result.Error);
    }

    [Fact]
    public async Task Input_cannot_be_overwritten()
    {
        string path = Path.Combine(_directory, "customers.json.gz");
        byte[] original = Compress(await MakePayload("json"), ".gz");
        await File.WriteAllBytesAsync(path, original);
        var result = await Run([path, "-o", path]);
        Assert.Equal(2, result.Code);
        Assert.Equal(original, await File.ReadAllBytesAsync(path));
    }

    public static bool IsWindows => OperatingSystem.IsWindows();

    [Fact(Skip = "Windows symlinks require privileges; exercised on Linux and macOS.", SkipType = typeof(AppTests), SkipWhen = nameof(IsWindows))]
    public async Task Output_symlink_does_not_overwrite_its_target()
    {
        string path = Path.Combine(_directory, "customers.json");
        string output = Path.Combine(_directory, "model.cs");
        byte[] original = await MakePayload("json");
        await File.WriteAllBytesAsync(path, original);
        File.CreateSymbolicLink(output, path);
        var result = await Run([path, "-o", output]);
        Assert.Equal(0, result.Code);
        Assert.Equal(original, await File.ReadAllBytesAsync(path));
        Assert.Contains("public partial class customers", await File.ReadAllTextAsync(output));
    }

    [Fact]
    public async Task Invalid_utf8_csv_is_not_silently_rewritten()
    {
        await using var input = new MemoryStream([105, 100, 44, 255, 10]);
        var result = await Run(["-", "--format", "csv"], input);
        Assert.Equal(1, result.Code);
        Assert.Empty(result.Output);
    }

    [Theory]
    [InlineData(".gz")]
    [InlineData(".br")]
    public async Task Truncated_compression_is_rejected(string suffix)
    {
        string path = Path.Combine(_directory, "broken.csv" + suffix);
        byte[] compressed = Compress(await MakePayload("csv"), suffix);
        await File.WriteAllBytesAsync(path, compressed[..^1]);
        var result = await Run([path]);
        Assert.Equal(1, result.Code);
        Assert.Empty(result.Output);
    }

    [Fact]
    public async Task Duplicate_csv_headers_are_rejected_as_ambiguous()
    {
        await using var input = new MemoryStream("id,id\n1,2\n"u8.ToArray());
        var result = await Run(["-", "--format", "csv"], input);
        Assert.Equal(1, result.Code);
        Assert.Contains("Duplicate field names", result.Error);
    }

    [Theory]
    [InlineData("--name", "")]
    [InlineData("--compression", "zip")]
    public async Task Invalid_options_return_usage_error(string option, string value)
    {
        var result = await Run(["-", "--format", "json", option, value]);
        Assert.Equal(2, result.Code);
        Assert.Empty(result.Output);
    }

    [Fact]
    public async Task Stdin_requires_format()
    {
        var result = await Run(["-"]);
        Assert.Equal(2, result.Code);
        Assert.Contains("--format is required", result.Error);
    }

    private static async Task<(int Code, string Output, string Error)> Run(string[] args, Stream? input = null)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        int code = await App.RunAsync(args, output, error, standardInput: input);
        return (code, output.ToString(), error.ToString());
    }

    private static async Task<byte[]> MakePayload(string format)
    {
        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            return Encoding.UTF8.GetBytes("{\"id\":1,\"display-name\":\"Alice\"}");
        if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
            return Encoding.UTF8.GetBytes("id,display-name\n1,Alice\n");

        await using var stream = new MemoryStream();
        await using (ParquetWriter writer = await ParquetWriter.CreateAsync(
            new ParquetSchema(new DataField<long>("id"), new DataField<string>("display-name")), stream))
        {
        }
        return stream.ToArray();
    }

    private static byte[] Compress(byte[] payload, string suffix)
    {
        if (suffix.Length == 0) return payload;
        using var result = new MemoryStream();
        using (Stream compressor = suffix.Equals(".gz", StringComparison.OrdinalIgnoreCase)
            ? new GZipStream(result, CompressionLevel.SmallestSize, leaveOpen: true)
            : new BrotliStream(result, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            compressor.Write(payload);
        }
        return result.ToArray();
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
