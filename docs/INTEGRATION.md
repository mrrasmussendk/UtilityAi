# Integration Guide

This guide shows how to integrate the UtilityAI framework with various AI services and external systems.

## Table of Contents

1. [Microsoft Agent Framework (MAF) Integration](#microsoft-agent-framework-maf-integration)
2. [LLM Integration (OpenAI, Anthropic, Azure)](#llm-integration)
3. [EventBus Patterns](#eventbus-patterns)
4. [Multi-Agent Coordination](#multi-agent-coordination)
5. [State Persistence](#state-persistence)
6. [Observability & Metrics](#observability--metrics)

---

## Microsoft Agent Framework (MAF) Integration

The `UtilityAi.Maf` package provides integration between UtilityAI and [Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/) through Azure AI Projects. This enables you to use Azure AI Agents within your UtilityAI proposals.

### Installation

```bash
dotnet add package UtilityAi.Maf
```

### Quick Start

```csharp
using UtilityAi.Maf;
using UtilityAi.Orchestration;
using UtilityAi.Capabilities;

// Create MAF client with Azure credentials
var mafClient = new MafClient("https://your-project.openai.azure.com");

// Use in a capability module
public class MyModule : ICapabilityModule
{
    private readonly MafClient _mafClient;

    public MyModule(MafClient mafClient)
    {
        _mafClient = mafClient;
    }

    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        yield return new Proposal(
            id: "answer-query",
            cons: new[] { /* your considerations */ },
            act: async ct =>
            {
                // Get user query
                var query = rt.Intent.Slots?["query"]?.ToString() ?? "No query";

                // Create agent
                var agent = _mafClient.CreateAgent(
                    name: "assistant",
                    instructions: "You are a helpful assistant."
                );

                var agentsClient = _mafClient.GetAgentsClient();

                // Create thread and run agent
                var thread = agentsClient.Threads.CreateThread();
                agentsClient.Messages.CreateMessage(
                    thread.Value.Id,
                    MessageRole.User,
                    query
                );

                var run = agentsClient.Runs.CreateRun(thread.Value.Id, agent.Id);

                // Wait for completion
                do
                {
                    await Task.Delay(500, ct);
                    run = agentsClient.Runs.GetRun(thread.Value.Id, run.Value.Id);
                }
                while (run.Value.Status == RunStatus.Queued
                    || run.Value.Status == RunStatus.InProgress);

                // Get response
                var messages = agentsClient.Messages.GetMessages(
                    thread.Value.Id,
                    order: ListSortOrder.Ascending
                );

                foreach (var message in messages)
                {
                    if (message.Role.ToString() == "assistant")
                    {
                        foreach (var contentItem in message.ContentItems)
                        {
                            if (contentItem is MessageTextContent textItem)
                            {
                                rt.Bus.Publish(new AgentResponse(textItem.Text));
                                break;
                            }
                        }
                    }
                }

                // Cleanup
                agentsClient.Threads.DeleteThread(thread.Value.Id);
            }
        );
    }
}
```

### Key Components

| Component | Purpose |
|-----------|---------|
| `MafClient` | Wraps Azure AI Projects client for agent creation and management |
| `MafClient.CreateAgent()` | Creates a persistent agent with specified instructions |
| `MafClient.GetAgentsClient()` | Returns the `PersistentAgentsClient` for thread/run operations |

### Architecture

```
UtilityAI (Decision Layer)          Azure AI Agents (Execution Layer)
┌─────────────────────────┐         ┌──────────────────────────────┐
│  Sense → Propose →      │         │   MafClient                  │
│  Score → Select →  ──────────────→│   → CreateAgent()            │
│                    Act   │         │   → Threads.CreateThread()   │
│                          │         │   → Messages.CreateMessage() │
│  EventBus ← Result  ←──────────── │   → Runs.CreateRun()         │
└─────────────────────────┘         └──────────────────────────────┘
```

### Configuration

The `MafClient` supports multiple authentication methods:

```csharp
// Using Azure CLI credentials (default)
var client = new MafClient("https://your-project.openai.azure.com");

// Using custom credentials
var client = new MafClient(
    new Uri("https://your-project.openai.azure.com"),
    new DefaultAzureCredential()
);
```

Set the model deployment name via environment variable:
```bash
export MODEL_DEPLOYMENT_NAME="gpt-4"
```

Or pass it explicitly:
```csharp
var client = new MafClient(
    new Uri("https://your-project.openai.azure.com"),
    new DefaultAzureCredential(),
    modelDeploymentName: "gpt-4"
);
```

### Agent Caching

Agents created via `CreateAgent()` are automatically cached by name, preventing duplicate agent creation:

```csharp
var agent1 = mafClient.CreateAgent("assistant", "Instructions");
var agent2 = mafClient.CreateAgent("assistant", "Different instructions");

// agent1 and agent2 reference the same cached agent
```

### Structured Output with MAF

The `MafRequestExtensions` class provides helper methods to easily configure structured output (JSON Schema) for MAF chat completions, reducing boilerplate code.

#### Using AiRequestBuilder with MAF

```csharp
using UtilityAi.Helpers.OpenAiStructuredOutputHelper;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper.SchemaGenerator;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper.Strategy;
using UtilityAi.Maf;
using Azure.AI.Projects.OpenAI;
using System.Text.Json;

// Define your response model using regular properties (not positional records)
public record MathReasoning
{
    public MathStep[] Steps { get; init; } = Array.Empty<MathStep>();
    public string FinalAnswer { get; init; } = string.Empty;
}

public record MathStep
{
    public string Explanation { get; init; } = string.Empty;
    public string Output { get; init; } = string.Empty;
}

// IMPORTANT: Use RequiredStrategy.AllProperties for strict schema validation
var schemaOptions = new SchemaGeneratorOptions
{
    RequiredStrategy = RequiredStrategy.AllProperties
};

var chatClient = mafClient.GetOpenAiResponseClient();

// Method 1: Simplified approach with automatic deserialization
var reasoning = AiRequestBuilder.Create()
    .WithModel("gpt-4")
    .AddSystem("You are a math expert. Answer using clear step-by-step reasoning.")
    .AddUser("How can I solve 8x + 7 = -23?")
    .WithJsonSchemaFrom<MathReasoning>("math_reasoning", schemaOptions)
    .CompleteAndDeserialize<MathReasoning>(chatClient);

Console.WriteLine($"Final Answer: {reasoning.FinalAnswer}");
foreach (var step in reasoning.Steps)
{
    Console.WriteLine($"- {step.Explanation}: {step.Output}");
}

// Method 2: Manual deserialization with CompleteAzureOpenAiChat
var completion = AiRequestBuilder.Create()
    .WithModel("gpt-4")
    .AddSystem("You are a math expert. Answer using clear step-by-step reasoning.")
    .AddUser("How can I solve 8x + 7 = -23?")
    .WithJsonSchemaFrom<MathReasoning>("math_reasoning", schemaOptions)
    .CompleteAzureOpenAiChat(chatClient);

// Parse the structured response
// The response format is: { "output": [ { "steps": [...], "finalAnswer": "..." } ] }
using JsonDocument structuredJson = JsonDocument.Parse(completion.Content[0].Text);
var reasoningResponse = JsonSerializer.Deserialize<MathReasoning>(
    structuredJson.RootElement.GetProperty("output")[0]);

Console.WriteLine($"Final Answer: {reasoningResponse.FinalAnswer}");

// Method 3: Get options and messages separately for more control
var options = AiRequestBuilder.Create()
    .WithModel("gpt-4")
    .AddSystem("You are a math expert.")
    .AddUser("How can I solve 8x + 7 = -23?")
    .WithJsonSchemaFrom<MathReasoning>("math_reasoning", schemaOptions)
    .ToAzureOpenAiChatOptions(out var messages);

var completion = chatClient.CompleteChat(messages, options);
```

#### Direct Helper Methods

```csharp
using UtilityAi.Maf;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper.SchemaGenerator;
using UtilityAi.Helpers.OpenAiStructuredOutputHelper.Strategy;
using System.Text.Json.Nodes;

// Method 1: Create from .NET type with AllProperties strategy
var schemaOptions = new SchemaGeneratorOptions
{
    RequiredStrategy = RequiredStrategy.AllProperties
};

var options = MafRequestExtensions.CreateStructuredOptions<MathReasoning>(
    schemaName: "math_reasoning",
    options: schemaOptions,
    strict: true
);

// Method 2: Create from JsonObject schema
var schema = new JsonObject
{
    ["type"] = "object",
    ["properties"] = new JsonObject
    {
        ["explanation"] = new JsonObject { ["type"] = "string" },
        ["output"] = new JsonObject { ["type"] = "string" }
    },
    ["required"] = new JsonArray { "explanation", "output" },
    ["additionalProperties"] = false
};

var options = MafRequestExtensions.CreateStructuredOptions(
    schemaName: "math_reasoning",
    schema: schema,
    strict: true
);

// Use with chat completion
var completion = chatClient.CompleteChat(messages, options);
```

#### Benefits

- **Reduced Boilerplate**: No need to manually construct `BinaryData` and `ChatResponseFormat`
- **Type Safety**: Generate schemas directly from .NET types
- **Automatic Deserialization**: Use `CompleteAndDeserialize<T>` to execute and deserialize in one step
- **Reusability**: Leverage existing `AiRequestBuilder` infrastructure
- **Consistency**: Unified approach across OpenAI and MAF integrations
- **Flexible**: Choose between automatic deserialization or manual control over the response parsing

### Full Working Example

See the [`examples/Example.Maf/`](../examples/Example.Maf/) project for a complete working demonstration showing:
- MAF client initialization
- Agent creation within proposals
- Thread and message management
- Response handling
- Proper cleanup
- Structured output usage

### References

- [Azure AI Projects Documentation](https://learn.microsoft.com/en-us/azure/ai-services/agents/)
- [Azure.AI.Projects NuGet Package](https://www.nuget.org/packages/Azure.AI.Projects)
- [Azure.AI.Agents.Persistent NuGet Package](https://www.nuget.org/packages/Azure.AI.Agents.Persistent)
- [Structured Outputs Guide](https://platform.openai.com/docs/guides/structured-outputs)

---

## LLM Integration

The framework doesn't prescribe a specific LLM client. Here's how to integrate with common providers:

### OpenAI Integration

```csharp
using OpenAI;
using OpenAI.Chat;

public class LLMSummarizationModule : ICapabilityModule
{
    private readonly ChatClient _client;

    public LLMSummarizationModule(string apiKey)
    {
        _client = new ChatClient("gpt-4", apiKey);
    }

    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        // Only propose if we have content to summarize
        var content = rt.Bus.GetOrDefault<ContentToSummarize>();
        if (content == null) yield break;

        yield return new Proposal(
            id: "summarize.content",
            cons: new[]
            {
                new HasFact<ContentToSummarize>(),
                // Don't summarize if already done
                new HasFact<Summary>(invert: true)
            },
            act: async ct =>
            {
                // Build conversation history from EventBus
                var history = rt.Bus.GetHistory<UserMessage>(maxItems: 5);
                var messages = history
                    .Select(e => new UserChatMessage(e.Value.Text))
                    .Cast<ChatMessage>()
                    .Prepend(new SystemChatMessage("You are a helpful summarization assistant."))
                    .ToList();

                messages.Add(new UserChatMessage($"Summarize this: {content.Text}"));

                var response = await _client.CompleteChatAsync(messages, cancellationToken: ct);

                // Publish result back to EventBus
                rt.Bus.Publish(new Summary(response.Value.Content[0].Text));
            }
        );
    }
}
```

### Azure OpenAI

```csharp
using Azure;
using Azure.AI.OpenAI;

public class AzureOpenAIModule : ICapabilityModule
{
    private readonly AzureOpenAIClient _client;
    private readonly string _deploymentName;

    public AzureOpenAIModule(string endpoint, string apiKey, string deploymentName)
    {
        _client = new AzureOpenAIClient(
            new Uri(endpoint),
            new AzureKeyCredential(apiKey));
        _deploymentName = deploymentName;
    }

    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var userMsg = rt.Bus.GetOrDefault<UserMessage>();
        if (userMsg == null) yield break;

        yield return new Proposal(
            id: "respond.user",
            cons: new[] { new HasFact<UserMessage>() },
            act: async ct =>
            {
                var options = new ChatCompletionsOptions
                {
                    DeploymentName = _deploymentName,
                    Messages =
                    {
                        new ChatRequestSystemMessage("You are a helpful assistant."),
                        new ChatRequestUserMessage(userMsg.Text)
                    }
                };

                var response = await _client.GetChatCompletionsAsync(options, ct);
                var reply = response.Value.Choices[0].Message.Content;

                rt.Bus.Publish(new AssistantMessage(reply));
            }
        );
    }
}
```

### Anthropic Claude

```csharp
using Anthropic.SDK;

public class ClaudeModule : ICapabilityModule
{
    private readonly AnthropicClient _client;

    public ClaudeModule(string apiKey)
    {
        _client = new AnthropicClient(apiKey);
    }

    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var userMsg = rt.Bus.GetOrDefault<UserMessage>();
        if (userMsg == null) yield break;

        yield return new Proposal(
            id: "claude.respond",
            cons: new[] { new HasFact<UserMessage>() },
            act: async ct =>
            {
                var messages = new List<Message>
                {
                    new Message { Role = "user", Content = userMsg.Text }
                };

                var response = await _client.Messages.CreateAsync(
                    model: "claude-3-5-sonnet-20241022",
                    maxTokens: 1024,
                    messages: messages,
                    cancellationToken: ct
                );

                rt.Bus.Publish(new AssistantMessage(response.Content[0].Text));
            }
        );
    }
}
```

---

## EventBus Patterns

### Pattern: Conversation History Management

```csharp
public class ConversationSensor : ISensor
{
    public Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        // Get recent message history
        var messages = rt.Bus.GetHistory<UserMessage>(maxItems: 10);

        // Derive conversation metadata
        var messageCount = messages.Count;
        var firstMessage = messages.FirstOrDefault()?.Timestamp;
        var duration = firstMessage.HasValue
            ? DateTimeOffset.UtcNow - firstMessage.Value
            : TimeSpan.Zero;

        // Publish derived facts
        rt.Bus.Publish(new ConversationMetadata(
            MessageCount: messageCount,
            Duration: duration,
            IsLongConversation: messageCount > 20
        ));

        return Task.CompletedTask;
    }
}

// Use in considerations
new CurveSignal<ConversationMetadata>(
    selector: m => m.MessageCount,
    curve: Curves.Logistic(k: 0.1, x0: 10),
    inputDomain: (0, 50)
)
```

### Pattern: Event Reactions (Side Effects)

```csharp
public class MetricsSensor : ISensor
{
    private readonly IDisposable _taskCompletedSub;
    private readonly IMetricsClient _metrics;

    public MetricsSensor(EventBus bus, IMetricsClient metrics)
    {
        _metrics = metrics;

        // React to events without blocking orchestration
        _taskCompletedSub = bus.Subscribe<TaskCompleted>(task =>
        {
            _metrics.RecordTaskCompletion(task.Id, task.Duration);
        });
    }

    public Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        // Can also publish periodic metrics
        var allTasks = rt.Bus.GetHistory<TaskCompleted>();
        var avgDuration = allTasks.Any()
            ? allTasks.Average(t => t.Value.Duration.TotalSeconds)
            : 0;

        rt.Bus.Publish(new PerformanceMetrics(avgDuration));
        return Task.CompletedTask;
    }

    public void Dispose() => _taskCompletedSub?.Dispose();
}
```

### Pattern: Scoped State (Multi-Agent)

```csharp
// Root bus for shared facts
var rootBus = new EventBus();
rootBus.Publish(new GlobalConfig("prod"));

// Agent-specific buses
var agent1Bus = rootBus.CreateScope("agent-1");
var agent2Bus = rootBus.CreateScope("agent-2");

// Agent 1 orchestrator
var orch1 = new UtilityAiOrchestrator(bus: agent1Bus)
    .AddSensor(new Agent1Sensor())
    .AddModule(new Agent1Module());

// Agent 2 orchestrator
var orch2 = new UtilityAiOrchestrator(bus: agent2Bus)
    .AddSensor(new Agent2Sensor())
    .AddModule(new Agent2Module());

// Agents can read global config
agent1Bus.TryGetWithFallback<GlobalConfig>(out var config); // ✅ Finds it in parent

// But have isolated local state
agent1Bus.Publish(new AgentState("busy"));
agent2Bus.TryGet<AgentState>(out var state); // ❌ Not found (isolated)
```

---

## Multi-Agent Coordination

### Pattern: Agent Handoffs

```csharp
public record HandoffRequest(string FromAgent, string ToAgent, object Context);

public class HandoffModule : ICapabilityModule
{
    private readonly string _currentAgent;

    public HandoffModule(string currentAgent)
    {
        _currentAgent = currentAgent;
    }

    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var handoff = rt.Bus.GetOrDefault<HandoffRequest>();
        if (handoff?.ToAgent != _currentAgent)
            yield break;

        yield return new Proposal(
            id: $"handoff.accept.{handoff.FromAgent}",
            cons: new[]
            {
                new HasFact<HandoffRequest>(),
                // Only accept if we're not busy
                new HasFact<AgentBusyStatus>(invert: true)
            },
            act: async ct =>
            {
                rt.Bus.Publish(new AgentBusyStatus(true));
                rt.Bus.Publish(new HandoffAccepted(_currentAgent, handoff.Context));

                // Clear the handoff request
                rt.Bus.Clear<HandoffRequest>();

                await Task.CompletedTask;
            }
        );
    }
}
```

### Pattern: Coordinated Multi-Agent Execution

```csharp
public class CoordinatorOrchestrator
{
    private readonly EventBus _rootBus;
    private readonly Dictionary<string, UtilityAiOrchestrator> _agents = new();

    public CoordinatorOrchestrator()
    {
        _rootBus = new EventBus();

        // Create specialized agents
        _agents["researcher"] = CreateResearchAgent(_rootBus.CreateScope("researcher"));
        _agents["writer"] = CreateWriterAgent(_rootBus.CreateScope("writer"));
        _agents["reviewer"] = CreateReviewerAgent(_rootBus.CreateScope("reviewer"));
    }

    public async Task RunAsync(CancellationToken ct)
    {
        // Run agents in sequence or parallel as needed
        await _agents["researcher"].RunAsync(maxTicks: 5, ct);

        var researchComplete = _rootBus.GetOrDefault<ResearchResults>();
        if (researchComplete != null)
        {
            await _agents["writer"].RunAsync(maxTicks: 5, ct);
        }

        var draftComplete = _rootBus.GetOrDefault<DraftArticle>();
        if (draftComplete != null)
        {
            await _agents["reviewer"].RunAsync(maxTicks: 3, ct);
        }
    }

    private UtilityAiOrchestrator CreateResearchAgent(EventBus bus) =>
        new UtilityAiOrchestrator(bus: bus)
            .AddModule(new WebSearchModule())
            .AddModule(new FactExtractionModule());

    // ... similar for other agents
}
```

---

## State Persistence

The EventBus doesn't persist by default. Here's how to add persistence:

### Pattern: Session Serialization

```csharp
public class PersistentEventBus
{
    private readonly EventBus _bus;
    private readonly string _sessionId;
    private readonly IStateStore _store;

    public PersistentEventBus(string sessionId, IStateStore store)
    {
        _sessionId = sessionId;
        _store = store;
        _bus = new EventBus();

        // Load previous state if exists
        LoadState().Wait();
    }

    public EventBus Bus => _bus;

    private async Task LoadState()
    {
        var state = await _store.LoadAsync(_sessionId);
        if (state != null)
        {
            foreach (var (type, value) in state.Facts)
            {
                // Deserialize and publish each fact
                var method = typeof(EventBus).GetMethod("Publish");
                var generic = method.MakeGenericMethod(type);
                generic.Invoke(_bus, new[] { value });
            }
        }
    }

    public async Task SaveState()
    {
        // Capture current bus state
        var snapshot = new SessionSnapshot
        {
            SessionId = _sessionId,
            Timestamp = DateTimeOffset.UtcNow,
            Facts = ExtractAllFacts(_bus)
        };

        await _store.SaveAsync(_sessionId, snapshot);
    }

    private Dictionary<Type, object> ExtractAllFacts(EventBus bus)
    {
        // Use reflection or maintain a registry of fact types
        // This is framework-user responsibility since EventBus is type-agnostic
        var facts = new Dictionary<Type, object>();

        // Example for known types:
        if (bus.TryGet<UserMessage>(out var msg))
            facts[typeof(UserMessage)] = msg;

        return facts;
    }
}

// Usage with orchestration
var persistentBus = new PersistentEventBus("session-123", new RedisStateStore());
var orchestrator = new UtilityAiOrchestrator(bus: persistentBus.Bus);

await orchestrator.RunAsync(maxTicks: 10, ct);
await persistentBus.SaveState(); // Persist after each run
```

---

## Observability & Metrics

### Pattern: Structured Logging Sink

```csharp
using Microsoft.Extensions.Logging;

public class LoggingSink : IOrchestrationSink
{
    private readonly ILogger _logger;

    public LoggingSink(ILogger logger)
    {
        _logger = logger;
    }

    public void OnTickStart(Runtime rt)
    {
        _logger.LogDebug("Tick {Tick} starting", rt.Tick);
    }

    public void OnScored(Runtime rt, IReadOnlyList<(Proposal Proposal, double Utility)> scored)
    {
        foreach (var (proposal, utility) in scored.Take(5))
        {
            _logger.LogInformation(
                "Tick {Tick}: Proposal {ProposalId} scored {Utility:F3}",
                rt.Tick, proposal.Id, utility);
        }
    }

    public void OnChosen(Runtime rt, Proposal chosen, double utility)
    {
        _logger.LogInformation(
            "Tick {Tick}: Chose {ProposalId} with utility {Utility:F3}",
            rt.Tick, chosen.Id, utility);
    }

    public void OnActed(Runtime rt, Proposal chosen)
    {
        _logger.LogInformation(
            "Tick {Tick}: Executed {ProposalId}",
            rt.Tick, chosen.Id);
    }

    public void OnStopped(Runtime rt, OrchestrationStopReason reason)
    {
        _logger.LogInformation(
            "Orchestration stopped at tick {Tick}: {Reason}",
            rt.Tick, reason);
    }
}
```

### Pattern: Metrics Collection

```csharp
public class MetricsSink : IOrchestrationSink
{
    private readonly IMeterFactory _meterFactory;
    private readonly Histogram<double> _utilityHistogram;
    private readonly Counter<int> _proposalCounter;
    private readonly Counter<int> _actionCounter;

    public MetricsSink(IMeterFactory meterFactory)
    {
        _meterFactory = meterFactory;
        var meter = meterFactory.Create("UtilityAI.Orchestration");

        _utilityHistogram = meter.CreateHistogram<double>(
            "utility_scores",
            description: "Distribution of proposal utility scores");

        _proposalCounter = meter.CreateCounter<int>(
            "proposals_generated",
            description: "Total proposals generated");

        _actionCounter = meter.CreateCounter<int>(
            "actions_executed",
            description: "Total actions executed");
    }

    public void OnScored(Runtime rt, IReadOnlyList<(Proposal Proposal, double Utility)> scored)
    {
        _proposalCounter.Add(scored.Count);

        foreach (var (proposal, utility) in scored)
        {
            _utilityHistogram.Record(utility,
                new KeyValuePair<string, object?>("proposal_id", proposal.Id));
        }
    }

    public void OnActed(Runtime rt, Proposal chosen)
    {
        _actionCounter.Add(1,
            new KeyValuePair<string, object?>("proposal_id", chosen.Id));
    }

    // ... other methods
}
```

### Pattern: Testing Assertions

```csharp
public class TestingSink : IOrchestrationSink
{
    public List<string> ExecutedProposals { get; } = new();
    public List<(string ProposalId, double Utility)> ScoredProposals { get; } = new();
    public OrchestrationStopReason? StopReason { get; private set; }

    public void OnChosen(Runtime rt, Proposal chosen, double utility)
    {
        ExecutedProposals.Add(chosen.Id);
    }

    public void OnScored(Runtime rt, IReadOnlyList<(Proposal Proposal, double Utility)> scored)
    {
        ScoredProposals.AddRange(scored.Select(s => (s.Proposal.Id, s.Utility)));
    }

    public void OnStopped(Runtime rt, OrchestrationStopReason reason)
    {
        StopReason = reason;
    }

    // ... other methods
}

// In tests
[Fact]
public async Task Orchestrator_ExecutesHighestUtilityProposal()
{
    var bus = new EventBus();
    bus.Publish(new UserMessage("test"));

    var sink = new TestingSink();
    var orchestrator = new UtilityAiOrchestrator(bus: bus)
        .AddModule(new TestModule());

    await orchestrator.RunAsync(maxTicks: 1, CancellationToken.None, sink);

    Assert.Single(sink.ExecutedProposals);
    Assert.Equal("test.action", sink.ExecutedProposals[0]);
}
```

---

## Best Practices

### 1. Keep Sensors Lightweight
- Cache expensive computations as EventBus facts
- Use subscriptions for reactions, not polling

### 2. Design Stateless Modules
- All state → EventBus
- Makes testing and reasoning easier

### 3. Use Eligibilities for Hard Requirements
- Filter proposals early with `IEligibility`
- Save computation on ineligible proposals

### 4. Leverage EventBus History
- Build LLM context from message history
- Track state transitions over time

### 5. Compose Sinks for Observability
```csharp
var sink = new CompositeSink(
    new LoggingSink(logger),
    new MetricsSink(meterFactory),
    new TestingSink()
);
```

### 6. Test with Mocked EventBus State
```csharp
[Fact]
public void Module_ProposesWhenFactExists()
{
    var bus = new EventBus();
    bus.Publish(new RequiredFact());

    var module = new MyModule();
    var proposals = module.Propose(new Runtime(bus, 0));

    Assert.NotEmpty(proposals);
}
```

---

## Next Steps

- See [ARCHITECTURE.md](./ARCHITECTURE.md) for framework internals
- See [Examples](../examples/Example/) for complete working code
- Check the [README](../src/UtilityAi/README.md) for a quick overview
