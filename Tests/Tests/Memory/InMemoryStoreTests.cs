using UtilityAi.Memory;
using Xunit;

namespace Tests.Memory;

public class InMemoryStoreTests
{
    private record TestFact(string Data);

    [Fact]
    public async Task InMemoryStore_StoreAndRecall_ReturnsStoredFact()
    {
        var store = new InMemoryStore();
        var fact = new TestFact("test-data");
        var timestamp = DateTimeOffset.UtcNow;

        await store.StoreAsync(fact, timestamp);

        var query = new MemoryQuery { MaxResults = 10 };
        var results = await store.RecallAsync<TestFact>(query);

        Assert.Single(results);
        Assert.Equal("test-data", results[0].Fact.Data);
        Assert.Equal(timestamp, results[0].Timestamp);
    }

    [Fact]
    public async Task InMemoryStore_RecallWithTimeWindow_FiltersCorrectly()
    {
        var store = new InMemoryStore();
        var now = DateTimeOffset.UtcNow;

        await store.StoreAsync(new TestFact("old"), now.AddMinutes(-10));
        await store.StoreAsync(new TestFact("recent"), now.AddSeconds(-30));

        var query = new MemoryQuery { TimeWindow = TimeSpan.FromMinutes(1) };
        var results = await store.RecallAsync<TestFact>(query);

        Assert.Single(results);
        Assert.Equal("recent", results[0].Fact.Data);
    }

    [Fact]
    public async Task InMemoryStore_Prune_RemovesOldFacts()
    {
        var store = new InMemoryStore();
        var now = DateTimeOffset.UtcNow;

        await store.StoreAsync(new TestFact("old"), now.AddHours(-2));
        await store.StoreAsync(new TestFact("recent"), now.AddMinutes(-5));

        await store.PruneAsync(TimeSpan.FromHours(1));

        var query = new MemoryQuery { MaxResults = 10 };
        var results = await store.RecallAsync<TestFact>(query);

        Assert.Single(results);
        Assert.Equal("recent", results[0].Fact.Data);
    }

    [Fact]
    public async Task InMemoryStore_Count_ReturnsCorrectCount()
    {
        var store = new InMemoryStore();
        var now = DateTimeOffset.UtcNow;

        await store.StoreAsync(new TestFact("fact1"), now);
        await store.StoreAsync(new TestFact("fact2"), now);
        await store.StoreAsync(new TestFact("fact3"), now);

        var count = await store.CountAsync<TestFact>();
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task InMemoryStore_InterfaceConvenienceMethods_DelegateToAsyncMethods()
    {
        IMemoryStore store = new InMemoryStore();
        var now = DateTimeOffset.UtcNow;

        await store.Store(new TestFact("fact"), now);
        var count = await store.Count<TestFact>();
        var results = await store.Recall<TestFact>(new MemoryQuery { MaxResults = 1 });

        Assert.Equal(1, count);
        Assert.Single(results);
        Assert.Equal("fact", results[0].Fact.Data);
    }
}
