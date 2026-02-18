using UtilityAi.Utils;

namespace UtilityAi.Consideration.Composite;

/// <summary>
/// Combines multiple considerations by taking the maximum value (OR logic).
/// Result is high if any consideration scores high.
/// </summary>
public sealed class OrConsideration : IConsideration
{
    private readonly IConsideration[] _considerations;

    /// <summary>
    /// Creates an OR consideration.
    /// </summary>
    /// <param name="considerations">Considerations to combine with OR logic.</param>
    public OrConsideration(params IConsideration[] considerations)
    {
        if (considerations == null || considerations.Length == 0)
            throw new ArgumentException("At least one consideration is required.", nameof(considerations));

        _considerations = considerations;
    }

    public string Name => $"OR({_considerations.Length})";

    public double Evaluate(Runtime rt)
    {
        var max = 0.0;
        foreach (var consideration in _considerations)
        {
            var value = consideration.Evaluate(rt);
            if (value > max)
            {
                max = value;
                if (max >= 1.0) break; // Short-circuit on max value
            }
        }
        return max;
    }
}
