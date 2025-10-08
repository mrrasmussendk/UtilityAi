using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace Example.Action.Considerations;

public class SmsOutPutConsideration : ConsiderationBase
{
    protected override double ComputeRaw(IBlackboard bb)
    {
        string outputMode = bb.GetOr("task:output_mode", "");
        return bb.Has("answer:text") && outputMode == "sms" ? 1 : 0;
    }
}