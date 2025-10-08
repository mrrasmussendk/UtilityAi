using UtilityAi.Utils;

namespace UtilityAi.Consideration;

public interface IConsideration
{
    double Consider(IBlackboard bb);
}