using UtilityAi.Consideration;
using UtilityAi.Dashboard;
using UtilityAi.Orchestration;
using UtilityAi.Utils;
using Xunit;

namespace UtilityAi.Dashboard.Tests;

public class DashboardSinkTests
{
    private static Runtime MakeRuntime(int tick = 0)
    {
        var bus = new EventBus();
        var intent = new UserIntent(new IntentGoal("test"), new Dictionary<string, object?>());
        return new Runtime(bus, intent, tick);
    }

    private static Proposal MakeProposal(string id)
    {
        return new Proposal(id, Array.Empty<IConsideration>(), _ => Task.CompletedTask);
    }

    [Fact]
    public void Constructor_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => new DashboardSink(null!));
    }

    [Fact]
    public void State_ReturnsInjectedState()
    {
        var state = new DashboardState();
        var sink = new DashboardSink(state);
        Assert.Same(state, sink.State);
    }

    [Fact]
    public void FullLifecycle_RecordsAllEvents()
    {
        var state = new DashboardState();
        var sink = new DashboardSink(state);
        var rt = MakeRuntime(0);

        // TickStart (no-op but should not throw)
        sink.OnTickStart(rt);

        // Scored
        var p1 = MakeProposal("action.a");
        var p2 = MakeProposal("action.b");
        var scored = new List<(Proposal, double)> { (p1, 0.9), (p2, 0.3) };
        sink.OnScored(rt, scored);

        Assert.NotNull(state.CurrentTick);
        Assert.Equal(2, state.CurrentTick!.Proposals.Count);

        // Chosen
        sink.OnChosen(rt, p1, 0.9);
        Assert.Equal("action.a", state.ActiveProposalId);

        // Acted
        sink.OnActed(rt, p1);
        Assert.Single(state.Ticks);

        // Stopped
        sink.OnStopped(rt, OrchestrationStopReason.NoProposals);
        Assert.Equal(OrchestrationStopReason.NoProposals, state.StopReason);
    }

    [Fact]
    public async Task IntegrationWithOrchestrator_CapturesDecisions()
    {
        var state = new DashboardState();
        var sink = new DashboardSink(state);
        var bus = new EventBus();
        bus.Publish("test-fact");

        var orchestrator = new UtilityAiOrchestrator(bus: bus);
        orchestrator.AddModule(new TestModule());

        var intent = new UserIntent(new IntentGoal("test"), new Dictionary<string, object?>());
        await orchestrator.RunAsync(intent, maxTicks: 2, CancellationToken.None, sink: sink);

        // Should have captured ticks
        Assert.True(state.Ticks.Count > 0);
        Assert.NotNull(state.StopReason);
    }

    private sealed class TestModule : Capabilities.ICapabilityModule
    {
        private int _callCount;
        public IEnumerable<Proposal> Propose(Runtime rt)
        {
            _callCount++;
            if (_callCount > 1) yield break;

            yield return new Proposal("test.action",
                Array.Empty<IConsideration>(),
                _ => Task.CompletedTask);
        }
    }
}
