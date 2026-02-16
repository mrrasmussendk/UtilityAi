using Example.TaskManagement.Considerations;
using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace Example.TaskManagement.Modules;

/// <summary>
/// Manages resource allocation and can throttle or boost capacity.
/// </summary>
public sealed class ResourceManagementModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var resources = rt.Bus.GetOrDefault<SystemResources>();
        var queue = rt.Bus.GetOrDefault<TaskQueue>();

        if (resources is null || queue is null) yield break;

        var executingCount = queue.Tasks.Count(t => t.Status == TaskStatus.Executing);
        var pendingCount = queue.Tasks.Count(t =>
            t.Status == TaskStatus.Pending ||
            t.Status == TaskStatus.Validated ||
            t.Status == TaskStatus.Prioritized);

        // Propose boosting capacity if we have many pending high-priority tasks
        var highPriorityPending = queue.Tasks.Count(t =>
            t.Priority >= TaskPriority.High &&
            t.Status != TaskStatus.Completed &&
            t.Status != TaskStatus.Executing);

        if (highPriorityPending > 0 && resources.MaxParallelTasks < 5)
        {
            yield return new Proposal(
                id: "resource.boost_capacity",
                cons: new IConsideration[]
                {
                    new FixedValue("high_priority_backlog",
                        Math.Pow(Math.Min(1.0, highPriorityPending / 3.0), 2.0)),

                    new FixedValue("current_capacity",
                        1.0 - (resources.MaxParallelTasks / 5.0))
                },
                act: async ct =>
                {
                    await Task.Delay(20, ct);

                    var newCapacity = Math.Min(5, resources.MaxParallelTasks + 1);
                    rt.Bus.Publish(resources with { MaxParallelTasks = newCapacity });

                    Console.WriteLine($"    📈 Boosted capacity: {resources.MaxParallelTasks} → {newCapacity}");
                }
            );
        }

        // Propose throttling if no pending work and we're over-provisioned
        if (pendingCount == 0 && executingCount < resources.MaxParallelTasks - 1 && resources.MaxParallelTasks > 2)
        {
            yield return new Proposal(
                id: "resource.reduce_capacity",
                cons: new IConsideration[]
                {
                    new FixedValue("idle_capacity",
                        (resources.MaxParallelTasks - executingCount) / (double)resources.MaxParallelTasks),

                    new FixedValue("no_pending", pendingCount == 0 ? 1.0 : 0.0)
                },
                act: async ct =>
                {
                    await Task.Delay(20, ct);

                    var newCapacity = Math.Max(2, resources.MaxParallelTasks - 1);
                    rt.Bus.Publish(resources with { MaxParallelTasks = newCapacity });

                    Console.WriteLine($"    📉 Reduced capacity: {resources.MaxParallelTasks} → {newCapacity}");
                }
            );
        }
    }
}
