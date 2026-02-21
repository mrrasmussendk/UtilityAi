using UtilityAi.Consideration.General;
using UtilityAi.Consideration;
using UtilityAi.Utils;
using Xunit;

namespace Tests;

public class ConsiderationTests
{
    [Fact]
    public void HasFact_ReturnsOne_WhenFactExists_ElseZero()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        var cons = new HasFact<int>();
        Assert.Equal(0.0, cons.Evaluate(rt));
        bus.Publish(5);
        Assert.Equal(1.0, cons.Evaluate(rt));
    }

    [Fact]
    public void NotHasFact_Inverts_HasFact()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        var cons = new NotHasFact<string>();
        Assert.Equal(1.0, cons.Evaluate(rt));
        bus.Publish("hi");
        Assert.Equal(0.0, cons.Evaluate(rt));
    }

    [Fact]
    public void HasFact_Name_IncludesTypeName()
    {
        var cons = new HasFact<int>();
        Assert.Equal("HasFact<Int32>", cons.Name);
    }

    [Fact]
    public void FactExists_And_FactMissing_MatchHasFactBehavior()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);

        var exists = new FactExists<int>();
        var missing = new FactMissing<int>();

        Assert.Equal(0.0, exists.Evaluate(rt));
        Assert.Equal(1.0, missing.Evaluate(rt));

        bus.Publish(42);

        Assert.Equal(1.0, exists.Evaluate(rt));
        Assert.Equal(0.0, missing.Evaluate(rt));
    }

    [Fact]
    public void FactExists_And_FactMissing_DefaultNames_AreSet()
    {
        Assert.Equal("FactExists<Int32>", new FactExists<int>().Name);
        Assert.Equal("FactMissing<Int32>", new FactMissing<int>().Name);
    }

    private sealed record Sig(double V);

    [Fact]
    public void CurveSignal_DefaultsWhenMissing_AndClamps()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        var c = new CurveSignal<Sig>("c", s => s.V, v => v, defaultValue: 0.25);
        Assert.Equal(0.25, c.Evaluate(rt));
        bus.Publish(new Sig(2)); // project 2 -> clamp to 1
        Assert.Equal(1.0, c.Evaluate(rt));
        bus.Publish(new Sig(-1));
        Assert.Equal(0.0, c.Evaluate(rt));
    }

    [Fact]
    public void CurveSignal_Name_IsSetCorrectly()
    {
        var c = new CurveSignal<Sig>("test-signal", s => s.V, v => v);
        Assert.Equal("test-signal", c.Name);
    }

    [Fact]
    public void NoRepeatConsideration_ReturnsOne_WhenHistoryFactMissing()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, 0);
        var consideration = new NoRepeatConsideration("action-1");

        Assert.Equal(1.0, consideration.Evaluate(rt));
    }

    [Fact]
    public void CollectionSize_ThrowsWhenDomainMaxEqualsMin()
    {
        var curve = new UtilityAi.Evaluators.PowerCurve(new UtilityAi.Evaluators.Range(0, 1));
        Assert.Throws<ArgumentException>(() =>
            new CollectionSize<List<int>>(l => l.Count, curve, (5, 5)));
    }

    [Fact]
    public void CollectionSize_ThrowsWhenDomainMaxLessThanMin()
    {
        var curve = new UtilityAi.Evaluators.PowerCurve(new UtilityAi.Evaluators.Range(0, 1));
        Assert.Throws<ArgumentException>(() =>
            new CollectionSize<List<int>>(l => l.Count, curve, (10, 5)));
    }

    [Fact]
    public void TimeSinceEvent_ThrowsWhenDomainMaxEqualsMin()
    {
        var curve = new UtilityAi.Evaluators.PowerCurve(new UtilityAi.Evaluators.Range(0, 1));
        Assert.Throws<ArgumentException>(() =>
            new TimeSinceEvent<string>(curve, (5.0, 5.0)));
    }

    [Fact]
    public void TimeSinceEvent_ThrowsWhenDomainMaxLessThanMin()
    {
        var curve = new UtilityAi.Evaluators.PowerCurve(new UtilityAi.Evaluators.Range(0, 1));
        Assert.Throws<ArgumentException>(() =>
            new TimeSinceEvent<string>(curve, (10.0, 5.0)));
    }
}
