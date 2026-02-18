using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace UtilityAi.Orchestration.SelectionStrategies;

/// <summary>
/// Selection strategy that rotates through proposals tied for the highest utility.
/// Useful for load balancing, ensuring all equivalent options get executed fairly over time.
/// </summary>
public sealed class RoundRobinSelectionStrategy : ISelectionStrategy
{
    private readonly double _tieThreshold;
    private readonly Dictionary<string, int> _lastSelectedIndex = new();
    private readonly object _lock = new();

    /// <summary>
    /// Creates a new round-robin selection strategy.
    /// </summary>
    /// <param name="tieThreshold">Maximum utility difference to consider proposals "tied" (default: 0.001)</param>
    public RoundRobinSelectionStrategy(double tieThreshold = 0.001)
    {
        _tieThreshold = tieThreshold;
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

        // Multiple tied - use round-robin
        // Create a stable key from the tied proposal IDs
        var tieGroupKey = string.Join("|", topProposals.Select(p => p.P.Id).OrderBy(id => id));

        lock (_lock)
        {
            // Get the last index for this tie group (or -1 if first time)
            if (!_lastSelectedIndex.TryGetValue(tieGroupKey, out var lastIndex))
                lastIndex = -1;

            // Move to next index (wrapping around)
            var nextIndex = (lastIndex + 1) % topProposals.Count;
            _lastSelectedIndex[tieGroupKey] = nextIndex;

            return topProposals[nextIndex].P;
        }
    }
}
