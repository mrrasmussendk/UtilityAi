using UtilityAi.Utils;
using Xunit;

namespace Tests;

public class EventBusExtensionsAdditionalTests
{
    private sealed record TestFact(string Value);
    private sealed record OtherFact(int Number);

    #region Snapshot

    [Fact]
    public void Snapshot_NullBus_Throws()
    {
        EventBus bus = null!;
        Assert.Throws<ArgumentNullException>(() => bus.Snapshot(new[] { typeof(TestFact) }));
    }

    [Fact]
    public void Snapshot_NullTypes_Throws()
    {
        var bus = new EventBus();
        Assert.Throws<ArgumentException>(() => bus.Snapshot(null!));
    }

    [Fact]
    public void Snapshot_EmptyTypes_Throws()
    {
        var bus = new EventBus();
        Assert.Throws<ArgumentException>(() => bus.Snapshot(Array.Empty<Type>()));
    }

    [Fact]
    public void Snapshot_IncludeHistory_CapturesHistoryEntries()
    {
        var bus = new EventBus();
        bus.Publish(new TestFact("first"));
        bus.Publish(new TestFact("second"));

        var snapshot = bus.Snapshot(new[] { typeof(TestFact) }, includeHistory: true);

        Assert.Single(snapshot.Facts);
        Assert.True(snapshot.History.ContainsKey(typeof(TestFact).FullName!));
        Assert.Equal(2, snapshot.History[typeof(TestFact).FullName!].Count);
    }

    [Fact]
    public void Snapshot_WithoutHistory_HistoryIsEmpty()
    {
        var bus = new EventBus();
        bus.Publish(new TestFact("data"));

        var snapshot = bus.Snapshot(new[] { typeof(TestFact) }, includeHistory: false);

        Assert.Empty(snapshot.History);
    }

    [Fact]
    public void Snapshot_CapturesScopeId()
    {
        var root = new EventBus();
        var scoped = root.CreateScope("test-scope");
        scoped.Publish(new TestFact("scoped"));

        var snapshot = scoped.Snapshot(new[] { typeof(TestFact) });

        Assert.Equal("test-scope", snapshot.ScopeId);
    }

    [Fact]
    public void Snapshot_SetsTimestamp()
    {
        var bus = new EventBus();
        bus.Publish(new TestFact("data"));
        var before = DateTimeOffset.UtcNow;

        var snapshot = bus.Snapshot(new[] { typeof(TestFact) });

        Assert.True(snapshot.Timestamp <= DateTimeOffset.UtcNow);
        Assert.True(snapshot.Timestamp >= before.AddSeconds(-1));
    }

    [Fact]
    public void Snapshot_TypeWithNoFact_SkipsThatType()
    {
        var bus = new EventBus();
        // Only publish TestFact, not OtherFact
        bus.Publish(new TestFact("data"));

        var snapshot = bus.Snapshot(new[] { typeof(TestFact), typeof(OtherFact) });

        Assert.Single(snapshot.Facts);
        Assert.Contains(typeof(TestFact).FullName!, snapshot.Facts.Keys);
    }

    #endregion

    #region Restore

    [Fact]
    public void Restore_NullBus_Throws()
    {
        EventBus bus = null!;
        var snapshot = new EventBusSnapshot();
        Assert.Throws<ArgumentNullException>(() => bus.Restore(snapshot));
    }

    [Fact]
    public void Restore_NullSnapshot_Throws()
    {
        var bus = new EventBus();
        Assert.Throws<ArgumentNullException>(() => bus.Restore(null!));
    }

    [Fact]
    public void Restore_UnresolvableType_SkipsIt()
    {
        var snapshot = new EventBusSnapshot
        {
            Facts = new Dictionary<string, object>
            {
                ["NonExistent.TypeName"] = "value"
            }
        };

        var bus = new EventBus();
        // Should not throw; just skips unresolvable types
        bus.Restore(snapshot, _ => null);

        // Bus should have no facts
        Assert.Null(bus.GetOrDefault<TestFact>());
    }

    [Fact]
    public void Restore_DefaultTypeResolver_UsesTypeGetType()
    {
        // With no custom resolver, Type.GetType is used.
        // Private nested types won't resolve via Type.GetType, so facts are skipped.
        var bus1 = new EventBus();
        bus1.Publish(new TestFact("hello"));
        var snapshot = bus1.Snapshot(new[] { typeof(TestFact) });

        var bus2 = new EventBus();
        bus2.Restore(snapshot); // no custom resolver

        // TestFact is a private nested type, Type.GetType won't resolve it
        Assert.Null(bus2.GetOrDefault<TestFact>());
    }

    [Fact]
    public void Restore_CustomTypeResolver_RestoresCorrectly()
    {
        var bus1 = new EventBus();
        bus1.Publish(new TestFact("restored"));
        bus1.Publish(new OtherFact(99));

        var snapshot = bus1.Snapshot(new[] { typeof(TestFact), typeof(OtherFact) });

        var bus2 = new EventBus();
        bus2.Restore(snapshot, typeName =>
        {
            if (typeName == typeof(TestFact).FullName) return typeof(TestFact);
            if (typeName == typeof(OtherFact).FullName) return typeof(OtherFact);
            return null;
        });

        Assert.Equal("restored", bus2.GetOrDefault<TestFact>()?.Value);
        Assert.Equal(99, bus2.GetOrDefault<OtherFact>()?.Number);
    }

    #endregion

    #region GetHistoryInWindow

    [Fact]
    public void GetHistoryInWindow_NullBus_Throws()
    {
        EventBus bus = null!;
        Assert.Throws<ArgumentNullException>(() => bus.GetHistoryInWindow<TestFact>(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void GetHistoryInWindow_NoEvents_ReturnsEmpty()
    {
        var bus = new EventBus();
        var results = bus.GetHistoryInWindow<TestFact>(TimeSpan.FromSeconds(10));
        Assert.Empty(results);
    }

    [Fact]
    public void GetHistoryInWindow_AllRecentEvents_ReturnsAll()
    {
        var bus = new EventBus();
        bus.Publish(new TestFact("a"));
        bus.Publish(new TestFact("b"));

        var results = bus.GetHistoryInWindow<TestFact>(TimeSpan.FromSeconds(10));

        Assert.Equal(2, results.Count);
    }

    #endregion

    #region GetHistoryWhere

    [Fact]
    public void GetHistoryWhere_NullBus_Throws()
    {
        EventBus bus = null!;
        Assert.Throws<ArgumentNullException>(() => bus.GetHistoryWhere<TestFact>(_ => true));
    }

    [Fact]
    public void GetHistoryWhere_NullPredicate_Throws()
    {
        var bus = new EventBus();
        Assert.Throws<ArgumentNullException>(() => bus.GetHistoryWhere<TestFact>(null!));
    }

    [Fact]
    public void GetHistoryWhere_MaxResults_LimitsOutput()
    {
        var bus = new EventBus();
        bus.Publish(new OtherFact(1));
        bus.Publish(new OtherFact(2));
        bus.Publish(new OtherFact(3));

        var results = bus.GetHistoryWhere<OtherFact>(_ => true, maxResults: 2);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void GetHistoryWhere_NoMatch_ReturnsEmpty()
    {
        var bus = new EventBus();
        bus.Publish(new OtherFact(5));

        var results = bus.GetHistoryWhere<OtherFact>(f => f.Number > 100);

        Assert.Empty(results);
    }

    #endregion

    #region GetEventFrequency

    [Fact]
    public void GetEventFrequency_NullBus_Throws()
    {
        EventBus bus = null!;
        Assert.Throws<ArgumentNullException>(() => bus.GetEventFrequency<TestFact>(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void GetEventFrequency_ZeroWindow_Throws()
    {
        var bus = new EventBus();
        Assert.Throws<ArgumentException>(() => bus.GetEventFrequency<TestFact>(TimeSpan.Zero));
    }

    [Fact]
    public void GetEventFrequency_NegativeWindow_Throws()
    {
        var bus = new EventBus();
        Assert.Throws<ArgumentException>(() => bus.GetEventFrequency<TestFact>(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void GetEventFrequency_NoEvents_ReturnsZero()
    {
        var bus = new EventBus();
        var freq = bus.GetEventFrequency<TestFact>(TimeSpan.FromSeconds(10));
        Assert.Equal(0.0, freq);
    }

    [Fact]
    public void GetEventFrequency_WithEvents_ReturnsCorrectRate()
    {
        var bus = new EventBus();
        bus.Publish(new TestFact("a"));
        bus.Publish(new TestFact("b"));
        bus.Publish(new TestFact("c"));

        var freq = bus.GetEventFrequency<TestFact>(TimeSpan.FromSeconds(10));

        // 3 events in a 10-second window = 0.3 events/sec
        Assert.Equal(0.3, freq, precision: 1);
    }

    #endregion

    #region GetTimeSinceLastEvent

    [Fact]
    public void GetTimeSinceLastEvent_NullBus_Throws()
    {
        EventBus bus = null!;
        Assert.Throws<ArgumentNullException>(() => bus.GetTimeSinceLastEvent<TestFact>());
    }

    [Fact]
    public void GetTimeSinceLastEvent_NoEvents_ReturnsNull()
    {
        var bus = new EventBus();
        var result = bus.GetTimeSinceLastEvent<TestFact>();
        Assert.Null(result);
    }

    [Fact]
    public void GetTimeSinceLastEvent_AfterPublish_ReturnsNonNull()
    {
        var bus = new EventBus();
        bus.Publish(new TestFact("data"));

        var result = bus.GetTimeSinceLastEvent<TestFact>();

        Assert.NotNull(result);
        Assert.True(result.Value >= TimeSpan.Zero);
        Assert.True(result.Value < TimeSpan.FromSeconds(2));
    }

    #endregion

    #region HasRecentEvent

    [Fact]
    public void HasRecentEvent_NullBus_Throws()
    {
        EventBus bus = null!;
        Assert.Throws<ArgumentNullException>(() => bus.HasRecentEvent<TestFact>(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void HasRecentEvent_NoEvents_ReturnsFalse()
    {
        var bus = new EventBus();
        Assert.False(bus.HasRecentEvent<TestFact>(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void HasRecentEvent_RecentEvent_ReturnsTrue()
    {
        var bus = new EventBus();
        bus.Publish(new TestFact("data"));

        Assert.True(bus.HasRecentEvent<TestFact>(TimeSpan.FromSeconds(10)));
    }

    #endregion

    #region EventBusSnapshot properties

    [Fact]
    public void EventBusSnapshot_DefaultProperties()
    {
        var snapshot = new EventBusSnapshot();

        Assert.NotNull(snapshot.Facts);
        Assert.Empty(snapshot.Facts);
        Assert.NotNull(snapshot.History);
        Assert.Empty(snapshot.History);
        Assert.Null(snapshot.ScopeId);
        Assert.Equal(default, snapshot.Timestamp);
    }

    [Fact]
    public void EventBusSnapshot_InitProperties()
    {
        var ts = DateTimeOffset.UtcNow;
        var snapshot = new EventBusSnapshot
        {
            Facts = new Dictionary<string, object> { ["key"] = "value" },
            History = new Dictionary<string, List<EventBusSnapshot.TimestampedEntry>>
            {
                ["key"] = new List<EventBusSnapshot.TimestampedEntry>
                {
                    new("val", ts)
                }
            },
            Timestamp = ts,
            ScopeId = "scope-1"
        };

        Assert.Single(snapshot.Facts);
        Assert.Equal("value", snapshot.Facts["key"]);
        Assert.Single(snapshot.History);
        Assert.Equal(ts, snapshot.Timestamp);
        Assert.Equal("scope-1", snapshot.ScopeId);
    }

    [Fact]
    public void TimestampedEntry_StoresValueAndTimestamp()
    {
        var ts = DateTimeOffset.UtcNow;
        var entry = new EventBusSnapshot.TimestampedEntry("hello", ts);

        Assert.Equal("hello", entry.Value);
        Assert.Equal(ts, entry.Timestamp);
    }

    #endregion
}
