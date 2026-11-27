using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace dottype.Build;

internal sealed record GenerationResult(string ExampleFile, string GeneratedFile, string CacheFile, string Source, bool CacheHit);

internal static class GenerationCache
{
    public static async Task<GenerationResult> GenerateAsync(CliOptions generation, string projectDirectory,
        string projectFile, string cacheDirectory, CancellationToken cancellationToken = default)
    {
        string exampleFile = generation.InputPath;
        string directory = Path.GetFullPath(cacheDirectory, projectDirectory);
        Directory.CreateDirectory(directory);
        string identity = Hash(JsonSerializer.Serialize(new[] { Path.GetFullPath(projectFile), exampleFile,
            generation.Namespace, generation.TypeName ?? generation.DefaultTypeName }));
        string generatedFile = Path.Combine(directory, identity + ".g.cs");
        string cacheFile = Path.Combine(directory, identity + ".cache.json");
        await using FileStream gate = await AcquireLockAsync(Path.Combine(directory, identity + ".lock"), cancellationToken);
        InputStamp stamp = ReadStamp(exampleFile);
        InputFormat format = generation.ResolveFormat();
        string fingerprint = Hash(JsonSerializer.Serialize(new
        {
            Input = exampleFile,
            File = stamp,
            generation.TypeName,
            generation.Namespace,
            Format = format,
            Compression = generation.ResolveCompression(),
            generation.SampleRecords,
            generation.CsvHelperMap,
            generation.ColumnTypes,
            generation.NestChildTypes,
            Generator = typeof(GenerationCache).Assembly.ManifestModule.ModuleVersionId
        }));
        if (!generation.Force && File.Exists(cacheFile) && File.Exists(generatedFile))
        {
            CacheEntry? cached = null;
            try { cached = JsonSerializer.Deserialize<CacheEntry>(await File.ReadAllTextAsync(cacheFile, cancellationToken)); }
            catch (JsonException) { /* Regenerate a corrupt cache. */ }
            if (cached?.Fingerprint == fingerprint)
            {
                string cachedSource = await File.ReadAllTextAsync(generatedFile, cancellationToken);
                if (cached.SourceHash == Hash(cachedSource))
                    return new(exampleFile, generatedFile, cacheFile, cachedSource, true);
            }
        }
        await using Stream rawInput = File.OpenRead(exampleFile);
        await using Stream input = InputStreams.Decode(rawInput, generation.ResolveCompression());
        string source = await ExampleInference.GenerateAsync(input, format, generation, cancellationToken);
        if (ReadStamp(exampleFile) != stamp)
            throw new IOException("The example changed during inference. Save it and retry the build.");
        if (!File.Exists(generatedFile) || await File.ReadAllTextAsync(generatedFile, cancellationToken) != source)
            await WriteAtomicallyAsync(generatedFile, source, cancellationToken);
        await WriteAtomicallyAsync(cacheFile, JsonSerializer.Serialize(new CacheEntry(fingerprint, Hash(source))), cancellationToken);
        return new(exampleFile, generatedFile, cacheFile, source, false);
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static InputStamp ReadStamp(string path)
    {
        var file = new FileInfo(File.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName ?? path);
        return new(file.FullName, file.Length, file.LastWriteTimeUtc.Ticks);
    }

    private static async Task<FileStream> AcquireLockAsync(string path, CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (attempt < 100) { await Task.Delay(50, cancellationToken); }
        }
    }

    private static async Task WriteAtomicallyAsync(string path, string content, CancellationToken cancellationToken)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, content, new UTF8Encoding(false), cancellationToken);
            File.Move(temporary, path, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }

    private sealed record CacheEntry(string Fingerprint, string SourceHash);
    private sealed record InputStamp(string Path, long Length, long Modified);
}
