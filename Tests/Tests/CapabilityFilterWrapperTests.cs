using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Orchestration;
using UtilityAi.Utils;
using Xunit;

namespace Tests;

public class CapabilityFilterWrapperTests
{
    [Fact]
    public void DiscoverCapabilities_RequiresFact_MissingFactSkipsProposal()
    {
        var bus = new EventBus();
        var orchestrator = new UtilityAiOrchestrator(bus: bus)
            .DiscoverCapabilities(typeof(CapabilityFilterWrapperTests).Assembly);

        var capabilityInfo = orchestrator.GetCapabilitiesInfo(new Runtime(bus, 0));

        Assert.DoesNotContain(
            capabilityInfo.SelectMany(c => c.PotentialActions),
            p => p.ProposalId == "requires.fact");
    }

    [Fact]
    public void DiscoverCapabilities_RequiresFact_PresentFactAllowsProposal()
    {
        var bus = new EventBus();
        bus.Publish(new RequiredFact());

        var orchestrator = new UtilityAiOrchestrator(bus: bus)
            .DiscoverCapabilities(typeof(CapabilityFilterWrapperTests).Assembly);

        var capabilityInfo = orchestrator.GetCapabilitiesInfo(new Runtime(bus, 0));

        Assert.Contains(
            capabilityInfo.SelectMany(c => c.PotentialActions),
            p => p.ProposalId == "requires.fact");
    }

    private sealed record RequiredFact;

    [Capability]
    [RequiresFact<RequiredFact>]
    private sealed class RequiresFactDiscoveryModule : ICapabilityModule
    {
        public IEnumerable<Proposal> Propose(Runtime rt)
        {
            yield return new Proposal("requires.fact", Array.Empty<IConsideration>(), _ => Task.CompletedTask);
        }
    }
}
