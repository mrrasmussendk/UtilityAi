using UtilityAi.Sensor.LLM;

namespace Tests.Sensor.LLM;

/// <summary>
/// Mock LLM client for testing intent analysis.
/// </summary>
public sealed class MockLlmClient : ILlmClient
{
    private readonly string _response;
    public int CallCount { get; private set; }
    public string? LastPrompt { get; private set; }

    public MockLlmClient(string response)
    {
        _response = response;
    }

    public Task<string> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        CallCount++;
        LastPrompt = prompt;
        return Task.FromResult(_response);
    }
}
