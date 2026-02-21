using UtilityAi.Consideration;
using UtilityAi.Orchestration.SelectionStrategies;
using UtilityAi.Utils;
using Xunit;

namespace Tests;

public class SelectionStrategyTests
{
    private static Proposal MakeProposal(string id) =>
        new(id, Array.Empty<IConsideration>(), _ => Task.CompletedTask);

    private static Runtime MakeRuntime() => new(new EventBus(), Tick: 0);

    #region RandomSelectionStrategy

    [Fact]
    public void RandomSelection_ThrowsOnEmpty()
    {
        var strategy = new RandomSelectionStrategy();
        var empty = Array.Empty<(Proposal, double)>();

        Assert.Throws<InvalidOperationException>(() => strategy.Select(empty, MakeRuntime()));
    }

    [Fact]
    public void RandomSelection_SingleProposal_ReturnsIt()
    {
        var strategy = new RandomSelectionStrategy();
        var p = MakeProposal("only");
        var scored = new List<(Proposal P, double Utility)> { (p, 0.8) };

        var result = strategy.Select(scored, MakeRuntime());

        Assert.Same(p, result);
    }

    [Fact]
    public void RandomSelection_DifferentUtilities_ReturnsTop()
    {
        var strategy = new RandomSelectionStrategy(seed: 42);
        var top = MakeProposal("top");
        var low = MakeProposal("low");
        var scored = new List<(Proposal P, double Utility)>
        {
            (low, 0.3),
            (top, 0.9),
        };

        for (var i = 0; i < 20; i++)
        {
            var result = strategy.Select(scored, MakeRuntime());
            Assert.Same(top, result);
        }
    }

    [Fact]
    public void RandomSelection_TiedProposals_SelectsDifferentOnes()
    {
        var strategy = new RandomSelectionStrategy(seed: 12345);
        var a = MakeProposal("a");
        var b = MakeProposal("b");
        var c = MakeProposal("c");
        var scored = new List<(Proposal P, double Utility)>
        {
            (a, 1.0),
            (b, 1.0),
            (c, 1.0),
        };

        var selected = new HashSet<string>();
        for (var i = 0; i < 100; i++)
        {
            var result = strategy.Select(scored, MakeRuntime());
            selected.Add(result.Id);
        }

        Assert.True(selected.Count > 1, "Random strategy should select different tied proposals over many calls");
    }

    [Fact]
    public void RandomSelection_NonTiedProposals_ExcludedFromSelection()
    {
        var strategy = new RandomSelectionStrategy(tieThreshold: 0.001, seed: 99);
        var top = MakeProposal("top");
        var nearTop = MakeProposal("near-top");
        var far = MakeProposal("far");
        var scored = new List<(Proposal P, double Utility)>
        {
            (top, 1.0),
            (nearTop, 0.99),
            (far, 0.5),
        };

        for (var i = 0; i < 50; i++)
        {
            var result = strategy.Select(scored, MakeRuntime());
            Assert.Same(top, result);
        }
    }

    [Fact]
    public void RandomSelection_TiedWithinThreshold_BothSelected()
    {
        var strategy = new RandomSelectionStrategy(tieThreshold: 0.05, seed: 7);
        var a = MakeProposal("a");
        var b = MakeProposal("b");
        var scored = new List<(Proposal P, double Utility)>
        {
            (a, 1.0),
            (b, 0.97),
        };

        var selected = new HashSet<string>();
        for (var i = 0; i < 100; i++)
        {
            var result = strategy.Select(scored, MakeRuntime());
            selected.Add(result.Id);
        }

        Assert.Contains("a", selected);
        Assert.Contains("b", selected);
    }

    #endregion

    #region RoundRobinSelectionStrategy

    [Fact]
    public void RoundRobin_ThrowsOnEmpty()
    {
        var strategy = new RoundRobinSelectionStrategy();
        var empty = Array.Empty<(Proposal, double)>();

        Assert.Throws<InvalidOperationException>(() => strategy.Select(empty, MakeRuntime()));
    }

    [Fact]
    public void RoundRobin_SingleProposal_ReturnsIt()
    {
        var strategy = new RoundRobinSelectionStrategy();
        var p = MakeProposal("only");
        var scored = new List<(Proposal P, double Utility)> { (p, 0.8) };

        var result = strategy.Select(scored, MakeRuntime());

        Assert.Same(p, result);
    }

    [Fact]
    public void RoundRobin_DifferentUtilities_ReturnsTop()
    {
        var strategy = new RoundRobinSelectionStrategy();
        var top = MakeProposal("top");
        var low = MakeProposal("low");
        var scored = new List<(Proposal P, double Utility)>
        {
            (low, 0.3),
            (top, 0.9),
        };

        for (var i = 0; i < 10; i++)
        {
            var result = strategy.Select(scored, MakeRuntime());
            Assert.Same(top, result);
        }
    }

    [Fact]
    public void RoundRobin_TiedProposals_CyclesThroughAll()
    {
        var strategy = new RoundRobinSelectionStrategy();
        var a = MakeProposal("a");
        var b = MakeProposal("b");
        var c = MakeProposal("c");
        var scored = new List<(Proposal P, double Utility)>
        {
            (a, 1.0),
            (b, 1.0),
            (c, 1.0),
        };

        // Collect one full cycle plus one to verify wrap-around
        var results = new List<string>();
        for (var i = 0; i < 7; i++)
            results.Add(strategy.Select(scored, MakeRuntime()).Id);

        // All three proposals should appear
        var distinct = results.Distinct().ToList();
        Assert.Equal(3, distinct.Count);

        // Verify cycling: the 4th selection should match the 1st (wrap-around)
        Assert.Equal(results[0], results[3]);
        Assert.Equal(results[1], results[4]);
        Assert.Equal(results[2], results[5]);
    }

    [Fact]
    public void RoundRobin_NonTiedProposals_ExcludedFromRotation()
    {
        var strategy = new RoundRobinSelectionStrategy(tieThreshold: 0.001);
        var top = MakeProposal("top");
        var notTied = MakeProposal("not-tied");
        var scored = new List<(Proposal P, double Utility)>
        {
            (top, 1.0),
            (notTied, 0.5),
        };

        for (var i = 0; i < 10; i++)
        {
            var result = strategy.Select(scored, MakeRuntime());
            Assert.Same(top, result);
        }
    }

    [Fact]
    public void RoundRobin_TiedWithinThreshold_BothRotate()
    {
        var strategy = new RoundRobinSelectionStrategy(tieThreshold: 0.05);
        var a = MakeProposal("a");
        var b = MakeProposal("b");
        var scored = new List<(Proposal P, double Utility)>
        {
            (a, 1.0),
            (b, 0.97),
        };

        var selected = new HashSet<string>();
        for (var i = 0; i < 4; i++)
            selected.Add(strategy.Select(scored, MakeRuntime()).Id);

        Assert.Contains("a", selected);
        Assert.Contains("b", selected);
    }

    [Fact]
    public void RoundRobin_StableKeyFromSortedIds()
    {
        // Two separate strategy instances selecting from the same tie group (same IDs)
        // should share the same round-robin key logic based on sorted IDs.
        var strategy = new RoundRobinSelectionStrategy();
        var a = MakeProposal("alpha");
        var b = MakeProposal("beta");

        var scored = new List<(Proposal P, double Utility)> { (a, 1.0), (b, 1.0) };

        // First call starts at index 0, second advances to index 1
        var result1 = strategy.Select(scored, MakeRuntime());
        var result2 = strategy.Select(scored, MakeRuntime());

        Assert.NotEqual(result1.Id, result2.Id);
        Assert.Contains(result1.Id, new[] { "alpha", "beta" });
        Assert.Contains(result2.Id, new[] { "alpha", "beta" });
    }

    #endregion
}
