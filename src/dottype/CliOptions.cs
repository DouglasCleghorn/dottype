using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Text;

namespace dottype;

internal enum InputFormat
{
    Json,
    Csv,
    Parquet
}

internal sealed record CliOptions(
    string InputPath,
    string? OutputPath,
    string? TypeName,
    string? Namespace,
    InputFormat? Format,
    bool ShowHelp,
    InputCompression? Compression = null,
    int? SampleRecords = CliOptions.DefaultSampleRecords,
    bool Force = false)
{
    public const int DefaultSampleRecords = 1000;

    private static readonly Lazy<string> Help = new(CreateHelp);
    public static string HelpText => Help.Value;

    // A fresh command keeps concurrent compiler and build parsing independent.
    // Use Command so no process-dependent name, version action, or directives are added.
    private sealed class CommandDefinition
    {
        public Command Command { get; } = new("dottype", "Generate C# types from example JSON, CSV, or Parquet data.");
        public Argument<string?> Input { get; } = new("input")
        {
            HelpName = "input",
            Description = "Input file, or - for stdin.",
            Arity = ArgumentArity.ZeroOrOne
        };
        public Option<string?> Output { get; } = new("--output", "-o")
        {
            HelpName = "path",
            Description = "Write source to a file; omitted or - means stdout."
        };
        public Option<string?> Name { get; } = new("--name", "-n")
        {
            HelpName = "[namespace.]type",
            Description = "Example: Models.Customer. Default: filename, or Root for stdin; global namespace."
        };
        public Option<string?> Format { get; } = new("--format", "-f")
        {
            HelpName = "format",
            Arity = ArgumentArity.ExactlyOne,
            Description = "json, csv, parquet/parq (lowercase); inferred from extension, required for stdin."
        };
        public Option<string?> Compression { get; } = new("--compression")
        {
            HelpName = "kind",
            Arity = ArgumentArity.ExactlyOne,
            Description = "auto (default), none, gzip/gz, brotli/br (lowercase)."
        };
        public Option<string> SampleRecords { get; } = new("--sample", "--sample-records")
        {
            HelpName = "n|all",
            Arity = ArgumentArity.ExactlyOne,
            Description = "First n complete records; all for full input (lowercase).",
            DefaultValueFactory = _ => DefaultSampleRecords.ToString(CultureInfo.InvariantCulture)
        };
        public Option<bool> Help { get; } = new("--help", "-h")
        {
            Arity = ArgumentArity.Zero,
            Description = "Show help without generating types."
        };
        public Option<bool> Force { get; } = new("--force")
        {
            Arity = ArgumentArity.Zero,
            Description = "Bypass the build cache; remove after refreshing."
        };

        public CommandDefinition(bool buildIntegration)
        {
            Format.AcceptOnlyFromAmong("json", "csv", "parquet", "parq");
            Compression.AcceptOnlyFromAmong("auto", "none", "gzip", "gz", "brotli", "br");
            SampleRecords.CompletionSources.Add("all", DefaultSampleRecords.ToString(CultureInfo.InvariantCulture));
            Input.CompletionSources.Add(CompletePaths);
            Output.CompletionSources.Add(CompletePaths);

            Command.Arguments.Add(Input);
            foreach (Option option in new Option[] { Output, Name, Format, Compression, SampleRecords, Help, Force })
                Command.Options.Add(option);

            // Recognize unavailable options to give the same actionable error for aliases and = syntax.
            foreach (Option option in new Option[] { Output, Help })
            {
                option.Hidden = buildIntegration;
                if (buildIntegration) option.Validators.Add(result =>
                {
                    if (!result.Implicit) result.AddError(BuildOnlyInputError(option.Name).Message);
                });
            }
            Force.Hidden = !buildIntegration;
            if (!buildIntegration)
                Force.Validators.Add(result =>
                {
                    if (!result.Implicit) result.AddError("--force applies only to the build cache; the CLI always generates.");
                });
        }
    }

    // Called only by the CLI, before generation validation or any input/output streams are opened.
    public static int? Complete(IReadOnlyList<string> args, TextWriter output)
    {
        if (args.Count == 0 || !(args[0] == "[suggest]" ||
            (args[0].StartsWith("[suggest:", StringComparison.Ordinal) && args[0].EndsWith(']'))))
            return null;

        if (args.Count > 2) throw new CliException("Completion expects one quoted command line after [suggest].");
        string commandLine = args.Count == 2 ? args[1] : "dottype ";
        int position = commandLine.Length;
        if (args[0] != "[suggest]" &&
            (!int.TryParse(args[0].AsSpan(9, args[0].Length - 10), NumberStyles.None,
                CultureInfo.InvariantCulture, out position) || position < 0 || position > commandLine.Length))
            throw new CliException("Completion cursor must be a position within the command line.");

        var definition = new CommandDefinition(buildIntegration: false);
        // The 2.0.11 text completion context splits on spaces without respecting quotes or '='.
        // Tokenize only up to the cursor instead, preserving a new word after unquoted whitespace.
        // The first token is the executable, which may be a full path supplied by a shell.
        List<string> words = CompletionWords(commandLine[..position]);
        ParseResult result = definition.Command.Parse(words.Skip(1).ToArray(), CreateParserConfiguration());
        Token? last = result.Tokens.Count == 0 ? null : result.Tokens[^1];
        Option? valueOption = definition.Command.Options.FirstOrDefault(option =>
            last is not null && result.GetResult(option)?.Tokens.Contains(last) == true);
        IEnumerable<CompletionItem> completions;
        if (result.Tokens.Take(result.Tokens.Count - 1).Any(token => token.Type == TokenType.DoubleDash))
            completions = definition.Input.GetCompletions(result.GetCompletionContext());
        else if (valueOption is not null)
            completions = valueOption.GetCompletions(result.GetCompletionContext());
        else
            completions = result.GetCompletions();
        foreach (CompletionItem completion in completions) output.WriteLine(completion.Label);
        return 0;
    }

    private static List<string> CompletionWords(string prefix)
    {
        // Completion must tolerate unfinished quotes. Backslashes remain literal for Windows paths.
        var words = new List<string>();
        var word = new StringBuilder();
        char quote = '\0';
        bool started = false;
        foreach (char character in prefix)
        {
            if (quote != '\0')
            {
                if (character == quote) quote = '\0';
                else word.Append(character);
            }
            else if (character is '\'' or '"') quote = character;
            else if (char.IsWhiteSpace(character))
            {
                if (started) words.Add(word.ToString());
                word.Clear();
                started = false;
                continue;
            }
            else word.Append(character);
            started = true;
        }
        words.Add(word.ToString());
        return words;
    }

    private static List<string> CompletePaths(CompletionContext context)
    {
        string word = context.WordToComplete;
        var matches = new List<string>();
        if (word.Length == 0 || word == "-") matches.Add("-");
        try
        {
            string directory = Path.GetDirectoryName(word) ?? "";
            string prefix = Path.GetFileName(word);
            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory.Length == 0 ? "." : directory))
            {
                string name = Path.GetFileName(entry);
                if (!name.StartsWith(prefix, comparison)) continue;
                string completion = word[..^prefix.Length] + name;
                if (Directory.Exists(entry)) completion += Path.DirectorySeparatorChar;
                matches.Add(completion);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // An incomplete or inaccessible directory should simply yield no path suggestions.
        }
        return matches;
    }

    private static ParserConfiguration CreateParserConfiguration() => new()
    {
        EnablePosixBundling = false,
        // Settings parsing must not read unrelated @files inside an editor or MSBuild process.
        ResponseFileTokenReplacer = null
    };

    private static int? ParseSampleRecords(string value)
    {
        if (value == "all") return null;
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int count) && count > 0)
            return count;
        throw new CliException("--sample must be a positive record count or 'all'.");
    }

    private static (string? TypeName, string? Namespace) SplitQualifiedName(string? name)
    {
        int separator = name?.LastIndexOf('.') ?? -1;
        return separator < 0 ? (name, null) : (name![(separator + 1)..], name[..separator]);
    }

    private static string CreateHelp()
    {
        var definition = new CommandDefinition(buildIntegration: false);
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        definition.Help.Action = new HelpAction { MaxWidth = 120 };
        definition.Command.Parse(["--help"]).Invoke(new InvocationConfiguration { Output = text, Error = text });
        text.Write($"""
            Input is required unless help is requested.
            Build only: {definition.Force.Name} — {definition.Force.Description}

            Examples:
              dottype customer.json --name Models.Customer --output Customer.cs
              <producer> | dottype - --format json

            Exit codes: 0 success, 1 invalid data/I/O failure, 2 invalid usage.
            Tab completion: see USAGE.md for shell setup; the CLI supports [suggest].
            Docs: USAGE.md and llms.txt in the package, or https://github.com/DouglasCleghorn/dottype
            """);
        return text.ToString();
    }

    public static CliOptions Parse(IReadOnlyList<string> args, bool requireInputExists = true,
        bool buildIntegration = false, string? baseDirectory = null)
    {
        var definition = new CommandDefinition(buildIntegration);
        ParseResult result = definition.Command.Parse(args, CreateParserConfiguration());
        if (result.Errors.Count != 0) throw new CliException(string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
        if (result.Tokens.Any(token => token.Type == TokenType.Directive))
            throw new CliException("Command-line directives are not supported.");

        string? input = result.GetValue(definition.Input);
        Token? inputToken = result.GetResult(definition.Input)?.Tokens is { Count: > 0 } inputTokens ? inputTokens[0] : null;
        // System.CommandLine can bind an unknown option to a positional string argument.
        // Preserve usage errors while allowing literal dash-prefixed paths after --.
        if (input is { Length: > 1 } && input[0] == '-' &&
            !result.Tokens.TakeWhile(token => token != inputToken).Any(token => token.Type == TokenType.DoubleDash))
            throw new CliException($"Unknown option '{input}'.");
        var (name, namespaceName) = SplitQualifiedName(result.GetValue(definition.Name));
        InputFormat? format = result.GetValue(definition.Format) switch
        {
            null => null,
            "json" => InputFormat.Json,
            "csv" => InputFormat.Csv,
            "parquet" or "parq" => InputFormat.Parquet,
            var value => throw new CliException($"Unsupported format '{value}'.")
        };
        InputCompression? compression = result.GetValue(definition.Compression) switch
        {
            null or "auto" => null,
            "none" => InputCompression.None,
            "gzip" or "gz" => InputCompression.Gzip,
            "brotli" or "br" => InputCompression.Brotli,
            var value => throw new CliException($"Unsupported compression '{value}'.")
        };
        var options = new CliOptions(input ?? "", result.GetValue(definition.Output), name,
            namespaceName, format, result.GetValue(definition.Help), compression,
            ParseSampleRecords(result.GetValue(definition.SampleRecords)!), result.GetValue(definition.Force));
        if (options.ShowHelp) return options;
        if (string.IsNullOrEmpty(input)) throw new CliException("Missing input file.");
        if (buildIntegration && input == "-") throw BuildOnlyInputError(input);
        if (input != "-" && baseDirectory is not null) input = Path.GetFullPath(input, baseDirectory);
        if (requireInputExists && input != "-" && !File.Exists(input))
            throw new CliException($"Input file '{input}' does not exist.");
        if (options.TypeName is not null && string.IsNullOrWhiteSpace(options.TypeName))
            throw new CliException("The type name cannot be empty.");
        if (options.Namespace is not null && !Identifier.IsValidNamespace(options.Namespace))
            throw new CliException($"'{options.Namespace}' is not a valid C# namespace.");
        return options with { InputPath = input };
    }

    private static CliException BuildOnlyInputError(string argument)
        => new($"'{argument}' is only available in the CLI. Build integration takes one example file and manages output automatically.");

    public InputCompression ResolveCompression() => Compression ?? InputStreams.InferCompression(InputPath);

    public string DataPath => InputStreams.InferCompression(InputPath) == InputCompression.None
        ? InputPath
        : InputPath[..^Path.GetExtension(InputPath).Length];

    public string DefaultTypeName => InputPath == "-" ? "Root" : Path.GetFileNameWithoutExtension(DataPath);

    public InputFormat ResolveFormat()
    {
        if (Format is not null) return Format.Value;
        if (InputPath == "-") throw new CliException("--format is required when reading from stdin.");
        return Path.GetExtension(DataPath).ToLowerInvariant() switch
        {
            ".json" => InputFormat.Json,
            ".csv" => InputFormat.Csv,
            ".parquet" or ".parq" => InputFormat.Parquet,
            var extension => throw new CliException($"Cannot infer a format from extension '{extension}'.")
        };
    }
}

internal sealed class CliException(string message) : Exception(message);
