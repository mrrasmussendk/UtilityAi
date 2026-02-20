using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Facts;
using UtilityAi.Orchestration;
using UtilityAi.Orchestration.Events;
using UtilityAi.Utils;

namespace UtilityAi.Capabilities.BuiltIn;

/// <summary>
/// A module that responds to stop signals by publishing a StopOrchestrationEvent.
/// Useful for graceful shutdown based on external conditions or user commands.
/// </summary>
[Capability(Priority = 10000, Domain = "control")]
[RequiresFact<StopSignal>]
public sealed class StopOnSignalModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var stopSignal = rt.Bus.GetOrDefault<StopSignal>();
        if (stopSignal == null)
            yield break;

        yield return new Proposal(
            id: "stop.on-signal",
            cons: new IConsideration[]
            {
                new HasFact<StopSignal>(),
                new ConstantValue(1.0) // Highest priority when signal exists
            },
            act: ct =>
            {
                rt.Bus.Publish(new StopOrchestrationEvent(
                    Orchestration.OrchestrationStopReason.GoalAchieved,
                    stopSignal.Reason));
                return Task.CompletedTask;
            }
        );
    }

    public IEnumerable<ProposalDefinition> GetProposalDefinitions()
    {
        yield return new ProposalDefinition(
            ProposalId: "stop.on-signal",
            Description: null,
            Prior: 1.0,
            Temperature: 0.0,
            ConsiderationNames: new[] { nameof(HasFact<StopSignal>), nameof(ConstantValue) }.ToList(),
            EligibilityNames: Array.Empty<string>().ToList(),
            NoRepeat: false,
            JsonOutput: null
        );
    }
}
