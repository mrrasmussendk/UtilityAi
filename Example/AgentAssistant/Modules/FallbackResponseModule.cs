using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace Example.AgentAssistant.Modules;

/// <summary>
/// Fallback capability when other modules cannot handle the request.
/// Always provides some response to avoid leaving the user hanging.
/// Proposes different fallback strategies based on failure mode.
/// Uses considerations instead of if-statements for cleaner, declarative logic.
/// </summary>
[Capability(Priority = 10, Domain = "fallback")] // Low priority - only if others fail
[RequiresFact<UserMessage>]
public sealed class FallbackResponseModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        // PROPOSAL 1: Graceful decline (research needed but unavailable)
        yield return ProposalHelper.For("fallback.no_research")
            .WithConsideration(new HasFact<AssistantResponse>(
                name: "no_response_yet",
                selector: _ => false)) // Inverted - only propose if no response exists
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "research_needed",
                selector: ctx => ctx.RequiresResearch ? 1.0 : 0.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new HasFact<ResearchResults>(
                name: "research_failed",
                selector: _ => false)) // Inverted - only if no research results
            .WithAction(async ct =>
            {
                await Task.Delay(50, ct);
                var response = "I apologize, but I'm unable to research that topic right now. " +
                               "Could you try rephrasing your question, or is there something else I can help with?";
                rt.Bus.Publish(new AssistantResponse(response, "fallback-no-research"));
                Console.WriteLine($"    🤷 Fallback: Research unavailable");
            })
            .Build();

        // PROPOSAL 2: Generic helpful response (low confidence)
        yield return ProposalHelper.For("fallback.low_confidence")
            .WithConsideration(new HasFact<AssistantResponse>(
                name: "no_response_yet",
                selector: _ => false)) // Inverted
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "very_low_confidence",
                selector: ctx => ctx.Confidence < 0.3 ? 1.0 : 0.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithAction(async ct =>
            {
                await Task.Delay(50, ct);
                var response = "I'm not entirely sure I understand your question. " +
                               "Could you provide more details or context?";
                rt.Bus.Publish(new AssistantResponse(response, "fallback-clarification"));
                Console.WriteLine($"    🤷 Fallback: Low confidence");
            })
            .Build();

        // PROPOSAL 3: Emergency fallback (last resort, always proposes with very low score)
        yield return ProposalHelper.For("fallback.emergency")
            .WithConsideration(new HasFact<AssistantResponse>(
                name: "no_response_yet",
                selector: _ => false)) // Inverted
            .WithConsideration(new FixedValueConsideration(
                name: "last_resort",
                value: 0.1)) // Very low - only wins if nothing else can
            .WithAction(async ct =>
            {
                await Task.Delay(50, ct);
                var response = "I'm experiencing some difficulty processing your request. " +
                               "Please try again or contact support if the issue persists.";
                rt.Bus.Publish(new AssistantResponse(response, "fallback-emergency"));
                Console.WriteLine($"    🚨 Emergency fallback triggered");
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

/// <summary>
/// Consideration that returns a fixed value.
/// </summary>
file sealed class FixedValueConsideration(string name, double value) : IConsideration
{
    public string Name => name;
    public double Evaluate(Runtime rt) => Math.Clamp(value, 0.0, 1.0);
}
