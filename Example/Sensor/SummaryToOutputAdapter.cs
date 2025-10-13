using Example.OutputModule.DTO;
using Example.SearchAndSummerizeModule.DTO;
using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace Example.Sensor;

public class SummaryToOutputAdapter : ISensor
{
    public Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        if (rt.Bus.GetOrDefault<Summary>() is not null && rt.Bus.GetOrDefault<Summary>() is { } sum)
        {
            rt.Bus.Publish(new OutputTextMessage(sum.Text));
        }

        return Task.CompletedTask;
    }
}