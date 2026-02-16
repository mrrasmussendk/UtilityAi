using UtilityAi.Capabilities;
using UtilityAi.Consideration;
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
        // PROPOSAL 1: Web search (for current events, factual queries)
        yield return ProposalHelper.For("research.web")
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "needs_research",
                selector: ctx => ctx.RequiresResearch ? 1.0 : 0.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new HasFact<ResearchResults>(
                name: "no_existing_research",
                selector: _ => false)) // Inverted - returns 1.0 if fact doesn't exist
            .WithConsideration(new SignalConsideration<AvailableTools>(
                name: "web_available",
                selector: tools => tools.CanAccessWeb ? 1.0 : 0.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new SignalConsideration<AvailableTools>(
                name: "rate_limit",
                selector: tools => tools.RateLimitRemaining,
                curve: x => x,
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
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "needs_research",
                selector: ctx => ctx.RequiresResearch ? 1.0 : 0.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new HasFact<ResearchResults>(
                name: "no_existing_research",
                selector: _ => false)) // Inverted
            .WithConsideration(new SignalConsideration<AvailableTools>(
                name: "database_available",
                selector: tools => tools.CanAccessDatabase ? 1.0 : 0.0,
                curve: x => x,
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
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "needs_research",
                selector: ctx => ctx.RequiresResearch ? 1.0 : 0.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new HasFact<ResearchResults>(
                name: "no_existing_research",
                selector: _ => false)) // Inverted
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

/// <summary>
/// Consideration that reads a signal from the EventBus and applies a curve.
/// </summary>
file sealed class SignalConsideration<T>(
    string name,
    Func<T, double> selector,
    Func<double, double> curve,
    (double min, double max) inputDomain) : IConsideration where T : notnull
{
    public string Name => name;

    public double Evaluate(Runtime rt)
    {
        var fact = rt.Bus.GetOrDefault<T>();
        if (fact == null) return 0.0;

        var rawValue = selector(fact);
        var normalized = (rawValue - inputDomain.min) / (inputDomain.max - inputDomain.min);
        var clamped = Math.Clamp(normalized, 0.0, 1.0);
        return curve(clamped);
    }
}

/// <summary>
/// Consideration that checks if a fact exists and optionally validates it.
/// Returns 1.0 if fact exists and selector returns true (or no selector).
/// Returns 1.0 if fact doesn't exist and selector returns false.
/// </summary>
file sealed class HasFact<T>(string name, Func<T, bool>? selector = null) : IConsideration where T : notnull
{
    public string Name => name;

    public double Evaluate(Runtime rt)
    {
        var fact = rt.Bus.GetOrDefault<T>();

        if (fact == null)
            return selector != null && !selector(default(T)!) ? 1.0 : 0.0;

        return selector == null || selector(fact) ? 1.0 : 0.0;
    }
}
