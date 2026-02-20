namespace UtilityAi.Facts;

/// <summary>
/// Current system time.
/// </summary>
public sealed record CurrentTime(DateTimeOffset Value)
{
    /// <summary>
    /// Gets the current time value.
    /// </summary>
    public DateTimeOffset Time => Value;
}

/// <summary>
/// Current orchestration tick number.
/// </summary>
public sealed record TickNumber(int Value)
{
    /// <summary>
    /// Gets the current orchestration tick number.
    /// </summary>
    public int Tick => Value;
}

/// <summary>
/// Elapsed time since orchestration started.
/// </summary>
public sealed record ElapsedTime(TimeSpan Value)
{
    /// <summary>
    /// Gets the elapsed orchestration time.
    /// </summary>
    public TimeSpan Duration => Value;
}

/// <summary>
/// A message from a user.
/// </summary>
public sealed record UserMessage(string Text, string UserId, DateTimeOffset Timestamp);

/// <summary>
/// A message from the assistant/agent.
/// </summary>
public sealed record AssistantMessage(string Text, DateTimeOffset Timestamp);

/// <summary>
/// Metadata about the current conversation.
/// </summary>
public sealed record ConversationMetadata(
    int MessageCount,
    TimeSpan Duration,
    bool IsLongConversation,
    DateTimeOffset? FirstMessageTime = null,
    DateTimeOffset? LastMessageTime = null);

/// <summary>
/// System resource usage metrics.
/// </summary>
public sealed record ResourceUsage(
    double CpuPercent,
    double MemoryMegabytes,
    DateTimeOffset Timestamp);

/// <summary>
/// Rate limiting status.
/// </summary>
public sealed record RateLimitStatus(
    int RemainingRequests,
    DateTimeOffset ResetTime,
    bool IsLimited);

/// <summary>
/// Indicates orchestration should stop.
/// </summary>
public sealed record StopSignal(string Reason);

/// <summary>
/// Record of a single executed action.
/// </summary>
public sealed record ExecutedAction(
    string ProposalId,
    string? Description,
    int TickNumber,
    DateTimeOffset Timestamp);

/// <summary>
/// Stack of all actions executed during the orchestration session.
/// Published and updated after each action execution.
/// </summary>
public sealed record ExecutionHistory(IReadOnlyList<ExecutedAction> Actions)
{
    /// <summary>
    /// Adds a new executed action to the history.
    /// </summary>
    public ExecutionHistory WithAction(ExecutedAction action)
    {
        var newActions = Actions.Append(action).ToList();
        return this with { Actions = newActions };
    }
}
