# Memory System Documentation

## Overview

The Memory System provides long-term fact retention beyond the EventBus's built-in history limits (100 items per type). It's essential for applications that need to maintain context over extended periods, such as conversational AI agents, analytics systems, and audit logging.

**Key Concepts:**
- **EventBus** - Short-term memory (last 100 events per type)
- **IMemoryStore** - Long-term memory (unlimited retention)
- **Automatic archival** - MemorySensor moves old facts to storage
- **Flexible querying** - Query by time, filters, or custom criteria
- **Pruning** - Remove very old data to manage storage

---

## Architecture

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

---

## IMemoryStore Interface

The core interface for long-term memory storage.

### Interface Definition

```csharp
public interface IMemoryStore
{
    /// Store a fact with timestamp
    Task StoreAsync<T>(T fact, DateTimeOffset timestamp, CancellationToken ct = default)
        where T : class;

    /// Recall facts based on query
    Task<IReadOnlyList<TimestampedMemory<T>>> RecallAsync<T>(
        MemoryQuery query,
        CancellationToken ct = default)
        where T : class;

    /// Count stored facts of a type
    Task<int> CountAsync<T>(CancellationToken ct = default)
        where T : class;

    /// Remove old facts beyond retention period
    Task PruneAsync(TimeSpan retentionPeriod, CancellationToken ct = default);
}
```

### TimestampedMemory<T>

A fact retrieved from memory with its timestamp.

```csharp
public sealed record TimestampedMemory<T>(T Fact, DateTimeOffset Timestamp)
    where T : class;
```

### MemoryQuery

Query parameters for recalling facts from memory.

```csharp
public sealed record MemoryQuery
{
    public int MaxResults { get; init; } = 100;
    public DateTimeOffset? After { get; init; }
    public DateTimeOffset? Before { get; init; }
    public TimeSpan? TimeWindow { get; init; }
    public SortOrder SortOrder { get; init; } = SortOrder.NewestFirst;
}
```

**Query Options:**
- `MaxResults` - Limit number of results (default: 100)
- `After` - Only return facts after this timestamp
- `Before` - Only return facts before this timestamp
- `TimeWindow` - Search within this window from now (e.g., last 7 days)
- `SortOrder` - `NewestFirst` or `OldestFirst`

---

## InMemoryStore

Built-in implementation for simple scenarios and testing. Stores all facts in memory using thread-safe collections.

### Features

- ✅ Thread-safe
- ✅ Fast queries
- ✅ No external dependencies
- ✅ Perfect for testing
- ⚠️ Data lost when process ends
- ⚠️ Not suitable for production (no persistence)

### Usage

```csharp
using UtilityAi.Memory;

var store = new InMemoryStore();

// Store facts
var fact = new UserMessage("Hello", "user-1", DateTimeOffset.UtcNow);
await store.StoreAsync(fact, DateTimeOffset.UtcNow);

// Recall recent facts
var query = new MemoryQuery
{
    MaxResults = 10,
    TimeWindow = TimeSpan.FromHours(1),
    SortOrder = SortOrder.NewestFirst
};
var memories = await store.RecallAsync<UserMessage>(query);

// Count stored facts
var count = await store.CountAsync<UserMessage>();
Console.WriteLine($"Stored {count} messages");

// Prune old data (remove facts older than 7 days)
await store.PruneAsync(TimeSpan.FromDays(7));

// Clear everything (testing only)
store.Clear();
```

### Example: Testing

```csharp
[Fact]
public async Task InMemoryStore_StoreAndRecall()
{
    var store = new InMemoryStore();
    var now = DateTimeOffset.UtcNow;

    // Store multiple facts
    await store.StoreAsync(new UserMessage("Hello", "user-1", now), now);
    await store.StoreAsync(new UserMessage("Hi", "user-2", now.AddMinutes(1)), now.AddMinutes(1));

    // Recall all
    var query = new MemoryQuery { MaxResults = 10 };
    var results = await store.RecallAsync<UserMessage>(query);

    Assert.Equal(2, results.Count);
}
```

---

## MemorySensor

Automatically archives old EventBus facts to long-term storage. See [Sensor documentation](../Sensor/BuiltIn/README.md#memorysensor) for details.

---

## Query Examples

### Time Window Query

```csharp
// Get facts from the last 24 hours
var query = new MemoryQuery
{
    TimeWindow = TimeSpan.FromHours(24),
    MaxResults = 100,
    SortOrder = SortOrder.NewestFirst
};
var recent = await store.RecallAsync<UserMessage>(query);
```

### Date Range Query

```csharp
// Get facts between two dates
var query = new MemoryQuery
{
    After = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
    Before = new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero),
    MaxResults = 1000
};
var january = await store.RecallAsync<UserMessage>(query);
```

### Oldest First

```csharp
// Get oldest facts first (useful for processing backlog)
var query = new MemoryQuery
{
    MaxResults = 50,
    SortOrder = SortOrder.OldestFirst
};
var oldest = await store.RecallAsync<TaskCompleted>(query);
```

---

## Use Cases

### 1. Long Conversational History

**Problem:** LLM needs context from a 2-hour conversation, but EventBus only keeps 100 messages.

**Solution:**
```csharp
// Setup
var memoryStore = new InMemoryStore();
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddSensor(new MemorySensor(
        store: memoryStore,
        archiveThreshold: TimeSpan.FromMinutes(10),
        typeof(UserMessage),
        typeof(AssistantMessage)
    ))
    .AddModule(new LLMModule(memoryStore));

// In LLMModule
public async Task<string> BuildContext(Runtime rt, CancellationToken ct)
{
    // Recent from EventBus
    var recent = rt.Bus.GetHistory<UserMessage>(maxItems: 10);

    // Historical from memory
    var historical = await _memoryStore.RecallAsync<UserMessage>(
        new MemoryQuery
        {
            TimeWindow = TimeSpan.FromHours(2),
            MaxResults = 100
        },
        ct
    );

    // Combine for full context
    return CombineForLLM(historical, recent);
}
```

### 2. Analytics & Reporting

**Problem:** Need to analyze patterns over days/weeks.

**Solution:**
```csharp
// Store all API calls
var memoryStore = new InMemoryStore();
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddSensor(new MemorySensor(
        store: memoryStore,
        archiveThreshold: TimeSpan.FromMinutes(5),
        typeof(ApiCall)
    ));

// Later: analyze patterns
public async Task<Report> GenerateReport(DateTimeOffset start, DateTimeOffset end)
{
    var query = new MemoryQuery
    {
        After = start,
        Before = end,
        MaxResults = 10000
    };

    var calls = await memoryStore.RecallAsync<ApiCall>(query);

    return new Report
    {
        TotalCalls = calls.Count,
        AveragePerHour = calls.Count / (end - start).TotalHours,
        PeakHour = CalculatePeakHour(calls)
    };
}
```

### 3. Audit Logging

**Problem:** Need to retain all events for compliance (90 days).

**Solution:**
```csharp
// Archive everything
var memoryStore = new InMemoryStore();
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    .AddSensor(new MemorySensor(
        store: memoryStore,
        archiveThreshold: TimeSpan.FromMinutes(1),
        typeof(UserAction),
        typeof(AdminAction),
        typeof(DataAccess)
    ));

// Prune old data (run periodically)
await memoryStore.PruneAsync(TimeSpan.FromDays(90));

// Audit query
var auditQuery = new MemoryQuery
{
    After = incident.Timestamp.AddHours(-1),
    Before = incident.Timestamp.AddHours(1),
    MaxResults = 1000
};
var events = await memoryStore.RecallAsync<UserAction>(auditQuery);
```

### 4. User Personalization

**Problem:** Build user profile from interaction history.

**Solution:**
```csharp
public class UserProfileBuilder
{
    private readonly IMemoryStore _memory;

    public async Task<UserProfile> BuildProfile(string userId, CancellationToken ct)
    {
        // Get all user messages (last 30 days)
        var query = new MemoryQuery
        {
            TimeWindow = TimeSpan.FromDays(30),
            MaxResults = 1000
        };

        var messages = await _memory.RecallAsync<UserMessage>(query, ct);
        var userMessages = messages.Where(m => m.Fact.UserId == userId).ToList();

        return new UserProfile
        {
            UserId = userId,
            MessageCount = userMessages.Count,
            AverageMessageLength = userMessages.Average(m => m.Fact.Text.Length),
            CommonTopics = ExtractTopics(userMessages),
            ActiveHours = CalculateActiveHours(userMessages)
        };
    }
}
```

---

## Production Implementations

### File-Based Storage

```csharp
public class FileMemoryStore : IMemoryStore
{
    private readonly string _basePath;

    public FileMemoryStore(string basePath)
    {
        _basePath = basePath;
    }

    public async Task StoreAsync<T>(T fact, DateTimeOffset timestamp, CancellationToken ct)
        where T : class
    {
        var typeName = typeof(T).Name;
        var directory = Path.Combine(_basePath, typeName);
        Directory.CreateDirectory(directory);

        var fileName = $"{timestamp:yyyyMMdd-HHmmss-fff}_{Guid.NewGuid()}.json";
        var filePath = Path.Combine(directory, fileName);

        var json = JsonSerializer.Serialize(new
        {
            Timestamp = timestamp,
            Fact = fact
        });

        await File.WriteAllTextAsync(filePath, json, ct);
    }

    // Implement RecallAsync, CountAsync, PruneAsync...
}
```

### SQL Storage (Entity Framework)

```csharp
public class SqlMemoryStore : IMemoryStore
{
    private readonly DbContext _context;

    public async Task StoreAsync<T>(T fact, DateTimeOffset timestamp, CancellationToken ct)
        where T : class
    {
        var entry = new MemoryEntry
        {
            TypeName = typeof(T).FullName,
            Timestamp = timestamp,
            Data = JsonSerializer.Serialize(fact)
        };

        _context.MemoryEntries.Add(entry);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TimestampedMemory<T>>> RecallAsync<T>(
        MemoryQuery query,
        CancellationToken ct)
        where T : class
    {
        var typeName = typeof(T).FullName;
        var queryable = _context.MemoryEntries
            .Where(e => e.TypeName == typeName);

        // Apply filters
        if (query.After.HasValue)
            queryable = queryable.Where(e => e.Timestamp >= query.After.Value);

        if (query.Before.HasValue)
            queryable = queryable.Where(e => e.Timestamp <= query.Before.Value);

        // Order and limit
        queryable = query.SortOrder == SortOrder.NewestFirst
            ? queryable.OrderByDescending(e => e.Timestamp)
            : queryable.OrderBy(e => e.Timestamp);

        var entries = await queryable
            .Take(query.MaxResults)
            .ToListAsync(ct);

        return entries.Select(e => new TimestampedMemory<T>(
            JsonSerializer.Deserialize<T>(e.Data)!,
            e.Timestamp
        )).ToList();
    }

    // Implement CountAsync, PruneAsync...
}
```

### Redis Storage

```csharp
public class RedisMemoryStore : IMemoryStore
{
    private readonly IConnectionMultiplexer _redis;

    public async Task StoreAsync<T>(T fact, DateTimeOffset timestamp, CancellationToken ct)
        where T : class
    {
        var db = _redis.GetDatabase();
        var key = $"memory:{typeof(T).Name}:{timestamp.Ticks}";
        var value = JsonSerializer.Serialize(fact);

        await db.StringSetAsync(key, value);

        // Add to sorted set for querying
        var setKey = $"memory:index:{typeof(T).Name}";
        await db.SortedSetAddAsync(setKey, key, timestamp.Ticks);
    }

    // Implement RecallAsync using ZRANGE, CountAsync, PruneAsync...
}
```

---

## Best Practices

### 1. Choose the Right Storage

| Scenario | Recommendation |
|----------|----------------|
| Development | `InMemoryStore` |
| Testing | `InMemoryStore` |
| Single-instance production | `FileMemoryStore` |
| Multi-instance production | `SqlMemoryStore` or `RedisMemoryStore` |
| High throughput | `RedisMemoryStore` |
| Compliance/audit | `SqlMemoryStore` (with backup) |

### 2. Prune Regularly

```csharp
// Run pruning periodically (e.g., daily)
public class PruningModule : ICapabilityModule
{
    private readonly IMemoryStore _store;

    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        yield return new Proposal(
            id: "prune-memory",
            cons: new[]
            {
                new TimeWindow(new TimeOnly(3, 0), new TimeOnly(4, 0)),  // 3-4 AM
                new Cooldown<MemoryPruned>(TimeSpan.FromHours(24))
            },
            act: async ct =>
            {
                await _store.PruneAsync(TimeSpan.FromDays(90), ct);
                rt.Bus.Publish(new MemoryPruned(DateTimeOffset.UtcNow));
            }
        );
    }
}
```

### 3. Optimize Queries

```csharp
// ✅ Good: Use time window for recent data
var query = new MemoryQuery
{
    TimeWindow = TimeSpan.FromHours(24),
    MaxResults = 100
};

// ❌ Bad: No filters, retrieves everything
var query = new MemoryQuery
{
    MaxResults = 100000  // Potentially slow
};
```

### 4. Monitor Storage Growth

```csharp
// Track storage metrics
public async Task MonitorStorage()
{
    var count = await store.CountAsync<UserMessage>();
    var sizeEstimate = count * 500; // Rough estimate

    if (sizeEstimate > 1_000_000_000) // 1 GB
    {
        Console.WriteLine("Warning: Storage approaching 1 GB");
        // Consider pruning or archiving
    }
}
```

### 5. Test Backup/Restore

```csharp
// Ensure your production store can be backed up
public async Task BackupTest()
{
    var store = new SqlMemoryStore(context);

    // Store test data
    await store.StoreAsync(new TestFact("data"), DateTimeOffset.UtcNow);

    // Backup database
    await BackupDatabase();

    // Restore database
    await RestoreDatabase();

    // Verify data
    var query = new MemoryQuery { MaxResults = 10 };
    var results = await store.RecallAsync<TestFact>(query);

    Assert.NotEmpty(results);
}
```

---

## Performance Considerations

### InMemoryStore
- **Storage:** O(1) insert
- **Recall:** O(n) scan (where n = total facts of type)
- **Memory:** All data in RAM
- **Scalability:** Limited by available RAM

### SQL-based Store
- **Storage:** O(1) insert (with proper indexes)
- **Recall:** O(log n) with indexes on timestamp
- **Memory:** Independent of data size
- **Scalability:** Excellent (supports billions of rows)

### Redis-based Store
- **Storage:** O(1) insert
- **Recall:** O(log n) using sorted sets
- **Memory:** All data in RAM (but distributed)
- **Scalability:** Excellent for read-heavy workloads

---

## Troubleshooting

### Issue: Memory grows unbounded

**Solution:** Enable automatic pruning

```csharp
// Add pruning module
.AddModule(new PruningModule(store, retentionPeriod: TimeSpan.FromDays(30)))
```

### Issue: Slow queries

**Solution:** Add indexes, limit results, use time windows

```csharp
// Always use MaxResults
var query = new MemoryQuery
{
    MaxResults = 100,  // Don't omit this!
    TimeWindow = TimeSpan.FromDays(7)
};
```

### Issue: Data lost on restart (InMemoryStore)

**Solution:** Use persistent storage for production

```csharp
// Development: InMemoryStore
var store = new InMemoryStore();

// Production: FileMemoryStore or SqlMemoryStore
var store = new FileMemoryStore("/var/app/memory");
```

---

## See Also

- [BUILT_IN_COMPONENTS.md](../../docs/BUILT_IN_COMPONENTS.md) - Complete components reference
- [Sensor/BuiltIn/README.md](../Sensor/BuiltIn/README.md#memorysensor) - MemorySensor documentation
- [ARCHITECTURE.md](../../docs/ARCHITECTURE.md) - Framework architecture
