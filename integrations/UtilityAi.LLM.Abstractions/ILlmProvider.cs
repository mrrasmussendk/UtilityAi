namespace UtilityAi.LLM.Abstractions;

/// <summary>
/// Abstraction for LLM providers (OpenAI, Anthropic, Azure, Ollama, etc.).
/// Implementations handle provider-specific API calls and response mapping.
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// Name of the provider (e.g., "OpenAI", "Anthropic").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Model identifier (e.g., "gpt-4", "claude-3-opus").
    /// </summary>
    string Model { get; }

    /// <summary>
    /// Sends a completion request to the LLM and returns the full response.
    /// </summary>
    /// <param name="request">The completion request with messages and options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The complete LLM response.</returns>
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default);

    /// <summary>
    /// Streams a completion response token-by-token.
    /// </summary>
    /// <param name="request">The completion request with messages and options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async stream of response chunks.</returns>
    IAsyncEnumerable<LlmStreamChunk> StreamAsync(LlmRequest request, CancellationToken ct = default);

    /// <summary>
    /// Estimates the number of tokens in a text string.
    /// Used for budget management and truncation.
    /// </summary>
    /// <param name="text">Text to count tokens for.</param>
    /// <returns>Estimated token count.</returns>
    int EstimateTokenCount(string text);
}
