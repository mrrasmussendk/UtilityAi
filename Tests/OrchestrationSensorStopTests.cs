using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Orchestration;
using UtilityAi.Orchestration.Events;
using UtilityAi.Sensor;
using UtilityAi.Utils;
using Xunit;

namespace Tests;

public class OrchestrationSensorStopTests
{
    private sealed class StopPublishingSensor : ISensor
    {
        private readonly OrchestrationStopReason _reason;
        public StopPublishingSensor(OrchestrationStopReason reason) { _reason = reason; }
        public Task SenseAsync(Runtime rt, CancellationToken ct)
        {
            rt.Bus.Publish(new StopOrchestrationEvent(_reason, "test-request"));
            return Task.CompletedTask;
        }
    }

    private sealed class CountingModule : ICapabilityModule
    {
        public int ProposeCalls { get; private set; }
        public int ActCalls { get; private set; }

        public IEnumerable<Proposal> Propose(Runtime rt)
        {
            ProposeCalls++;
            yield return new Proposal(
                id: "counting",
                cons: Enumerable.Empty<IConsideration>(),
                act: async ct => { ActCalls++; await Task.CompletedTask; }
            );
        }
    }

    private sealed class CapturingSink : IOrchestrationSink
    {
        private readonly IOrchestrationSink _inner;
        public OrchestrationStopReason? Reason { get; private set; }
        public CapturingSink(IOrchestrationSink? inner = null) { _inner = inner ?? NullSink.Instance; }
        public void OnTickStart(Runtime rt) => _inner.OnTickStart(rt);
        public void OnScored(Runtime rt, IReadOnlyList<(Proposal Proposal, double Utility)> scored) => _inner.OnScored(rt, scored);
        public void OnChosen(Runtime rt, Proposal chosen, double utility) => _inner.OnChosen(rt, chosen, utility);
        public void OnActed(Runtime rt, Proposal chosen) => _inner.OnActed(rt, chosen);
        public void OnStopped(Runtime rt, OrchestrationStopReason reason) { Reason = reason; _inner.OnStopped(rt, reason); }
    }

    [Fact]
    public async Task SensorStop_PublishesEvent_StopsOrchestrator_WithReason_AndNoChoice()
    {
        var bus = new EventBus();
        var orch = new UtilityAiOrchestrator(null, true, bus);
        var rec = new RecordingSink();
        var cap = new CapturingSink(rec);

        // Sensor requests stop
        orch.AddSensor(new StopPublishingSensor(OrchestrationStopReason.GoalAchieved));
        // Add a module that would otherwise propose and act
        orch.AddModule(new CountingModule());

        await orch.RunAsync(new UserIntent("test"), 5, CancellationToken.None, cap);

        // Verify orchestrator stopped for the reason and before any choice was made
        Assert.Equal(OrchestrationStopReason.GoalAchieved, cap.Reason);
        Assert.Empty(rec.Ticks); // no OnChosen calls recorded
    }

    [Fact]
    public async Task SensorStop_Prevents_Propose_And_Act()
    {
        var bus = new EventBus();
        var orch = new UtilityAiOrchestrator(null, true, bus);
        orch.AddSensor(new StopPublishingSensor(OrchestrationStopReason.SensorRequestedStop));
        var module = new CountingModule();
        orch.AddModule(module);

        await orch.RunAsync(new UserIntent("test"), 3, CancellationToken.None);

        // Because stop happens after sensing and before proposal gathering,
        // module.Propose should not have been called at all, and thus Act never runs.
        Assert.Equal(0, module.ProposeCalls);
        Assert.Equal(0, module.ActCalls);
    }
}
