using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace UtilityAi.Helpers.OpenAiStructuredOutputHelper.DTO;

/// <summary>
/// JSON Schema formatter for the "text" field.
/// </summary>
public sealed record JsonSchemaFormat(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("schema")] JsonObject Schema,
    [property: JsonPropertyName("strict")] bool Strict = true)
{
    public static JsonSchemaFormat Create(string name, JsonObject schema, bool strict = true)
        => new("json_schema", name, schema, strict);
}
