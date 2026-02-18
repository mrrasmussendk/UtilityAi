using UtilityAi.Utils;
using Xunit;

namespace Tests.Utils;

public class EventBusExtensionsTests
{
    private record TestFact(string Data);
    private record AnotherFact(int Value);

    [Fact]
    public void Snapshot_CapturesCurrentFacts()
    {
        var bus = new EventBus();
        bus.Publish(new TestFact("data1"));
        bus.Publish(new AnotherFact(42));

        var snapshot = bus.Snapshot(new[] { typeof(TestFact), typeof(AnotherFact) });

        Assert.Equal(2, snapshot.Facts.Count);
        Assert.Contains(typeof(TestFact).FullName!, snapshot.Facts.Keys);
        Assert.Contains(typeof(AnotherFact).FullName!, snapshot.Facts.Keys);
    }

    [Fact]
    public void Restore_RestoresFacts()
    {
        var bus1 = new EventBus();
        bus1.Publish(new TestFact("original"));

        var snapshot = bus1.Snapshot(new[] { typeof(TestFact) });

        var bus2 = new EventBus();
        // Provide a type resolver that can find types in the current assembly
        bus2.Restore(snapshot, typeName =>
        {
            if (typeName == typeof(TestFact).FullName)
                return typeof(TestFact);
            return Type.GetType(typeName);
        });

        var restored = bus2.GetOrDefault<TestFact>();
        Assert.NotNull(restored);
        Assert.Equal("original", restored.Data);
    }

    [Fact]
    public async Task GetHistoryInWindow_FiltersCorrectly()
    {
        var bus = new EventBus();

        bus.Publish(new TestFact("old"));
        await Task.Delay(100);
        bus.Publish(new TestFact("recent"));

        var results = bus.GetHistoryInWindow<TestFact>(TimeSpan.FromMilliseconds(50));

        Assert.Single(results);
        Assert.Equal("recent", results[0].Value.Data);
    }

    [Fact]
    public void GetHistoryWhere_FiltersWithPredicate()
    {
        var bus = new EventBus();
        bus.Publish(new AnotherFact(10));
        bus.Publish(new AnotherFact(50));
        bus.Publish(new AnotherFact(30));

        var results = bus.GetHistoryWhere<AnotherFact>(f => f.Value > 20);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void GetTimeSinceLastEvent_ReturnsCorrectTimespan()
    {
        var bus = new EventBus();
        bus.Publish(new TestFact("data"));

        var timeSince = bus.GetTimeSinceLastEvent<TestFact>();

        Assert.NotNull(timeSince);
        Assert.True(timeSince.Value.TotalMilliseconds >= 0);
        Assert.True(timeSince.Value.TotalSeconds < 1); // Should be very recent
    }

    [Fact]
    public void HasRecentEvent_ReturnsTrueForRecentEvent()
    {
        var bus = new EventBus();
        bus.Publish(new TestFact("data"));

        var hasRecent = bus.HasRecentEvent<TestFact>(TimeSpan.FromSeconds(10));

        Assert.True(hasRecent);
    }
}
