using System.Text.Json.Serialization;

namespace UtilityAi.Helpers.OpenAiStructuredOutputHelper.MCP;

/// <summary>
/// A DTO for "mcp" tools.
/// </summary>
public sealed record McpTool(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("server_label")] string ServerLabel,
    [property: JsonPropertyName("server_url")] string ServerUrl,
    [property: JsonPropertyName("allowed_tools")] string[] AllowedTools,
    [property: JsonPropertyName("require_approval")] string RequireApproval = "never")
{
    public static McpTool Create(string label, string serverUrl, IEnumerable<string> allowed, string requireApproval = "never")
        => new("mcp", label, serverUrl, allowed.ToArray(), requireApproval);
}