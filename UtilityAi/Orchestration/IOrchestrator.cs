using UtilityAi.Utils;

namespace UtilityAi.Orchestration;

/// <summary>
/// Orchestrates the sense → propose → score → select → act loop over discrete ticks.
/// </summary>
public interface IOrchestrator
{
    /// <summary>
    /// Runs the orchestration loop for up to <paramref name="maxTicks"/> or until a stop condition occurs.
    /// </summary>
    /// <param name="bus">Blackboard-style event bus used by sensors, modules, and actions to exchange facts.</param>
    /// <param name="intent">High-level user intent and slots for the current session.</param>
    /// <param name="maxTicks">Maximum number of decision ticks to execute.</param>
    /// <param name="ct">Cancellation token to stop early.</param>
    /// <param name="sink">Optional observer to receive per-tick telemetry. Pass <c>null</c> (default) for no output.</param>
    Task RunAsync(UserIntent intent, int maxTicks, CancellationToken ct, IOrchestrationSink? sink = null);
}