namespace UtilityAi.Capabilities;

/// <summary>
/// Indicates that a capability module requires a specific fact type on the EventBus.
/// </summary>
/// <remarks>
/// Use this to declare dependencies on blackboard facts. The orchestrator can use this
/// for optimization or validation to ensure required facts exist before proposing.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequiresFactAttribute<T> : Attribute where T : notnull
{
    /// <summary>
    /// Whether this fact is optional (module can still run without it). Default: false.
    /// </summary>
    public bool Optional { get; init; }
}
