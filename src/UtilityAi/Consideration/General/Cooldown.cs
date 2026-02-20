using UtilityAi.Utils;

namespace UtilityAi.Consideration.General;

/// <summary>
/// Prevents repeated execution of an action by requiring a cooldown period.
/// Returns 0.0 if the cooldown is active, 1.0 if enough time has elapsed.
/// </summary>
/// <typeparam name="T">The type of event to track for cooldown.</typeparam>
public sealed class Cooldown<T> : IConsideration where T : notnull
{
    private readonly TimeSpan _cooldownPeriod;

    /// <summary>
    /// Creates a cooldown consideration.
    /// </summary>
    /// <param name="cooldownPeriod">Minimum time that must elapse between events.</param>
    public Cooldown(TimeSpan cooldownPeriod)
    {
        if (cooldownPeriod <= TimeSpan.Zero)
            throw new ArgumentException("Cooldown period must be positive.", nameof(cooldownPeriod));

        _cooldownPeriod = cooldownPeriod;
    }

    public string Name => $"Cooldown<{typeof(T).Name}>({_cooldownPeriod.TotalSeconds}s)";

    public double Evaluate(Runtime rt)
    {
        var history = rt.Bus.GetHistory<T>(maxItems: 1);
        if (history.Count == 0)
            return 1.0; // No previous event, cooldown not active

        var lastEvent = history[^1];
        var elapsed = DateTimeOffset.UtcNow - lastEvent.Timestamp;

        return elapsed >= _cooldownPeriod ? 1.0 : 0.0;
    }
}
