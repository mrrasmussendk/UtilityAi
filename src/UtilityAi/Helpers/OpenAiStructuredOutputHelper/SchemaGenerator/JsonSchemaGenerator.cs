using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper.Strategy;

namespace UtilityAi.Helpers.OpenAiStructuredOutputHelper.SchemaGenerator;

/// <summary>
/// Generates a JSON Schema (as JsonNode) for the envelope:
/// { type: "object", properties: { output: { ... }}, required:["output"], additionalProperties:false }
/// The output property will be an array if T is a collection type, otherwise a single object.
/// </summary>
public static class JsonSchemaGenerator
{
    /// <summary>
    /// Builds a schema that intelligently wraps T in "output" property.
    /// If T is a collection (List, Array, IEnumerable), output will be an array.
    /// If T is a single object, output will be that object's schema directly.
    /// </summary>
    public static JsonObject BuildOutputSchemaFrom<T>(SchemaGeneratorOptions? options = null)
        => BuildOutputSchemaFrom(typeof(T), options);

    /// <summary>
    /// Builds a schema that intelligently wraps T in "output" property.
    /// If T is a collection (List, Array, IEnumerable), output will be an array.
    /// If T is a single object, output will be that object's schema directly.
    /// </summary>
    public static JsonObject BuildOutputSchemaFrom(Type type, SchemaGeneratorOptions? options = null)
    {
        options ??= new SchemaGeneratorOptions();

        // Check if the type is a collection (but not string or dictionary)
        bool isCollection = IsCollectionType(type, out var elementType);

        JsonObject outputSchema;

        if (isCollection)
        {
            // It's a collection - wrap in array
            var items = BuildItemObjectSchema(elementType!, options);
            outputSchema = new JsonObject
            {
                ["type"] = "array",
                ["items"] = items
            };
        }
        else
        {
            // It's a single object - use its schema directly
            outputSchema = BuildItemObjectSchema(type, options);
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["output"] = outputSchema
            },
            ["required"] = new JsonArray("output"),
            ["additionalProperties"] = false
        };
    }

    /// <summary>
    /// DEPRECATED: Always wraps in array. Use BuildOutputSchemaFrom instead for intelligent wrapping.
    /// Generates a JSON Schema for the envelope with output always as an array.
    /// </summary>
    [Obsolete("Use BuildOutputSchemaFrom instead - it intelligently decides array vs object based on type")]
    public static JsonObject BuildOutputArraySchemaFrom<T>(SchemaGeneratorOptions? options = null)
        => BuildOutputArraySchemaFrom(typeof(T), options);

    /// <summary>
    /// DEPRECATED: Always wraps in array. Use BuildOutputSchemaFrom instead for intelligent wrapping.
    /// Generates a JSON Schema for the envelope with output always as an array.
    /// </summary>
    [Obsolete("Use BuildOutputSchemaFrom instead - it intelligently decides array vs object based on type")]
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

    private static bool IsCollectionType(Type type, out Type? elementType)
    {
        elementType = null;

        // String is not a collection for our purposes
        if (type == typeof(string))
            return false;

        // Dictionary is not a collection for our purposes (it's an object with additionalProperties)
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            return false;

        // Check for array
        if (type.IsArray)
        {
            elementType = type.GetElementType();
            return true;
        }

        // Check for IEnumerable<T>
        var iEnumerable = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (iEnumerable != null)
        {
            elementType = iEnumerable.GetGenericArguments()[0];
            return true;
        }

        // Check if type itself is IEnumerable<T>
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            elementType = type.GetGenericArguments()[0];
            return true;
        }

        return false;
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

        // Handle JsonElement as generic object type
        if (effective == typeof(System.Text.Json.JsonElement))
            return (new JsonObject {["type"] = "object", ["properties"] = new JsonObject(), ["required"] = new JsonArray(), ["additionalProperties"] = false}, !isNullableValue);

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

        // Handle Dictionary<string, T> as object with additionalProperties
        if (effective.IsGenericType && effective.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            var keyType = effective.GetGenericArguments()[0];
            var valueType = effective.GetGenericArguments()[1];

            // Only support Dictionary<string, T>
            if (keyType == typeof(string))
            {
                // For Dictionary<string, JsonElement>, use object with additionalProperties: true
                // to allow dynamic key-value pairs (e.g., entities like "kilometer": 100).
                //
                // IMPORTANT: Using additionalProperties: false here would instruct OpenAI's
                // structured output to return only empty objects {}, silently dropping all
                // dynamic keys extracted from the user's message. This was a bug that
                // prevented IntentAnalysis.Entities and .Parameters from ever being populated.
                // See: Regression_EntitiesSchema_MustAllowAdditionalProperties_ForDynamicKeys
                if (valueType == typeof(System.Text.Json.JsonElement))
                {
                    return (new JsonObject
                    {
                        ["type"] = "object",
                        ["additionalProperties"] = true
                    }, false);
                }

                // For other Dictionary<string, T>, use the value type schema
                var (valueSchema, _) = SchemaFor(valueType);
                return (new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = valueSchema
                }, false);
            }
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

        // For strict schema mode: when additionalProperties is false, all properties must be required
        var allPropKeys = nestedProps.Select(p => p.Key).ToList();

        return (new JsonObject
        {
            ["type"] = "object",
            ["properties"] = nestedProps,
            ["required"] = new JsonArray(allPropKeys.Select(r => (JsonNode) r).ToArray()),
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