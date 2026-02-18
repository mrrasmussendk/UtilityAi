using System.Text.Json.Serialization;

namespace UtilityAi.Helpers.OpenAiStructuredOutputHelper.MCP;

/// <summary>
/// Generic simple tool with only a "type" field (e.g., "web_search").
/// </summary>
public sealed record SimpleTool([property: JsonPropertyName("type")] string Type)
{
    public static SimpleTool Of(string type) => new(type);
}