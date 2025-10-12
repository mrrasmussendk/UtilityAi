using System;
using UtilityAi.Utils;
using Xunit;

namespace Tests;

public class EventBusTests
{
    [Fact]
    public void Publish_Then_GetOrDefault_ReturnsValue()
    {
        var bus = new EventBus();
        bus.Publish(42);
        Assert.Equal(42, bus.GetOrDefault<int>());
    }

    [Fact]
    public void TryGet_WhenNotPublished_ReturnsFalse()
    {
        var bus = new EventBus();
        var ok = bus.TryGet<string>(out var v);
        Assert.False(ok);
        Assert.Null(v);
    }

    [Fact]
    public void Publish_OverwritesLatestOfSameType()
    {
        var bus = new EventBus();
        bus.Publish(1);
        bus.Publish(2);
        Assert.Equal(2, bus.GetOrDefault<int>());
    }
}
