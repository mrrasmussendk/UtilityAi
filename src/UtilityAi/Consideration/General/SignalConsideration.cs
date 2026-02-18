using UtilityAi.Utils;

namespace UtilityAi.Consideration.General;

/// <summary>
/// Consideration that reads a value from a fact on the EventBus, normalizes it,
/// and applies a response curve.
///
/// This is the recommended way to score proposals based on continuous values.
/// The response curve shapes how the raw value translates to utility.
///
/// Example:
/// <code>
/// new SignalConsideration&lt;BatteryState&gt;(
///     name: "battery_level",
///     selector: battery => battery.Percentage,
///     curve: x => x * x, // Quadratic - rewards high battery
///     inputDomain: (0, 100))
/// </code>
/// </summary>
/// <typeparam name="T">The type of fact to read from the EventBus</typeparam>
public sealed class SignalConsideration<T> : IConsideration where T : notnull
{
    private readonly Func<T, double> _selector;
    private readonly Func<double, double> _curve;
    private readonly (double min, double max) _inputDomain;

    public string Name { get; }

    /// <summary>
    /// Creates a consideration that evaluates a signal from the EventBus.
    /// </summary>
    /// <param name="name">Name for debugging/logging</param>
    /// <param name="selector">Function to extract a numeric value from the fact</param>
    /// <param name="curve">Response curve to apply (receives normalized 0-1 input, returns 0-1 output)</param>
    /// <param name="inputDomain">The min/max range of the raw value for normalization</param>
    public SignalConsideration(
        string name,
        Func<T, double> selector,
        Func<double, double> curve,
        (double min, double max) inputDomain)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
        _curve = curve ?? throw new ArgumentNullException(nameof(curve));
        _inputDomain = inputDomain;

        if (_inputDomain.max <= _inputDomain.min)
            throw new ArgumentException("Input domain max must be greater than min", nameof(inputDomain));
    }

    public double Evaluate(Runtime rt)
    {
        // If fact doesn't exist, return 0.0
        if (!rt.Bus.TryGet<T>(out var fact))
            return 0.0;

        // Extract the raw value
        var rawValue = _selector(fact);

        // Normalize to 0-1 range
        var normalized = (rawValue - _inputDomain.min) / (_inputDomain.max - _inputDomain.min);
        var clamped = Math.Clamp(normalized, 0.0, 1.0);

        // Apply response curve
        var result = _curve(clamped);

        // Clamp output to valid range
        return Math.Clamp(result, 0.0, 1.0);
    }
}
