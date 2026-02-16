using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace Example.TaskManagement.Sensors;

/// <summary>
/// Converts the user intent into initial facts on the blackboard.
/// </summary>
public sealed class IntentSensor : ISensor
{
    public Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        // Extract priority mode from intent
        var mode = rt.Intent.Slots?.TryGetValue("priority_mode", out var m) == true 
            ? m as string ?? "balanced" 
            : "balanced";
        
        rt.Bus.Publish(new PriorityMode(mode));

        // Extract max parallel tasks
        var maxParallel = rt.Intent.Slots?.TryGetValue("max_parallel", out var mp) == true
            ? Convert.ToInt32(mp)
            : 2;

        // Initialize system resources
        rt.Bus.Publish(new SystemResources(
            AvailableCpu: 100,
            AvailableMemory: 1000,
            MaxParallelTasks: maxParallel
        ));

        return Task.CompletedTask;
    }
}
