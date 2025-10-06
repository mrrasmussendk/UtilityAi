using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace Example.Sensor;

public sealed class UncertaintySensor : ISensor
{
    public Task SenseAsync(IBlackboard bb, CancellationToken ct)
    {
        // If we already have text answer, uncertainty drops; else higher
        var hasText = bb.Has("answer:text");
        var hasResults = bb.Has("search:results");
        double u = hasText ? 0.2 : hasResults ? 0.45 : 0.7;
        bb.Set("signal:uncertainty", u);
        return Task.CompletedTask;
    }
}