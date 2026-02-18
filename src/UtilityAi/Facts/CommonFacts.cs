namespace UtilityAi.Facts;

/// <summary>
/// Current system time.
/// </summary>
public sealed record CurrentTime(DateTimeOffset Value);

/// <summary>
/// Current orchestration tick number.
/// </summary>
public sealed record TickNumber(int Value);

/// <summary>
/// Elapsed time since orchestration started.
/// </summary>
public sealed record ElapsedTime(TimeSpan Value);

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
