using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Orchestration;
using UtilityAi.Orchestration.Events;
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
        var context = rt.Bus.GetOrDefault<ConversationContext>();
        var hasResearch = rt.Bus.GetOrDefault<ResearchResults>() != null;
        var hasResponse = rt.Bus.GetOrDefault<AssistantResponse>() != null;

        // PROPOSAL 1: Direct confident response (after research or high confidence)
        yield return ProposalHelper.For("message.direct")
            .WithDescription("Send a direct, confident response to the user")
            .WithEligibility(new NotHasFactEligible<AssistantResponse>()) // Only if no response sent yet
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "confidence",
                selector: ctx => ctx.Confidence,
                curve: x => x * x, // Quadratic - rewards high confidence, penalizes medium
                inputDomain: (0, 1)))
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "ready_to_respond",
                selector: ctx => {
                    // Can respond if: (high confidence and no research needed) OR (research done)
                    if (!ctx.RequiresResearch) return 1.0;
                    return hasResearch ? 1.0 : 0.0;
                },
                curve: x => x,
                inputDomain: (0, 1)))
            .WithAction(async ct =>
            {
                var userMsg = rt.Bus.GetOrDefault<UserMessage>();
                var research = rt.Bus.GetOrDefault<ResearchResults>();
                await Task.Delay(100, ct); // Simulate LLM call

                var response = research != null
                    ? $"Based on research from {string.Join(", ", research.Sources)}: {research.Summary}"
                    : $"Based on your question '{userMsg?.Text}', here's my response...";

                rt.Bus.Publish(new AssistantResponse(response, "direct-knowledge"));

                // Stop orchestration after responding
                rt.Bus.Publish(new StopOrchestrationEvent(OrchestrationStopReason.GoalAchieved, "Response sent"));
                Console.WriteLine($"    💬 Sent direct response");
            })
            .Build();

        // PROPOSAL 2: Clarifying question (low confidence, no research available)
        yield return ProposalHelper.For("message.clarify")
            .WithDescription("Ask for clarification when query is ambiguous")
            .WithEligibility(new NotHasFactEligible<AssistantResponse>()) // Only if no response sent yet
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "ambiguity",
                selector: ctx => 1.0 - ctx.Confidence, // Inverted: lower confidence = higher ambiguity
                curve: x => Math.Pow(x, 3), // Cubic - strongly penalizes medium confidence, only triggers on very low
                inputDomain: (0, 1)))
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "no_research_option",
                selector: ctx => !ctx.RequiresResearch ? 1.0 : 0.1, // Much lower if research is an option
                curve: x => x,
                inputDomain: (0, 1)))
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
            .WithDescription("Acknowledge query and indicate research is in progress")
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "needs_research",
                selector: ctx => ctx.RequiresResearch && !hasResearch ? 1.0 : 0.0, // Only before research completes
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "moderate_confidence",
                selector: ctx => 1.0 - Math.Abs(ctx.Confidence - 0.5) * 2, // Peaks at 0.5 confidence
                curve: x => Math.Sqrt(x), // Square root - gentle preference
                inputDomain: (0, 1)))
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "not_already_acknowledged",
                selector: ctx => ctx.HasRecentResponse ? 0.0 : 1.0, // Only acknowledge once
                curve: x => x,
                inputDomain: (0, 1)))
            .WithAction(async ct =>
            {
                await Task.Delay(50, ct);
                Console.WriteLine($"    ⏳ Acknowledged, waiting for research");

                // Update context to indicate we've acknowledged
                var currentContext = rt.Bus.GetOrDefault<ConversationContext>();
                if (currentContext != null)
                {
                    rt.Bus.Publish(currentContext with { HasRecentResponse = true });
                }
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
