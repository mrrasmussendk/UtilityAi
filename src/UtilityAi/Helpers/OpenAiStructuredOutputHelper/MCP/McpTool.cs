using System.Text.Json.Serialization;

namespace UtilityAi.Helpers.OpenAiStructuredOutputHelper.MCP;

/// <summary>
/// A DTO for "mcp" tools.
/// </summary>
/// <param name="Type">Tool type identifier. Expected value is <c>mcp</c>.</param>
/// <param name="ServerLabel">Logical MCP server label used by the model.</param>
/// <param name="ServerUrl">URL of the MCP server endpoint.</param>
/// <param name="AllowedTools">Whitelisted tool names that can be invoked.</param>
/// <param name="RequireApproval">Approval mode for tool execution.</param>
public sealed record McpTool(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("server_label")] string ServerLabel,
    [property: JsonPropertyName("server_url")] string ServerUrl,
    [property: JsonPropertyName("allowed_tools")] string[] AllowedTools,
    [property: JsonPropertyName("require_approval")] string RequireApproval = "never")
{
    /// <summary>
    /// Creates an MCP tool descriptor with <c>type</c> preset to <c>mcp</c>.
    /// </summary>
    /// <param name="label">Logical MCP server label used by the model.</param>
    /// <param name="serverUrl">URL of the MCP server endpoint.</param>
    /// <param name="allowed">Whitelisted tool names that can be invoked.</param>
    /// <param name="requireApproval">Approval mode for tool execution.</param>
    /// <returns>A new <see cref="McpTool"/> instance.</returns>
    public static McpTool Create(string label, string serverUrl, IEnumerable<string> allowed, string requireApproval = "never")
        => new("mcp", label, serverUrl, allowed.ToArray(), requireApproval);
}
