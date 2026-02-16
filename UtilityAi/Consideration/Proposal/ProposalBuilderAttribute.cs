namespace UtilityAi.Capabilities;

/// <summary>
/// Marks a method that builds proposals. The method should return IEnumerable&lt;Proposal&gt;
/// and can be combined with [ConsiderationWeight] to reduce boilerplate.
/// </summary>
/// <remarks>
/// This is for future expansion to support method-level proposal generation with
/// automatic consideration injection based on attributes.
///
/// Example:
/// <code>
/// [ProposalBuilder]
/// [ConsiderationWeight("priority", 0.7)]
/// [ConsiderationWeight("urgency", 0.3)]
/// public IEnumerable&lt;Proposal&gt; ProposeExecutions(Runtime rt) { ... }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ProposalBuilderAttribute : Attribute
{
    /// <summary>
    /// Optional prefix for all proposal IDs from this method.
    /// </summary>
    public string? IdPrefix { get; init; }

    /// <summary>
    /// Optional description for documentation.
    /// </summary>
    public string? Description { get; init; }
}
