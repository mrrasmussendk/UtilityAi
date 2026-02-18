using UtilityAi.Utils;

namespace UtilityAi.Consideration.Composite;

/// <summary>
/// Combines multiple considerations using multiplication (AND logic).
/// All considerations must score high for the result to be high.
/// Result approaches zero if any consideration is low.
/// </summary>
public sealed class AndConsideration : IConsideration
{
    private readonly IConsideration[] _considerations;

    /// <summary>
    /// Creates an AND consideration.
    /// </summary>
    /// <param name="considerations">Considerations to combine with AND logic.</param>
    public AndConsideration(params IConsideration[] considerations)
    {
        if (considerations == null || considerations.Length == 0)
            throw new ArgumentException("At least one consideration is required.", nameof(considerations));

        _considerations = considerations;
    }

    public string Name => $"AND({_considerations.Length})";

    public double Evaluate(Runtime rt)
    {
        var result = 1.0;
        foreach (var consideration in _considerations)
        {
            result *= consideration.Evaluate(rt);
            if (result == 0.0) break; // Short-circuit on zero
        }
        return result;
    }
}
