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
    /// <param name="maxTicks">Maximum number of decision ticks to execute.</param>
    /// <param name="ct">Cancellation token to stop early.</param>
    /// <param name="sink">Optional observer to receive per-tick telemetry. Pass <c>null</c> (default) for no output.</param>
    Task RunAsync( int maxTicks, CancellationToken ct, IOrchestrationSink? sink = null);

    /// <summary>
    /// Executes a single orchestration tick (Sense → Propose → Score → Act).
    /// </summary>
    /// <param name="tick">The current tick index.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="sink">Optional telemetry sink.</param>
    /// <returns>The result of the tick, including the chosen proposal and its utility, or null if no action was taken.</returns>
    Task<OrchestrationTick?> RunTickAsync( int tick, CancellationToken ct, IOrchestrationSink? sink = null);
}
