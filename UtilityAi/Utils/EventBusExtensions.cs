using System.Reflection;

namespace UtilityAi.Utils;

/// <summary>
/// Extension methods for EventBus providing persistence, querying, and utility functions.
/// </summary>
public static class EventBusExtensions
{
    /// <summary>
    /// Creates a snapshot of the EventBus state for the specified fact types.
    /// </summary>
    /// <param name="bus">The EventBus to snapshot.</param>
    /// <param name="typesToCapture">Specific types to capture. If empty, attempts to capture registered types.</param>
    /// <param name="includeHistory">Whether to include history in the snapshot. Default is false.</param>
    /// <returns>A serializable snapshot of the bus state.</returns>
    public static EventBusSnapshot Snapshot(
        this EventBus bus,
        Type[] typesToCapture,
        bool includeHistory = false)
    {
        if (bus == null) throw new ArgumentNullException(nameof(bus));
        if (typesToCapture == null || typesToCapture.Length == 0)
            throw new ArgumentException("Must specify at least one type to capture.", nameof(typesToCapture));

        var snapshot = new EventBusSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            ScopeId = bus.ScopeId
        };

        foreach (var type in typesToCapture)
        {
            // Get latest value
            var getMethod = typeof(EventBus)
                .GetMethod(nameof(EventBus.GetOrDefault))!
                .MakeGenericMethod(type);

            var value = getMethod.Invoke(bus, null);
            if (value != null)
            {
                snapshot.Facts[type.FullName ?? type.Name] = value;
            }

            // Optionally include history
            if (includeHistory)
            {
                var historyMethod = typeof(EventBus)
                    .GetMethod(nameof(EventBus.GetHistory))!
                    .MakeGenericMethod(type);

                var history = historyMethod.Invoke(bus, new object?[] { null }) as System.Collections.IEnumerable;

                if (history != null)
                {
                    var entries = new List<EventBusSnapshot.TimestampedEntry>();

                    foreach (var item in history)
                    {
                        var valueProp = item.GetType().GetProperty("Value");
                        var timestampProp = item.GetType().GetProperty("Timestamp");

                        if (valueProp != null && timestampProp != null)
                        {
                            var val = valueProp.GetValue(item);
                            var ts = (DateTimeOffset)timestampProp.GetValue(item)!;

                            if (val != null)
                            {
                                entries.Add(new EventBusSnapshot.TimestampedEntry(val, ts));
                            }
                        }
                    }

                    if (entries.Count > 0)
                    {
                        snapshot.History[type.FullName ?? type.Name] = entries;
                    }
                }
            }
        }

        return snapshot;
    }

    /// <summary>
    /// Restores facts from a snapshot to the EventBus.
    /// </summary>
    /// <param name="bus">The EventBus to restore to.</param>
    /// <param name="snapshot">The snapshot to restore from.</param>
    /// <param name="typeResolver">Function to resolve type names to Type objects. If null, uses Type.GetType.</param>
    public static void Restore(
        this EventBus bus,
        EventBusSnapshot snapshot,
        Func<string, Type?>? typeResolver = null)
    {
        if (bus == null) throw new ArgumentNullException(nameof(bus));
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

        typeResolver ??= Type.GetType;

        // Restore facts
        foreach (var (typeName, value) in snapshot.Facts)
        {
            var type = typeResolver(typeName);
            if (type == null) continue;

            var publishMethod = typeof(EventBus)
                .GetMethod(nameof(EventBus.Publish))!
                .MakeGenericMethod(type);

            publishMethod.Invoke(bus, new[] { value });
        }
    }

    /// <summary>
    /// Gets events within a time window from now.
    /// </summary>
    /// <typeparam name="T">The type of events to retrieve.</typeparam>
    /// <param name="bus">The EventBus to query.</param>
    /// <param name="window">Time window to look back.</param>
    /// <returns>Events within the time window, newest first.</returns>
    public static IReadOnlyList<TimestampedEvent<T>> GetHistoryInWindow<T>(
        this EventBus bus,
        TimeSpan window)
    {
        if (bus == null) throw new ArgumentNullException(nameof(bus));

        var cutoff = DateTimeOffset.UtcNow - window;
        var allHistory = bus.GetHistory<T>();

        return allHistory
            .Where(e => e.Timestamp >= cutoff)
            .OrderByDescending(e => e.Timestamp)
            .ToList();
    }

    /// <summary>
    /// Gets events matching a predicate.
    /// </summary>
    /// <typeparam name="T">The type of events to retrieve.</typeparam>
    /// <param name="bus">The EventBus to query.</param>
    /// <param name="predicate">Predicate to filter events.</param>
    /// <param name="maxResults">Maximum number of results. Default is all matching.</param>
    /// <returns>Events matching the predicate.</returns>
    public static IReadOnlyList<TimestampedEvent<T>> GetHistoryWhere<T>(
        this EventBus bus,
        Func<T, bool> predicate,
        int? maxResults = null)
    {
        if (bus == null) throw new ArgumentNullException(nameof(bus));
        if (predicate == null) throw new ArgumentNullException(nameof(predicate));

        var allHistory = bus.GetHistory<T>();
        var filtered = allHistory.Where(e => predicate(e.Value));

        if (maxResults.HasValue)
            filtered = filtered.Take(maxResults.Value);

        return filtered.ToList();
    }

    /// <summary>
    /// Calculates the frequency of events over a time window.
    /// </summary>
    /// <typeparam name="T">The type of events to measure.</typeparam>
    /// <param name="bus">The EventBus to query.</param>
    /// <param name="window">Time window to measure frequency over.</param>
    /// <returns>Events per second within the time window.</returns>
    public static double GetEventFrequency<T>(
        this EventBus bus,
        TimeSpan window)
    {
        if (bus == null) throw new ArgumentNullException(nameof(bus));
        if (window <= TimeSpan.Zero)
            throw new ArgumentException("Window must be positive.", nameof(window));

        var events = bus.GetHistoryInWindow<T>(window);
        return events.Count / window.TotalSeconds;
    }

    /// <summary>
    /// Gets the time elapsed since the most recent event of a given type.
    /// </summary>
    /// <typeparam name="T">The type of event to check.</typeparam>
    /// <param name="bus">The EventBus to query.</param>
    /// <returns>Time elapsed since most recent event, or null if no events exist.</returns>
    public static TimeSpan? GetTimeSinceLastEvent<T>(this EventBus bus)
    {
        if (bus == null) throw new ArgumentNullException(nameof(bus));

        var history = bus.GetHistory<T>(maxItems: 1);
        if (history.Count == 0) return null;

        return DateTimeOffset.UtcNow - history[^1].Timestamp;
    }

    /// <summary>
    /// Checks if an event of the given type has occurred within a time window.
    /// </summary>
    /// <typeparam name="T">The type of event to check.</typeparam>
    /// <param name="bus">The EventBus to query.</param>
    /// <param name="window">Time window to check.</param>
    /// <returns>True if at least one event occurred within the window.</returns>
    public static bool HasRecentEvent<T>(this EventBus bus, TimeSpan window)
    {
        if (bus == null) throw new ArgumentNullException(nameof(bus));

        var timeSince = bus.GetTimeSinceLastEvent<T>();
        return timeSince.HasValue && timeSince.Value <= window;
    }
}
