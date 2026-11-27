using System.Collections.Immutable;
using System.Text;
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
        "dottype: {0} Check the dottype property or file options and the example data.", "dottype", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var options = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) => new Settings(
            Get(provider, "dottypeInputs"), Get(provider, "dottypeProjectDirectory"),
            Get(provider, "dottypeProjectFile"), Get(provider, "dottypeCacheDirectory"),
            Get(provider, "dottypeDesignTime") == "true"));
        // Observe the host snapshot so Roslyn tracks external file changes. Binary
        // inputs may have no text snapshot; inference always uses our own readers.
        var examples = context.AdditionalTextsProvider.Combine(context.AnalyzerConfigOptionsProvider)
            .Where(static pair => pair.Right.GetOptions(pair.Left).TryGetValue("build_metadata.AdditionalFiles.dottypeExample", out string? value) && value == "true")
            .Select(static (pair, cancellationToken) => Observe(pair.Left, cancellationToken)).Collect();
        var source = examples.Combine(options).SelectMany(static (pair, cancellationToken) => Generate(pair.Right, cancellationToken));
        context.RegisterSourceOutput(source, static (output, result) =>
        {
            if (result.Error is not null)
            {
                Location location = string.IsNullOrEmpty(result.File) ? Location.None
                    : Location.Create(result.File, default, new LinePositionSpan(default, default));
                output.ReportDiagnostic(Diagnostic.Create(InvalidExample, location, result.Error));
            }
            if (result.Source is not null) output.AddSource(result.Hint!, SourceText.From(result.Source, Encoding.UTF8));
        });
    }

    private static ImmutableArray<Result> Generate(Settings settings, CancellationToken cancellationToken)
    {
        if (!settings.DesignTime || string.IsNullOrEmpty(settings.Inputs)) return [];
        string project = ExampleInputs.DiagnosticFile(settings.Project);
        ResolvedInput[] inputs;
        try
        {
            inputs = ExampleInputs.Resolve(ExampleInputs.Decode(settings.Inputs), settings.Directory);
        }
        catch (Exception exception) when (ExampleInputs.IsGenerationError(exception))
        {
            return [new(null, null, exception.Message, project)];
        }
        var results = ImmutableArray.CreateBuilder<Result>(inputs.Length);
        foreach (ResolvedInput input in inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string file = input.ItemPath ?? project;
            if (input.Error is not null)
            {
                results.Add(new(null, null, input.Error, file));
                continue;
            }
            try
            {
                GenerationResult result = GenerationCache.GenerateAsync(input.Options!, settings.Directory, settings.Project,
                    Path.Combine(settings.Cache, "editor"), cancellationToken).GetAwaiter().GetResult();
                results.Add(new(Path.GetFileName(result.GeneratedFile), result.Source, null, null));
            }
            catch (Exception exception) when (ExampleInputs.IsGenerationError(exception))
            {
                results.Add(new(null, null, exception.Message, file));
            }
        }
        return results.MoveToImmutable();
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
    private sealed record Settings(string Inputs, string Directory, string Project, string Cache, bool DesignTime);
    private sealed record Result(string? Hint, string? Source, string? Error, string? File);
}
