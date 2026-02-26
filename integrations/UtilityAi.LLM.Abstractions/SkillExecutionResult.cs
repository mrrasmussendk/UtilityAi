namespace UtilityAi.LLM.Abstractions;

/// <summary>
/// Result from executing a script-backed proposal skill.
/// </summary>
public sealed record SkillExecutionResult(
    string SkillName,
    string? ScriptPath,
    int ExitCode,
    string StandardOutput,
    string StandardError);
