using UtilityAi.Utils;

namespace UtilityAi.Consideration.General;

/// <summary>
/// Returns a constant value regardless of runtime state.
/// Useful for testing, debugging, or fixed weights.
/// </summary>
public sealed class ConstantValue : IConsideration
{
    private readonly double _value;

    /// <summary>
    /// Creates a constant value consideration.
    /// </summary>
    /// <param name="value">The constant value to return (typically 0.0 to 1.0).</param>
    public ConstantValue(double value)
    {
        _value = Math.Clamp(value, 0.0, 1.0);
    }

    public string Name => $"Constant({_value:F2})";

    public double Evaluate(Runtime rt) => _value;
}
