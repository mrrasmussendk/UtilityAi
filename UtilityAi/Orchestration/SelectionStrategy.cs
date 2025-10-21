using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace UtilityAi.Orchestration;

public interface ISelectionStrategy
{
    Proposal Select(IReadOnlyList<(Proposal P, double Utility)> scored, Runtime rt);
}

public sealed class MaxUtilitySelection : ISelectionStrategy
{
    public Proposal Select(IReadOnlyList<(Proposal P, double Utility)> scored, Runtime rt)
    {
        if (scored.Count == 0) throw new InvalidOperationException("No proposals to select from.");
        // Assuming scored is already ordered desc by utility
        return scored[0].P;
    }
}
