# Built-in Components Reference

This guide documents the built-in considerations, sensors, modules, and facts provided by UtilityAI.

---

## 📊 Built-in Considerations

### General Considerations

#### `HasFact<T>`
Checks if a fact of type T exists in the EventBus.

```csharp
new HasFact<UserMessage>()         // 1.0 if exists, 0.0 if not
new HasFact<UserMessage>(invert: true)  // Inverted logic
```

#### `ThresholdValue<T>`
Returns 1.0 if a numeric value exceeds (or falls below) a threshold.

```csharp
new ThresholdValue<TaskPriority>(
    selector: t => t.Value,
    threshold: 0.7,
    above: true  // 1.0 if value > 0.7
)
```

#### `RangeValue<T>`
Returns 1.0 if a value falls within a specified range.

```csharp
new RangeValue<Temperature>(
    selector: t => t.Celsius,
    min: 18.0,
    max: 24.0,
    inclusive: true
)
```

#### `InverseValue<T>`
Inverts a normalized value (1.0 - value).

```csharp
new InverseValue<Confidence>(c => c.Score)  // High when confidence is low
```

#### `CurveSignal<T>`
Maps a value through a response curve.

```csharp
new CurveSignal<TaskAge>(
    selector: t => t.Minutes,
    curve: Curves.Logistic(k: 0.1, x0: 30),
    inputDomain: (0, 60)
)
```

#### `TimeSinceEvent<T>`
Evaluates time elapsed since the most recent event.

```csharp
new TimeSinceEvent<UserMessage>(
    curve: Curves.Logistic(k: 0.05, x0: 60),
    inputDomain: (0, 300)  // 0-5 minutes
)
```

#### `Cooldown<T>`
Prevents repeated actions by requiring a cooldown period.

```csharp
new Cooldown<ApiCallMade>(TimeSpan.FromSeconds(30))
```

#### `CollectionSize<T>`
Scores based on the size of a collection.

```csharp
new CollectionSize<TaskQueue>(
    sizeSelector: q => q.Count,
    curve: Curves.Logistic(k: 0.1, x0: 5),
    inputDomain: (0, 20)
)
```

#### `AnyMatch<TFact, TItem>`
Returns 1.0 if any item in a collection matches a predicate.

```csharp
new AnyMatch<TaskQueue, Task>(
    collectionSelector: q => q.Tasks,
    predicate: t => t.Priority > 0.8
)
```

#### `AllMatch<TFact, TItem>`
Returns 1.0 if all items in a collection match a predicate.

```csharp
new AllMatch<ValidationResults, Result>(
    collectionSelector: vr => vr.Results,
    predicate: r => r.IsValid
)
```

#### `TimeWindow`
Returns 1.0 during specified time windows (e.g., business hours).

```csharp
new TimeWindow(
    startTime: new TimeOnly(9, 0),   // 9 AM
    endTime: new TimeOnly(17, 0),    // 5 PM
    allowedDays: new[] { DayOfWeek.Monday, DayOfWeek.Tuesday }
)
```

#### `RandomValue`
Returns a random value between 0.0 and 1.0 for exploration.

```csharp
new RandomValue()
```

#### `WeightedRandomValue<T>`
Combines deterministic score with randomness.

```csharp
new WeightedRandomValue<Priority>(
    scoreSelector: p => p.Value,
    deterministicWeight: 0.7  // 70% deterministic, 30% random
)
```

#### `ConstantValue`
Returns a fixed value. Useful for testing or fixed weights.

```csharp
new ConstantValue(0.5)
```

### Composite Considerations

#### `AndConsideration`
Combines multiple considerations using multiplication (AND logic).

```csharp
new AndConsideration(
    new HasFact<UserMessage>(),
    new ThresholdValue<Confidence>(c => c.Score, 0.7),
    new Cooldown<ResponseSent>(TimeSpan.FromSeconds(5))
)
```

#### `OrConsideration`
Takes the maximum value of multiple considerations (OR logic).

```csharp
new OrConsideration(
    new HasFact<HighPriority>(),
    new HasFact<UrgentRequest>(),
    new TimeSinceEvent<UserMessage>(...)
)
```

#### `NotConsideration`
Inverts a consideration's result.

```csharp
new NotConsideration(
    new HasFact<ProcessingComplete>()
)
```

---

## 🔬 Built-in Sensors

### `TimeSensor`
Publishes time-related facts every tick.

```csharp
.AddSensor(new TimeSensor())

// Publishes:
// - CurrentTime(DateTimeOffset)
// - TickNumber(int)
// - ElapsedTime(TimeSpan)
```

### `ConversationHistorySensor`
Analyzes message history and publishes conversation metadata.

```csharp
.AddSensor(new ConversationHistorySensor(
    maxHistoryToAnalyze: 100,
    longConversationThreshold: 20
))

// Publishes:
// - ConversationMetadata(MessageCount, Duration, IsLongConversation, ...)
```

### `EventFrequencySensor<TEvent, TFrequencyFact>`
Tracks event frequency over a time window.

```csharp
.AddSensor(new EventFrequencySensor<ApiCall, ApiCallFrequency>(
    timeWindow: TimeSpan.FromMinutes(1),
    factFactory: (count, perSecond) => new ApiCallFrequency(count, perSecond)
))
```

### `ResourceSensor`
Monitors CPU and memory usage.

```csharp
.AddSensor(new ResourceSensor())

// Publishes:
// - ResourceUsage(CpuPercent, MemoryMegabytes, Timestamp)
```

### `MemorySensor`
Automatically archives old EventBus facts to long-term memory.

```csharp
.AddSensor(new MemorySensor(
    store: new InMemoryStore(),
    archiveThreshold: TimeSpan.FromMinutes(5),
    typeof(UserMessage),
    typeof(TaskCompleted)
))
```

---

## 🧩 Built-in Modules

### `IdleModule`
Fallback module that always proposes a no-op action with minimal utility.

```csharp
.AddModule(new IdleModule(idleUtility: 0.001))
```

**Use case:** Prevents orchestrator from stopping when no other proposals are eligible.

### `CleanupModule`
Periodically cleans up old facts from the EventBus.

```csharp
.AddModule(new CleanupModule(
    typesToClean: new[] { typeof(TempData), typeof(CachedResult) },
    cleanupInterval: TimeSpan.FromMinutes(5),
    cooldownPeriod: TimeSpan.FromMinutes(1)
))
```

### `StopOnSignalModule`
Gracefully stops orchestration when a StopSignal fact is published.

```csharp
.AddModule(new StopOnSignalModule())

// Trigger stop:
bus.Publish(new StopSignal("User requested shutdown"));
```

---

## 📡 Built-in Orchestration Sinks

Sinks observe the orchestration lifecycle (`OnTickStart`, `OnScored`, `OnChosen`, `OnActed`, `OnStopped`) and are passed to `RunAsync`:

```csharp
var sink = new RecordingSink();
await orchestrator.RunAsync(maxTicks: 10, ct, sink);
```

### `NullSink`
Default no-op sink (`NullSink.Instance`) for scenarios where you don't need telemetry.

```csharp
await orchestrator.RunAsync(maxTicks: 10, ct, NullSink.Instance);
```

### `CompositeSink`
Forwards sink events to multiple sinks in order, so you can combine logging/metrics/testing sinks.

```csharp
var sink = new CompositeSink(
    new LoggingSink(logger),
    new RecordingSink()
);
await orchestrator.RunAsync(maxTicks: 10, ct, sink);
```

### `RecordingSink`
Records per-tick decisions as `OrchestrationTick` entries for diagnostics and tests.

```csharp
var recording = new RecordingSink();
await orchestrator.RunAsync(maxTicks: 3, ct, recording);

foreach (var tick in recording.Ticks)
{
    Console.WriteLine($"{tick.Tick}: {tick.Chosen.Id} ({tick.ChosenUtility:F3})");
}
```

> Note: The `UtilityAi.Dashboard` tool package also includes `DashboardSink` for real-time visualization.

---

## 📦 Built-in Facts

### Time Facts
```csharp
CurrentTime(DateTimeOffset Value)
TickNumber(int Value)
ElapsedTime(TimeSpan Value)
```

### Conversation Facts
```csharp
UserMessage(string Text, string UserId, DateTimeOffset Timestamp)
AssistantMessage(string Text, DateTimeOffset Timestamp)

ConversationMetadata(
    int MessageCount,
    TimeSpan Duration,
    bool IsLongConversation,
    DateTimeOffset? FirstMessageTime,
    DateTimeOffset? LastMessageTime
)
```

### System Facts
```csharp
ResourceUsage(double CpuPercent, double MemoryMegabytes, DateTimeOffset Timestamp)
RateLimitStatus(int RemainingRequests, DateTimeOffset ResetTime, bool IsLimited)
StopSignal(string Reason)
```

---

## 🗄️ Memory System

### `IMemoryStore`
Interface for long-term memory storage beyond EventBus history limits.

```csharp
public interface IMemoryStore
{
    Task StoreAsync<T>(T fact, DateTimeOffset timestamp, CancellationToken ct);
    Task<IReadOnlyList<TimestampedMemory<T>>> RecallAsync<T>(MemoryQuery query, CancellationToken ct);
    Task<int> CountAsync<T>(CancellationToken ct);
    Task PruneAsync(TimeSpan retentionPeriod, CancellationToken ct);
}
```

### `InMemoryStore`
In-memory implementation for simple scenarios and testing.

```csharp
var store = new InMemoryStore();

// Store facts
await store.StoreAsync(new UserMessage("Hello", "user-1", now), now);

// Recall with query
var query = new MemoryQuery
{
    MaxResults = 10,
    TimeWindow = TimeSpan.FromHours(1),
    SortOrder = SortOrder.NewestFirst
};
var memories = await store.RecallAsync<UserMessage>(query);

// Prune old data
await store.PruneAsync(retentionPeriod: TimeSpan.FromDays(7));
```

---

## 🔧 EventBus Extensions

### Persistence

#### `Snapshot()`
Captures current EventBus state for serialization.

```csharp
var snapshot = bus.Snapshot(
    typesToCapture: new[] { typeof(UserMessage), typeof(GameState) },
    includeHistory: true
);

// Serialize snapshot to JSON, database, etc.
var json = JsonSerializer.Serialize(snapshot);
```

#### `Restore()`
Restores EventBus state from a snapshot.

```csharp
var snapshot = JsonSerializer.Deserialize<EventBusSnapshot>(json);
bus.Restore(snapshot);
```

### Enhanced Queries

#### `GetHistoryInWindow<T>()`
Gets events within a time window.

```csharp
var recentMessages = bus.GetHistoryInWindow<UserMessage>(TimeSpan.FromMinutes(5));
```

#### `GetHistoryWhere<T>()`
Filters history with a predicate.

```csharp
var highPriorityTasks = bus.GetHistoryWhere<Task>(
    t => t.Priority > 0.8,
    maxResults: 10
);
```

#### `GetEventFrequency<T>()`
Calculates events per second over a window.

```csharp
var apiCallsPerSecond = bus.GetEventFrequency<ApiCall>(TimeSpan.FromMinutes(1));
```

#### `GetTimeSinceLastEvent<T>()`
Time elapsed since most recent event.

```csharp
var timeSinceMessage = bus.GetTimeSinceLastEvent<UserMessage>();
if (timeSinceMessage > TimeSpan.FromMinutes(5))
{
    // User has been inactive for 5+ minutes
}
```

#### `HasRecentEvent<T>()`
Checks if an event occurred recently.

```csharp
if (bus.HasRecentEvent<ErrorOccurred>(TimeSpan.FromSeconds(30)))
{
    // Error happened in last 30 seconds
}
```

---

## 💡 Usage Examples

### Complete Agent with Built-ins

```csharp
var orchestrator = new UtilityAiOrchestrator(bus: bus, stopAtZero: false)
    // Built-in sensors
    .AddSensor(new TimeSensor())
    .AddSensor(new ConversationHistorySensor())
    .AddSensor(new ResourceSensor())

    // Custom domain modules
    .AddModule(new RespondToUserModule())
    .AddModule(new ProcessTasksModule())

    // Built-in fallback modules
    .AddModule(new IdleModule())
    .AddModule(new StopOnSignalModule());

await orchestrator.RunAsync(maxTicks: 100, ct);
```

### Using Memory System

```csharp
var memoryStore = new InMemoryStore();

var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddSensor(new MemorySensor(
        store: memoryStore,
        archiveThreshold: TimeSpan.FromMinutes(5),
        typeof(UserMessage),
        typeof(TaskCompleted)
    ))
    .AddModule(new MyModule());

// Later: recall from long-term memory
var oldMessages = await memoryStore.RecallAsync<UserMessage>(
    new MemoryQuery { TimeWindow = TimeSpan.FromDays(7) }
);
```

### Session Persistence

```csharp
// Save session
var snapshot = bus.Snapshot(
    new[] { typeof(UserMessage), typeof(GameState), typeof(PlayerInventory) },
    includeHistory: true
);
await File.WriteAllTextAsync("session.json", JsonSerializer.Serialize(snapshot));

// Load session
var json = await File.ReadAllTextAsync("session.json");
var snapshot = JsonSerializer.Deserialize<EventBusSnapshot>(json);
bus.Restore(snapshot);
```

---

## 🎯 Best Practices

1. **Use Built-in Considerations**: Don't reinvent `ThresholdValue` or `Cooldown`
2. **Add TimeSensor by Default**: Most agents benefit from time awareness
3. **Enable IdleModule**: Prevents orchestrator from stopping unexpectedly
4. **Use Memory for Long Conversations**: EventBus history is limited to 100 items per type
5. **Snapshot Critical State**: Use `Snapshot()` / `Restore()` for persistence
6. **Composite Considerations**: Use `AndConsideration` / `OrConsideration` for complex logic

---

## 📚 See Also

- [ARCHITECTURE.md](./ARCHITECTURE.md) - Framework architecture
- [INTEGRATION.md](./INTEGRATION.md) - LLM and external system integration
- [PROPOSAL_PATTERNS.md](./PROPOSAL_PATTERNS.md) - Best practices for proposals
