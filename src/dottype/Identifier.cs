using System.Globalization;
using System.Text;

namespace dottype;

internal static class Identifier
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach",
        "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long",
        "namespace", "new", "null", "object", "operator", "out", "override", "params", "private",
        "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof",
        "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try",
        "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void",
        "volatile", "while", "record", "required", "file", "scoped", "field", "var", "dynamic", "nint", "nuint",
        "__arglist", "__makeref", "__reftype", "__refvalue"
    };

    public static string MakeValid(string source, string fallback = "Field")
    {
        if (source.Length == 0)
        {
            return fallback;
        }

        var result = new StringBuilder(source.Length + 1);
        int position = 0;
        foreach (Rune rune in source.EnumerateRunes())
        {
            // Roslyn identifiers use BMP characters; formatting characters are erased
            // from identifier identity, so retaining them would lose the source name.
            if (!rune.IsBmp || GetCategory(rune) == UnicodeCategory.Format)
            {
                result.Append('_');
                position++;
                continue;
            }
            if (position == 0 && !IsStart(rune))
            {
                if (IsPart(rune))
                {
                    result.Append('_');
                    result.Append(rune.ToString());
                }
                else
                {
                    result.Append('_');
                }
            }
            else
            {
                result.Append(IsPart(rune) ? rune.ToString() : "_");
            }
            position++;
        }

        string value = result.Length == 0 ? fallback : result.ToString();
        return Keywords.Contains(value) ? "@" + value : value;
    }

    public static string Unescaped(string identifier) => identifier.StartsWith('@') ? identifier[1..] : identifier;

    public static bool IsValidNamespace(string value) =>
        value.Split('.', StringSplitOptions.None) is { Length: > 0 } parts && parts.All(IsValidNamespacePart);

    private static bool IsValidNamespacePart(string part)
    {
        if (part.Length == 0) return false;
        if (part[0] == '@')
        {
            string unescaped = part[1..];
            return unescaped.Length > 0 && Unescaped(MakeValid(unescaped)) == unescaped;
        }
        return MakeValid(part) == part;
    }

    private static bool IsStart(Rune rune) => GetCategory(rune) is
        UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter or
        UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or UnicodeCategory.LetterNumber || rune.Value == '_';

    private static bool IsPart(Rune rune) => IsStart(rune) || GetCategory(rune) is
        UnicodeCategory.DecimalDigitNumber or UnicodeCategory.ConnectorPunctuation or UnicodeCategory.NonSpacingMark or
        UnicodeCategory.SpacingCombiningMark or UnicodeCategory.Format;

    private static UnicodeCategory GetCategory(Rune rune) => Rune.GetUnicodeCategory(rune);
}
