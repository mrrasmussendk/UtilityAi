using UtilityAi.Utils;
using Xunit;

namespace Tests;

public class EventBusTests
{
    [Fact]
    public void PublishAndGetOrDefault_WorksForLatestPerType()
    {
        var bus = new EventBus();
        Assert.Null(bus.GetOrDefault<string>());

        bus.Publish("first");
        Assert.Equal("first", bus.GetOrDefault<string>());

        bus.Publish("second");
        Assert.Equal("second", bus.GetOrDefault<string>());

        bus.Publish(123);
        Assert.Equal(123, bus.GetOrDefault<int>());
        // string remains the latest published string
        Assert.Equal("second", bus.GetOrDefault<string>());
    }
}
