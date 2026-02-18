using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Example.Maf.Agents;

/// <summary>
/// A simulated MAF writing agent that composes responses based on research.
/// In production, this would wrap a real LLM-powered agent (e.g., via Azure OpenAI).
/// </summary>
public sealed class WriterAgent : AIAgent
{
    public override string? Name => "WriterAgent";
    public override string? Description => "Composes well-structured responses and summaries from research findings.";

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
        var input = userMessage?.Text ?? "no input provided";

        // Simulate writing delay
        await Task.Delay(150, cancellationToken);

        var result = $"📝 Composed response based on: '{input}'\n\n" +
                     $"After reviewing the available information, here is a comprehensive summary:\n" +
                     $"The analysis reveals several important findings that are relevant to your query. " +
                     $"The evidence supports a clear conclusion, and further details can be provided upon request.";

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
