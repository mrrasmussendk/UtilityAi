using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace Example.TaskManagement.Sensors;

/// <summary>
/// Initializes the task queue from the user's intent on the first tick.
/// </summary>
public sealed class TaskQueueSensor : ISensor
{
    public Task SenseAsync(Runtime rt, CancellationToken ct)
    {
        // Only initialize on first tick
        if (rt.Tick > 1) return Task.CompletedTask;
        
        if (rt.Bus.GetOrDefault<TaskQueue>() is not null)
            return Task.CompletedTask;

        var taskNames = rt.Intent.Slots?.TryGetValue("tasks", out var t) == true
            ? t as string[] ?? Array.Empty<string>()
            : Array.Empty<string>();

        var tasks = new List<TaskItem>();
        var random = new Random(42); // Deterministic for demo

        for (int i = 0; i < taskNames.Length; i++)
        {
            var name = taskNames[i];
            var priority = (TaskPriority)(random.Next(1, 5));
            var cost = random.Next(10, 50);
            
            // Create some dependencies to make it interesting
            string[]? deps = null;
            if (i > 0 && random.Next(0, 3) == 0)
            {
                deps = new[] { $"task-{random.Next(0, i)}" };
            }

            tasks.Add(new TaskItem(
                Id: $"task-{i}",
                Name: name,
                Priority: priority,
                Status: TaskStatus.Pending,
                ResourceCost: cost,
                SubmittedAt: DateTime.UtcNow.AddSeconds(-random.Next(0, 60)),
                DependsOn: deps
            ));
        }

        rt.Bus.Publish(new TaskQueue(tasks));
        return Task.CompletedTask;
    }
}
