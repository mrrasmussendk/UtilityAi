using UtilityAi.Utils;
using Xunit;

namespace Tests;

public class EventBusV2Tests
{
    private record TestMessage(string Text);
    private record OtherMessage(int Value);

    [Fact]
    public void GetHistory_ReturnsEmptyForUnpublishedType()
    {
        var bus = new EventBus();
        var history = bus.GetHistory<TestMessage>();

        Assert.Empty(history);
    }

    [Fact]
    public void GetHistory_ReturnsPublishedEvents()
    {
        var bus = new EventBus();

        bus.Publish(new TestMessage("first"));
        bus.Publish(new TestMessage("second"));
        bus.Publish(new TestMessage("third"));

        var history = bus.GetHistory<TestMessage>();

        Assert.Equal(3, history.Count);
        Assert.Equal("first", history[0].Value.Text);
        Assert.Equal("second", history[1].Value.Text);
        Assert.Equal("third", history[2].Value.Text);
    }

    [Fact]
    public void GetHistory_LimitsToMaxItems()
    {
        var bus = new EventBus();

        for (int i = 0; i < 10; i++)
            bus.Publish(new TestMessage($"msg-{i}"));

        var history = bus.GetHistory<TestMessage>(maxItems: 3);

        Assert.Equal(3, history.Count);
        // Should return last 3
        Assert.Equal("msg-7", history[0].Value.Text);
        Assert.Equal("msg-8", history[1].Value.Text);
        Assert.Equal("msg-9", history[2].Value.Text);
    }

    [Fact]
    public void GetHistory_RespectsMaxHistoryPerType()
    {
        var bus = new EventBus(maxHistoryPerType: 5);

        for (int i = 0; i < 10; i++)
            bus.Publish(new TestMessage($"msg-{i}"));

        var history = bus.GetHistory<TestMessage>();

        // Should only retain last 5
        Assert.Equal(5, history.Count);
        Assert.Equal("msg-5", history[0].Value.Text);
    }

    [Fact]
    public void GetHistory_IncludesTimestamps()
    {
        var bus = new EventBus();
        var before = DateTimeOffset.UtcNow;

        bus.Publish(new TestMessage("test"));

        var after = DateTimeOffset.UtcNow;
        var history = bus.GetHistory<TestMessage>();

        Assert.Single(history);
        Assert.InRange(history[0].Timestamp, before, after);
    }

    [Fact]
    public void Subscribe_InvokesHandlerOnPublish()
    {
        var bus = new EventBus();
        TestMessage? received = null;

        using var sub = bus.Subscribe<TestMessage>(msg => received = msg);

        bus.Publish(new TestMessage("hello"));

        Assert.NotNull(received);
        Assert.Equal("hello", received.Text);
    }

    [Fact]
    public void Subscribe_InvokesMultipleHandlers()
    {
        var bus = new EventBus();
        int count = 0;

        using var sub1 = bus.Subscribe<TestMessage>(_ => count++);
        using var sub2 = bus.Subscribe<TestMessage>(_ => count++);

        bus.Publish(new TestMessage("test"));

        Assert.Equal(2, count);
    }

    [Fact]
    public void Subscribe_DoesNotInvokeAfterDispose()
    {
        var bus = new EventBus();
        int count = 0;

        var sub = bus.Subscribe<TestMessage>(_ => count++);

        bus.Publish(new TestMessage("first"));
        Assert.Equal(1, count);

        sub.Dispose();

        bus.Publish(new TestMessage("second"));
        Assert.Equal(1, count); // Should not increment
    }

    [Fact]
    public void Subscribe_SwallowsHandlerExceptions()
    {
        var bus = new EventBus();
        int successCount = 0;

        using var sub1 = bus.Subscribe<TestMessage>(_ => throw new Exception("Handler error"));
        using var sub2 = bus.Subscribe<TestMessage>(_ => successCount++);

        // Should not throw
        bus.Publish(new TestMessage("test"));

        Assert.Equal(1, successCount); // Second handler should still run
    }

    [Fact]
    public void Subscribe_ThrowsIfHandlerIsNull()
    {
        var bus = new EventBus();

        Assert.Throws<ArgumentNullException>(() =>
            bus.Subscribe<TestMessage>(null!));
    }

    [Fact]
    public void CreateScope_ReturnsScopedBus()
    {
        var parent = new EventBus();
        var child = parent.CreateScope("child-1");

        Assert.NotNull(child);
        Assert.Equal("child-1", child.ScopeId);
        Assert.Same(parent, child.Parent);
    }

    [Fact]
    public void CreateScope_ThrowsIfScopeIdIsNull()
    {
        var bus = new EventBus();

        Assert.Throws<ArgumentNullException>(() => bus.CreateScope(null!));
        Assert.Throws<ArgumentNullException>(() => bus.CreateScope(""));
        Assert.Throws<ArgumentNullException>(() => bus.CreateScope("  "));
    }

    [Fact]
    public void ScopedBus_IsolatesWrites()
    {
        var parent = new EventBus();
        var child = parent.CreateScope("child");

        child.Publish(new TestMessage("from-child"));

        // Child can read its own
        Assert.True(child.TryGet<TestMessage>(out var childMsg));
        Assert.Equal("from-child", childMsg.Text);

        // Parent cannot read child's writes
        Assert.False(parent.TryGet<TestMessage>(out _));
    }

    [Fact]
    public void ScopedBus_CanReadParentFacts()
    {
        var parent = new EventBus();
        parent.Publish(new TestMessage("from-parent"));

        var child = parent.CreateScope("child");

        // Child cannot read parent via TryGet
        Assert.False(child.TryGet<TestMessage>(out _));

        // But can via TryGetWithFallback
        Assert.True(child.TryGetWithFallback<TestMessage>(out var msg));
        Assert.Equal("from-parent", msg.Text);
    }

    [Fact]
    public void ScopedBus_LocalValueShadowsParent()
    {
        var parent = new EventBus();
        parent.Publish(new TestMessage("from-parent"));

        var child = parent.CreateScope("child");
        child.Publish(new TestMessage("from-child"));

        // TryGetWithFallback should return local (child) value
        Assert.True(child.TryGetWithFallback<TestMessage>(out var msg));
        Assert.Equal("from-child", msg.Text);
    }

    [Fact]
    public void ScopedBus_MultipleLevels()
    {
        var root = new EventBus();
        root.Publish(new TestMessage("from-root"));

        var child = root.CreateScope("child");
        child.Publish(new OtherMessage(42));

        var grandchild = child.CreateScope("grandchild");
        grandchild.Publish(new TestMessage("from-grandchild"));

        // Grandchild can access its own
        Assert.True(grandchild.TryGet<TestMessage>(out var own));
        Assert.Equal("from-grandchild", own.Text);

        // Grandchild can access parent's OtherMessage
        Assert.True(grandchild.TryGetWithFallback<OtherMessage>(out var parentMsg));
        Assert.Equal(42, parentMsg.Value);

        // But if grandchild overwrites TestMessage, it shadows root
        var fallback = grandchild.GetOrDefault<TestMessage>();
        Assert.Equal("from-grandchild", fallback?.Text);
    }

    [Fact]
    public void Clear_RemovesLatestAndHistory()
    {
        var bus = new EventBus();
        bus.Publish(new TestMessage("first"));
        bus.Publish(new TestMessage("second"));

        Assert.True(bus.TryGet<TestMessage>(out _));
        Assert.Equal(2, bus.GetHistory<TestMessage>().Count);

        bus.Clear<TestMessage>();

        Assert.False(bus.TryGet<TestMessage>(out _));
        Assert.Empty(bus.GetHistory<TestMessage>());
    }

    [Fact]
    public void ClearAll_RemovesAllFacts()
    {
        var bus = new EventBus();
        bus.Publish(new TestMessage("test"));
        bus.Publish(new OtherMessage(42));

        bus.ClearAll();

        Assert.False(bus.TryGet<TestMessage>(out _));
        Assert.False(bus.TryGet<OtherMessage>(out _));
        Assert.Empty(bus.GetHistory<TestMessage>());
        Assert.Empty(bus.GetHistory<OtherMessage>());
    }

    [Fact]
    public void Dispose_ClearsSubscriptions()
    {
        var bus = new EventBus();
        int count = 0;

        var sub = bus.Subscribe<TestMessage>(_ => count++);

        bus.Publish(new TestMessage("before-dispose"));
        Assert.Equal(1, count);

        bus.Dispose();

        // Should throw after dispose
        Assert.Throws<ObjectDisposedException>(() =>
            bus.Publish(new TestMessage("after-dispose")));
    }

    [Fact]
    public void Dispose_ThrowsOnSubsequentOperations()
    {
        var bus = new EventBus();
        bus.Dispose();

        Assert.Throws<ObjectDisposedException>(() => bus.Publish(new TestMessage("test")));
        Assert.Throws<ObjectDisposedException>(() => bus.TryGet<TestMessage>(out _));
        Assert.Throws<ObjectDisposedException>(() => bus.GetOrDefault<TestMessage>());
        Assert.Throws<ObjectDisposedException>(() => bus.GetHistory<TestMessage>());
        Assert.Throws<ObjectDisposedException>(() => bus.Subscribe<TestMessage>(_ => { }));
        Assert.Throws<ObjectDisposedException>(() => bus.CreateScope("test"));
    }
}
