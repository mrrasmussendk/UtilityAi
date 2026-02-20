using Example.AgentAssistant;
using Example.AgentAssistant.Modules;
using UtilityAi.Consideration;
using UtilityAi.Orchestration;
using UtilityAi.Utils;

// =============================================================================
// AI AGENT ASSISTANT EXAMPLE - ATTRIBUTE-BASED CAPABILITY REGISTRATION
// =============================================================================
// This example demonstrates how to build an AI agent using UtilityAI with:
//
// - CAPABILITY MODULES: Represent what the agent CAN DO
//   * SendMessageModule: Respond to user messages
//   * DoResearchModule: Gather information from various sources
//   * FallbackResponseModule: Handle failures gracefully
//
// - CONSIDERATIONS: Declarative conditions that score proposals
//   * No if-statements in Propose() - all logic in considerations
//   * Evaluates EventBus facts to determine utility
//
// - ATTRIBUTE-BASED REGISTRATION: Automatic discovery and ordering
//   * [Capability] marks modules for auto-discovery
//   * [RequiresFact<T>] declares EventBus dependencies
//   * Topologically sorted by dependencies
//
// Key Patterns Demonstrated:
// - One module per capability (not per data item)
// - Multiple strategies per capability (direct, clarify, acknowledge)
// - Considerations validate EventBus facts (confidence, research needed, tools available)
// - Utility system decides between capabilities (send vs. research vs. fallback)
// =============================================================================

Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("🤖 AI Agent Assistant - Utility AI Orchestration");
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine();

// Scenario setup
var bus = new EventBus();

// Publish initial facts to EventBus
bus.Publish(new UserMessage("What's the weather in New York today?", "user-123"));
bus.Publish(new ConversationContext(
    MessageCount: 1,
    RequiresResearch: true,
    HasRecentResponse: false,
    Confidence: 0.7
));
bus.Publish(new AvailableTools(
    CanAccessWeb: true,
    CanAccessDatabase: false,
    RateLimitRemaining: 10
));


Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("📊 Initial EventBus State:");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine($"  UserMessage: \"{bus.GetOrDefault<UserMessage>()?.Text}\"");
Console.WriteLine($"  Confidence: {bus.GetOrDefault<ConversationContext>()?.Confidence:F2}");
Console.WriteLine($"  RequiresResearch: {bus.GetOrDefault<ConversationContext>()?.RequiresResearch}");
Console.WriteLine($"  CanAccessWeb: {bus.GetOrDefault<AvailableTools>()?.CanAccessWeb}");
Console.WriteLine($"  RateLimitRemaining: {bus.GetOrDefault<AvailableTools>()?.RateLimitRemaining}");
Console.WriteLine();

Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("✨ Discovered Capabilities (Attribute-Based):");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("  • SendMessageModule [Priority: 100]");
Console.WriteLine("    - Strategies: direct, clarify, acknowledge");
Console.WriteLine("    - Requires: UserMessage, ConversationContext");
Console.WriteLine();
Console.WriteLine("  • DoResearchModule [Priority: 80]");
Console.WriteLine("    - Strategies: web, database, embedded");
Console.WriteLine("    - Requires: UserMessage, ConversationContext, AvailableTools");
Console.WriteLine();
Console.WriteLine("  • FallbackResponseModule [Priority: 10]");
Console.WriteLine("    - Strategies: no_research, low_confidence, emergency");
Console.WriteLine("    - Requires: UserMessage");
Console.WriteLine();

// Create orchestrator with auto-discovered capabilities
var orchestrator = new UtilityAiOrchestrator(null, true, bus)
    .DiscoverCapabilities(typeof(SendMessageModule).Assembly);

Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("🔄 Running Orchestration Loop:");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

await orchestrator.RunAsync(maxTicks: 5, CancellationToken.None,
    sink: new DetailedConsoleSink());

Console.WriteLine();
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("✅ Orchestration Complete");
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine();
Console.WriteLine("💡 Key Takeaways:");
Console.WriteLine("  • Capabilities = What the agent CAN DO (send, research, fallback)");
Console.WriteLine("  • Proposals = Different STRATEGIES for each capability");
Console.WriteLine("  • Considerations = Declarative scoring based on EventBus facts");
Console.WriteLine("  • No if-statements in Propose() - all logic in considerations");
Console.WriteLine();
Console.WriteLine("📚 Documentation:");
Console.WriteLine("  • docs/PROPOSAL_PATTERNS.md - Best practices & anti-patterns");
Console.WriteLine("  • Example/AgentAssistant/README.md - Detailed explanation");
Console.WriteLine("  • docs/INTEGRATION.md - Connect to real LLM APIs");

/// <summary>
/// A custom sink to provide visibility into the decision-making process.
/// </summary>
public sealed class DetailedConsoleSink : IOrchestrationSink
{
    public void OnTickStart(Runtime rt)
        => Console.WriteLine($"\n[Tick {rt.Tick}] 🔍 Evaluating proposals...");

    public void OnScored(Runtime rt, IReadOnlyList<(Proposal Proposal, double Utility)> scored)
    {
        Console.WriteLine($"[Tick {rt.Tick}] ⚖️  Scored Proposals:");
        foreach (var s in scored.OrderByDescending(x => x.Utility))
        {
            Console.WriteLine($"  - {s.Proposal.Id,-35} | Utility: {s.Utility:F3}");
        }
    }

    public void OnChosen(Runtime rt, Proposal chosen, double utility)
        => Console.WriteLine($"[Tick {rt.Tick}] ✨ Winner: {chosen.Id} (utility={utility:F3})");

    public void OnActed(Runtime rt, Proposal chosen)
    {
        // Action output already logged in modules
    }

    public void OnStopped(Runtime rt, OrchestrationStopReason reason)
    {
        var emoji = reason == OrchestrationStopReason.MaxTicksReached ? "⏹️" : "🛑";
        Console.WriteLine($"\n{emoji} Stopped: {reason}");
    }
}
