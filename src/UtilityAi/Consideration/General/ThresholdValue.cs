using UtilityAi.Utils;

namespace UtilityAi.Consideration.General;

/// <summary>
/// Evaluates whether a numeric value from a fact exceeds a threshold.
/// Returns 1.0 if above threshold (or below if inverted), 0.0 otherwise.
/// </summary>
/// <typeparam name="T">The type of fact containing the numeric value.</typeparam>
public sealed class ThresholdValue<T> : IConsideration where T : notnull
{
    private readonly Func<T, double> _selector;
    private readonly double _threshold;
    private readonly bool _above;

    /// <summary>
    /// Creates a threshold consideration.
    /// </summary>
    /// <param name="selector">Function to extract the numeric value from the fact.</param>
    /// <param name="threshold">The threshold value to compare against.</param>
    /// <param name="above">If true, returns 1.0 when value is above threshold. If false, returns 1.0 when below.</param>
    public ThresholdValue(Func<T, double> selector, double threshold, bool above = true)
    {
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
        _threshold = threshold;
        _above = above;
    }

    public string Name => $"Threshold<{typeof(T).Name}>({_threshold}, {(_above ? "above" : "below")})";

    public double Evaluate(Runtime rt)
    {
        var fact = rt.Bus.GetOrDefault<T>();
        if (fact == null) return 0.0;

        var value = _selector(fact);
        return _above ? (value > _threshold ? 1.0 : 0.0) : (value < _threshold ? 1.0 : 0.0);
    }
}
