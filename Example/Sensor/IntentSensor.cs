using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace Example.Sensor;

public sealed class IntentSensor : ISensor
{
    public Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        string? delivery = null;
        if (rt.Intent.Slots is { } slots && slots.TryGetValue("delivery", out var d))
            delivery = d as string;

        var mode = (delivery ?? "").ToLowerInvariant() switch
        {
            "email" => "email",
            "sms" => "sms",
            "push" => "push",
            "web" => "web",
            _ => "unknown"
        };
        rt.Bus.Publish(new SignalOutputMode(mode));
        return Task.CompletedTask;
    }
}
