using System.Text;
using System.Text.Json;
using dottype.Build;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace dottype.Generator;

/// <summary>Provides live example types and diagnostics to the editor.</summary>
[Generator]
public sealed class ExampleGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor InvalidExample = new("dottype001", "Cannot generate example type",
        "dottype: {0} Check the dottype property and its example file.", "dottype", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var options = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) => new Settings(
            Get(provider, "dottype"), Get(provider, "dottypeProjectDirectory"),
            Get(provider, "dottypeProjectFile"), Get(provider, "dottypeCacheDirectory"),
            Get(provider, "dottypeDesignTime") == "true"));
        // Observe the host snapshot so Roslyn tracks external file changes. Binary
        // inputs may have no text snapshot; inference always uses our own readers.
        var examples = context.AdditionalTextsProvider.Combine(context.AnalyzerConfigOptionsProvider)
            .Where(static pair => pair.Right.GetOptions(pair.Left).TryGetValue("build_metadata.AdditionalFiles.dottypeExample", out string? value) && value == "true")
            .Select(static (pair, cancellationToken) => Observe(pair.Left, cancellationToken)).Collect();
        var source = examples.Combine(options).Select(static (pair, cancellationToken) =>
        {
            Settings settings = pair.Right;
            if (!settings.DesignTime || string.IsNullOrWhiteSpace(settings.Value)) return new Result(null, null);
            try
            {
                GenerationResult result = GenerationCache.GenerateAsync(settings.Value, settings.Directory, settings.Project,
                    Path.Combine(settings.Cache, "editor"), cancellationToken).GetAwaiter().GetResult();
                return new Result(result.Source, null);
            }
            catch (Exception exception) when (exception is CliException or IOException or UnauthorizedAccessException
                or InvalidDataException or JsonException or ArgumentException or InvalidOperationException or OverflowException)
            {
                string file = settings.Project;
                if (!File.Exists(file) && file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                {
                    file = File.Exists(file[..^7]) ? file[..^7] : Path.ChangeExtension(file, ".cs");
                }
                return new Result(null, exception.Message, file);
            }
        });
        context.RegisterSourceOutput(source, static (output, result) =>
        {
            if (result.Error is not null)
            {
                Location location = string.IsNullOrEmpty(result.File) ? Location.None
                    : Location.Create(result.File, default, new LinePositionSpan(default, default));
                output.ReportDiagnostic(Diagnostic.Create(InvalidExample, location, result.Error));
            }
            if (result.Source is not null) output.AddSource("dottype.Example.g.cs", SourceText.From(result.Source, Encoding.UTF8));
        });
    }

    private static string Get(AnalyzerConfigOptionsProvider provider, string name)
        => provider.GlobalOptions.TryGetValue("build_property." + name, out string? value) ? value : "";

    private static ObservedExample Observe(AdditionalText file, CancellationToken cancellationToken)
    {
        SourceText? text = null;
        try { text = file.GetText(cancellationToken); }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException) { }
        return new ObservedExample(file, text);
    }

    private sealed record ObservedExample(AdditionalText File, SourceText? Text);
    private sealed record Settings(string Value, string Directory, string Project, string Cache, bool DesignTime);
    private sealed record Result(string? Source, string? Error, string? File = null);
}
