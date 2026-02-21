using UtilityAi.Utils;

namespace UtilityAi.Consideration.General;

/// <summary>
/// Consideration that returns 1.0 if a fact exists on the EventBus, 0.0 otherwise.
///
/// WARNING: Using this as a consideration means if the fact doesn't exist, the entire
/// proposal utility becomes 0.0 due to geometric mean. Consider using HasFactEligible
/// instead if you want a hard requirement.
///
/// Use this only when you want to SCORE based on fact existence, not filter proposals.
/// </summary>
public sealed class HasFact<T> : IConsideration where T : notnull
{
    public string Name { get; }

    /// <summary>
    /// Creates a consideration that checks if a fact exists.
    /// </summary>
    /// <param name="name">Name for debugging/logging. Defaults to "HasFact&lt;TypeName&gt;"</param>
    public HasFact(string? name = null)
    {
        Name = name ?? $"HasFact<{typeof(T).Name}>";
    }

    public double Evaluate(Runtime rt)
    {
        return rt.Bus.FactExists<T>() ? 1.0 : 0.0;
    }
}

/// <summary>
/// Consideration that returns 1.0 if a fact does NOT exist on the EventBus, 0.0 if it does.
///
/// WARNING: Using this as a consideration means if the fact EXISTS, the entire
/// proposal utility becomes 0.0 due to geometric mean. Consider using NotHasFactEligible
/// instead if you want a hard requirement.
///
/// Use this only when you want to SCORE based on fact absence, not filter proposals.
/// </summary>
public sealed class NotHasFact<T> : IConsideration where T : notnull
{
    public string Name { get; }

    /// <summary>
    /// Creates a consideration that checks if a fact does not exist.
    /// </summary>
    /// <param name="name">Name for debugging/logging. Defaults to "NotHasFact&lt;TypeName&gt;"</param>
    public NotHasFact(string? name = null)
    {
        Name = name ?? $"NotHasFact<{typeof(T).Name}>";
    }

    public double Evaluate(Runtime rt)
    {
        return rt.Bus.FactExists<T>() ? 0.0 : 1.0;
    }
}

/// <summary>
/// Consideration that checks if a fact exists and validates it with a predicate.
/// Returns 1.0 if the fact exists and predicate returns true, 0.0 otherwise.
///
/// WARNING: This will cause geometric mean = 0.0 if the predicate fails.
/// Consider using eligibility if you want a hard requirement.
///
/// Use this for scoring based on fact properties, not filtering.
/// </summary>
public sealed class HasFactWhere<T> : IConsideration where T : notnull
{
    private readonly Func<T, bool> _predicate;

    public string Name { get; }

    /// <summary>
    /// Creates a consideration that checks if a fact exists and satisfies a predicate.
    /// </summary>
    /// <param name="predicate">Predicate to test the fact against</param>
    /// <param name="name">Name for debugging/logging</param>
    public HasFactWhere(Func<T, bool> predicate, string? name = null)
    {
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        Name = name ?? $"HasFactWhere<{typeof(T).Name}>";
    }

    public double Evaluate(Runtime rt)
    {
        if (!rt.Bus.TryGet<T>(out var fact))
            return 0.0;

        return _predicate(fact) ? 1.0 : 0.0;
    }
}

/// <summary>
/// Consideration that always returns a fixed value.
/// Useful for fallback proposals or setting baseline utilities.
///
/// Example: A "last resort" proposal that should only win if nothing else can.
/// </summary>
public sealed class FixedValueConsideration : IConsideration
{
    private readonly double _value;

    public string Name { get; }

    /// <summary>
    /// Creates a consideration that always returns the same value.
    /// </summary>
    /// <param name="name">Name for debugging/logging</param>
    /// <param name="value">The fixed value to return (will be clamped to 0-1 range)</param>
    public FixedValueConsideration(string name, double value)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _value = Math.Clamp(value, 0.0, 1.0);
    }

    public double Evaluate(Runtime rt) => _value;
}

/// <summary>
/// Alias for <see cref="HasFact{T}"/> using explicit existence naming.
/// </summary>
public sealed class FactExists<T> : IConsideration where T : notnull
{
    private readonly HasFact<T> _inner;

    /// <summary>
    /// Creates a consideration that checks if a fact exists.
    /// </summary>
    /// <param name="name">Name for debugging/logging. Defaults to "FactExists&lt;TypeName&gt;"</param>
    public FactExists(string? name = null)
    {
        _inner = new HasFact<T>(name ?? $"FactExists<{typeof(T).Name}>");
    }

    public string Name => _inner.Name;

    public double Evaluate(Runtime rt) => _inner.Evaluate(rt);
}

/// <summary>
/// Alias for <see cref="NotHasFact{T}"/> using explicit existence naming.
/// </summary>
public sealed class FactMissing<T> : IConsideration where T : notnull
{
    private readonly NotHasFact<T> _inner;

    /// <summary>
    /// Creates a consideration that checks if a fact does not exist.
    /// </summary>
    /// <param name="name">Name for debugging/logging. Defaults to "FactMissing&lt;TypeName&gt;"</param>
    public FactMissing(string? name = null)
    {
        _inner = new NotHasFact<T>(name ?? $"FactMissing<{typeof(T).Name}>");
    }

    public string Name => _inner.Name;

    public double Evaluate(Runtime rt) => _inner.Evaluate(rt);
}
