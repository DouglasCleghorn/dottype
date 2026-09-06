using System.Globalization;

namespace dottype.tests;

public sealed class CompletionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dottype-completion", Guid.NewGuid().ToString("N"));

    public CompletionTests() => Directory.CreateDirectory(_directory);

    [Theory]
    [InlineData("--format", "csv,json,parq,parquet")]
    [InlineData("-f", "csv,json,parq,parquet")]
    [InlineData("--compression", "auto,br,brotli,gz,gzip,none")]
    [InlineData("--sample", "1000,all")]
    [InlineData("--sample-records", "1000,all")]
    public async Task Suggests_values_for_options_and_aliases(string option, string expected)
        => Assert.Equal(expected.Split(','), await Complete($"dottype {option} "));

    [Theory]
    [InlineData("--format=JS", "json")]
    [InlineData("-f pa", "parq,parquet")]
    [InlineData("--compression G", "gz,gzip")]
    [InlineData("--sample A", "all")]
    public async Task Filters_values_case_insensitively(string arguments, string expected)
        => Assert.Equal(expected.Split(','), await Complete("dottype " + arguments));

    [Fact]
    public async Task Suggests_cli_flags_but_not_build_only_or_default_root_flags()
    {
        string[] suggestions = await Complete("dottype --");
        Assert.Contains("--format", suggestions);
        Assert.Contains("--output", suggestions);
        Assert.Contains("--help", suggestions);
        Assert.DoesNotContain("--force", suggestions);
        Assert.DoesNotContain("--version", suggestions);
    }

    [Fact]
    public async Task Uses_cursor_position_with_text_after_the_cursor()
    {
        const string line = "dottype --format js --name Customer";
        int cursor = line.IndexOf(" --name", StringComparison.Ordinal);
        Assert.Equal(["json"], await Complete(line, cursor));
    }

    [Theory]
    [InlineData("")]
    [InlineData("--output ")]
    [InlineData("-o ")]
    public async Task Completes_quoted_paths_and_directories(string option)
    {
        string path = Path.Combine(_directory, "sample file.json");
        File.WriteAllText(path, "{}");
        string directory = Path.Combine(_directory, "sample folder");
        Directory.CreateDirectory(directory);
        string[] suggestions = await Complete($"dottype {option}\"{Path.Combine(_directory, "sample")}\"");
        Assert.Contains(path, suggestions);
        Assert.Contains(directory + Path.DirectorySeparatorChar, suggestions);
    }

    [Fact]
    public async Task Missing_directories_yield_no_path_suggestions()
        => Assert.Empty(await Complete($"dottype --output \"{Path.Combine(_directory, "missing", "file")}\""));

    [Fact]
    public async Task Preserves_forward_slashes_in_path_prefixes()
    {
        string path = Path.Combine(_directory, "sample.json").Replace('\\', '/');
        File.WriteAllText(path, "{}");
        Assert.Contains(path, await Complete($"dottype --output \"{path[..^5]}\""));
    }

    [Fact]
    public async Task Terminator_disables_option_suggestions()
        => Assert.DoesNotContain("--format", await Complete("dottype -- --"));

    [Theory]
    [InlineData("\"", "\"")]
    [InlineData("\"", "")]
    [InlineData("'", "'")]
    [InlineData("'", "")]
    public async Task Completes_a_word_containing_spaces_with_closed_or_unfinished_quotes(string open, string close)
    {
        string path = Path.Combine(_directory, "sample file.json");
        File.WriteAllText(path, "{}");
        Assert.Contains(path, await Complete($"dottype --output {open}{Path.Combine(_directory, "sample ")}{close}"));
    }

    [Fact]
    public async Task Completes_after_a_quoted_executable_path()
        => Assert.Equal(["json"], await Complete("\"C:/program files/dottype.exe\" --format j"));

    [Theory]
    [InlineData("dottype --format", 0)]
    [InlineData("", 0)]
    public async Task Empty_prefix_is_a_valid_completion_request(string line, int cursor)
        => Assert.Contains("--format", await Complete(line, cursor));

    [Fact]
    public async Task Completion_does_not_read_stdin_or_write_output()
    {
        string path = Path.Combine(_directory, "generated.cs");
        File.WriteAllText(path, "keep");
        using var input = new MemoryStream([1, 2, 3]);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        int code = await App.RunAsync(["[suggest]", $"dottype - --output \"{path}\" --format j"], output, error, input);
        Assert.Equal(0, code);
        Assert.Equal("json", output.ToString().Trim());
        Assert.Empty(error.ToString());
        Assert.Equal(0, input.Position);
        Assert.Equal("keep", File.ReadAllText(path));
    }

    [Theory]
    [InlineData("[suggest:invalid]")]
    [InlineData("[suggest:-1]")]
    [InlineData("[suggest:999999999999]")]
    [InlineData("[suggest:100]")]
    public async Task Malformed_cursor_returns_usage_error(string directive)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        Assert.Equal(2, await App.RunAsync([directive, "dottype --format "], output, error));
        Assert.Empty(output.ToString());
        Assert.Contains("Completion cursor", error.ToString());
    }

    private static async Task<string[]> Complete(string commandLine, int? cursor = null)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        string directive = cursor is int position ? $"[suggest:{position.ToString(CultureInfo.InvariantCulture)}]" : "[suggest]";
        Assert.Equal(0, await App.RunAsync([directive, commandLine], output, error));
        Assert.Empty(error.ToString());
        return output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
