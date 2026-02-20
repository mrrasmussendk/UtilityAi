using UtilityAi.Consideration.General;
using UtilityAi.Utils;
using Xunit;

namespace Tests.Consideration;

public class ThresholdValueTests
{
    private record TestFact(double Value);

    [Fact]
    public void ThresholdValue_AboveThreshold_ReturnsOne()
    {
        var bus = new EventBus();
        bus.Publish(new TestFact(75.0));
        var rt = new Runtime(bus, 0);

        var consideration = new ThresholdValue<TestFact>(f => f.Value, threshold: 50.0, above: true);
        var result = consideration.Evaluate(rt);

        Assert.Equal(1.0, result);
    }

    [Fact]
    public void ThresholdValue_BelowThreshold_ReturnsZero()
    {
        var bus = new EventBus();
        bus.Publish(new TestFact(25.0));
        var rt = new Runtime(bus, 0);

        var consideration = new ThresholdValue<TestFact>(f => f.Value, threshold: 50.0, above: true);
        var result = consideration.Evaluate(rt);

        Assert.Equal(0.0, result);
    }

    [Fact]
    public void ThresholdValue_InvertedBelow_ReturnsOne()
    {
        var bus = new EventBus();
        bus.Publish(new TestFact(25.0));
        var rt = new Runtime(bus, 0);

        var consideration = new ThresholdValue<TestFact>(f => f.Value, threshold: 50.0, above: false);
        var result = consideration.Evaluate(rt);

        Assert.Equal(1.0, result);
    }

    [Fact]
    public void ThresholdValue_NoFact_ReturnsZero()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);

        var consideration = new ThresholdValue<TestFact>(f => f.Value, threshold: 50.0);
        var result = consideration.Evaluate(rt);

        Assert.Equal(0.0, result);
    }
}
