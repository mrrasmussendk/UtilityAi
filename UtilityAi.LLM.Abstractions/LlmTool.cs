using System.Text.Json;

namespace UtilityAi.LLM.Abstractions;

/// <summary>
/// Definition of a tool that an LLM can call.
/// </summary>
public record LlmTool(
    string Name,
    string Description,
    JsonDocument ParametersSchema);

/// <summary>
/// Represents a tool call requested by the LLM.
/// </summary>
public record LlmToolCall(
    string Id,
    string Name,
    string ArgumentsJson);
