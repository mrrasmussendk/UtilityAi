using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace Example.Sensor;

public sealed class RecencySensor : ISensor
{
    public Task SenseAsync(IBlackboard bb, CancellationToken ct)
    {
        var q = (bb.GetOr("prompt", "") ?? "").ToLowerInvariant();
        var hits = new[] { "latest", "today", "breaking", "now", "this week" }.Count(q.Contains);
        bb.Set("signal:recency", Math.Clamp(hits / 5.0, 0, 1));
        return Task.CompletedTask;
    }
}