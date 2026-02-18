using Example.SmartHomeAgent.Models;
using Example.SmartHomeAgent.Modules;
using Example.SmartHomeAgent.Sensors;
using UtilityAi.Consideration;
using UtilityAi.Orchestration;
using UtilityAi.Utils;

// =============================================================================
// SMART HOME AUTOMATION AGENT - UTILITY AI ORCHESTRATION EXAMPLE
// =============================================================================
// This example demonstrates a realistic smart home automation system that uses
// UtilityAI to intelligently balance multiple competing priorities:
//
// CAPABILITIES (What the agent CAN DO):
// 1. Energy Management - Optimize power consumption and reduce costs
// 2. Comfort Optimization - Maintain comfortable temperature, lighting, humidity
// 3. Security Monitoring - Arm/disarm system, detect intrusions, verify entry points
// 4. Device Maintenance - Monitor device health, alert on issues, schedule service
//
// KEY PATTERNS DEMONSTRATED:
// - One module per capability (NOT per device or room)
// - Multiple strategies per capability (different approaches to same goal)
// - Domain-specific item selection (picking which device needs attention)
// - Declarative scoring via considerations (no if-statements in Propose())
// - Real-time priority balancing (energy vs comfort vs security)
//
// ANTI-PATTERNS AVOIDED:
// - ❌ Creating a module per device/room (would be 100+ modules)
// - ❌ Looping through devices to create proposals (wastes CPU scoring)
// - ❌ Using if-statements for scoring (use considerations instead)
// =============================================================================

Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("🏠 Smart Home Automation Agent - Utility AI Example");
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine();

// Initialize the EventBus and publish initial state
var bus = new EventBus();

// Scenario: It's 4:30 PM, peak electricity hours are approaching
// Homeowner has just left for the day
Console.WriteLine("📅 Scenario: Late Afternoon - Owner Just Left Home");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine();

// Publish initial occupancy state
bus.Publish(new OccupancyPattern(
    IsHome: false,
    OccupantCount: 0,
    CurrentMode: "away",
    ModeChangedAt: DateTime.UtcNow.AddMinutes(-3) // Left 3 minutes ago
));

// Initial climate state (slightly warm, HVAC was running)
bus.Publish(new ClimateState(
    IndoorTempCelsius: 23.5,
    OutdoorTempCelsius: 18.0,
    TargetTempCelsius: 21.0,
    Humidity: 55.0,
    OccupancyDetected: false
));

// Energy state (approaching peak hours)
bus.Publish(new EnergyState(
    CurrentWatts: 1200,
    PeakDemandWatts: 2400,
    CostPerKwh: 0.30, // Getting expensive
    IsPeakHours: false, // Not quite peak yet
    LastUpdated: DateTime.UtcNow
));

// Security state (not yet armed, kitchen window left open!)
bus.Publish(new SecurityState(
    AlarmArmed: false,
    OpenDoors: new List<string>(),
    OpenWindows: new List<string> { "kitchen_window" }, // Oops!
    MotionDetected: false,
    LastSecurityCheck: null
));

Console.WriteLine("📊 Initial System State:");
Console.WriteLine($"  🏠 Occupancy: {bus.GetOrDefault<OccupancyPattern>()?.CurrentMode}");
Console.WriteLine($"  🌡️  Temperature: {bus.GetOrDefault<ClimateState>()?.IndoorTempCelsius:F1}°C (target: {bus.GetOrDefault<ClimateState>()?.TargetTempCelsius:F1}°C)");
Console.WriteLine($"  ⚡ Power Usage: {bus.GetOrDefault<EnergyState>()?.CurrentWatts:F0}W @ ${bus.GetOrDefault<EnergyState>()?.CostPerKwh:F2}/kWh");
Console.WriteLine($"  🔒 Security: {(bus.GetOrDefault<SecurityState>()?.AlarmArmed == true ? "Armed" : "NOT ARMED")}");
Console.WriteLine($"  ⚠️  Open Windows: {string.Join(", ", bus.GetOrDefault<SecurityState>()?.OpenWindows ?? new List<string>())}");
Console.WriteLine();

// Create orchestrator with sensors and auto-discovered capability modules
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("🔍 Discovering Capabilities...");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

var orchestrator = new UtilityAiOrchestrator(null, true, bus)
    .AddSensor(new EnvironmentSensor())
    .AddSensor(new EnergyMonitorSensor())
    .AddSensor(new SecuritySensor())
    .AddSensor(new DeviceHealthSensor())
    .DiscoverCapabilities(typeof(EnergyManagementModule).Assembly);

Console.WriteLine("  ✅ EnergyManagementModule [Priority: 80]");
Console.WriteLine("     • Reduce HVAC during peak hours");
Console.WriteLine("     • Enable away mode power savings");
Console.WriteLine("     • Pre-condition during off-peak");
Console.WriteLine("     • Defer high-draw appliances");
Console.WriteLine();
Console.WriteLine("  ✅ ComfortOptimizationModule [Priority: 90]");
Console.WriteLine("     • Adjust temperature to target");
Console.WriteLine("     • Optimize lighting for time/occupancy");
Console.WriteLine("     • Control humidity levels");
Console.WriteLine("     • Anticipate weather changes");
Console.WriteLine();
Console.WriteLine("  ✅ SecurityMonitoringModule [Priority: 100] ⚠️ HIGHEST");
Console.WriteLine("     • Arm system when away");
Console.WriteLine("     • Alert on unexpected entry");
Console.WriteLine("     • Check doors/windows at bedtime");
Console.WriteLine("     • Vacation mode surveillance");
Console.WriteLine();
Console.WriteLine("  ✅ MaintenanceModule [Priority: 60]");
Console.WriteLine("     • Battery replacement alerts");
Console.WriteLine("     • Reconnect offline devices");
Console.WriteLine("     • Schedule routine maintenance");
Console.WriteLine("     • Address device warnings");
Console.WriteLine();

var intent = new UserIntent(
    Goal: new IntentGoal("optimize-home"),
    Slots: new Dictionary<string, object?>
    {
        ["scenario"] = "owner_left_home",
        ["priorities"] = new[] { "security", "energy", "comfort" }
    }
);

Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
Console.WriteLine("🔄 Running Orchestration (10 ticks)...");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

var sink = new SmartHomeConsoleSink();
await orchestrator.RunAsync(intent, maxTicks: 10, CancellationToken.None, sink);

Console.WriteLine();
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("📈 Orchestration Summary");
Console.WriteLine("═══════════════════════════════════════════════════════");
sink.PrintSummary();

Console.WriteLine();
Console.WriteLine("💡 Key Takeaways:");
Console.WriteLine("  • Capabilities represent what the agent CAN DO (manage energy, provide comfort, etc.)");
Console.WriteLine("  • Each capability has multiple STRATEGIES (different approaches)");
Console.WriteLine("  • Utility system automatically balances competing priorities");
Console.WriteLine("  • Security actions won due to highest priority when away");
Console.WriteLine("  • No hardcoded workflows - behavior emerges from utility scores");
Console.WriteLine("  • Domain logic selects WHICH device, utility decides WHAT to do");
Console.WriteLine();
Console.WriteLine("📚 Compare with AgentAssistant example to see different use cases!");
Console.WriteLine();

/// <summary>
/// Custom sink that provides visibility into the smart home agent's decision process.
/// </summary>
public sealed class SmartHomeConsoleSink : IOrchestrationSink
{
    private readonly List<(string proposalId, double utility, int tick)> _history = new();
    private readonly Dictionary<string, int> _actionCounts = new();

    public void OnTickStart(Runtime rt)
    {
        Console.WriteLine($"\n[Tick {rt.Tick}] 🔍 Sensors updated, evaluating proposals...");
    }

    public void OnScored(Runtime rt, IReadOnlyList<(Proposal Proposal, double Utility)> scored)
    {
        var top5 = scored.OrderByDescending(s => s.Utility).Take(5).ToList();

        if (rt.Tick <= 2 || scored.Count > 0) // Show details for first few ticks
        {
            Console.WriteLine($"[Tick {rt.Tick}] ⚖️  Top Proposals:");
            foreach (var s in top5)
            {
                var emoji = s.Proposal.Id.Split('.')[0] switch
                {
                    "energy" => "⚡",
                    "comfort" => "🌡️",
                    "security" => "🔒",
                    "maintenance" => "🛠️",
                    _ => "•"
                };
                Console.WriteLine($"  {emoji} {s.Proposal.Id,-40} | Utility: {s.Utility:F3}");
            }
        }
    }

    public void OnChosen(Runtime rt, Proposal chosen, double utility)
    {
        _history.Add((chosen.Id, utility, rt.Tick));

        var category = chosen.Id.Split('.')[0];
        _actionCounts[category] = _actionCounts.GetValueOrDefault(category) + 1;

        var emoji = category switch
        {
            "energy" => "⚡",
            "comfort" => "🌡️",
            "security" => "🔒",
            "maintenance" => "🛠️",
            _ => "✨"
        };

        Console.WriteLine($"[Tick {rt.Tick}] {emoji} WINNER: {chosen.Id} (utility={utility:F3})");
    }

    public void OnActed(Runtime rt, Proposal chosen)
    {
        // Action output already logged in modules
    }

    public void OnStopped(Runtime rt, OrchestrationStopReason reason)
    {
        Console.WriteLine($"\n⏹️  Orchestration stopped: {reason}");
    }

    public void PrintSummary()
    {
        Console.WriteLine("\n📊 Actions Taken by Category:");
        foreach (var (category, count) in _actionCounts.OrderByDescending(kv => kv.Value))
        {
            var emoji = category switch
            {
                "energy" => "⚡",
                "comfort" => "🌡️",
                "security" => "🔒",
                "maintenance" => "🛠️",
                _ => "•"
            };
            var bar = new string('█', Math.Min(count * 3, 30));
            Console.WriteLine($"  {emoji} {category,-15} {bar} ({count})");
        }

        Console.WriteLine("\n📋 Action Timeline:");
        foreach (var (id, utility, tick) in _history.Take(8))
        {
            var category = id.Split('.')[0];
            var emoji = category switch
            {
                "energy" => "⚡",
                "comfort" => "🌡️",
                "security" => "🔒",
                "maintenance" => "🛠️",
                _ => "•"
            };
            Console.WriteLine($"  Tick {tick}: {emoji} {id} (u={utility:F2})");
        }

        if (_history.Count > 8)
        {
            Console.WriteLine($"  ... and {_history.Count - 8} more actions");
        }
    }
}
