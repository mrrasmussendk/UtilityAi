using Example.Maf.Agents;
using UtilityAi.Consideration;
using UtilityAi.Maf;
using UtilityAi.Orchestration;
using UtilityAi.Utils;

// =============================================================================
// MAF + UTILITY AI INTEGRATION EXAMPLE
// =============================================================================
// This example demonstrates how to integrate Microsoft Agent Framework (MAF)
// agents with UtilityAI orchestration. The utility scoring system decides
// WHICH agent to run, while MAF handles actual agent execution.
//
// Key Concepts:
// - MAF agents (ResearchAgent, WriterAgent) are wrapped as UtilityAI capability modules
// - Considerations score each agent based on EventBus facts
// - The orchestrator selects the highest-utility agent each tick
// - Agent results are published back to the EventBus for downstream use
//
// Architecture:
//   UtilityAI (Decision) ←→ MAF (Execution)
//   Sense → Propose → Score → Act (via MAF AIAgent.RunAsync)
// =============================================================================

Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("🤖 MAF + UtilityAI Integration Example");
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine();

// --- Step 1: Create MAF agents ---
var researchAgent = new ResearchAgent();
var writerAgent = new WriterAgent();

Console.WriteLine("📋 Registered MAF Agents:");
Console.WriteLine($"  • {researchAgent.Name}: {researchAgent.Description}");
Console.WriteLine($"  • {writerAgent.Name}: {writerAgent.Description}");
Console.WriteLine();

// --- Step 2: Set up EventBus with initial facts ---
var bus = new EventBus();

// Publish a fact indicating research is needed
bus.Publish(new ResearchNeeded(true));
bus.Publish(new ResearchComplete(false));

// --- Step 3: Build orchestrator with MAF agents ---
var orchestrator = new UtilityAiOrchestrator(bus: bus)
    // Register the agent catalog sensor
    .AddMafAgentSensor(
        new MafAgentRegistration("research", researchAgent),
        new MafAgentRegistration("writer", writerAgent))
    // Register Research Agent: high utility when research is needed
    .AddMafAgent(
        agent: researchAgent,
        agentName: "research",
        considerations: new IConsideration[]
        {
            new MafAgentAvailable("research"),
            new FactConsideration<ResearchNeeded>(
                name: "needs-research",
                selector: r => r.IsNeeded ? 1.0 : 0.0),
            new FactConsideration<ResearchComplete>(
                name: "not-yet-complete",
                selector: r => r.IsComplete ? 0.0 : 1.0)
        },
        messageProvider: rt =>
        {
            // Extract query from UserIntent
            if (rt.Intent.Slots?.TryGetValue("query", out var q) == true && q is string query)
                return query;
            return "general research";
        })
    // Register Writer Agent: high utility after research is complete
    .AddMafAgent(
        agent: writerAgent,
        agentName: "writer",
        considerations: new IConsideration[]
        {
            new MafAgentAvailable("writer"),
            new HasMafAgentResult("research"),
            new FactConsideration<ResearchComplete>(
                name: "research-complete",
                selector: r => r.IsComplete ? 1.0 : 0.0)
        });

// --- Step 4: Define the user intent ---
var intent = new UserIntent(
    Goal: new IntentGoal("answer-question"),
    Slots: new Dictionary<string, object?>
    {
        ["query"] = "What are the benefits of utility-based AI decision making?"
    }
);

Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("🔄 Running Orchestration Loop:");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

// --- Step 5: Add an action sensor that reacts to agent results ---
orchestrator.AddSensor(new AgentResultSensor());

await orchestrator.RunAsync(intent, maxTicks: 5, CancellationToken.None,
    sink: new MafConsoleSink());

Console.WriteLine();
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("✅ Orchestration Complete");
Console.WriteLine("═══════════════════════════════════════════════════════");

// Check final results
var finalResult = bus.GetOrDefault<MafAgentResult>();
if (finalResult != null)
{
    Console.WriteLine($"\n📄 Final Agent Result (from {finalResult.AgentName}):");
    Console.WriteLine($"   {finalResult.Text}");
}

Console.WriteLine();
Console.WriteLine("💡 Key Takeaways:");
Console.WriteLine("  • MAF agents are wrapped as UtilityAI capability modules");
Console.WriteLine("  • Utility scoring selects WHICH agent runs each tick");
Console.WriteLine("  • Agent results flow back through the EventBus");
Console.WriteLine("  • Multi-agent workflows emerge from scoring dynamics");
Console.WriteLine();
Console.WriteLine("📚 References:");
Console.WriteLine("  • Microsoft Agent Framework: https://learn.microsoft.com/en-us/agent-framework/");
Console.WriteLine("  • UtilityAI docs: docs/INTEGRATION.md");

// =============================================================================
// Supporting Types
// =============================================================================

/// <summary>
/// Fact indicating whether research is needed for the current query.
/// </summary>
public record ResearchNeeded(bool IsNeeded);

/// <summary>
/// Fact indicating whether research has been completed.
/// </summary>
public record ResearchComplete(bool IsComplete);

/// <summary>
/// A consideration that reads a typed fact from the EventBus and returns a utility score.
/// </summary>
file sealed class FactConsideration<T>(string name, Func<T, double> selector) : IConsideration
    where T : notnull
{
    public string Name => name;

    public double Evaluate(Runtime rt)
    {
        var fact = rt.Bus.GetOrDefault<T>();
        return fact != null ? Math.Clamp(selector(fact), 0.0, 1.0) : 0.0;
    }
}

/// <summary>
/// A sensor that reacts to MAF agent results and updates EventBus state.
/// When the research agent completes, it marks research as complete so
/// the writer agent becomes the highest-utility option.
/// </summary>
file sealed class AgentResultSensor : UtilityAi.Sensor.ISensor
{
    public Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        var result = rt.Bus.GetOrDefault<MafAgentResult>();
        if (result?.AgentName == "research")
        {
            // Research is done - update state so writer agent scores higher
            rt.Bus.Publish(new ResearchComplete(true));
            rt.Bus.Publish(new ResearchNeeded(false));
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// A custom sink to display MAF agent orchestration decisions.
/// </summary>
file sealed class MafConsoleSink : IOrchestrationSink
{
    public void OnTickStart(Runtime rt)
        => Console.WriteLine($"\n[Tick {rt.Tick}] 🔍 Evaluating MAF agents...");

    public void OnScored(Runtime rt, IReadOnlyList<(Proposal Proposal, double Utility)> scored)
    {
        Console.WriteLine($"[Tick {rt.Tick}] ⚖️  Agent Scores:");
        foreach (var s in scored.OrderByDescending(x => x.Utility))
        {
            Console.WriteLine($"  - {s.Proposal.Id,-30} | Utility: {s.Utility:F3}");
        }
    }

    public void OnChosen(Runtime rt, Proposal chosen, double utility)
        => Console.WriteLine($"[Tick {rt.Tick}] ✨ Selected: {chosen.Id} (utility={utility:F3})");

    public void OnActed(Runtime rt, Proposal chosen)
    {
        var result = rt.Bus.GetOrDefault<MafAgentResult>();
        if (result != null)
            Console.WriteLine($"[Tick {rt.Tick}] 📨 Agent '{result.AgentName}' responded: {result.Text[..Math.Min(80, result.Text.Length)]}...");
    }

    public void OnStopped(Runtime rt, OrchestrationStopReason reason)
    {
        var emoji = reason == OrchestrationStopReason.MaxTicksReached ? "⏹️" : "🛑";
        Console.WriteLine($"\n{emoji} Stopped: {reason}");
    }
}
