using Example.SmartHomeAgent.Considerations;
using Example.SmartHomeAgent.Models;
using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace Example.SmartHomeAgent.Modules;

/// <summary>
/// Capability to manage energy consumption and reduce costs.
/// Proposes different energy optimization strategies based on usage patterns, time of day, and occupancy.
/// </summary>
[Capability(Priority = 80, Domain = "energy")]
[RequiresFact<EnergyState>]
[RequiresFact<OccupancyPattern>]
public sealed class EnergyManagementModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var energy = rt.Bus.GetOrDefault<EnergyState>();
        var occupancy = rt.Bus.GetOrDefault<OccupancyPattern>();
        var climate = rt.Bus.GetOrDefault<ClimateState>();

        if (energy == null || occupancy == null) yield break;

        // STRATEGY 1: Reduce HVAC during peak hours (high cost, moderate comfort impact)
        yield return ProposalHelper.For("energy.reduce_hvac_peak")
            .WithDescription("Temporarily reduce heating/cooling during peak electricity pricing hours")
            .WithConsideration(new SignalConsideration<EnergyState>(
                name: "peak_hours",
                selector: e => e.IsPeakHours ? 1.0 : 0.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new SignalConsideration<EnergyState>(
                name: "high_cost",
                selector: e => e.CostPerKwh,
                curve: x => x * x, // Quadratic - gets urgent at high costs
                inputDomain: (0.10, 0.50)))
            .WithConsideration(new SignalConsideration<OccupancyPattern>(
                name: "not_critical_comfort",
                selector: o => o.CurrentMode != "sleep" ? 0.8 : 0.3,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithPrior(0.7) // Moderate priority
            .WithAction(async ct =>
            {
                await Task.Delay(100, ct);
                var targetAdjustment = occupancy.IsHome ? 2.0 : 3.0; // Less adjustment if home
                var action = new HomeAction(
                    ActionType: "adjust_hvac",
                    Target: "thermostat",
                    Parameters: new Dictionary<string, object>
                    {
                        ["adjustment"] = $"+{targetAdjustment}°C",
                        ["duration_minutes"] = 60,
                        ["reason"] = "peak_pricing"
                    },
                    Reason: $"Reducing HVAC load during peak hours (${energy.CostPerKwh:F2}/kWh)",
                    ExecutedAt: DateTime.UtcNow
                );
                rt.Bus.Publish(action);
                rt.Bus.Publish(new EnergyRecommendation(
                    RecommendationType: "peak_reduction",
                    EstimatedSavingsKwh: 2.5,
                    EstimatedCostSavings: 2.5 * energy.CostPerKwh,
                    Description: "Reduced HVAC during peak pricing"
                ));
                Console.WriteLine($"    ⚡ Reduced HVAC to save ${2.5 * energy.CostPerKwh:F2} during peak hours");
            })
            .Build();

        // STRATEGY 2: Turn off non-essential devices when away
        yield return ProposalHelper.For("energy.away_mode")
            .WithDescription("Power down non-essential devices when home is unoccupied")
            .WithConsideration(new SignalConsideration<OccupancyPattern>(
                name: "away_status",
                selector: o => (o.CurrentMode == "away" || o.CurrentMode == "vacation") ? 1.0 : 0.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new SignalConsideration<OccupancyPattern>(
                name: "been_away_long_enough",
                selector: o => (DateTime.UtcNow - o.ModeChangedAt).TotalMinutes,
                curve: x => Math.Min(1.0, x), // Ramp up over 30 minutes
                inputDomain: (15, 30)))
            .WithConsideration(new SignalConsideration<EnergyState>(
                name: "consuming_power",
                selector: e => e.CurrentWatts,
                curve: x => x, // More power = more reason to reduce
                inputDomain: (100, 2000)))
            .WithPrior(0.9) // High priority for away mode
            .WithAction(async ct =>
            {
                await Task.Delay(150, ct);
                var action = new HomeAction(
                    ActionType: "enable_away_mode",
                    Target: "all_zones",
                    Parameters: new Dictionary<string, object>
                    {
                        ["disable"] = new[] { "ambient_lights", "entertainment", "decorative" },
                        ["reduce"] = new[] { "hvac" },
                        ["maintain"] = new[] { "security", "refrigeration", "network" }
                    },
                    Reason: "Home unoccupied - entering energy conservation mode",
                    ExecutedAt: DateTime.UtcNow
                );
                rt.Bus.Publish(action);
                rt.Bus.Publish(new EnergyRecommendation(
                    RecommendationType: "away_mode",
                    EstimatedSavingsKwh: 5.0,
                    EstimatedCostSavings: 5.0 * energy.CostPerKwh,
                    Description: "Enabled away mode to reduce standby consumption"
                ));
                Console.WriteLine($"    🏠 Away mode activated - est. savings ${5.0 * energy.CostPerKwh:F2}/day");
            })
            .Build();

        // STRATEGY 3: Pre-cool/pre-heat using off-peak electricity
        yield return ProposalHelper.For("energy.precondition_offpeak")
            .WithDescription("Pre-condition home temperature during off-peak hours before peak pricing begins")
            .WithConsideration(new SignalConsideration<EnergyState>(
                name: "approaching_peak",
                selector: e =>
                {
                    var hour = DateTime.Now.Hour;
                    // Peak is typically 16:00-21:00, so precondition at 14:00-15:00
                    return (hour >= 14 && hour < 16) ? 1.0 : 0.0;
                },
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new SignalConsideration<EnergyState>(
                name: "currently_offpeak",
                selector: e => !e.IsPeakHours ? 1.0 : 0.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new SignalConsideration<OccupancyPattern>(
                name: "will_be_home",
                selector: o => o.CurrentMode == "home" || o.CurrentMode == "away" ? 0.8 : 0.2,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithPrior(0.6)
            .WithAction(async ct =>
            {
                await Task.Delay(100, ct);
                var isHeating = climate?.IndoorTempCelsius < climate?.TargetTempCelsius;
                var action = new HomeAction(
                    ActionType: "precondition_hvac",
                    Target: "thermostat",
                    Parameters: new Dictionary<string, object>
                    {
                        ["target"] = isHeating == true
                            ? climate!.TargetTempCelsius + 1.5
                            : climate!.TargetTempCelsius - 1.5,
                        ["mode"] = isHeating == true ? "heat" : "cool",
                        ["reason"] = "offpeak_preconditioning"
                    },
                    Reason: "Pre-conditioning home during off-peak hours to reduce peak demand",
                    ExecutedAt: DateTime.UtcNow
                );
                rt.Bus.Publish(action);
                Console.WriteLine($"    💡 Pre-conditioning home during off-peak pricing");
            })
            .Build();

        // STRATEGY 4: Load shifting for high-draw appliances
        yield return ProposalHelper.For("energy.defer_appliances")
            .WithDescription("Defer running of high-energy appliances like dishwasher, laundry to off-peak hours")
            .WithConsideration(new SignalConsideration<EnergyState>(
                name: "peak_demand_high",
                selector: e => e.PeakDemandWatts,
                curve: x => x * x, // Quadratic urgency
                inputDomain: (3000, 8000)))
            .WithConsideration(new SignalConsideration<OccupancyPattern>(
                name: "sleep_mode",
                selector: o => o.CurrentMode == "sleep" ? 0.3 : 0.8,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithPrior(0.5)
            .WithAction(async ct =>
            {
                await Task.Delay(100, ct);
                var action = new HomeAction(
                    ActionType: "defer_appliances",
                    Target: "smart_appliances",
                    Parameters: new Dictionary<string, object>
                    {
                        ["defer_until"] = "off_peak",
                        ["appliances"] = new[] { "dishwasher", "washing_machine", "dryer" },
                        ["max_delay_hours"] = 8
                    },
                    Reason: "Peak demand detected - deferring non-urgent appliances",
                    ExecutedAt: DateTime.UtcNow
                );
                rt.Bus.Publish(action);
                rt.Bus.Publish(new HomeNotification(
                    Priority: "low",
                    Category: "energy",
                    Message: "Dishwasher and laundry scheduled for off-peak hours",
                    CreatedAt: DateTime.UtcNow
                ));
                Console.WriteLine($"    ⏱️  Deferred high-draw appliances to off-peak hours");
            })
            .Build();
    }
}
