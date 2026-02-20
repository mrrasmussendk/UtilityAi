using UtilityAi.Consideration.Intent;

namespace UtilityAi.Orchestration;

/// <summary>
/// Metadata about a capability module and its potential actions.
/// Useful for planning, debugging, and LLM context building.
/// </summary>
public sealed record CapabilityInfo(
    string ModuleName,
    string ModuleTypeName,
    IReadOnlyList<ProposalInfo> PotentialActions
);

/// <summary>
/// Metadata about a proposal (action) that a capability can generate.
/// </summary>
public sealed record ProposalInfo(
    string ProposalId,
    string? Description,
    double Prior,
    double Temperature,
    IReadOnlyList<string> ConsiderationNames,
    IReadOnlyList<string> EligibilityNames,
    bool NoRepeat,
    string? JsonOutput,
    IntentMatchSpec? IntentMatch = null,
    IReadOnlyList<IntentParameterUsage>? IntentParameters = null
);

/// <summary>
/// Static metadata about a proposal that a module can generate, independent of runtime state.
/// Used for capability introspection without requiring a Runtime instance.
/// </summary>
public sealed record ProposalDefinition(
    string ProposalId,
    string? Description,
    double Prior,
    double Temperature,
    IReadOnlyList<string> ConsiderationNames,
    IReadOnlyList<string> EligibilityNames,
    bool NoRepeat,
    string? JsonOutput,
    IntentMatchSpec? IntentMatch = null,
    IReadOnlyList<IntentParameterUsage>? IntentParameters = null
);
