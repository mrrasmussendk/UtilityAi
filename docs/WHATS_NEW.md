# What's New in UtilityAI

## 🚀 Major Enhancements

This release transforms UtilityAI from a solid foundation into a **production-ready framework** with comprehensive built-in components, memory retention, and persistence.

---

## 📦 New Built-in Considerations (15+)

### General Considerations
- ✅ **ThresholdValue<T>** - Boolean checks for numeric thresholds
- ✅ **RangeValue<T>** - Check if values fall within ranges
- ✅ **InverseValue<T>** - Invert normalized values
- ✅ **TimeSinceEvent<T>** - Score based on time elapsed since events
- ✅ **Cooldown<T>** - Prevent repeated actions with cooldown periods
- ✅ **CollectionSize<T>** - Score based on collection sizes
- ✅ **AnyMatch<TFact, TItem>** - Check if any collection item matches
- ✅ **AllMatch<TFact, TItem>** - Check if all collection items match
- ✅ **TimeWindow** - Score based on time of day / day of week
- ✅ **RandomValue** - Add exploration/randomness
- ✅ **WeightedRandomValue<T>** - Blend deterministic and random scoring
- ✅ **ConstantValue** - Fixed values for testing and weights

### Composite Considerations
- ✅ **AndConsideration** - Multiply considerations (AND logic)
- ✅ **OrConsideration** - Take maximum (OR logic)
- ✅ **NotConsideration** - Invert consideration results

**Before:**
```csharp
// Users had to write these themselves
public class MyThresholdCheck : IConsideration { /* ... */ }
```

**After:**
```csharp
// Built-in and ready to use
new ThresholdValue<TaskPriority>(t => t.Value, threshold: 0.7)
```

---

## 🔬 Built-in Sensors

### TimeSensor
Publishes time-related facts every tick:
- `CurrentTime(DateTimeOffset)`
- `TickNumber(int)`
- `ElapsedTime(TimeSpan)`

### ConversationHistorySensor
Analyzes message history and publishes conversation metadata:
- Message counts
- Conversation duration
- Long conversation detection

### EventFrequencySensor<TEvent, TFrequencyFact>
Tracks event frequency over time windows for rate limiting

### ResourceSensor
Monitors system resources:
- CPU usage percentage
- Memory usage in MB

### MemorySensor
Automatically archives old EventBus facts to long-term storage

---

## 🗄️ Memory Retention System

**Problem:** EventBus only keeps last 100 events per type - insufficient for long conversations.

**Solution:** `IMemoryStore` interface with `InMemoryStore` implementation.

```csharp
var store = new InMemoryStore();

// Store facts long-term
await store.StoreAsync(new UserMessage("Hello", "user-1", now), now);

// Recall with flexible queries
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

**Key Features:**
- Query by time window, date range, or custom filters
- Automatic pruning of old data
- Extensible for SQL, Redis, file-based storage

---

## 💾 EventBus Persistence

**New Extension Methods:**

### Snapshot & Restore
```csharp
// Save session
var snapshot = bus.Snapshot(
    typesToCapture: new[] { typeof(UserMessage), typeof(GameState) },
    includeHistory: true
);
await File.WriteAllTextAsync("session.json", JsonSerializer.Serialize(snapshot));

// Load session
var json = await File.ReadAllTextAsync("session.json");
var snapshot = JsonSerializer.Deserialize<EventBusSnapshot>(json);
bus.Restore(snapshot);
```

### Enhanced Queries
```csharp
// Time-window queries
var recent = bus.GetHistoryInWindow<UserMessage>(TimeSpan.FromMinutes(5));

// Predicate filtering
var highPriority = bus.GetHistoryWhere<Task>(t => t.Priority > 0.8);

// Event frequency
var callsPerSecond = bus.GetEventFrequency<ApiCall>(TimeSpan.FromMinutes(1));

// Time since last event
var timeSince = bus.GetTimeSinceLastEvent<UserMessage>();

// Recent event check
if (bus.HasRecentEvent<ErrorOccurred>(TimeSpan.FromSeconds(30)))
{
    // Handle recent error
}
```

---

## 🧩 Built-in Modules

### IdleModule
Fallback module that prevents orchestrator from stopping when no other proposals are eligible.

```csharp
.AddModule(new IdleModule(idleUtility: 0.001))
```

### CleanupModule
Periodically cleans up old facts from the EventBus to manage memory.

```csharp
.AddModule(new CleanupModule(
    typesToClean: new[] { typeof(TempData), typeof(CachedResult) },
    cleanupInterval: TimeSpan.FromMinutes(5)
))
```

### StopOnSignalModule
Gracefully stops orchestration when a `StopSignal` fact is published.

```csharp
.AddModule(new StopOnSignalModule())

// Trigger stop anywhere:
bus.Publish(new StopSignal("User requested shutdown"));
```

---

## 📦 Common Facts

Pre-defined fact types for common scenarios:

```csharp
// Time facts
CurrentTime(DateTimeOffset Value)
TickNumber(int Value)
ElapsedTime(TimeSpan Value)

// Conversation facts
UserMessage(string Text, string UserId, DateTimeOffset Timestamp)
AssistantMessage(string Text, DateTimeOffset Timestamp)
ConversationMetadata(...)

// System facts
ResourceUsage(double CpuPercent, double MemoryMegabytes, DateTimeOffset Timestamp)
RateLimitStatus(int RemainingRequests, DateTimeOffset ResetTime, bool IsLimited)
StopSignal(string Reason)
```

---

## 🧪 Testing

**Added 31 new tests** covering all new components:
- ThresholdValue, RangeValue, Cooldown tests
- Composite consideration tests (AND, OR, NOT)
- Memory store tests (Store, Recall, Prune)
- EventBus extensions tests (Snapshot, Restore, Queries)
- Sensor tests (Time, History, Resource)
- Module tests (Idle, Cleanup, StopOnSignal)

**Total: 100 tests, all passing** ✅

---

## 📚 Documentation

### New Documentation
- **[BUILT_IN_COMPONENTS.md](./BUILT_IN_COMPONENTS.md)** - Complete reference for all built-ins
- **[WHATS_NEW.md](./WHATS_NEW.md)** - This document

### Updated Documentation
- **[README.md](../README.md)** - Updated features list and links
- **[ARCHITECTURE.md](./ARCHITECTURE.md)** - Added memory system references

---

## 🎯 Migration Guide

### Before (v1.1.4)
```csharp
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddModule(new MyCustomModule());

// Users had to implement:
// - All considerations from scratch
// - Time tracking manually
// - Session persistence logic
// - Memory management
```

### After (v2.0.0)
```csharp
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    // Built-in sensors
    .AddSensor(new TimeSensor())
    .AddSensor(new ConversationHistorySensor())

    // Your domain modules
    .AddModule(new MyCustomModule())

    // Built-in safety nets
    .AddModule(new IdleModule())
    .AddModule(new StopOnSignalModule());

// Use built-in considerations
new ThresholdValue<Confidence>(c => c.Score, 0.7)
new Cooldown<ApiCall>(TimeSpan.FromSeconds(30))
new TimeSinceEvent<UserMessage>(curve, inputDomain)

// Use built-in persistence
var snapshot = bus.Snapshot(types, includeHistory: true);
bus.Restore(snapshot);

// Use memory retention
var store = new InMemoryStore();
await store.StoreAsync(fact, timestamp);
var memories = await store.RecallAsync<T>(query);
```

---

## ⚡ Breaking Changes

### IConsideration Interface
**All** considerations now require a `Name` property for better debugging and observability.

**Before:**
```csharp
public class MyConsideration : IConsideration
{
    public double Evaluate(Runtime rt) { /* ... */ }
}
```

**After:**
```csharp
public class MyConsideration : IConsideration
{
    public string Name => "MyConsideration";
    public double Evaluate(Runtime rt) { /* ... */ }
}
```

**Impact:** Existing custom considerations need to add the `Name` property.

---

## 📊 Statistics

- **15+ new built-in considerations**
- **5 new built-in sensors**
- **3 new built-in modules**
- **10+ new common fact types**
- **Memory retention system** with IMemoryStore
- **EventBus persistence** with Snapshot/Restore
- **Enhanced query extensions** (6 new methods)
- **31 new tests** (100 total, all passing)
- **1 new comprehensive documentation file**

---

## 🔮 Future Enhancements

Potential future additions:
- SQL/Redis memory store implementations
- More built-in sensors (network, file system)
- Built-in rate limiting module
- Performance benchmarks
- Visual Studio/Rider templates
- Real-world integration examples with OpenAI/Anthropic

---

## 📞 Getting Started

1. **Read the documentation:**
   - [BUILT_IN_COMPONENTS.md](./BUILT_IN_COMPONENTS.md) - Full component reference
   - [ARCHITECTURE.md](./ARCHITECTURE.md) - Framework architecture
   - [INTEGRATION.md](./INTEGRATION.md) - LLM integration examples

2. **Check the examples:**
   - [Example/](../Example/) - Working demonstrations

3. **Run the tests:**
   ```bash
   dotnet test
   ```

---

## 💡 Key Takeaways

UtilityAI is now **production-ready** with:
- ✅ **Batteries included** - 15+ built-in considerations
- ✅ **Memory retention** - Long-term fact storage
- ✅ **Persistence** - Save/restore session state
- ✅ **Comprehensive tests** - 100 tests, all passing
- ✅ **Well documented** - Complete component reference
- ✅ **SOLID principles** - Clean, extensible architecture
- ✅ **Zero dependencies** - Pure .NET 8

The framework is ready for real-world AI agent applications! 🚀
