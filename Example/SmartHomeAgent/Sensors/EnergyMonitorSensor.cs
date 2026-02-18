using Example.SmartHomeAgent.Models;
using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace Example.SmartHomeAgent.Sensors;

/// <summary>
/// Monitors energy consumption and publishes energy state to EventBus.
/// Simulates smart meter data and time-of-use pricing.
/// </summary>
public sealed class EnergyMonitorSensor : ISensor
{
    private readonly Random _random = new();

    public async Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        var occupancy = rt.Bus.GetOrDefault<OccupancyPattern>();
        var climate = rt.Bus.GetOrDefault<ClimateState>();

        // Simulate power consumption based on various factors
        var baseLoad = 300.0; // Watts (always-on devices)
        var hvacLoad = CalculateHvacLoad(climate);
        var occupancyLoad = occupancy?.IsHome == true ? 800.0 : 200.0;
        var randomVariance = _random.Next(-100, 200);

        var currentWatts = baseLoad + hvacLoad + occupancyLoad + randomVariance;
        var peakDemand = Math.Max(rt.Bus.GetOrDefault<EnergyState>()?.PeakDemandWatts ?? currentWatts, currentWatts);

        // Time-of-use pricing simulation
        var hour = DateTime.Now.Hour;
        var isPeakHours = hour >= 16 && hour <= 21; // 4 PM - 9 PM
        var costPerKwh = isPeakHours ? 0.35 : 0.12;

        var energyState = new EnergyState(
            CurrentWatts: currentWatts,
            PeakDemandWatts: peakDemand,
            CostPerKwh: costPerKwh,
            IsPeakHours: isPeakHours,
            LastUpdated: DateTime.UtcNow
        );

        rt.Bus.Publish(energyState);
        await Task.CompletedTask;
    }

    private double CalculateHvacLoad(ClimateState? climate)
    {
        if (climate == null) return 0;

        // HVAC load depends on temperature difference
        var tempDiff = Math.Abs(climate.IndoorTempCelsius - climate.TargetTempCelsius);
        var outdoorDiff = Math.Abs(climate.OutdoorTempCelsius - climate.TargetTempCelsius);

        // Higher difference = more power needed
        var load = (tempDiff * 200) + (outdoorDiff * 50);
        return Math.Min(load, 3000); // Cap at 3kW
    }
}
