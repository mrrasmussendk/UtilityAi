namespace Example.SmartHomeAgent.Models;

/// <summary>
/// Current energy consumption and cost data.
/// </summary>
public sealed record EnergyState(
    double CurrentWatts,
    double PeakDemandWatts,
    double CostPerKwh,
    bool IsPeakHours,
    DateTime LastUpdated
);

/// <summary>
/// Temperature and comfort metrics for the home.
/// </summary>
public sealed record ClimateState(
    double IndoorTempCelsius,
    double OutdoorTempCelsius,
    double TargetTempCelsius,
    double Humidity,
    bool OccupancyDetected
);

/// <summary>
/// Security system status and alerts.
/// </summary>
public sealed record SecurityState(
    bool AlarmArmed,
    List<string> OpenDoors,
    List<string> OpenWindows,
    bool MotionDetected,
    DateTime? LastSecurityCheck
);

/// <summary>
/// Device status and health metrics.
/// </summary>
public sealed record DeviceHealth(
    string DeviceId,
    string DeviceType,
    double BatteryLevel,
    bool IsResponsive,
    DateTime LastMaintenance,
    List<string> Warnings
);

/// <summary>
/// Home occupancy pattern data.
/// </summary>
public sealed record OccupancyPattern(
    bool IsHome,
    int OccupantCount,
    string CurrentMode, // "home", "away", "sleep", "vacation"
    DateTime ModeChangedAt
);

/// <summary>
/// Weather forecast data for decision making.
/// </summary>
public sealed record WeatherForecast(
    double ForecastTempCelsius,
    double ChanceOfRain,
    double WindSpeedKph,
    string Conditions,
    DateTime ForecastTime
);

/// <summary>
/// Action taken by the home automation system.
/// </summary>
public sealed record HomeAction(
    string ActionType,
    string Target,
    Dictionary<string, object> Parameters,
    string Reason,
    DateTime ExecutedAt
);

/// <summary>
/// Energy optimization recommendation.
/// </summary>
public sealed record EnergyRecommendation(
    string RecommendationType,
    double EstimatedSavingsKwh,
    double EstimatedCostSavings,
    string Description
);

/// <summary>
/// Alert or notification to be sent to homeowner.
/// </summary>
public sealed record HomeNotification(
    string Priority, // "low", "medium", "high", "critical"
    string Category,
    string Message,
    DateTime CreatedAt
);
