using Example.Action.Considerations;
using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace Tests.Utility;

public class ScoringAndConsiderationTests
{
    private sealed class ConstConsideration : ConsiderationBase
    {
        private readonly double _raw;
        private readonly Func<double, double>? _shape;
        public ConstConsideration(double raw, Func<double,double>? shape = null)
        { _raw = raw; _shape = shape; }
        protected override double ComputeRaw(IBlackboard bb) => _raw;
        protected override double Shape(double v) => _shape is null ? v : _shape(v);
    }

    [Fact]
    public void AggregateWithMakeup_Multiplies_Then_Applies_Makeup()
    {
        var cons = new List<IConsideration>
        {
            new ConstConsideration(0.60),
            new ConstConsideration(0.80)
        };
        var bb = new Blackboard();
        var result = Scoring.AggregateWithMakeup(cons, bb);
        var expectedProduct = 0.60 * 0.80; // 0.48
        var expected = 1.0 - Math.Pow(1.0 - expectedProduct, 1.0 / cons.Count);
        Assert.Equal(expected, result, 5);
    }

    [Fact]
    public void AggregateWithMakeup_Returns_One_When_No_Considerations()
    {
        var cons = new List<IConsideration>();
        var bb = new Blackboard();
        var result = Scoring.AggregateWithMakeup(cons, bb);
        Assert.Equal(1.0, result, 10);
    }

    [Fact]
    public void AggregateWithMakeup_Returns_Zero_When_Any_Zero()
    {
        var cons = new List<IConsideration>
        {
            new ConstConsideration(0.9),
            new ConstConsideration(0.0),
            new ConstConsideration(0.7)
        };
        var bb = new Blackboard();
        var result = Scoring.AggregateWithMakeup(cons, bb);
        Assert.Equal(0.0, result, 10);
    }

    [Fact]
    public void HasValueConsideration_Basics()
    {
        var bb = new Blackboard();
        var hasTopic = new HasValueConsideration("context:topic");
        var notHasAnswer = new HasValueConsideration("answer:text", true);

        // initially empty
        Assert.Equal(0.0, hasTopic.Consider(bb));
        Assert.Equal(1.0, notHasAnswer.Consider(bb));

        bb.Set("context:topic", "ai");
        bb.Set("answer:text", "Done");
        Assert.Equal(1.0, hasTopic.Consider(bb));
        Assert.Equal(0.0, notHasAnswer.Consider(bb));
    }

    [Fact]
    public void ConsiderationBase_Invert_And_Weight_Applied()
    {
        var bb = new Blackboard();
        // raw=0.4 -> shaped(identity)=0.4 -> invert => 0.6 -> weight 0.5 => 0.3
        var c = new ConstConsideration(0.4) { Invert = true, Weight = 0.5 };
        var value = c.Consider(bb);
        Assert.Equal(0.3, value, 6);
    }

    [Fact]
    public void ConsiderationBase_Shape_Used()
    {
        var bb = new Blackboard();
        // smootherstep(0.25)=~0.103515625 for quintic?  we implemented Smootherstep; let's use Smoothstep where at 0.5 it's exactly 0.5
        var c = new ConstConsideration(0.5, Curves.Smoothstep);
        var value = c.Consider(bb);
        Assert.Equal(0.5, value, 10);
    }
}
