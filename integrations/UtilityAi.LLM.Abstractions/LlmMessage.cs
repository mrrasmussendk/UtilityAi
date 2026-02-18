namespace UtilityAi.LLM.Abstractions;

/// <summary>
/// Represents a message in an LLM conversation.
/// </summary>
public record LlmMessage(
    LlmRole Role,
    string Content,
    string? Name = null)
{
    /// <summary>
    /// Creates a system message (for instructions/context).
    /// </summary>
    public static LlmMessage System(string content) => new(LlmRole.System, content);

    /// <summary>
    /// Creates a user message.
    /// </summary>
    public static LlmMessage User(string content, string? name = null) => new(LlmRole.User, content, name);

    /// <summary>
    /// Creates an assistant message.
    /// </summary>
    public static LlmMessage Assistant(string content) => new(LlmRole.Assistant, content);

    /// <summary>
    /// Creates a tool result message.
    /// </summary>
    public static LlmMessage Tool(string content, string toolCallId) => new(LlmRole.Tool, content, toolCallId);
}

/// <summary>
/// Role of the message sender in an LLM conversation.
/// </summary>
public enum LlmRole
{
    /// <summary>
    /// System message (instructions, context).
    /// </summary>
    System,

    /// <summary>
    /// User message (human input).
    /// </summary>
    User,

    /// <summary>
    /// Assistant message (LLM output).
    /// </summary>
    Assistant,

    /// <summary>
    /// Tool result message (output from tool execution).
    /// </summary>
    Tool
}
