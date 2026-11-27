using Microsoft.Build.Framework;

namespace dottype.Build;

/// <summary>Generates types at build time and registers editor inputs at design time.</summary>
public sealed class GenerateExampleType : Microsoft.Build.Utilities.Task
{
    public string Settings { get; set; } = "";
    public ITaskItem[] Examples { get; set; } = [];
    [Required]
    public string ProjectDirectory { get; set; } = "";
    [Required]
    public string ProjectFile { get; set; } = "";
    [Required]
    public string CacheDirectory { get; set; } = "";
    public bool DesignTimeBuild { get; set; }
    [Output]
    public string[] GeneratedFiles { get; private set; } = [];
    [Output]
    public string[] ExampleFiles { get; private set; } = [];
    [Output]
    public string[] CacheFiles { get; private set; } = [];
    [Output]
    public string EditorInputs { get; private set; } = "";
    [Output]
    public bool CacheHit { get; private set; }

    // Keep the task's single-example outputs available to existing targets.
    [Output]
    public string GeneratedFile => GeneratedFiles.Length == 1 ? GeneratedFiles[0] : "";
    [Output]
    public string ExampleFile => ExampleFiles.Length == 1 ? ExampleFiles[0] : "";
    [Output]
    public string CacheFile => CacheFiles.Length == 1 ? CacheFiles[0] : "";

    public override bool Execute()
    {
        if (Examples.Length > 0 && !string.IsNullOrEmpty(Settings))
        {
            LogError(ProjectFile, "Use either a dottype property or dottype file items, not both.");
            return false;
        }
        ExampleInput[] inputs = Examples.Length == 0 ? [new(null, Settings)] :
            Examples.Select(item => new ExampleInput(item.GetMetadata("FullPath"), item.GetMetadata("Options"))).ToArray();
        if (DesignTimeBuild) EditorInputs = ExampleInputs.Encode(inputs);
        ResolvedInput[] resolved = ExampleInputs.Resolve(inputs, ProjectDirectory);
        // Even missing files must be watched so creating them can refresh the editor.
        ExampleFiles = resolved.Select(r => r.Options?.InputPath ?? r.ItemPath).OfType<string>()
            .Distinct(StringComparer.Ordinal).ToArray();
        if (DesignTimeBuild) return true; // Roslyn supplies per-example sources and diagnostics.

        var results = new List<GenerationResult>();
        foreach (ResolvedInput input in resolved)
        {
            string diagnosticFile = input.ItemPath ?? ExampleInputs.DiagnosticFile(ProjectFile);
            if (input.Error is not null)
            {
                LogError(diagnosticFile, input.Error);
                continue;
            }
            try
            {
                var result = GenerationCache.GenerateAsync(input.Options!, ProjectDirectory, ProjectFile, CacheDirectory).GetAwaiter().GetResult();
                results.Add(result);
                Log.LogMessage(MessageImportance.Low, "dottype: {0} types for {1}", result.CacheHit ? "cached" : "inferred", result.ExampleFile);
            }
            catch (Exception exception) when (ExampleInputs.IsGenerationError(exception))
            {
                LogError(diagnosticFile, exception.Message);
            }
        }
        GeneratedFiles = results.Select(r => r.GeneratedFile).ToArray();
        CacheFiles = results.Select(r => r.CacheFile).ToArray();
        CacheHit = results.Count == inputs.Length && results.TrueForAll(r => r.CacheHit);
        return !Log.HasLoggedErrors;
    }

    private void LogError(string file, string message) =>
        Log.LogError(null, "dottype001", null, file, 1, 1, 1, 1,
            "dottype: {0} Check the dottype property or file options and the example data.", message);
}
