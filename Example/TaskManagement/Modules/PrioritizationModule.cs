using Example.TaskManagement.Considerations;
using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace Example.TaskManagement.Modules;

/// <summary>
/// Calculates priority scores for validated tasks.
/// </summary>
public sealed class PrioritizationModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var queue = rt.Bus.GetOrDefault<TaskQueue>();
        if (queue is null) yield break;

        var mode = rt.Bus.GetOrDefault<PriorityMode>();
        var validatedTasks = queue.Tasks.Where(t => t.Status == TaskStatus.Validated).ToList();

        foreach (var task in validatedTasks)
        {
            var modeValue = mode?.Mode switch
            {
                "urgent" => task.Priority >= TaskPriority.High ? 1.0 : 0.3,
                "efficiency" => 1.0 - (task.ResourceCost / 100.0),
                _ => 0.7 // balanced
            };

            yield return new Proposal(
                id: $"prioritize.{task.Id}",
                cons: new IConsideration[]
                {
                    // Base priority from task metadata
                    new FixedValue("base_priority", (int)task.Priority / 4.0),

                    // Mode-specific adjustments
                    new FixedValue("mode_factor", modeValue)
                },
                act: async ct =>
                {
                    await Task.Delay(30, ct);

                    // Calculate urgency score
                    var urgencyScore = CalculateUrgency(task, mode?.Mode ?? "balanced");

                    var updatedTasks = queue.Tasks.Select(t =>
                        t.Id == task.Id
                            ? t with { Status = TaskStatus.Prioritized }
                            : t
                    ).ToList();

                    rt.Bus.Publish(new TaskQueue(updatedTasks));
                    rt.Bus.Publish(new TaskPrioritized(task.Id, urgencyScore));

                    Console.WriteLine($"    ⚖️  Prioritized task: {task.Name} (score: {urgencyScore:F2})");
                }
            );
        }
    }

    private static double CalculateUrgency(TaskItem task, string mode)
    {
        var basePriority = (int)task.Priority * 0.25;
        var ageFactor = Math.Min(1.0, (DateTime.UtcNow - task.SubmittedAt).TotalSeconds / 120.0);
        var resourceFactor = 1.0 - (task.ResourceCost / 100.0);

        return mode switch
        {
            "urgent" => basePriority * 0.7 + ageFactor * 0.3,
            "efficiency" => resourceFactor * 0.6 + basePriority * 0.4,
            _ => (basePriority + ageFactor + resourceFactor) / 3.0
        };
    }
}
