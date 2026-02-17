using Example.SmartHomeAgent.Models;
using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace Example.SmartHomeAgent.Sensors;

/// <summary>
/// Monitors security system status and publishes to EventBus.
/// Tracks door/window sensors, motion detectors, and alarm state.
/// </summary>
public sealed class SecuritySensor : ISensor
{
    private readonly Random _random = new();

    public async Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        var occupancy = rt.Bus.GetOrDefault<OccupancyPattern>();
        var previousSecurity = rt.Bus.GetOrDefault<SecurityState>();

        // Check if security system should be armed based on actions
        var recentActions = rt.Bus.GetHistory<HomeAction>(maxItems: 5);
        var armAction = recentActions.FirstOrDefault(a => a.Value.ActionType == "arm_security");
        var isArmed = armAction != null || previousSecurity?.AlarmArmed == true;

        // If away mode but not armed, keep checking
        if (occupancy?.CurrentMode == "away" || occupancy?.CurrentMode == "vacation")
        {
            // Will be armed by SecurityMonitoringModule
        }
        else if (occupancy?.CurrentMode == "home")
        {
            isArmed = false; // Disarm when home
        }

        // Simulate door/window status
        var openDoors = new List<string>();
        var openWindows = new List<string>();

        if (occupancy?.CurrentMode == "home")
        {
            // Random chance of doors/windows being open when home
            if (_random.NextDouble() < 0.2) openDoors.Add("front_door");
            if (_random.NextDouble() < 0.3) openWindows.Add("kitchen_window");
        }
        else if (occupancy?.CurrentMode == "sleep")
        {
            // Rarely open during sleep
            if (_random.NextDouble() < 0.05) openWindows.Add("bedroom_window");
        }
        // Away/vacation mode: should be closed (but sometimes forgotten)
        else
        {
            if (_random.NextDouble() < 0.05) openDoors.Add("garage_door");
        }

        // Motion detection
        var motionDetected = false;
        if (isArmed && _random.NextDouble() < 0.02) // 2% chance of motion when armed
        {
            motionDetected = true; // Could be intruder or false alarm
        }

        var securityState = new SecurityState(
            AlarmArmed: isArmed,
            OpenDoors: openDoors,
            OpenWindows: openWindows,
            MotionDetected: motionDetected,
            LastSecurityCheck: previousSecurity?.LastSecurityCheck
        );

        // Update last check time if a security check was performed
        var checkAction = recentActions.FirstOrDefault(a => a.Value.ActionType == "security_check");
        if (checkAction != null && checkAction.Timestamp > (previousSecurity?.LastSecurityCheck ?? DateTime.MinValue))
        {
            securityState = securityState with { LastSecurityCheck = DateTime.UtcNow };
        }

        rt.Bus.Publish(securityState);
        await Task.CompletedTask;
    }
}
