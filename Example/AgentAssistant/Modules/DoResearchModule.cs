using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Utils;

namespace Example.AgentAssistant.Modules;

/// <summary>
/// Capability to perform research when needed.
/// Proposes different research strategies: web search, database query, or cached lookup.
/// Uses considerations instead of if-statements for cleaner, declarative logic.
/// </summary>
[Capability(Priority = 80, Domain = "research")]
[RequiresFact<UserMessage>]
[RequiresFact<ConversationContext>]
[RequiresFact<AvailableTools>]
public sealed class DoResearchModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        // Only propose research if we haven't done it yet
        var existingResearch = rt.Bus.GetOrDefault<ResearchResults>();
        if (existingResearch != null) yield break;

        // PROPOSAL 1: Web search (for current events, factual queries)
        yield return ProposalHelper.For("research.web")
            .WithDescription("Search the web for current, factual information")
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "needs_research",
                selector: ctx => ctx.RequiresResearch ? 1.0 : 0.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new SignalConsideration<AvailableTools>(
                name: "web_available",
                selector: tools => tools.CanAccessWeb ? 1.0 : 0.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new SignalConsideration<AvailableTools>(
                name: "rate_limit",
                selector: tools => tools.RateLimitRemaining,
                curve: x => 1.0 / (1.0 + Math.Exp(-0.5 * (x - 5))), // Logistic S-curve centered at 5
                inputDomain: (0, 10)))
            .WithAction(async ct =>
            {
                var userMsg = rt.Bus.GetOrDefault<UserMessage>();
                Console.WriteLine($"    🔍 Searching web for: {userMsg?.Text}");
                await Task.Delay(500, ct); // Simulate API call

                var results = new ResearchResults(
                    Query: userMsg?.Text ?? "",
                    Sources: new List<string> { "example.com", "source.org" },
                    Summary: $"Research summary for '{userMsg?.Text}' from web sources"
                );
                rt.Bus.Publish(results);
                Console.WriteLine($"    ✅ Web research complete");
            })
            .Build();

        // PROPOSAL 2: Database query (for internal knowledge, structured data)
        yield return ProposalHelper.For("research.database")
            .WithDescription("Query internal database for structured information")
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "needs_research",
                selector: ctx => ctx.RequiresResearch ? 1.0 : 0.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new SignalConsideration<AvailableTools>(
                name: "database_available",
                selector: tools => tools.CanAccessDatabase ? 1.0 : 0.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "preference_for_structured",
                selector: ctx => ctx.Confidence,
                curve: x => Math.Sqrt(x), // Square root - slight preference for higher confidence
                inputDomain: (0, 1)))
            .WithAction(async ct =>
            {
                var userMsg = rt.Bus.GetOrDefault<UserMessage>();
                Console.WriteLine($"    📊 Querying database for: {userMsg?.Text}");
                await Task.Delay(200, ct); // Simulate DB query

                var results = new ResearchResults(
                    Query: userMsg?.Text ?? "",
                    Sources: new List<string> { "internal-db" },
                    Summary: $"Database results for '{userMsg?.Text}'"
                );
                rt.Bus.Publish(results);
                Console.WriteLine($"    ✅ Database query complete");
            })
            .Build();

        // PROPOSAL 3: Cached/embedded knowledge (fallback, no external calls)
        yield return ProposalHelper.For("research.embedded")
            .WithDescription("Use embedded knowledge as fallback when external sources unavailable")
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "needs_research",
                selector: ctx => ctx.RequiresResearch ? 1.0 : 0.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new SignalConsideration<AvailableTools>(
                name: "external_unavailable",
                selector: tools => (!tools.CanAccessWeb && !tools.CanAccessDatabase) ? 1.0 : 0.3,
                curve: x => x * x, // Quadratic - strong preference when external is down
                inputDomain: (0, 1)))
            .WithAction(async ct =>
            {
                var userMsg = rt.Bus.GetOrDefault<UserMessage>();
                Console.WriteLine($"    📚 Using embedded knowledge for: {userMsg?.Text}");
                await Task.Delay(50, ct);

                var results = new ResearchResults(
                    Query: userMsg?.Text ?? "",
                    Sources: new List<string> { "embedded-knowledge" },
                    Summary: $"Based on training data, regarding '{userMsg?.Text}'..."
                );
                rt.Bus.Publish(results);
                Console.WriteLine($"    ✅ Embedded knowledge retrieved");
            })
            .Build();
    }
}

