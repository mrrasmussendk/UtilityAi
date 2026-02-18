using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Dashboard;
using UtilityAi.Orchestration;
using UtilityAi.Utils;
using Xunit;

namespace UtilityAi.Dashboard.Tests;

public class DashboardStateTests
{
    private static Runtime MakeRuntime(int tick = 0)
    {
        var bus = new EventBus();
        var intent = new UserIntent(new IntentGoal("test"), new Dictionary<string, object?>());
        return new Runtime(bus, intent, tick);
    }

    private static Proposal MakeProposal(string id, double prior = 1.0, double temperature = 1.0,
        IEnumerable<IConsideration>? considerations = null, IEnumerable<IEligibility>? eligibilities = null)
    {
        return new Proposal(id, considerations ?? Array.Empty<IConsideration>(),
            _ => Task.CompletedTask, eligibilities)
        {
            Prior = prior,
            Temperature = temperature
        };
    }

    [Fact]
    public void InitialState_IsEmpty()
    {
        var state = new DashboardState();

        Assert.Null(state.CurrentTick);
        Assert.Null(state.ActiveProposalId);
        Assert.Null(state.StopReason);
        Assert.Empty(state.Ticks);
        Assert.Empty(state.PriorOverrides);
        Assert.Empty(state.TemperatureOverrides);
    }

    [Fact]
    public void RecordScored_CapturesProposals()
    {
        var state = new DashboardState();
        var rt = MakeRuntime(0);
        var p1 = MakeProposal("action.a", prior: 0.8);
        var p2 = MakeProposal("action.b", prior: 0.5);

        var scored = new List<(Proposal, double)> { (p1, 0.8), (p2, 0.5) };
        state.RecordScored(0, scored, rt);

        Assert.NotNull(state.CurrentTick);
        Assert.Equal(0, state.CurrentTick!.Tick);
        Assert.Equal(2, state.CurrentTick.Proposals.Count);
        Assert.Equal("action.a", state.CurrentTick.Proposals[0].Id);
        Assert.Equal(0.8, state.CurrentTick.Proposals[0].Utility);
        Assert.Equal("action.b", state.CurrentTick.Proposals[1].Id);
    }

    [Fact]
    public void RecordChosen_SetsActiveProposal()
    {
        var state = new DashboardState();
        var rt = MakeRuntime(0);
        var p1 = MakeProposal("action.a");
        var scored = new List<(Proposal, double)> { (p1, 0.9) };

        state.RecordScored(0, scored, rt);
        state.RecordChosen(0, p1, 0.9);

        Assert.Equal("action.a", state.ActiveProposalId);
        Assert.Equal("action.a", state.CurrentTick!.ChosenProposalId);
        Assert.Equal(0.9, state.CurrentTick.ChosenUtility);
        Assert.True(state.CurrentTick.Proposals[0].IsChosen);
    }

    [Fact]
    public void RecordActed_AddsTickToHistory()
    {
        var state = new DashboardState();
        var rt = MakeRuntime(0);
        var p1 = MakeProposal("action.a");
        var scored = new List<(Proposal, double)> { (p1, 0.9) };

        state.RecordScored(0, scored, rt);
        state.RecordChosen(0, p1, 0.9);
        state.RecordActed(p1);

        Assert.Single(state.Ticks);
        Assert.Equal(0, state.Ticks[0].Tick);
    }

    [Fact]
    public void RecordStopped_SetsStopReason()
    {
        var state = new DashboardState();
        state.RecordStopped(OrchestrationStopReason.MaxTicksReached);

        Assert.Equal(OrchestrationStopReason.MaxTicksReached, state.StopReason);
        Assert.Null(state.ActiveProposalId);
    }

    [Fact]
    public void PriorOverride_IsClampedAndStored()
    {
        var state = new DashboardState();

        state.SetPriorOverride("action.a", 0.75);
        Assert.Equal(0.75, state.PriorOverrides["action.a"]);

        state.SetPriorOverride("action.b", 1.5); // Should be clamped to 1.0
        Assert.Equal(1.0, state.PriorOverrides["action.b"]);

        state.SetPriorOverride("action.c", -0.5); // Should be clamped to 0.0
        Assert.Equal(0.0, state.PriorOverrides["action.c"]);
    }

    [Fact]
    public void TemperatureOverride_IsStoredAndClamped()
    {
        var state = new DashboardState();

        state.SetTemperatureOverride("action.a", 2.5);
        Assert.Equal(2.5, state.TemperatureOverrides["action.a"]);

        state.SetTemperatureOverride("action.b", -1.0); // Should be clamped to 0.0
        Assert.Equal(0.0, state.TemperatureOverrides["action.b"]);
    }

    [Fact]
    public void RemoveOverrides_ClearsValues()
    {
        var state = new DashboardState();
        state.SetPriorOverride("action.a", 0.5);
        state.SetTemperatureOverride("action.a", 1.5);

        state.RemovePriorOverride("action.a");
        state.RemoveTemperatureOverride("action.a");

        Assert.Empty(state.PriorOverrides);
        Assert.Empty(state.TemperatureOverrides);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var state = new DashboardState();
        var rt = MakeRuntime(0);
        var p1 = MakeProposal("action.a");
        var scored = new List<(Proposal, double)> { (p1, 0.9) };

        state.RecordScored(0, scored, rt);
        state.RecordChosen(0, p1, 0.9);
        state.RecordActed(p1);

        state.Reset();

        Assert.Null(state.CurrentTick);
        Assert.Null(state.ActiveProposalId);
        Assert.Null(state.StopReason);
        Assert.Empty(state.Ticks);
    }

    [Fact]
    public void Version_IncrementsOnChanges()
    {
        var state = new DashboardState();
        var v0 = state.Version;

        var rt = MakeRuntime(0);
        var p1 = MakeProposal("action.a");
        var scored = new List<(Proposal, double)> { (p1, 0.9) };

        state.RecordScored(0, scored, rt);
        Assert.True(state.Version > v0);
        var v1 = state.Version;

        state.RecordChosen(0, p1, 0.9);
        Assert.True(state.Version > v1);
        var v2 = state.Version;

        state.SetPriorOverride("action.a", 0.5);
        Assert.True(state.Version > v2);
    }

    [Fact]
    public void ConsiderationSnapshots_CaptureScores()
    {
        var state = new DashboardState();
        var bus = new EventBus();
        bus.Publish("hello"); // Publish a string fact
        var intent = new UserIntent(new IntentGoal("test"), new Dictionary<string, object?>());
        var rt = new Runtime(bus, intent, 0);

        var consideration = new HasFact<string>();
        var p1 = new Proposal("action.a", new[] { consideration }, _ => Task.CompletedTask);
        var scored = new List<(Proposal, double)> { (p1, 1.0) };

        state.RecordScored(0, scored, rt);

        Assert.Single(state.CurrentTick!.Proposals[0].Considerations);
        Assert.Equal("HasFact<String>", state.CurrentTick.Proposals[0].Considerations[0].Name);
        Assert.Equal(1.0, state.CurrentTick.Proposals[0].Considerations[0].Score);
    }

    [Fact]
    public void MultipleTicks_BuildsHistory()
    {
        var state = new DashboardState();

        for (int i = 0; i < 3; i++)
        {
            var rt = MakeRuntime(i);
            var p = MakeProposal($"action.tick{i}");
            var scored = new List<(Proposal, double)> { (p, 0.5 + i * 0.1) };
            state.RecordScored(i, scored, rt);
            state.RecordChosen(i, p, 0.5 + i * 0.1);
            state.RecordActed(p);
        }

        Assert.Equal(3, state.Ticks.Count);
        Assert.Equal("action.tick0", state.Ticks[0].ChosenProposalId);
        Assert.Equal("action.tick2", state.Ticks[2].ChosenProposalId);
    }
}
