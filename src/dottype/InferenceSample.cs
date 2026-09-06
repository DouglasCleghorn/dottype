using System.Text;
using System.Text.Json;

namespace dottype;

internal static class InferenceSample
{
    // Stop exactly at the final sampled element. DeserializeAsyncEnumerable can
    // parse ahead into an invalid, deliberately unexamined tail before yielding.
    public static async Task<MemoryStream> ReadJsonAsync(Stream input, int records, CancellationToken cancellationToken)
    {
        var output = new MemoryStream();
        try
        {
            byte[] buffer = new byte[4096];
            int buffered = 0;
            int completed = 0;
            bool array = false;
            bool first = true;
            var state = new JsonReaderState();
            while (true)
            {
                int read = await input.ReadAsync(buffer.AsMemory(buffered), cancellationToken);
                buffered += read;
                bool final = read == 0;
                if (first && buffered < 3 && !final) continue;
                if (first && buffered >= 3 && buffer.AsSpan(0, 3).SequenceEqual(new byte[] { 0xef, 0xbb, 0xbf }))
                {
                    buffer.AsSpan(3, buffered - 3).CopyTo(buffer);
                    buffered -= 3;
                }
                first = false;
                (int consumed, bool done) = ScanJson(buffer.AsSpan(0, buffered), final, records, ref completed, ref array, ref state);
                output.Write(buffer, 0, consumed);
                if (done)
                {
                    output.WriteByte((byte)']');
                    break;
                }
                if (final) break;
                buffer.AsSpan(consumed, buffered - consumed).CopyTo(buffer);
                buffered -= consumed;
                if (buffered == buffer.Length) Array.Resize(ref buffer, checked(buffer.Length * 2));
            }
            output.Position = 0;
            return output;
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }

    private static (int Consumed, bool Done) ScanJson(ReadOnlySpan<byte> data, bool final, int limit,
        ref int completed, ref bool array, ref JsonReaderState state)
    {
        var reader = new Utf8JsonReader(data, final, state);
        while (reader.Read())
        {
            if (reader.CurrentDepth == 0 && reader.TokenType == JsonTokenType.StartArray) array = true;
            bool completeElement = reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray
                or JsonTokenType.String or JsonTokenType.Number or JsonTokenType.True or JsonTokenType.False or JsonTokenType.Null;
            if (array && reader.CurrentDepth == 1 && completeElement && ++completed == limit)
            {
                return ((int)reader.BytesConsumed, true);
            }
        }
        state = reader.CurrentState;
        return ((int)reader.BytesConsumed, false);
    }

    public static string ReadCsv(Stream input, int records, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(input, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var sample = new StringBuilder();
        bool quoted = false;
        long completed = 0;
        while (reader.Read() is int value && value >= 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            char character = (char)value;
            sample.Append(character);
            if (character == '"') quoted = !quoted;
            if (!quoted && character is '\r' or '\n')
            {
                if (++completed == (long)records + 1) break; // Header is not a data record.
                if (character == '\r' && reader.Peek() == '\n') sample.Append((char)reader.Read());
            }
        }
        return sample.ToString();
    }
}
