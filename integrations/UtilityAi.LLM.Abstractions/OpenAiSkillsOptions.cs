namespace UtilityAi.LLM.Abstractions;

/// <summary>
/// OpenAI execution environment for mounting hosted or inline skills.
/// </summary>
public enum OpenAiSkillEnvironmentType
{
    ContainerAuto,
    Local
}

/// <summary>
/// Reference to a hosted OpenAI skill.
/// </summary>
public sealed record OpenAiSkillReference(
    string SkillId,
    string? Version = null);

/// <summary>
/// Inline skill payload (base64-encoded zip bundle).
/// </summary>
public sealed record OpenAiInlineSkill(
    string Base64ZipBundle);

/// <summary>
/// Options for mounting skills in OpenAI shell environments.
/// </summary>
public sealed record OpenAiSkillsOptions(
    OpenAiSkillEnvironmentType EnvironmentType = OpenAiSkillEnvironmentType.ContainerAuto,
    IReadOnlyList<OpenAiSkillReference>? References = null,
    IReadOnlyList<OpenAiInlineSkill>? Inline = null);
