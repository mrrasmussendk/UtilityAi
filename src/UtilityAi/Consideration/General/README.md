# Built-in Considerations (`Consideration/General`)

This folder contains the default scoring primitives used by UtilityAi proposals.

All considerations implement `IConsideration` and return a score in the `[0.0, 1.0]` range.

## How to choose

- Use **eligibility** (`IEligibility`) for **hard gates** (proposal excluded entirely).
- Use **considerations** for **scoring** (proposal remains in the candidate set, but may score low).

> `HasFact<T>` and `NotHasFact<T>` are considerations. If missing/present facts should hard-block proposals, prefer `HasFactEligible<T>` / `NotHasFactEligible<T>`.

## Built-in types in this folder

### Fact presence and simple scoring

- `HasFact<T>(string? name = null)`
  - `1.0` when fact exists, else `0.0`.
- `NotHasFact<T>(string? name = null)`
  - `1.0` when fact does not exist, else `0.0`.
- `HasFactWhere<T>(Func<T, bool> predicate, string? name = null)`
  - `1.0` when fact exists and predicate passes, else `0.0`.
- `FactExists<T>(string? name = null)` / `FactMissing<T>(string? name = null)`
  - Alias names for `HasFact<T>` / `NotHasFact<T>`.
- `FixedValueConsideration(string name, double value)`
  - Always returns a clamped fixed value.
- `ConstantValue(double value)`
  - Always returns a clamped fixed value.

### Thresholds and ranges

- `ThresholdValue<T>(Func<T, double> selector, double threshold, bool above = true)`
  - Binary threshold check (`>` when `above = true`, `<` otherwise).
- `RangeValue<T>(Func<T, double> selector, double min, double max, bool inclusive = true)`
  - Binary range check.
- `InverseValue<T>(Func<T, double> selector)`
  - Returns `1.0 - selector(fact)` (clamped).

### Curve-based scoring

- `SignalConsideration<T>(string name, Func<T, double> selector, Func<double, double> curve, (double min, double max) inputDomain)`
  - Reads a fact value, normalizes to `0..1`, applies response curve.
- `CurveSignal<TSignal>(string name, Func<TSignal, double> project, Func<double, double> curve, double defaultValue = 0.5)`
  - Applies a curve to a signal already expected in `0..1` (uses `defaultValue` if fact is missing).
- `TimeSinceEvent<T>(ICurve curve, (double min, double max) inputDomain)`
  - Scores by elapsed time since the most recent event of type `T`.
- `CollectionSize<T>(Func<T, int> sizeSelector, ICurve curve, (int min, int max) inputDomain)`
  - Scores based on normalized collection size.

### Temporal and anti-repeat controls

- `Cooldown<T>(TimeSpan cooldownPeriod)`
  - `0.0` while cooldown is active after most recent event `T`, otherwise `1.0`.
- `TimeWindow(TimeOnly startTime, TimeOnly endTime, DayOfWeek[]? allowedDays = null)`
  - `1.0` only inside the configured UTC time window (supports windows crossing midnight).
- `NoRepeatConsideration(string proposalId, int lookback = 5, double penalty = 0.0)`
  - Penalizes/block repeated proposal IDs using execution history from the bus.

### Collection predicates

- `AnyMatch<TFact, TItem>(Func<TFact, IEnumerable<TItem>> collectionSelector, Func<TItem, bool> predicate)`
  - `1.0` if any item matches.
- `AllMatch<TFact, TItem>(Func<TFact, IEnumerable<TItem>> collectionSelector, Func<TItem, bool> predicate)`
  - `1.0` if all items match (including empty collections).

### Exploration and stochastic behavior

- `RandomValue()`
  - Uniform random score in `0..1`.
- `WeightedRandomValue<T>(Func<T, double> scoreSelector, double deterministicWeight = 0.5)`
  - Blends deterministic score with randomness:
  - `deterministicWeight * score + (1 - deterministicWeight) * random`

## Eligibility types provided in this folder

These are not considerations, but they are commonly used together with them:

- `HasFactEligible<T>`
- `NotHasFactEligible<T>`
- `NoRepeatEligible(string id)`
- `HasIntentParametersEligible(IReadOnlyList<string> requiredParameters)`

Use these when proposals should be filtered out before scoring.
