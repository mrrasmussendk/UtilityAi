using Example.SmartHomeAgent.Considerations;
using Example.SmartHomeAgent.Models;
using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace Example.SmartHomeAgent.Modules;

/// <summary>
/// Capability to monitor device health and schedule maintenance.
/// Proposes proactive maintenance actions to prevent failures and extend device life.
/// </summary>
[Capability(Priority = 60, Domain = "maintenance")]
[RequiresFact<OccupancyPattern>]
public sealed class MaintenanceModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var occupancy = rt.Bus.GetOrDefault<OccupancyPattern>();
        if (occupancy == null) yield break;

        // Get recent device health facts from history
        // (DeviceHealthSensor publishes multiple DeviceHealth facts, one per device)
        var deviceHealthHistory = rt.Bus.GetHistory<DeviceHealth>(maxItems: 10);
        var deviceHealthList = deviceHealthHistory.Select(e => e.Value).ToList();

        // Select devices that need attention using domain logic
        var devicesNeedingAttention = deviceHealthList
            .Where(d => !d.IsResponsive ||
                       d.BatteryLevel < 20 ||
                       d.Warnings.Any() ||
                       (DateTime.UtcNow - d.LastMaintenance).TotalDays > 90)
            .OrderByDescending(d => CalculateUrgency(d))
            .Take(3) // Focus on top 3 most urgent
            .ToList();

        if (!devicesNeedingAttention.Any()) yield break;

        var mostUrgent = devicesNeedingAttention.First();

        // STRATEGY 1: Replace low battery devices
        yield return ProposalHelper.For("maintenance.battery_alert")
            .WithDescription("Alert and recommend battery replacement for low-battery devices")
            .WithConsideration(new SignalConsideration<DeviceHealth>(
                name: "battery_critical",
                selector: d => mostUrgent.BatteryLevel,
                curve: x => 1.0 - x, // Inverted - lower battery = higher urgency
                inputDomain: (0, 100)))
            .WithConsideration(new ConstantValue(
                name: "device_exists",
                value: 1.0))
            .WithPrior(0.9) // High priority - device failure is bad
            .WithAction(async ct =>
            {
                await Task.Delay(80, ct);
                var action = new HomeAction(
                    ActionType: "battery_maintenance",
                    Target: mostUrgent.DeviceId,
                    Parameters: new Dictionary<string, object>
                    {
                        ["device_type"] = mostUrgent.DeviceType,
                        ["current_level"] = mostUrgent.BatteryLevel,
                        ["action_required"] = mostUrgent.BatteryLevel < 5 ? "immediate" : "soon"
                    },
                    Reason: $"Battery at {mostUrgent.BatteryLevel:F0}% - maintenance required",
                    ExecutedAt: DateTime.UtcNow
                );
                rt.Bus.Publish(action);
                rt.Bus.Publish(new HomeNotification(
                    Priority: mostUrgent.BatteryLevel < 10 ? "high" : "medium",
                    Category: "maintenance",
                    Message: $"🔋 {mostUrgent.DeviceType} ({mostUrgent.DeviceId}) battery at {mostUrgent.BatteryLevel:F0}%",
                    CreatedAt: DateTime.UtcNow
                ));
                Console.WriteLine($"    🔋 Battery alert: {mostUrgent.DeviceType} at {mostUrgent.BatteryLevel:F0}%");
            })
            .Build();

        // STRATEGY 2: Handle unresponsive devices
        yield return ProposalHelper.For("maintenance.device_offline")
            .WithDescription("Attempt to reconnect or alert about unresponsive devices")
            .WithConsideration(new SignalConsideration<DeviceHealth>(
                name: "is_unresponsive",
                selector: d => !mostUrgent.IsResponsive ? 1.0 : 0.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new ConstantValue(
                name: "device_exists",
                value: 1.0))
            .WithPrior(0.85)
            .WithAction(async ct =>
            {
                await Task.Delay(100, ct);
                var action = new HomeAction(
                    ActionType: "reconnect_device",
                    Target: mostUrgent.DeviceId,
                    Parameters: new Dictionary<string, object>
                    {
                        ["device_type"] = mostUrgent.DeviceType,
                        ["last_seen"] = DateTime.UtcNow - TimeSpan.FromMinutes(30),
                        ["action"] = new[] { "ping", "reboot", "reconfigure" }
                    },
                    Reason: $"Device {mostUrgent.DeviceId} not responding",
                    ExecutedAt: DateTime.UtcNow
                );
                rt.Bus.Publish(action);
                rt.Bus.Publish(new HomeNotification(
                    Priority: "high",
                    Category: "maintenance",
                    Message: $"⚠️ {mostUrgent.DeviceType} ({mostUrgent.DeviceId}) is offline. Attempting reconnection...",
                    CreatedAt: DateTime.UtcNow
                ));
                Console.WriteLine($"    📡 Device offline: Attempting to reconnect {mostUrgent.DeviceId}");
            })
            .Build();

        // STRATEGY 3: Schedule routine maintenance
        yield return ProposalHelper.For("maintenance.routine_service")
            .WithDescription("Schedule or remind about routine maintenance for devices")
            .WithConsideration(new SignalConsideration<DeviceHealth>(
                name: "overdue_maintenance",
                selector: d => (DateTime.UtcNow - mostUrgent.LastMaintenance).TotalDays,
                curve: x => Math.Min(1.0, x), // Linear ramp up
                inputDomain: (90, 180)))
            .WithConsideration(new SignalConsideration<OccupancyPattern>(
                name: "good_timing",
                selector: o => o.CurrentMode == "home" ? 0.8 : 0.3,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new ConstantValue(
                name: "device_exists",
                value: 1.0))
            .WithPrior(0.5)
            .WithAction(async ct =>
            {
                await Task.Delay(80, ct);
                var daysSinceMaintenance = (DateTime.UtcNow - mostUrgent.LastMaintenance).TotalDays;

                var action = new HomeAction(
                    ActionType: "schedule_maintenance",
                    Target: mostUrgent.DeviceId,
                    Parameters: new Dictionary<string, object>
                    {
                        ["device_type"] = mostUrgent.DeviceType,
                        ["days_since_last"] = (int)daysSinceMaintenance,
                        ["maintenance_type"] = GetMaintenanceType(mostUrgent.DeviceType),
                        ["suggested_date"] = DateTime.UtcNow.AddDays(7)
                    },
                    Reason: $"Last maintenance {daysSinceMaintenance:F0} days ago",
                    ExecutedAt: DateTime.UtcNow
                );
                rt.Bus.Publish(action);
                rt.Bus.Publish(new HomeNotification(
                    Priority: "low",
                    Category: "maintenance",
                    Message: $"🛠️ {mostUrgent.DeviceType} ({mostUrgent.DeviceId}) is due for routine maintenance",
                    CreatedAt: DateTime.UtcNow
                ));
                Console.WriteLine($"    🛠️  Maintenance due: {mostUrgent.DeviceType} ({daysSinceMaintenance:F0} days)");
            })
            .Build();

        // STRATEGY 4: Address device warnings
        yield return ProposalHelper.For("maintenance.address_warnings")
            .WithDescription("Investigate and resolve device warning messages")
            .WithConsideration(new SignalConsideration<DeviceHealth>(
                name: "has_warnings",
                selector: d => mostUrgent.Warnings.Count,
                curve: x => Math.Min(1.0, x),
                inputDomain: (1, 5)))
            .WithConsideration(new ConstantValue(
                name: "device_exists",
                value: 1.0))
            .WithPrior(0.7)
            .WithAction(async ct =>
            {
                await Task.Delay(90, ct);
                var action = new HomeAction(
                    ActionType: "investigate_warnings",
                    Target: mostUrgent.DeviceId,
                    Parameters: new Dictionary<string, object>
                    {
                        ["device_type"] = mostUrgent.DeviceType,
                        ["warning_count"] = mostUrgent.Warnings.Count,
                        ["warnings"] = mostUrgent.Warnings,
                        ["diagnostic_action"] = "run_diagnostics"
                    },
                    Reason: $"{mostUrgent.Warnings.Count} warning(s) detected",
                    ExecutedAt: DateTime.UtcNow
                );
                rt.Bus.Publish(action);
                rt.Bus.Publish(new HomeNotification(
                    Priority: "medium",
                    Category: "maintenance",
                    Message: $"⚠️ {mostUrgent.DeviceType} reporting: {string.Join(", ", mostUrgent.Warnings)}",
                    CreatedAt: DateTime.UtcNow
                ));
                Console.WriteLine($"    ⚠️  Device warnings: {mostUrgent.DeviceType} - {mostUrgent.Warnings.Count} issue(s)");
            })
            .Build();
    }

    private static double CalculateUrgency(DeviceHealth device)
    {
        double urgency = 0.0;

        // Battery contributes to urgency
        if (device.BatteryLevel < 20)
            urgency += (20 - device.BatteryLevel) / 20.0;

        // Unresponsive devices are very urgent
        if (!device.IsResponsive)
            urgency += 0.8;

        // Warnings contribute
        urgency += Math.Min(0.5, device.Warnings.Count * 0.2);

        // Overdue maintenance contributes
        var daysSinceMaintenance = (DateTime.UtcNow - device.LastMaintenance).TotalDays;
        if (daysSinceMaintenance > 90)
            urgency += Math.Min(0.3, (daysSinceMaintenance - 90) / 180.0);

        return urgency;
    }

    private static string GetMaintenanceType(string deviceType)
    {
        return deviceType.ToLowerInvariant() switch
        {
            "thermostat" => "filter_replacement",
            "smoke_detector" => "sensor_test",
            "camera" => "lens_cleaning",
            "door_lock" => "battery_and_mechanism_check",
            "smart_plug" => "firmware_update",
            _ => "general_inspection"
        };
    }
}
