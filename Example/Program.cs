using Example.TaskManagement.Sensors;
using Example.TaskManagement.Modules;
using UtilityAi.Consideration;
using UtilityAi.Orchestration;
using UtilityAi.Utils;

// =============================================================================
// TASK ORCHESTRATION EXAMPLE
// =============================================================================
// This example demonstrates a task management system where multiple modules
// coordinate to process incoming tasks, validate them, prioritize them,
// and execute them based on available resources and dependencies.
//
// Key Concepts Demonstrated:
// 1. Multiple modules competing for attention (validation, prioritization, execution)
// 2. Resource constraints (CPU, memory budgets)
// 3. Task dependencies (some tasks must complete before others can start)
// 4. Dynamic utility scoring (tasks become more urgent over time)
// 5. Adaptive behavior (system responds to changing resource availability)
// =============================================================================

// 1. Initialize the EventBus (the "Blackboard")
var bus = new EventBus();

// 2. Define the User's Intent
// Simulating a batch of tasks being submitted to the system
var intent = new UserIntent(
    Goal: new IntentGoal("process-task-batch"),
    Slots: new Dictionary<string, object?>
    {
        ["tasks"] = new[]
        {
            "analyze-data-set-A",
            "generate-report",
            "backup-database",
            "optimize-indexes",
            "send-notifications"
        },
        ["priority_mode"] = "balanced", // Can be: urgent, balanced, efficiency
        ["max_parallel"] = 3
    }
);

// 3. Configure the Orchestrator
// Wire up Sensors (observe state) and Capability Modules (propose actions)
var orch = new UtilityAiOrchestrator(null, true, bus)
    // Sensors: Read environment and update the blackboard
    .AddSensor(new IntentSensor())
    .AddSensor(new ResourceMonitorSensor())
    .AddSensor(new TaskQueueSensor())
    // Modules: Propose actions based on current state
    .AddModule(new ValidationModule())
    .AddModule(new PrioritizationModule())
    .AddModule(new ExecutionModule());

// 4. Run the Orchestration Loop
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("🚀 Task Management System - Utility AI Orchestration");
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine();

var consoleSink = new DetailedConsoleSink();
await orch.RunAsync(intent, maxTicks: 20, CancellationToken.None, sink: consoleSink);

Console.WriteLine();
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("✅ Orchestration Complete");
Console.WriteLine("═══════════════════════════════════════════════════════");

/// <summary>
/// A custom sink to provide visibility into the decision-making process.
/// </summary>
public sealed class DetailedConsoleSink : IOrchestrationSink
{
    public void OnTickStart(Runtime rt) 
        => Console.WriteLine($"\n[Tick {rt.Tick}] 🔍 Sensing environment...");

    public void OnScored(Runtime rt, IReadOnlyList<(Proposal Proposal, double Utility)> scored)
    {
        Console.WriteLine($"[Tick {rt.Tick}] ⚖️ Scored Proposals:");
        foreach (var s in scored)
        {
            Console.WriteLine($"  - {s.Proposal.Id,-25} | Utility: {s.Utility:F3}");
        }
    }

    public void OnChosen(Runtime rt, Proposal chosen, double utility)
        => Console.WriteLine($"[Tick {rt.Tick}] ✨ Chosen: {chosen.Id} (u={utility:F3})");

    public void OnActed(Runtime rt, Proposal chosen) 
        => Console.WriteLine($"[Tick {rt.Tick}] ⚙️ Executed action for {chosen.Id}");

    public void OnStopped(Runtime rt, OrchestrationStopReason reason)
    {
        var emoji = reason == OrchestrationStopReason.MaxTicksReached ? "⏹️" : "🛑";
        Console.WriteLine($"\n{emoji} Stopped: {reason}");
    }
}