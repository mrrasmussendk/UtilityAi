namespace UtilityAi.Capabilities;

/// <summary>
/// Declares a consideration with a specific weight for declarative utility calculation.
/// </summary>
/// <remarks>
/// Multiple attributes can be stacked to define weighted considerations.
/// Example:
/// [ConsiderationWeight("priority", weight: 0.7)]
/// [ConsiderationWeight("age", weight: 0.3)]
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class ConsiderationWeightAttribute : Attribute
{
    /// <summary>
    /// Creates a consideration weight declaration.
    /// </summary>
    /// <param name="name">Name/key of the consideration for lookup in runtime context.</param>
    /// <param name="weight">Relative weight (0.0 to 1.0) for this consideration.</param>
    public ConsiderationWeightAttribute(string name, double weight = 1.0)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Weight = Math.Clamp(weight, 0.0, 1.0);
    }

    /// <summary>
    /// Name of the consideration (used to lookup values from context).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Weight of this consideration (0.0 to 1.0).
    /// </summary>
    public double Weight { get; }

    /// <summary>
    /// Optional curve type to apply to the raw value.
    /// </summary>
    public CurveType Curve { get; init; } = CurveType.Linear;

    /// <summary>
    /// Optional minimum threshold - consideration is 0 if value below this.
    /// </summary>
    public double? MinThreshold { get; init; }

    /// <summary>
    /// Optional maximum threshold - consideration is capped at this value.
    /// </summary>
    public double? MaxThreshold { get; init; }
}

/// <summary>
/// Curve types for transforming consideration values.
/// </summary>
public enum CurveType
{
    /// <summary>Linear mapping (identity function).</summary>
    Linear,

    /// <summary>Exponential growth curve.</summary>
    Exponential,

    /// <summary>Logarithmic curve (diminishing returns).</summary>
    Logarithmic,

    /// <summary>Logistic (S-curve) response.</summary>
    Logistic,

    /// <summary>Power curve with configurable exponent.</summary>
    Power
}
