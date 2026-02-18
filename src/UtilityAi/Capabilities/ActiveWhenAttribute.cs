namespace UtilityAi.Capabilities;

/// <summary>
/// Conditionally activates a capability based on runtime conditions.
/// </summary>
/// <remarks>
/// Prevents a capability from proposing unless the specified conditions are met.
/// Can check for fact types, intent slots, or use custom condition types.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ActiveWhenAttribute : Attribute
{
    /// <summary>
    /// Creates a condition based on a fact type existing on the EventBus.
    /// </summary>
    public ActiveWhenAttribute(Type factType)
    {
        FactType = factType ?? throw new ArgumentNullException(nameof(factType));
    }

    /// <summary>
    /// Creates a condition based on an intent slot having specific values.
    /// </summary>
    public ActiveWhenAttribute(string intentSlot, params string[] allowedValues)
    {
        IntentSlot = intentSlot ?? throw new ArgumentNullException(nameof(intentSlot));
        AllowedValues = allowedValues ?? throw new ArgumentNullException(nameof(allowedValues));
    }

    /// <summary>
    /// The fact type that must exist on the EventBus.
    /// </summary>
    public Type? FactType { get; }

    /// <summary>
    /// The intent slot name to check.
    /// </summary>
    public string? IntentSlot { get; }

    /// <summary>
    /// Allowed values for the intent slot.
    /// </summary>
    public string[]? AllowedValues { get; }
}
