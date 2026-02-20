using UtilityAi.Consideration;
using UtilityAi.Consideration.Composite;
using UtilityAi.Consideration.General;
using UtilityAi.Utils;
using Xunit;

namespace Tests.Consideration;

public class CompositeConsiderationTests
{
    [Fact]
    public void AndConsideration_AllHigh_ReturnsHigh()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus,  0);

        var consideration = new AndConsideration(
            new ConstantValue(0.8),
            new ConstantValue(0.9),
            new ConstantValue(1.0)
        );

        var result = consideration.Evaluate(rt);
        Assert.True(result > 0.7); // 0.8 * 0.9 * 1.0 = 0.72
    }

    [Fact]
    public void AndConsideration_OneLow_ReturnsLow()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);

        var consideration = new AndConsideration(
            new ConstantValue(1.0),
            new ConstantValue(0.1),
            new ConstantValue(1.0)
        );

        var result = consideration.Evaluate(rt);
        Assert.True(result < 0.2); // 1.0 * 0.1 * 1.0 = 0.1
    }

    [Fact]
    public void OrConsideration_AnyHigh_ReturnsHigh()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);

        var consideration = new OrConsideration(
            new ConstantValue(0.1),
            new ConstantValue(0.9),
            new ConstantValue(0.2)
        );

        var result = consideration.Evaluate(rt);
        Assert.Equal(0.9, result);
    }

    [Fact]
    public void NotConsideration_InvertsValue()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);

        var consideration = new NotConsideration(new ConstantValue(0.3));
        var result = consideration.Evaluate(rt);

        Assert.Equal(0.7, result, precision: 3);
    }
}
