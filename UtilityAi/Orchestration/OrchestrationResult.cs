namespace UtilityAi.Orchestration;

public sealed record OrchestrationResult(
    string DoneReason,
    int Ticks,
    IReadOnlyList<string> Steps,
    IReadOnlyDictionary<string, object> FinalSnapshot
)
{
    public bool Success => DoneReason is "done:success";
}