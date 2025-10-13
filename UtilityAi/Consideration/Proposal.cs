using UtilityAi.Utils;

namespace UtilityAi.Consideration;

public sealed class Proposal
{
    public string Id { get; }
    public double BaseScore { get; } // prior, 0..1
    public IReadOnlyList<IConsideration> Considerations { get; }
    public Func<CancellationToken, Task> Act { get; }

    public string? JsonOutput;

    public Proposal(string id, double baseScore, IEnumerable<IConsideration> cons, Func<CancellationToken, Task> act)
    {
        Id = id;
        BaseScore = baseScore;
        Considerations = cons.ToList();
        Act = act;
    }

    // Multiply utilities (common Utility-AI practice). Small epsilon to avoid annihilation.
    public double Utility(Runtime rt)
    {
        double u = Math.Clamp(BaseScore, 0, 1);
        foreach (var c in Considerations) u *= Math.Clamp(c.Evaluate(rt), 0, 1);
        return u;
    }
}
