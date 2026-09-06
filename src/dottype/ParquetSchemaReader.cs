using Parquet;
using Parquet.Schema;

namespace dottype;

internal static class ParquetSchemaReader
{
    public static async Task<PocoSchema> ReadAsync(
        Stream input,
        string requestedName,
        CancellationToken cancellationToken = default)
    {
        MemoryStream? seekableCopy = null;
        try
        {
            if (!input.CanSeek)
            {
                seekableCopy = new MemoryStream();
                await input.CopyToAsync(seekableCopy, cancellationToken);
                seekableCopy.Position = 0;
                input = seekableCopy;
            }

            await using ParquetReader reader = await ParquetReader.CreateAsync(
                input,
                leaveStreamOpen: true,
                cancellationToken: cancellationToken);
            return new ParquetPocoBuilder().Build(reader.Schema.Fields, requestedName);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not InvalidDataException)
        {
            throw new InvalidDataException($"Invalid Parquet file: {exception.Message}", exception);
        }
        finally
        {
            await (seekableCopy?.DisposeAsync() ?? ValueTask.CompletedTask);
        }
    }

    private sealed class ParquetPocoBuilder
    {
        private readonly NameAllocator _classNames = new();
        private readonly List<PocoClass> _classes = [];

        public PocoSchema Build(IReadOnlyList<Field> fields, string requestedName)
        {
            string rootName = _classNames.Allocate(Identifier.MakeValid(requestedName, "Root"));
            BuildClass(fields, rootName);
            return new PocoSchema(rootName, _classes);
        }

        private void BuildClass(IReadOnlyList<Field> fields, string className)
        {
            int classIndex = _classes.Count;
            _classes.Add(new PocoClass(className, []));
            var properties = new List<PocoProperty>(fields.Count);
            var propertyNames = NameAllocator.ForProperties(fields.Select(field => field.Name), className);

            foreach (Field field in fields)
            {
                string propertyName = propertyNames[field.Name];
                TypeDescription description = Describe(field, field.Name);
                properties.Add(new PocoProperty(
                    field.Name,
                    propertyName,
                    description.Name,
                    field.IsNullable,
                    description.IsValueType));
            }

            _classes[classIndex] = new PocoClass(className, properties);
        }

        private TypeDescription Describe(Field field, string suggestedName) => field switch
        {
            DataField data => DescribeData(data),
            StructField structure => DescribeStruct(structure, suggestedName),
            ListField list => DescribeList(list, suggestedName),
            MapField map => DescribeMap(map, suggestedName),
            _ => new TypeDescription("object", false)
        };

        private static TypeDescription DescribeData(DataField field)
        {
            Type type = Nullable.GetUnderlyingType(field.ClrType) ?? field.ClrType;
            // Classify the type we emit, rather than Parquet.Net's storage type.
            if (type == typeof(ReadOnlyMemory<char>) || type == typeof(Memory<char>)) type = typeof(string);
            if (type == typeof(ReadOnlyMemory<byte>) || type == typeof(Memory<byte>)) type = typeof(byte[]);
            if (type.Assembly == typeof(ParquetReader).Assembly)
                throw new InvalidDataException($"Unsupported Parquet type '{type.FullName}' for field '{field.Name}': no dependency-free C# representation is available.");
            string typeName = GetTypeName(type);
            bool isValueType = type.IsValueType;
            if (field.IsArray)
            {
                string itemNullability = field.IsNullable ? "?" : "";
                return new TypeDescription($"{typeName}{itemNullability}[]", false);
            }
            return new TypeDescription(typeName, isValueType);
        }

        private TypeDescription DescribeStruct(StructField field, string suggestedName)
        {
            string className = _classNames.Allocate(Identifier.MakeValid(suggestedName + "Type", "ItemType"));
            BuildClass(field.Fields, className);
            return new TypeDescription(className, false);
        }

        private TypeDescription DescribeList(ListField field, string suggestedName)
        {
            TypeDescription item = Describe(field.Item, suggestedName + "Item");
            string itemNullability = field.Item.IsNullable ? "?" : "";
            return new TypeDescription(
                $"global::System.Collections.Generic.List<{item.Name}{itemNullability}>",
                false);
        }

        private TypeDescription DescribeMap(MapField field, string suggestedName)
        {
            TypeDescription key = Describe(field.Key, suggestedName + "Key");
            TypeDescription value = Describe(field.Value, suggestedName + "Value");
            string valueNullability = field.Value.IsNullable ? "?" : "";
            return new TypeDescription(
                $"global::System.Collections.Generic.Dictionary<{key.Name}, {value.Name}{valueNullability}>",
                false);
        }

        private static string GetTypeName(Type type)
        {
            if (type == typeof(bool)) return "bool";
            if (type == typeof(byte)) return "byte";
            if (type == typeof(sbyte)) return "sbyte";
            if (type == typeof(short)) return "short";
            if (type == typeof(ushort)) return "ushort";
            if (type == typeof(int)) return "int";
            if (type == typeof(uint)) return "uint";
            if (type == typeof(long)) return "long";
            if (type == typeof(ulong)) return "ulong";
            if (type == typeof(float)) return "float";
            if (type == typeof(double)) return "double";
            if (type == typeof(decimal)) return "decimal";
            if (type == typeof(string)) return "string";
            if (type == typeof(object)) return "object";
            if (type == typeof(byte[])) return "byte[]";
            if (type.IsArray) return GetTypeName(type.GetElementType()!) + "[]";
            if (type.IsGenericType)
            {
                string name = type.GetGenericTypeDefinition().FullName ?? type.Name;
                name = name[..name.IndexOf('`')].Replace('+', '.');
                string arguments = string.Join(", ", type.GetGenericArguments().Select(GetTypeName));
                return $"global::{name}<{arguments}>";
            }
            return "global::" + (type.FullName ?? type.Name).Replace('+', '.');
        }
    }

    private sealed record TypeDescription(string Name, bool IsValueType);
}
