using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace Example.Sensor;

public class OutputSensor : ISensor
{
    public Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        // Derive output mode from the intent (super simple)
        var mode = rt.Intent.Delivery?.ToLowerInvariant() == "email" ? "email" : "sms";
        rt.Bus.Publish(new SignalOutputMode(mode));
        return Task.CompletedTask;
    }
}