using System.Linq;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
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
    public void Utility_MultipliesBaseAndConsiderations()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, new UserIntent("test"), 0);
        var p = new Proposal(
            id: "x",
            baseScore: 0.5,
            cons: new IConsideration[] { new ConstCons(0.8), new ConstCons(0.5) },
            act: _ => Task.CompletedTask
        );

        var u = p.Utility(rt);
        Assert.True(System.Math.Abs(u - (0.5 * 0.8 * 0.5)) < 1e-9);
    }

    [Fact]
    public void Utility_ClampsInputsToZeroOne()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus, new UserIntent("test"), 0);
        var p = new Proposal(
            id: "x",
            baseScore: 2.0, // will clamp to 1
            cons: new IConsideration[] { new ConstCons(-10), new ConstCons(2.5) },
            act: _ => Task.CompletedTask
        );
        var u = p.Utility(rt);
        // base becomes 1, cons clamp to 0 and 1 -> total 0
        Assert.Equal(0, u);
    }
}
