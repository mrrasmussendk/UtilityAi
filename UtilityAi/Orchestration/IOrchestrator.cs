using UtilityAi.Utils;

namespace UtilityAi.Orchestration;

public interface IOrchestrator
{
    Task<OrchestrationResult> RunAsync(IBlackboard bb, CancellationToken ct);
}