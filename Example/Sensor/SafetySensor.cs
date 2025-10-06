using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace Example.Sensor;


public sealed class SafetySensor : ISensor
{
    public Task SenseAsync(IBlackboard bb, CancellationToken ct)
    {
        // Stub: low risk for tech news
        bb.Set("risk:safety", 0.1);
        return Task.CompletedTask;
    }
}