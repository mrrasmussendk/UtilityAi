namespace UtilityAi.Memory;

/// <summary>
/// In-memory implementation of IMemoryStore for simple scenarios and testing.
/// Stores all facts in memory using thread-safe collections.
/// Data is lost when the process ends.
/// </summary>
public sealed class InMemoryStore : IMemoryStore
{
    private readonly Dictionary<Type, List<MemoryEntry>> _storage = new();
    private readonly object _lock = new();

    public Task StoreAsync<T>(T fact, DateTimeOffset timestamp, CancellationToken ct = default) where T : class
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));

        lock (_lock)
        {
            var type = typeof(T);
            if (!_storage.ContainsKey(type))
                _storage[type] = new List<MemoryEntry>();

            _storage[type].Add(new MemoryEntry(fact, timestamp));
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TimestampedMemory<T>>> RecallAsync<T>(
        MemoryQuery query,
        CancellationToken ct = default) where T : class
    {
        if (query == null) throw new ArgumentNullException(nameof(query));

        lock (_lock)
        {
            var type = typeof(T);
            if (!_storage.TryGetValue(type, out var entries))
                return Task.FromResult<IReadOnlyList<TimestampedMemory<T>>>(Array.Empty<TimestampedMemory<T>>());

            var filtered = entries.AsEnumerable();

            // Apply time filters
            if (query.TimeWindow.HasValue)
            {
                var cutoff = DateTimeOffset.UtcNow - query.TimeWindow.Value;
                filtered = filtered.Where(e => e.Timestamp >= cutoff);
            }
            else
            {
                if (query.After.HasValue)
                    filtered = filtered.Where(e => e.Timestamp >= query.After.Value);

                if (query.Before.HasValue)
                    filtered = filtered.Where(e => e.Timestamp <= query.Before.Value);
            }

            // Sort
            filtered = query.SortOrder == SortOrder.NewestFirst
                ? filtered.OrderByDescending(e => e.Timestamp)
                : filtered.OrderBy(e => e.Timestamp);

            // Limit results
            var results = filtered
                .Take(query.MaxResults)
                .Select(e => new TimestampedMemory<T>((T)e.Fact, e.Timestamp))
                .ToList();

            return Task.FromResult<IReadOnlyList<TimestampedMemory<T>>>(results);
        }
    }

    public Task<int> CountAsync<T>(CancellationToken ct = default) where T : class
    {
        lock (_lock)
        {
            var type = typeof(T);
            return Task.FromResult(_storage.TryGetValue(type, out var entries) ? entries.Count : 0);
        }
    }

    public Task PruneAsync(TimeSpan retentionPeriod, CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow - retentionPeriod;

        lock (_lock)
        {
            foreach (var type in _storage.Keys.ToList())
            {
                _storage[type].RemoveAll(e => e.Timestamp < cutoff);

                // Remove empty lists
                if (_storage[type].Count == 0)
                    _storage.Remove(type);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Clears all stored memories. Useful for testing.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _storage.Clear();
        }
    }

    private sealed record MemoryEntry(object Fact, DateTimeOffset Timestamp);
}
