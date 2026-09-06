namespace dottype;

internal static class App
{
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        Stream? standardInput = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (CliOptions.Complete(args, output) is int completionExitCode)
                return completionExitCode;

            CliOptions options = CliOptions.Parse(args);
            if (options.ShowHelp)
            {
                await output.WriteLineAsync(CliOptions.HelpText);
                return 0;
            }

            InputFormat format = options.ResolveFormat();
            await using Stream rawInput = options.InputPath == "-"
                ? standardInput ?? Console.OpenStandardInput()
                : File.OpenRead(options.InputPath);
            await using Stream input = InputStreams.Decode(rawInput, options.ResolveCompression());
            string defaultName = options.DefaultTypeName;

            PocoSchema schema;
            try
            {
                schema = format switch
                {
                    InputFormat.Json => await JsonSchemaReader.ReadAsync(input, options.TypeName ?? defaultName, options.SampleRecords, cancellationToken),
                    InputFormat.Csv => await CsvSchemaReader.ReadAsync(input, options.TypeName ?? defaultName, options.SampleRecords, cancellationToken),
                    InputFormat.Parquet => await ParquetSchemaReader.ReadAsync(input, options.TypeName ?? defaultName, cancellationToken),
                    _ => throw new System.Diagnostics.UnreachableException()
                };
            }
            catch (InvalidOperationException exception) when (options.ResolveCompression() == InputCompression.Brotli)
            {
                throw new InvalidDataException($"Invalid Brotli input: {exception.Message}", exception);
            }

            string source = CSharpWriter.Write(schema, options.Namespace);
            if (options.OutputPath is null or "-")
            {
                await output.WriteAsync(source);
            }
            else
            {
                string inputFullPath = options.InputPath == "-" ? "" : Path.GetFullPath(options.InputPath);
                string outputFullPath = Path.GetFullPath(options.OutputPath);
                StringComparison pathComparison = OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;
                if (string.Equals(inputFullPath, outputFullPath, pathComparison))
                {
                    throw new CliException("The output path cannot overwrite the input file.");
                }

                string? directory = Path.GetDirectoryName(outputFullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Replacing the directory entry avoids following a symlink/hard-link
                // alias to the input and keeps an existing output intact on failure.
                string temporaryPath = Path.Combine(directory!, $".dottype-{Guid.NewGuid():N}.tmp");
                try
                {
                    await using (var temporary = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write))
                    await using (var writer = new StreamWriter(temporary))
                    {
                        await writer.WriteAsync(source.AsMemory(), cancellationToken);
                    }
                    File.Move(temporaryPath, outputFullPath, overwrite: true);
                }
                finally
                {
                    File.Delete(temporaryPath);
                }
            }

            return 0;
        }
        catch (CliException exception)
        {
            await error.WriteLineAsync($"dottype: {exception.Message}");
            await error.WriteLineAsync("Try 'dottype --help' for usage.");
            return 2;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            await error.WriteLineAsync($"dottype: {exception.Message}");
            return 1;
        }
    }
}
