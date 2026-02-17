# 🧠 UtilityAI Framework (.NET 8)

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Tests](https://img.shields.io/badge/tests-69%20passing-brightgreen)](./Tests/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A lightweight, modular framework for building AI agent orchestration systems using classic **Utility AI** decision-making patterns. The framework scores candidate actions each tick and executes the highest-utility option based on current context—no hardcoded workflows required.

> **Don't script workflows — evaluate them.**

## ⚡ Core Philosophy

This framework follows a **capability-based** design where:

1. **Capability Modules** = What your agent CAN DO (e.g., "respond to messages", "do research", "handle errors")
2. **Proposals** = Different STRATEGIES for each capability (e.g., "direct response" vs "ask for clarification" vs "acknowledge")
3. **Considerations** = Declarative scoring based on EventBus facts (NO if-statements in `Propose()`!)
4. **Utility System** = Automatically selects the best strategy each tick based on current state

**Anti-Pattern:**
❌ Don't loop through data items creating proposals: `foreach (var task in tasks) yield return new Proposal(...)`
✅ Select the best item first, then propose strategies: `var best = tasks.OrderByDescending(...).First(); yield return ProposalHelper.For(...)`

See [PROPOSAL_PATTERNS.md](./docs/PROPOSAL_PATTERNS.md) for detailed guidance.

---

## ✨ Features

- 🎯 **Utility-Based Decision Making** - Actions compete based on dynamic scoring
- 🏷️ **Attribute-Based Registration** - Java-style annotations for declarative module configuration
- 🔧 **ProposalHelper Fluent API** - Clean, readable proposal creation with method chaining
- 📝 **Event History & Memory** - EventBus history + long-term memory retention with `IMemoryStore`
- 🔔 **Type-Safe Subscriptions** - React to events with callbacks
- 🏗️ **Scoped State** - Isolate multi-agent state while sharing global facts
- 🔌 **Pluggable Architecture** - Sensors, modules, and considerations are fully extensible
- 📦 **15+ Built-in Considerations** - SignalConsideration, HasFact, Threshold, Range, Cooldown, Time-based, and more
- 🎲 **Selection Strategies** - Built-in RandomSelectionStrategy and RoundRobinSelectionStrategy for intentional tie-breaking
- 🛡️ **Eligibility System** - Separate filtering (eligibility) from scoring (considerations) to prevent common bugs
- 🔬 **Built-in Sensors** - Time, History, Frequency, Resource monitoring
- 💾 **Persistence** - Snapshot/Restore EventBus state for session management
- 📊 **Built-in Observability** - Sinks for logging, metrics, and testing
- 🧪 **Well Tested** - 69+ comprehensive tests covering all core functionality
- 📚 **Production Ready** - Thread-safe, documented, with integration guides
- 📖 **Clear Patterns** - Well-documented patterns and anti-patterns to avoid common mistakes

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

### Complete Working Example

Here's a simple AI agent that decides between responding directly or doing research:

```csharp
using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Orchestration;
using UtilityAi.Utils;

// 1. Define your fact types (what goes on the EventBus)
public record UserMessage(string Text, string UserId);
public record ConversationContext(double Confidence, bool RequiresResearch);
public record AssistantResponse(string Text, string Source);

// 2. Create a capability module (what your agent CAN DO)
[Capability(Priority = 100)]
[RequiresFact<UserMessage>]
[RequiresFact<ConversationContext>]
public class RespondModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        // Strategy 1: Direct confident response
        yield return ProposalHelper.For("respond.direct")
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "confidence",
                selector: ctx => ctx.Confidence,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "no_research_needed",
                selector: ctx => ctx.RequiresResearch ? 0.0 : 1.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithAction(async ct =>
            {
                var userMsg = rt.Bus.GetOrDefault<UserMessage>();
                rt.Bus.Publish(new AssistantResponse($"Response to: {userMsg?.Text}", "direct"));
            });

        // Strategy 2: Acknowledge and research
        yield return ProposalHelper.For("respond.research")
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "needs_research",
                selector: ctx => ctx.RequiresResearch ? 1.0 : 0.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithAction(async ct =>
            {
                rt.Bus.Publish(new AssistantResponse("Let me research that...", "acknowledgment"));
            });
    }
}

// 3. Set up and run the orchestrator
var bus = new EventBus();

// Publish initial facts BEFORE orchestration
bus.Publish(new UserMessage("What's the weather today?", "user-123"));
bus.Publish(new ConversationContext(Confidence: 0.7, RequiresResearch: true));

// Create orchestrator and discover capability modules
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .DiscoverCapabilities(Assembly.GetExecutingAssembly());

// Run orchestration loop
var intent = new UserIntent(Goal: new IntentGoal("respond-to-user"));
await orchestrator.RunAsync(intent, maxTicks: 5, CancellationToken.None);
```

**Key Points:**
- **Facts** are published to EventBus BEFORE orchestration starts
- **Capability modules** represent what the agent CAN DO (e.g., respond, research, fallback)
- **Proposals** represent different STRATEGIES for that capability (e.g., direct response vs. research)
- **Considerations** score proposals based on EventBus facts (no if-statements in `Propose()`!)
- **Utility system** picks the highest-scoring proposal each tick

**How it works:**
```
Tick 1:
  - Sensors observe environment → publish facts to EventBus
  - Modules propose strategies based on EventBus state
  - Orchestrator scores all proposals using considerations
  - Highest-utility proposal executes → publishes new facts to EventBus

Tick 2: (repeat with updated EventBus state)
```

### 🔧 Built-in Considerations

The framework provides robust considerations out of the box:

**Fact-Based Considerations** (in `UtilityAi.Consideration.General`):
```csharp
using UtilityAi.Consideration.General;

// Score based on continuous values with response curves
yield return ProposalHelper.For("my.action")
    .WithConsideration(new SignalConsideration<ResourceUsage>(
        name: "cpu_available",
        selector: r => r.CpuPercent,
        curve: x => 1.0 - x,  // Inverted: lower CPU = higher score
        inputDomain: (0, 100)))
    .WithAction(async ct => { /* ... */ });
```

**Available Considerations:**
- `SignalConsideration<T>` - Score continuous values with response curves (recommended for most use cases)
- `FixedValueConsideration` - Always returns same value (for fallback proposals)
- `HasFact<T>` - Returns 1.0 if fact exists, 0.0 otherwise (⚠️ see warning below)
- `NotHasFact<T>` - Returns 1.0 if fact doesn't exist, 0.0 otherwise (⚠️ see warning below)
- `HasFactWhere<T>` - Returns 1.0 if fact exists and predicate passes (⚠️ see warning below)
- `ThresholdValue` - Binary threshold (above/below a value)
- `RangeValue` - Scores based on distance from ideal range
- `Cooldown` - Time-based gating (prevents rapid re-execution)
- `TimeWindow` - Active only during specific time periods
- And 10+ more in [BUILT_IN_COMPONENTS.md](./docs/BUILT_IN_COMPONENTS.md)

**⚠️ Important: Eligibility vs Considerations**

Using `HasFact<T>` or `NotHasFact<T>` as considerations can cause the **geometric mean trap** where proposals unexpectedly score 0.0. For hard requirements (yes/no filtering), use **eligibility** instead:

```csharp
// ❌ DANGEROUS - Can cause geometric mean = 0
.WithConsideration(new HasFact<ResearchResults>())

// ✅ CORRECT - Use eligibility for hard requirements
.WithEligibility(new HasFactEligible<ResearchResults>())
```

**Golden Rule:**
- Use **Eligibility** for filtering (yes/no) - `HasFactEligible<T>`, `NotHasFactEligible<T>`
- Use **Considerations** for scoring (0-1) - `SignalConsideration<T>`, `FixedValueConsideration`

See [ELIGIBILITY_VS_CONSIDERATIONS.md](./docs/ELIGIBILITY_VS_CONSIDERATIONS.md) for detailed explanation

See the example projects for complete implementations:
- **[AgentAssistant](./Example/AgentAssistant/)** - Conversational AI with LLM integration
- **[SmartHomeAgent](./Example/SmartHomeAgent/)** - IoT home automation with competing priorities

---

## 📚 Documentation

### Getting Started
- **[Architecture Guide](./docs/ARCHITECTURE.md)** - Understanding the framework design and patterns
- **[Built-in Components](./docs/BUILT_IN_COMPONENTS.md)** - Complete reference for considerations, sensors, modules
- **[Integration Guide](./docs/INTEGRATION.md)** - Connect to OpenAI, Anthropic, Azure AI, and more
- **[Example Projects](./Example/)**
  - **[AgentAssistant](./Example/AgentAssistant/)** - Conversational AI agent with LLM integration patterns
  - **[SmartHomeAgent](./Example/SmartHomeAgent/)** - IoT home automation balancing energy, security, comfort, and maintenance

### Core Concepts
- **[EventBus Patterns](./docs/ARCHITECTURE.md#1-eventbus-blackboard-pattern)** - History, subscriptions, and scoping
- **[Sensors](./docs/ARCHITECTURE.md#3-sensors-isensor)** - Observing and publishing facts
- **[Capability Modules](./docs/ARCHITECTURE.md#4-capability-modules-icapabilitymodule)** - Proposing actions
- **[Considerations](./docs/ARCHITECTURE.md#6-considerations-iconsideration)** - Scoring proposals
- **[Eligibility vs Considerations](./docs/ELIGIBILITY_VS_CONSIDERATIONS.md)** - ⚠️ **Critical:** Understanding when to use each (prevents common bugs)
- **[Response Curves & Tie-Breaking](./Example/AgentAssistant/CURVES_AND_TIES.md)** - Avoiding utility score ties with proper curves
- **[Memory System](./docs/BUILT_IN_COMPONENTS.md#-memory-system)** - Long-term fact retention beyond EventBus
- **[Observability](./docs/ARCHITECTURE.md#8-observability-iorchestrationSink)** - Monitoring and debugging

### Advanced Topics
- **[Multi-Agent Coordination](./docs/INTEGRATION.md#multi-agent-coordination)** - Patterns for agent collaboration
- **[State Persistence](./docs/BUILT_IN_COMPONENTS.md#persistence)** - Snapshot/Restore for session management
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

Proposals are scored dynamically using considerations. Use `ProposalHelper` for clean, fluent syntax:

```csharp
// Create a proposal that scores higher when confidence is high
yield return ProposalHelper.For("respond-to-user")
    .WithDescription("Sends a direct, confident response to the user's message")
    .WithConsideration(new HasFact<UserMessage>())  // 1.0 if exists, 0.0 if not
    .WithConsideration(new SignalConsideration<ConversationContext>(
        name: "confidence",
        selector: ctx => ctx.Confidence,
        curve: x => x,  // Linear - higher confidence = higher score
        inputDomain: (0, 1)))
    .WithConsideration(new SignalConsideration<UserWaitTime>(
        name: "urgency",
        selector: t => t.Seconds,
        curve: x => 1.0 / (1.0 + Math.Exp(-0.1 * (x - 0.5))),  // Logistic S-curve
        inputDomain: (0, 60)))
    .WithAction(async ct => { /* respond */ });
```

**Utility Formula:**
```
utility = prior × (geometric_mean_of_considerations)^temperature
```

**Key Design Principle:** Considerations do ALL the scoring logic - no if-statements in `Propose()`. Let the utility system decide what action to take based on the current EventBus state.

[Learn more →](./docs/ARCHITECTURE.md#5-proposals)

---

### 5️⃣ Capability Introspection

Introspect all registered capabilities and their actions - perfect for LLM planning:

```csharp
var orchestrator = new UtilityAiOrchestrator()
    .DiscoverCapabilities(Assembly.GetExecutingAssembly());

var bus = new EventBus();
bus.Publish(new UserMessage("Hello"));

var rt = new Runtime(bus, new UserIntent("plan"), 0);
var capabilities = orchestrator.GetCapabilitiesInfo(rt);

foreach (var cap in capabilities)
{
    Console.WriteLine($"Module: {cap.ModuleName}");
    foreach (var action in cap.PotentialActions)
    {
        Console.WriteLine($"  Action: {action.ProposalId}");
        Console.WriteLine($"    Description: {action.Description}");
        Console.WriteLine($"    Prior: {action.Prior}, Temperature: {action.Temperature}");
        Console.WriteLine($"    Considerations: {string.Join(", ", action.ConsiderationNames)}");
        Console.WriteLine($"    Eligibilities: {string.Join(", ", action.EligibilityNames)}");
    }
}
```

**Output:**
```
Module: RespondModule
  Action: respond.direct
    Description: Sends a direct, confident response to the user's message
    Prior: 1.0, Temperature: 1.0
    Considerations: confidence, no_research_needed
    Eligibilities:
  Action: respond.research
    Description: Acknowledges message and begins research
    Prior: 0.8, Temperature: 1.0
    Considerations: needs_research
    Eligibilities: RequiresAuth
```

**Use Cases:**
- LLM-based planning agents that need to know available actions
- Debugging and documentation generation
- Dynamic UI generation for agent controls
- Slack bots and conversational agents that explain their capabilities

[Learn more →](./docs/INTEGRATION.md#capability-introspection)

---

## 🔌 Integration Examples

### OpenAI Integration

```csharp
[Capability(Priority = 100, Domain = "llm-response")]
[RequiresFact<UserMessage>]
[RequiresFact<ConversationContext>]
public class OpenAIModule : ICapabilityModule
{
    private readonly ChatClient _client;

    public OpenAIModule(string apiKey)
    {
        _client = new ChatClient("gpt-4", apiKey);
    }

    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        // Strategy 1: Confident direct response
        yield return ProposalHelper.For("openai.respond")
            .WithConsideration(new HasFact<UserMessage>())
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "confidence",
                selector: ctx => ctx.Confidence,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new HasFact<AssistantMessage>(
                name: "not_already_responded",
                selector: _ => false))  // Inverted - only if no response yet
            .WithAction(async ct =>
            {
                // Build context from EventBus history
                var history = rt.Bus.GetHistory<UserMessage>(maxItems: 5);
                var messages = history
                    .Select(e => new UserChatMessage(e.Value.Text))
                    .ToList();

                var response = await _client.CompleteChatAsync(messages, ct);
                rt.Bus.Publish(new AssistantMessage(response.Value.Content[0].Text));
            });

        // Strategy 2: Research first, then respond
        yield return ProposalHelper.For("openai.research_then_respond")
            .WithConsideration(new HasFact<UserMessage>())
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "needs_research",
                selector: ctx => ctx.RequiresResearch ? 1.0 : 0.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new HasFact<ResearchResults>())  // Only if research done
            .WithAction(async ct =>
            {
                var research = rt.Bus.GetOrDefault<ResearchResults>();
                var userMsg = rt.Bus.GetOrDefault<UserMessage>();

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage($"Research data: {research?.Summary}"),
                    new UserChatMessage(userMsg?.Text ?? "")
                };

                var response = await _client.CompleteChatAsync(messages, ct);
                rt.Bus.Publish(new AssistantMessage(response.Value.Content[0].Text));
            });
    }
}
```

**Key Points:**
- Multiple strategies for LLM integration (direct response vs. research-enhanced)
- Uses EventBus history for conversation context
- Considerations check for required facts and confidence levels
- Actions publish results back to EventBus for other modules to use

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
├── Example/                # Complete task management demo (manual + attributes)
├── Tests/                  # 69 comprehensive tests
└── docs/                   # Architecture and integration guides
```

---

## 🎯 Use Cases

This framework is ideal for:

- **AI Agent Systems** - Coordinate multiple AI agents with shared and isolated state
- **LLM-Based Applications** - Build context from event history for prompts (see [AgentAssistant](./Example/AgentAssistant/))
- **Smart Home Automation** - Balance energy, security, comfort, and maintenance (see [SmartHomeAgent](./Example/SmartHomeAgent/))
- **Dynamic Workflows** - Let actions emerge from current state rather than hardcoding sequences
- **Game AI** - Classic utility AI for NPCs and decision-making
- **Task Orchestration** - Prioritize and execute tasks based on resources and constraints
- **Reactive Systems** - React to events with subscriptions while maintaining orchestrated behavior

---

## 🚨 Common Mistakes to Avoid

### ❌ Anti-Pattern: Looping Through Items as Proposals

**WRONG - Don't do this:**
```csharp
public IEnumerable<Proposal> Propose(Runtime rt)
{
    var tasks = GetAllTasks();

    // ❌ BAD: Yields 100+ proposals that differ only by data instance
    foreach (var task in tasks)
    {
        yield return ProposalHelper.For($"execute.{task.Id}")
            .WithValue("priority", task.Priority)
            .WithAction(async ct => await Execute(task, ct));
    }
    // This misuses the utility system for item selection!
}
```

**Why it's wrong:**
- Wastes CPU scoring 100+ similar proposals when only 1 executes
- Confuses "what to do" (capability) with "which data to process" (selection)
- The utility system is for choosing STRATEGIES, not choosing ITEMS

### ✅ Correct Pattern: Select Best Item First, Then Propose Strategies

**CORRECT - Do this instead:**
```csharp
public IEnumerable<Proposal> Propose(Runtime rt)
{
    var tasks = GetAllTasks();

    // ✅ GOOD: Select the best item FIRST using domain logic
    var bestTask = tasks
        .OrderByDescending(t => t.Priority)
        .ThenBy(t => t.SubmittedAt)
        .FirstOrDefault();

    if (bestTask == null) yield break;

    // Propose different STRATEGIES for handling this task

    // Strategy 1: Execute immediately (high priority)
    yield return ProposalHelper.For("task.execute_immediate")
        .WithValue("urgency", bestTask.Priority / 10.0)
        .WithValue("resources", GetResourceAvailability())
        .WithAction(async ct => await Execute(bestTask, ct));

    // Strategy 2: Batch with similar tasks (efficiency)
    var similarTasks = tasks.Where(t => t.Type == bestTask.Type).Take(5).ToList();
    if (similarTasks.Count > 1)
    {
        yield return ProposalHelper.For("task.execute_batched")
            .WithValue("efficiency", similarTasks.Count / 10.0)
            .WithValue("resources", GetResourceAvailability())
            .WithAction(async ct => await ExecuteBatch(similarTasks, ct));
    }
}
```

**Key principle:** One module = One CAPABILITY (e.g., "execute tasks"). Multiple proposals = Different STRATEGIES (e.g., "immediate" vs "batched").

**See comprehensive guidance:** [PROPOSAL_PATTERNS.md](./docs/PROPOSAL_PATTERNS.md)

---

## 🤝 Contributing

This is a framework - contributions that improve extensibility, documentation, or add well-tested features are welcome!

1. Fork the repository
2. Create a feature branch
3. Write tests for your changes
4. Submit a pull request

Please ensure all tests pass before submitting.

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
- 💬 [Issues](https://github.com/yourusername/UtilityAi/issues)
- 📧 [Discussions](https://github.com/yourusername/UtilityAi/discussions)
- 💡 [Example Projects](./Example/) - AgentAssistant & SmartHomeAgent

---

<p align="center">
  <sub>Built with ❤️ for the AI agent community</sub>
</p>
