using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Orchestration;
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
        var hasResearch = rt.Bus.GetOrDefault<ResearchResults>() != null;
        var context = rt.Bus.GetOrDefault<ConversationContext>();

        // PROPOSAL 1: Graceful decline (research needed but unavailable) - one time only
        yield return ProposalHelper.For("fallback.no_research")
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "research_needed_but_missing",
                selector: ctx => (ctx.RequiresResearch && !hasResearch && !ctx.HasRecentResponse) ? 1.0 : 0.01,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithAction(async ct =>
            {
                await Task.Delay(50, ct);
                Console.WriteLine($"    🤷 Fallback: Research unavailable");

                // Mark that we've responded so this doesn't win again
                if (context != null)
                {
                    rt.Bus.Publish(context with { HasRecentResponse = true });
                }
            })
            .Build();

        // PROPOSAL 2: Generic helpful response (low confidence)
        yield return ProposalHelper.For("fallback.low_confidence")
            .WithConsideration(new SignalConsideration<ConversationContext>(
                name: "very_low_confidence",
                selector: ctx => ctx.Confidence < 0.3 ? 1.0 : 0.001,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithAction(async ct =>
            {
                await Task.Delay(50, ct);
                Console.WriteLine($"    🤷 Fallback: Low confidence");
                // Note: Don't publish AssistantResponse - let message.clarify handle actual response
            })
            .Build();

        // PROPOSAL 3: Emergency fallback (last resort, always proposes with very low score)
        yield return ProposalHelper.For("fallback.emergency")
            .WithConsideration(new FixedValueConsideration(
                name: "last_resort",
                value: 0.001)) // Very low - only wins if nothing else can
            .WithAction(async ct =>
            {
                await Task.Delay(50, ct);
                Console.WriteLine($"    🚨 Emergency fallback triggered");
                // Note: Don't publish AssistantResponse - this indicates a problem
            })
            .Build();
    }

    public IEnumerable<ProposalDefinition> GetProposalDefinitions()
    {
        yield return new ProposalDefinition(
            ProposalId: "fallback.no_research",
            Description: null,
            Prior: 1.0,
            Temperature: 1.0,
            ConsiderationNames: new[] { "research_needed_but_missing" },
            EligibilityNames: Array.Empty<string>(),
            NoRepeat: false,
            JsonOutput: null
        );

        yield return new ProposalDefinition(
            ProposalId: "fallback.low_confidence",
            Description: null,
            Prior: 1.0,
            Temperature: 1.0,
            ConsiderationNames: new[] { "very_low_confidence" },
            EligibilityNames: Array.Empty<string>(),
            NoRepeat: false,
            JsonOutput: null
        );

        yield return new ProposalDefinition(
            ProposalId: "fallback.emergency",
            Description: null,
            Prior: 1.0,
            Temperature: 1.0,
            ConsiderationNames: new[] { "last_resort" },
            EligibilityNames: Array.Empty<string>(),
            NoRepeat: false,
            JsonOutput: null
        );
    }
}
