using UtilityAi.Evaluators;
using UtilityAi.Utils;

namespace UtilityAi.Consideration.General;

/// <summary>
/// Evaluates the time elapsed since the most recent event of a given type.
/// Maps the elapsed time through a response curve to produce a utility score.
/// </summary>
/// <typeparam name="T">The type of event to measure time since.</typeparam>
public sealed class TimeSinceEvent<T> : IConsideration where T : class
{
    private readonly ICurve _curve;
    private readonly (double min, double max) _inputDomain;

    /// <summary>
    /// Creates a time-since-event consideration.
    /// </summary>
    /// <param name="curve">Response curve to map elapsed seconds to utility (0.0 to 1.0).</param>
    /// <param name="inputDomain">Expected range of elapsed seconds (min, max).</param>
    public TimeSinceEvent(ICurve curve, (double min, double max) inputDomain)
    {
        _curve = curve ?? throw new ArgumentNullException(nameof(curve));

        if (inputDomain.max <= inputDomain.min)
            throw new ArgumentException("Input domain max must be greater than min.", nameof(inputDomain));

        _inputDomain = inputDomain;
    }

    public string Name => $"TimeSince<{typeof(T).Name}>";

    public double Evaluate(Runtime rt)
    {
        var history = rt.Bus.GetHistory<T>(maxItems: 1);
        if (history.Count == 0)
            return 0.0; // No events exist

        var mostRecent = history[^1];
        var elapsed = (DateTimeOffset.UtcNow - mostRecent.Timestamp).TotalSeconds;

        // Normalize elapsed time to 0-1 range based on input domain
        var normalized = (elapsed - _inputDomain.min) / (_inputDomain.max - _inputDomain.min);
        normalized = Math.Clamp(normalized, 0.0, 1.0);

        return _curve.Evaluate(normalized);
    }
}
