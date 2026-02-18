namespace UtilityAi.Sensor.LLM;

/// <summary>
/// Simple abstraction for LLM clients used by intent analysis.
/// Adapt your LLM library to this interface (e.g., wrap ILanguageModel from UtilityAi.LLM.Abstractions).
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Generates a text response from a prompt.
    /// </summary>
    Task<string> GenerateAsync(string prompt, CancellationToken ct = default);
}
