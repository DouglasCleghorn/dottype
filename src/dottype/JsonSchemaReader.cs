using System.Text.Json;

namespace dottype;

internal static class JsonSchemaReader
{
    public static async Task<PocoSchema> ReadAsync(
        Stream input,
        string requestedName,
        int? sampleRecords = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (sampleRecords is int limit)
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
                await using MemoryStream sample = await InferenceSample.ReadJsonAsync(input, limit, cancellationToken);
                return await ReadAsync(sample, requestedName, cancellationToken: cancellationToken);
            }
            using JsonDocument document = await JsonDocument.ParseAsync(input, cancellationToken: cancellationToken);
            InferredType root = document.RootElement.ValueKind switch
            {
                JsonValueKind.Object => Infer(document.RootElement),
                JsonValueKind.Array => InferRootArray(document.RootElement),
                _ => throw new InvalidDataException("The JSON root must be an object or an array of objects.")
            };

            if (root.Kind != JsonKind.Object)
            {
                throw new InvalidDataException("The JSON root array must contain objects.");
            }

            return new JsonPocoBuilder().Build(root, requestedName);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Invalid JSON: {exception.Message}", exception);
        }
    }

    private static InferredType InferRootArray(JsonElement array)
    {
        InferredType? result = null;
        foreach (JsonElement item in array.EnumerateArray())
        {
            result = result is null ? Infer(item) : Merge(result, Infer(item));
        }
        return result ?? throw new InvalidDataException("Cannot infer a type from an empty JSON array.");
    }

    private static InferredType Infer(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => new(JsonKind.Unknown) { IsNullable = true },
        JsonValueKind.True or JsonValueKind.False => new(JsonKind.Boolean),
        JsonValueKind.String => new(JsonKind.String),
        JsonValueKind.Number when element.TryGetInt64(out _) => new(JsonKind.Integer),
        JsonValueKind.Number when element.TryGetDecimal(out _) => new(JsonKind.Decimal),
        JsonValueKind.Number => new(JsonKind.Double),
        JsonValueKind.Object => InferObject(element),
        JsonValueKind.Array => InferArray(element),
        _ => new(JsonKind.Mixed) { IsNullable = true }
    };

    private static InferredType InferObject(JsonElement element)
    {
        var result = new InferredType(JsonKind.Object);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            InferredType value = Infer(property.Value);
            if (result.Fields.TryGetValue(property.Name, out InferredType? duplicate))
            {
                result.Fields[property.Name] = Merge(duplicate, value);
            }
            else
            {
                result.Fields.Add(property.Name, value);
            }
        }
        return result;
    }

    private static InferredType InferArray(JsonElement element)
    {
        InferredType? itemType = null;
        foreach (JsonElement item in element.EnumerateArray())
        {
            itemType = itemType is null ? Infer(item) : Merge(itemType, Infer(item));
        }
        return new InferredType(JsonKind.Array)
        {
            ElementType = itemType ?? new InferredType(JsonKind.Unknown) { IsNullable = true }
        };
    }

    private static InferredType Merge(InferredType left, InferredType right)
    {
        if (left.Kind == JsonKind.Unknown)
        {
            right.IsNullable |= left.IsNullable;
            return right;
        }
        if (right.Kind == JsonKind.Unknown)
        {
            left.IsNullable |= right.IsNullable;
            return left;
        }

        if (left.Kind == right.Kind)
        {
            left.IsNullable |= right.IsNullable;
            if (left.Kind == JsonKind.Array)
            {
                left.ElementType = Merge(left.ElementType!, right.ElementType!);
            }
            else if (left.Kind == JsonKind.Object)
            {
                foreach ((string name, InferredType leftField) in left.Fields.ToArray())
                {
                    if (!right.Fields.ContainsKey(name))
                    {
                        leftField.IsNullable = true;
                    }
                }
                foreach ((string name, InferredType rightField) in right.Fields)
                {
                    if (left.Fields.TryGetValue(name, out InferredType? leftField))
                    {
                        left.Fields[name] = Merge(leftField, rightField);
                    }
                    else
                    {
                        rightField.IsNullable = true;
                        left.Fields.Add(name, rightField);
                    }
                }
            }
            return left;
        }

        if (IsNumber(left.Kind) && IsNumber(right.Kind))
        {
            left.Kind = (JsonKind)Math.Max((int)left.Kind, (int)right.Kind);
            left.IsNullable |= right.IsNullable;
            return left;
        }

        return new InferredType(JsonKind.Mixed) { IsNullable = left.IsNullable || right.IsNullable };
    }

    private static bool IsNumber(JsonKind kind) => kind is JsonKind.Integer or JsonKind.Decimal or JsonKind.Double;

    private enum JsonKind
    {
        Unknown = 0,
        Boolean = 1,
        Integer = 2,
        Decimal = 3,
        Double = 4,
        String = 5,
        Object = 6,
        Array = 7,
        Mixed = 8
    }

    private sealed class InferredType(JsonKind kind)
    {
        public JsonKind Kind { get; set; } = kind;
        public bool IsNullable { get; set; }
        public Dictionary<string, InferredType> Fields { get; } = new(StringComparer.Ordinal);
        public InferredType? ElementType { get; set; }
    }

    private sealed class JsonPocoBuilder
    {
        private readonly NameAllocator _classNames = new();
        private readonly List<PocoClass> _classes = [];

        public PocoSchema Build(InferredType root, string requestedName)
        {
            string rootName = _classNames.Allocate(Identifier.MakeValid(requestedName, "Root"));
            BuildClass(root, rootName);
            return new PocoSchema(rootName, _classes);
        }

        private void BuildClass(InferredType type, string className)
        {
            int classIndex = _classes.Count;
            _classes.Add(new PocoClass(className, []));
            var properties = new List<PocoProperty>();
            var propertyNames = NameAllocator.ForProperties(type.Fields.Keys, className);

            foreach ((string sourceName, InferredType field) in type.Fields)
            {
                string propertyName = propertyNames[sourceName];
                TypeDescription description = Describe(field, sourceName);
                properties.Add(new PocoProperty(
                    sourceName,
                    propertyName,
                    description.Name,
                    field.IsNullable || field.Kind is JsonKind.Unknown or JsonKind.Mixed,
                    description.IsValueType));
            }

            _classes[classIndex] = new PocoClass(className, properties);
        }

        private TypeDescription Describe(InferredType type, string suggestedName)
        {
            return type.Kind switch
            {
                JsonKind.Boolean => new("bool", true),
                JsonKind.Integer => new("long", true),
                JsonKind.Decimal => new("decimal", true),
                JsonKind.Double => new("double", true),
                JsonKind.String => new("string", false),
                JsonKind.Object => DescribeObject(type, suggestedName),
                JsonKind.Array => DescribeArray(type, suggestedName),
                _ => new("object", false)
            };
        }

        private TypeDescription DescribeObject(InferredType type, string suggestedName)
        {
            string className = _classNames.Allocate(Identifier.MakeValid(suggestedName + "Type", "ItemType"));
            BuildClass(type, className);
            return new TypeDescription(className, false);
        }

        private TypeDescription DescribeArray(InferredType type, string suggestedName)
        {
            InferredType element = type.ElementType!;
            TypeDescription item = Describe(element, suggestedName + "Item");
            string itemNullability = element.IsNullable || element.Kind is JsonKind.Unknown or JsonKind.Mixed ? "?" : "";
            return new TypeDescription($"global::System.Collections.Generic.List<{item.Name}{itemNullability}>", false);
        }
    }

    private sealed record TypeDescription(string Name, bool IsValueType);
}
