using System.Collections;
using System.IO.Compression;
using Microsoft.Build.Framework;
using Parquet;
using Parquet.Schema;

namespace dottype.Build.tests;

public sealed class BuildTaskTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dottype-build-tests", Guid.NewGuid().ToString("N"));
    public BuildTaskTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void Shared_arguments_have_the_same_values_in_cli_and_build_contexts()
    {
        File.WriteAllText(Path.Combine(_directory, "sample file.json"), "{}");
        string[] variants = ["", "-n Models.Customer -f json --compression none --sample-records 2",
            "--name Customer --format parq --compression gz --sample-records all",
            "--format csv --compression br --sample-records 1000",
            "--name=Acme.Models.Customer --format=json --compression=auto --sample=all",
            "-n=Models.Customer -f=csv --compression=none --sample=2"];
        foreach (string variant in variants)
        {
            string settings = "'sample file.json' " + variant;
            CliOptions cli = CliOptions.Parse(BuildOptions.Tokenize(settings), baseDirectory: _directory);
            CliOptions build = BuildOptions.Parse(settings, _directory);
            Assert.Equal(cli, build);
        }
    }

    [Theory]
    [InlineData("[suggest] 'dottype --format '")]
    [InlineData("[suggest:17] 'dottype --format '")]
    [InlineData("[parse] sample.json")]
    public void Build_settings_reject_cli_directives(string settings)
        => Assert.Throws<CliException>(() => BuildOptions.Parse(settings, _directory, requireInputExists: false));

    [Theory]
    [InlineData("--sample-records 0")]
    [InlineData("--sample-records -1")]
    [InlineData("--sample-records invalid")]
    [InlineData("--format xml")]
    [InlineData("--compression zip")]
    [InlineData("--name invalid-namespace.Customer")]
    [InlineData("--name Models.")]
    [InlineData("--name .Customer")]
    [InlineData("--name Models..Customer")]
    [InlineData("--name")]
    [InlineData("--name --format json")]
    [InlineData("--namespace Models")]
    [InlineData("--format=xml")]
    [InlineData("--sample-records=0")]
    [InlineData("--sample=0")]
    [InlineData("--sample -1")]
    [InlineData("--sample")]
    [InlineData("--format JSON")]
    [InlineData("-f=Csv")]
    [InlineData("--compression GZIP")]
    [InlineData("--compression=Auto")]
    [InlineData("--sample ALL")]
    [InlineData("--sample-records=All")]
    public void Shared_arguments_have_the_same_validation_errors(string arguments)
    {
        string settings = "sample.json " + arguments;
        CliException cli = Assert.Throws<CliException>(() => CliOptions.Parse(BuildOptions.Tokenize(settings), requireInputExists: false));
        CliException build = Assert.Throws<CliException>(() => BuildOptions.Parse(settings, _directory, requireInputExists: false));
        Assert.Equal(cli.Message, build.Message);
    }

    [Theory]
    [InlineData("2", 2)]
    [InlineData("all", null)]
    public void Sampling_aliases_have_identical_values_in_cli_and_build(string value, int? expected)
    {
        CliOptions cli = CliOptions.Parse(["sample.json", "--sample=" + value], requireInputExists: false);
        CliOptions legacyCli = CliOptions.Parse(["sample.json", "--sample-records", value], requireInputExists: false);
        Assert.Equal(expected, cli.SampleRecords);
        Assert.Equal(cli, legacyCli);

        CliOptions build = BuildOptions.Parse("sample.json --sample=" + value, _directory, requireInputExists: false);
        CliOptions legacyBuild = BuildOptions.Parse("sample.json --sample-records " + value, _directory, requireInputExists: false);
        Assert.Equal(expected, build.SampleRecords);
        Assert.Equal(build, legacyBuild);
    }

    [Fact]
    public void Defaults_and_quoted_paths_use_the_app_directory()
    {
        File.WriteAllText(Path.Combine(_directory, "sample file.json"), "{}");
        CliOptions options = BuildOptions.Parse("\"sample file.json\" --name Models.Person", _directory);
        Assert.Equal(Path.Combine(_directory, "sample file.json"), options.InputPath);
        Assert.Equal(1000, options.SampleRecords);
        Assert.Equal("Person", options.TypeName);
        Assert.Equal("Models", options.Namespace);
        Assert.Equal<string>([@"C:\data\some file.json", "--name", "Row"], BuildOptions.Tokenize("\"C:\\data\\some file.json\" --name Row"));
    }

    [Theory]
    [InlineData("sample.json --sample-records 0")]
    [InlineData("sample.json --sample-records -1")]
    [InlineData("sample.json --sample-records")]
    [InlineData("sample.json --output stolen.cs")]
    [InlineData("sample.json --output=stolen.cs")]
    [InlineData("sample.json -o=stolen.cs")]
    [InlineData("sample.json --help")]
    [InlineData("sample.json another.json")]
    [InlineData("\"unterminated")]
    public void Bad_settings_report_actionable_diagnostics(string settings)
    {
        File.WriteAllText(Path.Combine(_directory, "sample.json"), "{}");
        GenerateExampleType task = CreateTask(settings);
        Assert.False(task.Execute());
        BuildErrorEventArgs error = Assert.Single(((BuildEngine)task.BuildEngine).Errors);
        Assert.Equal("dottype001", error.Code);
        Assert.Equal(task.ProjectFile, error.File);
        Assert.Contains("dottype property", error.Message);
    }

    [Fact]
    public void Cache_preserves_source_timestamp_and_invalidates_for_input_and_options()
    {
        string path = Path.Combine(_directory, "sample.json");
        File.WriteAllText(path, "{\"id\":1}");
        GenerateExampleType first = Run();
        DateTime sentinel = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(first.GeneratedFile, sentinel);
        Assert.True(Run().CacheHit);
        Assert.Equal(sentinel, File.GetLastWriteTimeUtc(first.GeneratedFile));

        File.WriteAllText(path, "{\"id\":222}");
        Assert.False(Run().CacheHit);
        Assert.Equal(sentinel, File.GetLastWriteTimeUtc(first.GeneratedFile));
        File.WriteAllText(path, "{\"name\":\"A\"}");
        Assert.False(Run().CacheHit);
        Assert.Contains("string name", File.ReadAllText(first.GeneratedFile));
        Assert.False(Run("sample.json --name Changed").CacheHit);
        Assert.Contains("class Changed", File.ReadAllText(first.GeneratedFile));
    }

    [Fact]
    public void Force_refresh_handles_preserved_metadata_and_missing_or_corrupt_outputs()
    {
        string path = Path.Combine(_directory, "sample.json");
        File.WriteAllText(path, "{\"aa\":1}");
        GenerateExampleType first = Run();
        DateTime modified = File.GetLastWriteTimeUtc(path);
        File.WriteAllText(path, "{\"bb\":1}");
        File.SetLastWriteTimeUtc(path, modified);
        Assert.True(Run().CacheHit); // Documented metadata-cache tradeoff.
        Assert.False(Run("sample.json --force").CacheHit);
        Assert.Contains("long bb", File.ReadAllText(first.GeneratedFile));
        Assert.True(Run().CacheHit);
        File.WriteAllText(first.CacheFile, "broken");
        Assert.False(Run().CacheHit);
        File.WriteAllText(first.GeneratedFile, "broken");
        Assert.False(Run().CacheHit);
        File.Delete(first.GeneratedFile);
        Assert.False(Run().CacheHit);
    }

    [Fact]
    public void Missing_or_invalid_example_never_succeeds_from_cache()
    {
        string path = Path.Combine(_directory, "sample.json");
        File.WriteAllText(path, "{}");
        Run();
        File.Delete(path);
        Assert.False(CreateTask("sample.json").Execute());
        File.WriteAllText(path, "invalid");
        Assert.False(CreateTask("sample.json").Execute());
    }

    [Fact]
    public void Default_sampling_and_all_records_have_distinct_cache_entries()
    {
        File.WriteAllText(Path.Combine(_directory, "sample.json"),
            "[" + string.Join(",", Enumerable.Repeat("{\"id\":1}", 1000)) + ",{\"id\":\"later\",\"extra\":true}]");
        GenerateExampleType sampled = Run();
        Assert.Contains("long id", File.ReadAllText(sampled.GeneratedFile));
        Assert.DoesNotContain("extra", File.ReadAllText(sampled.GeneratedFile));
        GenerateExampleType all = Run("sample.json --sample-records all");
        Assert.False(all.CacheHit);
        Assert.Contains("extra", File.ReadAllText(all.GeneratedFile));
    }

    [Fact]
    public async System.Threading.Tasks.Task Compressed_inputs_and_parquet_use_shared_inference()
    {
        await using (var file = File.Create(Path.Combine(_directory, "sample.json.gz")))
        await using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
            await gzip.WriteAsync("[{\"id\":1}]"u8.ToArray());
        Assert.Contains("long id", File.ReadAllText(Run("sample.json.gz").GeneratedFile));
        await using (var file = File.Create(Path.Combine(_directory, "sample.parquet")))
        await using (await ParquetWriter.CreateAsync(new ParquetSchema(new DataField<int>("id")), file)) { }
        Assert.Contains("int id", File.ReadAllText(Run("sample.parquet").GeneratedFile));
    }

    private GenerateExampleType CreateTask(string settings) => new()
    {
        BuildEngine = new BuildEngine(),
        Settings = settings,
        ProjectDirectory = _directory,
        ProjectFile = Path.Combine(_directory, "app.cs"),
        CacheDirectory = "obj/dottype"
    };

    private GenerateExampleType Run(string settings = "sample.json")
    {
        GenerateExampleType task = CreateTask(settings);
        Assert.True(task.Execute(), string.Join("\n", ((BuildEngine)task.BuildEngine).Errors.Select(e => e.Message)));
        return task;
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private sealed class BuildEngine : IBuildEngine
    {
        public List<BuildErrorEventArgs> Errors { get; } = [];
        public bool ContinueOnError => false;
        public int LineNumberOfTaskNode => 0;
        public int ColumnNumberOfTaskNode => 0;
        public string ProjectFileOfTaskNode => "app.cs";
        public void LogErrorEvent(BuildErrorEventArgs e) => Errors.Add(e);
        public void LogWarningEvent(BuildWarningEventArgs e) { }
        public void LogMessageEvent(BuildMessageEventArgs e) { }
        public void LogCustomEvent(CustomBuildEventArgs e) { }
        public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs) => throw new NotSupportedException();
    }
}
