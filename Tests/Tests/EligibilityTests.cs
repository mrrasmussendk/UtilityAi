using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Orchestration;
using UtilityAi.Utils;
using Xunit;

namespace Tests;

public class EligibilityTests
{
    private sealed class NoopModule(params Proposal[] proposals) : ICapabilityModule
    {
        public IEnumerable<Proposal> Propose(Runtime rt) => proposals;

    }

    [Fact]
    public async Task Orchestrator_Filters_Ineligible_Proposals()
    {
        var bus = new EventBus();
        var orch = new UtilityAiOrchestrator(null, true, bus);
        var sink = new RecordingSink();

        // Ensure an int fact is present so HasFactEligible<int> passes and NotHasFactEligible<int> fails
        bus.Publish(42);

        var ineligible = new Proposal(
            id: "A",
            cons: Enumerable.Empty<IConsideration>(),
            act: ct => Task.CompletedTask,
            eligibilities: new IEligibility[] { new NotHasFactEligible<int>() }
        );
        var eligible = new Proposal(
            id: "B",
            cons: Enumerable.Empty<IConsideration>(),
            act: ct => Task.CompletedTask,
            eligibilities: new IEligibility[] { new HasFactEligible<int>() }
        );

        orch.AddModule(new NoopModule(ineligible, eligible));
        await orch.RunAsync( 1, CancellationToken.None, sink);

        Assert.Single(sink.Ticks);
        var tick = sink.Ticks[0];
        Assert.Single(tick.Scored);
        Assert.Equal("B", tick.Scored[0].Proposal.Id);
        Assert.Equal("B", tick.Chosen.Id);
    }

    private sealed class ReasonCaptureSink : IOrchestrationSink
    {
        public OrchestrationStopReason? Reason { get; private set; }
        public void OnTickStart(Runtime rt) { }
        public void OnScored(Runtime rt, IReadOnlyList<(Proposal Proposal, double Utility)> scored) { }
        public void OnChosen(Runtime rt, Proposal chosen, double utility) { }
        public void OnActed(Runtime rt, Proposal chosen) { }
        public void OnStopped(Runtime rt, OrchestrationStopReason reason) { Reason = reason; }
    }

    [Fact]
    public async Task Orchestrator_Stops_When_No_Eligible_Proposals()
    {
        var bus = new EventBus();
        var orch = new UtilityAiOrchestrator(null, true, bus);
        var sink = new ReasonCaptureSink();

        // Publish int to invalidate NotHasFactEligible<int>
        bus.Publish(7);

        var p1 = new Proposal("P1", Enumerable.Empty<IConsideration>(), ct => Task.CompletedTask,
            eligibilities: new IEligibility[] { new NotHasFactEligible<int>() });
        var p2 = new Proposal("P2", Enumerable.Empty<IConsideration>(), ct => Task.CompletedTask,
            eligibilities: new IEligibility[] { new NotHasFactEligible<int>() });

        orch.AddModule(new NoopModule(p1, p2));
        await orch.RunAsync( 1, CancellationToken.None, sink);

        Assert.Equal(OrchestrationStopReason.NoEligibleProposals, sink.Reason);
    }

    [Fact]
    public void Proposal_Without_Eligibilities_Is_Eligible_BackCompat()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus,  0);
        var p = new Proposal("X", Enumerable.Empty<IConsideration>(), ct => Task.CompletedTask);
        Assert.True(p.IsEligible(rt));
    }

    [Fact]
    public void NoRepeatEligible_Handles_Null_Stack()
    {
        // Test for Bug #4: NoRepeatEligible should handle null stack gracefully
        var bus = new EventBus();
        var rt = new Runtime(bus,  0);
        var eligibility = new NoRepeatEligible("test-id");
        
        // Should return true when stack doesn't exist (null)
        Assert.True(eligibility.IsEligible(rt));
    }

    [Fact]
    public void NoRepeatEligible_Returns_False_When_Id_In_History()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus,  0);
        var eligibility = new NoRepeatEligible("test-id");
        
        // Publish execution history as IReadOnlyList<string> (matching orchestrator behavior)
        bus.Publish<IReadOnlyList<string>>(new List<string> { "test-id" });
        
        // Should return false when ID is in execution history
        Assert.False(eligibility.IsEligible(rt));
    }

    [Fact]
    public void NoRepeatEligible_Returns_True_When_Id_Not_In_History()
    {
        var bus = new EventBus();
        var rt = new Runtime(bus,  0);
        var eligibility = new NoRepeatEligible("test-id");
        
        // Publish execution history as IReadOnlyList<string> (matching orchestrator behavior)
        bus.Publish<IReadOnlyList<string>>(new List<string> { "other-id" });
        
        // Should return true when ID is not in execution history
        Assert.True(eligibility.IsEligible(rt));
    }

    [Fact]
    public void HasFactEligible_Name_IncludesTypeName()
    {
        var eligibility = new HasFactEligible<int>();
        Assert.Equal("HasFactEligible<Int32>", eligibility.Name);
    }

    [Fact]
    public void NotHasFactEligible_Name_IncludesTypeName()
    {
        var eligibility = new NotHasFactEligible<string>();
        Assert.Equal("NotHasFactEligible<String>", eligibility.Name);
    }

    [Fact]
    public void NoRepeatEligible_Name_IsConstant()
    {
        var eligibility = new NoRepeatEligible("test-id");
        Assert.Equal("NoRepeatEligible", eligibility.Name);
    }

    [Fact]
    public async Task NoRepeatEligible_BlocksRepeat_ViaOrchestrator()
    {
        var bus = new EventBus();
        var orch = new UtilityAiOrchestrator(null, false, bus);
        var sink = new RecordingSink();
        var executedIds = new List<string>();

        var proposalA = new Proposal(
            id: "A",
            cons: new IConsideration[] { new ConstantValue(0.9) },
            act: ct => { executedIds.Add("A"); return Task.CompletedTask; },
            eligibilities: new IEligibility[] { new NoRepeatEligible("A") }
        );
        var proposalB = new Proposal(
            id: "B",
            cons: new IConsideration[] { new ConstantValue(0.5) },
            act: ct => { executedIds.Add("B"); return Task.CompletedTask; }
        );

        orch.AddModule(new NoopModule(proposalA, proposalB));

        // Run 2 ticks: First tick should choose A (higher utility), second tick A is ineligible so B
        await orch.RunAsync(2, CancellationToken.None, sink);

        Assert.Equal(2, executedIds.Count);
        Assert.Equal("A", executedIds[0]);
        Assert.Equal("B", executedIds[1]);
    }
}
