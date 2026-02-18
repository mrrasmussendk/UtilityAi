namespace UtilityAi.Memory;

/// <summary>
/// Defines long-term memory storage for facts beyond EventBus history limits.
/// Implementations can use in-memory, file-based, or database storage.
/// </summary>
public interface IMemoryStore
{
    /// <summary>
    /// Stores a fact with timestamp for long-term retention.
    /// </summary>
    /// <typeparam name="T">The type of fact to store.</typeparam>
    /// <param name="fact">The fact instance to store.</param>
    /// <param name="timestamp">When the fact occurred.</param>
    /// <param name="ct">Cancellation token.</param>
    Task StoreAsync<T>(T fact, DateTimeOffset timestamp, CancellationToken ct = default) where T : class;

    /// <summary>
    /// Recalls facts from memory based on a query.
    /// </summary>
    /// <typeparam name="T">The type of facts to recall.</typeparam>
    /// <param name="query">Query parameters for recall.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of matching facts with their timestamps.</returns>
    Task<IReadOnlyList<TimestampedMemory<T>>> RecallAsync<T>(
        MemoryQuery query,
        CancellationToken ct = default) where T : class;

    /// <summary>
    /// Counts the number of stored facts of a given type.
    /// </summary>
    /// <typeparam name="T">The type of facts to count.</typeparam>
    /// <param name="ct">Cancellation token.</param>
    Task<int> CountAsync<T>(CancellationToken ct = default) where T : class;

    /// <summary>
    /// Removes old facts beyond a retention period.
    /// </summary>
    /// <param name="retentionPeriod">How long to keep facts.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PruneAsync(TimeSpan retentionPeriod, CancellationToken ct = default);
}

/// <summary>
/// A fact retrieved from memory with its timestamp.
/// </summary>
/// <typeparam name="T">The type of the fact.</typeparam>
public sealed record TimestampedMemory<T>(T Fact, DateTimeOffset Timestamp) where T : class;

/// <summary>
/// Query parameters for recalling facts from memory.
/// </summary>
public sealed record MemoryQuery
{
    /// <summary>
    /// Maximum number of results to return. Default is 100.
    /// </summary>
    public int MaxResults { get; init; } = 100;

    /// <summary>
    /// Only return facts after this timestamp.
    /// </summary>
    public DateTimeOffset? After { get; init; }

    /// <summary>
    /// Only return facts before this timestamp.
    /// </summary>
    public DateTimeOffset? Before { get; init; }

    /// <summary>
    /// Time window to search within (from now backwards).
    /// </summary>
    public TimeSpan? TimeWindow { get; init; }

    /// <summary>
    /// Sort order for results.
    /// </summary>
    public SortOrder SortOrder { get; init; } = SortOrder.NewestFirst;
}

/// <summary>
/// Sort order for memory recall.
/// </summary>
public enum SortOrder
{
    NewestFirst,
    OldestFirst
}
