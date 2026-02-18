using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace UtilityAi.Orchestration.SelectionStrategies;

/// <summary>
/// Selection strategy that randomly chooses among proposals tied for the highest utility.
/// Useful for exploration, A/B testing, and load balancing scenarios.
/// </summary>
public sealed class RandomSelectionStrategy : ISelectionStrategy
{
    private readonly Random _random;
    private readonly double _tieThreshold;

    /// <summary>
    /// Creates a new random selection strategy.
    /// </summary>
    /// <param name="tieThreshold">Maximum utility difference to consider proposals "tied" (default: 0.001)</param>
    /// <param name="seed">Optional random seed for reproducibility in tests</param>
    public RandomSelectionStrategy(double tieThreshold = 0.001, int? seed = null)
    {
        _tieThreshold = tieThreshold;
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public Proposal Select(IReadOnlyList<(Proposal P, double Utility)> scored, Runtime rt)
    {
        if (scored.Count == 0)
            throw new InvalidOperationException("Cannot select from empty proposal list");

        if (scored.Count == 1)
            return scored[0].P;

        // Find the maximum utility
        var maxUtility = scored.Max(s => s.Utility);

        // Find all proposals within threshold of max (considered "tied")
        var topProposals = scored
            .Where(s => Math.Abs(s.Utility - maxUtility) <= _tieThreshold)
            .ToList();

        // If only one at the top, return it
        if (topProposals.Count == 1)
            return topProposals[0].P;

        // Multiple tied - pick randomly
        var selected = topProposals[_random.Next(topProposals.Count)];

        return selected.P;
    }
}
