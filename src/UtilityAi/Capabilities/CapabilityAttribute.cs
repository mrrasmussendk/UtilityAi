namespace UtilityAi.Capabilities;

/// <summary>
/// Marks a class as a capability module that can be auto-discovered and registered.
/// </summary>
/// <remarks>
/// This attribute enables automatic registration of capability modules without manual
/// AddModule() calls. Use with DiscoverCapabilities() extension method.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CapabilityAttribute : Attribute
{
    /// <summary>
    /// Registration priority (higher values register first). Default: 0.
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Optional domain name for grouping related capabilities.
    /// </summary>
    public string? Domain { get; init; }

    /// <summary>
    /// Optional list of capability types this module depends on.
    /// Dependencies are registered before this capability.
    /// </summary>
    public Type[]? DependsOn { get; init; }

    /// <summary>
    /// Whether this capability is enabled by default. Default: true.
    /// </summary>
    public bool Enabled { get; init; } = true;
}
