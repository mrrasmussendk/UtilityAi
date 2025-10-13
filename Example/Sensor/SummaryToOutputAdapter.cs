using Example.OutputModule.DTO;
using Example.SearchAndSummerizeModule.DTO;
using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace Example.Sensor;

public class SummaryToOutputAdapter : ISensor
{
    public Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        // Publish OutputTextMessage once when a Summary exists
        if (rt.Bus.GetOrDefault<OutputTextMessage>() is null &&
            rt.Bus.GetOrDefault<Summary>() is { } sum)
        {
            rt.Bus.Publish(new OutputTextMessage(sum.Text));
        }

        return Task.CompletedTask;
    }
}