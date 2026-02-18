using Example.SmartHomeAgent.Considerations;
using Example.SmartHomeAgent.Models;
using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace Example.SmartHomeAgent.Modules;

/// <summary>
/// Capability to maintain optimal comfort levels for occupants.
/// Proposes climate and lighting adjustments based on occupancy, time, and environmental conditions.
/// </summary>
[Capability(Priority = 90, Domain = "comfort")]
[RequiresFact<ClimateState>]
[RequiresFact<OccupancyPattern>]
public sealed class ComfortOptimizationModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var climate = rt.Bus.GetOrDefault<ClimateState>();
        var occupancy = rt.Bus.GetOrDefault<OccupancyPattern>();
        var weather = rt.Bus.GetOrDefault<WeatherForecast>();

        if (climate == null || occupancy == null) yield break;

        // STRATEGY 1: Adjust temperature when occupied and uncomfortable
        yield return ProposalHelper.For("comfort.adjust_temperature")
            .WithDescription("Adjust HVAC to reach target temperature when home is occupied")
            .WithConsideration(new SignalConsideration<OccupancyPattern>(
                name: "occupied",
                selector: o => o.IsHome ? 1.0 : 0.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new SignalConsideration<ClimateState>(
                name: "temp_deviation",
                selector: c => Math.Abs(c.IndoorTempCelsius - c.TargetTempCelsius),
                curve: x => x * x, // Quadratic - bigger deviations are more urgent
                inputDomain: (0, 4)))
            .WithConsideration(new SignalConsideration<ClimateState>(
                name: "reasonable_target",
                selector: c =>
                {
                    // Penalize extreme targets that waste energy
                    var deviation = Math.Abs(c.TargetTempCelsius - c.OutdoorTempCelsius);
                    return deviation < 10 ? 1.0 : Math.Max(0.3, 1.0 - (deviation - 10) / 20);
                },
                curve: x => x,
                inputDomain: (0, 1)))
            .WithPrior(0.95) // High priority - comfort matters
            .WithTemperature(1.3) // Sharp curve - want decisive action
            .WithAction(async ct =>
            {
                await Task.Delay(100, ct);
                var delta = climate.TargetTempCelsius - climate.IndoorTempCelsius;
                var mode = delta > 0 ? "heat" : "cool";

                var action = new HomeAction(
                    ActionType: "adjust_temperature",
                    Target: "thermostat",
                    Parameters: new Dictionary<string, object>
                    {
                        ["target"] = climate.TargetTempCelsius,
                        ["current"] = climate.IndoorTempCelsius,
                        ["mode"] = mode,
                        ["fan"] = "auto"
                    },
                    Reason: $"Indoor {climate.IndoorTempCelsius:F1}°C, target {climate.TargetTempCelsius:F1}°C",
                    ExecutedAt: DateTime.UtcNow
                );
                rt.Bus.Publish(action);
                Console.WriteLine($"    🌡️  Adjusting temperature: {climate.IndoorTempCelsius:F1}°C → {climate.TargetTempCelsius:F1}°C");
            })
            .Build();

        // STRATEGY 2: Adjust lighting based on time and occupancy
        yield return ProposalHelper.For("comfort.adjust_lighting")
            .WithDescription("Optimize lighting levels based on time of day and occupancy patterns")
            .WithConsideration(new SignalConsideration<OccupancyPattern>(
                name: "occupied",
                selector: o => o.IsHome ? 1.0 : 0.0,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new SignalConsideration<OccupancyPattern>(
                name: "time_based",
                selector: o =>
                {
                    var hour = DateTime.Now.Hour;
                    // Morning (6-9): 0.8, Day (9-18): 0.3, Evening (18-23): 1.0, Night (23-6): 0.1
                    if (hour >= 6 && hour < 9) return 0.8;
                    if (hour >= 9 && hour < 18) return 0.3;
                    if (hour >= 18 && hour < 23) return 1.0;
                    return 0.1;
                },
                curve: x => x,
                inputDomain: (0, 1)))
            .WithConsideration(new SignalConsideration<OccupancyPattern>(
                name: "mode_appropriate",
                selector: o => o.CurrentMode switch
                {
                    "sleep" => 0.1, // Don't adjust lights during sleep
                    "home" => 1.0,
                    "away" => 0.0,
                    _ => 0.5
                },
                curve: x => x,
                inputDomain: (0, 1)))
            .WithPrior(0.7)
            .WithAction(async ct =>
            {
                await Task.Delay(80, ct);
                var hour = DateTime.Now.Hour;
                var brightness = hour switch
                {
                    >= 6 and < 9 => 70,    // Morning
                    >= 9 and < 18 => 40,   // Daytime
                    >= 18 and < 23 => 85,  // Evening
                    _ => 10                 // Night
                };

                var action = new HomeAction(
                    ActionType: "adjust_lighting",
                    Target: "smart_lights",
                    Parameters: new Dictionary<string, object>
                    {
                        ["brightness"] = brightness,
                        ["color_temp"] = hour < 18 ? "cool" : "warm",
                        ["zones"] = occupancy.IsHome ? new[] { "living", "kitchen", "bedroom" } : new[] { "entrance" }
                    },
                    Reason: $"Optimizing lighting for time of day (hour: {hour})",
                    ExecutedAt: DateTime.UtcNow
                );
                rt.Bus.Publish(action);
                Console.WriteLine($"    💡 Adjusted lighting to {brightness}% brightness");
            })
            .Build();

        // STRATEGY 3: Humidity control
        yield return ProposalHelper.For("comfort.adjust_humidity")
            .WithDescription("Maintain comfortable humidity levels using humidifier/dehumidifier")
            .WithConsideration(new SignalConsideration<ClimateState>(
                name: "humidity_deviation",
                selector: c =>
                {
                    // Ideal humidity: 40-60%
                    if (c.Humidity >= 40 && c.Humidity <= 60) return 0.0;
                    if (c.Humidity < 40) return (40 - c.Humidity) / 20; // Too dry
                    return (c.Humidity - 60) / 30; // Too humid
                },
                curve: x => x * x,
                inputDomain: (0, 1)))
            .WithConsideration(new SignalConsideration<OccupancyPattern>(
                name: "occupied",
                selector: o => o.IsHome ? 1.0 : 0.3,
                curve: x => x,
                inputDomain: (0, 1)))
            .WithPrior(0.6)
            .WithAction(async ct =>
            {
                await Task.Delay(80, ct);
                var action = climate.Humidity < 40
                    ? new HomeAction(
                        ActionType: "increase_humidity",
                        Target: "humidifier",
                        Parameters: new Dictionary<string, object>
                        {
                            ["target_humidity"] = 50,
                            ["current_humidity"] = climate.Humidity
                        },
                        Reason: $"Humidity too low at {climate.Humidity:F0}%",
                        ExecutedAt: DateTime.UtcNow)
                    : new HomeAction(
                        ActionType: "decrease_humidity",
                        Target: "dehumidifier",
                        Parameters: new Dictionary<string, object>
                        {
                            ["target_humidity"] = 55,
                            ["current_humidity"] = climate.Humidity
                        },
                        Reason: $"Humidity too high at {climate.Humidity:F0}%",
                        ExecutedAt: DateTime.UtcNow);

                rt.Bus.Publish(action);
                Console.WriteLine($"    💧 Adjusting humidity: {climate.Humidity:F0}% → target 50%");
            })
            .Build();

        // STRATEGY 4: Anticipatory heating/cooling based on weather forecast
        yield return ProposalHelper.For("comfort.anticipate_weather")
            .WithDescription("Proactively adjust climate settings based on upcoming weather changes")
            .WithConsideration(new HasFact<WeatherForecast>(
                name: "has_forecast",
                selector: null))
            .WithConsideration(new SignalConsideration<WeatherForecast>(
                name: "significant_change",
                selector: w => Math.Abs(w.ForecastTempCelsius - (climate?.OutdoorTempCelsius ?? 20)),
                curve: x => x,
                inputDomain: (5, 15)))
            .WithConsideration(new SignalConsideration<WeatherForecast>(
                name: "near_term",
                selector: w => (w.ForecastTime - DateTime.UtcNow).TotalHours,
                curve: x => 1.0 - x, // Inverted - closer = higher urgency
                inputDomain: (1, 6)))
            .WithPrior(0.5)
            .WithAction(async ct =>
            {
                await Task.Delay(100, ct);
                var tempChange = weather!.ForecastTempCelsius - climate.OutdoorTempCelsius;

                var action = new HomeAction(
                    ActionType: "anticipatory_adjustment",
                    Target: "hvac_system",
                    Parameters: new Dictionary<string, object>
                    {
                        ["forecast_temp"] = weather.ForecastTempCelsius,
                        ["forecast_conditions"] = weather.Conditions,
                        ["proactive_adjustment"] = tempChange > 0 ? "pre_cool" : "pre_heat"
                    },
                    Reason: $"Weather forecast: {weather.Conditions}, {weather.ForecastTempCelsius:F0}°C",
                    ExecutedAt: DateTime.UtcNow
                );
                rt.Bus.Publish(action);
                Console.WriteLine($"    🌤️  Anticipating weather change: {tempChange:+0.0;-0.0}°C");
            })
            .Build();
    }
}
