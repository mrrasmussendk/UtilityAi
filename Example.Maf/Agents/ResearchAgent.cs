using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Example.Maf.Agents;

/// <summary>
/// A simulated MAF research agent that gathers information on a topic.
/// In production, this would wrap a real LLM-powered agent (e.g., via Azure OpenAI).
/// </summary>
public sealed class ResearchAgent : AIAgent
{
    public override string? Name => "ResearchAgent";
    public override string? Description => "Gathers and synthesizes information from various sources on a given topic.";

    protected override ValueTask<AgentSession> CreateSessionCoreAsync(
        CancellationToken cancellationToken = default)
    {
        return new ValueTask<AgentSession>(
            new SimpleAgentSession());
    }

    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var userMessage = messages.LastOrDefault(m => m.Role == ChatRole.User);
        var query = userMessage?.Text ?? "unknown topic";

        // Simulate research delay
        await Task.Delay(200, cancellationToken);

        var result = $"Research findings for '{query}': " +
                     $"Based on analysis of multiple sources, the key points are: " +
                     $"1) The topic is well-documented in recent literature. " +
                     $"2) Current consensus supports the mainstream view. " +
                     $"3) Further investigation may yield additional insights.";

        return new AgentResponse(
            new ChatMessage(ChatRole.Assistant, result));
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await RunCoreAsync(messages, session, options, cancellationToken);
        yield return new AgentResponseUpdate(ChatRole.Assistant, response.Text);
    }

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement data, JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default) =>
        new(new SimpleAgentSession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session, JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default) =>
        new(JsonSerializer.SerializeToElement(new { }));
}
