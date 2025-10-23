using UtilityAi.Utils;

namespace UtilityAi.Consideration;

public sealed class Proposal
{
    public string Id { get; }
    public IReadOnlyList<IConsideration> Considerations { get; }
    public IReadOnlyList<IEligibility> Eligibilities { get; }
    public Func<CancellationToken, Task> Act { get; }
    public double Prior { get; init; } = 1.0;        // base tendency for this action (0..1)
    public double Temperature { get; init; } = 1.0;  // >1 = sharper/stricter, <1 = flatter
    const double Eps = 1e-6;

    public string? JsonOutput;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="id"></param>
    /// <param name="baseScore"></param>
    /// <param name="cons"></param>
    /// <param name="act"></param>
    /// <param name="eligibilities">Optional eligibility hard-gates. If null or empty, proposal is always eligible.</param>
    public Proposal(string id, IEnumerable<IConsideration> cons, Func<CancellationToken, Task> act, IEnumerable<IEligibility>? eligibilities = null)
    {
        Id = id;
        Considerations = cons.ToList();
        Act = act;
        Eligibilities = (eligibilities ?? Array.Empty<IEligibility>()).ToList();
    }
    static double Clamp01(double x) => Math.Clamp(x, 0, 1);

    public bool IsEligible(Runtime rt) => Eligibilities.Count == 0 || Eligibilities.All(e => e.IsEligible(rt));

    // Multiply utilities (common Utility-AI practice). Small epsilon to avoid annihilation.
    /// <summary>
    /// 
    /// </summary>
    /// <param name="rt"></param>
    /// <returns></returns>
    public double Utility(Runtime rt)
    {
        // Prior/bias in [0,1], protected from complete annihilation by epsilon
        var prior = Math.Max(Clamp01(Prior), Eps);

        // Handle the no-considerations case: utility equals prior
        if (Considerations.Count == 0)
            return Clamp01(prior);

        // Accumulate consideration values in log-space for geometric mean
        double sumLog = 0.0;
        int count = 0;

        foreach (var c in Considerations)
        {
            // Clamp each consideration and protect with epsilon
            var v = Math.Max(Clamp01(c.Evaluate(rt)), Eps);
            sumLog += Math.Log(v);
            count++;
        }

        // Geometric mean of considerations in (0,1]
        var geom = Math.Exp(sumLog / Math.Max(1, count));
        var gamma = Math.Max(Temperature, Eps);

        // Final utility: prior times tempered geometric mean of considerations
        var utility = prior * Math.Pow(geom, gamma);

        return Clamp01(utility);
    }

}
