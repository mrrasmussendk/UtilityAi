using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace UtilityAi.Orchestration;

/// <summary>
/// Strategy used by the orchestrator to choose a single <see cref="Proposal"/> from a set of already-scored candidates.
/// </summary>
/// <remarks>
/// The orchestrator evaluates proposals to a numeric utility and then delegates the final choice to an
/// implementation of this interface. Separating scoring from selection lets you plug different decision
/// policies without changing how utilities are computed.
///
/// Typical strategies include:
/// - Deterministic maximum (pick the highest utility).
/// - Random tie-breaking or pure stochastic choice among the top-N.
/// - Epsilon-greedy or softmax (Boltzmann) to balance exploration vs. exploitation.
/// - Threshold/guarded strategies that require a minimum utility before acting, otherwise choose a fallback.
///
/// The provided <paramref name="rt"/> exposes the current runtime context so strategies can consult
/// time/tick, intent, or the event bus to influence the choice (e.g., inject randomness or preferences).
/// </remarks>
public interface ISelectionStrategy
{
    /// <summary>
    /// Selects exactly one <see cref="Proposal"/> from the given list of scored candidates.
    /// </summary>
    /// <param name="scored">A non-empty list of tuples containing a proposal and its computed utility (0..1).</param>
    /// <param name="rt">The current <see cref="Runtime"/> context for this tick.</param>
    /// <returns>The chosen <see cref="Proposal"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="scored"/> is empty.</exception>
    /// <remarks>
    /// Unless otherwise documented by a specific implementation, you should not rely on the order of
    /// <paramref name="scored"/>. Some strategies may assume it is pre-sorted by descending utility for efficiency,
    /// while others might sort or randomize internally.
    /// </remarks>
    Proposal Select(IReadOnlyList<(Proposal P, double Utility)> scored, Runtime rt);
}

/// <summary>
/// A simple selection strategy that returns the proposal with the highest utility.
/// </summary>
/// <remarks>
/// Assumes the <c>scored</c> list is already ordered by descending utility for efficiency and picks the first item.
/// If the list is not ordered, the result is still a valid element but may not be the true maximum.
/// </remarks>
public sealed class MaxUtilitySelection : ISelectionStrategy
{
    /// <inheritdoc />
    public Proposal Select(IReadOnlyList<(Proposal P, double Utility)> scored, Runtime rt)
    {
        if (scored.Count == 0) throw new InvalidOperationException("No proposals to select from.");
        // Assuming scored is already ordered desc by utility
        return scored[0].P;
    }
}
