using UtilityAi.Utils;

namespace UtilityAi.Consideration.General;

/// <summary>
/// Inverts a numeric value from a fact (1.0 - value).
/// Useful for representing "lack of" or "opposite of" a property.
/// </summary>
/// <typeparam name="T">The type of fact containing the numeric value.</typeparam>
public sealed class InverseValue<T> : IConsideration where T : notnull
{
    private readonly Func<T, double> _selector;

    /// <summary>
    /// Creates an inverse value consideration.
    /// </summary>
    /// <param name="selector">Function to extract the numeric value (0.0 to 1.0) from the fact.</param>
    public InverseValue(Func<T, double> selector)
    {
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
    }

    public string Name => $"Inverse<{typeof(T).Name}>";

    public double Evaluate(Runtime rt)
    {
        var fact = rt.Bus.GetOrDefault<T>();
        if (fact == null) return 0.0;

        var value = _selector(fact);
        return Math.Clamp(1.0 - value, 0.0, 1.0);
    }
}
