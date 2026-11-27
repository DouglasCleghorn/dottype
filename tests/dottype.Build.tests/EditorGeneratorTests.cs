extern alias generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Parquet;
using Parquet.Schema;
using ExampleGenerator = generator::dottype.Generator.ExampleGenerator;

namespace dottype.Build.tests;

public sealed class EditorGeneratorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dottype-generator-tests", Guid.NewGuid().ToString("N"));
    public EditorGeneratorTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void Editor_generates_before_build_and_refreshes_when_the_example_changes()
    {
        string file = Path.Combine(_directory, "sample.json");
        File.WriteAllText(file, "{\"id\":1}");
        var additional = new DiskFile(file);
        GeneratorDriver driver = Driver("sample.json --name=Acme.Models.Customer", additional);
        var compilation = CSharpCompilation.Create("App");
        driver = driver.RunGenerators(compilation);
        Assert.Contains("long id", Source(driver));
        Assert.Contains("namespace Acme.Models;", Source(driver));
        Assert.Contains("class Customer", Source(driver));
        File.WriteAllText(file, "{\"displayName\":\"Alice\"}");
        driver = driver.ReplaceAdditionalText(additional, new DiskFile(file)).RunGenerators(compilation);
        Assert.Contains("string displayName", Source(driver));
        Assert.DoesNotContain("long id", Source(driver));
    }

    [Fact]
    public void Editor_emits_the_csv_map_and_pinned_columns()
    {
        string file = Path.Combine(_directory, "sample.csv");
        File.WriteAllText(file, "Invoice ID,Total\n42,12.50\n");
        GeneratorDriver driver = Driver("sample.csv --name Models.Invoice --csvhelper-map --column-type 'Invoice ID=string'",
            new DiskFile(file)).RunGenerators(CSharpCompilation.Create("App"));
        string source = Source(driver);
        Assert.Contains("string Invoice_ID", source);
        Assert.Contains("class InvoiceMap : global::CsvHelper.Configuration.ClassMap<Invoice>", source);
        Assert.Contains("Map(row => row.Invoice_ID).Name(\"Invoice ID\")", source);
    }

    [Fact]
    public void Editor_reports_invalid_input_and_removes_previous_generated_type()
    {
        string file = Path.Combine(_directory, "sample.json");
        File.WriteAllText(file, "{}");
        var additional = new DiskFile(file);
        var compilation = CSharpCompilation.Create("App");
        GeneratorDriver driver = Driver("sample.json", additional).RunGenerators(compilation);
        Assert.NotEmpty(Source(driver));
        File.WriteAllText(file, "invalid");
        driver = driver.ReplaceAdditionalText(additional, new DiskFile(file)).RunGenerators(compilation);
        GeneratorDriverRunResult result = driver.GetRunResult();
        Assert.Empty(result.GeneratedTrees);
        Assert.Equal("dottype001", Assert.Single(result.Diagnostics).Id);
    }

    [Theory]
    [InlineData("missing.json")]
    [InlineData("sample.json --sample-records 0")]
    public void Editor_reports_errors_even_without_a_registered_file(string settings)
    {
        GeneratorDriverRunResult result = Driver(settings).RunGenerators(CSharpCompilation.Create("App")).GetRunResult();
        Assert.Empty(result.GeneratedTrees);
        Assert.Equal("dottype001", Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public void Regular_builds_do_not_emit_duplicate_types_from_the_editor_generator()
    {
        File.WriteAllText(Path.Combine(_directory, "sample.json"), "{}");
        GeneratorDriverRunResult result = Driver("sample.json", designTime: false).RunGenerators(CSharpCompilation.Create("App")).GetRunResult();
        Assert.Empty(result.GeneratedTrees);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async System.Threading.Tasks.Task Editor_reads_binary_parquet_without_treating_it_as_text()
    {
        string file = Path.Combine(_directory, "sample.parquet");
        await using (var output = File.Create(file))
        await using (await ParquetWriter.CreateAsync(new ParquetSchema(new DataField<int>("id")), output)) { }
        GeneratorDriver driver = Driver("sample.parquet", new DiskFile(file)).RunGenerators(CSharpCompilation.Create("App"));
        Assert.Contains("int id", Source(driver));
    }

    private CSharpGeneratorDriver Driver(string settings, AdditionalText? additional = null, bool designTime = true)
        => Driver([new(null, settings)], additional is null ? [] : [additional], designTime);

    private CSharpGeneratorDriver Driver(ExampleInput[] inputs, AdditionalText[] files, bool designTime = true)
        => CSharpGeneratorDriver.Create([new ExampleGenerator().AsSourceGenerator()],
            additionalTexts: files,
            optionsProvider: new OptionsProvider(new Dictionary<string, string>
            {
                ["build_property.dottypeInputs"] = ExampleInputs.Encode(inputs),
                ["build_property.dottypeProjectDirectory"] = _directory,
                ["build_property.dottypeProjectFile"] = Path.Combine(_directory, "app.cs"),
                ["build_property.dottypeCacheDirectory"] = Path.Combine(_directory, "obj"),
                ["build_property.dottypeDesignTime"] = designTime ? "true" : "false"
            }));

    [Fact]
    public void Editor_keeps_other_models_when_one_file_has_an_error_and_recovers_on_save()
    {
        string good = Path.Combine(_directory, "one.json");
        string bad = Path.Combine(_directory, "two.ndjson");
        File.WriteAllText(good, "{\"name\":\"a\"}");
        File.WriteAllText(bad, "{}\ninvalid");
        var changing = new DiskFile(bad);
        var compilation = CSharpCompilation.Create("App");
        GeneratorDriver driver = Driver([new(good, "--name Good"), new(bad, "--name Events")], [new DiskFile(good), changing])
            .RunGenerators(compilation);
        var result = driver.GetRunResult();
        Assert.Contains("class Good", Assert.Single(result.GeneratedTrees).ToString());
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("dottype001", diagnostic.Id);
        Assert.Equal(bad, diagnostic.Location.GetLineSpan().Path);
        Assert.Contains("line 2", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture));
        File.WriteAllText(bad, "{\"count\":2}\n");
        result = driver.ReplaceAdditionalText(changing, new DiskFile(bad)).RunGenerators(compilation).GetRunResult();
        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, result.GeneratedTrees.Length);
        Assert.Contains(result.GeneratedTrees, tree => tree.ToString().Contains("long count", StringComparison.Ordinal));
    }

    private static string Source(GeneratorDriver driver)
    {
        GeneratorDriverRunResult result = driver.GetRunResult();
        Assert.Empty(result.Diagnostics);
        return Assert.Single(result.GeneratedTrees).GetText().ToString();
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private sealed class DiskFile(string path) : AdditionalText
    {
        public override string Path => path;
        public override SourceText? GetText(CancellationToken cancellationToken = default)
            => File.Exists(path) && Path.EndsWith(".json", StringComparison.Ordinal) ? SourceText.From(File.ReadAllText(path)) : null;
    }

    private sealed class Options(Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value) => values.TryGetValue(key, out value!);
    }

    private sealed class OptionsProvider(Dictionary<string, string> values) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Options(values);
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new Options([]);
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
            => new Options(new Dictionary<string, string> { ["build_metadata.AdditionalFiles.dottypeExample"] = "true" });
    }
}
