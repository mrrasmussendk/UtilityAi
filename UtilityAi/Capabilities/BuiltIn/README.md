# Built-in Modules Documentation

This document provides detailed documentation for all built-in capability modules in UtilityAI.

## Overview

Built-in modules provide common functionality that most agents need, such as fallback behavior, automatic cleanup, and graceful shutdown. They follow best practices and serve as examples for creating custom modules.

**Key Concepts:**
- Modules propose actions based on current state
- Built-in modules handle infrastructure concerns
- They're designed to "just work" with minimal configuration
- All follow the single-responsibility principle

---

## IdleModule

**Purpose:** Provides a fallback no-op action that always has minimal utility. Ensures the orchestrator never stops due to lack of proposals.

### Problem It Solves

Without `IdleModule`, if all other modules fail to propose eligible actions, the orchestrator stops with `NoEligibleProposals`. This is often undesirable in long-running agents.

**Behavior:**
- Always proposes an idle action
- Has very low utility (default: 0.001)
- Acts as a safety net
- Action does nothing (`Task.CompletedTask`)

### Signature

```csharp
[Capability(Priority = -1000, Domain = "fallback")]
public sealed class IdleModule : ICapabilityModule
{
    public IdleModule(double idleUtility = 0.001)
}
```

### Parameters

- `idleUtility` - The utility score for the idle action (default: 0.001)
  - Lower values = only chosen when nothing else is available
  - Valid range: [0.0, 1.0]

### Usage

```csharp
var orchestrator = new UtilityAiOrchestrator(bus: bus, stopAtZero: false)
    .AddModule(new MyModule1())
    .AddModule(new MyModule2())
    .AddModule(new IdleModule());  // Always add last

await orchestrator.RunAsync(intent, maxTicks: 1000, ct);
```

### Configuration

The module is configured with:
- **Priority:** `-1000` (lowest priority, added last)
- **Domain:** `"fallback"`
- **Attribute-based:** Yes (auto-discovered with `DiscoverCapabilities()`)

### Example: Long-Running Agent

```csharp
// Without IdleModule
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddModule(new ProcessTasksModule());

await orchestrator.RunAsync(intent, maxTicks: 1000, ct);
// Problem: Stops immediately if no tasks are available

// With IdleModule
var orchestrator = new UtilityAiOrchestrator(bus: bus, stopAtZero: false)
    .AddModule(new ProcessTasksModule())
    .AddModule(new IdleModule());

await orchestrator.RunAsync(intent, maxTicks: 1000, ct);
// Solution: Idles when no tasks, continues running
```

### When to Use

✅ **Use when:**
- Building long-running agents
- You want the orchestrator to keep running even when idle
- You need a fallback safety net

❌ **Don't use when:**
- You want the orchestrator to stop when no work is available
- You're building a one-shot decision system
- All modules should always have proposals

### Best Practices

1. **Always add last** - Should have lowest priority
2. **Combine with `stopAtZero: false`** - Prevents stopping on idle
3. **Keep utility very low** - Should only win when nothing else is available
4. **Monitor idle frequency** - If idle is chosen often, your other modules may need work

### Testing

```csharp
[Fact]
public void IdleModule_AlwaysProposesIdleAction()
{
    var bus = new EventBus();
    var rt = new Runtime(bus, new UserIntent(new IntentGoal("test")), 0);
    var module = new IdleModule();

    var proposals = module.Propose(rt).ToList();

    Assert.Single(proposals);
    Assert.Equal("idle", proposals[0].Id);
}

[Fact]
public async Task IdleModule_ActionCompletesSuccessfully()
{
    var bus = new EventBus();
    var rt = new Runtime(bus, new UserIntent(new IntentGoal("test")), 0);
    var module = new IdleModule();

    var proposals = module.Propose(rt).ToList();

    // Should not throw
    await proposals[0].Act(CancellationToken.None);
}
```

---

## CleanupModule

**Purpose:** Periodically proposes cleanup actions to clear old facts from the EventBus, managing memory and preventing stale data accumulation.

### Problem It Solves

EventBus accumulates facts over time. Old facts consume memory and may cause stale data to influence decisions. `CleanupModule` automatically cleans up specified fact types on a schedule.

**Behavior:**
- Proposes cleanup after a configured interval
- Uses cooldown to prevent frequent cleanup
- Clears specified fact types from EventBus
- Publishes `CleanupExecuted` fact to track execution

### Signature

```csharp
[Capability(Priority = -500, Domain = "maintenance")]
public sealed class CleanupModule : ICapabilityModule
{
    public CleanupModule(
        Type[] typesToClean,
        TimeSpan? cleanupInterval = null,
        TimeSpan? cooldownPeriod = null
    )
}
```

### Parameters

- `typesToClean` - Array of fact types to clean up (required)
- `cleanupInterval` - How often to propose cleanup (default: 5 minutes)
- `cooldownPeriod` - Minimum time between cleanups (default: 1 minute)

### Usage

```csharp
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddSensor(new TimeSensor())  // Required for ElapsedTime
    .AddModule(new MyModule())
    .AddModule(new CleanupModule(
        typesToClean: new[]
        {
            typeof(TempData),
            typeof(CachedResult),
            typeof(ProcessingStatus)
        },
        cleanupInterval: TimeSpan.FromMinutes(10),
        cooldownPeriod: TimeSpan.FromMinutes(2)
    ));

await orchestrator.RunAsync(intent, maxTicks: 1000, ct);
```

### How It Works

1. **Interval Check** - Only proposes after `cleanupInterval` has elapsed
2. **Cooldown Gate** - `Cooldown<CleanupExecuted>` prevents rapid re-execution
3. **Cleanup Action** - Calls `bus.Clear<T>()` for each type
4. **Tracking** - Publishes `CleanupExecuted` fact

### Published Facts

```csharp
public sealed record CleanupExecuted(DateTimeOffset Timestamp);
```

### Example: Web Scraper Agent

```csharp
// Scraper accumulates temporary data
public record ScrapedPage(string Url, string Html);
public record ProcessingStatus(string PageId, string Status);
public record TempCache(string Key, object Value);

var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddSensor(new TimeSensor())
    .AddModule(new ScraperModule())
    .AddModule(new CleanupModule(
        typesToClean: new[]
        {
            typeof(ScrapedPage),      // Clear after processing
            typeof(ProcessingStatus), // Clear old statuses
            typeof(TempCache)         // Clear temp data
        },
        cleanupInterval: TimeSpan.FromMinutes(5)
    ));
```

### Example: Conversational Agent

```csharp
// Clear old temporary conversation state
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddSensor(new TimeSensor())
    .AddModule(new ConversationModule())
    .AddModule(new CleanupModule(
        typesToClean: new[]
        {
            typeof(UserTyping),         // Ephemeral state
            typeof(IntermediateResult), // Temp processing data
            typeof(CachedEmbedding)     // Old embeddings
        },
        cleanupInterval: TimeSpan.FromMinutes(3)
    ));
```

### When to Use

✅ **Use when:**
- Your agent runs for extended periods
- Facts accumulate over time
- You have temporary/ephemeral data types
- Memory management is important

❌ **Don't use when:**
- Short-lived orchestrations (single tick)
- All facts are important long-term
- You have custom cleanup logic

### Best Practices

1. **Only clean temporary data** - Don't clean important facts
2. **Set appropriate intervals** - Too frequent = wasted CPU, too rare = memory issues
3. **Use cooldown** - Prevents accidental rapid cleanup
4. **Monitor cleanup frequency** - Should match your data lifecycle

### Testing

```csharp
[Fact]
public async Task CleanupModule_ClearsFacts()
{
    var bus = new EventBus();
    bus.Publish(new ElapsedTime(TimeSpan.FromMinutes(10))); // Past interval

    var rt = new Runtime(bus, new UserIntent(new IntentGoal("test")), 0);
    var module = new CleanupModule(
        typesToClean: new[] { typeof(TempData) },
        cleanupInterval: TimeSpan.FromMinutes(5)
    );

    // Publish temp data
    bus.Publish(new TempData("test"));
    Assert.NotNull(bus.GetOrDefault<TempData>());

    // Execute cleanup proposal
    var proposals = module.Propose(rt).ToList();
    await proposals[0].Act(CancellationToken.None);

    // Verify cleared
    Assert.Null(bus.GetOrDefault<TempData>());
}
```

---

## StopOnSignalModule

**Purpose:** Responds to stop signals by publishing a `StopOrchestrationEvent`, enabling graceful shutdown based on external conditions or user commands.

### Problem It Solves

Orchestrators need a way to stop gracefully based on runtime conditions (e.g., user command, error threshold, completion criteria). `StopOnSignalModule` provides a clean mechanism for this.

**Behavior:**
- Listens for `StopSignal` fact
- When signal exists, proposes high-priority stop action
- Publishes `StopOrchestrationEvent` to halt orchestration
- Orchestrator stops at end of current tick

### Signature

```csharp
[Capability(Priority = 10000, Domain = "control")]
[RequiresFact<StopSignal>]
public sealed class StopOnSignalModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
}
```

### Configuration

The module is configured with:
- **Priority:** `10000` (highest priority)
- **Domain:** `"control"`
- **RequiresFact:** `StopSignal` (only active when signal exists)
- **Attribute-based:** Yes (auto-discovered)

### Usage

```csharp
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddModule(new MyModule1())
    .AddModule(new MyModule2())
    .AddModule(new StopOnSignalModule());  // Add for graceful shutdown

await orchestrator.RunAsync(intent, maxTicks: 1000, ct);

// Elsewhere: trigger shutdown
bus.Publish(new StopSignal("User requested shutdown"));
// Orchestrator will stop gracefully after current tick
```

### StopSignal Fact

```csharp
public sealed record StopSignal(string Reason);
```

### Published Events

```csharp
StopOrchestrationEvent(
    OrchestrationStopReason.GoalAchieved,
    string? message
)
```

### Example: User-Initiated Shutdown

```csharp
public class UserCommandModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var userMsg = rt.Bus.GetOrDefault<UserMessage>();
        if (userMsg == null) yield break;

        // Check for shutdown command
        if (userMsg.Text.ToLower() == "/exit")
        {
            yield return new Proposal(
                id: "user-exit",
                cons: new[] { new ConstantValue(1.0) },
                act: ct =>
                {
                    rt.Bus.Publish(new StopSignal("User typed /exit"));
                    return Task.CompletedTask;
                }
            );
        }
    }
}

// With StopOnSignalModule, orchestrator stops gracefully
```

### Example: Error Threshold

```csharp
public class ErrorMonitorSensor : ISensor
{
    public Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        var errors = rt.Bus.GetHistory<ErrorOccurred>(maxItems: 10);

        // Stop if 5+ errors in last 10 events
        if (errors.Count >= 5)
        {
            rt.Bus.Publish(new StopSignal($"Too many errors: {errors.Count}"));
        }

        return Task.CompletedTask;
    }
}
```

### Example: Goal Completion

```csharp
public class GoalTrackerModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var progress = rt.Bus.GetOrDefault<TaskProgress>();
        if (progress?.Completed >= progress?.Total)
        {
            yield return new Proposal(
                id: "goal-complete",
                cons: new[] { new ConstantValue(1.0) },
                act: ct =>
                {
                    rt.Bus.Publish(new StopSignal("All tasks completed"));
                    return Task.CompletedTask;
                }
            );
        }
    }
}
```

### When to Use

✅ **Use when:**
- You need graceful shutdown
- Users can trigger stop (commands, UI)
- Goal-based orchestration (stop when done)
- Error handling (stop on critical errors)

❌ **Don't use when:**
- You only need timeout-based stopping (use `maxTicks`)
- You want immediate termination (`CancellationToken`)

### Shutdown Flow

```
User/Sensor/Module
        ↓
Publish StopSignal
        ↓
StopOnSignalModule detects signal
        ↓
Proposes stop action (utility = 1.0)
        ↓
Action executes: publishes StopOrchestrationEvent
        ↓
Orchestrator sees event, stops after current tick
```

### Testing

```csharp
[Fact]
public void StopOnSignalModule_ProposesWhenSignalExists()
{
    var bus = new EventBus();
    bus.Publish(new StopSignal("test"));

    var rt = new Runtime(bus, new UserIntent(new IntentGoal("test")), 0);
    var module = new StopOnSignalModule();

    var proposals = module.Propose(rt).ToList();

    Assert.Single(proposals);
    Assert.Equal("stop.on-signal", proposals[0].Id);
}

[Fact]
public async Task StopOnSignalModule_PublishesStopEvent()
{
    var bus = new EventBus();
    bus.Publish(new StopSignal("test reason"));

    var rt = new Runtime(bus, new UserIntent(new IntentGoal("test")), 0);
    var module = new StopOnSignalModule();

    var proposals = module.Propose(rt).ToList();
    await proposals[0].Act(CancellationToken.None);

    var stopEvent = bus.GetOrDefault<StopOrchestrationEvent>();
    Assert.NotNull(stopEvent);
    Assert.Equal("test reason", stopEvent.Message);
}
```

---

## Combining Built-in Modules

Built-in modules work together seamlessly:

```csharp
var orchestrator = new UtilityAiOrchestrator(bus: bus, stopAtZero: false)
    // Sensors
    .AddSensor(new TimeSensor())
    .AddSensor(new ConversationHistorySensor())

    // Your domain modules
    .AddModule(new ConversationModule())
    .AddModule(new TaskProcessingModule())

    // Built-in modules
    .AddModule(new CleanupModule(
        typesToClean: new[] { typeof(TempData) },
        cleanupInterval: TimeSpan.FromMinutes(10)
    ))
    .AddModule(new StopOnSignalModule())
    .AddModule(new IdleModule());  // Always add last

await orchestrator.RunAsync(intent, maxTicks: 10000, ct);
```

**Behavior:**
1. Runs normally processing tasks
2. Cleans up temp data every 10 minutes
3. Idles when no work available
4. Stops gracefully on signal

---

## Best Practices

### Module Ordering

1. **Domain modules first** - Your application logic
2. **Cleanup module** - Maintenance
3. **Stop module** - Control
4. **Idle module last** - Fallback

### Priority Guidelines

- **High priority (>1000):** Control modules (StopOnSignal)
- **Normal priority (0-1000):** Domain modules
- **Low priority (<0):** Maintenance, fallback (Cleanup, Idle)

### Attribute-Based Discovery

All built-in modules support auto-discovery:

```csharp
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddSensor(new TimeSensor())
    .DiscoverCapabilities(typeof(IdleModule).Assembly);
// Automatically discovers and registers all built-in modules
```

---

## Creating Custom Built-in Style Modules

Follow these patterns for your own infrastructure modules:

```csharp
[Capability(Priority = 100, Domain = "my-domain")]
[RequiresFact<MyTriggerFact>]
public class MyInfrastructureModule : ICapabilityModule
{
    private readonly string _config;

    public MyInfrastructureModule(string config)
    {
        _config = config;
    }

    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        // Check for trigger fact
        var trigger = rt.Bus.GetOrDefault<MyTriggerFact>();
        if (trigger == null) yield break;

        yield return new Proposal(
            id: "my-infrastructure-action",
            cons: new[]
            {
                new HasFact<MyTriggerFact>(shouldHave: true),
                new Cooldown<MyActionExecuted>(TimeSpan.FromMinutes(5))
            },
            act: async ct =>
            {
                // Perform action
                await DoWork(ct);

                // Publish completion fact
                rt.Bus.Publish(new MyActionExecuted(DateTimeOffset.UtcNow));
            }
        );
    }
}
```

---

## See Also

- [BUILT_IN_COMPONENTS.md](../../docs/BUILT_IN_COMPONENTS.md) - Complete components reference
- [PROPOSAL_PATTERNS.md](../../docs/PROPOSAL_PATTERNS.md) - Best practices for proposals
- [ARCHITECTURE.md](../../docs/ARCHITECTURE.md) - Framework architecture
