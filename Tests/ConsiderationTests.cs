using UtilityAi.Consideration.General;
using UtilityAi.Utils;
using Xunit;

namespace Tests;

public class ConsiderationTests
{
    [Fact]
    public void HasFact_True_WhenFactExists_ElseZero()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, new UserIntent("t"), 0);
        var cons = new HasFact<int>(true);
        Assert.Equal(0.0, cons.Evaluate(rt));
        bus.Publish(5);
        Assert.Equal(1.0, cons.Evaluate(rt));
    }

    [Fact]
    public void HasFact_False_Inverts()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, new UserIntent("t"), 0);
        var cons = new HasFact<string>(false);
        Assert.Equal(1.0, cons.Evaluate(rt));
        bus.Publish("hi");
        Assert.Equal(0.0, cons.Evaluate(rt));
    }

    private sealed record Sig(double V);

    [Fact]
    public void CurveSignal_DefaultsWhenMissing_AndClamps()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, new UserIntent("t"), 0);
        var c = new CurveSignal<Sig>("c", s => s.V, v => v, defaultValue: 0.25);
        Assert.Equal(0.25, c.Evaluate(rt));
        bus.Publish(new Sig(2)); // project 2 -> clamp to 1
        Assert.Equal(1.0, c.Evaluate(rt));
        bus.Publish(new Sig(-1));
        Assert.Equal(0.0, c.Evaluate(rt));
    }
}
