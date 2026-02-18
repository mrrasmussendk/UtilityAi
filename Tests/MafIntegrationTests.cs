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
/// Tests for the Microsoft Agent Framework (MAF) integration layer.
/// </summary>
public class MafIntegrationTests
{
    // ─── MafAgentCapabilityModule ─────────────────────────────────

    [Fact]
    public void MafAgentCapabilityModule_ProposesAgentAction()
    {
        var agent = new StubAgent("test-response");
        var module = new MafAgentCapabilityModule(
            agent, "test-agent",
            considerations: new IConsideration[] { new FixedScore(0.8) });

        var bus = new EventBus();
        var rt = new Runtime(bus, new UserIntent("hello"), 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Single(proposals);
        Assert.Equal("maf.agent.test-agent", proposals[0].Id);
    }

    [Fact]
    public void MafAgentCapabilityModule_SkipsUnavailableAgent()
    {
        var agent = new StubAgent("response");
        var module = new MafAgentCapabilityModule(
            agent, "offline-agent",
            considerations: new IConsideration[] { new FixedScore(0.8) });

        var bus = new EventBus();
        // Mark agent as unavailable
        bus.Publish(new MafAgentCatalog(new[]
        {
            new MafAgentRegistration("offline-agent", agent, IsAvailable: false)
        }));

        var rt = new Runtime(bus, new UserIntent("test"), 0);
        var proposals = module.Propose(rt).ToList();

        Assert.Empty(proposals);
    }

    [Fact]
    public async Task MafAgentCapabilityModule_ExecutesAgent_PublishesResult()
    {
        var agent = new StubAgent("Hello from MAF agent!");
        var module = new MafAgentCapabilityModule(
            agent, "greeting-agent",
            considerations: new IConsideration[] { new FixedScore(1.0) });

        var bus = new EventBus();
        var rt = new Runtime(bus, new UserIntent("greet me"), 0);

        var proposals = module.Propose(rt).ToList();
        Assert.Single(proposals);

        // Execute the proposal action
        await proposals[0].Act(CancellationToken.None);

        // Verify result was published to EventBus
        var result = bus.GetOrDefault<MafAgentResult>();
        Assert.NotNull(result);
        Assert.Equal("greeting-agent", result.AgentName);
        Assert.Equal("Hello from MAF agent!", result.Text);
    }

    [Fact]
    public async Task MafAgentCapabilityModule_UsesCustomMessageProvider()
    {
        string? capturedMessage = null;
        var agent = new StubAgent("response", msg => capturedMessage = msg);
        var module = new MafAgentCapabilityModule(
            agent, "custom-agent",
            considerations: new IConsideration[] { new FixedScore(1.0) },
            messageProvider: rt => "custom message from provider");

        var bus = new EventBus();
        var rt = new Runtime(bus, new UserIntent("ignored"), 0);

        var proposals = module.Propose(rt).ToList();
        await proposals[0].Act(CancellationToken.None);

        Assert.Equal("custom message from provider", capturedMessage);
    }

    [Fact]
    public async Task MafAgentCapabilityModule_ExtractsMessageFromUserIntentQuery()
    {
        string? capturedMessage = null;
        var agent = new StubAgent("response", msg => capturedMessage = msg);
        var module = new MafAgentCapabilityModule(
            agent, "intent-agent",
            considerations: new IConsideration[] { new FixedScore(1.0) });

        var bus = new EventBus();
        var intent = new UserIntent("What is AI?");
        var rt = new Runtime(bus, intent, 0);

        var proposals = module.Propose(rt).ToList();
        await proposals[0].Act(CancellationToken.None);

        Assert.Equal("What is AI?", capturedMessage);
    }

    // ─── MafAgentSensor ──────────────────────────────────────────

    [Fact]
    public async Task MafAgentSensor_PublishesCatalogToEventBus()
    {
        var agent1 = new StubAgent("response1");
        var agent2 = new StubAgent("response2");

        var sensor = new MafAgentSensor();
        sensor.Register(new MafAgentRegistration("agent1", agent1));
        sensor.Register(new MafAgentRegistration("agent2", agent2));

        var bus = new EventBus();
        var rt = new Runtime(bus, new UserIntent("test"), 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var catalog = bus.GetOrDefault<MafAgentCatalog>();
        Assert.NotNull(catalog);
        Assert.Equal(2, catalog.Agents.Count);
        Assert.Equal("agent1", catalog.Agents[0].AgentName);
        Assert.Equal("agent2", catalog.Agents[1].AgentName);
    }

    // ─── MafConsiderations ───────────────────────────────────────

    [Fact]
    public void MafAgentAvailable_ReturnsOne_WhenAgentIsAvailable()
    {
        var agent = new StubAgent("resp");
        var bus = new EventBus();
        bus.Publish(new MafAgentCatalog(new[]
        {
            new MafAgentRegistration("my-agent", agent, IsAvailable: true)
        }));

        var consideration = new MafAgentAvailable("my-agent");
        var rt = new Runtime(bus, new UserIntent("test"), 0);

        Assert.Equal(1.0, consideration.Evaluate(rt));
    }

    [Fact]
    public void MafAgentAvailable_ReturnsZero_WhenAgentIsUnavailable()
    {
        var agent = new StubAgent("resp");
        var bus = new EventBus();
        bus.Publish(new MafAgentCatalog(new[]
        {
            new MafAgentRegistration("my-agent", agent, IsAvailable: false)
        }));

        var consideration = new MafAgentAvailable("my-agent");
        var rt = new Runtime(bus, new UserIntent("test"), 0);

        Assert.Equal(0.0, consideration.Evaluate(rt));
    }

    [Fact]
    public void MafAgentAvailable_ReturnsZero_WhenNoCatalog()
    {
        var bus = new EventBus();
        var consideration = new MafAgentAvailable("missing-agent");
        var rt = new Runtime(bus, new UserIntent("test"), 0);

        Assert.Equal(0.0, consideration.Evaluate(rt));
    }

    [Fact]
    public void HasMafAgentResult_ReturnsOne_WhenResultExists()
    {
        var bus = new EventBus();
        bus.Publish(new MafAgentResult(
            "research",
            new AgentResponse(new ChatMessage(ChatRole.Assistant, "result")),
            DateTimeOffset.UtcNow));

        var consideration = new HasMafAgentResult("research");
        var rt = new Runtime(bus, new UserIntent("test"), 0);

        Assert.Equal(1.0, consideration.Evaluate(rt));
    }

    [Fact]
    public void HasMafAgentResult_ReturnsZero_WhenNoResult()
    {
        var bus = new EventBus();
        var consideration = new HasMafAgentResult("research");
        var rt = new Runtime(bus, new UserIntent("test"), 0);

        Assert.Equal(0.0, consideration.Evaluate(rt));
    }

    [Fact]
    public void HasMafAgentResult_Inverted_ReturnsOne_WhenNoResult()
    {
        var bus = new EventBus();
        var consideration = new HasMafAgentResult("research", invert: true);
        var rt = new Runtime(bus, new UserIntent("test"), 0);

        Assert.Equal(1.0, consideration.Evaluate(rt));
    }

    [Fact]
    public void HasMafAgentResult_AnyAgent_ReturnsOne_WhenAnyResultExists()
    {
        var bus = new EventBus();
        bus.Publish(new MafAgentResult(
            "some-agent",
            new AgentResponse(new ChatMessage(ChatRole.Assistant, "data")),
            DateTimeOffset.UtcNow));

        var consideration = new HasMafAgentResult(); // no specific agent
        var rt = new Runtime(bus, new UserIntent("test"), 0);

        Assert.Equal(1.0, consideration.Evaluate(rt));
    }

    // ─── RequiresMafAgent (Eligibility) ──────────────────────────

    [Fact]
    public void RequiresMafAgent_ReturnsTrue_WhenAgentAvailable()
    {
        var agent = new StubAgent("resp");
        var bus = new EventBus();
        bus.Publish(new MafAgentCatalog(new[]
        {
            new MafAgentRegistration("required-agent", agent, IsAvailable: true)
        }));

        var eligibility = new RequiresMafAgent("required-agent");
        var rt = new Runtime(bus, new UserIntent("test"), 0);

        Assert.True(eligibility.IsEligible(rt));
    }

    [Fact]
    public void RequiresMafAgent_ReturnsFalse_WhenAgentUnavailable()
    {
        var agent = new StubAgent("resp");
        var bus = new EventBus();
        bus.Publish(new MafAgentCatalog(new[]
        {
            new MafAgentRegistration("required-agent", agent, IsAvailable: false)
        }));

        var eligibility = new RequiresMafAgent("required-agent");
        var rt = new Runtime(bus, new UserIntent("test"), 0);

        Assert.False(eligibility.IsEligible(rt));
    }

    [Fact]
    public void RequiresMafAgent_ReturnsFalse_WhenNoCatalog()
    {
        var bus = new EventBus();
        var eligibility = new RequiresMafAgent("required-agent");
        var rt = new Runtime(bus, new UserIntent("test"), 0);

        Assert.False(eligibility.IsEligible(rt));
    }

    // ─── MafOrchestratorExtensions ───────────────────────────────

    [Fact]
    public async Task AddMafAgent_IntegratesWithOrchestrator()
    {
        var agent = new StubAgent("orchestrated response");
        var bus = new EventBus();

        var orchestrator = new UtilityAiOrchestrator(bus: bus)
            .AddMafAgentSensor(new MafAgentRegistration("test", agent))
            .AddMafAgent(
                agent: agent,
                agentName: "test",
                considerations: new IConsideration[] { new FixedScore(0.9) });

        await orchestrator.RunAsync(
            new UserIntent("run test"),
            maxTicks: 1,
            CancellationToken.None);

        var result = bus.GetOrDefault<MafAgentResult>();
        Assert.NotNull(result);
        Assert.Equal("test", result.AgentName);
        Assert.Equal("orchestrated response", result.Text);
    }

    [Fact]
    public async Task MultipleAgents_HighestUtilityWins()
    {
        var lowAgent = new StubAgent("low-priority response");
        var highAgent = new StubAgent("high-priority response");
        var bus = new EventBus();

        var orchestrator = new UtilityAiOrchestrator(bus: bus)
            .AddMafAgentSensor(
                new MafAgentRegistration("low", lowAgent),
                new MafAgentRegistration("high", highAgent))
            .AddMafAgent(
                agent: lowAgent,
                agentName: "low",
                considerations: new IConsideration[] { new FixedScore(0.3) })
            .AddMafAgent(
                agent: highAgent,
                agentName: "high",
                considerations: new IConsideration[] { new FixedScore(0.9) });

        await orchestrator.RunAsync(
            new UserIntent("test selection"),
            maxTicks: 1,
            CancellationToken.None);

        var result = bus.GetOrDefault<MafAgentResult>();
        Assert.NotNull(result);
        Assert.Equal("high", result.AgentName);
        Assert.Equal("high-priority response", result.Text);
    }

    // ─── MafTypes ────────────────────────────────────────────────

    [Fact]
    public void MafAgentResult_ExposesTextFromResponse()
    {
        var response = new AgentResponse(
            new ChatMessage(ChatRole.Assistant, "test output"));
        var result = new MafAgentResult("agent1", response, DateTimeOffset.UtcNow);

        Assert.Equal("test output", result.Text);
        Assert.Equal("agent1", result.AgentName);
    }

    [Fact]
    public void MafAgentCatalog_StoresRegistrations()
    {
        var agent = new StubAgent("resp");
        var catalog = new MafAgentCatalog(new[]
        {
            new MafAgentRegistration("a1", agent),
            new MafAgentRegistration("a2", agent, IsAvailable: false)
        });

        Assert.Equal(2, catalog.Agents.Count);
        Assert.True(catalog.Agents[0].IsAvailable);
        Assert.False(catalog.Agents[1].IsAvailable);
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
}
