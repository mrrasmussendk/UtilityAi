using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Azure.AI.Projects.OpenAI;
using OpenAI.Chat;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper.SchemaGenerator;

namespace UtilityAi.Maf;

/// <summary>
/// Extension methods for building MAF ChatCompletionOptions with structured output using AiRequestBuilder.
/// </summary>
public static class MafRequestExtensions
{
    /// <summary>
    /// Converts an AiRequestBuilder to Azure OpenAI ChatCompletionOptions with structured output for MAF.
    /// </summary>
    /// <param name="builder">The AiRequestBuilder instance</param>
    /// <param name="messages">Output parameter containing the list of chat messages</param>
    /// <returns>ChatCompletionOptions configured with the JSON schema from the builder</returns>
    /// <exception cref="InvalidOperationException">Thrown when no JSON schema format is configured</exception>
    public static ChatCompletionOptions ToAzureOpenAiChatOptions(this AiRequestBuilder builder, out List<ChatMessage> messages)
    {
        var json = builder.BuildJson();
        var requestEnvelope = System.Text.Json.JsonSerializer.Deserialize<JsonObject>(json);

        if (requestEnvelope?["text"]?["format"] is not JsonObject formatObj)
        {
            throw new InvalidOperationException("No JSON schema format configured in AiRequestBuilder");
        }

        var name = formatObj["name"]?.GetValue<string>() ?? throw new InvalidOperationException("Schema name is required");
        var schema = formatObj["schema"] as JsonObject ?? throw new InvalidOperationException("Schema is required");
        var strict = formatObj["strict"]?.GetValue<bool>() ?? true;

        var schemaBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(schema);

        // Extract messages from the "input" field
        messages = new List<ChatMessage>();
        if (requestEnvelope?["input"] is JsonArray messagesArray)
        {
            foreach (var msg in messagesArray)
            {
                var role = msg?["role"]?.GetValue<string>();
                var content = msg?["content"]?.GetValue<string>();

                if (role != null && content != null)
                {
                    messages.Add(role switch
                    {
                        "system" => new SystemChatMessage(content),
                        "user" => new UserChatMessage(content),
                        _ => throw new InvalidOperationException($"Unsupported message role: {role}")
                    });
                }
            }
        }

        return new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: name,
                jsonSchema: BinaryData.FromBytes(schemaBytes),
                jsonSchemaIsStrict: strict)
        };
    }

    /// <summary>
    /// Creates a ChatCompletionOptions with structured output directly from a .NET type.
    /// </summary>
    /// <typeparam name="T">The type to generate the schema from</typeparam>
    /// <param name="schemaName">Name for the JSON schema format</param>
    /// <param name="options">Schema generation options</param>
    /// <param name="strict">Whether to use strict schema validation</param>
    /// <returns>ChatCompletionOptions configured with the generated schema</returns>
    /// <remarks>
    /// WARNING: Properties without [JsonPropertyName] attributes may not serialize as expected.
    /// Ensure your type's properties have explicit [JsonPropertyName] attributes for proper JSON mapping.
    /// </remarks>
    public static ChatCompletionOptions CreateStructuredOptions<T>(
        string schemaName,
        SchemaGeneratorOptions? options = null,
        bool strict = true)
    {
        CheckTypeForJsonPropertyAttributes<T>();

        var schema = JsonSchemaGenerator.BuildOutputArraySchemaFrom<T>(options);
        var schemaBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(schema);

        return new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: schemaName,
                jsonSchema: BinaryData.FromBytes(schemaBytes),
                jsonSchemaIsStrict: strict)
        };
    }

    /// <summary>
    /// Creates a ChatCompletionOptions with structured output from a JSON schema object.
    /// </summary>
    /// <param name="schemaName">Name for the JSON schema format</param>
    /// <param name="schema">The JSON schema object</param>
    /// <param name="strict">Whether to use strict schema validation</param>
    /// <returns>ChatCompletionOptions configured with the provided schema</returns>
    public static ChatCompletionOptions CreateStructuredOptions(
        string schemaName,
        JsonObject schema,
        bool strict = true)
    {
        var schemaBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(schema);

        return new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: schemaName,
                jsonSchema: BinaryData.FromBytes(schemaBytes),
                jsonSchemaIsStrict: strict)
        };
    }

    /// <summary>
    /// Executes an Azure OpenAI chat completion request using the AiRequestBuilder configuration.
    /// </summary>
    /// <param name="builder">The AiRequestBuilder instance</param>
    /// <param name="chatClient">The Azure OpenAI ChatClient to use for the completion</param>
    /// <returns>ChatCompletion result</returns>
    public static ChatCompletion CompleteAzureOpenAiChat(this AiRequestBuilder builder, ChatClient chatClient)
    {
        var options = builder.ToAzureOpenAiChatOptions(out var messages);
        return chatClient.CompleteChat(messages, options);
    }

    /// <summary>
    /// Executes an Azure OpenAI chat completion request and automatically deserializes the response.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the response to</typeparam>
    /// <param name="builder">The AiRequestBuilder instance</param>
    /// <param name="chatClient">The Azure OpenAI ChatClient to use for the completion</param>
    /// <param name="propertyName">The JSON property name to extract from the response (default: "output")</param>
    /// <returns>Deserialized instance of type T</returns>
    public static T CompleteAndDeserialize<T>(this AiRequestBuilder builder, ChatClient chatClient, string propertyName = "output")
    {
        var completion = builder.CompleteAzureOpenAiChat(chatClient);
        using System.Text.Json.JsonDocument structuredJson = System.Text.Json.JsonDocument.Parse(completion.Content[0].Text);

        // Try multiple strategies to find and deserialize the target type
        var outputElement = FindDeserializableElement<T>(structuredJson.RootElement, propertyName);

        return System.Text.Json.JsonSerializer.Deserialize<T>(outputElement)!;
    }

    /// <summary>
    /// Searches for a JSON element that can be deserialized to type T using multiple strategies.
    /// </summary>
    private static System.Text.Json.JsonElement FindDeserializableElement<T>(System.Text.Json.JsonElement root, string propertyName)
    {
        // Strategy 1: Try root element directly (unwrapped)
        var unwrappedRoot = TryUnwrapElement(root);
        if (CanDeserialize<T>(unwrappedRoot))
            return unwrappedRoot;

        // Strategy 2: Try the specified property name
        if (root.TryGetProperty(propertyName, out var namedElement))
        {
            var result = TryUnwrapElement(namedElement);
            if (CanDeserialize<T>(result))
                return result;
        }

        // Strategy 3: If root is an array, try first element
        if (root.ValueKind == System.Text.Json.JsonValueKind.Array && root.GetArrayLength() > 0)
        {
            var result = TryUnwrapElement(root[0]);
            if (CanDeserialize<T>(result))
                return result;
        }

        // Strategy 4: Search all properties for a deserializable match
        if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var property in root.EnumerateObject())
            {
                var result = TryUnwrapElement(property.Value);
                if (CanDeserialize<T>(result))
                    return result;
            }
        }

        // Fallback: return root as-is
        return root;
    }

    /// <summary>
    /// Unwraps JSON strings and arrays to get the actual data element.
    /// </summary>
    private static System.Text.Json.JsonElement TryUnwrapElement(System.Text.Json.JsonElement element)
    {
        // Unwrap JSON-encoded strings
        if (element.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            var str = element.GetString();
            if (!string.IsNullOrEmpty(str) && (str.TrimStart().StartsWith("{") || str.TrimStart().StartsWith("[")))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(str);
                    return doc.RootElement.Clone();
                }
                catch
                {
                    // Not valid JSON, return as-is
                }
            }
        }

        // Unwrap single-element arrays
        if (element.ValueKind == System.Text.Json.JsonValueKind.Array && element.GetArrayLength() == 1)
        {
            return element[0];
        }

        return element;
    }

    /// <summary>
    /// Checks if a JsonElement can be successfully deserialized to type T.
    /// </summary>
    private static bool CanDeserialize<T>(System.Text.Json.JsonElement element)
    {
        try
        {
            var result = System.Text.Json.JsonSerializer.Deserialize<T>(element);
            return result != null;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if a type's properties have JsonPropertyName attributes and emits warnings if not.
    /// </summary>
    private static void CheckTypeForJsonPropertyAttributes<T>()
    {
        var type = typeof(T);

        // Skip primitive types and common system types
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime))
            return;

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            var hasJsonPropertyName = prop.GetCustomAttribute<JsonPropertyNameAttribute>() != null;

            if (!hasJsonPropertyName)
            {
                Debug.WriteLine($"WARNING: Property '{prop.Name}' on type '{type.Name}' does not have a [JsonPropertyName] attribute. " +
                               "This may cause unexpected JSON serialization behavior with Azure OpenAI structured outputs.");
            }

            // Recursively check nested types
            var propType = prop.PropertyType;
            if (propType.IsClass && propType != typeof(string) && !propType.IsArray)
            {
                var checkMethod = typeof(MafRequestExtensions)
                    .GetMethod(nameof(CheckTypeForJsonPropertyAttributes), BindingFlags.NonPublic | BindingFlags.Static)
                    ?.MakeGenericMethod(propType);

                checkMethod?.Invoke(null, null);
            }
            else if (propType.IsArray)
            {
                var elementType = propType.GetElementType();
                if (elementType?.IsClass == true && elementType != typeof(string))
                {
                    var checkMethod = typeof(MafRequestExtensions)
                        .GetMethod(nameof(CheckTypeForJsonPropertyAttributes), BindingFlags.NonPublic | BindingFlags.Static)
                        ?.MakeGenericMethod(elementType);

                    checkMethod?.Invoke(null, null);
                }
            }
        }
    }
}
