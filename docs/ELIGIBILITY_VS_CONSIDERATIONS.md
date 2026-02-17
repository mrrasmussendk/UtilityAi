# Eligibility vs Considerations

**TL;DR:** Use **Eligibility** for hard requirements (yes/no), use **Considerations** for preferences (scored 0-1).

## The Problem

One of the most common mistakes when building Utility AI proposals is **using considerations for hard requirements**. This causes subtle bugs because of how the geometric mean works.

### The Geometric Mean Trap

Proposals are scored using the **geometric mean** of all consideration scores:

```
utility = (c1 × c2 × c3 × ... × cN)^(1/N)
```

**If ANY consideration returns 0.0, the entire utility becomes 0.0**, regardless of other scores.

### Bad Example ❌

```csharp
yield return ProposalHelper.For("send.message")
    .WithConsideration(new HasFact<ResearchResults>()) // Returns 0.0 if missing
    .WithConsideration(new SignalConsideration<Context>(
        name: "confidence",
        selector: ctx => ctx.Confidence,
        curve: x => x * x,
        inputDomain: (0, 1))) // Returns 0.49 when confidence = 0.7
    .WithAction(async ct => { /* send message */ })
    .Build();
```

**Problem:** When `ResearchResults` doesn't exist:
- `HasFact<ResearchResults>()` returns **0.0**
- `confidence` consideration returns **0.49**
- Geometric mean = `(0.0 × 0.49)^(1/2) = 0.0`
- **Proposal is eliminated!**

This is confusing because you expected the proposal to score 0.49 when research is missing, not 0.0.

## The Solution: Eligibility

**Eligibility** is a hard yes/no filter that runs BEFORE considerations are evaluated.

### Good Example ✅

```csharp
yield return ProposalHelper.For("send.message")
    .WithEligibility(new HasFactEligible<ResearchResults>()) // Hard requirement
    .WithConsideration(new SignalConsideration<Context>(
        name: "confidence",
        selector: ctx => ctx.Confidence,
        curve: x => x * x,
        inputDomain: (0, 1)))
    .WithAction(async ct => { /* send message */ })
    .Build();
```

**Now:**
- If `ResearchResults` doesn't exist → proposal is **ineligible**, never evaluated
- If `ResearchResults` exists → consideration scores normally (0.49 when confidence = 0.7)

## When to Use Each

### Use Eligibility for:

✅ **Hard requirements** - "This proposal cannot run without X"
✅ **Binary conditions** - "Only if authenticated"
✅ **One-time actions** - "Only if not already done"
✅ **Filtering proposals** - "Never run this twice"

**Built-in eligibility checks:**
- `HasFactEligible<T>` - Fact must exist
- `NotHasFactEligible<T>` - Fact must NOT exist
- `NoRepeatEligible(id)` - Only run once

### Use Considerations for:

✅ **Preferences** - "I prefer high confidence"
✅ **Continuous values** - "Battery level affects desirability"
✅ **Trade-offs** - "Balance speed vs quality"
✅ **Scoring** - "How urgent is this?"

**Built-in considerations:**
- `SignalConsideration<T>` - Score based on fact property with response curve
- `HasFact<T>` - Returns 1.0 if exists, 0.0 otherwise (⚠️ use with caution!)
- `NotHasFact<T>` - Returns 1.0 if doesn't exist, 0.0 otherwise (⚠️ use with caution!)
- `HasFactWhere<T>` - Returns 1.0 if predicate passes, 0.0 otherwise (⚠️ use with caution!)
- `FixedValueConsideration` - Always returns same value

## Common Patterns

### Pattern 1: "Must have X, prefer high Y"

```csharp
.WithEligibility(new HasFactEligible<ResearchResults>()) // Must have
.WithConsideration(new SignalConsideration<Context>(
    name: "confidence",
    selector: ctx => ctx.Confidence,
    curve: x => x * x, // Prefer high
    inputDomain: (0, 1)))
```

### Pattern 2: "Only run once"

```csharp
.WithEligibility(new NotHasFactEligible<AssistantResponse>()) // Only if no response sent
.WithConsideration(new SignalConsideration<Context>(
    name: "urgency",
    selector: ctx => ctx.Urgency,
    curve: x => x,
    inputDomain: (0, 1)))
```

### Pattern 3: "Fallback (last resort)"

```csharp
.WithConsideration(new FixedValueConsideration("last_resort", 0.001))
```

No eligibility needed - we want this to always be available, just with very low score.

## Warning: HasFact as Consideration

The `HasFact<T>` consideration is provided but comes with a warning:

```csharp
// ⚠️ DANGEROUS - geometric mean trap!
.WithConsideration(new HasFact<ResearchResults>())
```

**Why dangerous?**
- If fact doesn't exist → returns 0.0 → entire utility = 0.0
- Often not what you want (you wanted filtering, not scoring)

**When to use it:**
- You actually want to SCORE based on existence (rare)
- You understand the geometric mean implications

**Better alternative:**
```csharp
// ✅ CLEAR INTENT
.WithEligibility(new HasFactEligible<ResearchResults>())
```

## Why This Matters

### Before (Confusing)

```csharp
.WithConsideration(new HasFact<T>()) // What does this do?
.WithConsideration(new NotHasFact<T>()) // Is this filtering or scoring?
```

- Unclear intent
- Geometric mean surprise
- Debugging nightmare

### After (Clear)

```csharp
.WithEligibility(new HasFactEligible<T>()) // Clear: Hard requirement
.WithConsideration(new SignalConsideration<T>(...)) // Clear: Scoring
```

- Obvious intent
- Predictable behavior
- Easy to debug

## Summary

| Aspect | Eligibility | Consideration |
|--------|-------------|---------------|
| **Type** | Binary (yes/no) | Scored (0-1) |
| **Purpose** | Filter proposals | Score proposals |
| **When evaluated** | Before scoring | During scoring |
| **Effect if fails** | Proposal ineligible | Reduces utility score |
| **Geometric mean** | Not involved | Multiplied with others |
| **Use for** | Hard requirements | Preferences & trade-offs |

**Golden Rule:** If you find yourself using `HasFact<T>` or `NotHasFact<T>` as considerations, you probably want eligibility instead.
