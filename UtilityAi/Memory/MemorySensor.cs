using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace UtilityAi.Memory;

/// <summary>
/// Sensor that automatically archives EventBus facts to long-term memory.
/// Periodically moves old events from EventBus history to IMemoryStore.
/// </summary>
public sealed class MemorySensor : ISensor
{
    private readonly IMemoryStore _store;
    private readonly TimeSpan _archiveThreshold;
    private readonly Type[] _typesToArchive;
    private DateTimeOffset _lastArchiveTime;

    /// <summary>
    /// Creates a memory sensor.
    /// </summary>
    /// <param name="store">The memory store to archive to.</param>
    /// <param name="archiveThreshold">Archive events older than this threshold. Default is 5 minutes.</param>
    /// <param name="typesToArchive">Specific types to archive. If empty, archives all types in history.</param>
    public MemorySensor(
        IMemoryStore store,
        TimeSpan? archiveThreshold = null,
        params Type[] typesToArchive)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _archiveThreshold = archiveThreshold ?? TimeSpan.FromMinutes(5);
        _typesToArchive = typesToArchive ?? Array.Empty<Type>();
        _lastArchiveTime = DateTimeOffset.UtcNow;
    }

    public async Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        // Only archive periodically (e.g., every 10 ticks or 1 minute)
        if (DateTimeOffset.UtcNow - _lastArchiveTime < TimeSpan.FromMinutes(1))
            return;

        await ArchiveOldEventsAsync(rt.Bus, ct);
        _lastArchiveTime = DateTimeOffset.UtcNow;
    }

    private async Task ArchiveOldEventsAsync(EventBus bus, CancellationToken ct)
    {
        var cutoffTime = DateTimeOffset.UtcNow - _archiveThreshold;

        // Archive events older than threshold
        // Note: This is a simplified implementation
        // In production, you'd need reflection or registration to enumerate all types
        if (_typesToArchive.Length > 0)
        {
            foreach (var type in _typesToArchive)
            {
                await ArchiveTypeAsync(bus, type, cutoffTime, ct);
            }
        }
    }

    private async Task ArchiveTypeAsync(EventBus bus, Type type, DateTimeOffset cutoffTime, CancellationToken ct)
    {
        // Use reflection to call GetHistory<T> on the bus
        var getHistoryMethod = typeof(EventBus)
            .GetMethod(nameof(EventBus.GetHistory))!
            .MakeGenericMethod(type);

        var history = getHistoryMethod.Invoke(bus, new object?[] { null }) as System.Collections.IEnumerable;

        if (history == null) return;

        foreach (var item in history)
        {
            var timestampProp = item.GetType().GetProperty("Timestamp");
            var valueProp = item.GetType().GetProperty("Value");

            if (timestampProp == null || valueProp == null) continue;

            var timestamp = (DateTimeOffset)timestampProp.GetValue(item)!;
            var value = valueProp.GetValue(item);

            if (value == null || timestamp >= cutoffTime) continue;

            // Archive old event
            var storeMethod = typeof(IMemoryStore)
                .GetMethod(nameof(IMemoryStore.StoreAsync))!
                .MakeGenericMethod(type);

            await (Task)storeMethod.Invoke(_store, new[] { value, timestamp, ct })!;
        }
    }
}
