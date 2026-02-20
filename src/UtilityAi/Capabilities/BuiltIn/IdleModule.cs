using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Orchestration;
using UtilityAi.Utils;

namespace UtilityAi.Capabilities.BuiltIn;

/// <summary>
/// A fallback module that proposes a no-op idle action.
/// Always has minimal utility, ensuring the orchestrator never stops due to no proposals.
/// Useful as a safety net when no other modules have eligible proposals.
/// </summary>
[Capability(Priority = -1000, Domain = "fallback")]
public sealed class IdleModule : ICapabilityModule
{
    private readonly double _idleUtility;

    /// <summary>
    /// Creates an idle module.
    /// </summary>
    /// <param name="idleUtility">The utility score for the idle action. Default is 0.001.</param>
    public IdleModule(double idleUtility = 0.001)
    {
        _idleUtility = Math.Clamp(idleUtility, 0.0, 1.0);
    }

    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        yield return new Proposal(
            id: "idle",
            cons: new IConsideration[]
            {
                new ConstantValue(_idleUtility)
            },
            act: _ => Task.CompletedTask
        );
    }

    public IEnumerable<ProposalDefinition> GetProposalDefinitions()
    {
        yield return new ProposalDefinition(
            ProposalId: "idle",
            Description: null,
            Prior: 1.0,
            Temperature: 0.0,
            ConsiderationNames: new[] { nameof(ConstantValue) }.ToList(),
            EligibilityNames: Array.Empty<string>().ToList(),
            NoRepeat: false,
            JsonOutput: null
        );
    }
}
