namespace dottype.tests;

public sealed class CliOptionsTests
{
    public static TheoryData<string[]> InvalidArguments { get; } = new()
    {
        Array.Empty<string>(),
        new[] { "input.json", "other.json" },
        new[] { "input.json", "--name" },
        new[] { "input.json", "--name", "--format", "json" },
        new[] { "input.json", "--name", "Models." },
        new[] { "input.json", "--name", ".Customer" },
        new[] { "input.json", "--namespace", "Models" },
        new[] { "input.json", "--sample-records" },
        new[] { "input.json", "--sample" },
        new[] { "input.json", "--sample", "1", "--sample-records", "2" },
        new[] { "input.json", "--unknown" },
        new[] { "input.json", "--Name", "Customer" },
        new[] { "input.json", "-nf", "json" },
        new[] { "input.json", "--name", "First", "-n", "Second" },
        new[] { "input.json", "--force" },
        new[] { "input.json", "--force=false" },
        new[] { "--version" },
        new[] { "[suggest]", "input.json" }
    };

    [Theory]
    [MemberData(nameof(InvalidArguments))]
    public void Invalid_syntax_returns_a_usage_error(string[] arguments)
        => Assert.Throws<CliException>(() => CliOptions.Parse(arguments, requireInputExists: false));

    [Fact]
    public void Terminator_allows_a_filename_that_looks_like_an_option()
    {
        CliOptions options = CliOptions.Parse(["--name=Models.Customer", "--", "--help"], requireInputExists: false);
        Assert.Equal("--help", options.InputPath);
        Assert.False(options.ShowHelp);
        Assert.Equal("Customer", options.TypeName);
        Assert.Equal("Models", options.Namespace);
    }

    [Fact]
    public void Response_file_tokens_remain_literal_input_paths()
    {
        CliOptions options = CliOptions.Parse(["@missing-response-file.json"], requireInputExists: false);
        Assert.Equal("@missing-response-file.json", options.InputPath);
    }

    [Theory]
    [InlineData("--format", "JSON")]
    [InlineData("--format", "Json")]
    [InlineData("-f", "PARQ")]
    [InlineData("--compression", "GZ")]
    [InlineData("--compression", "Auto")]
    [InlineData("--sample", "ALL")]
    [InlineData("--sample-records", "All")]
    public void Keyword_values_require_lowercase(string option, string value)
        => Assert.Throws<CliException>(() => CliOptions.Parse(["input.json", option, value], requireInputExists: false));

    [Theory]
    [InlineData("--format", "xml", "json", "parq")]
    [InlineData("--compression", "zip", "gzip", "brotli")]
    public void Invalid_choice_lists_accepted_values(string option, string value, string first, string second)
    {
        CliException error = Assert.Throws<CliException>(() => CliOptions.Parse(["input.json", option, value], requireInputExists: false));
        Assert.Contains(first, error.Message);
        Assert.Contains(second, error.Message);
    }
}
