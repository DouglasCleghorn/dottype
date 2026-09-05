using System.IO.Compression;

namespace Dottype;

internal enum InputCompression
{
    None,
    Gzip,
    Brotli
}

internal static class InputStreams
{
    public static InputCompression InferCompression(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".gz" => InputCompression.Gzip,
        ".br" => InputCompression.Brotli,
        _ => InputCompression.None
    };

    // The caller owns input; a decoder never closes the underlying stream.
    public static Stream Decode(Stream input, InputCompression compression) => compression switch
    {
        InputCompression.None => input,
        InputCompression.Gzip => new GZipStream(input, CompressionMode.Decompress, leaveOpen: true),
        InputCompression.Brotli => new BrotliStream(input, CompressionMode.Decompress, leaveOpen: true),
        _ => throw new System.Diagnostics.UnreachableException()
    };
}
