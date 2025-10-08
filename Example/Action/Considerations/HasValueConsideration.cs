using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace Example.Action.Considerations;

public class HasValueConsideration(string key, bool isInverted = false) : ConsiderationBase
{
    private string _key { get; set; } = key;


    protected override double ComputeRaw(IBlackboard bb)
    {
        if (isInverted)
        {
            return bb.Has(_key) ? 0 : 1;
        }

        return bb.Has(_key) ? 1 : 0;
    }
}