# UtilityAI Framework Architecture

## Overview

UtilityAI is a framework for building AI agent orchestration systems using the classic **Utility AI** decision-making pattern. The framework provides abstractions for evaluating and selecting actions based on the current state, without prescribing specific AI implementations.

## Core Concepts

### The Orchestration Loop

Every tick, the orchestrator executes this sequence:

```
┌─────────────┐
│   SENSE     │  Sensors observe environment, publish facts to EventBus
└──────┬──────┘
       │
┌──────▼──────┐
│  PROPOSE    │  Modules generate candidate actions (Proposals)
└──────┬──────┘
       │
┌──────▼──────┐
│   SCORE     │  Each proposal is evaluated based on Considerations
└──────┬──────┘
       │
┌──────▼──────┐
│   SELECT    │  Highest-utility eligible proposal is chosen
└──────┬──────┘
       │
┌──────▼──────┐
│    ACT      │  Chosen proposal's action executes, publishes new facts
└─────────────┘
```

## Component Architecture

### 1. EventBus (Blackboard Pattern)

The `EventBus` is the central state container. It's a type-safe publish/subscribe system.

**Features:**
- **Latest Value Storage**: Each type has one "latest" value
- **History**: Timestamped event history per type (configurable max items)
- **Subscriptions**: React to events as they're published
- **Scoping**: Create child buses with isolated write state but inherited read access

**Use Cases:**
```csharp
// Basic publish/subscribe
bus.Publish(new UserMessage("Hello"));
var msg = bus.GetOrDefault<UserMessage>();

// History access (NEW in v2)
var history = bus.GetHistory<UserMessage>(maxItems: 10);
foreach (var evt in history)
    Console.WriteLine($"{evt.Timestamp}: {evt.Value.Text}");

// Subscriptions (NEW in v2)
using var sub = bus.Subscribe<UserMessage>(msg => {
    Console.WriteLine($"New message: {msg.Text}");
});

// Scoping (NEW in v2)
var agentBus = bus.CreateScope("agent-1");
agentBus.Publish(new AgentState("busy")); // Isolated to this scope
agentBus.TryGetWithFallback<GlobalConfig>(out var config); // Falls back to parent
```

**Thread Safety**: All operations are thread-safe.

---

### 2. Runtime

An immutable context passed to sensors, modules, and considerations:

```csharp
public sealed record Runtime(EventBus Bus, UserIntent Intent, int Tick);
```

- **Bus**: Access to the current EventBus state
- **Intent**: The user's goal and parameters for this orchestration session
- **Tick**: Current tick number (starts at 0)

---

### 3. Sensors (ISensor)

Sensors observe the environment and publish facts to the EventBus.

**Interface:**
```csharp
public interface ISensor
{
    Task SenseAsync(Runtime rt, CancellationToken ct);
}
```

**Design Guidelines:**
- Read from external sources (APIs, databases, files)
- Derive higher-level facts from existing EventBus state
- Publish zero or more facts per tick
- Should be idempotent where possible

**Example:**
```csharp
public class ResourceMonitorSensor : ISensor
{
    public Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        var cpuUsage = GetCpuUsage();
        var memoryUsage = GetMemoryUsage();

        rt.Bus.Publish(new ResourceSnapshot(cpuUsage, memoryUsage));
        return Task.CompletedTask;
    }
}
```

---

### 4. Capability Modules (ICapabilityModule)

Modules propose candidate actions based on the current state.

**Interface:**
```csharp
public interface ICapabilityModule
{
    IEnumerable<Proposal> Propose(Runtime rt);
}
```

**Design Guidelines:**
- **Stateless**: All state should be in the EventBus
- **Domain-focused**: Each module handles one capability area
- **Conditional**: Return empty if no actions are appropriate
- **Multiple proposals**: Can propose multiple variants with different considerations

**Example:**
```csharp
public class ValidationModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var tasks = rt.Bus.GetOrDefault<TaskQueue>();
        if (tasks == null) yield break;

        foreach (var task in tasks.Pending)
        {
            yield return new Proposal(
                id: $"validate.{task.Id}",
                cons: new[]
                {
                    new HasFact<TaskQueue>(),
                    new CurveSignal<TaskPriority>(
                        t => t.Value,
                        Curves.Logistic(k: 2, x0: 0.5))
                },
                act: async ct => {
                    var result = await ValidateTask(task, ct);
                    rt.Bus.Publish(result);
                }
            );
        }
    }
}
```

---

### 5. Proposals

A **Proposal** represents a candidate action with a utility score.

**Structure:**
```csharp
public sealed class Proposal
{
    public string Id { get; }
    public double Prior { get; init; } = 1.0;          // Base tendency (0..1)
    public double Temperature { get; init; } = 1.0;    // >1 = sharper, <1 = flatter
    public IReadOnlyList<IConsideration> Considerations { get; }
    public IReadOnlyList<IEligibility> Eligibilities { get; }
    public Func<CancellationToken, Task> Act { get; }

    public double Utility(Runtime rt);  // Calculates final score
    public bool IsEligible(Runtime rt); // Hard gates
}
```

**Utility Calculation:**
```
utility = prior × (geometric_mean_of_considerations) ^ temperature
```

**Eligibility** (hard gates):
- If any eligibility returns `false`, the proposal is filtered out entirely
- Use for requirements that must be met (e.g., "has authentication", "resource available")

---

### 6. Considerations (IConsideration)

Considerations evaluate the current state and return a score from 0.0 to 1.0.

**Interface:**
```csharp
public interface IConsideration
{
    double Evaluate(Runtime rt);
}
```

**Built-in Considerations:**

#### `HasFact<T>`
```csharp
new HasFact<UserMessage>()           // 1.0 if exists, 0.0 if not
new HasFact<UserMessage>(invert: true)  // 0.0 if exists, 1.0 if not
```

#### `CurveSignal<T>`
```csharp
new CurveSignal<TaskAge>(
    selector: t => t.Seconds / 60.0,  // Extract signal
    curve: Curves.Logistic(k: 2, x0: 30),  // Map to 0..1
    inputDomain: (0, 60),
    outputDomain: (0, 1)
)
```

**Response Curves:**
- `Curves.Identity()` - Linear mapping
- `Curves.Logistic(k, x0)` - S-curve (k controls steepness, x0 is midpoint)
- `Curves.Power(gamma)` - Exponential (gamma > 1 = aggressive, < 1 = conservative)
- `Curves.PiecewiseLinear(points)` - Custom keyframe curve
- `Curves.MonotoneCubic(points)` - Smooth interpolation

---

### 7. Selection Strategies (ISelectionStrategy)

After scoring, a strategy selects the winning proposal.

**Interface:**
```csharp
public interface ISelectionStrategy
{
    Proposal Select(IReadOnlyList<(Proposal, double utility)> scored, Runtime rt);
}
```

**Built-in:**
- `MaxUtilitySelection` (default) - Always picks highest utility
- Custom strategies can add randomness, round-robin, etc.

---

### 8. Observability (IOrchestrationSink)

Sinks observe the orchestration process without affecting behavior.

**Interface:**
```csharp
public interface IOrchestrationSink
{
    void OnTickStart(Runtime rt);
    void OnScored(Runtime rt, IReadOnlyList<(Proposal, double)> scored);
    void OnChosen(Runtime rt, Proposal chosen, double utility);
    void OnActed(Runtime rt, Proposal chosen);
    void OnStopped(Runtime rt, OrchestrationStopReason reason);
}
```

**Built-in Sinks:**
- `NullSink` - No-op (default)
- `RecordingSink` - Captures history for analysis
- `CompositeSink` - Forwards to multiple sinks

**Use Cases:**
- Logging
- Metrics/telemetry
- Testing assertions
- Debugging

---

## Data Flow Diagram

```
┌─────────────────────────────────────────────────────┐
│                    EventBus                         │
│  ┌───────────────┐  ┌────────────┐  ┌────────────┐ │
│  │ Latest Values │  │  History   │  │Subscribers │ │
│  └───────────────┘  └────────────┘  └────────────┘ │
└──────────▲─────────────────────┬────────────────────┘
           │                     │
    ┌──────┴──────┐       ┌─────▼──────┐
    │   Sensors   │       │   Modules  │
    │             │       │            │
    │ - Monitor   │       │ - Propose  │
    │ - Derive    │       │ - Check    │
    │ - Publish   │       │ - Score    │
    └─────────────┘       └─────┬──────┘
                                │
                         ┌──────▼──────┐
                         │  Proposals  │
                         │             │
                         │ + Prior     │
                         │ + Considers │
                         │ + Action    │
                         └──────┬──────┘
                                │
                         ┌──────▼──────┐
                         │ Orchestrator│
                         │             │
                         │ Score →     │
                         │ Select →    │
                         │ Act         │
                         └─────────────┘
```

---

## Extension Points

### For Framework Users

1. **Implement ISensor** - Observe your domain
2. **Implement ICapabilityModule** - Propose domain actions
3. **Create Considerations** - Custom scoring logic
4. **Implement ISelectionStrategy** - Custom selection (e.g., epsilon-greedy)
5. **Implement IOrchestrationSink** - Custom observability
6. **Use EventBus Scoping** - Isolate multi-agent state

### Design Patterns

#### Pattern: Conversation History
```csharp
// Use EventBus history for LLM context
var history = bus.GetHistory<UserMessage>(maxItems: 10);
var context = history.Select(e => e.Value.Text).ToArray();
```

#### Pattern: Per-Agent State
```csharp
// Create scoped bus for each agent
var agent1Bus = rootBus.CreateScope("agent-1");
var agent2Bus = rootBus.CreateScope("agent-2");

// Agents have isolated state but can read shared facts
agent1Bus.Publish(new AgentStatus("thinking"));
rootBus.Publish(new SharedKnowledge("fact"));

agent1Bus.TryGet<AgentStatus>(out var status);  // Gets agent-1's status
agent1Bus.TryGetWithFallback<SharedKnowledge>(out var knowledge);  // Falls back to root
```

#### Pattern: Event Reactions
```csharp
// Subscribe to react to specific events
using var sub = bus.Subscribe<TaskCompleted>(task => {
    logger.LogInformation($"Task {task.Id} completed");
    metrics.RecordCompletion(task.Duration);
});
```

---

## Performance Considerations

### EventBus
- **Lock granularity**: Single lock for simplicity. For high-contention scenarios, consider custom implementation with finer locks
- **History size**: Default 100 items per type. Tune based on memory constraints
- **Subscription overhead**: Subscribers execute synchronously during `Publish()`. Keep handlers lightweight.

### Sensors
- Run sequentially each tick
- Use `async` for I/O-bound operations
- Consider caching to reduce external calls

### Proposals
- Scored every tick (can be expensive with many proposals)
- Consider using eligibilities to filter early
- Cache expensive computations in EventBus facts

---

## Thread Safety

- **EventBus**: Thread-safe for all operations
- **Orchestrator**: Single-threaded execution loop (one tick at a time)
- **Sensors/Modules**: Should be stateless; mutable state → EventBus

---

## Next Steps

- See [INTEGRATION.md](./INTEGRATION.md) for connecting to AI services
- See [Examples](../Example/) for complete implementations
- See [API Reference](../UtilityAi/README.md) for detailed API docs
