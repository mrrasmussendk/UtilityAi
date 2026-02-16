namespace Example.AgentAssistant;

/// <summary>
/// User's message input to the agent.
/// </summary>
public sealed record UserMessage(string Text, string UserId);

/// <summary>
/// Agent's response to the user.
/// </summary>
public sealed record AssistantResponse(string Text, string Source);

/// <summary>
/// Research results from external sources.
/// </summary>
public sealed record ResearchResults(string Query, List<string> Sources, string Summary);

/// <summary>
/// Current conversation context metadata.
/// </summary>
public sealed record ConversationContext(
    int MessageCount,
    bool RequiresResearch,
    bool HasRecentResponse,
    double Confidence
);

/// <summary>
/// Indicates research is needed for the current query.
/// </summary>
public sealed record ResearchNeeded(string Query, string Reason);

/// <summary>
/// Represents available tools/capabilities.
/// </summary>
public sealed record AvailableTools(bool CanAccessWeb, bool CanAccessDatabase, int RateLimitRemaining);
