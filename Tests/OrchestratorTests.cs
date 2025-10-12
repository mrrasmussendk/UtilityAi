using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Utils;
using UtilityAi.Orchestration;
using UtilityAi.Sensor;
using Xunit;

namespace Tests;

public class OrchestratorTests
{
    private sealed class StubSensor : ISensor
    {
        public Task SenseAsync(Runtime rt, CancellationToken ct)
        {
            // set a signal once
            if (rt.Tick == 0)
                rt.Bus.Publish("ready");
            return Task.CompletedTask;
        }
    }

    private sealed class StubModule : ICapabilityModule
    {
        public IEnumerable<Proposal> Propose(Runtime rt)
        {
            // two proposals: one gated on signal
            yield return new Proposal("p1", 0.6, Array.Empty<IConsideration>(), _ => { rt.Bus.Publish(1); return Task.CompletedTask; });
            var gated = rt.Bus.GetOrDefault<string>() == "ready" ? 1.0 : 0.0;
            yield return new Proposal("p2", 0.9 * gated, Array.Empty<IConsideration>(), _ => { rt.Bus.Publish(2); return Task.CompletedTask; });
        }
    }

    [Fact]
    public async Task Orchestrator_PicksHighestUtility_AndActs()
    {
        var orch = new UtilityAiOrchestrator()
            .AddSensor(new StubSensor())
            .AddModule(new StubModule());
        var bus = new EventBus();
        await orch.RunAsync(bus, new UserIntent("t"), maxTicks: 1, CancellationToken.None);
        // p2 should be picked after sensor sets the signal, thus publish 2
        Assert.Equal(2, bus.GetOrDefault<int>());
    }
}
