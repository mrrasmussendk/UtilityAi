using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace Example.TaskManagement.Sensors;

/// <summary>
/// Monitors resource usage and updates available resources based on running tasks.
/// </summary>
public sealed class ResourceMonitorSensor : ISensor
{
    public Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        var resources = rt.Bus.GetOrDefault<SystemResources>();
        if (resources is null) return Task.CompletedTask;

        var queue = rt.Bus.GetOrDefault<TaskQueue>();
        if (queue is null) return Task.CompletedTask;

        // Calculate resources consumed by executing tasks
        var executingTasks = queue.Tasks.Where(t => t.Status == TaskStatus.Executing).ToList();
        var cpuUsed = executingTasks.Sum(t => t.ResourceCost);
        var currentlyRunning = executingTasks.Count;

        // Simulate some resource recovery over time
        var baseAvailableCpu = 100 - cpuUsed;
        var baseAvailableMemory = 1000 - (currentlyRunning * 100);

        // Update resources if they changed
        if (resources.AvailableCpu != baseAvailableCpu || 
            resources.AvailableMemory != baseAvailableMemory)
        {
            rt.Bus.Publish(resources with 
            { 
                AvailableCpu = baseAvailableCpu,
                AvailableMemory = baseAvailableMemory
            });
        }

        return Task.CompletedTask;
    }
}
