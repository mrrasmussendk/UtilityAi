using System.Text.Json.Nodes;
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
    /// Converts an AiRequestBuilder to ChatCompletionOptions with structured output for MAF.
    /// </summary>
    /// <param name="builder">The AiRequestBuilder instance</param>
    /// <returns>ChatCompletionOptions configured with the JSON schema from the builder</returns>
    /// <exception cref="InvalidOperationException">Thrown when no JSON schema format is configured</exception>
    public static ChatCompletionOptions ToChatCompletionOptions(this AiRequestBuilder builder)
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
    public static ChatCompletionOptions CreateStructuredOptions<T>(
        string schemaName,
        SchemaGeneratorOptions? options = null,
        bool strict = true)
    {
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
}
