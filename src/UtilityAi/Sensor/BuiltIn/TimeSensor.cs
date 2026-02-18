using UtilityAi.Facts;
using UtilityAi.Utils;

namespace UtilityAi.Sensor.BuiltIn;

/// <summary>
/// Publishes time-related facts every tick.
/// Tracks current time, tick number, and elapsed time since orchestration started.
/// </summary>
public sealed class TimeSensor : ISensor
{
    private DateTimeOffset? _startTime;

    public Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // Initialize start time on first tick
        _startTime ??= now;

        // Publish time facts
        rt.Bus.Publish(new CurrentTime(now));
        rt.Bus.Publish(new TickNumber(rt.Tick));
        rt.Bus.Publish(new ElapsedTime(now - _startTime.Value));

        return Task.CompletedTask;
    }
}
