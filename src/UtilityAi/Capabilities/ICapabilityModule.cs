using UtilityAi.Consideration;
using UtilityAi.Orchestration;
using UtilityAi.Utils;

namespace UtilityAi.Capabilities;

/// <summary>
/// Represents a capability module that proposes candidate actions based on the current state.
/// </summary>
/// <remarks>
/// Capability modules are the core decision-making components in the orchestration system.
/// Each module examines the current Runtime (EventBus facts and tick number) and
/// returns zero or more Proposals that represent potential actions the system could take.
/// Modules should be stateless and focus on a specific domain or capability area.
/// Examples: SearchModule, SummarizationModule, OutputModule, ValidationModule.
/// </remarks>
public interface ICapabilityModule
{
    /// <summary>
    /// Proposes zero or more candidate actions based on the current runtime state.
    /// </summary>
    /// <param name="rt">The current runtime context, providing access to the EventBus, Intent, and tick number.</param>
    /// <returns>An enumerable of proposals. May return empty if no actions are appropriate for the current state.</returns>
    IEnumerable<Proposal> Propose(Runtime rt);
}
