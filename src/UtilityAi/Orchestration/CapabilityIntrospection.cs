using UtilityAi.Consideration.Intent;
using UtilityAi.Capabilities;

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
    IReadOnlyList<IntentParameterUsage>? IntentParameters = null,
    IReadOnlyList<ProposalSkill>? Skills = null
);

/// <summary>
/// Fact containing a snapshot of available capabilities.
/// Published by the orchestrator and optionally used by LlmIntentSensor for context.
/// </summary>
public sealed record CapabilitiesSnapshot(IReadOnlyList<CapabilityInfo> Capabilities);
