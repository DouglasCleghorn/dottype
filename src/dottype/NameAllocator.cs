namespace dottype;

internal sealed class NameAllocator
{
    private readonly HashSet<string> _used = new(StringComparer.Ordinal);

    public static Dictionary<string, string> ForProperties(IEnumerable<string> sourceNames, string className)
    {
        string[] names = sourceNames.ToArray();
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Length)
            throw new InvalidDataException("Duplicate field names cannot be mapped reversibly to POCO properties.");

        var allocator = new NameAllocator();
        allocator.Allocate(className);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        // Reserve names that need no repair first, so e.g. a-b never steals a_b.
        foreach (string name in names)
        {
            string identifier = Identifier.MakeValid(name);
            if (Identifier.Unescaped(identifier) == name && name != Identifier.Unescaped(className))
                result.Add(name, allocator.Allocate(identifier));
        }
        foreach (string name in names)
        {
            if (!result.ContainsKey(name))
                result.Add(name, allocator.Allocate(Identifier.MakeValid(name)));
        }
        return result;
    }

    public string Allocate(string requested)
    {
        string candidate = requested;
        int suffix = 2;
        while (!_used.Add(Identifier.Unescaped(candidate)))
        {
            string unescaped = Identifier.Unescaped(requested);
            candidate = $"{unescaped}_{suffix++}";
        }
        return candidate;
    }
}
