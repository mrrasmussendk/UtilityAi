using UtilityAi.Utils;

namespace UtilityAi.Actions;

public readonly record struct ActionResult(bool Success, string? Error = null);

public sealed record AgentOutcome(bool Success, double Cost, TimeSpan Latency);

public interface IAction
{
    string Id { get; }
    bool Gate(IBlackboard bb);
    Task<AgentOutcome> ActAsync(IBlackboard bb, CancellationToken ct);
}