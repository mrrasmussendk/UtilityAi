using UtilityAi.Utils;

namespace UtilityAi.Consideration.Composite;

/// <summary>
/// Inverts a consideration's result (1.0 - value).
/// Useful for expressing negation in utility logic.
/// </summary>
public sealed class NotConsideration : IConsideration
{
    private readonly IConsideration _consideration;

    /// <summary>
    /// Creates a NOT consideration.
    /// </summary>
    /// <param name="consideration">The consideration to invert.</param>
    public NotConsideration(IConsideration consideration)
    {
        _consideration = consideration ?? throw new ArgumentNullException(nameof(consideration));
    }

    public string Name => $"NOT({_consideration.Name})";

    public double Evaluate(Runtime rt)
    {
        return 1.0 - _consideration.Evaluate(rt);
    }
}
