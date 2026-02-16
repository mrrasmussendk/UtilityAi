using UtilityAi.Utils;

namespace UtilityAi.Sensor;

/// <summary>
/// Represents a sensor that observes the environment or internal state and publishes facts to the EventBus.
/// </summary>
/// <remarks>
/// Sensors run at the beginning of each orchestration tick, before proposals are gathered.
/// They are responsible for updating the EventBus with fresh observations that will be
/// used by considerations to score proposals.
/// Common sensor implementations include: reading external APIs, monitoring system resources,
/// processing user input, or deriving higher-level facts from existing EventBus state.
/// </remarks>
public interface ISensor
{
    /// <summary>
    /// Observes the environment and publishes zero or more facts to the EventBus via the Runtime.
    /// </summary>
    /// <param name="rt">The current runtime context, providing access to the EventBus, Intent, and tick number.</param>
    /// <param name="ct">Cancellation token to allow early termination of long-running sense operations.</param>
    /// <returns>A task that completes when sensing is finished.</returns>
    Task SenseAsync(Runtime rt, CancellationToken ct);
}
