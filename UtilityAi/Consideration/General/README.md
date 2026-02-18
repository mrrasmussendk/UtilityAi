# Built-in Considerations Reference

This document provides detailed documentation for all built-in considerations in UtilityAI.

## Overview

Considerations are the building blocks of utility scoring. They evaluate runtime state and return a value between 0.0 and 1.0 that represents how well the current state matches the consideration's criteria.

**Key Concepts:**
- All considerations return values in the range `[0.0, 1.0]`
- `0.0` = condition not met / lowest utility
- `1.0` = condition fully met / highest utility
- Intermediate values represent partial matches
- Considerations are stateless and deterministic (except Random*)

---

## General Considerations

### HasFact<T>

**Purpose:** Checks if a fact of type T exists in the EventBus.

**Signature:**
```csharp
public HasFact(bool shouldHave = true)
```

**Parameters:**
- `shouldHave` - If true, returns 1.0 when fact exists. If false, returns 1.0 when fact doesn't exist.

**Returns:**
- `1.0` if condition is met
- `0.0` if condition is not met

**Example:**
```csharp
// Require a UserMessage to exist
new HasFact<UserMessage>(shouldHave: true)

// Require that processing is NOT complete
new HasFact<ProcessingComplete>(shouldHave: false)
```

**Use Cases:**
- Gating actions that require specific facts
- Preventing actions when certain conditions exist
- Simple boolean logic in proposals

---

### ThresholdValue<T>

**Purpose:** Evaluates whether a numeric value from a fact exceeds or falls below a threshold.

**Signature:**
```csharp
public ThresholdValue(
    Func<T, double> selector,
    double threshold,
    bool above = true
)
```

**Parameters:**
- `selector` - Function to extract the numeric value from the fact
- `threshold` - The threshold value to compare against
- `above` - If true, returns 1.0 when value > threshold. If false, returns 1.0 when value < threshold.

**Returns:**
- `1.0` if condition is met
- `0.0` if condition is not met

**Example:**
```csharp
// High priority tasks (above threshold)
new ThresholdValue<TaskPriority>(
    selector: t => t.Value,
    threshold: 0.7,
    above: true
)

// Low CPU usage (below threshold)
new ThresholdValue<ResourceUsage>(
    selector: r => r.CpuPercent,
    threshold: 50.0,
    above: false
)
```

**Use Cases:**
- Priority-based task selection
- Resource constraints
- Confidence thresholds
- Health checks

---

### RangeValue<T>

**Purpose:** Evaluates whether a numeric value falls within a specified range.

**Signature:**
```csharp
public RangeValue(
    Func<T, double> selector,
    double min,
    double max,
    bool inclusive = true
)
```

**Parameters:**
- `selector` - Function to extract the numeric value from the fact
- `min` - Minimum value of the range
- `max` - Maximum value of the range
- `inclusive` - If true, includes boundary values. If false, excludes them.

**Returns:**
- `1.0` if value is within range
- `0.0` if value is outside range

**Example:**
```csharp
// Comfortable temperature range
new RangeValue<Temperature>(
    selector: t => t.Celsius,
    min: 18.0,
    max: 24.0,
    inclusive: true
)

// Valid percentage (exclusive boundaries)
new RangeValue<Confidence>(
    selector: c => c.Score,
    min: 0.0,
    max: 1.0,
    inclusive: false
)
```

**Use Cases:**
- Valid input ranges
- Comfort zones
- Optimal operating conditions
- Percentage validations

---

### InverseValue<T>

**Purpose:** Inverts a normalized value (1.0 - value). Useful for representing "lack of" or "opposite of" a property.

**Signature:**
```csharp
public InverseValue(Func<T, double> selector)
```

**Parameters:**
- `selector` - Function to extract the numeric value (0.0 to 1.0) from the fact

**Returns:**
- `1.0 - value` (clamped to [0.0, 1.0])

**Example:**
```csharp
// High utility when confidence is LOW
new InverseValue<Confidence>(c => c.Score)

// Prefer when battery is LOW
new InverseValue<BatteryLevel>(b => b.Percentage)
```

**Use Cases:**
- Inverting confidence scores
- Representing urgency (low time remaining = high urgency)
- Resource depletion (low resources = high need to replenish)

---

### CurveSignal<T>

**Purpose:** Maps a value through a response curve to produce a utility score. Already exists in the framework, documented here for completeness.

**Signature:**
```csharp
public CurveSignal(
    Func<T, double> selector,
    ICurve curve,
    (double min, double max) inputDomain,
    (double min, double max)? outputDomain = null
)
```

**Parameters:**
- `selector` - Function to extract the signal value from the fact
- `curve` - Response curve to map the normalized input
- `inputDomain` - Expected range of input values
- `outputDomain` - Optional output range (defaults to [0.0, 1.0])

**Returns:**
- Curve-mapped value in [0.0, 1.0]

**Example:**
```csharp
// Task age with logistic curve (urgency increases over time)
new CurveSignal<TaskAge>(
    selector: t => t.Minutes,
    curve: Curves.Logistic(k: 0.1, x0: 30),
    inputDomain: (0, 60)
)
```

**Available Curves:**
- `Curves.Identity()` - Linear mapping
- `Curves.Logistic(k, x0)` - S-curve
- `Curves.Power(gamma)` - Exponential
- `Curves.PiecewiseLinear(points)` - Custom segments
- `Curves.MonotoneCubic(points)` - Smooth interpolation

---

### TimeSinceEvent<T>

**Purpose:** Evaluates the time elapsed since the most recent event of a given type, mapped through a response curve.

**Signature:**
```csharp
public TimeSinceEvent(
    ICurve curve,
    (double min, double max) inputDomain
)
```

**Parameters:**
- `curve` - Response curve to map elapsed seconds to utility
- `inputDomain` - Expected range of elapsed seconds (min, max)

**Returns:**
- `0.0` if no events exist
- Curve-mapped value based on elapsed time

**Example:**
```csharp
// Increase urgency to respond as time passes
new TimeSinceEvent<UserMessage>(
    curve: Curves.Logistic(k: 0.05, x0: 60),
    inputDomain: (0, 300)  // 0 to 5 minutes
)

// Decrease relevance of old data
new TimeSinceEvent<DataFetch>(
    curve: Curves.Power(gamma: 0.5),  // Decay curve
    inputDomain: (0, 3600)  // 0 to 1 hour
)
```

**Use Cases:**
- Urgency modeling (respond to old messages)
- Data freshness (prefer recent data)
- Cooldown visualization
- Timeout detection

---

### Cooldown<T>

**Purpose:** Prevents repeated execution of an action by requiring a cooldown period. Returns 0.0 if cooldown is active, 1.0 if ready.

**Signature:**
```csharp
public Cooldown(TimeSpan cooldownPeriod)
```

**Parameters:**
- `cooldownPeriod` - Minimum time that must elapse between events

**Returns:**
- `1.0` if no previous event OR cooldown expired
- `0.0` if cooldown is still active

**Example:**
```csharp
// Prevent API calls within 30 seconds of each other
new Cooldown<ApiCallMade>(TimeSpan.FromSeconds(30))

// Rate limit user notifications (5 minutes)
new Cooldown<NotificationSent>(TimeSpan.FromMinutes(5))

// Debounce rapid user inputs (1 second)
new Cooldown<UserInput>(TimeSpan.FromSeconds(1))
```

**Use Cases:**
- Rate limiting
- Debouncing
- Preventing spam
- Resource conservation
- API quota management

---

### CollectionSize<T>

**Purpose:** Scores based on the size of a collection from a fact, mapped through a response curve.

**Signature:**
```csharp
public CollectionSize(
    Func<T, int> sizeSelector,
    ICurve curve,
    (int min, int max) inputDomain
)
```

**Parameters:**
- `sizeSelector` - Function to extract the collection size from the fact
- `curve` - Response curve to map size to utility
- `inputDomain` - Expected range of collection sizes

**Returns:**
- `0.0` if fact doesn't exist
- Curve-mapped value based on collection size

**Example:**
```csharp
// Process queue when it has items (logistic increase)
new CollectionSize<TaskQueue>(
    sizeSelector: q => q.Count,
    curve: Curves.Logistic(k: 0.2, x0: 5),
    inputDomain: (0, 20)
)

// Batch processing (prefer larger batches)
new CollectionSize<PendingItems>(
    sizeSelector: p => p.Items.Count,
    curve: Curves.Power(gamma: 1.5),
    inputDomain: (0, 100)
)
```

**Use Cases:**
- Queue processing
- Batch optimization
- Load balancing
- Work distribution

---

### AnyMatch<TFact, TItem>

**Purpose:** Returns 1.0 if any item in a collection matches a predicate, 0.0 otherwise.

**Signature:**
```csharp
public AnyMatch(
    Func<TFact, IEnumerable<TItem>> collectionSelector,
    Func<TItem, bool> predicate
)
```

**Parameters:**
- `collectionSelector` - Function to extract the collection from the fact
- `predicate` - Predicate to test each item

**Returns:**
- `1.0` if any item matches
- `0.0` if no items match or fact doesn't exist

**Example:**
```csharp
// Check if any task is high priority
new AnyMatch<TaskQueue, Task>(
    collectionSelector: q => q.Tasks,
    predicate: t => t.Priority > 0.8
)

// Check if any error is critical
new AnyMatch<ErrorLog, Error>(
    collectionSelector: log => log.Errors,
    predicate: e => e.Severity == ErrorSeverity.Critical
)
```

**Use Cases:**
- Validation checks
- Priority detection
- Error scanning
- Condition monitoring

---

### AllMatch<TFact, TItem>

**Purpose:** Returns 1.0 if all items in a collection match a predicate, 0.0 otherwise.

**Signature:**
```csharp
public AllMatch(
    Func<TFact, IEnumerable<TItem>> collectionSelector,
    Func<TItem, bool> predicate
)
```

**Parameters:**
- `collectionSelector` - Function to extract the collection from the fact
- `predicate` - Predicate to test each item

**Returns:**
- `1.0` if all items match (or collection is empty)
- `0.0` if any item doesn't match or fact doesn't exist

**Example:**
```csharp
// Verify all validations passed
new AllMatch<ValidationResults, Result>(
    collectionSelector: vr => vr.Results,
    predicate: r => r.IsValid
)

// Check if all workers are idle
new AllMatch<WorkerPool, Worker>(
    collectionSelector: p => p.Workers,
    predicate: w => w.Status == WorkerStatus.Idle
)
```

**Use Cases:**
- Completion checks
- Validation gates
- Readiness verification
- State synchronization

---

### TimeWindow

**Purpose:** Returns 1.0 during specified time windows (e.g., business hours), 0.0 otherwise.

**Signature:**
```csharp
public TimeWindow(
    TimeOnly startTime,
    TimeOnly endTime,
    DayOfWeek[]? allowedDays = null
)
```

**Parameters:**
- `startTime` - Start of the time window (inclusive)
- `endTime` - End of the time window (exclusive)
- `allowedDays` - Optional array of allowed days. If null, all days are allowed.

**Returns:**
- `1.0` if current time falls within window
- `0.0` if current time is outside window

**Example:**
```csharp
// Business hours (9 AM to 5 PM, weekdays only)
new TimeWindow(
    startTime: new TimeOnly(9, 0),
    endTime: new TimeOnly(17, 0),
    allowedDays: new[]
    {
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday
    }
)

// Night-time maintenance (11 PM to 6 AM, any day)
new TimeWindow(
    startTime: new TimeOnly(23, 0),
    endTime: new TimeOnly(6, 0)
)
```

**Use Cases:**
- Scheduled operations
- Business hours enforcement
- Time-based task prioritization
- Maintenance windows

---

### RandomValue

**Purpose:** Returns a random value between 0.0 and 1.0. Adds exploration and randomness to decision-making.

**Signature:**
```csharp
public RandomValue()
```

**Returns:**
- Random value in [0.0, 1.0]

**Example:**
```csharp
// Add random exploration to proposals
new Proposal(
    id: "explore",
    cons: new IConsideration[]
    {
        new ConstantValue(0.5),  // Base utility
        new RandomValue()         // Random factor
    },
    act: async ct => { /* ... */ }
)
```

**Use Cases:**
- Exploration vs exploitation
- Breaking ties randomly
- Adding unpredictability
- Testing and experimentation

**Note:** Thread-safe implementation using locked random number generator.

---

### WeightedRandomValue<T>

**Purpose:** Combines deterministic score with randomness using a weight factor.

**Signature:**
```csharp
public WeightedRandomValue(
    Func<T, double> scoreSelector,
    double deterministicWeight = 0.5
)
```

**Parameters:**
- `scoreSelector` - Function to extract the base score (0.0 to 1.0) from the fact
- `deterministicWeight` - Weight for deterministic vs random (0.0 to 1.0). Default is 0.5.

**Formula:**
```
result = (deterministicWeight × score) + ((1 - deterministicWeight) × random)
```

**Returns:**
- Weighted blend of score and random value

**Example:**
```csharp
// 70% deterministic, 30% random
new WeightedRandomValue<Priority>(
    scoreSelector: p => p.Value,
    deterministicWeight: 0.7
)

// Mostly random with slight bias
new WeightedRandomValue<Preference>(
    scoreSelector: p => p.Score,
    deterministicWeight: 0.2
)
```

**Use Cases:**
- Epsilon-greedy strategies
- Softmax-like selection
- Controlled exploration
- Fuzzy decision-making

---

### ConstantValue

**Purpose:** Returns a fixed constant value. Useful for testing, debugging, or fixed weights.

**Signature:**
```csharp
public ConstantValue(double value)
```

**Parameters:**
- `value` - The constant value to return (automatically clamped to [0.0, 1.0])

**Returns:**
- The constant value

**Example:**
```csharp
// Fixed weight in proposals
new ConstantValue(0.8)

// Always eligible (when used in eligibility)
new ConstantValue(1.0)

// For testing
new ConstantValue(0.5)
```

**Use Cases:**
- Testing and debugging
- Fixed weights/priors
- Placeholder values
- Simple gates

---

## Composite Considerations

### AndConsideration

**Purpose:** Combines multiple considerations using multiplication (AND logic). All must score high for the result to be high.

**Signature:**
```csharp
public AndConsideration(params IConsideration[] considerations)
```

**Formula:**
```
result = consideration1 × consideration2 × ... × considerationN
```

**Parameters:**
- `considerations` - Array of considerations to combine

**Returns:**
- Product of all consideration values

**Example:**
```csharp
// All conditions must be met
new AndConsideration(
    new HasFact<UserMessage>(),
    new ThresholdValue<Confidence>(c => c.Score, 0.7),
    new Cooldown<ResponseSent>(TimeSpan.FromSeconds(5))
)
```

**Use Cases:**
- Require multiple conditions
- Strict gating logic
- Combining multiple criteria
- Safety checks

**Note:** Short-circuits on zero (early exit when any consideration returns 0.0).

---

### OrConsideration

**Purpose:** Combines multiple considerations by taking the maximum value (OR logic). Result is high if any consideration scores high.

**Signature:**
```csharp
public OrConsideration(params IConsideration[] considerations)
```

**Formula:**
```
result = max(consideration1, consideration2, ..., considerationN)
```

**Parameters:**
- `considerations` - Array of considerations to combine

**Returns:**
- Maximum value among all considerations

**Example:**
```csharp
// At least one condition should be met
new OrConsideration(
    new HasFact<HighPriority>(),
    new HasFact<UrgentRequest>(),
    new TimeSinceEvent<UserMessage>(/* ... */)
)
```

**Use Cases:**
- Alternative conditions
- Fallback logic
- Multiple valid paths
- Priority selection

**Note:** Short-circuits on 1.0 (early exit when any consideration returns 1.0).

---

### NotConsideration

**Purpose:** Inverts a consideration's result (1.0 - value).

**Signature:**
```csharp
public NotConsideration(IConsideration consideration)
```

**Formula:**
```
result = 1.0 - consideration.Evaluate()
```

**Parameters:**
- `consideration` - The consideration to invert

**Returns:**
- Inverted consideration value

**Example:**
```csharp
// High utility when processing is NOT complete
new NotConsideration(
    new HasFact<ProcessingComplete>()
)

// Prefer when confidence is NOT high
new NotConsideration(
    new ThresholdValue<Confidence>(c => c.Score, 0.8)
)
```

**Use Cases:**
- Negation logic
- Inverted conditions
- Opposite preferences
- Exclusion rules

---

## Best Practices

### Choosing the Right Consideration

1. **Binary conditions** → `HasFact<T>`, `ThresholdValue<T>`, `TimeWindow`
2. **Continuous scoring** → `CurveSignal<T>`, `TimeSinceEvent<T>`, `CollectionSize<T>`
3. **Rate limiting** → `Cooldown<T>`, `EventFrequencySensor`
4. **Collections** → `AnyMatch`, `AllMatch`, `CollectionSize<T>`
5. **Combining logic** → `AndConsideration`, `OrConsideration`, `NotConsideration`
6. **Exploration** → `RandomValue`, `WeightedRandomValue<T>`

### Performance Tips

1. **Use eligibilities for hard gates** - Filter proposals early before scoring
2. **Short-circuit composites** - AND/OR considerations exit early when possible
3. **Cache expensive selectors** - Consider caching extracted values in facts
4. **Prefer built-ins** - Optimized and well-tested

### Common Patterns

```csharp
// Pattern: High priority OR urgent
new OrConsideration(
    new ThresholdValue<Priority>(p => p.Value, 0.8),
    new HasFact<UrgentFlag>()
)

// Pattern: Ready AND not on cooldown
new AndConsideration(
    new HasFact<TaskReady>(),
    new Cooldown<TaskExecuted>(TimeSpan.FromMinutes(5))
)

// Pattern: Time-based with fallback
new OrConsideration(
    new TimeWindow(new TimeOnly(9, 0), new TimeOnly(17, 0)),
    new HasFact<EmergencyOverride>()
)
```

---

## See Also

- [BUILT_IN_COMPONENTS.md](../../docs/BUILT_IN_COMPONENTS.md) - Complete built-in components reference
- [ARCHITECTURE.md](../../docs/ARCHITECTURE.md) - Framework architecture
- [PROPOSAL_PATTERNS.md](../../docs/PROPOSAL_PATTERNS.md) - Best practices for proposals
