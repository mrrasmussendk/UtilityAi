using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Orchestration;
using UtilityAi.Sensor;
using UtilityAi.Utils;
using Xunit;

namespace Tests;

public class OrchestratorTests
{
    private sealed class NoopSensor : ISensor
    {
        public Task SenseAsync(Runtime rt, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class PublishFactModule<T>(T value, string id, double baseScore) : ICapabilityModule
    {
        // Read baseScore to avoid CS9113 warning (it's unused in this test helper)
        private readonly double _baseScore = baseScore;
        public IEnumerable<Proposal> Propose(Runtime rt)
        {
            // touch the field so it's not optimized away entirely
            _ = _baseScore;
            yield return new Proposal(
                id: id,
                cons: Enumerable.Empty<IConsideration>(),
                act: ct => { rt.Bus.Publish(value); return Task.CompletedTask; }
            );
        }

    }

    [Fact]
    public async Task Orchestrator_ChoosesHighestUtility()
    {
        var bus = new EventBus();
        var orch = new UtilityAiOrchestrator(null, true, bus);
        orch.AddSensor(new NoopSensor());
        // Module A higher baseScore
        orch.AddModule(new PublishFactModule<string>("A", id: "A", baseScore: 0.9));
        // Module B lower baseScore
        orch.AddModule(new PublishFactModule<string>("B", id: "B", baseScore: 0.2));

        await orch.RunAsync(1, CancellationToken.None);

        Assert.Equal("A", bus.GetOrDefault<string>());
    }

    [Fact]
    public async Task Orchestrator_PublishesCapabilitiesSnapshot()
    {
        var bus = new EventBus();
        var orch = new UtilityAiOrchestrator(null, true, bus);
        orch.AddSensor(new NoopSensor());
        orch.AddModule(new PublishFactModule<string>("TestValue", id: "TestModule", baseScore: 0.9));

        await orch.RunAsync(1, CancellationToken.None);

        // Verify IReadOnlyList<CapabilityInfo> is published
        var capabilities = bus.GetOrDefault<IReadOnlyList<CapabilityInfo>>();
        Assert.NotNull(capabilities);
        Assert.Single(capabilities);
        Assert.Equal("PublishFactModule`1", capabilities[0].ModuleName);
        Assert.Single(capabilities[0].PotentialActions);
        Assert.Equal("TestModule", capabilities[0].PotentialActions[0].ProposalId);

        // Verify CapabilitiesSnapshot wrapper is also published
        var snapshot = bus.GetOrDefault<CapabilitiesSnapshot>();
        Assert.NotNull(snapshot);
        Assert.Single(snapshot.Capabilities);
        Assert.Equal("PublishFactModule`1", snapshot.Capabilities[0].ModuleName);
        Assert.Equal("TestModule", snapshot.Capabilities[0].PotentialActions[0].ProposalId);
    }
}
