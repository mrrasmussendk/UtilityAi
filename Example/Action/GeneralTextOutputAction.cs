using UtilityAi.Actions;
using UtilityAi.Utils;

namespace Example.Action;

public class GeneralTextOutputAction : IAction
{
    public string Id => "output_text"; // "output_text"
    public bool Gate(IBlackboard bb)
    {
        string answerText = bb.GetOr("answer:text", "");
        return answerText.Length > 0;
    }

    public Task<AgentOutcome> ActAsync(IBlackboard bb, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}