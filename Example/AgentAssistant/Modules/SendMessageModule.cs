using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace Example.AgentAssistant.Modules;

/// <summary>
/// Capability to send a direct message response when we have high confidence and no research is needed.
/// Proposes different response strategies based on context.
/// Uses considerations instead of if-statements for cleaner, declarative logic.
/// </summary>
[Capability(Priority = 100, Domain = "response")]
[RequiresFact<UserMessage>]
[RequiresFact<ConversationContext>]
public sealed class SendMessageModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        // PROPOSAL 1: Direct confident response (high confidence, no research needed)
        yield return ProposalHelper.For("message.direct")
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "confidence",
                selector: ctx => ctx.Confidence,
                curve: x => x, // Higher confidence = higher score
                inputDomain: (0, 1)))
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "no_research_needed",
                selector: ctx => ctx.RequiresResearch ? 0.0 : 1.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new HasFact<ConversationContext>(
                name: "not_already_responded",
                selector: ctx => !ctx.HasRecentResponse))
            .WithAction(async ct =>
            {
                var userMsg = rt.Bus.GetOrDefault<UserMessage>();
                await Task.Delay(100, ct); // Simulate LLM call
                var response = $"Based on your question '{userMsg?.Text}', here's my response...";
                rt.Bus.Publish(new AssistantResponse(response, "direct-knowledge"));
                Console.WriteLine($"    💬 Sent direct response");
            })
            .Build();

        // PROPOSAL 2: Clarifying question (low confidence, ambiguous query)
        yield return ProposalHelper.For("message.clarify")
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "ambiguity",
                selector: ctx => 1.0 - ctx.Confidence, // Inverted: lower confidence = higher ambiguity
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new HasFact<ConversationContext>(
                name: "not_already_responded",
                selector: ctx => !ctx.HasRecentResponse))
            .WithAction(async ct =>
            {
                var userMsg = rt.Bus.GetOrDefault<UserMessage>();
                await Task.Delay(50, ct);
                var response = $"I want to make sure I understand correctly. Could you clarify what you mean by '{userMsg?.Text}'?";
                rt.Bus.Publish(new AssistantResponse(response, "clarification"));
                Console.WriteLine($"    ❓ Asked for clarification");
            })
            .Build();

        // PROPOSAL 3: Acknowledge and wait (research needed)
        yield return ProposalHelper.For("message.acknowledge")
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "needs_research",
                selector: ctx => ctx.RequiresResearch ? 1.0 : 0.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new HasFact<ConversationContext>(
                name: "not_already_responded",
                selector: ctx => !ctx.HasRecentResponse))
            .WithAction(async ct =>
            {
                await Task.Delay(50, ct);
                var response = "Let me look that up for you. One moment...";
                rt.Bus.Publish(new AssistantResponse(response, "acknowledgment"));
                Console.WriteLine($"    ⏳ Acknowledged, waiting for research");
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
/// </summary>
file sealed class HasFact<T>(string name, Func<T, bool>? selector = null) : IConsideration where T : notnull
{
    public string Name => name;

    public double Evaluate(Runtime rt)
    {
        var fact = rt.Bus.GetOrDefault<T>();
        if (fact == null) return 0.0;

        return selector == null || selector(fact) ? 1.0 : 0.0;
    }
}
