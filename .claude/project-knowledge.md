# UtilityAI Framework - Project Knowledge

> This document contains deep understanding of the UtilityAI framework architecture, design philosophy, patterns, and implementation details. Use this as a reference when working with or extending the framework.

## 🎯 Core Philosophy & Design Principles

### The Capability-Based Architecture

UtilityAI follows a **capability-based** design pattern, not a data-driven or item-based pattern. This is the most critical concept to understand:

```
Capability Module = What the agent CAN DO (an ability)
   ├─ Proposal (Strategy 1) = One way to exercise that capability
   ├─ Proposal (Strategy 2) = Another way to exercise that capability
   └─ Proposal (Strategy 3) = Yet another way to exercise that capability
```

**Example:**
- **Capability Module:** "SendMessageModule" (ability to respond to users)
  - **Strategy 1:** Direct confident response (when confidence > 0.8)
  - **Strategy 2:** Clarifying question (when confidence < 0.5)
  - **Strategy 3:** Acknowledge and wait (when research is needed)

### The Central Anti-Pattern: Item Looping

**❌ NEVER DO THIS:**
```csharp
public IEnumerable<Proposal> Propose(Runtime rt)
{
    var tasks = GetAllTasks();
    foreach (var task in tasks)  // ❌ WRONG!
        yield return new Proposal($"execute.{task.Id}", ...);
}
```

**Why it's wrong:**
- Yields 100+ proposals that represent the SAME ACTION TYPE on different DATA
- Wastes CPU scoring similar proposals when only 1 executes
- Misuses the utility system as an item selector
- Confuses "what to do" with "which item to process"

**✅ CORRECT PATTERN:**
```csharp
public IEnumerable<Proposal> Propose(Runtime rt)
{
    var tasks = GetAllTasks();

    // 1. Select best item FIRST using domain logic
    var bestTask = tasks.OrderByDescending(t => t.Priority).FirstOrDefault();
    if (bestTask == null) yield break;

    // 2. Propose different STRATEGIES for that item
    yield return ProposalHelper.For("execute.immediate")
        .WithValue("urgency", bestTask.Priority / 10.0)
        .WithAction(async ct => await Execute(bestTask, ct));

    yield return ProposalHelper.For("execute.batched")
        .WithValue("efficiency", tasks.Count / 10.0)
        .WithAction(async ct => await ExecuteBatch(tasks.Take(5), ct));
}
```

---

## 🏗️ Architecture Components

### 1. EventBus (The Blackboard)

**What it is:** A type-safe publish/subscribe system that serves as the central state container.

**Key Features:**
- **Latest Value Storage:** Each type has ONE current value
- **History:** Timestamped events (configurable max per type, default 100)
- **Subscriptions:** React to events as they're published
- **Scoping:** Create child buses with isolated write state but inherited read access

**Design Pattern:** Blackboard Pattern from classical AI

**Thread Safety:** All operations are thread-safe (single lock)

**Usage Pattern:**
```csharp
// Publish facts
bus.Publish(new UserMessage("Hello"));

// Retrieve current fact
var msg = bus.GetOrDefault<UserMessage>();

// Access history (for LLM context building)
var history = bus.GetHistory<UserMessage>(maxItems: 10);

// Subscribe to events
using var sub = bus.Subscribe<UserMessage>(msg => {
    Console.WriteLine($"New message: {msg.Text}");
});

// Scoping for multi-agent systems
var agentBus = rootBus.CreateScope("agent-1");
agentBus.Publish(new AgentState("thinking"));  // Isolated
agentBus.TryGetWithFallback<GlobalConfig>(out var config);  // Falls back to parent
```

**Important:** Facts should be published to EventBus BEFORE orchestration starts. Sensors can then observe and publish additional facts each tick.

---

### 2. Runtime Context

**What it is:** An immutable record passed to all sensors, modules, and considerations.

```csharp
public sealed record Runtime(EventBus Bus, UserIntent Intent, int Tick);
```

**Fields:**
- `Bus` - Access to EventBus for reading/writing facts
- `Intent` - User's goal and parameters (request context)
- `Tick` - Current tick number (starts at 0)

**Design Principle:** Immutable context prevents side effects and makes code easier to reason about.

---

### 3. Sensors (ISensor)

**What they do:** Observe the environment and publish facts to EventBus each tick.

**Interface:**
```csharp
public interface ISensor
{
    Task SenseAsync(Runtime rt, CancellationToken ct);
}
```

**Design Guidelines:**
- Read from external sources (APIs, databases, files, system resources)
- Derive higher-level facts from existing EventBus state
- Publish zero or more facts per tick
- Should be stateless (all state in EventBus)
- Should be idempotent where possible

**Examples:**
- `TimeSensor` - Publishes current time facts
- `ResourceSensor` - Monitors CPU/memory usage
- `ConversationHistorySensor` - Tracks conversation metrics
- `EventFrequencySensor` - Counts event occurrences

**Execution:** Sensors run sequentially at the start of each tick.

---

### 4. Capability Modules (ICapabilityModule)

**What they do:** Propose candidate actions (strategies) based on current EventBus state.

**Interface:**
```csharp
public interface ICapabilityModule
{
    IEnumerable<Proposal> Propose(Runtime rt);
}
```

**Design Guidelines:**
- **Stateless** - All state must be in EventBus
- **Domain-focused** - Each module handles ONE capability area
- **Conditional** - Return empty if no actions are appropriate
- **Multiple proposals** - Yield different strategies, not different data items
- **No if-statements for scoring** - Use considerations instead

**Attribute-Based Registration:**
```csharp
[Capability(Priority = 100, Domain = "response")]
[RequiresFact<UserMessage>]
[RequiresFact<ConversationContext>]
public class SendMessageModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt) { /* ... */ }
}
```

**Attributes:**
- `[Capability]` - Marks for auto-discovery, sets priority
- `[RequiresFact<T>]` - Declares EventBus dependencies
- `[ActiveWhen]` - Conditional activation based on facts
- `[ConsiderAttribute]` - Declarative consideration attachment
- `[ProposalAction]` - Method-level proposal generation

**Discovery:**
```csharp
orchestrator.DiscoverCapabilities(Assembly.GetExecutingAssembly());
```

The framework uses topological sorting to register modules in dependency order.

---

### 5. Proposals

**What they are:** Candidate actions with utility scores.

**Structure:**
```csharp
public sealed class Proposal
{
    public string Id { get; }
    public double Prior { get; init; } = 1.0;          // Base tendency [0..1]
    public double Temperature { get; init; } = 1.0;    // >1=sharper, <1=flatter
    public IReadOnlyList<IConsideration> Considerations { get; }
    public IReadOnlyList<IEligibility> Eligibilities { get; }
    public Func<CancellationToken, Task> Act { get; }
}
```

**Utility Formula:**
```
utility = prior × (geometric_mean_of_considerations)^temperature
```

Where:
- `prior` - Base tendency for this action (0..1), default 1.0
- `geometric_mean` - nth root of product of all consideration scores
- `temperature` - Sharpens (>1) or flattens (<1) the curve

**Eligibility vs Considerations:**
- **Eligibility** - Hard gates. If ANY returns false, proposal is filtered out entirely
- **Considerations** - Soft scores. All contribute to final utility (0.0 to 1.0)

**ProposalHelper Fluent API:**
```csharp
yield return ProposalHelper.For("action.id")
    .WithConsideration(new HasFact<UserMessage>())
    .WithValue("priority", 0.8)
    .WithEligibility(new RequiresAuth())
    .WithPrior(0.9)
    .WithTemperature(1.2)
    .WithAction(async ct => { /* ... */ });
```

**Important:** The implicit conversion from `ProposalBuilder` to `Proposal` calls `.Build()` automatically.

---

### 6. Considerations (IConsideration)

**What they do:** Evaluate current state and return a score from 0.0 to 1.0.

**Interface:**
```csharp
public interface IConsideration
{
    string Name { get; }
    double Evaluate(Runtime rt);
}
```

**Design Principle:** ALL scoring logic should be in considerations, NOT in if-statements within `Propose()`.

**Built-in Considerations:**

#### General
- `HasFact<T>` - 1.0 if fact exists, 0.0 if not (can invert)
- `CurveSignal<T>` - Extracts signal from fact, applies response curve
- `ConstantValue` - Fixed value (for testing/baseline)
- `RandomValue` - Random value (for exploration)
- `WeightedRandomValue` - Weighted random selection

#### Threshold & Range
- `ThresholdValue` - Binary threshold (above/below)
- `RangeValue` - Scores based on ideal range
- `InverseValue` - Inverts another consideration

#### Time-Based
- `TimeSinceEvent` - Scores based on elapsed time
- `Cooldown` - Prevents rapid re-execution
- `TimeWindow` - Active only during specific periods

#### Collection-Based
- `CollectionSize` - Scores based on collection count
- `AnyMatch` - 1.0 if any item matches predicate
- `AllMatch` - 1.0 if all items match predicate

#### Composite
- `AndConsideration` - Logical AND of multiple considerations
- `OrConsideration` - Logical OR of multiple considerations
- `NotConsideration` - Logical NOT of a consideration

**Custom Consideration Pattern:**
```csharp
public sealed class SignalConsideration<T>(
    string name,
    Func<T, double> selector,
    Func<double, double> curve,
    (double min, double max) inputDomain) : IConsideration where T : notnull
{
    public string Name => name;

    public double Evaluate(Runtime rt)
    {
        var fact = rt.Bus.GetOrDefault<T>();
        if (fact == null) return 0.0;

        var rawValue = selector(fact);
        var normalized = (rawValue - inputDomain.min) / (inputDomain.max - inputDomain.min);
        var clamped = Math.Clamp(normalized, 0.0, 1.0);

        return curve(clamped);
    }
}
```

**Response Curves:**
- `x => x` - Linear (identity)
- `x => x * x` - Quadratic (accelerating)
- `x => Math.Sqrt(x)` - Square root (decelerating)
- `x => 1.0 / (1.0 + Math.Exp(-k * (x - x0)))` - Logistic S-curve
- `x => Math.Pow(x, gamma)` - Power curve
- Custom piecewise/cubic splines available in `Curves` class

---

### 7. Selection Strategy

**What it does:** Selects the winning proposal from scored candidates.

**Interface:**
```csharp
public interface ISelectionStrategy
{
    Proposal Select(IReadOnlyList<(Proposal, double utility)> scored, Runtime rt);
}
```

**Built-in:**
- `MaxUtilitySelection` (default) - Always picks highest utility
- Custom strategies can add randomness, epsilon-greedy, round-robin, etc.

---

### 8. Orchestration Sink (IOrchestrationSink)

**What it does:** Observes orchestration events without affecting behavior.

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

**Built-in:**
- `NullSink` - No-op (default)
- `RecordingSink` - Captures history for testing
- `CompositeSink` - Forwards to multiple sinks

**Use Cases:**
- Logging (console, file, structured)
- Metrics/telemetry (Prometheus, Application Insights)
- Testing assertions
- Debugging visualization

---

## 🔄 The Orchestration Loop

Each tick follows this sequence:

```
1. SENSE
   ├─ All sensors run sequentially
   ├─ Each publishes facts to EventBus
   └─ Facts are available for next phase

2. PROPOSE
   ├─ All modules run sequentially
   ├─ Each yields zero or more proposals
   └─ Proposals collected into list

3. FILTER
   ├─ Check eligibility for each proposal
   └─ Remove ineligible proposals

4. SCORE
   ├─ Evaluate considerations for each proposal
   ├─ Calculate utility using formula
   └─ Sort by utility (descending)

5. SELECT
   ├─ Selection strategy picks winner
   └─ Handle ties (usually first by registration order)

6. ACT
   ├─ Execute chosen proposal's action
   ├─ Action may publish new facts to EventBus
   └─ OnActed sink notification

7. CHECK STOP CONDITIONS
   ├─ Max ticks reached?
   ├─ No eligible proposals?
   ├─ StopOrchestrationEvent published?
   └─ Loop back to step 1 or stop
```

**Important Notes:**
- Each phase has full access to current EventBus state
- Actions can modify EventBus (publish new facts)
- Next tick sees updated EventBus state
- Single-threaded execution (one tick at a time)

---

## 🎨 Design Patterns & Best Practices

### Pattern 1: Capability-Based Modules

```csharp
// ✅ CORRECT: One module per capability
[Capability(Priority = 100)]
[RequiresFact<UserMessage>]
public class RespondModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        // Strategy 1: Direct response
        yield return ProposalHelper.For("respond.direct")
            .WithDescription("Sends a direct, confident response to the user's message")
            .WithConsideration(new SignalConsideration<Context>(
                name: "confidence",
                selector: c => c.Confidence,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithAction(async ct => await RespondDirectly(rt, ct));

        // Strategy 2: Ask for clarification
        yield return ProposalHelper.For("respond.clarify")
            .WithDescription("Asks the user for clarification when their intent is ambiguous")
            .WithConsideration(new SignalConsideration<Context>(
                name: "ambiguity",
                selector: c => 1.0 - c.Confidence,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithAction(async ct => await AskClarification(rt, ct));
    }
}
```

### Pattern 2: Item Selection First, Then Strategy

```csharp
// ✅ CORRECT: Select item using domain logic, then propose strategies
public IEnumerable<Proposal> Propose(Runtime rt)
{
    var tasks = rt.Bus.GetOrDefault<TaskQueue>()?.Tasks ?? [];

    // Domain-specific selection
    var urgentTask = tasks
        .Where(t => t.Priority > 8)
        .OrderBy(t => t.Deadline)
        .FirstOrDefault();

    if (urgentTask != null)
    {
        // Propose strategies for THIS task
        yield return ProposalHelper.For("task.execute_urgent")
            .WithValue("urgency", 1.0)
            .WithAction(async ct => await Execute(urgentTask, ct));
    }

    // Different capability dimension
    var batchable = tasks.Where(t => t.CanBatch).ToList();
    if (batchable.Count >= 3)
    {
        yield return ProposalHelper.For("task.batch_execute")
            .WithValue("efficiency", batchable.Count / 10.0)
            .WithAction(async ct => await ExecuteBatch(batchable, ct));
    }
}
```

### Pattern 3: Declarative Considerations (No If-Statements)

```csharp
// ❌ WRONG: If-statements in Propose()
public IEnumerable<Proposal> Propose(Runtime rt)
{
    var context = rt.Bus.GetOrDefault<Context>();
    if (context == null) yield break;

    if (context.Confidence > 0.8 && !context.RequiresResearch)  // ❌ Bad!
    {
        yield return ProposalHelper.For("respond")
            .WithValue("score", 1.0)
            .WithAction(async ct => await Respond(rt, ct));
    }
}

// ✅ CORRECT: Use considerations for all logic
public IEnumerable<Proposal> Propose(Runtime rt)
{
    // Always propose, let considerations do the scoring
    yield return ProposalHelper.For("respond")
        .WithConsideration(new SignalConsideration<Context>(
            name: "confidence",
            selector: c => c.Confidence,
            curve: x => x,
            inputDomain: (0, 1)))
        .WithConsideration(new SignalConsideration<Context>(
            name: "no_research_needed",
            selector: c => c.RequiresResearch ? 0.0 : 1.0,
            curve: x => x,
            inputDomain: (0, 1)))
        .WithAction(async ct => await Respond(rt, ct));
}
```

**Why this matters:**
- Considerations make scoring transparent and debuggable
- Sinks can log consideration values
- Easy to tune without changing code
- Can extract consideration weights to config

### Pattern 4: LLM Context from EventBus History

```csharp
public async Task RespondWithContext(Runtime rt, CancellationToken ct)
{
    // Build conversation context from EventBus history
    var userHistory = rt.Bus.GetHistory<UserMessage>(maxItems: 10);
    var assistantHistory = rt.Bus.GetHistory<AssistantMessage>(maxItems: 10);

    var messages = userHistory
        .Zip(assistantHistory, (u, a) => new[]
        {
            new UserChatMessage(u.Value.Text),
            new AssistantChatMessage(a.Value.Text)
        })
        .SelectMany(x => x)
        .ToList();

    var response = await _llmClient.CompleteChatAsync(messages, ct);
    rt.Bus.Publish(new AssistantMessage(response.Content));
}
```

### Pattern 5: Multi-Agent with Scoped Buses

```csharp
var rootBus = new EventBus();
rootBus.Publish(new GlobalConfig(/* shared config */));

var agent1Bus = rootBus.CreateScope("agent-1");
var agent2Bus = rootBus.CreateScope("agent-2");

// Each agent has isolated state
var orch1 = new UtilityAiOrchestrator(bus: agent1Bus)
    .DiscoverCapabilities(assembly);

var orch2 = new UtilityAiOrchestrator(bus: agent2Bus)
    .DiscoverCapabilities(assembly);

// Agents can read shared facts via fallback
agent1Bus.TryGetWithFallback<GlobalConfig>(out var config);
```

### Pattern 6: Testing with Sinks

```csharp
[Fact]
public async Task Should_Choose_Highest_Utility_Action()
{
    // Arrange
    var bus = new EventBus();
    bus.Publish(new TestFact(Priority: 0.9));

    var sink = new RecordingSink();
    var orch = new UtilityAiOrchestrator(bus: bus)
        .AddModule(new TestModule());

    // Act
    await orch.RunAsync(
        new UserIntent("test"),
        maxTicks: 1,
        CancellationToken.None,
        sink);

    // Assert
    Assert.Single(sink.ChosenProposals);
    Assert.Equal("expected.action", sink.ChosenProposals[0].Id);
    Assert.True(sink.ChosenProposals[0].Utility > 0.8);
}
```

---

## 🧠 Understanding Utility AI Theory

### Why Geometric Mean?

The framework uses **geometric mean** of considerations, not arithmetic mean:

```
Arithmetic: (a + b + c) / 3
Geometric:  (a × b × c)^(1/3)
```

**Why geometric mean is better:**
- **Compensatory behavior** - One high score can't compensate for one zero
- **Non-linear scaling** - Small changes in low scores have big impact
- **Natural gating** - Any zero consideration makes utility zero
- **Reflects AND logic** - All considerations must be satisfied

**Example:**
```
Considerations: [0.9, 0.8, 0.0]  (one is zero)
Arithmetic mean: 0.57  (medium score, might still win)
Geometric mean:  0.0   (correctly zeroed out)

Considerations: [0.9, 0.8, 0.7]  (all decent)
Arithmetic mean: 0.80
Geometric mean:  0.79  (similar, but slightly lower)
```

### Temperature Parameter

Temperature controls how "decisive" the scoring is:

```
utility = prior × (geometric_mean)^temperature
```

- **temperature = 1.0** (default) - Normal curve
- **temperature > 1.0** - Sharper curve (aggressive, decisive)
- **temperature < 1.0** - Flatter curve (conservative, exploratory)

**Example with considerations = [0.8, 0.7, 0.6] → geom_mean ≈ 0.70:**
```
temp = 0.5: utility = 0.84  (flatter, more exploration)
temp = 1.0: utility = 0.70  (normal)
temp = 2.0: utility = 0.49  (sharper, more decisive)
```

**When to adjust:**
- Increase temperature when you want clear winners (production)
- Decrease temperature for exploration/testing

### Prior Probability

The `prior` parameter represents the base tendency for an action:

```csharp
yield return ProposalHelper.For("fallback")
    .WithPrior(0.1)  // Low prior - only wins if others fail
    .WithAction(async ct => await Fallback(ct));

yield return ProposalHelper.For("primary")
    .WithPrior(0.9)  // High prior - preferred when equal
    .WithAction(async ct => await Primary(ct));
```

**Use cases:**
- Fallback actions (low prior)
- Preferred strategies (high prior)
- Breaking ties between equal utilities

---

## 📊 Performance Considerations

### EventBus Performance

**Lock Granularity:** Single lock for simplicity
- ✅ Good for: Most scenarios (< 1000 ops/sec)
- ❌ Consider custom: High-contention scenarios with many threads

**History Size:** Default 100 items per type
- Memory impact: O(types × history_size × item_size)
- Tune based on: Memory constraints, lookup patterns
- GC impact: Moderate (circular buffer, no allocations after warmup)

**Subscription Overhead:** Handlers execute synchronously during `Publish()`
- Keep handlers lightweight (< 1ms)
- Avoid I/O in handlers
- Use async queues for heavy work

### Proposal Scoring

**Cost:** O(modules × proposals_per_module × considerations_per_proposal)

**Optimization strategies:**
1. **Eligibility early-exit** - Use `IEligibility` for hard gates
2. **Conditional proposing** - Check cheap facts before yielding
3. **Cache in EventBus** - Expensive computations → publish as facts
4. **Limit proposals** - 3-5 per module is typical

**Example:**
```csharp
public IEnumerable<Proposal> Propose(Runtime rt)
{
    // Cheap check first
    var userMsg = rt.Bus.GetOrDefault<UserMessage>();
    if (userMsg == null) yield break;  // Early exit

    // Expensive check → cache result in EventBus via sensor
    var analysis = rt.Bus.GetOrDefault<MessageAnalysis>();  // Cached

    // Only propose if makes sense
    if (analysis?.RequiresResponse == true)
    {
        yield return ProposalHelper.For("respond")...;
    }
}
```

### Sensor Performance

**Execution:** Sequential, blocking
- Prefer async I/O over sync
- Consider caching (time-based, event-based)
- Monitor total sensor time (should be < 10% of tick time)

**Example caching pattern:**
```csharp
public class CachedApiSensor : ISensor
{
    private DateTime _lastFetch = DateTime.MinValue;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(30);

    public async Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        if (DateTime.UtcNow - _lastFetch < _cacheDuration)
            return;  // Use cached value in EventBus

        var data = await FetchFromApi(ct);
        rt.Bus.Publish(data);
        _lastFetch = DateTime.UtcNow;
    }
}
```

---

## 🔍 Capability Introspection

### Introspecting Available Capabilities

The orchestrator provides a `GetCapabilitiesInfo(Runtime rt)` method that allows you to introspect all registered capabilities and their potential actions. This is essential for:

1. **LLM Planning** - An agent can ask "what can I do?" and receive structured information
2. **Debugging** - Understanding what modules are registered and what they propose
3. **Documentation** - Auto-generating capability documentation
4. **Slack Bots & Conversational Agents** - Explaining available actions to users

### API

```csharp
public IReadOnlyList<CapabilityInfo> GetCapabilitiesInfo(Runtime rt)
```

**Returns:**
- `CapabilityInfo` - Module name, type, and list of potential actions
  - `ProposalInfo` - Action ID, description, prior, temperature, consideration names, eligibility names

### Usage Pattern

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
        Console.WriteLine($"    Prior: {action.Prior}");
        Console.WriteLine($"    Temperature: {action.Temperature}");
        Console.WriteLine($"    Considerations: {string.Join(", ", action.ConsiderationNames)}");
        Console.WriteLine($"    Eligibilities: {string.Join(", ", action.EligibilityNames)}");
    }
}
```

### Adding Descriptions to Proposals

Use `.WithDescription()` in the ProposalHelper fluent API:

```csharp
yield return ProposalHelper.For("respond.direct")
    .WithDescription("Sends a direct, confident response to the user's message")
    .WithConsideration(new HasFact<UserMessage>())
    .WithValue("confidence", 0.9)
    .WithAction(async ct => await RespondDirectly(rt, ct));
```

### LLM Planning Example

```csharp
// LLM asks: "What can I do in this situation?"
var capabilities = orchestrator.GetCapabilitiesInfo(rt);

var prompt = $@"
Available capabilities:
{string.Join("\n", capabilities.SelectMany(c =>
    c.PotentialActions.Select(a =>
        $"- {a.ProposalId}: {a.Description ?? "No description"}")))}

User request: {userMessage}

Which action should I take and what considerations matter?
";

var llmResponse = await llmClient.Complete(prompt);
```

### Use Case: Slack Bot State Management

For a Slack bot that DMs users:

1. **Store state per user** using scoped EventBus:
   ```csharp
   var userBus = rootBus.CreateScope($"slack-user-{userId}");
   ```

2. **Persist state** between messages:
   - Use a sensor to load previous state at orchestration start
   - Use a low-priority proposal to save state after actions

3. **Introspect capabilities** to let users ask "what can you do?":
   ```csharp
   if (message.Text == "help")
   {
       var caps = orchestrator.GetCapabilitiesInfo(rt);
       var response = "I can:\n" + string.Join("\n",
           caps.SelectMany(c => c.PotentialActions.Select(a =>
               $"• {a.Description ?? a.ProposalId}")));
       await slack.SendDM(userId, response);
   }
   ```

### Important Notes

- **GetCapabilitiesInfo calls Propose()** - The Runtime you pass determines which proposals are generated
- **State matters** - Different EventBus facts may result in different proposals
- **Description is optional** - If not set via `.WithDescription()`, it will be null
- **No action execution** - Introspection doesn't execute actions, only examines proposals

---

## 🧠 LLM Intent Interpretation (Chat Applications)

### The Problem

In chat applications, you need to:
1. Understand what the user wants (intent interpretation)
2. Bootstrap the utility system with the right initial state
3. Let the system naturally react to execute the right actions

**Naive approach (WRONG):** Ask LLM "what action should I execute?" → LLM picks action IDs → Bypasses utility system

**Correct approach:** LLM interprets intent and publishes facts → Utility system scores naturally → Right action wins

### The Solution: LlmIntentSensor

```csharp
using UtilityAi.Sensor.LLM;

// 1. Implement ILlmClient adapter for your LLM library
public class OpenAIAdapter : ILlmClient
{
    private readonly ILanguageModel _llm;

    public async Task<string> GenerateAsync(string prompt, CancellationToken ct)
    {
        return await _llm.GenerateAsync(prompt, ct);
    }
}

// 2. Add sensor to orchestrator
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddSensor(LlmIntentSensor.ForMessageType<UserMessage>(
        new OpenAIAdapter(llm),
        msg => msg.Text
    ))
    .AddModule(new QueryModule())
    .AddModule(new UpdateModule());

// 3. Modules react to IntentAnalysis facts naturally
public class QueryModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var analysis = rt.Bus.GetOrDefault<IntentAnalysis>();

        yield return ProposalHelper.For("execute-query")
            .WithConsideration(new SignalConsideration<IntentAnalysis>(
                "query-intent",
                a => a.Intent.StartsWith("query.") ? 1.0 : 0.0,
                x => x,
                (0, 1)))
            .WithConsideration(new SignalConsideration<IntentAnalysis>(
                "confidence",
                a => a.Confidence,
                x => x,
                (0, 1)))
            .WithAction(async ct => await ExecuteQuery(rt, ct));
    }
}
```

### What the LLM Does

1. **Analyzes the user message** - Extracts intent and entities
2. **Publishes structured facts** - `IntentAnalysis` with intent string, entity dictionary, and confidence score
3. **That's it** - LLM doesn't pick actions, doesn't know about action IDs

### What the Utility System Does

1. **Modules check for `IntentAnalysis`** - Like any other EventBus fact
2. **Considerations score based on intent** - Natural utility-based decision making
3. **Highest utility wins** - The right action executes automatically

### The IntentAnalysis Fact

```csharp
public sealed record IntentAnalysis(
    string Intent,                          // e.g., "query.sales", "update.customer"
    IReadOnlyDictionary<string, object> Entities,  // e.g., { "timeRange": "last_month" }
    double Confidence                        // 0.0 - 1.0
);
```

### Example Flow

```
User: "Show me top customers from last month"
         ↓
LlmIntentSensor analyzes
         ↓
Publishes: IntentAnalysis(
    Intent: "query.customers",
    Entities: { "timeRange": "last_month", "sortBy": "revenue" },
    Confidence: 0.95
)
         ↓
QueryModule scores high (has considerations for query intents)
UpdateModule scores low (no match)
         ↓
QueryModule's "execute-query" action wins
         ↓
Action executes, publishes QueryResult to EventBus
```

### Optional: Include Capabilities Context

```csharp
// LLM sees what actions are available (helps with intent interpretation)
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddSensor(LlmIntentSensor.ForMessageType<UserMessage>(
        llmClient,
        msg => msg.Text,
        includeCapabilities: true  // LLM sees available actions
    ))
    .AddModule(new QueryModule());

// You need to publish CapabilitiesSnapshot
var capabilities = orchestrator.GetCapabilitiesInfo(runtime);
bus.Publish(new CapabilitiesSnapshot(capabilities));
```

### Testing Intent Analysis

```csharp
[Fact]
public async Task IntentSensor_PublishesAnalysis()
{
    // Arrange
    var bus = new EventBus();
    bus.Publish(new UserMessage("Show sales data"));

    var mockLlm = new MockLlmClient(@"{
        ""intent"": ""query.sales"",
        ""entities"": { ""dataType"": ""sales"" },
        ""confidence"": 0.9
    }");

    var sensor = LlmIntentSensor.ForMessageType<UserMessage>(
        mockLlm,
        msg => msg.Text
    );

    var rt = new Runtime(bus, intent, 1);

    // Act
    await sensor.SenseAsync(rt, CancellationToken.None);

    // Assert
    var analysis = bus.GetOrDefault<IntentAnalysis>();
    Assert.Equal("query.sales", analysis.Intent);
    Assert.Equal(0.9, analysis.Confidence);
}
```

### Why This Approach is Better

**✅ Maintains utility AI principles**
- LLM provides data (facts), utility system makes decisions
- Actions compete naturally based on scoring
- No coupling between LLM and action IDs

**✅ Clean separation of concerns**
- LLM: Natural language understanding
- Utility System: Action selection
- Each does what it's best at

**✅ Testable**
- Mock the LLM easily
- Test modules independently
- Clear fact-based architecture

**✅ Extensible**
- Add new modules without touching LLM code
- Change intent interpretation independently
- Scale to complex multi-module systems

### When to Use This

**Use LLM intent interpretation when:**
- Building chat applications
- User input is natural language
- Need to bootstrap initial EventBus state
- Want intelligent intent classification

**Skip LLM intent interpretation when:**
- Input is already structured (APIs, forms)
- Intent is obvious from context
- Performance is critical
- Cost constraints (LLM calls are expensive)

---

## 🔧 Extension Points

### For Framework Users

1. **Implement ISensor** - Domain-specific observation
2. **Implement ICapabilityModule** - Domain actions
3. **Implement IConsideration** - Custom scoring logic
4. **Implement IEligibility** - Hard gates
5. **Implement ISelectionStrategy** - Custom selection (epsilon-greedy, softmax)
6. **Implement IOrchestrationSink** - Observability
7. **Implement ILlmClient** - LLM adapters for intent analysis
8. **Extend EventBus** - Custom persistence, distribution

### For Framework Developers

**Current architecture supports:**
- ✅ Custom considerations via `IConsideration`
- ✅ Custom curves via `Func<double, double>`
- ✅ Custom selection via `ISelectionStrategy`
- ✅ Plugin modules via assembly scanning
- ✅ Event history and subscriptions
- ✅ Scoped buses for multi-agent

**Future extensibility:**
- Consider: Async considerations (I/O-bound scoring)
- Consider: Parallel proposal scoring
- Consider: Distributed EventBus (Redis/pub-sub)
- Consider: Consideration composition DSL
- Consider: Visual debugging tools

---

## 🎓 Teaching the Framework

### Onboarding Checklist

When introducing someone to UtilityAI, cover in this order:

1. **Core concept** - Utility-based decision making (game AI origins)
2. **EventBus** - Central state container, type-safe facts
3. **Orchestration loop** - Sense → Propose → Score → Act
4. **Capability vs Strategy** - THE critical distinction
5. **Considerations** - Declarative scoring, no if-statements
6. **ProposalHelper** - Fluent API for clean code
7. **Anti-patterns** - Item looping, if-statement scoring
8. **Examples** - Walk through Example/AgentAssistant

### Common Misunderstandings

❌ **"I create one module per item"**
→ ✅ Create one module per capability, select items within

❌ **"I use if-statements to decide what to propose"**
→ ✅ Always propose, use considerations to score

❌ **"Proposals are the items to process"**
→ ✅ Proposals are strategies for capabilities

❌ **"I need lots of modules"**
→ ✅ Typically 3-10 modules per agent is enough

❌ **"EventBus is just for inter-module communication"**
→ ✅ EventBus is the SINGLE source of truth for ALL state

### Visual Mental Model

```
┌─────────────────────────────────────────────┐
│              CAPABILITIES                    │
│  (What the agent CAN DO)                    │
│                                             │
│  ┌──────────────┐  ┌──────────────┐       │
│  │   Respond    │  │   Research   │       │
│  │   Module     │  │   Module     │       │
│  └──────┬───────┘  └──────┬───────┘       │
│         │                  │                │
│         ├─ Direct          ├─ Web          │
│         ├─ Clarify         ├─ Database     │
│         └─ Acknowledge     └─ Cached       │
│           (strategies)       (strategies)   │
└─────────────────────────────────────────────┘
                   ↓
┌─────────────────────────────────────────────┐
│         UTILITY SYSTEM PICKS BEST           │
│    (Between capabilities AND strategies)    │
└─────────────────────────────────────────────┘
```

---

## 📚 References & Further Reading

### Utility AI Theory
- "Behavioral Mathematics for Game AI" - Dave Mark, 2009
- "Building Better AI: An Introduction to Utility Theory" - GDC talks
- "Architecture Tricks: Managing Behaviors in Time, Space, and Depth" - Kevin Dill

### Related Patterns
- **Blackboard Pattern** - Classical AI (EventBus implementation)
- **Strategy Pattern** - GoF (Proposals as strategies)
- **Chain of Responsibility** - GoF (Module ordering)

### Inspirations Cited in Codebase
- Microsoft Semantic Kernel
- LangGraph (LangChain)
- AutoGen
- Spring Framework (Attribute-based registration)
- Jakarta EE (Annotations)

---

## 🔍 Troubleshooting Guide

### Issue: Too many proposals, slow scoring

**Symptoms:** RunAsync takes too long, high CPU

**Diagnosis:**
1. Log proposal count per tick: `sink.OnScored` → count proposals
2. Check if any module loops through items

**Solution:**
- Apply "select first, then propose" pattern
- Use eligibility for hard gates
- Cache expensive computations in EventBus

### Issue: Wrong action chosen

**Symptoms:** Unexpected proposal wins

**Diagnosis:**
1. Add detailed logging sink
2. Log all proposal utilities
3. Check consideration values

**Solution:**
- Adjust consideration curves
- Add missing considerations
- Adjust temperature or prior

### Issue: No proposals eligible

**Symptoms:** Orchestration stops immediately

**Diagnosis:**
1. Check EventBus state at tick start
2. Verify required facts exist
3. Check eligibility conditions

**Solution:**
- Publish required facts before orchestration
- Make eligibility less strict
- Add fallback module with no requirements

### Issue: Memory leak

**Symptoms:** Memory grows over time

**Diagnosis:**
1. Check EventBus history size
2. Check subscription disposal
3. Profile allocations

**Solution:**
- Reduce history size: `new EventBus(maxHistoryPerType: 50)`
- Ensure subscriptions are disposed: `using var sub = ...`
- Clear EventBus periodically: `bus.Clear<T>()`

---

## ✅ Framework Design Validation

The current implementation successfully:

✅ Separates **what to do** (capability) from **how to decide** (utility)
✅ Makes state explicit and centralized (EventBus)
✅ Supports multiple strategies per capability
✅ Enables declarative scoring (considerations)
✅ Provides clean fluent API (ProposalHelper)
✅ Supports attribute-based registration
✅ Enables testing via sinks
✅ Supports multi-agent via scoping
✅ Maintains thread safety
✅ Has comprehensive documentation

The framework correctly implements Utility AI theory while providing modern C# ergonomics.

---

**Last Updated:** 2026-02-17
**Framework Version:** Current (as of analysis)
**Document Maintainer:** Generated from comprehensive codebase analysis
