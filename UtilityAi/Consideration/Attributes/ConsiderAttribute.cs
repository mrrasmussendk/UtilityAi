namespace UtilityAi.Consideration.Attributes;

/// <summary>
/// Declares that a proposal should use a specific IConsideration implementation.
/// </summary>
/// <remarks>
/// Alternative to [ConsiderationWeight] for using custom consideration types.
/// The consideration type must have a parameterless constructor.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class ConsiderAttribute : Attribute
{
    /// <summary>
    /// Creates a consideration reference.
    /// </summary>
    /// <param name="considerationType">Type implementing IConsideration. Must have parameterless constructor.</param>
    public ConsiderAttribute(Type considerationType)
    {
        if (considerationType is null)
            throw new ArgumentNullException(nameof(considerationType));

        if (!typeof(IConsideration).IsAssignableFrom(considerationType))
            throw new ArgumentException(
                $"Type {considerationType.Name} must implement IConsideration",
                nameof(considerationType));

        ConsiderationType = considerationType;
    }

    /// <summary>
    /// The IConsideration type to instantiate and apply.
    /// </summary>
    public Type ConsiderationType { get; }

    /// <summary>
    /// Optional weight multiplier for this consideration. Default: 1.0.
    /// </summary>
    public double Weight { get; init; } = 1.0;
}
