using UtilityAi.Utils;

namespace UtilityAi.Policies;

public interface IStopPolicy
{
    bool ShouldStop(IBlackboard bb, IReadOnlyList<ScoredDecision> lastScores, int step);
}