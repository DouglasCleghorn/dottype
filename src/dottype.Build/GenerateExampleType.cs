using System.Text.Json;
using Microsoft.Build.Framework;

namespace dottype.Build;

/// <summary>Generates types at build time and registers editor inputs at design time.</summary>
public sealed class GenerateExampleType : Microsoft.Build.Utilities.Task
{
    [Required]
    public string Settings { get; set; } = "";
    [Required]
    public string ProjectDirectory { get; set; } = "";
    [Required]
    public string ProjectFile { get; set; } = "";
    [Required]
    public string CacheDirectory { get; set; } = "";
    public bool DesignTimeBuild { get; set; }
    [Output]
    public string GeneratedFile { get; private set; } = "";
    [Output]
    public string ExampleFile { get; private set; } = "";
    [Output]
    public string CacheFile { get; private set; } = "";
    [Output]
    public bool CacheHit { get; private set; }

    public override bool Execute()
    {
        try
        {
            if (DesignTimeBuild)
            {
                // Register even a missing file so its creation can refresh the editor.
                ExampleFile = BuildOptions.Parse(Settings, ProjectDirectory, requireInputExists: false).InputPath;
                return true;
            }
            GenerationResult result = GenerationCache.GenerateAsync(Settings, ProjectDirectory, ProjectFile, CacheDirectory).GetAwaiter().GetResult();
            ExampleFile = result.ExampleFile;
            GeneratedFile = result.GeneratedFile;
            CacheFile = result.CacheFile;
            CacheHit = result.CacheHit;
            Log.LogMessage(MessageImportance.Low, "dottype: {0} types for {1}", CacheHit ? "cached" : "inferred", ExampleFile);
            return true;
        }
        catch (Exception exception) when (exception is CliException or IOException or UnauthorizedAccessException
            or InvalidDataException or JsonException or ArgumentException or InvalidOperationException or OverflowException)
        {
            if (DesignTimeBuild) return true; // The generator supplies the editor diagnostic.
            string diagnosticFile = ProjectFile;
            if (!File.Exists(diagnosticFile) && diagnosticFile.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                string entryPoint = diagnosticFile[..^7];
                if (!File.Exists(entryPoint)) entryPoint = Path.ChangeExtension(diagnosticFile, ".cs");
                if (File.Exists(entryPoint)) diagnosticFile = entryPoint;
            }
            Log.LogError(null, "dottype001", null, diagnosticFile, 1, 1, 1, 1,
                "dottype: {0} Check the dottype property and its example file.", exception.Message);
            return false;
        }
    }
}
