namespace dottype;

internal sealed record PocoSchema(string Name, IReadOnlyList<PocoClass> Classes, bool NestChildTypes = false);

internal sealed record PocoClass(string Name, IReadOnlyList<PocoProperty> Properties);

internal sealed record PocoProperty(
    string SourceName,
    string Name,
    string TypeName,
    bool IsNullable,
    bool IsValueType);
