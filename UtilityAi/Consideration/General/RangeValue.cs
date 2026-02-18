using UtilityAi.Utils;

namespace UtilityAi.Consideration.General;

/// <summary>
/// Evaluates whether a numeric value from a fact falls within a specified range.
/// Returns 1.0 if within range, 0.0 otherwise.
/// </summary>
/// <typeparam name="T">The type of fact containing the numeric value.</typeparam>
public sealed class RangeValue<T> : IConsideration where T : class
{
    private readonly Func<T, double> _selector;
    private readonly double _min;
    private readonly double _max;
    private readonly bool _inclusive;

    /// <summary>
    /// Creates a range consideration.
    /// </summary>
    /// <param name="selector">Function to extract the numeric value from the fact.</param>
    /// <param name="min">Minimum value of the range.</param>
    /// <param name="max">Maximum value of the range.</param>
    /// <param name="inclusive">If true, includes boundary values. Default is true.</param>
    public RangeValue(Func<T, double> selector, double min, double max, bool inclusive = true)
    {
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
        _min = min;
        _max = max;
        _inclusive = inclusive;

        if (_min > _max)
            throw new ArgumentException("Minimum value cannot be greater than maximum value.");
    }

    public string Name => $"Range<{typeof(T).Name}>({_min} to {_max})";

    public double Evaluate(Runtime rt)
    {
        var fact = rt.Bus.GetOrDefault<T>();
        if (fact == null) return 0.0;

        var value = _selector(fact);

        return _inclusive
            ? (value >= _min && value <= _max ? 1.0 : 0.0)
            : (value > _min && value < _max ? 1.0 : 0.0);
    }
}
