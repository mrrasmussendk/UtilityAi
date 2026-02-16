using Example.TaskManagement.Considerations;
using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace Example.TaskManagement.Modules;

/// <summary>
/// Executes prioritized tasks when resources are available.
/// </summary>
public sealed class ExecutionModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var queue = rt.Bus.GetOrDefault<TaskQueue>();
        var resources = rt.Bus.GetOrDefault<SystemResources>();

        if (queue is null || resources is null) yield break;

        var prioritizedTasks = queue.Tasks.Where(t => t.Status == TaskStatus.Prioritized).ToList();
        var executingCount = queue.Tasks.Count(t => t.Status == TaskStatus.Executing);

        // Don't propose more tasks if at max parallelism
        if (executingCount >= resources.MaxParallelTasks)
            yield break;

        foreach (var task in prioritizedTasks)
        {
            // Check if dependencies are met
            var dependenciesMet = task.DependsOn == null ||
                task.DependsOn.All(depId =>
                    queue.Tasks.Any(t => t.Id == depId && t.Status == TaskStatus.Completed));

            if (!dependenciesMet) continue;

            // Check if we have enough resources
            var hasResources = task.ResourceCost <= resources.AvailableCpu;

            yield return new Proposal(
                id: $"execute.{task.Id}",
                cons: new IConsideration[]
                {
                    // Favor higher priority tasks
                    new FixedValue("priority", Math.Pow((int)task.Priority / 4.0, 2.0)),

                    // Favor tasks that use fewer resources
                    new FixedValue("resource_efficiency", 1.0 - (task.ResourceCost / 100.0)),

                    // Strong preference for tasks with met dependencies
                    new FixedValue("dependencies", dependenciesMet ? 1.0 : 0.0),

                    // Resource availability check
                    new FixedValue("resource_available", hasResources ? 1.0 : 0.1)
                },
                act: async ct =>
                {
                    // Mark as executing
                    var updatedTasks = queue.Tasks.Select(t =>
                        t.Id == task.Id
                            ? t with { Status = TaskStatus.Executing }
                            : t
                    ).ToList();
                    rt.Bus.Publish(new TaskQueue(updatedTasks));
                    rt.Bus.Publish(new TaskExecutionStarted(task.Id, DateTime.UtcNow));

                    Console.WriteLine($"    🔧 Executing: {task.Name} (cost: {task.ResourceCost})");

                    // Simulate work (proportional to resource cost)
                    await Task.Delay(task.ResourceCost * 10, ct);

                    // Mark as completed
                    var completedTasks = updatedTasks.Select(t =>
                        t.Id == task.Id
                            ? t with { Status = TaskStatus.Completed }
                            : t
                    ).ToList();
                    rt.Bus.Publish(new TaskQueue(completedTasks));
                    rt.Bus.Publish(new TaskCompleted(task.Id, DateTime.UtcNow, Success: true));

                    Console.WriteLine($"    ✅ Completed: {task.Name}");
                },
                eligibilities: new IEligibility[]
                {
                    // Must have resources available
                    new CustomEligibility("has-resources", () => hasResources),
                    // Dependencies must be met
                    new CustomEligibility("deps-met", () => dependenciesMet)
                }
            );
        }
    }
}

public sealed class CustomEligibility(string name, Func<bool> predicate) : IEligibility
{
    public string Name => name;
    public bool IsEligible(Runtime rt) => predicate();
}
