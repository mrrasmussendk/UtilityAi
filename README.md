# 🧠 UtilityAI Framework (.NET 8)

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Tests](https://img.shields.io/badge/tests-203%20passing-brightgreen)](./Tests/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A lightweight, modular framework for building AI agent orchestration systems using classic **Utility AI** decision-making patterns. The framework scores candidate actions each tick and executes the highest-utility option based on current context—no hardcoded workflows required.

> **Don't script workflows — evaluate them.**

---

## ✨ Features

- 🎯 **Utility-Based Decision Making** - Actions compete based on dynamic scoring
- 🧠 **LLM Intent Interpretation with Rich Parameters** (NEW!) - Proposals declare what parameters they need, LLMs provide structured data, utility system scores automatically
- 🎨 **Self-Documenting Intent Matching** (NEW!) - Framework exposes capability metadata for LLM prompt generation - closed loop between code and AI
- 🏷️ **Attribute-Based Registration** - Java-style annotations for declarative module configuration
- 🔗 **Microsoft Agent Framework (MAF) Integration** - Utility-based orchestration of MAF agents
- 📝 **Event History** - Access timestamped event history for LLM conversation context
- 🔔 **Type-Safe Subscriptions** - React to events with callbacks
- 🏗️ **Scoped State** - Isolate multi-agent state while sharing global facts
- 🔌 **Pluggable Architecture** - Sensors, modules, and considerations are fully extensible
- 📊 **Built-in Observability** - Sinks for logging, metrics, and testing
- 🧪 **Well Tested** - 203 comprehensive tests covering all core functionality
- 💾 **Memory Management** - Two-tier memory with automatic archival from EventBus to long-term storage
- 📚 **Production Ready** - Thread-safe, documented, with integration guides

---

## 🚀 Quick Start

### Installation

```bash
# Clone or download the repository
git clone https://github.com/yourusername/UtilityAi.git
cd UtilityAi

# Build and test
dotnet build
dotnet test
```

### Basic Example (Manual Registration)

```csharp
using UtilityAi.Orchestration;
using UtilityAi.Utils;

// Create the event bus (blackboard for facts)
var bus = new EventBus();

// Configure orchestrator with sensors and modules
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddSensor(new MyEnvironmentSensor())
    .AddModule(new MyCapabilityModule());

// Run the orchestration loop
var intent = UserIntent.ForGoal("my-goal");
await orchestrator.RunAsync(intent, maxTicks: 10, CancellationToken.None);
```

### 🆕 Attribute-Based Registration (Java-Style Annotations)

```csharp
using UtilityAi.Capabilities;

// Define modules with attributes
[Capability(Priority = 100, Domain = "validation")]
[RequiresFact<TaskQueue>]
[ActiveWhen("priority_mode", "urgent", "balanced")]
public class ValidationModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt) { /* ... */ }
}

// Auto-discover and register
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddSensor(new MySensor())
    .DiscoverCapabilities(Assembly.GetExecutingAssembly());
```

**Benefits:** Declarative dependencies • Conditional activation • Automatic ordering • Reduced boilerplate

See the [Example](./Example/) project for a complete demo comparing both approaches.

### 🆕 Microsoft Agent Framework (MAF) Integration

Use UtilityAI to orchestrate [MAF agents](https://learn.microsoft.com/en-us/agent-framework/) with utility-based decision-making:

```csharp
using Microsoft.Agents.AI;
using UtilityAi.Maf;
using UtilityAi.Orchestration;

// Register MAF agents with the orchestrator
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddMafAgentSensor(
        new MafAgentRegistration("research", researchAgent),
        new MafAgentRegistration("writer", writerAgent))
    .AddMafAgent(researchAgent, "research",
        considerations: new IConsideration[] { new MafAgentAvailable("research") })
    .AddMafAgent(writerAgent, "writer",
        considerations: new IConsideration[] { new HasMafAgentResult("research") });

await orchestrator.RunAsync(intent, maxTicks: 5, CancellationToken.None);
```

**Benefits:** Utility scoring selects agents • Multi-agent workflows emerge naturally • Agent results flow via EventBus

See the [Example.Maf](./Example.Maf/) project and [MAF Integration Guide](./docs/INTEGRATION.md#microsoft-agent-framework-maf-integration) for details.

### 🆕 LLM Intent Interpretation with Rich Parameters

Bootstrap your agent with intelligent intent analysis - let the LLM interpret user messages into structured facts with **rich parameters**, then let proposals score based on those parameters:

```csharp
using UtilityAi.Sensor.LLM;
using UtilityAi.Consideration.Intent;

// 1. Create LLM client adapter
public class YourLlmAdapter : ILlmClient
{
    private readonly ILanguageModel _llm;

    public async Task<string> GenerateAsync(string prompt, CancellationToken ct)
    {
        return await _llm.GenerateAsync(prompt, ct);
    }
}

// 2. Add intent sensor
var llmClient = new YourLlmAdapter();
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddSensor(LlmIntentSensor.ForMessageType<UserMessage>(
        llmClient,
        msg => msg.Text,
        includeCapabilities: true  // Include proposal metadata in LLM prompt
    ))
    .AddModule(new TicketModule());

// 3. Proposals declare intent patterns AND parameters they need
public class TicketModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        // Declare what intent this handles and what parameters it needs
        yield return ProposalHelper.For("ticket.create.priority")
            .WithDescription("Create high-priority support ticket")
            .ForIntent("ticket.create", IntentMatchType.Exact)

            // Declare parameters with types, ranges, and descriptions
            .ScoreByIntentParameter(
                paramName: "urgency",
                curve: x => Math.Pow(x, 3),  // Cubic - heavily favor high urgency
                range: (0, 1),
                description: "How urgent the issue is (0=low, 1=critical)")

            .UsesIntentParameter(
                name: "customer_tier",
                type: "string",
                description: "Customer subscription level",
                allowedValues: new[] { "free", "premium", "enterprise" })

            .WithConsideration(new SignalConsideration<IntentAnalysis>(
                "customer-tier-bonus",
                intent => intent.GetParameter<string>("customer_tier") switch
                {
                    "enterprise" => 1.0,
                    "premium" => 0.85,
                    _ => 0.65
                },
                x => x,
                (0, 1)))

            .WithAction(async ct => await CreatePriorityTicket(rt, ct));
    }
}

// 4. LLM sees metadata and provides structured parameters
// GetCapabilitiesInfo() exposes what parameters each proposal needs:
var capabilities = orchestrator.GetCapabilitiesInfo(rt);
// Use this to build LLM prompt: "Provide these parameters: urgency (0-1), customer_tier (free/premium/enterprise)..."

// 5. LLM returns IntentAnalysis with parameters
var analysis = new IntentAnalysis(
    Intent: "ticket.create",
    Entities: new Dictionary<string, object> { ["email"] = "user@example.com" },
    Confidence: 0.95,
    Parameters: new Dictionary<string, object>
    {
        ["urgency"] = 0.9,              // High urgency
        ["customer_tier"] = "enterprise" // Premium customer
    }
);

// 6. Proposals automatically score based on parameters!
```

**Flow:**
1. Proposals declare intent patterns + parameters →
2. Framework exposes metadata via `GetCapabilitiesInfo()` →
3. LLM prompt includes required parameters →
4. LLM provides structured intent with parameters →
5. Proposals score based on parameters →
6. Best action wins!

**Benefits:**
- 🎯 **Self-Documenting**: Proposals declare what they need
- 🔄 **Closed Loop**: Framework tells LLM what to provide
- 🎨 **Flexible Scoring**: Different proposals use different parameters
- 📊 **Type-Safe**: `GetParameter<T>()` with compile-time checking
- 🧩 **Extensible**: Parameters dictionary holds any structure

**Advanced Example: Multiple Proposals, Different Parameters**

```csharp
public class MultiIntentModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        // High urgency issues → immediate handling
        yield return ProposalHelper.For("ticket.create.urgent")
            .ForIntent("ticket.create")
            .ScoreByIntentParameter("urgency", x => Math.Pow(x, 3), (0, 1))
            .WithPrior(0.9)
            .WithAction(async ct => await HandleUrgent(rt, ct));

        // Low urgency → routine handling
        yield return ProposalHelper.For("ticket.create.routine")
            .ForIntent("ticket.create")
            .ScoreByIntentParameter("urgency", x => 1.0 - x, (0, 1)) // Inverted!
            .WithPrior(0.6)
            .WithAction(async ct => await HandleRoutine(rt, ct));

        // Query tickets (different intent, different parameters)
        yield return ProposalHelper.For("ticket.query")
            .ForIntent("ticket.query")
            .UsesIntentParameter("has_ticket_id", "boolean")
            .WithConsideration(new SignalConsideration<IntentAnalysis>(
                "has-id",
                intent => intent.GetParameter<bool>("has_ticket_id") ? 1.0 : 0.2,
                x => x,
                (0, 1)))
            .WithAction(async ct => await QueryTicket(rt, ct));
    }
}
```

**The LLM sees all three proposals and knows:**
- `ticket.create` needs `urgency` parameter (0-1 number)
- `ticket.query` needs `has_ticket_id` parameter (boolean)

**See:** [Intent-Based Agent Example](./examples/Example/IntentBasedAgent/) for a complete working demo

---

## 📚 Documentation

### Getting Started
- **[Architecture Guide](./docs/ARCHITECTURE.md)** - Understanding the framework design and patterns
- **[Integration Guide](./docs/INTEGRATION.md)** - Connect to MAF, OpenAI, Anthropic, Azure AI, and more
- **[Example Project](./Example/)** - Complete working task management system
- **[MAF Example](./Example.Maf/)** - Microsoft Agent Framework integration demo

### Core Concepts
- **[EventBus Patterns](./docs/ARCHITECTURE.md#1-eventbus-blackboard-pattern)** - History, subscriptions, and scoping
- **[Sensors](./docs/ARCHITECTURE.md#3-sensors-isensor)** - Observing and publishing facts
- **[Capability Modules](./docs/ARCHITECTURE.md#4-capability-modules-icapabilitymodule)** - Proposing actions
- **[Considerations](./docs/ARCHITECTURE.md#6-considerations-iconsideration)** - Scoring proposals
- **[Observability](./docs/ARCHITECTURE.md#8-observability-iorchestrationSink)** - Monitoring and debugging

### Advanced Topics
- **[Memory Management](./src/UtilityAi/Memory/README.md)** - Long-term memory storage, querying, and automatic archival
- **[Multi-Agent Coordination](./docs/INTEGRATION.md#multi-agent-coordination)** - Patterns for agent collaboration
- **[State Persistence](./docs/INTEGRATION.md#state-persistence)** - Saving and restoring orchestration state
- **[LLM Integration](./docs/INTEGRATION.md#llm-integration)** - Examples for OpenAI, Anthropic, Azure
- **[Testing Patterns](./docs/INTEGRATION.md#pattern-testing-assertions)** - How to test your orchestration logic

---

## 🏗️ Architecture Overview

The framework follows a **Sense → Propose → Score → Act** loop:

```
┌─────────────────────────────────────────────────────┐
│                   EventBus                          │
│         (Shared state with history & scoping)       │
└──────────▲──────────────────────┬──────────────────┘
           │                      │
    ┌──────┴──────┐        ┌─────▼──────┐
    │   Sensors   │        │   Modules  │
    │  (Observe)  │        │  (Propose) │
    └─────────────┘        └─────┬──────┘
                                 │
                          ┌──────▼──────┐
                          │  Proposals  │
                          │   + Score   │
                          └──────┬──────┘
                                 │
                          ┌──────▼──────┐
                          │Orchestrator │
                          │ Select Best │
                          │    & Act    │
                          └─────────────┘
```

### Key Components

| Component | Purpose | Extensibility |
|-----------|---------|---------------|
| **EventBus** | Central state container with history, subscriptions, and scoping | Use as-is or wrap for persistence |
| **ISensor** | Observe environment and publish facts | Implement for your data sources |
| **ICapabilityModule** | Propose candidate actions | Implement + use attributes for auto-discovery |
| **IConsideration** | Score proposals (0.0-1.0) | Implement custom scoring logic |
| **IOrchestrationSink** | Observe orchestration events | Implement for logging/metrics |

---

## 💡 Core Features in Detail

### 1️⃣ Intent-Based Orchestration (NEW!)

**The Problem:** When building LLM-powered agents, how does the LLM know what parameters each proposal needs to make intelligent scoring decisions?

**The Solution:** Proposals declare their intent requirements and parameters, the framework exposes this metadata, and the LLM provides structured data that proposals use for scoring.

#### How It Works

```csharp
// Step 1: Proposals declare what they handle
yield return ProposalHelper.For("ticket.create.priority")
    .WithDescription("Create high-priority support ticket")
    .ForIntent("ticket.create", IntentMatchType.Exact)  // What intent pattern

    // Declare parameters with metadata
    .ScoreByIntentParameter("urgency", x => Math.Pow(x, 3), (0, 1),
        description: "Issue urgency level")
    .UsesIntentParameter("customer_tier", "string",
        allowedValues: new[] { "free", "premium", "enterprise" });

// Step 2: Framework exposes metadata
var capabilities = orchestrator.GetCapabilitiesInfo(rt);
foreach (var proposal in capabilities.SelectMany(c => c.PotentialActions))
{
    Console.WriteLine($"{proposal.ProposalId}: {proposal.IntentMatch?.Pattern}");
    foreach (var param in proposal.IntentParameters ?? [])
    {
        Console.WriteLine($"  - {param.ParameterName} ({param.Type}): {param.Description}");
    }
}
// Output:
// ticket.create.priority: ticket.create
//   - urgency (number): Issue urgency level
//   - customer_tier (string): Customer subscription level

// Step 3: Build LLM prompt with metadata
var promptBuilder = new StringBuilder();
promptBuilder.AppendLine("Analyze the user's message and provide:");
foreach (var proposal in capabilities.SelectMany(c => c.PotentialActions))
{
    if (proposal.IntentMatch == null) continue;
    promptBuilder.AppendLine($"For intent '{proposal.IntentMatch.Pattern}':");
    foreach (var param in proposal.IntentParameters ?? [])
    {
        promptBuilder.AppendLine($"  - {param.ParameterName}: {param.Description}");
        if (param.Range != null)
            promptBuilder.AppendLine($"    Range: {param.Range.Min} to {param.Range.Max}");
    }
}

// Step 4: LLM returns structured intent with parameters
var intent = new IntentAnalysis(
    Intent: "ticket.create",
    Entities: new Dictionary<string, object> { ["email"] = "user@example.com" },
    Confidence: 0.95,
    Parameters: new Dictionary<string, object>
    {
        ["urgency"] = 0.9,
        ["customer_tier"] = "enterprise"
    }
);

// Step 5: Proposals score automatically based on parameters
// urgency=0.9 → Math.Pow(0.9, 3) = 0.729
// customer_tier="enterprise" → 1.0 bonus
// Final utility: high!
```

#### Key APIs

**Declaring Intent Patterns:**
```csharp
.ForIntent("ticket.create", IntentMatchType.Exact)     // Exact match
.ForIntent("ticket.*", IntentMatchType.Prefix)         // Prefix match
.ForIntent(".*ticket.*", IntentMatchType.Regex)        // Regex match
```

**Declaring Parameters:**
```csharp
// Declare AND add consideration (shorthand)
.ScoreByIntentParameter(
    paramName: "urgency",
    curve: x => x * x,
    range: (0, 1),
    description: "How urgent is this")

// Declare only (use in custom consideration)
.UsesIntentParameter(
    name: "customer_tier",
    type: "string",
    allowedValues: new[] { "free", "premium", "enterprise" })
```

**Accessing Parameters:**
```csharp
var intent = rt.Bus.GetOrDefault<IntentAnalysis>();

// Type-safe access
double urgency = intent.GetParameter<double>("urgency", defaultValue: 0.5);
string tier = intent.GetParameter<string>("customer_tier", "free");
bool flag = intent.GetParameter<bool>("requires_human", false);

// Convenience methods
if (intent.ParameterAbove("urgency", 0.9))
    Console.WriteLine("Critical issue!");
```

#### Real-World Example: Support Ticket Bot

```csharp
public class TicketModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        // Critical issues → immediate escalation
        yield return ProposalHelper.For("ticket.escalate")
            .WithDescription("Escalate critical issues to human agent")
            .ForIntent("ticket.*", IntentMatchType.Prefix)
            .ScoreByIntentParameter("urgency", x => x > 0.9 ? 1.0 : 0.0, (0, 1),
                "Escalate when urgency > 0.9")
            .UsesIntentParameter("requires_human", "boolean")
            .WithConsideration(new SignalConsideration<IntentAnalysis>(
                "needs-human",
                intent => intent.GetParameter<bool>("requires_human") ||
                         intent.ParameterAbove("urgency", 0.95) ? 1.0 : 0.1,
                x => x, (0, 1)))
            .WithAction(async ct => await EscalateToHuman(rt, ct));

        // Enterprise customers → priority handling
        yield return ProposalHelper.For("ticket.create.enterprise")
            .ForIntent("ticket.create")
            .ScoreByIntentParameter("urgency", x => x * x, (0, 1))
            .WithConsideration(new SignalConsideration<IntentAnalysis>(
                "is-enterprise",
                intent => intent.GetParameter<string>("customer_tier") == "enterprise" ? 1.0 : 0.0,
                x => x, (0, 1)))
            .WithAction(async ct => await CreatePriorityTicket(rt, ct));

        // Routine tickets → standard queue
        yield return ProposalHelper.For("ticket.create.routine")
            .ForIntent("ticket.create")
            .ScoreByIntentParameter("urgency", x => 1.0 - x, (0, 1), "Inverted - prefer low urgency")
            .WithPrior(0.5)
            .WithAction(async ct => await CreateRoutineTicket(rt, ct));
    }
}
```

**User Message:**
```
"Our production API is down! We're an enterprise customer and losing revenue."
```

**LLM Analysis (using capability metadata):**
```json
{
  "intent": "ticket.create",
  "confidence": 0.98,
  "entities": {
    "issue_type": "api_outage",
    "environment": "production"
  },
  "parameters": {
    "urgency": 1.0,
    "customer_tier": "enterprise",
    "requires_human": false
  }
}
```

**Scoring Results:**
- `ticket.escalate`: utility = 0.95 (urgency=1.0, threshold met)
- `ticket.create.enterprise`: utility = 0.92 (urgency=1.0, is enterprise)
- `ticket.create.routine`: utility = 0.1 (inverted urgency = 0.0)

**Winner:** `ticket.escalate` (highest utility) ✅

#### Benefits

✅ **Self-Documenting** - Proposals declare what they need, no separate documentation
✅ **Closed Loop** - Framework tells LLM what to provide
✅ **Flexible** - Different proposals use different parameters for same intent
✅ **Type-Safe** - Compile-time checking with `GetParameter<T>()`
✅ **Extensible** - Parameters dictionary holds any structure
✅ **Testable** - Mock IntentAnalysis with specific parameters

[See complete example →](./examples/Example/IntentBasedAgent/)

---

### 2️⃣ Event History

Access timestamped history of published facts - perfect for building LLM conversation context:

```csharp
var bus = new EventBus();
bus.Publish(new UserMessage("Hello"));
bus.Publish(new UserMessage("How are you?"));

// Get recent message history
var history = bus.GetHistory<UserMessage>(maxItems: 10);
foreach (var evt in history)
    Console.WriteLine($"{evt.Timestamp}: {evt.Value.Text}");
```

**Use Cases:**
- Building LLM prompt context from conversation history
- Tracking state transitions over time
- Debugging orchestration decisions

[Learn more →](./docs/ARCHITECTURE.md#1-eventbus-blackboard-pattern)

---

### 2️⃣ Event Subscriptions

React to events as they happen with type-safe callbacks:

```csharp
// Subscribe to specific events
using var subscription = bus.Subscribe<TaskCompleted>(task => {
    logger.LogInformation($"Task {task.Id} completed in {task.Duration}");
    metrics.RecordCompletion(task.Duration);
});

// Subscriptions automatically unsubscribe when disposed
```

**Use Cases:**
- Side-effects like logging and metrics
- Triggering external systems
- Real-time notifications

[Learn more →](./docs/INTEGRATION.md#pattern-event-reactions-side-effects)

---

### 3️⃣ Scoped Buses

Create isolated state scopes for multi-agent systems while sharing global facts:

```csharp
var rootBus = new EventBus();
rootBus.Publish(new GlobalConfig("production"));

// Create per-agent scopes
var agent1Bus = rootBus.CreateScope("agent-1");
var agent2Bus = rootBus.CreateScope("agent-2");

// Agents have isolated state
agent1Bus.Publish(new AgentStatus("busy"));
agent2Bus.TryGet<AgentStatus>(out var status); // ❌ Not found

// But can access shared facts
agent1Bus.TryGetWithFallback<GlobalConfig>(out var config); // ✅ Found in parent
```

**Use Cases:**
- Multi-agent coordination
- Per-conversation state isolation
- Hierarchical state management

[Learn more →](./docs/INTEGRATION.md#pattern-scoped-state-multi-agent)

---

### 4️⃣ Utility-Based Scoring

Proposals are scored dynamically using considerations:

```csharp
new Proposal(
    id: "respond-to-user",
    cons: new[]
    {
        new HasFact<UserMessage>(),  // 1.0 if message exists, else 0.0
        new CurveSignal<UserWaitTime>(
            selector: t => t.Seconds,
            curve: Curves.Logistic(k: 0.1, x0: 30),  // Urgency increases over time
            inputDomain: (0, 60)
        )
    },
    act: async ct => { /* respond */ }
)
```

**Utility Formula:**
```
utility = prior × (geometric_mean_of_considerations)^temperature
```

[Learn more →](./docs/ARCHITECTURE.md#5-proposals)

---

### 5️⃣ Memory Management

Two-tier memory architecture: **EventBus** provides fast short-term storage (last 100 events per type), while **IMemoryStore** offers long-term retention with flexible querying. The **MemorySensor** automatically archives old EventBus facts to long-term storage.

```
┌──────────────────────────────────────┐
│         EventBus (Short-term)        │
│    Recent facts (last 100 items)     │
└────────────┬─────────────────────────┘
             │
             │ MemorySensor archives old facts
             │
             ▼
┌──────────────────────────────────────┐
│      IMemoryStore (Long-term)        │
│   Historical facts (unlimited)       │
└──────────────────────────────────────┘
```

**Store and recall facts with `InMemoryStore`:**

```csharp
using UtilityAi.Memory;

var store = new InMemoryStore();

// Store a fact with timestamp
await store.StoreAsync(new UserMessage("Hello"), DateTimeOffset.UtcNow);

// Recall recent facts
var query = new MemoryQuery
{
    TimeWindow = TimeSpan.FromHours(1),
    MaxResults = 10,
    SortOrder = SortOrder.NewestFirst
};
var memories = await store.RecallAsync<UserMessage>(query);

// Prune old data (remove facts older than 7 days)
await store.PruneAsync(TimeSpan.FromDays(7));
```

**Automatic archival with `MemorySensor`:**

```csharp
var memoryStore = new InMemoryStore();
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddSensor(new MemorySensor(
        store: memoryStore,
        archiveThreshold: TimeSpan.FromMinutes(10),
        typeof(UserMessage), typeof(AssistantMessage)
    ))
    .AddModule(new MyModule());
```

**Use Cases:** Long conversational history for LLMs • Analytics & reporting • Audit logging • User personalization

[Learn more →](./src/UtilityAi/Memory/README.md)

---

## 🔌 Integration Examples

### OpenAI Integration

```csharp
public class OpenAIModule : ICapabilityModule
{
    private readonly ChatClient _client;

    public OpenAIModule(string apiKey)
    {
        _client = new ChatClient("gpt-4", apiKey);
    }

    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var userMsg = rt.Bus.GetOrDefault<UserMessage>();
        if (userMsg == null) yield break;

        yield return new Proposal(
            id: "openai.respond",
            cons: new[] { new HasFact<UserMessage>() },
            act: async ct =>
            {
                // Build context from EventBus history
                var history = rt.Bus.GetHistory<UserMessage>(maxItems: 5);
                var messages = history
                    .Select(e => new UserChatMessage(e.Value.Text))
                    .ToList();

                var response = await _client.CompleteChatAsync(messages, ct);
                rt.Bus.Publish(new AssistantMessage(response.Value.Content[0].Text));
            }
        );
    }
}
```

[See more examples →](./docs/INTEGRATION.md#llm-integration)

---

## 🧪 Testing

The framework is designed to be testable:

```csharp
[Fact]
public async Task Orchestrator_ChoosesHighestUtility()
{
    // Arrange
    var bus = new EventBus();
    bus.Publish(new UserMessage("test"));

    var sink = new TestingSink();
    var orch = new UtilityAiOrchestrator(bus: bus)
        .AddModule(new MyModule());

    // Act
    await orch.RunAsync(new UserIntent("test"), maxTicks: 1, CancellationToken.None, sink);

    // Assert
    Assert.Single(sink.ExecutedProposals);
    Assert.Equal("my.action", sink.ExecutedProposals[0]);
}
```

69 tests covering all core functionality. Run with:
```bash
dotnet test
```

[Learn more →](./docs/INTEGRATION.md#pattern-testing-assertions)

---

## 📦 What's in the Box

```
UtilityAi/
├── UtilityAi/              # Core framework
│   ├── Utils/              # EventBus, Runtime
│   ├── Orchestration/      # UtilityAiOrchestrator, OrchestratorExtensions
│   ├── Sensor/             # ISensor interface
│   ├── Capabilities/       # ICapabilityModule, Attributes (NEW!)
│   ├── Consideration/      # Proposal, IConsideration, built-in considerations
│   └── Evaluators/         # Response curves (Logistic, Power, etc.)
├── UtilityAi.Maf/          # Microsoft Agent Framework integration (NEW!)
├── Example/                # Complete task management demo (manual + attributes)
├── Example.Maf/            # MAF integration demo (NEW!)
├── Tests/                  # Comprehensive tests
└── docs/                   # Architecture and integration guides
```

---

## 🎯 Use Cases

This framework is ideal for:

- **AI Agent Systems** - Coordinate multiple AI agents with shared and isolated state
- **LLM-Based Applications** - Build context from event history for prompts
- **Dynamic Workflows** - Let actions emerge from current state rather than hardcoding sequences
- **Game AI** - Classic utility AI for NPCs and decision-making
- **Task Orchestration** - Prioritize and execute tasks based on resources and constraints
- **Reactive Systems** - React to events with subscriptions while maintaining orchestrated behavior

---

## 🤝 Contributing

This is a framework - contributions that improve extensibility, documentation, or add well-tested features are welcome!

1. Fork the repository
2. Create a feature branch
3. Write tests for your changes
4. Submit a pull request

Please ensure all tests pass before submitting.

---

## 📦 Releases

For maintainers: See [RELEASE.md](RELEASE.md) for instructions on creating releases. The project supports both traditional tag-based releases and manual releases via GitHub Actions workflow dispatch.

---

## 📄 License

MIT License - see [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

Built with inspiration from:
- **Utility AI** pattern from game development
- **Blackboard pattern** from classical AI
- **Java Annotations** (Spring Framework, Jakarta EE)
- Modern agent orchestration needs (Microsoft Agent Framework, Semantic Kernel, AutoGen, LangGraph)

---

## 📞 Support

- 📖 [Documentation](./docs/)
- 💬 [Issues](https://github.com/yourusername/UtilityAi/issues)
- 📧 [Discussions](https://github.com/yourusername/UtilityAi/discussions)

---

<p align="center">
  <sub>Built with ❤️ for the AI agent community</sub>
</p>
