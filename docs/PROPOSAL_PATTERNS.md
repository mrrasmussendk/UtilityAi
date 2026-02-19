# Proposal Patterns and Anti-Patterns

This guide explains common patterns and mistakes when creating proposals in UtilityAI.

## Core Principle

**A capability module represents ONE ability or domain (e.g., "send message", "do research", "fallback").**

Each module yields MULTIPLE proposals representing **different strategies or approaches** for that capability. The orchestrator scores all proposals from all modules and selects the highest-utility one per tick.

**Example domains:**
- SendMessageModule: direct response, clarifying question, acknowledgment
- DoResearchModule: web search, database query, cached knowledge
- FallbackResponseModule: graceful decline, low confidence response, emergency fallback

---

## ❌ Anti-Pattern: Looping Through Items

### The Problem

```csharp
public IEnumerable<Proposal> Propose(Runtime rt)
{
    var tasks = GetTasks();

    // ❌ WRONG: Proposing all tasks as separate proposals
    foreach (var task in tasks)
    {
        yield return new Proposal($"execute.{task.Id}", ...);
    }
    // This yields 10+ proposals that represent the same ACTION TYPE
    // They only differ in which DATA INSTANCE they operate on
}
```

### Why It's Wrong

1. **Conceptual confusion** - "Execute task" is ONE capability, not N capabilities
2. **Wasted CPU** - Scores 100 similar proposals, executes only 1
3. **Poor utility modeling** - Item selection logic should happen BEFORE proposing, not during scoring
4. **Breaks modularity** - The "which item?" decision doesn't belong in the utility system

### What Happens

```
Tick 1: Score 100 "execute task X" proposals → Execute task #47
Tick 2: Score 99 "execute task X" proposals → Execute task #23
Tick 3: Score 98 "execute task X" proposals → Execute task #89
```

The utility system is being misused as an item selection algorithm.

---

## ✅ Correct Pattern 1: Select Best Instance, Then Propose

When dealing with multiple instances of the same action type (e.g., many tasks), **select the best instance first, then propose it**:

```csharp
public IEnumerable<Proposal> Propose(Runtime rt)
{
    var tasks = GetTasks();

    // ✅ CORRECT: Select the best task FIRST, then propose it
    var bestTask = tasks
        .OrderByDescending(t => t.Priority)
        .ThenBy(t => t.SubmittedAt)
        .FirstOrDefault();

    if (bestTask != null)
    {
        yield return ProposalHelper.For($"execute.{bestTask.Id}")
            .WithValue("priority", bestTask.Priority)
            .WithValue("urgency", bestTask.Urgency)
            .WithAction(async ct => await ExecuteTask(bestTask, ct));
    }
}
```

### When to Use This

- When you have many instances of the same action type (tasks, items, targets)
- Sequential/resource-limited operations
- The selection logic is domain-specific (not utility-based)

**Note:** This pattern still lets you yield OTHER types of proposals. A typical module yields 2-5 proposals of different types.

---

## ✅ Correct Pattern 2: Different Strategies for Same Capability (MOST COMMON)

**This is the typical pattern.** A capability module yields multiple proposals representing different strategies or approaches for the SAME capability domain:

```csharp
/// <summary>
/// Capability: Respond to user messages
/// Strategies: Direct answer, clarifying question, acknowledgment
/// </summary>
public class SendMessageModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var userMsg = rt.Bus.GetOrDefault<UserMessage>();
        var context = rt.Bus.GetOrDefault<ConversationContext>();

        if (userMsg == null || context == null) yield break;

        // PROPOSAL 1: Direct confident response
        if (context.Confidence > 0.8)
        {
            yield return ProposalHelper.For("message.direct")
                .WithValue("confidence", context.Confidence)
                .WithValue("urgency", 0.9)
                .WithAction(async ct => await SendDirectResponse(userMsg, ct));
        }

        // PROPOSAL 2: Clarifying question
        if (context.Confidence < 0.5)
        {
            yield return ProposalHelper.For("message.clarify")
                .WithValue("ambiguity", 1.0 - context.Confidence)
                .WithValue("conversational", 0.8)
                .WithAction(async ct => await AskForClarification(userMsg, ct));
        }

        // PROPOSAL 3: Acknowledge and wait
        if (context.RequiresResearch)
        {
            yield return ProposalHelper.For("message.acknowledge")
                .WithValue("needs_research", 1.0)
                .WithValue("politeness", 0.9)
                .WithAction(async ct => await SendAcknowledgment(ct));
        }
    }
}
```

### When to Use This

- **This is the default pattern** - one module per capability domain
- Each proposal represents a different STRATEGY for that capability
- Considerations model which strategy is best RIGHT NOW based on EventBus facts
- Competes with proposals from OTHER capability modules

**Key insight:** The utility system decides between CAPABILITIES (send message vs. do research vs. fallback), not between DATA INSTANCES (task #1 vs. task #2).

---

## ✅ Correct Pattern 3: Batch Execution

If you genuinely want to execute multiple tasks per tick, **do it in ONE action**:

```csharp
public IEnumerable<Proposal> Propose(Runtime rt)
{
    var tasks = GetReadyTasks();

    if (tasks.Any())
    {
        // ✅ CORRECT: ONE proposal that executes multiple tasks
        yield return ProposalHelper.For("execute.batch")
            .WithValue("batch_size", Math.Min(tasks.Count / 10.0, 1.0))
            .WithValue("resources_available", GetResourceAvailability())
            .WithAction(async ct =>
            {
                // Execute up to N tasks in parallel
                var batch = tasks.Take(3);
                await Task.WhenAll(batch.Select(t => ExecuteTask(t, ct)));
            });
    }
}
```

### When to Use This

- Parallel execution systems
- Batch processing
- When tasks don't compete for resources

---

## 🎯 Rule of Thumb

**One module = One capability domain. Multiple proposals = Different strategies for that capability.**

**Ask yourself:** *"What is my module's PURPOSE?"*

- **Good answer:** ✅
  - "This module handles SENDING MESSAGES" (strategies: direct, clarify, acknowledge)
  - "This module handles RESEARCH" (strategies: web, database, cached)
  - "This module handles FALLBACK RESPONSES" (strategies: graceful decline, low confidence, emergency)

- **Bad answer:** ❌
  - "This module handles TASK #1"
  - "This module processes USER #5"
  - "This module validates ITEM #47"

**Then ask:** *"What are the different WAYS I can fulfill this capability?"*

- **Good answers:** ✅
  - "I can send a direct response, OR ask for clarification, OR acknowledge"
  - "I can search the web, OR query the database, OR use cached knowledge"
  - "I can execute the top-priority task, OR batch process multiple, OR wait"

- **Bad answers:** ❌
  - "I can execute task #1, OR task #2, OR task #3..." (that's data selection, not strategy)
  - "I can validate user #1, OR user #2, OR user #3..." (same problem)

**Example AI agent structure:**
```csharp
// THREE capability modules for an AI agent
public class SendMessageModule : ICapabilityModule { }     // Strategies: direct, clarify, acknowledge
public class DoResearchModule : ICapabilityModule { }      // Strategies: web, database, cached
public class FallbackResponseModule : ICapabilityModule { } // Strategies: decline, clarify, emergency

// The utility system decides: Should I send a message? Do research? Or fallback?
// NOT: Should I process item #1, #2, or #3?
```

---

## 🔧 Using ProposalHelper

The `ProposalHelper` reduces boilerplate:

### Before (Verbose)

```csharp
yield return new Proposal(
    id: "my.action",
    cons: new IConsideration[]
    {
        new FixedValue("priority", 0.8),
        new FixedValue("urgency", 0.6)
    },
    act: async ct => await DoSomething(ct)
);
```

### After (Concise)

```csharp
yield return ProposalHelper.For("my.action")
    .WithValue("priority", 0.8)
    .WithValue("urgency", 0.6)
    .WithAction(async ct => await DoSomething(ct));
```

---

## 📊 Performance Impact

| Pattern | Proposals/Tick | Scoring Cost | Execution |
|---------|---------------|--------------|-----------|
| ❌ Loop through 100 tasks | 100 | High | 1 task |
| ✅ Select best first | 1 | Minimal | 1 task |
| ✅ Different actions | 3-5 | Low | 1 action |
| ✅ Batch execution | 1 | Minimal | N tasks |

---

## 🎓 Summary

1. **One module = One capability** - e.g., SendMessage, DoResearch, Fallback
2. **Multiple proposals = Different strategies** - e.g., direct response vs. clarifying question vs. acknowledgment
3. **Use EventBus facts for validation** - Check RequiresFact conditions before proposing
4. **Let utility decide between capabilities** - Not between data instances
5. **Use ProposalHelper** - Reduces boilerplate

Remember: **Capability modules represent WHAT THE AGENT CAN DO (abilities), not WHAT DATA TO PROCESS (items).**

## 📖 Complete Example

See `examples/Example/AgentAssistant/` for a complete AI agent example with:
- **SendMessageModule** - Responds to user (strategies: direct, clarify, acknowledge)
- **DoResearchModule** - Gathers information (strategies: web, database, embedded)
- **FallbackResponseModule** - Handles failures (strategies: decline, low-confidence, emergency)

Each module checks EventBus facts like `UserMessage`, `ConversationContext`, and `AvailableTools` to decide which strategies to propose.
