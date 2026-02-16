using UtilityAi.Utils;

namespace UtilityAi.Consideration.General;

/// <summary>
/// Returns 1.0 when current time falls within a specified time window (e.g., business hours),
/// 0.0 otherwise. Useful for time-based scheduling and prioritization.
/// </summary>
public sealed class TimeWindow : IConsideration
{
    private readonly TimeOnly _startTime;
    private readonly TimeOnly _endTime;
    private readonly DayOfWeek[]? _allowedDays;

    /// <summary>
    /// Creates a time window consideration.
    /// </summary>
    /// <param name="startTime">Start of the time window (inclusive).</param>
    /// <param name="endTime">End of the time window (exclusive).</param>
    /// <param name="allowedDays">Optional array of allowed days. If null, all days are allowed.</param>
    public TimeWindow(TimeOnly startTime, TimeOnly endTime, DayOfWeek[]? allowedDays = null)
    {
        _startTime = startTime;
        _endTime = endTime;
        _allowedDays = allowedDays;
    }

    public string Name => $"TimeWindow({_startTime} to {_endTime})";

    public double Evaluate(Runtime rt)
    {
        var now = DateTimeOffset.UtcNow;
        var currentTime = TimeOnly.FromDateTime(now.DateTime);

        // Check day of week if specified
        if (_allowedDays != null && !_allowedDays.Contains(now.DayOfWeek))
            return 0.0;

        // Handle time window that crosses midnight
        var inWindow = _startTime <= _endTime
            ? (currentTime >= _startTime && currentTime < _endTime)
            : (currentTime >= _startTime || currentTime < _endTime);

        return inWindow ? 1.0 : 0.0;
    }
}
