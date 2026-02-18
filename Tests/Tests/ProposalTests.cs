using UtilityAi.Consideration;
using UtilityAi.Utils;
using Xunit;

namespace Tests;

public class ProposalTests
{
    private sealed class ConstCons(double value) : IConsideration
    {
        public string Name => "const";
        public double Evaluate(Runtime rt) => value;
    }

    [Fact]
    public void Utility_PriorTimesGeometricMeanOfConsiderations()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, new UserIntent("test"), 0);
        var p = new Proposal(
            id: "x",
            cons: new IConsideration[] { new ConstCons(0.8), new ConstCons(0.5) },
            act: _ => Task.CompletedTask
        );

        var u = p.Utility(rt);

        // expected = prior (0.5) * sqrt(0.8 * 0.5)
        var expected =  System.Math.Sqrt(0.8 * 0.5);
        Assert.InRange(u, expected - 1e-12, expected + 1e-12);
    }

    [Fact]
    public void Utility_ClampsAndUsesEpsilonFloor()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, new UserIntent("test"), 0);
        var p = new Proposal(
            id: "x",
            cons: new IConsideration[] { new ConstCons(-10), new ConstCons(2.5) }, // clamp to [0,1]
            act: _ => Task.CompletedTask
        );

        var u = p.Utility(rt);
        // cons become eps and 1; geom = sqrt(eps) = 1e-3; prior=1 -> 1e-3
        var expected = 1e-3;
        Assert.InRange(u, expected - 1e-9, expected + 1e-9);
    }

    [Fact]
    public void Utility_WithNoConsiderations_EqualsPrior()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, new UserIntent("test"), 0);
        var p = new Proposal(
            id: "x",
            cons: System.Array.Empty<IConsideration>(),
            act: _ => Task.CompletedTask
        );

        var u = p.Utility(rt);
        Assert.Equal(1, u);
    }

    [Fact]
    public void Utility_AllZeroConsiderations_ResultIsEpsilon()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, new UserIntent("test"), 0);
        var p = new Proposal(
            id: "x",
            cons: new IConsideration[] { new ConstCons(-1), new ConstCons(0), new ConstCons(0) },
            act: _ => Task.CompletedTask
        );

        var u = p.Utility(rt);
        // product becomes eps^3; geom = eps; prior=1 -> eps
        var expected = 1e-6;
        Assert.InRange(u, expected - 1e-12, expected + 1e-12);
    }
}
