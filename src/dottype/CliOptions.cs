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
    InputCompression? Compression = null)
{
    public const string HelpText = """
        Generate a minimal C# POCO from a JSON, CSV, or Parquet file.

        Usage:
          dottype <input> [options]
          <producer> | dottype - --format <json|csv|parquet> [options]

        Arguments:
          <input>                  Input file, or - for stdin

        Options:
          -o, --output <path>      Write to a file instead of stdout; - means stdout
          -n, --name <name>        Root C# type name (default: input file name)
              --namespace <name>   Wrap generated types in this namespace
          -f, --format <format>    Required for stdin; otherwise inferred from extension
              --compression <kind> auto (default), none, gzip/gz, or brotli/br
          -h, --help               Show help

        Files ending in .gz or .br are decompressed before reading their inner format.
        For compressed stdin, specify both --format and --compression.
        """;

    public static CliOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            throw new CliException("Missing input file.");
        }

        string? input = null;
        string? output = null;
        string? name = null;
        string? namespaceName = null;
        InputFormat? format = null;
        InputCompression? compression = null;
        bool help = false;

        for (int index = 0; index < args.Count; index++)
        {
            string argument = args[index];
            switch (argument)
            {
                case "-h" or "--help":
                    help = true;
                    break;
                case "-o" or "--output":
                    output = ReadValue(args, ref index, argument);
                    break;
                case "-n" or "--name":
                    name = ReadValue(args, ref index, argument);
                    break;
                case "--namespace":
                    namespaceName = ReadValue(args, ref index, argument);
                    break;
                case "-f" or "--format":
                    string value = ReadValue(args, ref index, argument);
                    format = value.ToLowerInvariant() switch
                    {
                        "json" => InputFormat.Json,
                        "csv" => InputFormat.Csv,
                        "parquet" or "parq" => InputFormat.Parquet,
                        _ => throw new CliException($"Unsupported format '{value}'.")
                    };
                    break;
                case "-":
                    if (input is not null)
                    {
                        throw new CliException("Only one input is allowed.");
                    }
                    input = argument;
                    break;
                case "--compression":
                    string compressionValue = ReadValue(args, ref index, argument);
                    compression = compressionValue.ToLowerInvariant() switch
                    {
                        "auto" => null,
                        "none" => InputCompression.None,
                        "gzip" or "gz" => InputCompression.Gzip,
                        "brotli" or "br" => InputCompression.Brotli,
                        _ => throw new CliException($"Unsupported compression '{compressionValue}'.")
                    };
                    break;
                default:
                    if (argument.Length > 0 && argument[0] == '-')
                    {
                        throw new CliException($"Unknown option '{argument}'.");
                    }
                    if (input is not null)
                    {
                        throw new CliException("Only one input is allowed.");
                    }
                    input = argument;
                    break;
            }
        }

        if (help)
        {
            return new CliOptions(input ?? "", output, name, namespaceName, format, true, compression);
        }
        if (input is null)
        {
            throw new CliException("Missing input file.");
        }
        if (input != "-" && !File.Exists(input))
        {
            throw new CliException($"Input file '{input}' does not exist.");
        }
        if (name is not null && string.IsNullOrWhiteSpace(name))
        {
            throw new CliException("The type name cannot be empty.");
        }
        if (namespaceName is not null && !Identifier.IsValidNamespace(namespaceName))
        {
            throw new CliException($"'{namespaceName}' is not a valid C# namespace.");
        }

        return new CliOptions(input, output, name, namespaceName, format, false, compression);
    }

    public InputCompression ResolveCompression() => Compression ?? InputStreams.InferCompression(InputPath);

    public string DataPath => InputStreams.InferCompression(InputPath) == InputCompression.None
        ? InputPath
        : InputPath[..^Path.GetExtension(InputPath).Length];

    public string DefaultTypeName => InputPath == "-" ? "Root" : Path.GetFileNameWithoutExtension(DataPath);

    public InputFormat ResolveFormat()
    {
        if (Format is not null)
        {
            return Format.Value;
        }
        if (InputPath == "-")
        {
            throw new CliException("--format is required when reading from stdin.");
        }

        return Path.GetExtension(DataPath).ToLowerInvariant() switch
        {
            ".json" => InputFormat.Json,
            ".csv" => InputFormat.Csv,
            ".parquet" or ".parq" => InputFormat.Parquet,
            var extension => throw new CliException($"Cannot infer a format from extension '{extension}'.")
        };
    }

    private static string ReadValue(IReadOnlyList<string> args, ref int index, string option)
    {
        index++;
        if (index >= args.Count)
        {
            throw new CliException($"Option '{option}' requires a value.");
        }
        return args[index];
    }
}

internal sealed class CliException(string message) : Exception(message);
