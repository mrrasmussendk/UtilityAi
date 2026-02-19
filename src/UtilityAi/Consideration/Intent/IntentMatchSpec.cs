namespace UtilityAi.Consideration.Intent;

/// <summary>
/// Specifies how a proposal matches against intent patterns.
/// </summary>
public sealed record IntentMatchSpec(
    string Pattern,
    IntentMatchType MatchType = IntentMatchType.Exact
);

/// <summary>
/// Type of intent pattern matching.
/// </summary>
public enum IntentMatchType
{
    /// <summary>
    /// Exact string match (intent == pattern)
    /// </summary>
    Exact,

    /// <summary>
    /// Prefix match (intent.StartsWith(pattern))
    /// </summary>
    Prefix,

    /// <summary>
    /// Contains match (intent.Contains(pattern))
    /// </summary>
    Contains,

    /// <summary>
    /// Regex pattern match
    /// </summary>
    Regex
}

/// <summary>
/// Describes how a proposal uses an intent parameter for scoring.
/// </summary>
public sealed record IntentParameterUsage(
    string ParameterName,
    string Type,
    string? Description = null,
    ValueRange? Range = null,
    string[]? AllowedValues = null,
    string? ConsiderationName = null
);

/// <summary>
/// Specifies the valid range for a numeric parameter.
/// </summary>
public sealed record ValueRange(
    object Min,
    object Max,
    string? Unit = null
);
