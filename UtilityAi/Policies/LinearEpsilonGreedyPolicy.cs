using System;
using System.Collections.Generic;
using System.Linq;
using UtilityAi.Actions;
using UtilityAi.Utils;

namespace UtilityAi.Policies;

/// <summary>
/// Linear policy with epsilon-greedy exploration on top of
/// a linear utility model, plus UCB bonus, a decayed reliability prior,
/// and a small anti-stickiness penalty.
/// </summary>
/// <remarks>
/// Blackboard keys used:
/// - "orchestrator:decisions" : int total decisions (for UCB).
/// - $"agent:{id}:count"      : int count of selections per agent (for UCB).
/// - $"agent:{id}:ewma_success": double EWMA success in [0..1] (reliability).
/// - $"agent:{id}:last_used"   : DateTimeOffset of last selection.
/// - "orchestrator:last"       : string last chosen agent id.
/// 
/// Thread-safety: this class is not thread-safe.
/// </remarks>
public sealed class LinearEpsilonGreedyPolicy : IPolicy
{
    // ---- Tunable parameters ------------------------------------------------
    private const double EpsilonMin = 0.05;
    private const double EpsilonStart = 0.50;
    private const double EpsilonHalfLifeDec = 200.0; // larger => slower decay

    private const double LearningRate = 0.20; // SGD step size
    private const double UcbStrength = 0.90; // UCB exploration weight
    private const double ReliabilityTauSec = 30.0; // time-decay of reliability (seconds)
    private const double ReliabilityWeight = 0.10; // small weight around 0.5 prior
    private const double AntiStickinessGamma = 0.03; // small tie-breaker

    // ---- State -------------------------------------------------------------
    // weights[agentId][featureKey] = weight
    private readonly Dictionary<string, Dictionary<string, double>> _weights = new();

    // using an int is sufficient; we cast to double where needed
    private int _decisionCount;

    /// <summary>
    /// Selects an action with epsilon-greedy exploration over a scored list.
    /// </summary>
    /// <param name="bb">Blackboard providing counts, reliability, and last-used signals.</param>
    /// <param name="candidates">Available actions to choose from.</param>
    /// <param name="x">Current feature vector (feature -> value).</param>
    /// <returns>The chosen action id.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="bb"/>, <paramref name="candidates"/>, or <paramref name="x"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if <paramref name="candidates"/> is empty.</exception>
    public string Choose(IBlackboard bb, IEnumerable<IAction> candidates, IReadOnlyDictionary<string, double> x)
    {
        if (bb is null) throw new ArgumentNullException(nameof(bb));
        if (candidates is null) throw new ArgumentNullException(nameof(candidates));
        if (x is null) throw new ArgumentNullException(nameof(x));

        var list = candidates as IList<IAction> ?? candidates.ToList();
        if (list.Count == 0) throw new InvalidOperationException("No candidates provided.");

        var epsilon = ComputeEpsilon(++_decisionCount);
        var scored = list
            .Select(a => (Id: a.Id, Score: Score(a.Id, x, bb)))
            .OrderByDescending(t => t.Score)
            .ToList();

        // Explore vs. exploit
        return (Random.Shared.NextDouble() < epsilon)
            ? scored[Random.Shared.Next(scored.Count)].Id
            : scored[0].Id;
    }

    /// <summary>
    /// Performs a single step of supervised update on the agent weights
    /// using the reward prediction error (SGD).
    /// </summary>
    /// <param name="actionId">The agent (candidate action) identifier.</param>
    /// <param name="x">Feature vector for the decision.</param>
    /// <param name="reward">Observed reward.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="actionId"/> or <paramref name="x"/> is null.</exception>
    public void Learn(string actionId, IReadOnlyDictionary<string, double> x, double reward)
    {
        if (actionId is null) throw new ArgumentNullException(nameof(actionId));
        if (x is null) throw new ArgumentNullException(nameof(x));

        var w = GetWeights(actionId);
        EnsureFeatureKeys(w, x.Keys);

        var prediction = PredictRaw(w, x); // do not clamp here
        var delta = reward - prediction;

        foreach (var (k, v) in x)
        {
            // w_k ← w_k + α * (r - ŷ) * x_k
            w[k] = w[k] + LearningRate * delta * v;
        }
    }

    // ---- Internals ---------------------------------------------------------

    private static double ComputeEpsilon(int decisions)
    {
        // ε(t) = max(ε_min, ε_start * exp(-t / decay))
        var eps = EpsilonStart * Math.Exp(-(double) decisions / EpsilonHalfLifeDec);
        return Math.Max(EpsilonMin, eps);
    }

    private double Score(string id, IReadOnlyDictionary<string, double> x, IBlackboard bb)
    {
        var now = DateTimeOffset.UtcNow;

        // 1) Linear utility (unclamped)
        var w = GetWeights(id);
        EnsureFeatureKeys(w, x.Keys);
        var u = PredictRaw(w, x);

        // 2) UCB exploration bonus
        var N = Math.Max(1, bb.GetOr("orchestrator:decisions", 0));
        var na = Math.Max(0, bb.GetOr($"agent:{id}:count", 0));
        var ucb = UcbStrength * Math.Sqrt(Math.Log(N + 1.0) / (na + 1.0));

        // 3) Reliability prior with time decay
        var r = bb.GetOr($"agent:{id}:ewma_success", 0.5); // [0,1]
        var lastUsed = bb.GetOr($"agent:{id}:last_used", DateTimeOffset.MinValue);
        var ageSec = Math.Max(0.0, (now - lastUsed).TotalSeconds);
        var rel = ReliabilityWeight * (r - 0.5) * Math.Exp(-ageSec / ReliabilityTauSec);

        // 4) Anti-stickiness
        var last = bb.GetOr("orchestrator:last", "");
        var antiStickiness = (last == id) ? AntiStickinessGamma : 0.0;

        // 5) Single clamp at the end
        return Clamp01(u + ucb + rel - antiStickiness);
    }

    /// <summary>
    /// Raw linear prediction Σ w_k * x_k (unclamped).
    /// </summary>
    private static double PredictRaw(Dictionary<string, double> w, IReadOnlyDictionary<string, double> x)
        => x.Sum(kv => kv.Value * (w.TryGetValue(kv.Key, out var wk) ? wk : 0.0));

    /// <summary>
    /// Gets the weight map for a given agent id, creating it if needed.
    /// </summary>
    private Dictionary<string, double> GetWeights(string agentId)
        => _weights.TryGetValue(agentId, out var w)
            ? w
            : (_weights[agentId] = new Dictionary<string, double>());

    /// <summary>
    /// Ensures all feature keys exist in the weight map (default 0.0).
    /// </summary>
    private static void EnsureFeatureKeys(Dictionary<string, double> w, IEnumerable<string> keys)
    {
        foreach (var k in keys)
            if (!w.ContainsKey(k))
                w[k] = 0.0;
    }

    private static double Clamp01(double v) => Math.Clamp(v, 0.0, 1.0);
}