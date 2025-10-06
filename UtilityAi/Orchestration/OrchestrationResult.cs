namespace UtilityAi.Orchestration;

public sealed record OrchestrationResult(
    string DoneReason,
    int Ticks,
    IReadOnlyList<DecisionStep> Steps,
    IReadOnlyDictionary<string, object> FinalSnapshot
)
{
    public bool Success => DoneReason is "done:success";
}