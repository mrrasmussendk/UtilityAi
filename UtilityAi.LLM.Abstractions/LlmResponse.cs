namespace UtilityAi.LLM.Abstractions;

/// <summary>
/// Response from an LLM provider.
/// </summary>
public record LlmResponse(
    string Content,
    LlmFinishReason FinishReason,
    LlmUsage Usage,
    List<LlmToolCall>? ToolCalls = null,
    object? ProviderMetadata = null);

/// <summary>
/// Reason why the LLM stopped generating.
/// </summary>
public enum LlmFinishReason
{
    /// <summary>
    /// Model completed naturally.
    /// </summary>
    Stop,

    /// <summary>
    /// Hit max tokens limit.
    /// </summary>
    Length,

    /// <summary>
    /// Model wants to call one or more tools.
    /// </summary>
    ToolCalls,

    /// <summary>
    /// Content filtered by safety system.
    /// </summary>
    ContentFilter,

    /// <summary>
    /// Other/unknown reason.
    /// </summary>
    Other
}

/// <summary>
/// Token usage statistics for an LLM request.
/// </summary>
public record LlmUsage(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens);

/// <summary>
/// A chunk in a streaming LLM response.
/// </summary>
public record LlmStreamChunk(
    string? Delta,
    bool IsComplete,
    LlmFinishReason? FinishReason = null,
    LlmToolCall? ToolCall = null);
