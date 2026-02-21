using System.Reflection;
using UtilityAi.Memory;
using UtilityAi.Utils;
using Xunit;

namespace Tests.Memory;

public class MemorySensorTests
{
    [Fact]
    public async Task SenseAsync_WhenCalledBeforeInterval_DoesNotArchive()
    {
        var store = new RecordingMemoryStore();
        var sensor = new MemorySensor(store, TimeSpan.Zero, typeof(string));
        var rt = new Runtime(new EventBus(), 0);
        rt.Bus.Publish("message");

        await sensor.SenseAsync(rt, CancellationToken.None);

        Assert.Empty(store.Stored);
    }

    [Fact]
    public async Task SenseAsync_WhenIntervalElapsed_ArchivesMatchingTypes()
    {
        var store = new RecordingMemoryStore();
        var sensor = new MemorySensor(store, TimeSpan.Zero, typeof(string));
        SetLastArchiveTime(sensor, DateTimeOffset.UtcNow.AddMinutes(-2));
        var rt = new Runtime(new EventBus(), 0);
        rt.Bus.Publish("archivable");

        await sensor.SenseAsync(rt, CancellationToken.None);

        Assert.Single(store.Stored);
        Assert.Equal("archivable", store.Stored[0].Fact);
    }

    private static void SetLastArchiveTime(MemorySensor sensor, DateTimeOffset value)
    {
        var field = typeof(MemorySensor).GetField("_lastArchiveTime", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(sensor, value);
    }

    private sealed class RecordingMemoryStore : IMemoryStore
    {
        public List<TimestampedMemory<string>> Stored { get; } = new();

        public Task StoreAsync<T>(T fact, DateTimeOffset timestamp, CancellationToken ct = default) where T : class
        {
            if (fact is string value)
                Stored.Add(new TimestampedMemory<string>(value, timestamp));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TimestampedMemory<T>>> RecallAsync<T>(MemoryQuery query, CancellationToken ct = default) where T : class
            => Task.FromResult<IReadOnlyList<TimestampedMemory<T>>>(Array.Empty<TimestampedMemory<T>>());

        public Task<int> CountAsync<T>(CancellationToken ct = default) where T : class
            => Task.FromResult(0);

        public Task PruneAsync(TimeSpan retentionPeriod, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
