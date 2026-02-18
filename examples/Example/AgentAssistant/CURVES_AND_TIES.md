# Response Curves and Tie-Breaking Guide

This guide explains how to avoid unintentional utility ties and when ties are actually useful.

## The Problem: Unintentional Ties

When multiple proposals have **identical utility scores**, the framework picks one based on registration order (or whatever `ISelectionStrategy` does). This can create unpredictable behavior.

### Example of Bad Ties

```csharp
// ❌ BAD: All considerations return binary 0.0 or 1.0
.WithConsideration(new SignalConsideration<Context>(
    name: "needs_research",
    selector: ctx => ctx.RequiresResearch ? 1.0 : 0.0,  // Binary!
    curve: x => x,  // Identity - no shaping!
    inputDomain: (0, 1)))

// Result: utility = 1.0 × (1.0)^1.0 = 1.0 for EVERY proposal that needs research
// Everything ties at 1.0!
```

## Solution 1: Use Response Curves

Response curves shape the utility contribution to create nuanced scores.

### Built-in Curve Patterns

#### Linear (Identity)
```csharp
curve: x => x
```
- **Use when**: Direct proportional relationship
- **Example**: Confidence directly maps to utility
- **Shape**: Straight line from (0,0) to (1,1)

#### Quadratic (Accelerating)
```csharp
curve: x => x * x
```
- **Use when**: Strongly reward high values, penalize medium
- **Example**: Only want high confidence responses
- **Shape**: Slow start, rapid acceleration
- **Effect**: 0.5 → 0.25, 0.7 → 0.49, 0.9 → 0.81

#### Cubic (Very Sharp)
```csharp
curve: x => Math.Pow(x, 3)
```
- **Use when**: Only trigger on extreme values
- **Example**: Clarification only for very low confidence
- **Shape**: Very flat, then sharp rise
- **Effect**: 0.5 → 0.125, 0.7 → 0.343, 0.9 → 0.729

#### Square Root (Diminishing Returns)
```csharp
curve: x => Math.Sqrt(x)
```
- **Use when**: Early gains matter more than later gains
- **Example**: First few rate-limit tokens valuable, rest less so
- **Shape**: Fast start, slow plateau
- **Effect**: 0.5 → 0.707, 0.7 → 0.837, 0.9 → 0.949

#### Logistic S-Curve (Smooth Threshold)
```csharp
curve: x => 1.0 / (1.0 + Math.Exp(-steepness * (x - midpoint)))

// Example: Centered at 0.5, moderate steepness
curve: x => 1.0 / (1.0 + Math.Exp(-10 * (x - 0.5)))
```
- **Use when**: Want a smooth threshold with gradual transitions
- **Example**: Rate limiting that gradually kicks in
- **Shape**: S-curve with adjustable steepness and center
- **Effect**: Smooth transition from low to high around midpoint

#### Inverted (Flip)
```csharp
curve: x => 1.0 - x
```
- **Use when**: Lower is better
- **Example**: Energy cost, latency, error rate
- **Shape**: Descending line

### Real-World Examples from AgentAssistant

#### Example 1: Direct Response (Quadratic Confidence)
```csharp
yield return ProposalHelper.For("message.direct")
    .WithConsideration(new SignalConsideration<ConversationContext>(
        name: "confidence",
        selector: ctx => ctx.Confidence,
        curve: x => x * x,  // ← Quadratic rewards high confidence
        inputDomain: (0, 1)))
```

**Why this works:**
- Confidence 0.5 → utility contribution 0.25 (low)
- Confidence 0.7 → utility contribution 0.49 (moderate)
- Confidence 0.9 → utility contribution 0.81 (high)

Creates **natural differentiation** - no ties!

#### Example 2: Clarification (Cubic Ambiguity)
```csharp
yield return ProposalHelper.For("message.clarify")
    .WithConsideration(new SignalConsideration<ConversationContext>(
        name: "ambiguity",
        selector: ctx => 1.0 - ctx.Confidence,  // Inverted
        curve: x => Math.Pow(x, 3),  // ← Cubic only triggers on very low confidence
        inputDomain: (0, 1)))
```

**Why this works:**
- Confidence 0.9 → ambiguity 0.1 → utility 0.001 (negligible)
- Confidence 0.7 → ambiguity 0.3 → utility 0.027 (still low)
- Confidence 0.3 → ambiguity 0.7 → utility 0.343 (significant)
- Confidence 0.1 → ambiguity 0.9 → utility 0.729 (high)

Only triggers when **truly ambiguous**!

#### Example 3: Rate Limiting (Logistic S-Curve)
```csharp
yield return ProposalHelper.For("research.web")
    .WithConsideration(new SignalConsideration<AvailableTools>(
        name: "rate_limit",
        selector: tools => tools.RateLimitRemaining,
        curve: x => 1.0 / (1.0 + Math.Exp(-0.5 * (x - 5))),  // ← S-curve at midpoint
        inputDomain: (0, 10)))
```

**Why this works:**
- 0 remaining → utility ~0.08 (strongly discouraged)
- 3 remaining → utility ~0.27 (discouraged)
- 5 remaining → utility ~0.50 (neutral)
- 7 remaining → utility ~0.73 (encouraged)
- 10 remaining → utility ~0.92 (strongly encouraged)

Smooth transition, no sudden cliff!

## Solution 2: Use Temperature

Temperature sharpens or flattens the utility curve after considerations are combined:

```
utility = prior × (geometric_mean)^temperature
```

### Temperature Examples

```csharp
// Temperature = 0.5 (flatter, more exploration)
.WithTemperature(0.5)
// geom_mean=0.7 → utility=0.84 (boosted)

// Temperature = 1.0 (default, normal)
.WithTemperature(1.0)
// geom_mean=0.7 → utility=0.70 (unchanged)

// Temperature = 2.0 (sharper, more decisive)
.WithTemperature(2.0)
// geom_mean=0.7 → utility=0.49 (penalized)
```

**Use higher temperature** when you want clear winners.
**Use lower temperature** for exploration/testing.

## When Ties ARE Good (Intentional)

Sometimes you **want** ties and random/round-robin selection:

### Use Case 1: Exploration & A/B Testing
```csharp
// Multiple research strategies - want to try each
yield return ProposalHelper.For("research.web")
    .WithConsideration(...)  // Same considerations
    .WithAction(...);

yield return ProposalHelper.For("research.api")
    .WithConsideration(...)  // Same considerations
    .WithAction(...);

// ✅ GOOD: Tie is intentional - use RandomSelectionStrategy
```

### Use Case 2: Load Balancing
```csharp
// Multiple equivalent servers
yield return ProposalHelper.For("server.east")
    .WithValue("load", currentLoad);  // Same load = tie

yield return ProposalHelper.For("server.west")
    .WithValue("load", currentLoad);  // Same load = tie

// ✅ GOOD: Want fair distribution - use RoundRobinSelectionStrategy
```

### Use Case 3: Fallback Options
```csharp
// Multiple fallbacks - any works
yield return ProposalHelper.For("fallback.cache");
yield return ProposalHelper.For("fallback.default");

// ✅ GOOD: Ties are fine - doesn't matter which wins
```

## Using Built-in Selection Strategies

The framework includes selection strategies for intentional ties in `UtilityAi.Orchestration.SelectionStrategies`.

### RandomSelectionStrategy

Randomly picks among tied proposals - good for exploration:

```csharp
using UtilityAi.Orchestration.SelectionStrategies;

var randomStrategy = new RandomSelectionStrategy(
    tieThreshold: 0.001,  // Proposals within 0.001 utility considered tied
    seed: 42              // Optional: for reproducible tests
);

var orchestrator = new UtilityAiOrchestrator(
    selectionStrategy: randomStrategy,
    bus: bus
);
```

**When to use:**
- A/B testing different approaches
- Exploration in reinforcement learning scenarios
- Breaking ties when all options are truly equivalent

### RoundRobinSelectionStrategy

Rotates through tied proposals - good for load balancing:

```csharp
using UtilityAi.Orchestration.SelectionStrategies;

var roundRobinStrategy = new RoundRobinSelectionStrategy(
    tieThreshold: 0.001
);

var orchestrator = new UtilityAiOrchestrator(
    selectionStrategy: roundRobinStrategy,
    bus: bus
);
```

**When to use:**
- Load balancing across equivalent resources
- Ensuring fair execution of tied tasks
- Testing that all code paths work

## Detecting Ties (Debugging)

Add this to your custom sink to warn about ties:

```csharp
public void OnScored(Runtime rt, IReadOnlyList<(Proposal, double)> scored)
{
    if (scored.Count < 2) return;

    var topUtility = scored[0].Utility;  // Already sorted descending
    var tiedCount = scored.Count(s => Math.Abs(s.Utility - topUtility) < 0.001);

    if (tiedCount > 1)
    {
        var tiedIds = string.Join(", ", scored.Take(tiedCount).Select(s => s.Proposal.Id));
        Console.WriteLine($"⚠️  TIE: {tiedCount} proposals at {topUtility:F3}: {tiedIds}");
    }
}
```

## Quick Reference: Choosing Curves

| Goal | Curve | Code |
|------|-------|------|
| **Direct mapping** | Linear | `x => x` |
| **Reward high values** | Quadratic | `x => x * x` |
| **Only extreme values** | Cubic | `x => Math.Pow(x, 3)` |
| **Diminishing returns** | Square root | `x => Math.Sqrt(x)` |
| **Smooth threshold** | Logistic | `x => 1.0 / (1.0 + Math.Exp(-k*(x-m)))` |
| **Lower is better** | Inverted | `x => 1.0 - x` |
| **Sharp cutoff** | Step function | `x => x > 0.5 ? 1.0 : 0.0` |

## Summary Checklist

✅ **Always use response curves** - avoid binary `x => x` for everything
✅ **Test with different inputs** - ensure utilities actually differ
✅ **Use temperature** for fine-tuning sharpness
✅ **Intentional ties are OK** - use custom selection strategies
✅ **Add tie detection** to your sink for debugging
✅ **Document your curves** - explain why each curve was chosen

## See Also

- [Example/AgentAssistant/Modules/](./Modules/) - Full examples with curves
- [Example/AgentAssistant/SelectionStrategies/](./SelectionStrategies/) - Random & RoundRobin implementations
- [docs/BUILT_IN_COMPONENTS.md](../../docs/BUILT_IN_COMPONENTS.md) - Framework curve utilities
