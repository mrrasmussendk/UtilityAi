namespace UtilityAi.Capabilities;

/// <summary>
/// Marks a method as a proposal action generator. The method will be automatically
/// invoked to create proposals based on current runtime state.
/// </summary>
/// <remarks>
/// Method signature must be:
/// - IEnumerable&lt;Proposal&gt; MethodName(Runtime rt)
/// - Task&lt;IEnumerable&lt;Proposal&gt;&gt; MethodName(Runtime rt)
///
/// Or for direct action execution:
/// - Task MethodName(Runtime rt, CancellationToken ct)
///
/// When combined with other attributes like [ConsiderationWeight] and [RequiresFact],
/// the system can automatically build proposals without manual Proposal construction.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ProposalActionAttribute : Attribute
{
    /// <summary>
    /// Creates a proposal action attribute with an ID pattern.
    /// </summary>
    /// <param name="idPattern">
    /// ID pattern for the proposal. Can include placeholders like {variableName}
    /// which will be resolved from method parameters or runtime context.
    /// </param>
    public ProposalActionAttribute(string idPattern)
    {
        IdPattern = idPattern ?? throw new ArgumentNullException(nameof(idPattern));
    }

    /// <summary>
    /// Proposal ID pattern (may contain {placeholders}).
    /// </summary>
    public string IdPattern { get; }

    /// <summary>
    /// Optional description of what this action does.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Minimum utility threshold for this proposal to be considered. Default: 0.
    /// </summary>
    public double MinUtility { get; init; }
}
