using UtilityAi.Consideration.General;
using UtilityAi.Utils;
using Xunit;

namespace Tests.Consideration;

public class RangeValueTests
{
    private record TestFact(double Value);

    [Fact]
    public void RangeValue_WithinRange_ReturnsOne()
    {
        var bus = new EventBus();
        bus.Publish(new TestFact(50.0));
        var rt = new Runtime(bus, new UserIntent(new IntentGoal("test")), 0);

        var consideration = new RangeValue<TestFact>(f => f.Value, min: 10.0, max: 90.0);
        var result = consideration.Evaluate(rt);

        Assert.Equal(1.0, result);
    }

    [Fact]
    public void RangeValue_OutsideRange_ReturnsZero()
    {
        var bus = new EventBus();
        bus.Publish(new TestFact(95.0));
        var rt = new Runtime(bus, new UserIntent(new IntentGoal("test")), 0);

        var consideration = new RangeValue<TestFact>(f => f.Value, min: 10.0, max: 90.0);
        var result = consideration.Evaluate(rt);

        Assert.Equal(0.0, result);
    }

    [Fact]
    public void RangeValue_OnBoundaryInclusive_ReturnsOne()
    {
        var bus = new EventBus();
        bus.Publish(new TestFact(10.0));
        var rt = new Runtime(bus, new UserIntent(new IntentGoal("test")), 0);

        var consideration = new RangeValue<TestFact>(f => f.Value, min: 10.0, max: 90.0, inclusive: true);
        var result = consideration.Evaluate(rt);

        Assert.Equal(1.0, result);
    }

    [Fact]
    public void RangeValue_OnBoundaryExclusive_ReturnsZero()
    {
        var bus = new EventBus();
        bus.Publish(new TestFact(10.0));
        var rt = new Runtime(bus, new UserIntent(new IntentGoal("test")), 0);

        var consideration = new RangeValue<TestFact>(f => f.Value, min: 10.0, max: 90.0, inclusive: false);
        var result = consideration.Evaluate(rt);

        Assert.Equal(0.0, result);
    }
}
