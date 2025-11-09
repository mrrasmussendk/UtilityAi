using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper.Strategy;

namespace UtilityAi.Helpers.OpenAiStructuredOutputHelper.SchemaGenerator;

/// <summary>
/// Generates a JSON Schema (as JsonNode) for the envelope:
/// { type: "object", properties: { output: { type:"array", items: <T-schema> }}, required:["output"], additionalProperties:false }
/// </summary>
public static class JsonSchemaGenerator
{
    public static JsonObject BuildOutputArraySchemaFrom<T>(SchemaGeneratorOptions? options = null)
        => BuildOutputArraySchemaFrom(typeof(T), options);

    public static JsonObject BuildOutputArraySchemaFrom(Type itemType, SchemaGeneratorOptions? options = null)
    {
        options ??= new SchemaGeneratorOptions();
        var items = BuildItemObjectSchema(itemType, options);

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["output"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = items
                }
            },
            ["required"] = new JsonArray("output"),
            ["additionalProperties"] = false
        };
    }

    private static JsonObject BuildItemObjectSchema(Type type, SchemaGeneratorOptions options)
    {
        var (props, required) = BuildObjectProperties(type, options.RequiredStrategy);

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = props,
            ["required"] = new JsonArray(required.Select(r => (JsonNode) r).ToArray()),
            ["additionalProperties"] = options.AdditionalPropertiesForItem
        };
    }

    private static (JsonObject Props, List<string> Required) BuildObjectProperties(
        Type type,
        RequiredStrategy requiredStrategy)
    {
        var props = new JsonObject();
        var required = new List<string>();

        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (p.GetCustomAttribute<JsonIgnoreAttribute>() is not null) continue;

            var jsonName = p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? ToCamel(p.Name);
            var (schema, requiredByType) = SchemaFor(p.PropertyType);

            if (p.GetCustomAttribute<DescriptionAttribute>() is { } d)
                schema["description"] = d.Description;

            var hasRequiredAttr = p.GetCustomAttribute<RequiredAttribute>() is not null;
            var isRequired = requiredStrategy switch
            {
                RequiredStrategy.AllProperties => true,
                RequiredStrategy.AttributesOnly => hasRequiredAttr,
                _ => requiredByType || hasRequiredAttr
            };
            if (isRequired) required.Add(jsonName);

            props[jsonName] = schema;
        }

        return (props, required);
    }

    private static (JsonObject Schema, bool RequiredByType) SchemaFor(Type t)
    {
        var underlyingNullable = Nullable.GetUnderlyingType(t);
        var isNullableValue = underlyingNullable is not null;
        var effective = underlyingNullable ?? t;

        // ✅ Handle scalar primitives FIRST (string before IEnumerable)
        if (effective == typeof(string))
            return (new JsonObject {["type"] = "string"}, !isNullableValue);

        if (effective == typeof(DateOnly))
            return (new JsonObject {["type"] = "string", ["format"] = "date"}, !isNullableValue);

        if (effective == typeof(TimeOnly))
            return (new JsonObject {["type"] = "string", ["format"] = "time"}, !isNullableValue);

        if (effective == typeof(DateTime) || effective == typeof(DateTimeOffset))
            return (new JsonObject {["type"] = "string", ["format"] = "date-time"}, !isNullableValue);

        if (effective == typeof(bool))
            return (new JsonObject {["type"] = "boolean"}, !isNullableValue);

        if (effective == typeof(int) || effective == typeof(long) ||
            effective == typeof(short) || effective == typeof(byte))
            return (new JsonObject {["type"] = "integer"}, !isNullableValue);

        if (effective == typeof(float) || effective == typeof(double) || effective == typeof(decimal))
            return (new JsonObject {["type"] = "number"}, !isNullableValue);

        if (effective == typeof(Guid))
            return (new JsonObject {["type"] = "string", ["format"] = "uuid"}, !isNullableValue);

        if (effective == typeof(Uri))
            return (new JsonObject {["type"] = "string", ["format"] = "uri"}, !isNullableValue);

        if (effective.IsEnum)
        {
            var names = Enum.GetNames(effective);
            return (new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray(names.Select(n => (JsonNode) n).ToArray())
            }, !isNullableValue);
        }

        // ✅ Only now treat collections as arrays
        if (TryGetEnumerableElement(effective, out var elemType))
        {
            var (itemSchema, _) = SchemaFor(elemType);
            return (new JsonObject
            {
                ["type"] = "array",
                ["items"] = itemSchema
            }, false);
        }

        // Nested object
        var (nestedProps, nestedReq) =
            BuildObjectProperties(effective, RequiredStrategy.NonNullableValueTypesAndRequiredAttribute);

        return (new JsonObject
        {
            ["type"] = "object",
            ["properties"] = nestedProps,
            ["required"] = new JsonArray(nestedReq.Select(r => (JsonNode) r).ToArray()),
            ["additionalProperties"] = false
        }, !isNullableValue && effective.IsValueType);
    }

    private static bool TryGetEnumerableElement(Type t, out Type elem)
    {
        if (t == typeof(string)) { elem = null!; return false; }  // ✅ strings are scalars
        if (t.IsArray)
        {
            elem = t.GetElementType()!;
            return true;
        }

        // Prefer IEnumerable<T> if available
        var ienum = t.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (ienum is not null)
        {
            elem = ienum.GetGenericArguments()[0];
            return true;
        }

        elem = null!;
        return false;
    }

    private static string ToCamel(string s) =>
        string.IsNullOrEmpty(s) || char.IsLower(s[0]) ? s : char.ToLowerInvariant(s[0]) + s[1..];
}