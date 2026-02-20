using System.Text.Json.Serialization;

namespace UtilityAi.Helpers.OpenAiStructuredOutputHelper.MCP;

/// <summary>
/// Generic simple tool with only a "type" field (e.g., "web_search").
/// </summary>
/// <param name="Type">Tool type identifier.</param>
public sealed record SimpleTool([property: JsonPropertyName("type")] string Type)
{
    /// <summary>
    /// Creates a <see cref="SimpleTool"/> for the specified tool type.
    /// </summary>
    /// <param name="type">Tool type identifier.</param>
    /// <returns>A new <see cref="SimpleTool"/> instance.</returns>
    public static SimpleTool Of(string type) => new(type);
}
