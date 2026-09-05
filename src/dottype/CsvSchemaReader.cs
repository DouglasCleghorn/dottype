using System.Globalization;
using System.Text;

namespace dottype;

internal static class CsvSchemaReader
{
    public static async Task<PocoSchema> ReadAsync(
        Stream input,
        string requestedName,
        CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(input, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        string content;
        try
        {
            content = await reader.ReadToEndAsync(cancellationToken);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("The CSV file contains invalid text encoding.", exception);
        }
        IReadOnlyList<IReadOnlyList<string>> rows = Parse(content);
        if (rows.Count == 0)
        {
            throw new InvalidDataException("The CSV file is empty.");
        }

        IReadOnlyList<string> headers = rows[0];
        if (headers.Count == 0)
        {
            throw new InvalidDataException("The CSV header is empty.");
        }

        var types = Enumerable.Repeat(CsvType.Unknown, headers.Count).ToArray();
        var nullable = new bool[headers.Count];
        for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            IReadOnlyList<string> row = rows[rowIndex];
            if (row.Count > headers.Count)
            {
                throw new InvalidDataException($"CSV row {rowIndex + 1} has more fields than the header.");
            }
            for (int column = 0; column < headers.Count; column++)
            {
                if (column >= row.Count || string.IsNullOrEmpty(row[column]))
                {
                    nullable[column] = true;
                    continue;
                }
                types[column] = Merge(types[column], Infer(row[column]));
            }
        }

        var properties = new List<PocoProperty>(headers.Count);
        string className = Identifier.MakeValid(requestedName, "Root");
        var propertyNames = NameAllocator.ForProperties(headers, className);
        for (int index = 0; index < headers.Count; index++)
        {
            string sourceName = headers[index];
            string propertyName = propertyNames[sourceName];
            (string typeName, bool valueType) = Describe(types[index]);
            properties.Add(new PocoProperty(
                sourceName,
                propertyName,
                typeName,
                nullable[index] || types[index] == CsvType.Unknown,
                valueType));
        }

        return new PocoSchema(className, [new PocoClass(className, properties)]);
    }

    internal static IReadOnlyList<IReadOnlyList<string>> Parse(string content)
    {
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;
        bool afterQuote = false;
        bool recordStarted = false;

        for (int index = 0; index < content.Length; index++)
        {
            char character = content[index];
            if (quoted)
            {
                if (character == '"')
                {
                    if (index + 1 < content.Length && content[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = false;
                        afterQuote = true;
                    }
                }
                else
                {
                    field.Append(character);
                }
                recordStarted = true;
                continue;
            }

            if (afterQuote && character is not ',' and not '\r' and not '\n')
            {
                throw new InvalidDataException("Unexpected text after a closing quote in the CSV file.");
            }
            if (character == '"')
            {
                if (field.Length != 0)
                {
                    throw new InvalidDataException("A quote appeared inside an unquoted CSV field.");
                }
                quoted = true;
                recordStarted = true;
            }
            else if (character == ',')
            {
                row.Add(field.ToString());
                field.Clear();
                afterQuote = false;
                recordStarted = true;
            }
            else if (character is '\r' or '\n')
            {
                if (character == '\r' && index + 1 < content.Length && content[index + 1] == '\n')
                {
                    index++;
                }
                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = [];
                afterQuote = false;
                recordStarted = false;
            }
            else
            {
                field.Append(character);
                recordStarted = true;
            }
        }

        if (quoted)
        {
            throw new InvalidDataException("The CSV file ends inside a quoted field.");
        }
        if (recordStarted || row.Count > 0 || field.Length > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }
        return rows;
    }

    private static CsvType Infer(string value)
    {
        if (bool.TryParse(value, out _))
        {
            return CsvType.Boolean;
        }
        if (IsCanonicalInteger(value) && long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
        {
            return CsvType.Integer;
        }
        if (IsCanonicalNumber(value) && decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            return CsvType.Decimal;
        }
        if (IsCanonicalNumber(value) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            return CsvType.Double;
        }
        return CsvType.String;
    }

    private static CsvType Merge(CsvType left, CsvType right)
    {
        if (left == CsvType.Unknown) return right;
        if (right == CsvType.Unknown || left == right) return left;
        if (left is >= CsvType.Integer and <= CsvType.Double && right is >= CsvType.Integer and <= CsvType.Double)
        {
            return (CsvType)Math.Max((int)left, (int)right);
        }
        return CsvType.String;
    }

    private static (string TypeName, bool IsValueType) Describe(CsvType type) => type switch
    {
        CsvType.Boolean => ("bool", true),
        CsvType.Integer => ("long", true),
        CsvType.Decimal => ("decimal", true),
        CsvType.Double => ("double", true),
        _ => ("string", false)
    };

    private static bool IsCanonicalInteger(string value)
    {
        ReadOnlySpan<char> span = value.AsSpan();
        if (span.Length > 0 && span[0] is '+' or '-') span = span[1..];
        if (span.Length == 0 || (span.Length > 1 && span[0] == '0'))
        {
            return false;
        }
        foreach (char character in span)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsCanonicalNumber(string value)
    {
        int start = value.Length > 0 && value[0] is '+' or '-' ? 1 : 0;
        int end = value.IndexOfAny('e', 'E');
        if (end < 0) end = value.Length;
        int dot = value.IndexOf('.');
        int integerEnd = dot >= 0 && dot < end ? dot : end;
        ReadOnlySpan<char> integer = value.AsSpan(start, integerEnd - start);
        return integer.Length > 0 && (integer.Length == 1 || integer[0] != '0');
    }

    private enum CsvType
    {
        Unknown,
        Boolean,
        Integer,
        Decimal,
        Double,
        String
    }
}
