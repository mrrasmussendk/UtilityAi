using UtilityAi.Utils;

namespace UtilityAi.Consideration;

public sealed class Proposal
{
    public string Id { get; }
    public IReadOnlyList<IConsideration> Considerations { get; }
    public Func<CancellationToken, Task> Act { get; }
    const double Eps = 1e-6;

    public string? JsonOutput;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="id"></param>
    /// <param name="baseScore"></param>
    /// <param name="cons"></param>
    /// <param name="act"></param>
    public Proposal(string id, IEnumerable<IConsideration> cons, Func<CancellationToken, Task> act)
    {
        Id = id;
        Considerations = cons.ToList();
        Act = act;
    }
    static double Clamp01(double x) => Math.Clamp(x, 0, 1);


    // Multiply utilities (common Utility-AI practice). Small epsilon to avoid annihilation.
    /// <summary>
    /// 
    /// </summary>
    /// <param name="rt"></param>
    /// <returns></returns>
    public double Utility(Runtime rt)
    {
        // Prior as a weight/bias (never 0)

        // Geo-mean of considerations (never 0)
        int n = Math.Max(1, Considerations.Count);
        double product = 1.0;
        foreach (var c in Considerations)
            product *= Math.Max(Clamp01(c.Evaluate(rt)), Eps);

        double geom = Math.Pow(product, 1.0 / n);
        return geom; // final utility in [~0,1]
    }
}
