# AI Agent Assistant Example

This example demonstrates the proper use of UtilityAI for building an AI agent with multiple capabilities.

## Architecture

### Capability Modules (What the agent CAN DO)

1. **SendMessageModule** - Capability to respond to user messages
   - Strategy 1: Direct confident response (high confidence, simple query)
   - Strategy 2: Clarifying question (low confidence, ambiguous query)
   - Strategy 3: Acknowledge and wait (research needed)

2. **DoResearchModule** - Capability to gather information
   - Strategy 1: Web search (current events, factual queries)
   - Strategy 2: Database query (internal knowledge, structured data)
   - Strategy 3: Embedded knowledge (fallback, no external calls)

3. **FallbackResponseModule** - Capability to handle failures gracefully
   - Strategy 1: Graceful decline (research failed or unavailable)
   - Strategy 2: Low confidence response (ambiguous input)
   - Strategy 3: Emergency fallback (last resort)

## How It Works

Each tick, the orchestrator:

1. **Sensors** publish facts to EventBus:
   - `UserMessage` - The user's input
   - `ConversationContext` - Metadata (confidence, research needed, etc.)
   - `AvailableTools` - What capabilities are available

2. **Capability Modules** propose strategies based on EventBus facts:
   - SendMessageModule checks `UserMessage` and `ConversationContext` to decide which response strategy to propose
   - DoResearchModule checks `ConversationContext.RequiresResearch` and `AvailableTools` to decide which research strategy to propose
   - FallbackResponseModule always proposes low-utility fallbacks in case others fail

3. **Utility Scoring** evaluates all proposals:
   - Each proposal has considerations that model its value RIGHT NOW
   - "Direct response" has high utility when confidence is high
   - "Web search" has high utility when research is needed AND web is available
   - "Emergency fallback" has very low utility - only wins if nothing else can

4. **Execution** runs the highest-scoring proposal:
   - The agent might send a direct response, OR start research, OR ask for clarification
   - **Never** loops through items - always selects ONE capability to execute per tick

## Key Patterns

### ✅ Correct: Capability-based modules
```csharp
// Module represents ONE capability
[Capability(Priority = 100, Domain = "response")]
[RequiresFact<UserMessage>]
[RequiresFact<ConversationContext>]
public sealed class SendMessageModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        // Check facts from EventBus
        var userMsg = rt.Bus.GetOrDefault<UserMessage>();
        var context = rt.Bus.GetOrDefault<ConversationContext>();

        if (userMsg == null || context == null) yield break;

        // Propose different STRATEGIES for this capability
        if (context.Confidence > 0.8)
            yield return ProposalHelper.For("message.direct")... // Strategy 1

        if (context.Confidence < 0.5)
            yield return ProposalHelper.For("message.clarify")... // Strategy 2

        if (context.RequiresResearch)
            yield return ProposalHelper.For("message.acknowledge")... // Strategy 3
    }
}
```

### ❌ Wrong: Item-based modules
```csharp
// Don't do this - module represents one ITEM, not one CAPABILITY
public class ProcessTask47Module : ICapabilityModule { ... }

// Don't do this - looping through items as proposals
public IEnumerable<Proposal> Propose(Runtime rt)
{
    foreach (var user in users) // ❌ Wrong!
        yield return new Proposal($"message.{user.Id}", ...);
}
```

## Considerations

Each proposal uses EventBus facts to model its utility:

```csharp
// High confidence → Direct response has high utility
.WithValue("confidence", context.Confidence)

// Research needed → Web search has high utility
.WithValue("needs_research", context.RequiresResearch ? 1.0 : 0.0)

// Rate limit remaining → Affects web search utility
.WithValue("rate_limit", Math.Min(1.0, tools.RateLimitRemaining / 10.0))
```

## Running the Example

```csharp
var bus = new EventBus();

// Publish initial facts
bus.Publish(new UserMessage("What's the weather today?", "user-123"));
bus.Publish(new ConversationContext(
    MessageCount: 1,
    RequiresResearch: true,
    HasRecentResponse: false,
    Confidence: 0.9
));
bus.Publish(new AvailableTools(
    CanAccessWeb: true,
    CanAccessDatabase: false,
    RateLimitRemaining: 10
));

// Create orchestrator with capability modules
var orchestrator = new UtilityAiOrchestrator(bus)
    .DiscoverCapabilities(typeof(SendMessageModule).Assembly);

// Run decision loop
await orchestrator.RunAsync(maxTicks: 5, CancellationToken.None);
```

Expected output:
```
Tick 1: DoResearchModule.web_search wins → Searches web for weather
Tick 2: SendMessageModule.direct wins → Sends response with weather data
```

## Response Curves & Tie-Breaking

This example demonstrates **proper use of response curves** to avoid unintentional utility ties:

- **Quadratic curves** (`x => x * x`) - Reward high confidence, penalize medium
- **Cubic curves** (`x => Math.Pow(x, 3)`) - Only trigger on extreme values
- **Logistic S-curves** - Smooth thresholds for rate limiting
- **Square root** (`x => Math.Sqrt(x)`) - Diminishing returns

**Without curves**, proposals tie at 1.0 utility and behavior becomes unpredictable!

### Built-in Selection Strategies

The framework includes selection strategies for intentional ties (available in `UtilityAi.Orchestration.SelectionStrategies`):
- **`RandomSelectionStrategy`** - Randomly pick among tied proposals (A/B testing, exploration)
- **`RoundRobinSelectionStrategy`** - Rotate through ties (load balancing)

See [CURVES_AND_TIES.md](./CURVES_AND_TIES.md) for comprehensive guide!

## See Also

- [CURVES_AND_TIES.md](./CURVES_AND_TIES.md) - **Response curves and tie-breaking guide**
- [PROPOSAL_PATTERNS.md](../../docs/PROPOSAL_PATTERNS.md) - Detailed guide on correct patterns
- [INTEGRATION.md](../../docs/INTEGRATION.md) - How to integrate with real LLM APIs
