using Example.SmartHomeAgent.Models;
using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace Example.SmartHomeAgent.Sensors;

/// <summary>
/// Monitors health status of smart home devices and publishes to EventBus.
/// Tracks battery levels, responsiveness, and maintenance schedules.
/// </summary>
public sealed class DeviceHealthSensor : ISensor
{
    private readonly Random _random = new();
    private readonly List<(string id, string type, double battery, DateTime maintenance)> _devices = new()
    {
        ("lock_front", "door_lock", 85, DateTime.UtcNow.AddDays(-120)),
        ("thermostat_main", "thermostat", 100, DateTime.UtcNow.AddDays(-45)),
        ("smoke_kitchen", "smoke_detector", 15, DateTime.UtcNow.AddDays(-200)),
        ("camera_driveway", "camera", 100, DateTime.UtcNow.AddDays(-30)),
        ("sensor_motion_hall", "motion_sensor", 35, DateTime.UtcNow.AddDays(-180))
    };

    public async Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        // Publish health status for each device
        foreach (var device in _devices)
        {
            // Simulate battery drain
            var batteryDrain = _random.NextDouble() * 0.1;
            var currentBattery = Math.Max(0, device.battery - batteryDrain);

            // Simulate occasional device issues
            var isResponsive = _random.NextDouble() > 0.05; // 5% chance of being offline
            var warnings = GenerateWarnings(device.type, currentBattery, device.maintenance, isResponsive);

            var health = new DeviceHealth(
                DeviceId: device.id,
                DeviceType: device.type,
                BatteryLevel: currentBattery,
                IsResponsive: isResponsive,
                LastMaintenance: device.maintenance,
                Warnings: warnings
            );

            rt.Bus.Publish(health);
        }

        await Task.CompletedTask;
    }

    private List<string> GenerateWarnings(string deviceType, double battery, DateTime lastMaintenance, bool responsive)
    {
        var warnings = new List<string>();

        if (battery < 20)
            warnings.Add($"Low battery: {battery:F0}%");

        if (!responsive)
            warnings.Add("Device not responding");

        var daysSinceMaintenance = (DateTime.UtcNow - lastMaintenance).TotalDays;
        if (daysSinceMaintenance > 180)
            warnings.Add($"Maintenance overdue by {daysSinceMaintenance - 180:F0} days");

        // Device-specific warnings
        if (deviceType == "smoke_detector" && daysSinceMaintenance > 365)
            warnings.Add("Annual sensor test required");

        if (deviceType == "camera" && _random.NextDouble() < 0.1)
            warnings.Add("Lens obstruction detected");

        return warnings;
    }
}
