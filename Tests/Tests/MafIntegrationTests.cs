using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using UtilityAi.Consideration;
using UtilityAi.Maf;
using UtilityAi.Orchestration;
using UtilityAi.Utils;
using Xunit;

namespace Tests;

/// <summary>
/// Tests for the Microsoft Agent Framework (MAF) integration.
/// </summary>
public class MafIntegrationTests
{
    // ─── MafClient ────────────────────────────────────────────────

    [Fact]
    public void MafClient_CreatesAgent()
    {
        var client = new MafClient("https://example.openai.azure.com");

        var agent = client.CreateAgent("test-agent", "You are a test agent");

        Assert.NotNull(agent);
        Assert.Equal("test-agent", agent.Name);
    }

    [Fact]
    public void MafClient_GetAgentsClient_ReturnsClient()
    {
        var client = new MafClient("https://example.openai.azure.com");

        var agentsClient = client.GetAgentsClient();

        Assert.NotNull(agentsClient);
    }

    [Fact]
    public async Task MafClient_AgentCanBeUsedInProposal()
    {
        var client = new MafClient("https://example.openai.azure.com");
        var bus = new EventBus();
        var rt = new Runtime(bus, new UserIntent("test query"), 0);

        var proposal = new Proposal(
            id: "test",
            cons: new IConsideration[] { new FixedScore(1.0) },
            act: async ct =>
            {
                var agent = client.CreateAgent("test", "You are helpful");
                Assert.NotNull(agent);
                Assert.Equal("test", agent.Name);

                bus.Publish(new TestResult("Agent created successfully"));
                await Task.CompletedTask;
            });

        await proposal.Act(CancellationToken.None);

        var result = bus.GetOrDefault<TestResult>();
        Assert.NotNull(result);
        Assert.Contains("created", result.Text);
    }


    // ─── Test Helpers ────────────────────────────────────────────

    /// <summary>
    /// A stub MAF agent for testing that returns a configured response.
    /// </summary>
    private sealed class StubAgent : AIAgent
    {
        private readonly string _response;
        private readonly Action<string>? _onMessage;

        public StubAgent(string response, Action<string>? onMessage = null)
        {
            _response = response;
            _onMessage = onMessage;
        }

        protected override ValueTask<AgentSession> CreateSessionCoreAsync(
            CancellationToken cancellationToken = default) =>
            new(new StubSession());

        protected override Task<AgentResponse> RunCoreAsync(
            IEnumerable<ChatMessage> messages,
            AgentSession? session,
            AgentRunOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var userMsg = messages.LastOrDefault(m => m.Role == ChatRole.User);
            _onMessage?.Invoke(userMsg?.Text ?? "");

            return Task.FromResult(
                new AgentResponse(new ChatMessage(ChatRole.Assistant, _response)));
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
            new(new StubSession());

        protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
            AgentSession session, JsonSerializerOptions? options = null,
            CancellationToken cancellationToken = default) =>
            new(JsonSerializer.SerializeToElement(new { }));
    }

    private sealed class StubSession : AgentSession
    {
        public override object? GetService(Type serviceType, object? serviceKey = null) => null;
    }

    /// <summary>
    /// A simple fixed-value consideration for testing.
    /// </summary>
    private sealed class FixedScore : IConsideration
    {
        private readonly double _score;
        public FixedScore(double score) => _score = score;
        public string Name => "fixed";
        public double Evaluate(Runtime rt) => _score;
    }

    /// <summary>
    /// Test result record for EventBus.
    /// </summary>
    private record TestResult(string Text);
}
