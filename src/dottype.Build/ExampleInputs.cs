using System.Text.Json;

namespace dottype.Build;

// A null ItemPath denotes the single property/directive form, which includes its own path.
internal sealed record ExampleInput(string? ItemPath, string Settings);
internal sealed record ResolvedInput(string? ItemPath, CliOptions? Options, string? Error);

internal static class ExampleInputs
{
    // Analyzer config is line-oriented and interprets characters like '#' and ';'.
    // Transport the exact arguments, including quoted paths and CSV header names.
    public static string Encode(ExampleInput[] inputs) => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(inputs));
    public static ExampleInput[] Decode(string encoded) => JsonSerializer.Deserialize<ExampleInput[]>(Convert.FromBase64String(encoded))
        ?? throw new InvalidDataException("Missing dottype editor input settings.");

    public static ResolvedInput[] Resolve(ExampleInput[] inputs, string directory)
    {
        var resolved = new ResolvedInput[inputs.Length];
        var owners = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int index = 0; index < inputs.Length; index++)
        {
            ExampleInput input = inputs[index];
            try
            {
                CliOptions options = BuildOptions.Parse(input.Settings, directory, requireInputExists: false, itemPath: input.ItemPath);
                resolved[index] = new(input.ItemPath, options with { NestChildTypes = inputs.Length > 1 }, null);
                string root = Identifier.Unescaped(Identifier.MakeValid(options.TypeName ?? options.DefaultTypeName, "Root"));
                if (options.Namespace is not null)
                    root = string.Join('.', options.Namespace.Split('.').Select(Identifier.Unescaped)) + "." + root;
                Claim(root, index);
                if (options.CsvHelperMap) Claim(root + "Map", index);
            }
            catch (Exception e) when (IsGenerationError(e))
            {
                resolved[index] = new(input.ItemPath, null, e.Message);
            }
        }
        return resolved;

        void Claim(string name, int index)
        {
            if (owners.TryAdd(name, index)) return;
            int previous = owners[name];
            string error = $"Both '{resolved[previous].Options!.InputPath}' and '{resolved[index].Options!.InputPath}' generate '{name}'. Give each model a distinct --name.";
            resolved[index] = resolved[index] with { Error = error };
            resolved[previous] = resolved[previous] with { Error = error };
        }
    }

    public static bool IsGenerationError(Exception e) => e is CliException or IOException or UnauthorizedAccessException
        or InvalidDataException or JsonException or ArgumentException or InvalidOperationException or OverflowException or FormatException;

    public static string DiagnosticFile(string project)
    {
        if (File.Exists(project) || !project.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) return project;
        string entryPoint = File.Exists(project[..^7]) ? project[..^7] : Path.ChangeExtension(project, ".cs");
        return File.Exists(entryPoint) ? entryPoint : project;
    }
}
