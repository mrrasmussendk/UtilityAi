using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace Example.Sensor;


public sealed class BudgetSlaSensors : ISensor
{
    public Task SenseAsync(IBlackboard bb, CancellationToken ct)
    {
        bb.Set("budget:left", bb.GetOr("budget:left", 0.8));
        bb.Set("sla:tight", bb.GetOr("sla:tight", 0.4));
        return Task.CompletedTask;
    }
}