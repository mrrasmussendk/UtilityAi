using UtilityAi.Capabilities.BuiltIn;
using UtilityAi.Facts;
using UtilityAi.Sensor.BuiltIn;
using UtilityAi.Utils;
using Xunit;

namespace Tests;

public class BuiltInSensorTests
{
    // ── ConversationHistorySensor ───────────────────────────────────────

    [Fact]
    public async Task ConversationHistorySensor_PublishesZeroMetadata_WhenNoMessages()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        var sensor = new ConversationHistorySensor();

        await sensor.SenseAsync(rt, CancellationToken.None);

        var meta = bus.GetOrDefault<ConversationMetadata>();
        Assert.NotNull(meta);
        Assert.Equal(0, meta.MessageCount);
        Assert.Equal(TimeSpan.Zero, meta.Duration);
        Assert.False(meta.IsLongConversation);
        Assert.Null(meta.FirstMessageTime);
        Assert.Null(meta.LastMessageTime);
    }

    [Fact]
    public async Task ConversationHistorySensor_PublishesCorrectCounts_WhenMessagesExist()
    {
        var bus = new EventBus();
        var now = DateTimeOffset.UtcNow;

        bus.Publish(new UserMessage("hi", "u1", now));
        bus.Publish(new AssistantMessage("hello", now.AddSeconds(1)));
        bus.Publish(new UserMessage("how?", "u1", now.AddSeconds(2)));

        var rt = new Runtime(bus, 1);
        var sensor = new ConversationHistorySensor();
        await sensor.SenseAsync(rt, CancellationToken.None);

        var meta = bus.GetOrDefault<ConversationMetadata>();
        Assert.NotNull(meta);
        Assert.Equal(3, meta.MessageCount);
        Assert.True(meta.Duration > TimeSpan.Zero);
        Assert.False(meta.IsLongConversation);
        Assert.NotNull(meta.FirstMessageTime);
        Assert.NotNull(meta.LastMessageTime);
    }

    [Fact]
    public async Task ConversationHistorySensor_DetectsLongConversation()
    {
        var bus = new EventBus();
        var now = DateTimeOffset.UtcNow;
        const int threshold = 5;

        for (var i = 0; i < threshold; i++)
            bus.Publish(new UserMessage($"msg{i}", "u1", now.AddSeconds(i)));

        var rt = new Runtime(bus, 1);
        var sensor = new ConversationHistorySensor(longConversationThreshold: threshold);
        await sensor.SenseAsync(rt, CancellationToken.None);

        var meta = bus.GetOrDefault<ConversationMetadata>();
        Assert.NotNull(meta);
        Assert.True(meta.IsLongConversation);
    }

    // ── EventFrequencySensor ───────────────────────────────────────────

    [Fact]
    public void EventFrequencySensor_ThrowsOnZeroTimeWindow()
    {
        Assert.Throws<ArgumentException>(() =>
            new EventFrequencySensor<UserMessage, FreqFact>(
                TimeSpan.Zero, (c, r) => new FreqFact(c, r)));
    }

    [Fact]
    public void EventFrequencySensor_ThrowsOnNullFactory()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new EventFrequencySensor<UserMessage, FreqFact>(
                TimeSpan.FromSeconds(10), null!));
    }

    [Fact]
    public async Task EventFrequencySensor_PublishesFact_WithCorrectCount()
    {
        var bus = new EventBus();
        var now = DateTimeOffset.UtcNow;

        bus.Publish(new UserMessage("a", "u1", now));
        bus.Publish(new UserMessage("b", "u1", now));

        var sensor = new EventFrequencySensor<UserMessage, FreqFact>(
            TimeSpan.FromMinutes(1),
            (count, rate) => new FreqFact(count, rate));

        var rt = new Runtime(bus, 0);
        await sensor.SenseAsync(rt, CancellationToken.None);

        var fact = bus.GetOrDefault<FreqFact>();
        Assert.NotNull(fact);
        Assert.True(fact.Count >= 1);
        Assert.True(fact.Rate >= 0);
    }

    [Fact]
    public async Task EventFrequencySensor_PublishesZeroCount_WhenNoEvents()
    {
        var bus = new EventBus();
        var sensor = new EventFrequencySensor<UserMessage, FreqFact>(
            TimeSpan.FromMinutes(1),
            (count, rate) => new FreqFact(count, rate));

        var rt = new Runtime(bus, 0);
        await sensor.SenseAsync(rt, CancellationToken.None);

        var fact = bus.GetOrDefault<FreqFact>();
        Assert.NotNull(fact);
        Assert.Equal(0, fact.Count);
        Assert.Equal(0.0, fact.Rate);
    }

    // ── ResourceSensor ─────────────────────────────────────────────────

    [Fact]
    public async Task ResourceSensor_PublishesResourceUsage()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        var sensor = new ResourceSensor();

        await sensor.SenseAsync(rt, CancellationToken.None);

        var usage = bus.GetOrDefault<ResourceUsage>();
        Assert.NotNull(usage);
        Assert.True(usage.CpuPercent >= 0);
        Assert.True(usage.MemoryMegabytes > 0);
    }

    // ── CleanupModule ──────────────────────────────────────────────────

    [Fact]
    public void CleanupModule_ThrowsOnNullTypes()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CleanupModule(null!));
    }

    [Fact]
    public void CleanupModule_ProposesNothing_WhenNoElapsedTime()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        var module = new CleanupModule(new[] { typeof(UserMessage) });

        var proposals = module.Propose(rt).ToList();
        Assert.Empty(proposals);
    }

    [Fact]
    public void CleanupModule_ProposesNothing_WhenElapsedTimeBelowInterval()
    {
        var bus = new EventBus();
        bus.Publish(new ElapsedTime(TimeSpan.FromSeconds(10)));

        var rt = new Runtime(bus, 1);
        var module = new CleanupModule(
            new[] { typeof(UserMessage) },
            cleanupInterval: TimeSpan.FromMinutes(5));

        var proposals = module.Propose(rt).ToList();
        Assert.Empty(proposals);
    }

    [Fact]
    public void CleanupModule_ProposesCleanup_WhenElapsedTimeExceedsInterval()
    {
        var bus = new EventBus();
        bus.Publish(new ElapsedTime(TimeSpan.FromMinutes(6)));

        var rt = new Runtime(bus, 1);
        var module = new CleanupModule(
            new[] { typeof(UserMessage) },
            cleanupInterval: TimeSpan.FromMinutes(5));

        var proposals = module.Propose(rt).ToList();
        Assert.Single(proposals);
        Assert.Equal("cleanup.old-facts", proposals[0].Id);
    }

    [Fact]
    public async Task CleanupModule_ProposalAction_ClearsFactsFromBus()
    {
        var bus = new EventBus();
        bus.Publish(new ElapsedTime(TimeSpan.FromMinutes(10)));
        bus.Publish(new UserMessage("hi", "u1", DateTimeOffset.UtcNow));

        var rt = new Runtime(bus, 1);
        var module = new CleanupModule(
            new[] { typeof(UserMessage) },
            cleanupInterval: TimeSpan.FromMinutes(5));

        var proposal = module.Propose(rt).First();
        await proposal.Act(CancellationToken.None);

        Assert.Null(bus.GetOrDefault<UserMessage>());

        var cleanup = bus.GetOrDefault<CleanupExecuted>();
        Assert.NotNull(cleanup);
    }

    // ── Helper types ───────────────────────────────────────────────────

    private sealed record FreqFact(int Count, double Rate);
}
