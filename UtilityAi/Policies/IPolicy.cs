using UtilityAi.Actions;
using UtilityAi.Utils;

namespace UtilityAi.Policies;
public interface IPolicy
{
    string Choose(IBlackboard bb, IEnumerable<IAction> candidates, IReadOnlyDictionary<string, double> x);
    void Learn(string actionId, IReadOnlyDictionary<string, double> x, double reward);
}
