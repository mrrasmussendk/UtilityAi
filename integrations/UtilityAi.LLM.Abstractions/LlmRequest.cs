namespace UtilityAi.LLM.Abstractions;

/// <summary>
/// Request to an LLM provider for completion.
/// </summary>
public record LlmRequest(
    List<LlmMessage> Messages,
    LlmOptions? Options = null)
{
    /// <summary>
    /// Creates a simple request with a single user message.
    /// </summary>
    public static LlmRequest Simple(string userMessage, string? systemPrompt = null)
    {
        var messages = new List<LlmMessage>();
        if (systemPrompt != null)
            messages.Add(LlmMessage.System(systemPrompt));
        messages.Add(LlmMessage.User(userMessage));
        return new LlmRequest(messages);
    }
}

/// <summary>
/// Options for LLM completion requests.
/// </summary>
public record LlmOptions(
    double? Temperature = null,
    int? MaxTokens = null,
    double? TopP = null,
    double? FrequencyPenalty = null,
    double? PresencePenalty = null,
    List<string>? StopSequences = null,
    List<LlmTool>? Tools = null,
    string? ToolChoice = null);
