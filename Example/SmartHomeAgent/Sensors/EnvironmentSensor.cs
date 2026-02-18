using Example.SmartHomeAgent.Models;
using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace Example.SmartHomeAgent.Sensors;

/// <summary>
/// Simulates reading environmental data from smart home sensors and publishes to EventBus.
/// In a real implementation, this would interface with actual IoT devices via APIs.
/// </summary>
public sealed class EnvironmentSensor : ISensor
{
    private readonly Random _random = new();
    private DateTime _lastWeatherFetch = DateTime.MinValue;

    public async Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        // Simulate reading climate sensors
        var occupancy = rt.Bus.GetOrDefault<OccupancyPattern>();
        var currentClimate = rt.Bus.GetOrDefault<ClimateState>();

        // Simulate gradual temperature change based on HVAC actions
        var indoor = currentClimate?.IndoorTempCelsius ?? 22.0;
        var target = currentClimate?.TargetTempCelsius ?? 21.0;
        var outdoor = currentClimate?.OutdoorTempCelsius ?? 15.0;

        // Temperature drifts toward outdoor temp, HVAC pushes toward target
        var drift = (outdoor - indoor) * 0.05; // Slow drift toward outdoor
        var hvacEffect = (target - indoor) * 0.15; // HVAC effect
        var newIndoor = indoor + drift + hvacEffect + (_random.NextDouble() - 0.5) * 0.3;

        var climate = new ClimateState(
            IndoorTempCelsius: newIndoor,
            OutdoorTempCelsius: outdoor + (_random.NextDouble() - 0.5) * 0.5,
            TargetTempCelsius: target,
            Humidity: 45 + (_random.NextDouble() - 0.5) * 10,
            OccupancyDetected: occupancy?.IsHome ?? false
        );
        rt.Bus.Publish(climate);

        // Fetch weather forecast periodically (every 30 minutes in real system)
        if ((DateTime.UtcNow - _lastWeatherFetch).TotalSeconds > 10) // Simulated: every 10 seconds
        {
            var forecast = new WeatherForecast(
                ForecastTempCelsius: outdoor + _random.Next(-5, 8),
                ChanceOfRain: _random.NextDouble(),
                WindSpeedKph: _random.Next(5, 40),
                Conditions: _random.Next(3) switch
                {
                    0 => "sunny",
                    1 => "cloudy",
                    _ => "rainy"
                },
                ForecastTime: DateTime.UtcNow.AddHours(3)
            );
            rt.Bus.Publish(forecast);
            _lastWeatherFetch = DateTime.UtcNow;
        }

        await Task.CompletedTask;
    }
}
