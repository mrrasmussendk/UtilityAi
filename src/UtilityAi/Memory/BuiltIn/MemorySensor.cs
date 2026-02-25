using UtilityAi.Sensor;
using UtilityAi.Utils;
using System.Reflection;

namespace UtilityAi.Memory;

/// <summary>
/// Sensor that automatically archives EventBus facts to long-term memory.
/// Periodically moves old events from EventBus history to IMemoryStore.
/// </summary>
public sealed class MemorySensor : ISensor
{
    private static readonly MethodInfo? EventBusGetHistoryMethod = typeof(EventBus).GetMethod(nameof(EventBus.GetHistory));
    private static readonly MethodInfo? MemoryStoreStoreAsyncMethod = typeof(IMemoryStore).GetMethod(nameof(IMemoryStore.StoreAsync));

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
        if (EventBusGetHistoryMethod == null)
            throw new InvalidOperationException($"{nameof(EventBus)}.{nameof(EventBus.GetHistory)} method was not found.");

        var getHistoryMethod = EventBusGetHistoryMethod.MakeGenericMethod(type);

        var history = getHistoryMethod.Invoke(bus, new object?[] { null }) as System.Collections.IEnumerable;

        if (history == null) return;

        if (MemoryStoreStoreAsyncMethod == null)
            throw new InvalidOperationException($"{nameof(IMemoryStore)}.{nameof(IMemoryStore.StoreAsync)} method was not found.");

        var storeMethod = MemoryStoreStoreAsyncMethod.MakeGenericMethod(type);
        Type? historyItemType = null;
        PropertyInfo? timestampProp = null;
        PropertyInfo? valueProp = null;

        foreach (var item in history)
        {
            if (historyItemType == null)
            {
                historyItemType = item.GetType();
                timestampProp = historyItemType.GetProperty("Timestamp")
                    ?? throw new InvalidOperationException($"Could not find 'Timestamp' property on {historyItemType.Name}.");
                valueProp = historyItemType.GetProperty("Value")
                    ?? throw new InvalidOperationException($"Could not find 'Value' property on {historyItemType.Name}.");
            }

            if (timestampProp is null || valueProp is null)
                continue;

            var timestampValue = timestampProp.GetValue(item);
            if (timestampValue is not DateTimeOffset timestamp)
                continue;
            var value = valueProp.GetValue(item);

            if (value == null || timestamp >= cutoffTime) continue;

            // Archive old event
            var storeTask = storeMethod.Invoke(_store, new[] { value, timestamp, ct }) as Task;
            if (storeTask == null)
                throw new InvalidOperationException($"{nameof(IMemoryStore)}.{nameof(IMemoryStore.StoreAsync)} returned a null task.");

            await storeTask;
        }
    }
}
