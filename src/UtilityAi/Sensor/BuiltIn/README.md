# Built-in Sensors Documentation

This document provides detailed documentation for all built-in sensors in UtilityAI.

## Overview

Sensors observe the environment and publish facts to the EventBus each tick. They bridge the gap between your application's state and the orchestration system, converting raw data into actionable facts.

**Key Concepts:**
- Sensors run at the start of each orchestration tick
- They should be lightweight and fast (offload heavy work to async operations)
- Sensors publish facts by calling `rt.Bus.Publish<T>(fact)`
- Multiple sensors can publish the same fact type (last write wins)
- Sensors should be stateless where possible (state goes in EventBus)

---

## TimeSensor

**Purpose:** Publishes time-related facts every tick, providing time awareness to all modules and considerations.

### Published Facts

```csharp
CurrentTime(DateTimeOffset Value)    // Current UTC time
TickNumber(int Value)                // Current orchestration tick
ElapsedTime(TimeSpan Value)          // Time since orchestration started
```

### Signature

```csharp
public class TimeSensor : ISensor
{
    public Task SenseAsync(Runtime rt, CancellationToken ct)
}
```

### Usage

```csharp
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddSensor(new TimeSensor())
    .AddModule(/* your modules */);
```

### Access the Facts

```csharp
// In modules or considerations
var currentTime = rt.Bus.GetOrDefault<CurrentTime>();
var tickNumber = rt.Bus.GetOrDefault<TickNumber>();
var elapsed = rt.Bus.GetOrDefault<ElapsedTime>();

if (elapsed?.Value > TimeSpan.FromMinutes(5))
{
    // Orchestration has been running for 5+ minutes
}
```

### Use Cases

- **Time-based scheduling** - Schedule actions for specific times
- **Timeout detection** - Detect when orchestration runs too long
- **Performance monitoring** - Track time per tick
- **Time-aware scoring** - Use in TimeWindow considerations
- **Rate limiting** - Track time between events

### Example

```csharp
public class TimeAwareModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var elapsed = rt.Bus.GetOrDefault<ElapsedTime>();
        if (elapsed == null) yield break;

        // Increase urgency over time
        yield return new Proposal(
            id: "timeout-warning",
            cons: new[]
            {
                new CurveSignal<ElapsedTime>(
                    selector: e => e.Value.TotalSeconds,
                    curve: Curves.Logistic(k: 0.05, x0: 60),
                    inputDomain: (0, 120)
                )
            },
            act: async ct => { /* warn about timeout */ }
        );
    }
}
```

### Performance Notes

- **Overhead:** Minimal (3 simple assignments per tick)
- **State:** Maintains start time internally (initialized on first tick)
- **Thread Safety:** Safe (no shared mutable state)

---

## ConversationHistorySensor

**Purpose:** Analyzes message history from the EventBus and publishes aggregated conversation metadata. Essential for conversational AI agents.

### Published Facts

```csharp
ConversationMetadata(
    int MessageCount,
    TimeSpan Duration,
    bool IsLongConversation,
    DateTimeOffset? FirstMessageTime,
    DateTimeOffset? LastMessageTime
)
```

### Signature

```csharp
public class ConversationHistorySensor : ISensor
{
    public ConversationHistorySensor(
        int maxHistoryToAnalyze = 100,
        int longConversationThreshold = 20
    )
}
```

### Parameters

- `maxHistoryToAnalyze` - Maximum number of messages to analyze (default: 100)
- `longConversationThreshold` - Message count threshold for "long" conversations (default: 20)

### Usage

```csharp
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddSensor(new ConversationHistorySensor(
        maxHistoryToAnalyze: 50,
        longConversationThreshold: 15
    ))
    .AddModule(/* your modules */);
```

### Access the Facts

```csharp
// In modules or considerations
var metadata = rt.Bus.GetOrDefault<ConversationMetadata>();

if (metadata != null)
{
    Console.WriteLine($"Messages: {metadata.MessageCount}");
    Console.WriteLine($"Duration: {metadata.Duration}");
    Console.WriteLine($"Long: {metadata.IsLongConversation}");
}
```

### Use Cases

- **Conversation summarization** - Trigger summaries for long conversations
- **Context management** - Limit context window for LLMs
- **User engagement tracking** - Measure conversation length
- **Adaptive behavior** - Change strategy for long vs short conversations
- **Rate limiting** - Throttle responses based on message frequency

### Example

```csharp
public class SummarizationModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var metadata = rt.Bus.GetOrDefault<ConversationMetadata>();
        if (metadata == null) yield break;

        // Trigger summarization for long conversations
        if (metadata.IsLongConversation && metadata.MessageCount % 10 == 0)
        {
            yield return new Proposal(
                id: "summarize-conversation",
                cons: new[]
                {
                    new ConstantValue(0.8),  // High priority
                    new Cooldown<SummaryCreated>(TimeSpan.FromMinutes(5))
                },
                act: async ct =>
                {
                    var messages = rt.Bus.GetHistory<UserMessage>(maxItems: 50);
                    var summary = await CreateSummary(messages, ct);
                    rt.Bus.Publish(summary);
                }
            );
        }
    }
}
```

### Expected EventBus Facts

This sensor analyzes the following facts from EventBus history:
- `UserMessage(string Text, string UserId, DateTimeOffset Timestamp)`
- `AssistantMessage(string Text, DateTimeOffset Timestamp)`

Make sure to publish these facts in your application for the sensor to work correctly.

### Performance Notes

- **Overhead:** O(n) where n = number of messages to analyze
- **Recommended:** Run every tick or every N ticks (depending on conversation frequency)
- **Memory:** Minimal (just metadata, not full history)

---

## EventFrequencySensor<TEvent, TFrequencyFact>

**Purpose:** Tracks the frequency of specific event types over a time window. Essential for rate limiting and throttling.

### Generic Type Parameters

- `TEvent` - The type of event to track (e.g., `ApiCall`, `UserMessage`)
- `TFrequencyFact` - The type of fact to publish with frequency data

### Signature

```csharp
public class EventFrequencySensor<TEvent, TFrequencyFact> : ISensor
    where TEvent : class
    where TFrequencyFact : class
{
    public EventFrequencySensor(
        TimeSpan timeWindow,
        Func<int, double, TFrequencyFact> factFactory
    )
}
```

### Parameters

- `timeWindow` - Time window to measure frequency over
- `factFactory` - Factory function to create frequency fact from (count, eventsPerSecond)

### Usage

```csharp
// Define your frequency fact type
public record ApiCallFrequency(int Count, double EventsPerSecond);

// Create sensor
var sensor = new EventFrequencySensor<ApiCall, ApiCallFrequency>(
    timeWindow: TimeSpan.FromMinutes(1),
    factFactory: (count, eventsPerSec) => new ApiCallFrequency(count, eventsPerSec)
);

var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddSensor(sensor)
    .AddModule(/* your modules */);
```

### Access the Facts

```csharp
// In modules or considerations
var frequency = rt.Bus.GetOrDefault<ApiCallFrequency>();

if (frequency != null && frequency.EventsPerSecond > 5.0)
{
    // Rate limit triggered: more than 5 events per second
}
```

### Use Cases

- **Rate limiting** - Throttle operations based on frequency
- **Load detection** - Detect high-load periods
- **Anomaly detection** - Spot unusual activity patterns
- **Adaptive throttling** - Dynamically adjust behavior based on load
- **Quota management** - Track usage against quotas

### Example: API Rate Limiter

```csharp
// Define facts
public record ApiCall(string Endpoint, DateTimeOffset Timestamp);
public record ApiCallFrequency(int Count, double PerSecond);

// Create sensor
var sensor = new EventFrequencySensor<ApiCall, ApiCallFrequency>(
    timeWindow: TimeSpan.FromMinutes(1),
    factFactory: (count, perSec) => new ApiCallFrequency(count, perSec)
);

// Use in module
public class RateLimitedApiModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var frequency = rt.Bus.GetOrDefault<ApiCallFrequency>();

        yield return new Proposal(
            id: "api-call",
            cons: new[]
            {
                new ThresholdValue<ApiCallFrequency>(
                    selector: f => f.PerSecond,
                    threshold: 10.0,  // Max 10 calls per second
                    above: false  // 1.0 when below threshold
                ),
                new Cooldown<ApiCall>(TimeSpan.FromMilliseconds(100))
            },
            act: async ct =>
            {
                await MakeApiCall(ct);
                rt.Bus.Publish(new ApiCall("/endpoint", DateTimeOffset.UtcNow));
            }
        );
    }
}
```

### Performance Notes

- **Overhead:** O(h) where h = history size
- **Optimization:** EventBus automatically limits history (default 100 items)
- **Memory:** Minimal (just count and rate)

---

## ResourceSensor

**Purpose:** Monitors system resource usage (CPU and memory) and publishes metrics. Essential for resource-aware agents.

### Published Facts

```csharp
ResourceUsage(
    double CpuPercent,
    double MemoryMegabytes,
    DateTimeOffset Timestamp
)
```

### Signature

```csharp
public class ResourceSensor : ISensor
{
    public Task SenseAsync(Runtime rt, CancellationToken ct)
}
```

### Usage

```csharp
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddSensor(new ResourceSensor())
    .AddModule(/* your modules */);
```

### Access the Facts

```csharp
// In modules or considerations
var resources = rt.Bus.GetOrDefault<ResourceUsage>();

if (resources != null)
{
    Console.WriteLine($"CPU: {resources.CpuPercent}%");
    Console.WriteLine($"Memory: {resources.MemoryMegabytes} MB");
}
```

### Use Cases

- **Resource throttling** - Reduce work when CPU/memory is high
- **Adaptive scheduling** - Prioritize lightweight tasks under load
- **Health monitoring** - Track resource trends
- **Capacity planning** - Measure resource requirements
- **Graceful degradation** - Disable expensive features under pressure

### Example: Adaptive Task Scheduler

```csharp
public class AdaptiveTaskModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var resources = rt.Bus.GetOrDefault<ResourceUsage>();
        if (resources == null) yield break;

        // Heavy task (only when resources available)
        yield return new Proposal(
            id: "heavy-task",
            cons: new[]
            {
                new ThresholdValue<ResourceUsage>(
                    selector: r => r.CpuPercent,
                    threshold: 70.0,
                    above: false  // Only run when CPU < 70%
                ),
                new ThresholdValue<ResourceUsage>(
                    selector: r => r.MemoryMegabytes,
                    threshold: 1000.0,
                    above: false  // Only run when memory < 1GB
                )
            },
            act: async ct => await ProcessHeavyTask(ct)
        );

        // Light task (always OK)
        yield return new Proposal(
            id: "light-task",
            cons: new[] { new ConstantValue(0.5) },
            act: async ct => await ProcessLightTask(ct)
        );
    }
}
```

### Metrics Explained

**CPU Percentage:**
- Measured per-process (not system-wide)
- Normalized by processor count
- Formula: `(cpuTime / elapsedTime) / processorCount * 100`
- Range: 0.0 to 100.0 (can exceed 100 on multi-core systems if calculated differently)

**Memory Megabytes:**
- Working set size (physical memory used by process)
- Includes code, stack, heap, and loaded DLLs
- Does not include paged-out memory

### Performance Notes

- **Overhead:** Low (uses `Process.GetCurrentProcess()`)
- **Accuracy:** Updated each tick (may have slight delay)
- **Platform:** Works on Windows, Linux, macOS

### Recommendations

1. **Don't run every tick** - Resource monitoring can be expensive. Consider running every 5-10 ticks.
2. **Use thresholds** - Make decisions based on resource thresholds, not exact values
3. **Add hysteresis** - Avoid rapid on/off behavior (e.g., use 70% to disable, 50% to re-enable)

---

## MemorySensor

**Purpose:** Automatically archives old EventBus facts to long-term memory storage (IMemoryStore). Prevents EventBus history from growing unbounded.

### Signature

```csharp
public class MemorySensor : ISensor
{
    public MemorySensor(
        IMemoryStore store,
        TimeSpan? archiveThreshold = null,
        params Type[] typesToArchive
    )
}
```

### Parameters

- `store` - The memory store to archive to
- `archiveThreshold` - Archive events older than this threshold (default: 5 minutes)
- `typesToArchive` - Specific types to archive. If empty, archives all types.

### Usage

```csharp
var memoryStore = new InMemoryStore();

var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddSensor(new MemorySensor(
        store: memoryStore,
        archiveThreshold: TimeSpan.FromMinutes(5),
        typeof(UserMessage),
        typeof(TaskCompleted),
        typeof(ApiCall)
    ))
    .AddModule(/* your modules */);
```

### How It Works

1. **Periodic Check** - Runs every ~1 minute (configurable)
2. **Identifies Old Events** - Finds events older than `archiveThreshold`
3. **Archives to Store** - Calls `store.StoreAsync()` for each old event
4. **Keeps Recent** - EventBus retains recent events (within threshold)

### Retrieving Archived Data

```csharp
// Later: recall from long-term memory
var query = new MemoryQuery
{
    MaxResults = 100,
    TimeWindow = TimeSpan.FromDays(7),
    SortOrder = SortOrder.NewestFirst
};

var oldMessages = await memoryStore.RecallAsync<UserMessage>(query);

// Use archived data
foreach (var memory in oldMessages)
{
    Console.WriteLine($"{memory.Timestamp}: {memory.Fact.Text}");
}
```

### Use Cases

- **Long-term conversation history** - Store conversations beyond EventBus limits
- **Audit logging** - Archive all events for compliance
- **Analytics** - Analyze historical patterns
- **Context building** - Build rich context from long-term history
- **Memory management** - Prevent EventBus from consuming too much memory

### Example: LLM with Long-Term Memory

```csharp
var memoryStore = new InMemoryStore();

// Archive old messages
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddSensor(new MemorySensor(
        store: memoryStore,
        archiveThreshold: TimeSpan.FromMinutes(10),
        typeof(UserMessage),
        typeof(AssistantMessage)
    ))
    .AddModule(new LLMModule(memoryStore));

// In LLM module
public class LLMModule : ICapabilityModule
{
    private readonly IMemoryStore _memory;

    public LLMModule(IMemoryStore memory)
    {
        _memory = memory;
    }

    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        yield return new Proposal(
            id: "llm-respond",
            cons: new[] { new HasFact<UserMessage>() },
            act: async ct =>
            {
                // Get recent messages from EventBus
                var recentMessages = rt.Bus.GetHistory<UserMessage>(maxItems: 10);

                // Get older messages from long-term memory
                var oldMessages = await _memory.RecallAsync<UserMessage>(
                    new MemoryQuery
                    {
                        MaxResults = 20,
                        TimeWindow = TimeSpan.FromHours(24)
                    },
                    ct
                );

                // Combine for rich context
                var allMessages = oldMessages
                    .Select(m => m.Fact)
                    .Concat(recentMessages.Select(m => m.Value))
                    .OrderBy(m => m.Timestamp)
                    .ToList();

                // Send to LLM with full context
                var response = await CallLLM(allMessages, ct);
                rt.Bus.Publish(new AssistantMessage(response, DateTimeOffset.UtcNow));
            }
        );
    }
}
```

### Performance Notes

- **Overhead:** Minimal (runs every ~1 minute, not every tick)
- **I/O Bound:** Archive operation is async (non-blocking)
- **Configurable:** Adjust `archiveThreshold` to balance memory vs archive frequency

### Recommendations

1. **Choose appropriate threshold** - Too short = frequent archives, too long = high memory
2. **Selective archiving** - Only archive facts you need long-term
3. **Prune regularly** - Use `store.PruneAsync()` to remove very old data
4. **Consider storage backend** - Use SQL/Redis for production, InMemoryStore for dev/test

---

## Best Practices

### Sensor Design

1. **Keep sensors lightweight** - Heavy work should be async
2. **Publish facts, not decisions** - Sensors observe, modules decide
3. **Idempotent when possible** - Same input → same output
4. **Error handling** - Sensors should not throw (catch and log)
5. **Single responsibility** - One sensor = one concern

### Sensor Ordering

Sensors run in registration order. Consider dependencies:

```csharp
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddSensor(new TimeSensor())              // 1. Publish time first
    .AddSensor(new ConversationHistorySensor()) // 2. Analyze messages
    .AddSensor(new ResourceSensor())           // 3. Check resources
    .AddModule(/* ... */);
```

### Performance Optimization

**Run expensive sensors less frequently:**

```csharp
public class ExpensiveSensor : ISensor
{
    private int _tickCount = 0;

    public async Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        // Only run every 10 ticks
        if (_tickCount++ % 10 != 0) return;

        // Expensive operation
        var data = await FetchExpensiveData(ct);
        rt.Bus.Publish(data);
    }
}
```

### Testing Sensors

```csharp
[Fact]
public async Task TimeSensor_PublishesTimeFacts()
{
    var bus = new EventBus();
    var sensor = new TimeSensor();
    var rt = new Runtime(bus, new UserIntent(new IntentGoal("test")), 0);

    await sensor.SenseAsync(rt, CancellationToken.None);

    var currentTime = bus.GetOrDefault<CurrentTime>();
    var tickNumber = bus.GetOrDefault<TickNumber>();

    Assert.NotNull(currentTime);
    Assert.NotNull(tickNumber);
    Assert.Equal(0, tickNumber.Value);
}
```

---

## Creating Custom Sensors

### Template

```csharp
using UtilityAi.Sensor;
using UtilityAi.Utils;

public class MySensor : ISensor
{
    private readonly string _apiEndpoint;

    public MySensor(string apiEndpoint)
    {
        _apiEndpoint = apiEndpoint;
    }

    public async Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        try
        {
            // Fetch data (async is OK)
            var data = await FetchData(_apiEndpoint, ct);

            // Publish facts to EventBus
            rt.Bus.Publish(new MyFact(data));
        }
        catch (Exception ex)
        {
            // Log error but don't throw (don't break orchestration)
            Console.WriteLine($"Sensor error: {ex.Message}");
        }
    }

    private async Task<string> FetchData(string endpoint, CancellationToken ct)
    {
        // Your implementation
        return await Task.FromResult("data");
    }
}
```

### Guidelines

1. **Constructor injection** - Pass dependencies via constructor
2. **Async operations** - Use `async/await` for I/O
3. **Error handling** - Catch and log, don't throw
4. **Cancellation support** - Respect `CancellationToken`
5. **Thread safety** - Sensors should be thread-safe if reused

---

## See Also

- [BUILT_IN_COMPONENTS.md](../../docs/BUILT_IN_COMPONENTS.md) - Complete built-in components reference
- [ARCHITECTURE.md](../../docs/ARCHITECTURE.md) - Framework architecture
- [README.md](../README.md) - Considerations documentation
