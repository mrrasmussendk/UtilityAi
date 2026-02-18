namespace UtilityAi.Utils;

/// <summary>
/// Represents a serializable snapshot of EventBus state.
/// Captures current facts and optionally history for persistence.
/// </summary>
public sealed record EventBusSnapshot
{
    /// <summary>
    /// Dictionary of type names to serialized fact values.
    /// </summary>
    public Dictionary<string, object> Facts { get; init; } = new();

    /// <summary>
    /// Dictionary of type names to serialized history entries.
    /// </summary>
    public Dictionary<string, List<TimestampedEntry>> History { get; init; } = new();

    /// <summary>
    /// When this snapshot was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Optional identifier for the scope this snapshot represents.
    /// </summary>
    public string? ScopeId { get; init; }

    /// <summary>
    /// A timestamped entry in the history.
    /// </summary>
    public sealed record TimestampedEntry(object Value, DateTimeOffset Timestamp);
}
