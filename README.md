# 🧠 UtilityAI Framework (.NET 8)

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Tests](https://img.shields.io/badge/tests-138%20passing-brightgreen)](./Tests/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A lightweight, modular framework for building AI agent orchestration systems using classic **Utility AI** decision-making patterns. The framework scores candidate actions each tick and executes the highest-utility option based on current context—no hardcoded workflows required.

> **Don't script workflows — evaluate them.**

---

## ✨ Features

- 🎯 **Utility-Based Decision Making** - Actions compete based on dynamic scoring
- 🏷️ **Attribute-Based Registration** - Java-style annotations for declarative module configuration
- 📝 **Event History** - Access timestamped event history for LLM conversation context
- 🔔 **Type-Safe Subscriptions** - React to events with callbacks
- 🏗️ **Scoped State** - Isolate multi-agent state while sharing global facts
- 🔌 **Pluggable Architecture** - Sensors, modules, and considerations are fully extensible
- 📊 **Built-in Observability** - Sinks for logging, metrics, and testing
- 🧪 **Well Tested** - 138 comprehensive tests covering all core functionality
- 📚 **Production Ready** - Thread-safe, documented, with integration guides

---

## 🚀 Quick Start

### Installation

```bash
# Clone or download the repository
git clone https://github.com/mrrasmussendk/UtilityAi.git
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
var intent = new UserIntent(new IntentGoal("my-goal"));
await orchestrator.RunAsync(intent, maxTicks: 10, CancellationToken.None);
```

### Attribute-Based Registration

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

---

## 📚 Documentation

### Getting Started
- **[Architecture Guide](./docs/ARCHITECTURE.md)** - Understanding the framework design and patterns
- **[Integration Guide](./docs/INTEGRATION.md)** - Connect to OpenAI, Anthropic, Azure AI, and more
- **[Example Project](./Example/)** - Complete working task management system

### Core Concepts
- **[EventBus Patterns](./docs/ARCHITECTURE.md#1-eventbus-blackboard-pattern)** - History, subscriptions, and scoping
- **[Sensors](./docs/ARCHITECTURE.md#3-sensors-isensor)** - Observing and publishing facts
- **[Capability Modules](./docs/ARCHITECTURE.md#4-capability-modules-icapabilitymodule)** - Proposing actions
- **[Considerations](./docs/ARCHITECTURE.md#6-considerations-iconsideration)** - Scoring proposals
- **[Observability](./docs/ARCHITECTURE.md#8-observability-iorchestrationSink)** - Monitoring and debugging

### Advanced Topics
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

### 1️⃣ Event History

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

138 tests covering all core functionality. Run with:
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
│   ├── Capabilities/       # ICapabilityModule, Attributes
│   ├── Consideration/      # Proposal, IConsideration, built-in considerations
│   └── Evaluators/         # Response curves (Logistic, Power, etc.)
├── Example/                # Complete task management demo (manual + attributes)
├── Tests/                  # 138 comprehensive tests
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
- Modern agent orchestration needs (Microsoft Semantic Kernel, AutoGen, LangGraph)

---

## 📞 Support

- 📖 [Documentation](./docs/)
- 💬 [Issues](https://github.com/mrrasmussendk/UtilityAi/issues)
- 📧 [Discussions](https://github.com/mrrasmussendk/UtilityAi/discussions)

---

<p align="center">
  <sub>Built with ❤️ for the AI agent community</sub>
</p>
