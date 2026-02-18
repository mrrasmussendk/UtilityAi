using Example.SmartHomeAgent.Considerations;
using Example.SmartHomeAgent.Models;
using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace Example.SmartHomeAgent.Modules;

/// <summary>
/// Capability to monitor and respond to security concerns.
/// Proposes security actions based on occupancy, time, and detected anomalies.
/// </summary>
[Capability(Priority = 100, Domain = "security")]
[RequiresFact<SecurityState>]
[RequiresFact<OccupancyPattern>]
public sealed class SecurityMonitoringModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var security = rt.Bus.GetOrDefault<SecurityState>();
        var occupancy = rt.Bus.GetOrDefault<OccupancyPattern>();

        if (security == null || occupancy == null) yield break;

        // STRATEGY 1: Arm security system when leaving
        yield return ProposalHelper.For("security.arm_system")
            .WithDescription("Activate security system when home becomes unoccupied")
            .WithConsideration(new SignalConsideration<OccupancyPattern>(
                name: "away_mode",
                selector: o => (o.CurrentMode == "away" || o.CurrentMode == "vacation") ? 1.0 : 0.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new SignalConsideration<SecurityState>(
                name: "not_armed",
                selector: s => !s.AlarmArmed ? 1.0 : 0.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new SignalConsideration<OccupancyPattern>(
                name: "been_away_enough",
                selector: o => (DateTime.UtcNow - o.ModeChangedAt).TotalMinutes,
                curve: x => Math.Min(1.0, x),
                inputDomain: (2, 5))) // Wait 5 minutes to avoid false triggers
            .WithPrior(0.95) // Very high priority
            .WithTemperature(1.5) // Decisive action
            .WithAction(async ct =>
            {
                await Task.Delay(150, ct);
                var action = new HomeAction(
                    ActionType: "arm_security",
                    Target: "security_system",
                    Parameters: new Dictionary<string, object>
                    {
                        ["mode"] = occupancy.CurrentMode == "vacation" ? "full" : "away",
                        ["delay_seconds"] = 30,
                        ["notify"] = true
                    },
                    Reason: $"Home entered {occupancy.CurrentMode} mode",
                    ExecutedAt: DateTime.UtcNow
                );
                rt.Bus.Publish(action);
                rt.Bus.Publish(new HomeNotification(
                    Priority: "medium",
                    Category: "security",
                    Message: $"Security system armed in {occupancy.CurrentMode} mode",
                    CreatedAt: DateTime.UtcNow
                ));
                Console.WriteLine($"    🔒 Security system armed ({occupancy.CurrentMode} mode)");
            })
            .Build();

        // STRATEGY 2: Alert on unexpected entry
        yield return ProposalHelper.For("security.alert_unexpected_entry")
            .WithDescription("Send high-priority alert when entry detected while armed")
            .WithConsideration(new SignalConsideration<SecurityState>(
                name: "armed_and_motion",
                selector: s => s.AlarmArmed && s.MotionDetected ? 1.0 : 0.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new SignalConsideration<OccupancyPattern>(
                name: "should_be_away",
                selector: o => (o.CurrentMode == "away" || o.CurrentMode == "vacation") ? 1.0 : 0.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithPrior(1.0) // Critical priority
            .WithTemperature(2.0) // Very decisive
            .WithAction(async ct =>
            {
                await Task.Delay(100, ct);
                var action = new HomeAction(
                    ActionType: "security_alert",
                    Target: "notification_system",
                    Parameters: new Dictionary<string, object>
                    {
                        ["alert_type"] = "unexpected_entry",
                        ["timestamp"] = DateTime.UtcNow,
                        ["open_doors"] = security.OpenDoors,
                        ["open_windows"] = security.OpenWindows,
                        ["action_taken"] = new[] { "siren", "camera_record", "notify_owner", "notify_authorities" }
                    },
                    Reason: "Motion detected while security system armed",
                    ExecutedAt: DateTime.UtcNow
                );
                rt.Bus.Publish(action);
                rt.Bus.Publish(new HomeNotification(
                    Priority: "critical",
                    Category: "security",
                    Message: "⚠️ ALERT: Unexpected motion detected! Security system triggered.",
                    CreatedAt: DateTime.UtcNow
                ));
                Console.WriteLine($"    🚨 SECURITY ALERT: Unexpected entry detected!");
            })
            .Build();

        // STRATEGY 3: Check doors and windows before bedtime
        yield return ProposalHelper.For("security.bedtime_check")
            .WithDescription("Verify all doors and windows are secured before sleep mode")
            .WithConsideration(new SignalConsideration<OccupancyPattern>(
                name: "sleep_mode",
                selector: o => o.CurrentMode == "sleep" ? 1.0 : 0.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new SignalConsideration<SecurityState>(
                name: "has_openings",
                selector: s => (s.OpenDoors.Count + s.OpenWindows.Count),
                curve: x => Math.Min(1.0, x),
                inputDomain: (1, 3)))
            .WithConsideration(new SignalConsideration<SecurityState>(
                name: "check_needed",
                selector: s => s.LastSecurityCheck.HasValue
                    ? (DateTime.UtcNow - s.LastSecurityCheck.Value).TotalMinutes
                    : 1000,
                curve: x => Math.Min(1.0, x),
                inputDomain: (30, 60)))
            .WithPrior(0.85)
            .WithAction(async ct =>
            {
                await Task.Delay(100, ct);
                var openings = security.OpenDoors.Count + security.OpenWindows.Count;

                if (openings > 0)
                {
                    var items = security.OpenDoors.Concat(security.OpenWindows).ToList();
                    var action = new HomeAction(
                        ActionType: "security_check",
                        Target: "entry_points",
                        Parameters: new Dictionary<string, object>
                        {
                            ["open_count"] = openings,
                            ["open_items"] = items,
                            ["recommendation"] = "close_before_sleep"
                        },
                        Reason: "Bedtime security check",
                        ExecutedAt: DateTime.UtcNow
                    );
                    rt.Bus.Publish(action);
                    rt.Bus.Publish(new HomeNotification(
                        Priority: "high",
                        Category: "security",
                        Message: $"⚠️ {openings} entry point(s) open: {string.Join(", ", items)}. Please secure before sleeping.",
                        CreatedAt: DateTime.UtcNow
                    ));
                    Console.WriteLine($"    🔓 Security check: {openings} opening(s) detected");
                }
                else
                {
                    Console.WriteLine($"    ✅ Security check: All entry points secured");
                }
            })
            .Build();

        // STRATEGY 4: Vacation mode surveillance
        yield return ProposalHelper.For("security.vacation_monitoring")
            .WithDescription("Enhanced monitoring and activity simulation during vacation")
            .WithConsideration(new SignalConsideration<OccupancyPattern>(
                name: "vacation_mode",
                selector: o => o.CurrentMode == "vacation" ? 1.0 : 0.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new SignalConsideration<OccupancyPattern>(
                name: "time_for_simulation",
                selector: o =>
                {
                    var hour = DateTime.Now.Hour;
                    // Simulate activity in evening (18:00-22:00)
                    return (hour >= 18 && hour <= 22) ? 1.0 : 0.2;
                },
                curve: x => x,
                inputDomain: (0, 1)))
            .WithPrior(0.7)
            .WithAction(async ct =>
            {
                await Task.Delay(120, ct);
                var action = new HomeAction(
                    ActionType: "vacation_simulation",
                    Target: "smart_devices",
                    Parameters: new Dictionary<string, object>
                    {
                        ["simulate_occupancy"] = true,
                        ["lights"] = new[] { "living_room", "bedroom", "kitchen" },
                        ["pattern"] = "random_realistic",
                        ["tv_on"] = DateTime.Now.Hour >= 19 && DateTime.Now.Hour <= 22,
                        ["camera_recording"] = "continuous"
                    },
                    Reason: "Vacation mode - simulating presence to deter intrusion",
                    ExecutedAt: DateTime.UtcNow
                );
                rt.Bus.Publish(action);
                Console.WriteLine($"    🏖️  Vacation mode: Simulating occupancy");
            })
            .Build();
    }
}
