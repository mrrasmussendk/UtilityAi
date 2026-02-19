# 🧠 UtilityAI Framework (.NET 8)

[![NuGet](https://img.shields.io/nuget/v/UtilityAi?color=blue)](https://www.nuget.org/packages/UtilityAi)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Tests](https://img.shields.io/badge/tests-203%20passing-brightgreen)](./Tests/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A lightweight, modular .NET 8 framework for building **AI agent orchestration** systems using classic **Utility AI** decision-making patterns. The framework evaluates and scores candidate actions each tick, executing the highest-utility option based on current context — no hardcoded workflows required.

> **Don't script workflows — evaluate them.**

---

## Table of Contents

- [Features](#-features)
- [Quick Start](#-quick-start)
- [Architecture Overview](#-architecture-overview)
- [Core Features](#-core-features-in-detail)
- [Integration Examples](#-integration-examples)
- [Dashboard](#-dashboard)
- [Testing](#-testing)
- [Project Structure](#-project-structure)
- [Documentation](#-documentation)
- [Use Cases](#-use-cases)
- [Contributing](#-contributing)
- [License](#-license)

---

## ✨ Features

| Category | Highlights |
|----------|------------|
| **Decision Making** | Utility-based scoring with response curves (logistic, power, piecewise) |
| **LLM Integration** | Intent interpretation with rich parameters; self-documenting capability metadata for prompt generation |
| **Agent Orchestration** | Microsoft Agent Framework (MAF) integration; multi-agent coordination with scoped state |
| **Event System** | Timestamped event history, type-safe subscriptions, scoped buses |
| **Memory** | Two-tier memory with automatic archival from EventBus to long-term storage |
| **Extensibility** | Pluggable sensors, modules, considerations, and selection strategies |
| **Tooling** | Real-time web dashboard for visualizing proposals, scores, and tick history |
| **Observability** | Built-in sinks for logging, metrics, and testing |
| **Quality** | 203+ tests, thread-safe, XML-documented public API |

---

## 🚀 Quick Start

### Installation

```bash
# NuGet (recommended)
dotnet add package UtilityAi
```

Or clone the repository to explore the source and examples:

```bash
git clone https://github.com/mrrasmussendk/UtilityAi.git
cd UtilityAi
dotnet build
dotnet test
```

### Minimal Example

```csharp
using UtilityAi.Orchestration;
using UtilityAi.Utils;

// 1. Create the event bus (shared state / blackboard)
var bus = new EventBus();

// 2. Configure the orchestrator
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddSensor(new MyEnvironmentSensor())
    .AddModule(new MyCapabilityModule());

// 3. Run the sense → propose → score → act loop
var intent = UserIntent.ForGoal("my-goal");
await orchestrator.RunAsync(intent, maxTicks: 10, CancellationToken.None);
```

### Attribute-Based Registration

Reduce boilerplate with declarative attributes:

```csharp
[Capability(Priority = 100, Domain = "validation")]
[RequiresFact<TaskQueue>]
[ActiveWhen("priority_mode", "urgent", "balanced")]
public class ValidationModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt) { /* ... */ }
}

// Auto-discover all modules from the assembly
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddSensor(new MySensor())
    .DiscoverCapabilities(Assembly.GetExecutingAssembly());
```

See the [Example project](./examples/Example/) for a complete demo comparing manual and attribute-based approaches.

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
| **IConsideration** | Score proposals (0.0–1.0) | Implement custom scoring logic |
| **IOrchestrationSink** | Observe orchestration events | Implement for logging/metrics |

### Utility Formula

```
utility = prior × (geometric_mean_of_considerations) ^ temperature
```

- **Prior** — base tendency (0–1)
- **Considerations** — each returns a score in 0–1, combined via geometric mean
- **Temperature** — >1 sharpens differences, <1 flattens them

---

## 💡 Core Features in Detail

### 1️⃣ LLM Intent Interpretation

Proposals declare what parameters they need; the framework exposes this metadata so an LLM can provide structured responses that drive scoring automatically.

```csharp
yield return ProposalHelper.For("ticket.create.priority")
    .WithDescription("Create high-priority support ticket")
    .ForIntent("ticket.create", IntentMatchType.Exact)
    .ScoreByIntentParameter("urgency", x => Math.Pow(x, 3), (0, 1),
        description: "How urgent the issue is (0=low, 1=critical)")
    .UsesIntentParameter("customer_tier", "string",
        allowedValues: new[] { "free", "premium", "enterprise" })
    .WithAction(async ct => await CreatePriorityTicket(rt, ct));
```

**Flow:** Proposals declare parameters → Framework exposes metadata via `GetCapabilitiesInfo()` → LLM prompt includes parameter specs → LLM returns structured intent → Proposals score automatically → Best action wins.

[See the complete Intent-Based Agent example →](./examples/Example/IntentBasedAgent/)

---

### 2️⃣ Event History & Subscriptions

```csharp
// Timestamped history — perfect for building LLM conversation context
var history = bus.GetHistory<UserMessage>(maxItems: 10);
foreach (var evt in history)
    Console.WriteLine($"{evt.Timestamp}: {evt.Value.Text}");

// Type-safe subscriptions — react to events as they happen
using var sub = bus.Subscribe<TaskCompleted>(task =>
    logger.LogInformation($"Task {task.Id} completed in {task.Duration}"));
```

---

### 3️⃣ Scoped Buses (Multi-Agent State)

Isolate per-agent state while sharing global facts:

```csharp
var rootBus = new EventBus();
rootBus.Publish(new GlobalConfig("production"));

var agent1Bus = rootBus.CreateScope("agent-1");
var agent2Bus = rootBus.CreateScope("agent-2");

agent1Bus.Publish(new AgentStatus("busy"));
agent2Bus.TryGet<AgentStatus>(out var status);                   // ❌ Not found (isolated)
agent1Bus.TryGetWithFallback<GlobalConfig>(out var config);      // ✅ Found in parent
```

---

### 4️⃣ Memory Management

Two-tier architecture: **EventBus** (fast, last 100 events per type) + **IMemoryStore** (long-term, unlimited). The **MemorySensor** archives old facts automatically.

```csharp
var memoryStore = new InMemoryStore();
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddSensor(new MemorySensor(
        store: memoryStore,
        archiveThreshold: TimeSpan.FromMinutes(10),
        typeof(UserMessage), typeof(AssistantMessage)))
    .AddModule(new MyModule());
```

[Learn more →](./src/UtilityAi/Memory/README.md)

---

### 5️⃣ Microsoft Agent Framework (MAF) Integration

Orchestrate [MAF agents](https://learn.microsoft.com/en-us/agent-framework/) with utility-based decision-making:

```csharp
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddMafAgentSensor(
        new MafAgentRegistration("research", researchAgent),
        new MafAgentRegistration("writer", writerAgent))
    .AddMafAgent(researchAgent, "research",
        considerations: new IConsideration[] { new MafAgentAvailable("research") })
    .AddMafAgent(writerAgent, "writer",
        considerations: new IConsideration[] { new HasMafAgentResult("research") });
```

[See the MAF example →](./examples/Example.Maf/) | [MAF Integration Guide →](./docs/INTEGRATION.md#microsoft-agent-framework-maf-integration)

---

## 🔌 Integration Examples

### OpenAI

```csharp
public class OpenAIModule : ICapabilityModule
{
    private readonly ChatClient _client;

    public OpenAIModule(string apiKey)
        => _client = new ChatClient("gpt-4", apiKey);

    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var userMsg = rt.Bus.GetOrDefault<UserMessage>();
        if (userMsg == null) yield break;

        yield return new Proposal(
            id: "openai.respond",
            cons: new[] { new HasFact<UserMessage>() },
            act: async ct =>
            {
                var history = rt.Bus.GetHistory<UserMessage>(maxItems: 5);
                var messages = history.Select(e => new UserChatMessage(e.Value.Text)).ToList();
                var response = await _client.CompleteChatAsync(messages, ct);
                rt.Bus.Publish(new AssistantMessage(response.Value.Content[0].Text));
            });
    }
}
```

The framework also supports **Azure OpenAI**, **Anthropic Claude**, and any provider you wire up. See the [Integration Guide](./docs/INTEGRATION.md#llm-integration) for complete examples.

---

## 📊 Dashboard

An optional web dashboard to visualize proposals, consideration scores, and tick history in real time:

```csharp
var dashboardState = new DashboardState();
app.MapUtilityAiDashboard(dashboardState);
await orchestrator.RunAsync(intent, maxTicks: 10, ct, sink: new DashboardSink(dashboardState));
```

Navigate to `http://localhost:5000/utilityai/dashboard` to inspect scores, override priors/temperatures, and step through ticks.

[Dashboard documentation →](./tools/UtilityAi.Dashboard/README.md)

---

## 🧪 Testing

The framework is designed for testability. 203 tests cover all core functionality.

```csharp
[Fact]
public async Task Orchestrator_ChoosesHighestUtility()
{
    var bus = new EventBus();
    bus.Publish(new UserMessage("test"));

    var sink = new TestingSink();
    var orch = new UtilityAiOrchestrator(bus: bus)
        .AddModule(new MyModule());

    await orch.RunAsync(new UserIntent("test"), maxTicks: 1, CancellationToken.None, sink);

    Assert.Single(sink.ExecutedProposals);
    Assert.Equal("my.action", sink.ExecutedProposals[0]);
}
```

```bash
dotnet test
```

[Testing patterns →](./docs/INTEGRATION.md#pattern-testing-assertions)

---

## 📦 Project Structure

```
UtilityAi/
├── src/
│   └── UtilityAi/              # Core framework (NuGet: UtilityAi)
│       ├── Utils/              # EventBus, Runtime
│       ├── Orchestration/      # UtilityAiOrchestrator, Extensions
│       ├── Sensor/             # ISensor + built-in sensors
│       ├── Capabilities/       # ICapabilityModule, Attributes
│       ├── Consideration/      # IConsideration + built-in considerations
│       ├── Evaluators/         # Response curves (Logistic, Power, etc.)
│       └── Memory/             # IMemoryStore, InMemoryStore, MemorySensor
├── integrations/
│   ├── UtilityAi.Maf/         # Microsoft Agent Framework integration
│   ├── UtilityAi.LLM.Abstractions/  # LLM provider abstraction
│   └── UtilityAi.LLM.OpenAI/        # OpenAI provider implementation
├── examples/
│   ├── Example/               # Demo agents (AgentAssistant, SmartHome, ChatBot, Intent)
│   └── Example.Maf/           # MAF integration demo
├── tools/
│   └── UtilityAi.Dashboard/   # Real-time web dashboard
├── Tests/                     # 203 xUnit tests
└── docs/                      # Architecture, integration, and pattern guides
```

---

## 📚 Documentation

### Getting Started

| Guide | Description |
|-------|-------------|
| **[Architecture](./docs/ARCHITECTURE.md)** | Framework design, orchestration loop, component roles |
| **[Integration](./docs/INTEGRATION.md)** | Connect to MAF, OpenAI, Anthropic, Azure AI, and more |
| **[Built-in Components](./docs/BUILT_IN_COMPONENTS.md)** | Reference for all built-in sensors, considerations, and modules |
| **[Proposal Patterns](./docs/PROPOSAL_PATTERNS.md)** | Best practices and anti-patterns for building proposals |
| **[Eligibility vs Considerations](./docs/ELIGIBILITY_VS_CONSIDERATIONS.md)** | When to use hard gates vs soft scoring |

### Examples

| Example | Description |
|---------|-------------|
| **[Agent Assistant](./examples/Example/AgentAssistant/)** | Multi-strategy conversational AI agent |
| **[Smart Home](./examples/Example/SmartHomeAgent/)** | IoT automation balancing comfort, energy, and security |
| **[LLM ChatBot](./examples/Example/Example.LLM.ChatBot/)** | Simple OpenAI-powered chatbot |
| **[Intent-Based Agent](./examples/Example/IntentBasedAgent/)** | LLM intent interpretation with rich parameters |
| **[MAF Integration](./examples/Example.Maf/)** | Microsoft Agent Framework orchestration |

### Deep Dives

- **[Memory System](./src/UtilityAi/Memory/README.md)** — Long-term storage, querying, and automatic archival
- **[Sensors Reference](./src/UtilityAi/Sensor/BuiltIn/README.md)** — TimeSensor, ConversationHistorySensor, ResourceSensor, MemorySensor
- **[Considerations Reference](./src/UtilityAi/Consideration/General/README.md)** — HasFact, CurveSignal, Cooldown, TimeWindow, and more
- **[Built-in Modules Reference](./src/UtilityAi/Capabilities/BuiltIn/README.md)** — IdleModule, CleanupModule, StopOnSignalModule
- **[Dashboard](./tools/UtilityAi.Dashboard/README.md)** — Real-time orchestration visualization

---

## 🎯 Use Cases

- **AI Agent Systems** — Coordinate multiple AI agents with shared and isolated state
- **LLM-Based Applications** — Build context from event history for prompts
- **Dynamic Workflows** — Let actions emerge from current state instead of hardcoding sequences
- **Game AI** — Classic utility AI for NPCs and decision-making
- **Task Orchestration** — Prioritize and execute tasks based on resources and constraints
- **IoT / Smart Home** — Balance competing objectives (comfort, energy, security)

---

## 🤝 Contributing

Contributions that improve extensibility, documentation, or add well-tested features are welcome!

1. Fork the repository
2. Create a feature branch
3. Write tests for your changes
4. Ensure all tests pass (`dotnet test`)
5. Submit a pull request

For maintainers: see [RELEASE.md](RELEASE.md) for the release process.

---

## 📄 License

MIT License — see [LICENSE](LICENSE) for details.

---

## 🙏 Acknowledgments

Built with inspiration from:
- **Utility AI** pattern from game development
- **Blackboard pattern** from classical AI
- **Java Annotations** (Spring Framework, Jakarta EE)
- Modern agent orchestration (Microsoft Agent Framework, Semantic Kernel, AutoGen, LangGraph)

---

## 📞 Support

- 📖 [Documentation](./docs/)
- 💬 [Issues](https://github.com/mrrasmussendk/UtilityAi/issues)
- 📧 [Discussions](https://github.com/mrrasmussendk/UtilityAi/discussions)

---

<p align="center">
  <sub>Built with ❤️ for the AI agent community</sub>
</p>
