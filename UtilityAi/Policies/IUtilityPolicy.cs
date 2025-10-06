using UtilityAi.Actions;
using UtilityAi.Utils;

namespace UtilityAi.Policies;

public interface IUtilityPolicy
{
    ScoredDecision Score(IAction decision, IBlackboard bb);
}